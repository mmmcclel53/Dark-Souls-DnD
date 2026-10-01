using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

// The bottom HUD, in the shape of the Dark Souls games' with BG3's readouts: the equipment
// cross floating off its left end, then the character's face in a ring, their endurance
// bar, defence dice and tokens, the raised weapon's attack options as the rows of its
// printed card, and End Turn.
//
// It has three states and never leaves the layout, because its height is part of what
// BoardCamera fits the board around:
//   Idle       – between activations: the selected character, every button disabled.
//   Activation – the active character; clicking a hand slot raises that weapon and its
//                options fill the rows. Hovering an option ghosts its cost onto the bar.
//                Selecting anyone else from the party pane shows them read-only.
//   Reaction   – an enemy is attacking someone: the defender takes the bar over, the armour
//                gear that rolls lights on the cross, the header shows the attack and the rows become Block and
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
	[Signal] public delegate void HeroicChosenEventHandler();
	[Signal] public delegate void BackstabPassedEventHandler();

	[Export] public Control crossAnchor;
	[Export] public EquipmentCross cross;
	[Export] public PanelContainer panel;
	[Export] public CircleAvatar avatar;
	[Export] public Control aggroBadge;
	[Export] public Label nameLabel;
	[Export] public TickBar endurance;
	[Export] public Container defenceRow;
	[Export] public Container tokenRow;
	[Export] public Container header;
	[Export] public Container rows;
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
	// Attack rows shrink when a weapon has three options, so the pill never grows past the avatar.
	[Export] public float rowHeight = 32f;
	[Export] public float compactRowHeight = 24f;

	private enum Mode { IDLE, ACTIVATION, REACTION, DODGE_STEP, BACKSTAB }

	private Mode mode = Mode.IDLE;
	private PlayerToken token;
	private Weapon raised;
	private Player shown;
	private Player selected;
	private int shownSpent = -1;
	private int shownDamage = -1;

	private static StyleBoxFlat rowNormal, rowHover, rowArmed, rowDisabled;
	private StyleBoxFlat pill, pillReaction;
	private float currentRowHeight = 32f;
	// Backstab: the Assassin choosing a free attack on the enemy they dodged.
	private PlayerToken backstabber;
	private System.Func<Weapon, PlayerMove, bool> backstabReach;
	private ButtonFx endTurnFx;

	public override void _Ready() {
		BuildRowStyles();
		if (aggroBadge != null) aggroBadge.Material = PlayerToken.AggroMask;
		BuildPillStyles();

		if (endTurnButton != null) endTurnButton.Pressed += OnEndPressed;
		endTurnFx = ButtonFx.Attach(endTurnButton, round: true);
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
		if (aggroBadge != null) {
			PlayerToken holder = EncounterManager.aggroHolder;
			aggroBadge.Visible = shown != null && GodotObject.IsInstanceValid(holder) && holder.player == shown;
		}
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
		backstabber = null;
		backstabReach = null;
		ShowSelected();
	}

	public void Refresh(PlayerToken active) {
		if (active == null) {
			ShowIdle();
			return;
		}
		// A new activation brings its character forward; later refreshes keep whoever the
		// player is looking at.
		if (token != active) selected = active.player;
		mode = Mode.ACTIVATION;
		token = active;
		ShowSelected();
	}

	// Picks whose readout the bar shows between and during activations. A reaction or a
	// dodge step owns the bar while it lasts, so the choice is kept and shown afterwards.
	public void Select(Player player) {
		if (player == null) return;
		selected = player;
		if (mode == Mode.IDLE || mode == Mode.ACTIVATION) ShowSelected();
	}

	// The selected character, or the active one. Only the active character's own readout
	// is live; anyone else is shown with every button disabled.
	private void ShowSelected() {
		Player player = selected ?? token?.player;
		bool live = mode == Mode.ACTIVATION && token != null && token.player == player;
		PlayerToken viewed = live ? token : TokenOf(player);

		endurance?.ClearGhost();
		if (endTurnButton != null) {
			endTurnButton.Text = "End Turn";
			EnableEndTurn(live);
		}
		SetPanelStyle(false);
		Dim(false);

		if (player == null) {
			ClearChildren(header);
			ClearChildren(rows);
			cross?.Clear();
			if (nameLabel != null) nameLabel.Text = "";
			Dim(true);
			return;
		}

		ShowCharacter(player);
		cross?.Show(player);
		if (live) foreach (Weapon weapon in HandWeapons(player)) cross?.SetSpent(weapon, token.HasAttackedWith(weapon));

		if (raised == null || !Holds(player, raised) || (live && !CanSwing(token, raised))) raised = FirstUsable(player, live ? token : null);
		cross?.SetDefending(null);
		cross?.SetRaised(raised);

		BuildHeader(raised);
		BuildRows(viewed, raised, live);
	}

	// Backstab: after a successful dodge the Assassin may attack the enemy dodged, for free.
	// Their attack rows show at no cost, lit only where an option reaches that enemy; End
	// becomes Pass.
	public void ShowBackstab(PlayerToken assassin, System.Func<Weapon, PlayerMove, bool> reaches) {
		if (assassin == null) return;
		mode = Mode.BACKSTAB;
		token = null;
		backstabber = assassin;
		backstabReach = reaches;

		ShowCharacter(assassin.player);
		cross?.Show(assassin.player);
		cross?.SetDefending(null);
		if (raised == null || !Holds(assassin.player, raised) || !AnyReach(raised)) {
			raised = null;
			foreach (Weapon weapon in HandWeapons(assassin.player)) {
				if (!AnyReach(weapon)) continue;
				raised = weapon;
				break;
			}
			raised ??= FirstUsable(assassin.player, null);
		}
		cross?.SetRaised(raised);
		BuildHeader(raised);
		BuildRows(assassin, raised, false);

		if (endTurnButton != null) {
			endTurnButton.Text = "Pass";
			EnableEndTurn(true);
		}
		SetPanelStyle(false);
		Dim(false);
	}

	private bool AnyReach(Weapon weapon) {
		if (weapon?.attacks == null || backstabReach == null) return false;
		foreach (PlayerMove move in weapon.attacks) {
			if (move != null && backstabReach(weapon, move)) return true;
		}
		return false;
	}

	// A weapon swings once per activation (p22), unless Rapid Strike's extra attack is armed.
	private static bool CanSwing(PlayerToken attacker, Weapon weapon) =>
		!attacker.HasAttackedWith(weapon) || attacker.pendingHeroic == Heroic.Kind.RAPID_STRIKE;

	// A successful dodge lets the character move one node (p22): the bar shows the dodger
	// with an End Dodge button while CharacterTurn lights the nodes they may step to.
	public void ShowDodgeStep(PlayerToken dodger) {
		if (dodger == null) return;
		mode = Mode.DODGE_STEP;
		token = null;

		ShowCharacter(dodger.player);
		cross?.Show(dodger.player);
		cross?.SetRaised(null);
		cross?.SetDefending(null);
		ClearChildren(header);
		ClearChildren(rows);
		if (header != null) header.AddChild(Icon(dodgeIcon, dodgeColour, 40f));

		if (endTurnButton != null) {
			endTurnButton.Text = "End Dodge";
			EnableEndTurn(true);
		}
		SetPanelStyle(false);
		Dim(false);
	}

	// The button glints as it comes alive, so the moment the turn is the player's is seen.
	private void EnableEndTurn(bool enabled) {
		bool wasDisabled = endTurnButton.Disabled;
		endTurnButton.Disabled = !enabled;
		if (enabled && wasDisabled) endTurnFx?.Pulse();
	}

	private void OnEndPressed() {
		if (mode == Mode.DODGE_STEP) EmitSignal(SignalName.DodgeStepEnded);
		else if (mode == Mode.BACKSTAB) EmitSignal(SignalName.BackstabPassed);
		else EmitSignal(SignalName.EndActivationPressed);
	}

	// Any shown character's hands can be raised to read their options; only the active
	// character's spent weapons stay down.
	private void OnHandPressed(int hand) {
		if ((mode != Mode.IDLE && mode != Mode.ACTIVATION && mode != Mode.BACKSTAB) || cross == null) return;
		Weapon weapon = cross.WeaponIn(hand);
		if (weapon == null) return;
		if (mode == Mode.ACTIVATION && token?.player == shown && !CanSwing(token, weapon)) return;

		if (mode == Mode.BACKSTAB) {
			raised = weapon;
			cross.SetRaised(raised);
			BuildHeader(raised);
			BuildRows(backstabber, raised, false);
			return;
		}

		raised = weapon;
		ShowSelected();
	}

	private void ShowCharacter(Player player) {
		shown = player;
		shownSpent = shownDamage = -1;
		avatar?.Show(player);
		if (nameLabel != null) nameLabel.Text = player?.name ?? "";
		if (player != null) endurance?.ShowEndurance(player.endurance);
		BuildDefence(player);
		BuildTokens(player);
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

	// Estus, Heroic Action and Luck as they stand on the character's board: ready, or
	// flipped once spent. Only the Estus is used from here — by the active character, with
	// something to restore. Luck is spent on the roll reveal, where the dice are.
	private void BuildTokens(Player player) {
		ClearChildren(tokenRow);
		if (tokenRow == null || player == null) return;

		bool live = mode == Mode.ACTIVATION && token != null && token.player == player;
		AddToken(TokenArt.Kind.ESTUS, player.estusUsed, live && token.CanDrinkEstus, DrinkEstus);
		Heroic.Kind heroic = player.character?.heroicAction ?? Heroic.Kind.NONE;
		AddToken(TokenArt.Kind.HEROIC, player.heroicUsed, live && token.CanUseHeroicNow,
			() => EmitSignal(SignalName.HeroicChosen), $"{Heroic.Name(heroic)}\n{Heroic.Effect(heroic)}");
		AddToken(TokenArt.Kind.LUCK, player.luckUsed, false, null);
	}

	private void AddToken(TokenArt.Kind kind, bool used, bool usable, System.Action use, string tooltip = null) {
		TextureButton button = new TextureButton {
			TextureNormal = TokenArt.For(kind, used),
			IgnoreTextureSize = true,
			StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(30, 30),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			FocusMode = FocusModeEnum.None,
			Disabled = !usable,
			TooltipText = tooltip ?? TokenArt.Title(kind),
		};
		// Deferred: using a token rebuilds this row, which would free the button mid-signal.
		if (usable && use != null) button.Pressed += () => Callable.From(use).CallDeferred();
		tokenRow.AddChild(button);
	}

	private void DrinkEstus() {
		if (mode != Mode.ACTIVATION || token == null || !token.CanDrinkEstus) return;
		token.DrinkEstus();
		ShowSelected();
		if (tokenRow != null && tokenRow.GetChildCount() > 0 && tokenRow.GetChild(0) is Control flipped) {
			PopIn.Scale(flipped, 1.5f, 0.3f);
		}
	}

	public static List<Dice> DefencePool(Player player, bool magic) => player.GetDefensePool(magic).dice;

	public static int DefenceModifier(Player player, bool magic) => player.GetDefensePool(magic).modifier;

	// ----- Activation: header and attack rows -----

	private void BuildHeader(Weapon weapon) {
		ClearChildren(header);
		if (header == null || weapon == null) return;

		TextureRect art = Icon(EquipmentSlot.CropOf(weapon, cross?.left?.RegionFor(weapon)), Colors.White, 28f);
		art.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
		header.AddChild(art);
		header.AddChild(Text(weapon.name, 16, parchment));
		header.AddChild(Badge(weapon.attackRange.ToString(), gold));
	}

	// The token is null for a character not yet on the board; costs then leave out Stagger.
	private void BuildRows(PlayerToken owner, Weapon weapon, bool live) {
		ClearChildren(rows);
		if (rows == null || weapon?.attacks == null) return;

		bool used = live && owner.HasAttackedWith(weapon);
		currentRowHeight = weapon.attacks.Count >= 3 ? compactRowHeight : rowHeight;
		foreach (PlayerMove move in weapon.attacks) {
			if (move == null) continue;
			rows.AddChild(AttackRow(owner, weapon, move, used, live));
		}
	}

	private Button AttackRow(PlayerToken owner, Weapon weapon, PlayerMove move, bool weaponUsed, bool live) {
		bool backstab = mode == Mode.BACKSTAB;
		AttackTerms terms = AttackTerms.For(owner, weapon, move, free: backstab);
		int cost = terms.cost;
		bool armed = live && EncounterManager.characterTurn?.armed == move;

		Button row = Row(armed);
		row.Disabled = backstab
			? !(backstabReach?.Invoke(weapon, move) ?? false)
			: !live || (weaponUsed && !terms.reusesWeapon) || !owner.CanSpend(cost);
		row.TooltipText = Describe(move, terms, owner);

		HBoxContainer content = RowContent(row);
		content.AddChild(Text($"[{cost}]", 16, armed ? parchment : muted, 40f));
		content.AddChild(CubeRow.Stamina(cost));
		content.AddChild(Spacer(6f));
		List<Dice> pool = new List<Dice>();
		if (move.damage != null) pool.AddRange(move.damage);
		pool.AddRange(terms.extraDice);
		foreach (DiceChip chip in DiceChip.ForPool(pool, ChipSize)) content.AddChild(chip);
		if (move.modifier != 0 || pool.Count == 0) {
			content.AddChild(Text($"{move.modifier:+#;-#;+0}", 16, parchment));
		}
		if (move.isMagic) content.AddChild(Icon(magicIcon, resistColour, 18f));
		AddCondition(content, move.statusEffect, 18f);
		if (terms.range != weapon.attackRange) {
			content.AddChild(Badge(terms.range >= EnemyData.UNLIMITED_RANGE ? "∞" : terms.range.ToString(), gold));
		}
		// An armed Heroic Action reshaped this attack: its token marks the row.
		if (terms.boosted) content.AddChild(Icon(TokenArt.Ready(TokenArt.Kind.HEROIC), Colors.White, ChipSize));
		if (row.Disabled) content.Modulate = new Color(0.5f, 0.5f, 0.5f, 1f);

		// Capture the loop values before the lambda, or every row fires the last option.
		Weapon capturedWeapon = weapon;
		PlayerMove capturedMove = move;
		row.Pressed += () => EmitSignal(SignalName.AttackChosen, capturedWeapon, capturedMove);
		row.MouseEntered += () => { if (!row.Disabled) endurance?.SetGhost(cost, 0, 0); };
		row.MouseExited += () => endurance?.ClearGhost();
		return row;
	}

	private static string Describe(PlayerMove move, AttackTerms terms, PlayerToken owner) {
		string range = terms.range >= EnemyData.UNLIMITED_RANGE ? "unlimited" : terms.range.ToString();
		List<string> notes = new List<string> { $"Range {range}", $"{terms.cost} stamina" };

		if (terms.boosted && owner != null) notes.Add(Heroic.Name(owner.pendingHeroic));
		if (move.isMagic) notes.Add("magical");
		if (terms.aoe) notes.Add("whole node");
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

		ShowCharacter(target.player);
		cross?.Show(target.player);
		cross?.SetRaised(null);
		cross?.SetDefending(DefendingGear(target.player, move.isMagic));
		BuildReactionHeader(attacker, move);
		BuildReactionRows(attacker, move, target);

		if (endTurnButton != null) endTurnButton.Disabled = true;
		SetPanelStyle(true);
		Dim(false);

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
		Panel face = new Panel { CustomMinimumSize = new Vector2(21, 28), ClipChildren = ClipChildrenMode.Only, MouseFilter = MouseFilterEnum.Ignore };
		face.AddThemeStyleboxOverride("panel", Frame(attackColour, 2));
		TextureRect portrait = Icon(attacker?.data?.GetPortrait(), Colors.White, 28f);
		portrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
		portrait.CustomMinimumSize = Vector2.Zero;      // the frame sizes it, not the icon default
		portrait.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		portrait.OffsetLeft = portrait.OffsetTop = 2;
		portrait.OffsetRight = portrait.OffsetBottom = -2;
		face.AddChild(portrait);
		header.AddChild(face);

		int strength = CombatResolver.AttackStrength(move, attacker);
		header.AddChild(AttackBadge(move.isMagic, strength));
		header.AddChild(CubeRow.Damage(strength, 12f));
		AddCondition(header, move.statusEffect, 20f);
	}

	// The rulebook's attack icon with the strength inside it. The physical icon's number
	// sits in the shield, a little below centre; the magic icon's sits dead centre.
	private Control AttackBadge(bool magic, int strength) {
		Control badge = new Control { CustomMinimumSize = new Vector2(40, 40), MouseFilter = MouseFilterEnum.Ignore };
		TextureRect icon = Icon(magic ? magicIcon : physicalIcon, gold, 40f);
		icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		badge.AddChild(icon);

		Label value = Text(strength.ToString(), 14, parchment);
		value.HorizontalAlignment = HorizontalAlignment.Center;
		value.AddThemeColorOverride("font_outline_color", Colors.Black);
		value.AddThemeConstantOverride("outline_size", 5);
		value.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		// Centres the digit on the wide part of the physical icon's shield. The label's line box
		// has room for descenders, so a digit centred in it sits low: this lifts it back.
		// Measured against the drawn badge, not guessed.
		float drop = magic ? 0f : -0.075f;
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

		// Block: its dice and the damage that could still get through. The gear that rolls is lit
		// on the cross rather than repeated here.
		List<Dice> pool = DefencePool(player, move.isMagic);
		int modifier = DefenceModifier(player, move.isMagic);
		(int best, int worst, int expected) = DamageThrough(strength, pool, modifier);

		currentRowHeight = rowHeight;
		Button block = Row(true);
		block.TooltipText = move.isMagic ? "Resist" : "Block";
		HBoxContainer[] blockColumns = Columns(RowContent(block));
		blockColumns[0].AddChild(Icon(move.isMagic ? resistIcon : blockIcon, move.isMagic ? resistColour : blockColour, 24f));
		AddPool(blockColumns[1], pool, modifier, ChipSize);
		blockColumns[2].AddChild(CubeRow.Damage(1, 10f));
		blockColumns[2].AddChild(Text(best == worst ? $"{worst}" : $"{best}-{worst}", 16, parchment));
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
		HBoxContainer[] dodgeColumns = Columns(dodgeContent);
		dodgeColumns[0].AddChild(Icon(dodgeIcon, possible ? dodgeColour : muted, 24f));
		dodgeColumns[1].AddChild(DiceChip.Create(DiceUtility.DICE_TYPE.DODGE, dodgePool, ChipSize));
		dodgeColumns[1].AddChild(Badge(move.dodgeDifficulty.ToString(), possible ? gold : muted));
		dodgeColumns[2].AddChild(CubeRow.Stamina(cost));
		dodgeColumns[2].AddChild(Text($"{Mathf.RoundToInt(chance * 100f)}%", 16, possible ? parchment : muted));
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

	private float ChipSize => currentRowHeight - 10f;

	private Button Row(bool armed) {
		Button row = new Button {
			CustomMinimumSize = new Vector2(0, currentRowHeight),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		row.AddThemeStyleboxOverride("normal", armed ? rowArmed : rowNormal);
		row.AddThemeStyleboxOverride("hover", armed ? rowArmed : rowHover);
		row.AddThemeStyleboxOverride("pressed", rowArmed);
		row.AddThemeStyleboxOverride("focus", armed ? rowArmed : rowHover);
		row.AddThemeStyleboxOverride("disabled", rowDisabled);
		// The armed row breathes in step with the target nodes it lit.
		ButtonFx.Attach(row, breathing: armed);
		return row;
	}

	// The row's content is a child of the button, so the button is sized to it by hand.
	private HBoxContainer RowContent(Button row) {
		float height = currentRowHeight;
		HBoxContainer content = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		content.AddThemeConstantOverride("separation", 6);

		MarginContainer margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
		margin.AddThemeConstantOverride("margin_left", 10);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_top", 3);
		margin.AddThemeConstantOverride("margin_bottom", 3);
		margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margin.AddChild(content);
		row.AddChild(margin);

		margin.MinimumSizeChanged += () => row.CustomMinimumSize = new Vector2(0, Mathf.Max(height, margin.GetCombinedMinimumSize().Y));
		return content;
	}

	// Block and Dodge share three left-aligned columns (what rolls, the dice, the outcome) so
	// the two rows read against each other.
	private static HBoxContainer[] Columns(HBoxContainer content) {
		float[] widths = { 30f, 120f, 0f };
		HBoxContainer[] columns = new HBoxContainer[widths.Length];
		for (int i = 0; i < widths.Length; i++) {
			columns[i] = new HBoxContainer { CustomMinimumSize = new Vector2(widths[i], 0), MouseFilter = MouseFilterEnum.Ignore };
			columns[i].AddThemeConstantOverride("separation", 6);
			if (i == widths.Length - 1) columns[i].SizeFlagsHorizontal = SizeFlags.ExpandFill;
			content.AddChild(columns[i]);
		}
		return columns;
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

	// The bar is a pill: fully round ends at whatever height it is laid out at, so the radius
	// follows the panel's size rather than being a number in the scene.
	private void BuildPillStyles() {
		pill = panelStyle?.Duplicate() as StyleBoxFlat;
		pillReaction = panelReactionStyle?.Duplicate() as StyleBoxFlat;
		if (panel == null) return;
		panel.Resized += RoundPill;
		CallDeferred(nameof(RoundPill));
	}

	private void RoundPill() {
		int radius = Mathf.RoundToInt(panel.Size.Y * 0.5f);
		pill?.SetCornerRadiusAll(radius);
		pillReaction?.SetCornerRadiusAll(radius);
	}

	private void SetPanelStyle(bool reaction) {
		StyleBox style = reaction ? (pillReaction ?? panelReactionStyle) : (pill ?? panelStyle);
		if (panel != null && style != null) panel.AddThemeStyleboxOverride("panel", style);
	}

	// Dim by darkening, never by alpha: the cross floats over the board.
	private void Dim(bool dim) {
		float shade = dim ? dimShade : 1f;
		Color colour = new Color(shade, shade, shade, 1f);
		if (panel != null) panel.Modulate = colour;
		if (cross != null) cross.Modulate = colour;
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
	private static Weapon FirstUsable(Player player, PlayerToken active) {
		List<Weapon> held = HandWeapons(player);
		foreach (Weapon weapon in held) {
			if (weapon.attacks != null && weapon.attacks.Count > 0 && !(active?.HasAttackedWith(weapon) ?? false)) return weapon;
		}
		return held.Count > 0 ? held[0] : null;
	}

	private static PlayerToken TokenOf(Player player) {
		if (player == null) return null;
		foreach (Node2D model in EncounterManager.players) {
			PlayerToken token = EncounterManager.GetPlayerToken(model);
			if (token != null && token.player == player) return token;
		}
		return null;
	}
}
