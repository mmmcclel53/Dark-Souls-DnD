using Godot;
using System.Threading.Tasks;

// What a hex's loot gave, shown on the world map the moment it is taken (or on returning from
// the win that guarded it): the printed card, a shard, the souls or the key, and nothing
// written about them. Click, Continue or Enter dismisses. Above the portrait pane.
public partial class WorldMapLootPanel : CanvasLayer {
	[Signal] public delegate void DismissedEventHandler();

	private const string SOUL_ICON_PATH = "res://Resources/Images/Sprites/Soul.png";
	private const string KEY_CARDS = "res://Resources/Images/Sprites/Items/Keys/";
	private const float CARD_HEIGHT = 440f;
	private const float FADE_IN = 0.35f;
	private const float FADE_OUT = 0.15f;

	private static readonly Color PLATE = new Color(0.06f, 0.05f, 0.05f, 0.94f);
	private static readonly Color RIM = new Color(0.72f, 0.70f, 0.64f, 0.95f);
	private static readonly Color GOLD = new Color(0.91f, 0.82f, 0.58f);

	private Control root;
	private Button continueButton;
	private bool dismissed;
	// A boss's cards: the one picked, taken on Continue (WorldMapManager.TakeBossCard).
	private string chosen;
	private readonly System.Collections.Generic.List<PanelContainer> choiceFrames = new();

	public WorldMapLootPanel() {
		Layer = 12;
	}

	// False when there is nothing to show, such as a treasure drawn from an empty deck.
	public static bool CanShow(LootResult result) =>
		result.kind != WorldLoot.TREASURE || result.item != null || result.options.Length > 0;

	private const float CHOICE_HEIGHT = 330f;
	private static readonly Color DIM = new Color(0.25f, 0.24f, 0.22f, 0.9f);

	public async Task Present(LootResult result) {
		Build(result);
		root.Modulate = new Color(1, 1, 1, 0);
		CreateTween().TweenProperty(root, "modulate:a", 1f, FADE_IN);
		continueButton.GrabFocus();
		await ToSignal(this, SignalName.Dismissed);
	}

	private void Build(LootResult result) {
		root = new Control();
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.MouseFilter = Control.MouseFilterEnum.Stop;
		root.GuiInput += OnRootInput;
		AddChild(root);

		var scrim = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = Control.MouseFilterEnum.Ignore };
		scrim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(scrim);

		var centre = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(centre);

		var plate = new StyleBoxFlat { BgColor = PLATE, BorderColor = RIM };
		plate.SetBorderWidthAll(1);
		plate.SetCornerRadiusAll(6);
		plate.SetContentMarginAll(26);
		var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		panel.AddThemeStyleboxOverride("panel", plate);
		centre.AddChild(panel);

		var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		box.AddThemeConstantOverride("separation", 12);
		box.Alignment = BoxContainer.AlignmentMode.Center;
		panel.AddChild(box);

		switch (result.kind) {
			case WorldLoot.TREASURE:
				if (result.options.Length > 0) AddChoice(box, result.options);
				else AddTreasure(box, result.item);
				break;
			case WorldLoot.ESTUS_SHARD: AddShard(box, result.count); break;
			case WorldLoot.SOULS: AddSouls(box, result.souls); break;
			case WorldLoot.KEY: AddKey(box, result.keyId); break;
			case WorldLoot.HOMEWARD_BONE: AddBone(box, result.count); break;
		}

		continueButton = new Button { Text = "Continue", CustomMinimumSize = new Vector2(160, 36) };
		continueButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		continueButton.Pressed += Dismiss;
		continueButton.Disabled = result.options.Length > 0;
		box.AddChild(continueButton);
	}

	// A beaten boss's cards side by side; one click picks, Continue takes it. No words: the
	// cards are the choice.
	private void AddChoice(VBoxContainer box, string[] options) {
		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 14);
		box.AddChild(row);
		foreach (string name in options) {
			Equipment card = GameManager.GetTemplate(name);
			if (card?.image == null) continue;
			var frame = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop, TooltipText = name };
			frame.AddThemeStyleboxOverride("panel", Rim(DIM));
			var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			column.AddThemeConstantOverride("separation", 6);
			float aspect = card.image.GetWidth() / (float)card.image.GetHeight();
			column.AddChild(new TextureRect {
				Texture = card.image,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				CustomMinimumSize = new Vector2(CHOICE_HEIGHT * aspect, CHOICE_HEIGHT),
				MouseFilter = Control.MouseFilterEnum.Ignore,
			});
			column.AddChild(MakeLabel(name, 16, GOLD));
			frame.AddChild(column);
			frame.GuiInput += e => {
				if (e is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed) Choose(name, frame);
			};
			choiceFrames.Add(frame);
			row.AddChild(frame);
		}
	}

	private void Choose(string name, PanelContainer frame) {
		chosen = name;
		foreach (PanelContainer f in choiceFrames) f.AddThemeStyleboxOverride("panel", Rim(f == frame ? GOLD : DIM));
		continueButton.Disabled = false;
		continueButton.GrabFocus();
	}

	private static StyleBoxFlat Rim(Color colour) {
		var style = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0), BorderColor = colour };
		style.SetBorderWidthAll(3);
		style.SetCornerRadiusAll(6);
		style.SetContentMarginAll(6);
		return style;
	}

	private void AddTreasure(VBoxContainer box, Equipment item) {
		if (item.image != null) box.AddChild(MakeCard(item.image));
		box.AddChild(MakeLabel(item.name, 22, GOLD));
	}

	private void AddShard(VBoxContainer box, int held) {
		box.AddChild(MakeGlyph(LootGlyph.Shape.SHARD, new Vector2(120, 170)));
		box.AddChild(MakeLabel("+1", 46, GOLD));

		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 6);
		row.AddChild(MakeGlyph(LootGlyph.Shape.SHARD, new Vector2(18, 26)));
		row.AddChild(MakeLabel(held.ToString(), 20, RIM));
		box.AddChild(row);
	}

	private void AddSouls(VBoxContainer box, int souls) {
		Texture2D icon = ResourceLoader.Exists(SOUL_ICON_PATH) ? ResourceLoader.Load<Texture2D>(SOUL_ICON_PATH) : null;
		if (icon != null) {
			box.AddChild(new TextureRect {
				Texture = icon,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				CustomMinimumSize = new Vector2(110, 110),
				MouseFilter = Control.MouseFilterEnum.Ignore,
			});
		}
		box.AddChild(MakeLabel($"+{souls}", 46, GOLD));
	}

	// The card, then "+1" and how many the party now holds, as the shard shows.
	private void AddBone(VBoxContainer box, int held) {
		if (HomewardBone.Card != null) box.AddChild(MakeCard(HomewardBone.Card));
		box.AddChild(MakeLabel("+1", 46, GOLD));

		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 6);
		row.AddChild(new TextureRect {
			Texture = HomewardBone.Token,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(26, 26),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		});
		row.AddChild(MakeLabel(held.ToString(), 20, RIM));
		box.AddChild(row);
	}

	// A boss's key shows its printed card (Tools/CardEdits/edit_keys.py); a section's own key,
	// which has no card, the drawn glyph.
	private void AddKey(VBoxContainer box, string keyId) {
		string path = KEY_CARDS + keyId + ".png";
		if (ResourceLoader.Exists(path)) box.AddChild(MakeCard(ResourceLoader.Load<Texture2D>(path)));
		else box.AddChild(MakeGlyph(LootGlyph.Shape.KEY, new Vector2(100, 170)));
		box.AddChild(MakeLabel(keyId, 24, GOLD));
	}

	private static TextureRect MakeCard(Texture2D art) {
		float aspect = art.GetWidth() / (float)art.GetHeight();
		return new TextureRect {
			Texture = art,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(CARD_HEIGHT * aspect, CARD_HEIGHT),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
	}

	private static LootGlyph MakeGlyph(LootGlyph.Shape shape, Vector2 size) =>
		new LootGlyph { shape = shape, CustomMinimumSize = size, MouseFilter = Control.MouseFilterEnum.Ignore };

	private static Label MakeLabel(string text, int size, Color colour) {
		var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", colour);
		return label;
	}

	private void OnRootInput(InputEvent @event) {
		if (choiceFrames.Count > 0) return;
		if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed) {
			root.AcceptEvent();
			Dismiss();
		}
	}

	private void Dismiss() {
		if (dismissed) return;
		if (choiceFrames.Count > 0) {
			if (chosen == null) return;
			WorldMapManager.TakeBossCard(chosen);
		}
		dismissed = true;
		var tween = CreateTween();
		tween.TweenProperty(root, "modulate:a", 0f, FADE_OUT);
		tween.Finished += () => EmitSignal(SignalName.Dismissed);
	}
}
