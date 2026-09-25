using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

// The bottom HUD, in the shape of the Dark Souls games' with BG3's readouts: the equipment
// cross floating off its left end, then the character's face in a ring, their endurance
// bar, defence dice and tokens, the raised weapon's attack options as the rows of its
// printed card, the party's souls, and End Turn.
//
// It has three states and never leaves the layout, because its height is part of what
// BoardCamera fits the board around:
//   Idle       – between activations, dimmed, nothing to press.
//   Activation – the active character; clicking a hand slot raises that weapon and its
//                options fill the rows. Hovering an option ghosts its cost onto the bar.
//   Reaction   – an enemy is attacking someone: the defender takes the bar over, the armour
//                slot lights, the header shows the attack and the rows become Block and
//                Dodge with what each would do. Awaited by DodgePrompt.
//
// Rebuilt on every change rather than diffed — a stale button here would let a player take
// an illegal turn.
public partial class CharacterActionBar : HBoxContainer
{

	[Signal] public delegate void AttackChosenEventHandler(Weapon weapon, PlayerMove move);
	[Signal] public delegate void EndActivationPressedEventHandler();
	[Signal] public delegate void ReactionResolvedEventHandler(bool dodge);
	[Signal] public delegate void DodgeStepEndedEventHandler();

	[Export] public Control crossAnchor;
	[Export] public EquipmentCross cross;
	[Export] public PanelContainer panel;
	[Export] public CircleAvatar avatar;
	[Export] public Label nameLabel;
	[Export] public TickBar endurance;
	[Export] public Container defenceRow;
	[Export] public Container tokenRow;
	[Export] public Container header;
	[Export] public Container rows;
	[Export] public Label soulsLabel;
	[Export] public Button endTurnButton;

	[Export] public StyleBox panelStyle;
	[Export] public StyleBox panelReactionStyle;

	[Export] public Texture2D physicalIcon;
	[Export] public Texture2D magicIcon;
	[Export] public Texture2D blockIcon;
	[Export] public Texture2D resistIcon;
	[Export] public Texture2D dodgeIcon;

	// Ordered to match EncounterManager.StatusEffect: BLEED, POISON, FROST, STAGGER.
	[Export] public Array<Texture2D> conditionTextures;
	// Estus Flask, Heroic Action, Luck. Display only until those systems exist.
	[Export] public Array<Texture2D> tokenTextures;

	[Export] public Color gold = new Color(0.788f, 0.635f, 0.294f);
	[Export] public Color parchment = new Color(0.902f, 0.871f, 0.796f);
	[Export] public Color muted = new Color(0.55f, 0.51f, 0.45f);
	[Export] public Color blockColour = new Color(0.4f, 0.62f, 0.96f);
	[Export] public Color resistColour = new Color(0.62f, 0.5f, 0.93f);
	[Export] public Color dodgeColour = new Color(0.45f, 0.82f, 0.42f);
	[Export] public Color attackColour = new Color(0.851f, 0.18f, 0.122f);

	[Export] public float crossMargin = 12f;
	[Export] public float crossBottomMargin = 6f;
	[Export] public float dimShade = 0.55f;

	private enum Mode { IDLE, ACTIVATION, REACTION, DODGE_STEP }

	private Mode mode = Mode.IDLE;
	private PlayerToken token;
	private Weapon raised;
	private Player shown;
	private int shownSpent = -1;
	private int shownDamage = -1;

	private static StyleBoxFlat rowNormal, rowHover, rowArmed, rowDisabled;

	public override void _Ready() {
		BuildRowStyles();

		if (endTurnButton != null) endTurnButton.Pressed += OnEndPressed;
		if (cross != null) cross.HandPressed += OnHandPressed;
		if (crossAnchor != null) {
			crossAnchor.Resized += PlaceCross;
			CallDeferred(nameof(PlaceCross));
		}
		ShowIdle();
	}

	// The cross hangs off the anchor's bottom-left, taller than the bar, over the board.
	private void PlaceCross() {
		if (cross == null || crossAnchor == null) return;
		Vector2 size = cross.clusterSize;
		crossAnchor.CustomMinimumSize = new Vector2(size.X + crossMargin * 2f, 0f);
		cross.Position = new Vector2(crossMargin, crossAnchor.Size.Y - size.Y - crossBottomMargin);
	}

	// Endurance changes from several places (steps, attacks, damage landing after a
	// reaction), so the bar and face poll like the portraits do.
	public override void _Process(double delta) {
		if (shown == null) return;
		Endurance bar = shown.endurance;
		if (bar.staminaSpent == shownSpent && bar.damageTaken == shownDamage) return;
		shownSpent = bar.staminaSpent;
		shownDamage = bar.damageTaken;
		endurance?.ShowEndurance(bar);
		avatar?.SetDamage((float)bar.damageTaken / Endurance.BOXES);
	}

	// ----- States -----

	public void ShowIdle() {
		mode = Mode.IDLE;
		token = null;
		raised = null;

		ClearChildren(header);
		ClearChildren(rows);
		endurance?.ClearGhost();
		cross?.SetRaised(null);
		cross?.SetDefending(false);
		if (shown == null && nameLabel != null) nameLabel.Text = "";
		if (endTurnButton != null) {
			endTurnButton.Text = "End Turn";
			endTurnButton.Disabled = true;
		}
		SetPanelStyle(false);
		Dim(true);
		RefreshSouls();
	}

	public void Refresh(PlayerToken active) {
		if (active == null) {
			ShowIdle();
			return;
		}
		mode = Mode.ACTIVATION;
		token = active;

		ShowCharacter(active.player, true);
		cross?.Show(active.player);
		foreach (Weapon weapon in HandWeapons(active.player)) cross?.SetSpent(weapon, active.HasAttackedWith(weapon));

		if (raised == null || !Holds(active.player, raised) || active.HasAttackedWith(raised)) raised = FirstUsable(active);
		cross?.SetRaised(raised);
		cross?.SetDefending(false);

		BuildHeader(raised);
		BuildRows(active, raised);

		if (endTurnButton != null) {
			endTurnButton.Text = "End Turn";
			endTurnButton.Disabled = false;
		}
		SetPanelStyle(false);
		Dim(false);
		RefreshSouls();
	}

	// A successful dodge lets the character move one node (p22): the bar shows the dodger
	// with an End Dodge button while CharacterTurn lights the nodes they may step to.
	public void ShowDodgeStep(PlayerToken dodger) {
		if (dodger == null) return;
		mode = Mode.DODGE_STEP;
		token = null;

		ShowCharacter(dodger.player, false);
		cross?.Show(dodger.player);
		cross?.SetRaised(null);
		cross?.SetDefending(false);
		ClearChildren(header);
		ClearChildren(rows);
		if (header != null) header.AddChild(Icon(dodgeIcon, dodgeColour, 40f));

		if (endTurnButton != null) {
			endTurnButton.Text = "End Dodge";
			endTurnButton.Disabled = false;
		}
		SetPanelStyle(false);
		Dim(false);
		RefreshSouls();
	}

	private void OnEndPressed() {
		if (mode == Mode.DODGE_STEP) EmitSignal(SignalName.DodgeStepEnded);
		else EmitSignal(SignalName.EndActivationPressed);
	}

	private void OnHandPressed(int hand) {
		if (mode != Mode.ACTIVATION || token == null || cross == null) return;
		Weapon weapon = cross.WeaponIn(hand);
		if (weapon == null || token.HasAttackedWith(weapon)) return;

		raised = weapon;
		cross.SetRaised(raised);
		BuildHeader(raised);
		BuildRows(token, raised);
	}

	private void ShowCharacter(Player player, bool tokensReady) {
		shown = player;
		shownSpent = shownDamage = -1;
		avatar?.Show(player);
		if (nameLabel != null) nameLabel.Text = player?.name ?? "";
		if (player != null) endurance?.ShowEndurance(player.endurance);
		BuildDefence(player);
		BuildTokens(tokensReady);
	}

	// ----- Character block -----

	private void BuildDefence(Player player) {
		ClearChildren(defenceRow);
		if (defenceRow == null || player == null) return;

		AddIcon(defenceRow, blockIcon, blockColour, 22f);
		AddPool(defenceRow, DefencePool(player, false), DefenceModifier(player, false), 18f);
		defenceRow.AddChild(Spacer(10f));
		AddIcon(defenceRow, resistIcon, resistColour, 22f);
		AddPool(defenceRow, DefencePool(player, true), DefenceModifier(player, true), 18f);
	}

	private void BuildTokens(bool ready) {
		ClearChildren(tokenRow);
		if (tokenRow == null || tokenTextures == null) return;

		string[] names = { "Estus Flask", "Heroic Action", "Luck" };
		for (int i = 0; i < tokenTextures.Count; i++) {
			TextureRect token = Icon(tokenTextures[i], ready ? Colors.White : new Color(0.45f, 0.45f, 0.45f), 30f);
			token.TooltipText = i < names.Length ? names[i] : "";
			token.MouseFilter = MouseFilterEnum.Pass;
			tokenRow.AddChild(token);
		}
	}

	// Block or Resist gathered from every equipped piece, matching CombatResolver.RollDefence.
	public static List<Dice> DefencePool(Player player, bool magic) {
		List<Dice> pool = new List<Dice>();
		void Add(Array<Dice> dice) {
			if (dice == null) return;
			foreach (Dice die in dice) if (die != null) pool.Add(die);
		}
		Armour armour = player.GetArmour();
		Add(magic ? armour?.magicDefense : armour?.physicalDefense);
		foreach (Weapon weapon in new[] { player.GetLeftHand(), player.GetRightHand(), player.GetBackupSlot() }) {
			Add(magic ? weapon?.magicDefense : weapon?.physicalDefense);
		}
		return pool;
	}

	public static int DefenceModifier(Player player, bool magic) {
		Armour armour = player.GetArmour();
		return magic ? armour?.magicDefenseModifier ?? 0 : armour?.physicalDefenseModifier ?? 0;
	}

	// ----- Activation: header and attack rows -----

	private void BuildHeader(Weapon weapon) {
		ClearChildren(header);
		if (header == null || weapon == null) return;

		TextureRect art = Icon(EquipmentSlot.CropOf(weapon, cross?.left?.RegionFor(weapon)), Colors.White, 40f);
		art.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
		header.AddChild(art);
		header.AddChild(Text(weapon.name, 18, parchment));
		header.AddChild(Badge(weapon.attackRange.ToString(), gold));
	}

	private void BuildRows(PlayerToken active, Weapon weapon) {
		ClearChildren(rows);
		if (rows == null || weapon?.attacks == null) return;

		bool used = active.HasAttackedWith(weapon);
		foreach (PlayerMove move in weapon.attacks) {
			if (move == null) continue;
			rows.AddChild(AttackRow(active, weapon, move, used));
		}
	}

	private Button AttackRow(PlayerToken active, Weapon weapon, PlayerMove move, bool weaponUsed) {
		int cost = CombatResolver.AttackStaminaCost(active, move);
		bool armed = EncounterManager.characterTurn?.armed == move;

		Button row = Row(armed);
		row.Disabled = weaponUsed || !active.CanSpend(cost);
		row.TooltipText = Describe(weapon, move, cost);

		HBoxContainer content = RowContent(row);
		content.AddChild(Text($"[{cost}]", 16, armed ? parchment : muted, 40f));
		content.AddChild(CubeRow.Stamina(cost));
		content.AddChild(Spacer(6f));
		if (move.damage != null) {
			foreach (DiceChip chip in DiceChip.ForPool(move.damage, 22f)) content.AddChild(chip);
		}
		if (move.modifier != 0 || move.damage == null || move.damage.Count == 0) {
			content.AddChild(Text($"{move.modifier:+#;-#;+0}", 16, parchment));
		}
		if (move.isMagic) content.AddChild(Icon(magicIcon, resistColour, 18f));
		AddCondition(content, move.statusEffect, 18f);
		if (move.attackRange > 0 && move.attackRange != weapon.attackRange) {
			content.AddChild(Badge(move.attackRange.ToString(), gold));
		}
		if (row.Disabled) content.Modulate = new Color(0.5f, 0.5f, 0.5f, 1f);

		// Capture the loop values before the lambda, or every row fires the last option.
		Weapon capturedWeapon = weapon;
		PlayerMove capturedMove = move;
		row.Pressed += () => EmitSignal(SignalName.AttackChosen, capturedWeapon, capturedMove);
		row.MouseEntered += () => { if (!row.Disabled) endurance?.SetGhost(cost, 0, 0); };
		row.MouseExited += () => endurance?.ClearGhost();
		return row;
	}

	private static string Describe(Weapon weapon, PlayerMove move, int cost) {
		int range = move.attackRange > 0 ? move.attackRange : weapon.attackRange;
		List<string> notes = new List<string> { $"Range {range}", $"{cost} stamina" };

		if (move.isMagic) notes.Add("magical");
		if (move.isAOE) notes.Add("whole node");
		if (move.isNotZeroRange) notes.Add("cannot hit range 0");
		if (move.isIgnoreDefense) notes.Add("ignores Block");
		if (move.repeat > 1) notes.Add($"repeats x{move.repeat} (not implemented)");
		if (move.bonusMovement > 0) notes.Add($"shift {move.bonusMovement} (not implemented)");

		return string.Join("  ·  ", notes);
	}

	// ----- Reaction: Block or Dodge -----

	// Takes the bar over for the defender until Block or Dodge is pressed, then hands it
	// back to idle; the enemy's activation is suspended on the awaited result (p25).
	public async Task<bool> AskReaction(Enemy attacker, EnemyMove move, PlayerToken target) {
		mode = Mode.REACTION;
		token = null;

		ShowCharacter(target.player, false);
		cross?.Show(target.player);
		cross?.SetRaised(null);
		cross?.SetDefending(true);
		BuildReactionHeader(attacker, move);
		BuildReactionRows(attacker, move, target);

		if (endTurnButton != null) endTurnButton.Disabled = true;
		SetPanelStyle(true);
		Dim(false);
		RefreshSouls();

		Variant[] result = await ToSignal(this, SignalName.ReactionResolved);

		endurance?.ClearGhost();
		ShowIdle();
		return result.Length > 0 && result[0].AsBool();
	}

	private void BuildReactionHeader(Enemy attacker, EnemyMove move) {
		ClearChildren(header);
		if (header == null) return;

		// The attacker's face with the red rim the activation bar gives it. No taller than
		// the weapon art it replaces, so the bar keeps its height and the board stays put.
		Panel face = new Panel { CustomMinimumSize = new Vector2(30, 40), ClipChildren = ClipChildrenMode.Only, MouseFilter = MouseFilterEnum.Ignore };
		face.AddThemeStyleboxOverride("panel", Frame(attackColour, 2));
		TextureRect portrait = Icon(attacker?.data?.GetPortrait(), Colors.White, 40f);
		portrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
		portrait.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		portrait.OffsetLeft = portrait.OffsetTop = 2;
		portrait.OffsetRight = portrait.OffsetBottom = -2;
		face.AddChild(portrait);
		header.AddChild(face);

		int strength = CombatResolver.AttackStrength(move, attacker);
		header.AddChild(AttackBadge(move.isMagic, strength));
		header.AddChild(CubeRow.Damage(strength, 12f));
		AddCondition(header, move.statusEffect, 22f);
	}

	// The rulebook's attack icon with the strength inside it. The physical icon's number
	// sits in the shield, a little below centre; the magic icon's sits dead centre.
	private Control AttackBadge(bool magic, int strength) {
		Control badge = new Control { CustomMinimumSize = new Vector2(40, 40), MouseFilter = MouseFilterEnum.Ignore };
		TextureRect icon = Icon(magic ? magicIcon : physicalIcon, gold, 40f);
		icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		badge.AddChild(icon);

		Label value = Text(strength.ToString(), 15, parchment);
		value.HorizontalAlignment = HorizontalAlignment.Center;
		value.AddThemeColorOverride("font_outline_color", Colors.Black);
		value.AddThemeConstantOverride("outline_size", 5);
		value.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		float drop = magic ? 0f : 0.08f;
		value.AnchorTop = drop;
		value.AnchorBottom = 1f + drop;
		badge.AddChild(value);
		return badge;
	}

	private void BuildReactionRows(Enemy attacker, EnemyMove move, PlayerToken target) {
		ClearChildren(rows);
		if (rows == null) return;

		int strength = CombatResolver.AttackStrength(move, attacker);
		Player player = target.player;

		// Block: the gear that rolls, its dice, and the damage that could still get through.
		List<Dice> pool = DefencePool(player, move.isMagic);
		int modifier = DefenceModifier(player, move.isMagic);
		(int best, int worst, int expected) = DamageThrough(strength, pool, modifier);

		Button block = Row(true);
		block.TooltipText = move.isMagic ? "Resist" : "Block";
		HBoxContainer content = RowContent(block);
		content.AddChild(Icon(move.isMagic ? resistIcon : blockIcon, move.isMagic ? resistColour : blockColour, 26f));
		foreach (Equipment gear in DefendingGear(player, move.isMagic)) {
			TextureRect art = Icon(EquipmentSlot.CropOf(gear, cross?.left?.RegionFor(gear)), Colors.White, 26f);
			art.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
			content.AddChild(art);
		}
		content.AddChild(Spacer(4f));
		AddPool(content, pool, modifier, 22f);
		content.AddChild(Spacer(8f));
		content.AddChild(CubeRow.Damage(1, 10f));
		content.AddChild(Text(best == worst ? $"{worst}" : $"{best}-{worst}", 16, parchment));
		block.Pressed += () => EmitSignal(SignalName.ReactionResolved, false);
		block.MouseEntered += () => endurance?.SetGhost(0, expected, worst);
		block.MouseExited += () => endurance?.ClearGhost();
		rows.AddChild(block);

		// Dodge: the pool against the difficulty, the stamina, the odds. Greyed when it
		// cannot succeed or cannot be paid for — paying to fail is not a choice.
		int dodgePool = player.GetDodge();
		int cost = CombatResolver.DodgeStaminaCost(target);
		float chance = DodgeChance(dodgePool, move.dodgeDifficulty);
		bool possible = dodgePool >= move.dodgeDifficulty && target.CanSpend(cost);

		Button dodge = Row(false);
		dodge.Disabled = !possible;
		dodge.TooltipText = "Dodge";
		HBoxContainer dodgeContent = RowContent(dodge);
		dodgeContent.AddChild(Icon(dodgeIcon, possible ? dodgeColour : muted, 26f));
		dodgeContent.AddChild(DiceChip.Create(DiceUtility.DICE_TYPE.DODGE, dodgePool, 22f));
		dodgeContent.AddChild(Badge(move.dodgeDifficulty.ToString(), possible ? gold : muted));
		dodgeContent.AddChild(Spacer(4f));
		dodgeContent.AddChild(CubeRow.Stamina(cost));
		dodgeContent.AddChild(Spacer(8f));
		dodgeContent.AddChild(Text($"{Mathf.RoundToInt(chance * 100f)}%", 16, possible ? parchment : muted));
		if (!possible) dodgeContent.Modulate = new Color(0.5f, 0.5f, 0.5f, 1f);
		dodge.Pressed += () => EmitSignal(SignalName.ReactionResolved, true);
		dodge.MouseEntered += () => { if (possible) endurance?.SetGhost(cost, Mathf.RoundToInt(strength * (1f - chance)), strength); };
		dodge.MouseExited += () => endurance?.ClearGhost();
		rows.AddChild(dodge);

		block.GrabFocus();
	}

	// Every equipped piece contributing dice to this kind of defence, armour first.
	private static List<Equipment> DefendingGear(Player player, bool magic) {
		List<Equipment> gear = new List<Equipment>();
		Armour armour = player.GetArmour();
		if (armour != null && HasDice(magic ? armour.magicDefense : armour.physicalDefense)) gear.Add(armour);
		foreach (Weapon weapon in new[] { player.GetLeftHand(), player.GetRightHand(), player.GetBackupSlot() }) {
			if (weapon != null && HasDice(magic ? weapon.magicDefense : weapon.physicalDefense) && !gear.Contains(weapon)) gear.Add(weapon);
		}
		return gear;
	}

	private static bool HasDice(Array<Dice> dice) => dice != null && dice.Count > 0;

	// Damage that lands after the defence roll: with the best roll, the worst, and on average.
	private static (int best, int worst, int expected) DamageThrough(int strength, List<Dice> pool, int modifier) {
		int min = modifier, max = modifier;
		double avg = modifier;
		foreach (Dice die in pool) {
			if (die?.dice == null || die.dice.Length == 0) continue;
			int dieMin = int.MaxValue, dieMax = int.MinValue, sum = 0;
			foreach (int face in die.dice) {
				dieMin = Mathf.Min(dieMin, face);
				dieMax = Mathf.Max(dieMax, face);
				sum += face;
			}
			min += dieMin;
			max += dieMax;
			avg += (double)sum / die.dice.Length;
		}
		int best = Mathf.Max(0, strength - max);
		int worst = Mathf.Max(0, strength - min);
		int expected = Mathf.Clamp(Mathf.RoundToInt((float)(strength - avg)), best, worst);
		return (best, worst, expected);
	}

	// Each dodge die is a coin flip (p25): the chance of at least `difficulty` icons.
	public static float DodgeChance(int pool, int difficulty) {
		if (difficulty <= 0) return 1f;
		if (pool < difficulty) return 0f;

		double successes = 0;
		for (int k = difficulty; k <= pool; k++) successes += Choose(pool, k);
		return (float)(successes / System.Math.Pow(2, pool));
	}

	private static double Choose(int n, int k) {
		double result = 1;
		for (int i = 1; i <= k; i++) result = result * (n - k + i) / i;
		return result;
	}

	// ----- Pieces -----

	private static void BuildRowStyles() {
		if (rowNormal != null) return;
		rowNormal = RowStyle(new Color(0.078f, 0.07f, 0.055f, 0.9f), new Color(0.29f, 0.251f, 0.188f), 1);
		rowHover = RowStyle(new Color(0.11f, 0.098f, 0.075f, 0.95f), new Color(0.55f, 0.45f, 0.25f), 1);
		rowArmed = RowStyle(new Color(0.13f, 0.115f, 0.085f, 0.95f), new Color(0.788f, 0.635f, 0.294f), 2);
		rowDisabled = RowStyle(new Color(0.06f, 0.055f, 0.045f, 0.7f), new Color(0.18f, 0.157f, 0.125f), 1);
	}

	private static StyleBoxFlat RowStyle(Color bg, Color border, int width) {
		StyleBoxFlat style = new StyleBoxFlat { BgColor = bg, BorderColor = border };
		style.SetBorderWidthAll(width);
		style.SetContentMarginAll(0);
		return style;
	}

	private static StyleBoxFlat Frame(Color border, int width) {
		StyleBoxFlat style = new StyleBoxFlat { BgColor = new Color(0.106f, 0.094f, 0.075f), BorderColor = border };
		style.SetBorderWidthAll(width);
		return style;
	}

	private Button Row(bool armed) {
		Button row = new Button {
			CustomMinimumSize = new Vector2(0, 36),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		row.AddThemeStyleboxOverride("normal", armed ? rowArmed : rowNormal);
		row.AddThemeStyleboxOverride("hover", armed ? rowArmed : rowHover);
		row.AddThemeStyleboxOverride("pressed", rowArmed);
		row.AddThemeStyleboxOverride("focus", armed ? rowArmed : rowHover);
		row.AddThemeStyleboxOverride("disabled", rowDisabled);
		return row;
	}

	// The row's content is a child of the button, so the button is sized to it by hand.
	private static HBoxContainer RowContent(Button row) {
		HBoxContainer content = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		content.AddThemeConstantOverride("separation", 6);

		MarginContainer margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
		margin.AddThemeConstantOverride("margin_left", 10);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_top", 4);
		margin.AddThemeConstantOverride("margin_bottom", 4);
		margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margin.AddChild(content);
		row.AddChild(margin);

		margin.MinimumSizeChanged += () => row.CustomMinimumSize = new Vector2(0, Mathf.Max(36f, margin.GetCombinedMinimumSize().Y));
		return content;
	}

	private void AddPool(Container into, List<Dice> pool, int modifier, float side) {
		foreach (DiceChip chip in DiceChip.ForPool(pool, side)) into.AddChild(chip);
		if (modifier != 0 || pool.Count == 0) into.AddChild(Text($"{modifier:+#;-#;+0}", Mathf.RoundToInt(side * 0.8f), parchment));
	}

	private void AddCondition(Container into, EncounterManager.StatusEffect condition, float size) {
		int index = (int)condition;
		if (condition == EncounterManager.StatusEffect.NONE || conditionTextures == null || index >= conditionTextures.Count) return;
		into.AddChild(Icon(conditionTextures[index], Colors.White, size));
	}

	private static TextureRect Icon(Texture2D texture, Color tint, float size) {
		return new TextureRect {
			Texture = texture,
			Modulate = tint,
			CustomMinimumSize = new Vector2(size, size),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore,
		};
	}

	private static void AddIcon(Container into, Texture2D texture, Color tint, float size) {
		if (into == null || texture == null) return;
		into.AddChild(Icon(texture, tint, size));
	}

	private static Label Text(string text, int size, Color colour, float minWidth = 0f) {
		Label label = new Label {
			Text = text,
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(minWidth, 0),
		};
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", colour);
		label.AddThemeConstantOverride("outline_size", 0);
		return label;
	}

	// A number in a small circle: the printed card's range and dodge circles.
	private Label Badge(string text, Color edge) {
		Label badge = Text(text, 12, parchment);
		badge.HorizontalAlignment = HorizontalAlignment.Center;
		badge.CustomMinimumSize = new Vector2(22, 22);
		StyleBoxFlat circle = new StyleBoxFlat { BgColor = new Color(0.078f, 0.07f, 0.055f), BorderColor = edge };
		circle.SetBorderWidthAll(1);
		circle.SetCornerRadiusAll(11);
		badge.AddThemeStyleboxOverride("normal", circle);
		return badge;
	}

	private static Control Spacer(float width) =>
		new Control { CustomMinimumSize = new Vector2(width, 0), MouseFilter = MouseFilterEnum.Ignore };

	private void SetPanelStyle(bool reaction) {
		StyleBox style = reaction ? panelReactionStyle : panelStyle;
		if (panel != null && style != null) panel.AddThemeStyleboxOverride("panel", style);
	}

	// Dim by darkening, never by alpha: the cross floats over the board.
	private void Dim(bool dim) {
		float shade = dim ? dimShade : 1f;
		Color colour = new Color(shade, shade, shade, 1f);
		if (panel != null) panel.Modulate = colour;
		if (cross != null) cross.Modulate = colour;
	}

	private void RefreshSouls() {
		if (soulsLabel != null) soulsLabel.Text = SoulCache.current.ToString();
	}

	private static void ClearChildren(Container container) {
		if (container == null) return;
		foreach (Node child in container.GetChildren()) {
			container.RemoveChild(child);
			child.QueueFree();
		}
	}

	private static List<Weapon> HandWeapons(Player player) {
		List<Weapon> weapons = new List<Weapon>();
		foreach (Weapon weapon in new[] { player.GetLeftHand(), player.GetRightHand() }) {
			if (weapon != null && !weapons.Contains(weapon)) weapons.Add(weapon);
		}
		return weapons;
	}

	private static bool Holds(Player player, Weapon weapon) =>
		weapon != null && (player.GetLeftHand() == weapon || player.GetRightHand() == weapon);

	// The first hand weapon that can still attack; failing that, whatever is held.
	private static Weapon FirstUsable(PlayerToken active) {
		List<Weapon> held = HandWeapons(active.player);
		foreach (Weapon weapon in held) {
			if (weapon.attacks != null && weapon.attacks.Count > 0 && !active.HasAttackedWith(weapon)) return weapon;
		}
		return held.Count > 0 ? held[0] : null;
	}
}
