using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

// The roll, revealed: a modal in the middle of the screen where the dice tumble and land,
// BG3-style, then the arithmetic they decide — who was hit for how much — before a click
// lets the encounter carry on. Every roll in the game goes through here: a character's
// attack, a character's Block or Resist against an enemy, and a Dodge.
//
// The view is data (dice with their faces, a total, a line per model affected); nothing
// here rolls or applies anything — CombatPresenter does both around the await.
//
// Registers itself in EncounterManager on ready. It sits after the board in the scene, so
// the registration lands after ActionListener's Reset rather than being wiped by it.
public partial class RollReveal : CanvasLayer
{

	public struct Face {
		public DiceUtility.DICE_TYPE type;
		public int value;
		public int[] faces;
	}

	public class Line {
		public Texture2D portrait;
		public Color rim = new Color(0.851f, 0.18f, 0.122f);
		public Texture2D badge;
		public Color badgeTint = Colors.White;
		public int badgeValue = -1;
		public float badgeDrop;
		public int damage;
		public bool dodged;
	}

	public class View {
		public Texture2D actorArt;
		public Color actorTint = Colors.White;
		public bool actorIsCard;
		public string actorName = "";
		public List<Face> faces = new List<Face>();
		public int modifier;
		public int total;
		public List<Line> lines = new List<Line>();
		// An enemy's attack on a character: damage in the lines is coming the party's way.
		public bool againstCharacter;
		// Whose Luck token may reroll one die of this roll: the character who rolled it.
		public Player luckOwner;
		// Rebuilds total and lines from the faces after a reroll. The presenter reads the
		// outcome back from the same view once the reveal closes.
		public System.Action<View> recompute;
		// Whose Heroic Action may add a die to this roll (the Knight's Stand Fast), and which.
		public Player heroicOwner;
		public Dice heroicDie;
	}

	[Export] public Container actorRow;
	[Export] public Container diceRow;
	[Export] public Label totalLabel;
	[Export] public Container lineRows;
	[Export] public Button continueButton;

	[Export] public Texture2D dodgeIcon;
	[Export] public Texture2D physicalIcon;
	[Export] public Texture2D magicIcon;
	[Export] public Texture2D blockIcon;
	[Export] public Texture2D resistIcon;

	[Export] public float dieSide = 68f;
	[Export] public float settleStagger = 0.12f;
	[Export] public float countSeconds = 0.35f;

	[Export] public Color parchment = new Color(0.902f, 0.871f, 0.796f);
	[Export] public Color gold = new Color(0.788f, 0.635f, 0.294f);
	[Export] public Color dodgeColour = new Color(0.45f, 0.82f, 0.42f);

	private readonly List<RollDie> dice = new List<RollDie>();

	public override void _Ready() {
		Visible = false;
		EncounterManager.rollReveal = this;
	}

	public override void _ExitTree() {
		if (EncounterManager.rollReveal == this) EncounterManager.rollReveal = null;
	}

	public async Task Present(View view) {
		if (view == null) return;

		Build(view);
		Visible = true;
		if (continueButton != null) continueButton.Disabled = true;

		await Throw();
		await CountTotal(view.total);

		ShowLines(view);

		bool continued = (CanUseLuck(view) || CanUseHeroic(view)) && await OfferTokens(view);
		if (!continued && continueButton != null) {
			continueButton.Disabled = false;
			continueButton.GrabFocus();
			await ToSignal(continueButton, BaseButton.SignalName.Pressed);
		}

		Visible = false;
	}

	private void ShowLines(View view) {
		if (lineRows == null) return;
		Clear(lineRows);
		foreach (Line line in view.lines) lineRows.AddChild(BuildLine(line));
		lineRows.Visible = true;

		// Damage coming the party's way is felt as the numbers land, before it is applied.
		if (view.againstCharacter) {
			int worst = 0;
			foreach (Line line in view.lines) worst = Mathf.Max(worst, line.dodged ? 0 : line.damage);
			if (worst > 0) BoardFx.punch?.Flash(worst);
		}
	}

	// ----- Luck and the Heroic die -----

	private const int CONTINUE = -1;
	private const int HEROIC = -2;

	private bool CanUseLuck(View view) =>
		view.luckOwner != null && !view.luckOwner.luckUsed && view.recompute != null && dice.Count > 0 && diceRow != null;

	private bool CanUseHeroic(View view) =>
		view.heroicOwner != null && !view.heroicOwner.heroicUsed && view.heroicDie != null && view.recompute != null && diceRow != null;

	// The roller's unspent tokens sit at the end of the dice. With Luck, every die lights
	// under the cursor and a click throws it again; the Heroic token (Stand Fast) is clicked
	// to throw one more die. Either may follow the other. Continue passes on whatever is
	// left. Returns true if they carried on.
	private async Task<bool> OfferTokens(View view) {
		TextureRect luck = null;
		TextureButton heroic = null;
		if (CanUseLuck(view)) {
			luck = Icon(TokenArt.Ready(TokenArt.Kind.LUCK), Colors.White, 46f);
			luck.TooltipText = TokenArt.Title(TokenArt.Kind.LUCK);
			luck.MouseFilter = Control.MouseFilterEnum.Pass;
			diceRow.AddChild(luck);
			PopIn.Scale(luck, 1.4f, 0.3f);
		}
		if (CanUseHeroic(view)) {
			heroic = new TextureButton {
				TextureNormal = TokenArt.Ready(TokenArt.Kind.HEROIC),
				IgnoreTextureSize = true,
				StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
				CustomMinimumSize = new Vector2(46f, 46f),
				SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
				FocusMode = Control.FocusModeEnum.None,
				TooltipText = Heroic.Name(view.heroicOwner.character?.heroicAction ?? Heroic.Kind.NONE),
			};
			diceRow.AddChild(heroic);
			PopIn.Scale(heroic, 1.4f, 0.3f);
		}

		while (true) {
			bool canLuck = luck != null && CanUseLuck(view);
			bool canHeroic = heroic != null && CanUseHeroic(view);
			if (!canLuck && !canHeroic) return false;

			TaskCompletionSource<int> choice = new TaskCompletionSource<int>();
			List<System.Action> handlers = new List<System.Action>();
			if (canLuck) {
				for (int i = 0; i < dice.Count; i++) {
					int index = i;
					System.Action handler = () => choice.TrySetResult(index);
					handlers.Add(handler);
					dice[i].Picked += handler;
					dice[i].SetPickable(true);
				}
			}
			System.Action onHeroic = () => choice.TrySetResult(HEROIC);
			if (canHeroic) heroic.Pressed += onHeroic;
			System.Action onContinue = () => choice.TrySetResult(CONTINUE);
			if (continueButton != null) {
				continueButton.Pressed += onContinue;
				continueButton.Disabled = false;
				continueButton.GrabFocus();
			}

			int picked = await choice.Task;

			for (int i = 0; i < handlers.Count; i++) {
				dice[i].Picked -= handlers[i];
				dice[i].SetPickable(false);
			}
			if (canHeroic) heroic.Pressed -= onHeroic;
			if (continueButton != null) continueButton.Pressed -= onContinue;
			if (picked == CONTINUE) return true;

			if (continueButton != null) continueButton.Disabled = true;
			if (picked == HEROIC) await ThrowHeroicDie(view, heroic);
			else await RerollWithLuck(view, picked, luck);

			view.recompute(view);
			await CountTotal(view.total);
			ShowLines(view);
		}
	}

	private async Task RerollWithLuck(View view, int picked, TextureRect luck) {
		view.luckOwner.luckUsed = true;
		luck.Texture = TokenArt.Used(TokenArt.Kind.LUCK);
		PopIn.Scale(luck, 1.5f, 0.3f);

		Face face = view.faces[picked];
		face.value = face.faces != null && face.faces.Length > 0 ? DiceUtility.RollDice(face.faces) : face.value;
		view.faces[picked] = face;
		RollDie die = dice[picked];
		die.Reroll(face.value);
		while (!die.settled) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	private async Task ThrowHeroicDie(View view, TextureButton heroic) {
		view.heroicOwner.heroicUsed = true;
		heroic.Disabled = true;
		heroic.TextureNormal = TokenArt.Used(TokenArt.Kind.HEROIC);
		PopIn.Scale(heroic, 1.5f, 0.3f);

		Dice source = view.heroicDie;
		Face face = new Face { type = source.diceType, value = DiceUtility.Roll(source), faces = source.dice };
		view.faces.Add(face);
		RollDie die = RollDie.Create(face, dieSide);
		dice.Add(die);
		diceRow.AddChild(die);
		diceRow.MoveChild(die, dice.Count - 1);     // after the last die, before the modifier and tokens
		die.Throw(0f);
		while (!die.settled) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	// ----- Building -----

	private void Build(View view) {
		Clear(actorRow);
		Clear(diceRow);
		Clear(lineRows);
		dice.Clear();

		if (actorRow != null) {
			TextureRect art = Icon(view.actorArt, view.actorTint, 40f);
			if (view.actorIsCard) art.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
			actorRow.AddChild(art);
			actorRow.AddChild(Text(view.actorName, 22, parchment));
		}

		if (diceRow != null) {
			foreach (Face face in view.faces) {
				RollDie die = RollDie.Create(face, dieSide);
				dice.Add(die);
				diceRow.AddChild(die);
			}
			if (view.modifier != 0) diceRow.AddChild(Text($"{view.modifier:+#;-#}", 24, parchment));
		}
		if (totalLabel != null) totalLabel.Text = "";

		if (lineRows != null) lineRows.Visible = false;
	}

	private Control BuildLine(Line line) {
		HBoxContainer row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("separation", 12);

		if (line.portrait != null) {
			Panel face = new Panel { CustomMinimumSize = new Vector2(30, 40), ClipChildren = CanvasItem.ClipChildrenMode.Only, MouseFilter = Control.MouseFilterEnum.Ignore };
			StyleBoxFlat frame = new StyleBoxFlat { BgColor = new Color(0.106f, 0.094f, 0.075f), BorderColor = line.rim };
			frame.SetBorderWidthAll(2);
			face.AddThemeStyleboxOverride("panel", frame);
			TextureRect portrait = Icon(line.portrait, Colors.White, 30f);
			portrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
			portrait.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			portrait.OffsetLeft = portrait.OffsetTop = 2;
			portrait.OffsetRight = portrait.OffsetBottom = -2;
			face.AddChild(portrait);
			row.AddChild(face);
		}

		if (line.badge != null) row.AddChild(Badge(line));

		row.AddChild(Spacer(10f));
		if (line.dodged) {
			row.AddChild(Icon(dodgeIcon, dodgeColour, 34f));
		} else if (line.damage > 0) {
			row.AddChild(CubeRow.Damage(line.damage, 14f));
		} else {
			row.AddChild(Text("0", 22, parchment));
		}
		return row;
	}

	// An icon with a number inside it: the enemy's Block, the attack's strength, the dodge
	// difficulty. The drop nudges the number down for icons whose centre sits low.
	private Control Badge(Line line) {
		Control badge = new Control { CustomMinimumSize = new Vector2(38, 38), MouseFilter = Control.MouseFilterEnum.Ignore };
		TextureRect icon = Icon(line.badge, line.badgeTint, 38f);
		icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		badge.AddChild(icon);

		if (line.badgeValue >= 0) {
			Label value = Text(line.badgeValue.ToString(), 15, parchment);
			value.HorizontalAlignment = HorizontalAlignment.Center;
			value.AddThemeColorOverride("font_outline_color", Colors.Black);
			value.AddThemeConstantOverride("outline_size", 5);
			value.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			value.AnchorTop = line.badgeDrop;
			value.AnchorBottom = 1f + line.badgeDrop;
			badge.AddChild(value);
		}
		return badge;
	}

	// ----- The throw -----

	// The handful is thrown together, each die a beat after the last, and the reveal waits
	// until every one has come to rest.
	private async Task Throw() {
		for (int i = 0; i < dice.Count; i++) dice[i].Throw(i * settleStagger);

		while (true) {
			bool allSettled = true;
			foreach (RollDie die in dice) {
				if (!die.settled) {
					allSettled = false;
					break;
				}
			}
			if (allSettled) return;
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
	}

	// The total counts up from nothing and pops when it gets there.
	private async Task CountTotal(int total) {
		if (totalLabel == null) return;
		if (total <= 0) {
			totalLabel.Text = total.ToString();
			return;
		}

		Tween tween = CreateTween();
		tween.TweenMethod(Callable.From<float>(v => totalLabel.Text = Mathf.RoundToInt(v).ToString()), 0f, (float)total, countSeconds);
		await ToSignal(tween, Tween.SignalName.Finished);
		totalLabel.Text = total.ToString();
		PopIn.Scale(totalLabel, 1.5f, 0.25f);
	}

	// ----- Pieces -----

	private static TextureRect Icon(Texture2D texture, Color tint, float size) {
		return new TextureRect {
			Texture = texture,
			Modulate = tint,
			CustomMinimumSize = new Vector2(size, size),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
	}

	private static Label Text(string text, int size, Color colour) {
		Label label = new Label {
			Text = text,
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", colour);
		return label;
	}

	private static Control Spacer(float width) =>
		new Control { CustomMinimumSize = new Vector2(width, 0), MouseFilter = Control.MouseFilterEnum.Ignore };

	private static void Clear(Container container) {
		if (container == null) return;
		foreach (Node child in container.GetChildren()) {
			container.RemoveChild(child);
			child.QueueFree();
		}
	}
}
