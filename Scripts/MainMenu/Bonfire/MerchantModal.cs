using Godot;
using System.Linq;

// The Bonfire's Merchant (Merchant): the cards on show, face up, each with its price and a Buy.
// Under each card the party's faces say who could wield it: a gold ring now, a grey ring and a
// dimmed face once levelled (Tier 3 meets it), no face at all otherwise. A bought card leaves an empty frame until the stock
// turns over; the card itself goes to the party's inventory (the equipment modal).
public partial class MerchantModal : CanvasLayer {
	private static readonly Color GOLD = new Color(0.80f, 0.66f, 0.37f);
	private static readonly Color TEXT = new Color(0.84f, 0.82f, 0.75f);
	private static readonly Color PANEL_LINE = new Color(0.32f, 0.28f, 0.19f);
	private static readonly Color LATER = new Color(1f, 1f, 1f, 0.45f);
	private static readonly Color LATER_RING = new Color(0.45f, 0.44f, 0.42f);
	private const string SOUL_ICON = "res://Resources/Images/Sprites/Soul.png";
	private const int CARD_W = 140, CARD_H = 196, FACE = 26;

	[Signal] public delegate void ClosedEventHandler();

	private HFlowContainer cards;
	private Label soulsLabel;
	private Texture2D soulIcon;

	public override void _Ready() {
		Layer = 10;
		Visible = false;
		soulIcon = GD.Load<Texture2D>(SOUL_ICON);
		BuildUi();
	}

	public bool IsOpen() => Visible;

	public void Open() {
		Visible = true;
		Refresh();
	}

	public void Close() {
		Visible = false;
		EmitSignal(SignalName.Closed);
	}

	private void Buy(int slot) {
		if (!Merchant.Buy(slot)) return;
		CampaignManager.Autosave();
		Refresh();
	}

	private void Refresh() {
		soulsLabel.Text = SoulCache.current.ToString();
		foreach (Node child in cards.GetChildren()) child.QueueFree();
		string[] stock = Merchant.Stock();
		for (int i = 0; i < stock.Length; i++) cards.AddChild(Tile(i, GameManager.GetTemplate(stock[i])));
	}

	private Control Tile(int slot, Equipment item) {
		var tile = new VBoxContainer { CustomMinimumSize = new Vector2(CARD_W, 0) };
		tile.AddThemeConstantOverride("separation", 4);

		var frame = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Pass };
		var border = new StyleBoxFlat {
			BgColor = new Color(0.07f, 0.07f, 0.07f, 0.85f),
			BorderColor = item == null ? PANEL_LINE : EquipmentModal.RarityColor(item.rarity),
		};
		border.SetBorderWidthAll(2);
		border.SetCornerRadiusAll(4);
		frame.AddThemeStyleboxOverride("panel", border);
		frame.AddChild(new TextureRect {
			Texture = item?.image,
			CustomMinimumSize = new Vector2(CARD_W, CARD_H),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		});
		tile.AddChild(frame);
		if (item == null) return tile;
		frame.TooltipText = Describe(item);

		var name = new Label {
			Text = item.name,
			HorizontalAlignment = HorizontalAlignment.Center,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
			CustomMinimumSize = new Vector2(CARD_W, 0),
		};
		name.AddThemeFontSizeOverride("font_size", 13);
		name.AddThemeColorOverride("font_color", TEXT);
		tile.AddChild(name);

		var faces = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		faces.AddThemeConstantOverride("separation", 4);
		foreach (Player player in CampaignManager.Players) {
			bool now = player.MeetsRequirements(item);
			bool later = player.MeetsRequirements(item, Merchant.TopStats(player));
			if (later) faces.AddChild(Face(player, now));
		}
		tile.AddChild(faces);

		var buy = new Button {
			Text = SoulEconomy.CARD_PRICE.ToString(),
			Icon = soulIcon,
			ExpandIcon = false,
			CustomMinimumSize = new Vector2(0, 30),
			Disabled = SoulCache.current < SoulEconomy.CARD_PRICE,
		};
		buy.AddThemeConstantOverride("icon_max_width", 16);
		buy.Pressed += () => Buy(slot);
		tile.AddChild(buy);
		return tile;
	}

	private static Control Face(Player player, bool now) {
		var ring = new Panel { CustomMinimumSize = new Vector2(FACE, FACE), TooltipText = player.name };
		var style = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.6f), BorderColor = now ? GOLD : LATER_RING };
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(FACE / 2);
		ring.AddThemeStyleboxOverride("panel", style);
		var face = new TextureRect {
			Texture = player.character?.avatar,
			Material = PlayerToken.AggroMask,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Modulate = now ? Colors.White : LATER,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		face.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.KeepSize, 2);
		ring.AddChild(face);
		return ring;
	}

	private static string Describe(Equipment item) {
		var reqs = new[] { ("Str", item.strengthReq), ("Dex", item.dexterityReq), ("Int", item.intelligenceReq), ("Fai", item.faithReq) }
			.Where(r => r.Item2 > 0).Select(r => $"{r.Item1} {r.Item2}");
		string line = string.Join("  ", reqs);
		return $"{item.name}\n{item.type} · {item.rarity}" + (line.Length > 0 ? $"\n{line}" : "");
	}

	private void BuildUi() {
		var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.65f), MouseFilter = Control.MouseFilterEnum.Stop };
		backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddChild(backdrop);

		var frame = new PanelContainer();
		frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.KeepSize, 28);
		var frameStyle = new StyleBoxFlat { BgColor = new Color(0.055f, 0.052f, 0.05f, 0.99f), BorderColor = PANEL_LINE };
		frameStyle.SetBorderWidthAll(2);
		frameStyle.SetCornerRadiusAll(4);
		frameStyle.ContentMarginLeft = frameStyle.ContentMarginRight = 20;
		frameStyle.ContentMarginTop = frameStyle.ContentMarginBottom = 16;
		frame.AddThemeStyleboxOverride("panel", frameStyle);
		AddChild(frame);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 14);
		frame.AddChild(column);

		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 8);
		column.AddChild(header);
		var title = new Label { Text = "Merchant", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		title.AddThemeFontSizeOverride("font_size", 26);
		title.AddThemeColorOverride("font_color", GOLD);
		header.AddChild(title);
		header.AddChild(new TextureRect {
			Texture = soulIcon, CustomMinimumSize = new Vector2(20, 20),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
		});
		soulsLabel = new Label();
		soulsLabel.AddThemeFontSizeOverride("font_size", 20);
		soulsLabel.AddThemeColorOverride("font_color", TEXT);
		header.AddChild(soulsLabel);

		column.AddChild(new ColorRect { Color = PANEL_LINE, CustomMinimumSize = new Vector2(0, 1), MouseFilter = Control.MouseFilterEnum.Ignore });

		var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		column.AddChild(scroll);
		cards = new HFlowContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = FlowContainer.AlignmentMode.Center };
		cards.AddThemeConstantOverride("h_separation", 18);
		cards.AddThemeConstantOverride("v_separation", 18);
		scroll.AddChild(cards);

		var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
		column.AddChild(footer);
		var close = new Button { Text = "Close", CustomMinimumSize = new Vector2(88, 30) };
		close.Pressed += Close;
		footer.AddChild(close);
	}
}
