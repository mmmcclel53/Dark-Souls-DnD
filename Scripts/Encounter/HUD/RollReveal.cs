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

	[Export] public float dieSide = 52f;
	[Export] public float tumbleSeconds = 0.9f;
	[Export] public float settleStagger = 0.15f;
	[Export] public float tickSeconds = 0.05f;

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

		await Tumble();

		if (totalLabel != null) totalLabel.Text = view.total.ToString();
		if (lineRows != null) lineRows.Visible = true;
		if (continueButton != null) {
			continueButton.Disabled = false;
			continueButton.GrabFocus();
			await ToSignal(continueButton, BaseButton.SignalName.Pressed);
		}

		Visible = false;
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
				RollDie die = RollDie.Create(face, dodgeIcon, dieSide);
				dice.Add(die);
				diceRow.AddChild(die);
			}
			if (view.modifier != 0) diceRow.AddChild(Text($"{view.modifier:+#;-#}", 24, parchment));
		}
		if (totalLabel != null) totalLabel.Text = "";

		if (lineRows != null) {
			lineRows.Visible = false;
			foreach (Line line in view.lines) lineRows.AddChild(BuildLine(line));
		}
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

	// ----- The tumble -----

	// Every die cycles random faces, then they land one after another, left to right.
	private async Task Tumble() {
		double elapsed = 0;
		while (true) {
			await ToSignal(GetTree().CreateTimer(tickSeconds), SceneTreeTimer.SignalName.Timeout);
			elapsed += tickSeconds;

			bool allSettled = true;
			for (int i = 0; i < dice.Count; i++) {
				RollDie die = dice[i];
				if (die.settled) continue;
				if (elapsed >= tumbleSeconds + i * settleStagger) {
					die.Settle();
				} else {
					die.ShowFace(die.RandomFace());
					allSettled = false;
				}
			}
			if (allSettled) return;
		}
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
