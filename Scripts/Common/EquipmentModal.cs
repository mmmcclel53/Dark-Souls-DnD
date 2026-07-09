using Godot;

// Autoloaded modal: full-screen overlay with three columns
// [ Character Summary 30% | Character Equipment 30% | Inventory 40% ].
//
// Open(Player) preselects a character; Open(null) leaves character selection
// to the user (they pick via the floating CharacterPortraitPane).
public partial class EquipmentModal : CanvasLayer
{
    public enum SlotKind {
        None,
        Backup, BackupUpgrade1, BackupUpgrade2,
        LeftHand, LeftHandUpgrade1, LeftHandUpgrade2,
        RightHand, RightHandUpgrade1, RightHandUpgrade2,
        Armour, ArmourUpgrade1, ArmourUpgrade2,
    }

    // ----- Dark Souls AAA palette -----
    private static readonly Color GOLD       = new Color(0.80f, 0.66f, 0.37f);
    private static readonly Color GOLD_DIM   = new Color(0.55f, 0.47f, 0.31f);
    private static readonly Color TEXT       = new Color(0.84f, 0.82f, 0.75f);
    private static readonly Color TEXT_DIM   = new Color(0.62f, 0.60f, 0.54f);
    private static readonly Color PANEL_BG   = new Color(0.085f, 0.082f, 0.078f, 0.98f);
    private static readonly Color PANEL_LINE = new Color(0.32f, 0.28f, 0.19f);

    // Comparison deltas: green = improvement, red = worse, dim = no change.
    public static readonly Color BETTER  = new Color(0.44f, 0.82f, 0.44f);
    public static readonly Color WORSE   = new Color(0.87f, 0.38f, 0.35f);
    public static readonly Color NEUTRAL = new Color(0.58f, 0.56f, 0.50f);

    // Rarity tint, shared across the inventory grid and comparison cards.
    public static Color RarityColor(Equipment.Rarity r) {
        switch (r) {
            case Equipment.Rarity.STARTER:   return new Color(0.62f, 0.62f, 0.62f);
            case Equipment.Rarity.COMMON:    return new Color(0.86f, 0.86f, 0.86f);
            case Equipment.Rarity.UNCOMMON:  return new Color(0.40f, 0.80f, 0.40f);
            case Equipment.Rarity.RARE:      return new Color(0.35f, 0.65f, 1.00f);
            case Equipment.Rarity.LEGENDARY: return new Color(0.75f, 0.35f, 0.95f);
            case Equipment.Rarity.EPIC:      return new Color(0.90f, 0.65f, 0.15f);
            default:                         return new Color(0.86f, 0.86f, 0.86f);
        }
    }

    // Formats a comparison delta into a label: "+N" green, "-N" red, "—" dim for zero.
    // No signed zeros — zero always renders as a neutral dash.
    public static void SetDeltaLabel(Label l, int delta) {
        if (l == null) return;
        if (delta > 0)      { l.Text = $"+{delta}"; l.AddThemeColorOverride("font_color", BETTER); }
        else if (delta < 0) { l.Text = delta.ToString(); l.AddThemeColorOverride("font_color", WORSE); }
        else                { l.Text = "—"; l.AddThemeColorOverride("font_color", NEUTRAL); }
    }

    private Control root;
    private CharacterSummaryPanel summaryPanel;
    private CharacterEquipmentPanel equipmentPanel;
    private InventoryPanel inventoryPanel;
    private ComparisonPanel comparisonPanel;
    private Button closeButton;
    private CheckButton devToggle;

    private Player currentPlayer;
    private SlotKind selectedSlot = SlotKind.None;
    private Equipment selectedItem;                 // an Equipment instance from the owned pool

    public override void _Ready() {
        Layer = 10;
        Visible = false;

        BuildUi();

        if (closeButton != null) closeButton.Pressed += () => Close();
        if (devToggle != null) devToggle.Toggled += on => inventoryPanel?.SetDevMode(on);
        if (equipmentPanel != null) equipmentPanel.SlotSelected += OnSlotSelected;
        if (inventoryPanel != null) inventoryPanel.EquipmentClicked += OnInventoryClicked;
        if (comparisonPanel != null) {
            comparisonPanel.Confirmed += OnEquipConfirmed;
            comparisonPanel.Cancelled += OnEquipCancelled;
        }

        // Listen to portrait clicks so we can let the user reassign while inside the modal.
        var pane = GetNodeOrNull<CharacterPortraitPane>("/root/CharacterPortraitPane");
        if (pane != null) pane.PortraitClicked += OnPortraitClicked;
    }

    private void BuildUi() {
        // Dim the rest of the screen.
        var backdrop = new ColorRect();
        backdrop.Color = new Color(0, 0, 0, 0.65f);
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        backdrop.MouseFilter = Control.MouseFilterEnum.Stop;
        AddChild(backdrop);

        // Frame: near-full screen, dark panel with a warm gold border.
        var frame = new PanelContainer();
        frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.KeepSize, 28);
        var frameStyle = new StyleBoxFlat();
        frameStyle.BgColor = new Color(0.055f, 0.052f, 0.05f, 0.99f);
        frameStyle.BorderColor = PANEL_LINE;
        frameStyle.SetBorderWidthAll(2);
        frameStyle.SetCornerRadiusAll(4);
        frameStyle.ContentMarginLeft = frameStyle.ContentMarginRight = 20;
        frameStyle.ContentMarginTop = frameStyle.ContentMarginBottom = 16;
        frame.AddThemeStyleboxOverride("panel", frameStyle);
        AddChild(frame);
        root = frame;

        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        vbox.SizeFlagsVertical = Control.SizeFlags.Fill;
        vbox.AddThemeConstantOverride("separation", 12);
        frame.AddChild(vbox);

        // Header: title + Dev Mode toggle + close button.
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 12);
        var title = new Label();
        title.Text = "EQUIPMENT";
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", GOLD);
        header.AddChild(title);

        devToggle = new CheckButton();
        devToggle.Text = "Dev Mode";
        devToggle.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        devToggle.AddThemeColorOverride("font_color", TEXT_DIM);
        devToggle.TooltipText = "Reveal every catalog card (preview only — not owned or equippable)";
        header.AddChild(devToggle);

        closeButton = new Button();
        closeButton.Text = "✕";
        closeButton.CustomMinimumSize = new Vector2(36, 36);
        closeButton.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        header.AddChild(closeButton);
        vbox.AddChild(header);

        vbox.AddChild(MakeDivider());

        // Three columns: 30 / 30 / 40.
        var columns = new HBoxContainer();
        columns.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        columns.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        columns.AddThemeConstantOverride("separation", 12);
        vbox.AddChild(columns);

        summaryPanel = new CharacterSummaryPanel();
        summaryPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        summaryPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        summaryPanel.SizeFlagsStretchRatio = 3.0f;
        BuildSummaryChildren(summaryPanel);
        columns.AddChild(summaryPanel);

        equipmentPanel = new CharacterEquipmentPanel();
        equipmentPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        equipmentPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        equipmentPanel.SizeFlagsStretchRatio = 3.0f;
        BuildEquipmentChildren(equipmentPanel);
        columns.AddChild(equipmentPanel);

        inventoryPanel = new InventoryPanel();
        inventoryPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        inventoryPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        inventoryPanel.SizeFlagsStretchRatio = 4.0f;
        BuildInventoryChildren(inventoryPanel);
        columns.AddChild(inventoryPanel);

        // Comparison overlay fills the loadout column while an item is being previewed.
        comparisonPanel = new ComparisonPanel();
        comparisonPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.KeepSize, 2);
        var cmpStyle = new StyleBoxFlat();
        cmpStyle.BgColor = new Color(0.1f, 0.095f, 0.088f, 0.99f);
        cmpStyle.BorderColor = GOLD_DIM;
        cmpStyle.SetBorderWidthAll(1);
        cmpStyle.SetCornerRadiusAll(4);
        cmpStyle.ContentMarginLeft = cmpStyle.ContentMarginRight = 10;
        cmpStyle.ContentMarginTop = cmpStyle.ContentMarginBottom = 10;
        comparisonPanel.AddThemeStyleboxOverride("panel", cmpStyle);
        BuildComparisonChildren(comparisonPanel);
        equipmentPanel.AddChild(comparisonPanel);
    }

    // ----- Column constructors (each instantiates the panel's children + wires its [Export] refs) -----

    private static void BuildSummaryChildren(CharacterSummaryPanel s) {
        var outer = WrapColumn(s, null);

        // Scrollable so the extra sections never clip on a small window.
        var scroll = new ScrollContainer();
        scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        outer.AddChild(scroll);

        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        vbox.AddThemeConstantOverride("separation", 5);
        scroll.AddChild(vbox);

        s.playerNameLabel = AddLabel(vbox, "", 19, HorizontalAlignment.Center, GOLD);

        // Framed avatar portrait — trimmed height to keep the panel scroll-free by default.
        var avatarFrame = new PanelContainer();
        avatarFrame.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        avatarFrame.AddThemeStyleboxOverride("panel", SlotFrameStyle());
        vbox.AddChild(avatarFrame);
        s.avatarRect = new TextureRect();
        s.avatarRect.CustomMinimumSize = new Vector2(124, 148);
        s.avatarRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        s.avatarRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        avatarFrame.AddChild(s.avatarRect);

        s.levelClassLabel = AddLabel(vbox, "", 14, HorizontalAlignment.Center, GOLD_DIM);

        // Attributes — headers on one row, values directly beneath (BG3-style).
        vbox.AddChild(MakeOrnamentHeader("Attributes"));
        var attrGrid = new GridContainer();
        attrGrid.Columns = 4;
        attrGrid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        attrGrid.AddThemeConstantOverride("h_separation", 6);
        attrGrid.AddThemeConstantOverride("v_separation", 2);
        vbox.AddChild(attrGrid);
        AddAttrHeader(attrGrid, "STR");
        AddAttrHeader(attrGrid, "DEX");
        AddAttrHeader(attrGrid, "INT");
        AddAttrHeader(attrGrid, "FAI");
        s.strLabel = AddAttrValue(attrGrid);
        s.dexLabel = AddAttrValue(attrGrid);
        s.intLabel = AddAttrValue(attrGrid);
        s.faiLabel = AddAttrValue(attrGrid);

        // Combat table: rows = Damage / Defense, columns = Phys / Mag, with sword/shield glyphs.
        vbox.AddChild(MakeOrnamentHeader("Combat"));
        var combat = new GridContainer();
        combat.Columns = 3;
        combat.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        combat.AddThemeConstantOverride("h_separation", 8);
        combat.AddThemeConstantOverride("v_separation", 2);
        vbox.AddChild(combat);

        combat.AddChild(new Control());                 // empty top-left corner
        AddCombatColHeader(combat, "Phys");
        AddCombatColHeader(combat, "Mag");

        combat.AddChild(MakeIconRow(VectorIcon.IconKind.Sword, "Damage"));
        s.physDamageLabel  = AddCombatValue(combat);
        s.magicDamageLabel = AddCombatValue(combat);

        combat.AddChild(MakeIconRow(VectorIcon.IconKind.Shield, "Defense"));
        s.physDefenseLabel = AddCombatValue(combat);
        s.magicDefenseLabel = AddCombatValue(combat);

        s.dodgeLabel = AddLabel(vbox, "", 13, HorizontalAlignment.Center, TEXT_DIM);

        vbox.AddChild(MakeOrnamentHeader("Immunities"));
        var immRow = new HBoxContainer();
        immRow.Alignment = BoxContainer.AlignmentMode.Center;
        immRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        immRow.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(immRow);
        s.immunitiesRow = immRow;

        vbox.AddChild(MakeOrnamentHeader("Notable Features"));
        var features = new VBoxContainer();
        features.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        features.AddThemeConstantOverride("separation", 3);
        vbox.AddChild(features);
        s.featuresList = features;
    }

    private static void AddAttrHeader(Container parent, string text) {
        var l = AddLabel(parent, text, 12, HorizontalAlignment.Center, GOLD_DIM);
        l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
    }

    private static Label AddAttrValue(Container parent) {
        var l = AddLabel(parent, "", 16, HorizontalAlignment.Center, TEXT);
        l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return l;
    }

    private static void AddCombatColHeader(Container parent, string text) {
        var l = AddLabel(parent, text, 12, HorizontalAlignment.Center, GOLD_DIM);
        l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
    }

    private static Label AddCombatValue(Container parent) {
        var l = AddLabel(parent, "", 13, HorizontalAlignment.Center, TEXT);
        l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return l;
    }

    // A row-label cell: sword/shield glyph followed by "Damage"/"Defense".
    private static Control MakeIconRow(VectorIcon.IconKind kind, string text) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        var icon = new VectorIcon();
        icon.kind = kind;
        icon.CustomMinimumSize = new Vector2(18, 18);
        icon.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        icon.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(icon);
        AddLabel(row, text, 13, HorizontalAlignment.Left, TEXT).SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        return row;
    }

    // "─◆ Title ◆─" ornamental section header: gold label flanked by divider rules.
    private static Control MakeOrnamentHeader(string title) {
        var row = new HBoxContainer();
        row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddThemeConstantOverride("separation", 8);

        var lineL = MakeDivider();
        lineL.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        lineL.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(lineL);

        var label = AddLabel(row, title, 12, HorizontalAlignment.Center, GOLD_DIM);
        label.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;

        var lineR = MakeDivider();
        lineR.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        lineR.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(lineR);

        // Small pad above the header so sections breathe.
        var wrap = new VBoxContainer();
        wrap.AddThemeConstantOverride("separation", 4);
        var pad = new Control();
        pad.CustomMinimumSize = new Vector2(0, 4);
        wrap.AddChild(pad);
        wrap.AddChild(row);
        return wrap;
    }

    private static void BuildEquipmentChildren(CharacterEquipmentPanel e) {
        e.background = null;                        // no more character-art backdrop
        var vbox = WrapColumn(e, "LOADOUT");

        // Weapons side by side.
        var hands = new HBoxContainer();
        hands.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hands.AddThemeConstantOverride("separation", 10);
        vbox.AddChild(hands);
        hands.AddChild(BuildSlotCard("LEFT HAND", 72, 30, out e.leftHandButton,
                                     out e.leftHandUpgrade1Button, out e.leftHandUpgrade2Button));
        hands.AddChild(BuildSlotCard("RIGHT HAND", 72, 30, out e.rightHandButton,
                                     out e.rightHandUpgrade1Button, out e.rightHandUpgrade2Button));

        vbox.AddChild(BuildSlotCard("ARMOUR", 80, 32, out e.armourButton,
                                    out e.armourUpgrade1Button, out e.armourUpgrade2Button));
        vbox.AddChild(BuildSlotCard("BACKUP", 72, 30, out e.backupSlotButton,
                                    out e.backupUpgrade1Button, out e.backupUpgrade2Button));

        // Push cards to the top; leave breathing room below for the comparison overlay.
        var spacer = new Control();
        spacer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        vbox.AddChild(spacer);
    }

    // A framed loadout card: title, a main slot square, and two upgrade squares stacked beside it.
    private static Control BuildSlotCard(string title, int mainSize, int upSize,
                                         out TextureButton main, out TextureButton up1, out TextureButton up2) {
        var card = new PanelContainer();
        card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        card.AddThemeStyleboxOverride("panel", SlotCardStyle());

        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 6);
        card.AddChild(v);

        AddLabel(v, title, 12, HorizontalAlignment.Center, GOLD_DIM);

        var row = new HBoxContainer();
        row.Alignment = BoxContainer.AlignmentMode.Center;
        row.AddThemeConstantOverride("separation", 8);
        v.AddChild(row);

        main = MakeSlotButton(mainSize);
        row.AddChild(main);

        var upCol = new VBoxContainer();
        upCol.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        upCol.AddThemeConstantOverride("separation", 6);
        row.AddChild(upCol);
        up1 = MakeSlotButton(upSize);
        up2 = MakeSlotButton(upSize);
        upCol.AddChild(up1);
        upCol.AddChild(up2);

        return card;
    }

    // Container-friendly slot button with an inset framed border. Hiding the button hides its frame.
    private static TextureButton MakeSlotButton(int size) {
        var b = new TextureButton();
        b.IgnoreTextureSize = true;
        b.StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered;
        b.CustomMinimumSize = new Vector2(size, size);
        var border = new Panel();
        border.MouseFilter = Control.MouseFilterEnum.Ignore;
        border.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        border.AddThemeStyleboxOverride("panel", SlotFrameStyle());
        b.AddChild(border);
        return b;
    }

    private static void BuildInventoryChildren(InventoryPanel inv) {
        inv.ClipContents = true;                    // never let controls spill past the column
        var vbox = WrapColumn(inv, "INVENTORY");

        // Search.
        inv.searchField = new LineEdit();
        inv.searchField.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        inv.searchField.PlaceholderText = "Search name, type, rarity…";
        vbox.AddChild(inv.searchField);

        // Filters: Type + Rarity + Sort on a single row, equal width.
        var filterRow = new HBoxContainer();
        filterRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        filterRow.AddThemeConstantOverride("separation", 8);
        inv.typeFilter = MakeFilterOption();
        inv.rarityFilter = MakeFilterOption();
        inv.sortSelect = MakeFilterOption();
        filterRow.AddChild(inv.typeFilter);
        filterRow.AddChild(inv.rarityFilter);
        filterRow.AddChild(inv.sortSelect);
        vbox.AddChild(filterRow);

        // Scrollable grid (vertical only).
        var scroll = new ScrollContainer();
        scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        vbox.AddChild(scroll);

        inv.grid = new GridContainer();
        inv.grid.Columns = 5;
        inv.grid.AddThemeConstantOverride("h_separation", 6);
        inv.grid.AddThemeConstantOverride("v_separation", 6);
        inv.grid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(inv.grid);
    }

    private static OptionButton MakeFilterOption() {
        var o = new OptionButton();
        o.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        o.ClipText = true;                          // shrink to share row width instead of overflowing
        o.CustomMinimumSize = new Vector2(60, 0);
        o.AddThemeFontSizeOverride("font_size", 13);
        return o;
    }

    private static void BuildComparisonChildren(ComparisonPanel cmp) {
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (string m in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(m, 6);
        cmp.AddChild(margin);

        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 8);
        margin.AddChild(v);

        cmp.headerLabel = AddLabel(v, "Compare", 18, HorizontalAlignment.Center, GOLD);
        v.AddChild(MakeDivider());

        // Current | Proposed cards, side by side.
        var pair = new HBoxContainer();
        pair.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        pair.AddThemeConstantOverride("separation", 8);
        v.AddChild(pair);
        BuildCompareColumn(pair, "CURRENT", out cmp.currentIconFrame, out cmp.currentIcon,
                           out cmp.currentNameLabel, out cmp.currentStatsLabel);
        BuildCompareColumn(pair, "PROPOSED", out cmp.incomingIconFrame, out cmp.incomingIcon,
                           out cmp.incomingNameLabel, out cmp.incomingStatsLabel);

        v.AddChild(MakeDivider());
        AddLabel(v, "CHANGES", 12, HorizontalAlignment.Center, GOLD_DIM);

        var grid = new GridContainer();
        grid.Columns = 2;
        grid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 4);
        v.AddChild(grid);
        cmp.physDmgDelta  = AddDeltaRow(grid, "Physical Damage");
        cmp.magDmgDelta   = AddDeltaRow(grid, "Magic Damage");
        cmp.physDefDelta  = AddDeltaRow(grid, "Physical Defense");
        cmp.magDefDelta   = AddDeltaRow(grid, "Magic Defense");
        cmp.dodgeDelta    = AddDeltaRow(grid, "Dodge");

        var spacer = new Control();
        spacer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        v.AddChild(spacer);

        var actions = new HBoxContainer();
        actions.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        actions.Alignment = BoxContainer.AlignmentMode.Center;
        actions.AddThemeConstantOverride("separation", 16);
        cmp.confirmButton = new Button();
        cmp.confirmButton.Text = "Equip";
        cmp.confirmButton.CustomMinimumSize = new Vector2(110, 38);
        cmp.confirmButton.AddThemeColorOverride("font_color", GOLD);
        actions.AddChild(cmp.confirmButton);
        cmp.cancelButton = new Button();
        cmp.cancelButton.Text = "Cancel";
        cmp.cancelButton.CustomMinimumSize = new Vector2(90, 38);
        cmp.cancelButton.AddThemeColorOverride("font_color", TEXT_DIM);
        actions.AddChild(cmp.cancelButton);
        v.AddChild(actions);
    }

    private static void BuildCompareColumn(HBoxContainer parent, string title,
                                           out PanelContainer frame, out TextureRect icon,
                                           out Label name, out Label stats) {
        var col = new VBoxContainer();
        col.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        col.AddThemeConstantOverride("separation", 4);
        parent.AddChild(col);

        AddLabel(col, title, 11, HorizontalAlignment.Center, GOLD_DIM);

        frame = new PanelContainer();
        frame.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        frame.AddThemeStyleboxOverride("panel", SlotFrameStyle());
        col.AddChild(frame);

        icon = new TextureRect();
        icon.CustomMinimumSize = new Vector2(104, 104);
        icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        frame.AddChild(icon);

        name = AddLabel(col, "", 14, HorizontalAlignment.Center);
        name.AutowrapMode = TextServer.AutowrapMode.Word;
        stats = AddLabel(col, "", 10, HorizontalAlignment.Center, TEXT_DIM);
        stats.AutowrapMode = TextServer.AutowrapMode.Word;
    }

    // A "Stat name | value" row in the changes grid; returns the (initially dim) value label.
    private static Label AddDeltaRow(GridContainer grid, string label) {
        AddLabel(grid, label, 12, HorizontalAlignment.Left, TEXT);
        var val = AddLabel(grid, "—", 13, HorizontalAlignment.Right, NEUTRAL);
        val.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return val;
    }

    private static Label AddLabel(Container parent, string text, int fontSize,
                                  HorizontalAlignment align = HorizontalAlignment.Left, Color? color = null) {
        var l = new Label();
        l.Text = text;
        l.AddThemeFontSizeOverride("font_size", fontSize);
        l.HorizontalAlignment = align;
        if (color.HasValue) l.AddThemeColorOverride("font_color", color.Value);
        parent.AddChild(l);
        return l;
    }

    // ----- Shared AAA styling helpers -----

    // Wraps a column Control in a framed panel + margin, returns the content VBox.
    // Adds a gold section header when heading is non-null.
    private static VBoxContainer WrapColumn(Control col, string heading) {
        var frame = new PanelContainer();
        frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        frame.AddThemeStyleboxOverride("panel", ColumnStyle());
        col.AddChild(frame);

        var margin = new MarginContainer();
        foreach (string m in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(m, 14);
        frame.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);
        margin.AddChild(vbox);

        if (heading != null) {
            AddLabel(vbox, heading, 15, HorizontalAlignment.Center, GOLD);
            vbox.AddChild(MakeDivider());
        }
        return vbox;
    }

    private static Control MakeDivider() {
        var line = new Panel();
        line.CustomMinimumSize = new Vector2(0, 2);
        line.MouseFilter = Control.MouseFilterEnum.Ignore;
        var st = new StyleBoxFlat();
        st.BgColor = PANEL_LINE;
        line.AddThemeStyleboxOverride("panel", st);
        return line;
    }

    private static StyleBoxFlat ColumnStyle() {
        var st = new StyleBoxFlat();
        st.BgColor = PANEL_BG;
        st.BorderColor = PANEL_LINE;
        st.SetBorderWidthAll(1);
        st.SetCornerRadiusAll(4);
        return st;
    }

    private static StyleBoxFlat SlotCardStyle() {
        var st = new StyleBoxFlat();
        st.BgColor = new Color(0.12f, 0.115f, 0.10f, 0.9f);
        st.BorderColor = PANEL_LINE;
        st.SetBorderWidthAll(1);
        st.SetCornerRadiusAll(4);
        st.ContentMarginLeft = st.ContentMarginRight = 8;
        st.ContentMarginTop = st.ContentMarginBottom = 8;
        return st;
    }

    private static StyleBoxFlat SlotFrameStyle() {
        var st = new StyleBoxFlat();
        st.BgColor = new Color(0.03f, 0.03f, 0.03f, 0.85f);
        st.BorderColor = GOLD_DIM;
        st.SetBorderWidthAll(1);
        st.SetCornerRadiusAll(3);
        return st;
    }

    public void Open(Player p) {
        if (root == null) return;
        currentPlayer = p;
        selectedSlot = SlotKind.None;
        selectedItem = null;
        Visible = true;
        RefreshAll();
        UpdatePortraitSelection();
    }

    public void Close() {
        Visible = false;
        currentPlayer = null;
        selectedSlot = SlotKind.None;
        selectedItem = null;
        comparisonPanel?.HidePanel();
        UpdatePortraitSelection();
    }

    public bool IsOpen() => Visible;

    private void OnPortraitClicked(int playerIndex) {
        if (!Visible) return;                       // bonfire owns portrait clicks when modal isn't open
        var players = CampaignManager.Players;
        if (playerIndex < 0 || playerIndex >= players.Length) return;
        // Toggle-off if clicking the same character.
        if (currentPlayer == players[playerIndex]) {
            Close();
            return;
        }
        currentPlayer = players[playerIndex];
        selectedSlot = SlotKind.None;
        selectedItem = null;
        comparisonPanel?.HidePanel();
        RefreshAll();
        UpdatePortraitSelection();
    }

    private void OnSlotSelected(int slotKindInt) {
        selectedSlot = (SlotKind)slotKindInt;
        selectedItem = null;
        comparisonPanel?.HidePanel();
        inventoryPanel?.SetSlotFilter(selectedSlot, currentPlayer);
        equipmentPanel?.SetSelectedSlot(selectedSlot);
    }

    private void OnInventoryClicked(string key) {
        // key is either an owned instance id or (for dev-preview cards) a template name.
        Equipment item = GameManager.GetInstance(key) ?? GameManager.GetTemplate(key);
        selectedItem = item;
        if (item == null) return;
        equipmentPanel?.HighlightCompatibleSlots(item);

        // If a specific slot is already chosen and compatible, show comparison directly.
        SlotKind slot = selectedSlot;
        if (slot == SlotKind.None || !SlotAccepts(slot, item, currentPlayer)) {
            slot = FirstCompatibleSlot(item, currentPlayer);
        }
        if (slot == SlotKind.None || currentPlayer == null) return;

        Equipment current = GetEquipped(currentPlayer, slot);
        comparisonPanel?.ShowComparison(currentPlayer, slot, current, item);
    }

    private void OnEquipConfirmed(int slotKindInt, string newInstanceId) {
        if (currentPlayer == null) return;
        SlotKind slot = (SlotKind)slotKindInt;

        // Normal path: incoming is an owned instance. Dev-preview path: incoming is a
        // catalog template (no id yet) — mint a real owned instance so it can be equipped.
        Equipment incoming = GameManager.GetInstance(newInstanceId);
        if (incoming == null && selectedItem != null && string.IsNullOrEmpty(selectedItem.id))
            incoming = GameManager.MintInstance(selectedItem.name);
        if (incoming == null) return;

        // Outgoing returns to the pool; incoming was already in the pool — just rewire the slot ref.
        SetEquipped(currentPlayer, slot, incoming);
        // (Pool membership is unchanged — both are owned instances. Inventory display filters out equipped.)

        selectedSlot = SlotKind.None;
        selectedItem = null;
        comparisonPanel?.HidePanel();
        RefreshAll();
        RefreshPortraitForCurrent();
    }

    private void OnEquipCancelled() {
        comparisonPanel?.HidePanel();
        selectedItem = null;
    }

    private void RefreshAll() {
        summaryPanel?.SetPlayer(currentPlayer);
        equipmentPanel?.SetPlayer(currentPlayer);
        equipmentPanel?.SetSelectedSlot(selectedSlot);
        inventoryPanel?.SetSlotFilter(selectedSlot, currentPlayer);
        inventoryPanel?.Refresh();
    }

    private void UpdatePortraitSelection() {
        var pane = GetNodeOrNull<CharacterPortraitPane>("/root/CharacterPortraitPane");
        if (pane == null) return;
        if (currentPlayer == null) { pane.SetSelectedIndex(-1); return; }
        var players = CampaignManager.Players;
        for (int i = 0; i < players.Length; i++)
            if (players[i] == currentPlayer) { pane.SetSelectedIndex(i); return; }
        pane.SetSelectedIndex(-1);
    }

    private void RefreshPortraitForCurrent() {
        var pane = GetNodeOrNull<CharacterPortraitPane>("/root/CharacterPortraitPane");
        if (pane == null || currentPlayer == null) return;
        var players = CampaignManager.Players;
        for (int i = 0; i < players.Length; i++)
            if (players[i] == currentPlayer) { pane.RefreshPortrait(i); return; }
    }

    // ----- Slot/equipment helpers (shared with the column panels) -----

    public static bool SlotAccepts(SlotKind slot, Equipment item, Player p) {
        if (item == null) return false;
        switch (slot) {
            case SlotKind.Armour:
                return item.type == Equipment.EquipmentType.Armour;
            case SlotKind.Backup:
            case SlotKind.LeftHand:
            case SlotKind.RightHand:
                return item.type == Equipment.EquipmentType.Weapon
                    || item.type == Equipment.EquipmentType.Shield
                    || item.type == Equipment.EquipmentType.Spell
                    || item.type == Equipment.EquipmentType.Item;
            case SlotKind.BackupUpgrade1:
            case SlotKind.BackupUpgrade2:
            case SlotKind.LeftHandUpgrade1:
            case SlotKind.LeftHandUpgrade2:
            case SlotKind.RightHandUpgrade1:
            case SlotKind.RightHandUpgrade2:
                return item.type == Equipment.EquipmentType.Gem;     // weapon upgrades
            case SlotKind.ArmourUpgrade1:
            case SlotKind.ArmourUpgrade2:
                return item.type == Equipment.EquipmentType.Ring;    // armour upgrades
            default:
                return false;
        }
    }

    public static SlotKind FirstCompatibleSlot(Equipment item, Player p) {
        if (item == null || p == null) return SlotKind.None;
        switch (item.type) {
            case Equipment.EquipmentType.Armour:
                return SlotKind.Armour;
            case Equipment.EquipmentType.Weapon:
            case Equipment.EquipmentType.Shield:
            case Equipment.EquipmentType.Spell:
            case Equipment.EquipmentType.Item:
                return SlotKind.LeftHand;
            case Equipment.EquipmentType.Gem:
                return SlotKind.LeftHandUpgrade1;
            case Equipment.EquipmentType.Ring:
                return SlotKind.ArmourUpgrade1;
            default:
                return SlotKind.None;
        }
    }

    public static Equipment GetEquipped(Player p, SlotKind slot) {
        if (p == null) return null;
        switch (slot) {
            case SlotKind.Backup:    return p.GetBackupSlot();
            case SlotKind.LeftHand:  return p.GetLeftHand();
            case SlotKind.RightHand: return p.GetRightHand();
            case SlotKind.Armour:    return p.GetArmour();
            case SlotKind.BackupUpgrade1:    return UpgradeAt(p.backupUpgradeIds, 0);
            case SlotKind.BackupUpgrade2:    return UpgradeAt(p.backupUpgradeIds, 1);
            case SlotKind.LeftHandUpgrade1:  return UpgradeAt(p.leftHandUpgradeIds, 0);
            case SlotKind.LeftHandUpgrade2:  return UpgradeAt(p.leftHandUpgradeIds, 1);
            case SlotKind.RightHandUpgrade1: return UpgradeAt(p.rightHandUpgradeIds, 0);
            case SlotKind.RightHandUpgrade2: return UpgradeAt(p.rightHandUpgradeIds, 1);
            case SlotKind.ArmourUpgrade1:    return UpgradeAt(p.armourUpgradeIds, 0);
            case SlotKind.ArmourUpgrade2:    return UpgradeAt(p.armourUpgradeIds, 1);
            default: return null;
        }
    }

    public static void SetEquipped(Player p, SlotKind slot, Equipment item) {
        if (p == null) return;
        string id = item?.id ?? "";
        switch (slot) {
            case SlotKind.Backup:    p.backupSlotId = id; break;
            case SlotKind.LeftHand:  p.leftHandId   = id; break;
            case SlotKind.RightHand: p.rightHandId  = id; break;
            case SlotKind.Armour:    p.armourId     = id; break;
            case SlotKind.BackupUpgrade1:    SetUpgradeAt(ref p.backupUpgradeIds, 0, id); break;
            case SlotKind.BackupUpgrade2:    SetUpgradeAt(ref p.backupUpgradeIds, 1, id); break;
            case SlotKind.LeftHandUpgrade1:  SetUpgradeAt(ref p.leftHandUpgradeIds, 0, id); break;
            case SlotKind.LeftHandUpgrade2:  SetUpgradeAt(ref p.leftHandUpgradeIds, 1, id); break;
            case SlotKind.RightHandUpgrade1: SetUpgradeAt(ref p.rightHandUpgradeIds, 0, id); break;
            case SlotKind.RightHandUpgrade2: SetUpgradeAt(ref p.rightHandUpgradeIds, 1, id); break;
            case SlotKind.ArmourUpgrade1:    SetUpgradeAt(ref p.armourUpgradeIds, 0, id); break;
            case SlotKind.ArmourUpgrade2:    SetUpgradeAt(ref p.armourUpgradeIds, 1, id); break;
        }
    }

    private static Equipment UpgradeAt(string[] arr, int index) {
        if (arr == null || index < 0 || index >= arr.Length) return null;
        return GameManager.GetInstance(arr[index]);
    }

    private static void SetUpgradeAt(ref string[] arr, int index, string id) {
        if (arr == null) arr = new string[0];
        if (arr.Length <= index) {
            var grown = new string[index + 1];
            for (int i = 0; i < arr.Length; i++) grown[i] = arr[i];
            arr = grown;
        }
        arr[index] = id;
    }
}
