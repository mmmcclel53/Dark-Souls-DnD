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

    private Control root;
    private CharacterSummaryPanel summaryPanel;
    private CharacterEquipmentPanel equipmentPanel;
    private InventoryPanel inventoryPanel;
    private ComparisonPanel comparisonPanel;
    private Button closeButton;

    private Player currentPlayer;
    private SlotKind selectedSlot = SlotKind.None;
    private Equipment selectedItem;                 // an Equipment instance from the owned pool

    public override void _Ready() {
        Layer = 10;
        Visible = false;

        BuildUi();

        if (closeButton != null) closeButton.Pressed += () => Close();
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

        // Frame: 90% of screen, dark panel.
        var frame = new PanelContainer();
        frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.KeepSize, 32);
        var frameStyle = new StyleBoxFlat();
        frameStyle.BgColor = new Color(0.07f, 0.07f, 0.08f, 0.97f);
        frameStyle.BorderColor = new Color(0.35f, 0.35f, 0.35f, 1);
        frameStyle.SetBorderWidthAll(2);
        frameStyle.SetCornerRadiusAll(8);
        frameStyle.ContentMarginLeft = frameStyle.ContentMarginRight = 16;
        frameStyle.ContentMarginTop = frameStyle.ContentMarginBottom = 12;
        frame.AddThemeStyleboxOverride("panel", frameStyle);
        AddChild(frame);
        root = frame;

        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        vbox.SizeFlagsVertical = Control.SizeFlags.Fill;
        vbox.AddThemeConstantOverride("separation", 8);
        frame.AddChild(vbox);

        // Header: title + close button.
        var header = new HBoxContainer();
        var title = new Label();
        title.Text = "Equipment";
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.AddThemeFontSizeOverride("font_size", 22);
        header.AddChild(title);
        closeButton = new Button();
        closeButton.Text = "X";
        closeButton.CustomMinimumSize = new Vector2(32, 32);
        header.AddChild(closeButton);
        vbox.AddChild(header);

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

        // Comparison overlay (sits on top of the equipment column, slides in from right).
        comparisonPanel = new ComparisonPanel();
        comparisonPanel.SetAnchorsPreset(Control.LayoutPreset.CenterRight);
        comparisonPanel.CustomMinimumSize = new Vector2(260, 280);
        comparisonPanel.OffsetLeft = -270;
        comparisonPanel.OffsetTop = -140;
        comparisonPanel.OffsetRight = -10;
        comparisonPanel.OffsetBottom = 140;
        var cmpStyle = new StyleBoxFlat();
        cmpStyle.BgColor = new Color(0.1f, 0.1f, 0.12f, 0.97f);
        cmpStyle.BorderColor = new Color(0.5f, 0.5f, 0.55f, 1);
        cmpStyle.SetBorderWidthAll(1);
        cmpStyle.SetCornerRadiusAll(6);
        cmpStyle.ContentMarginLeft = cmpStyle.ContentMarginRight = 10;
        cmpStyle.ContentMarginTop = cmpStyle.ContentMarginBottom = 10;
        comparisonPanel.AddThemeStyleboxOverride("panel", cmpStyle);
        BuildComparisonChildren(comparisonPanel);
        equipmentPanel.AddChild(comparisonPanel);
    }

    // ----- Column constructors (each instantiates the panel's children + wires its [Export] refs) -----

    private static void BuildSummaryChildren(CharacterSummaryPanel s) {
        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("separation", 6);
        s.AddChild(vbox);

        s.playerNameLabel = AddLabel(vbox, "", 20, HorizontalAlignment.Center);

        s.avatarRect = new TextureRect();
        s.avatarRect.CustomMinimumSize = new Vector2(120, 160);
        s.avatarRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        s.avatarRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        vbox.AddChild(s.avatarRect);

        s.classNameLabel = AddLabel(vbox, "", 14, HorizontalAlignment.Center);
        s.descriptionLabel = AddLabel(vbox, "", 11);
        s.descriptionLabel.AutowrapMode = TextServer.AutowrapMode.Word;

        AddLabel(vbox, "— Stats —", 12, HorizontalAlignment.Center);
        var statsRow = new GridContainer();
        statsRow.Columns = 2;
        vbox.AddChild(statsRow);
        s.strLabel = AddLabel(statsRow, "STR", 12);
        s.dexLabel = AddLabel(statsRow, "DEX", 12);
        s.intLabel = AddLabel(statsRow, "INT", 12);
        s.faiLabel = AddLabel(statsRow, "FAI", 12);

        AddLabel(vbox, "— Summary —", 12, HorizontalAlignment.Center);
        s.physDamageLabel  = AddLabel(vbox, "", 12);
        s.magicDamageLabel = AddLabel(vbox, "", 12);
        s.physDefenseLabel = AddLabel(vbox, "", 12);
        s.magicDefenseLabel = AddLabel(vbox, "", 12);
        s.dodgeLabel       = AddLabel(vbox, "", 12);
    }

    private static void BuildEquipmentChildren(CharacterEquipmentPanel e) {
        // Background avatar.
        var bg = new TextureRect();
        bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        bg.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        bg.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        bg.Modulate = new Color(1, 1, 1, 0.35f);
        e.AddChild(bg);
        e.background = bg;

        // Slot anchors relative to the panel: Backup top, Left/Right middle, Armour bottom.
        AddSlotCluster(e, new Vector2(0.42f, 0.04f), out e.backupSlotButton,
                       out e.backupUpgrade1Button, out e.backupUpgrade2Button);
        AddSlotCluster(e, new Vector2(0.04f, 0.30f), out e.leftHandButton,
                       out e.leftHandUpgrade1Button, out e.leftHandUpgrade2Button);
        AddSlotCluster(e, new Vector2(0.78f, 0.30f), out e.rightHandButton,
                       out e.rightHandUpgrade1Button, out e.rightHandUpgrade2Button);
        AddSlotCluster(e, new Vector2(0.42f, 0.66f), out e.armourButton,
                       out e.armourUpgrade1Button, out e.armourUpgrade2Button);
    }

    private static void AddSlotCluster(Control parent, Vector2 anchorTopLeft,
                                       out TextureButton main, out TextureButton up1, out TextureButton up2) {
        var cluster = new Control();
        cluster.AnchorLeft = anchorTopLeft.X;
        cluster.AnchorTop = anchorTopLeft.Y;
        cluster.AnchorRight = anchorTopLeft.X + 0.18f;
        cluster.AnchorBottom = anchorTopLeft.Y + 0.28f;
        parent.AddChild(cluster);

        main = MakeSlotButton();
        main.AnchorRight = 1; main.AnchorBottom = 0.55f;
        cluster.AddChild(main);

        up1 = MakeSlotButton();
        up1.AnchorTop = 0.55f; up1.AnchorRight = 0.48f; up1.AnchorBottom = 1f;
        cluster.AddChild(up1);

        up2 = MakeSlotButton();
        up2.AnchorLeft = 0.52f; up2.AnchorTop = 0.55f; up2.AnchorRight = 1f; up2.AnchorBottom = 1f;
        cluster.AddChild(up2);
    }

    private static TextureButton MakeSlotButton() {
        var b = new TextureButton();
        b.IgnoreTextureSize = true;
        b.StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered;
        b.AnchorLeft = 0; b.AnchorTop = 0;
        var border = new Panel();
        border.MouseFilter = Control.MouseFilterEnum.Ignore;
        var style = new StyleBoxFlat();
        style.BgColor = new Color(0, 0, 0, 0);
        style.BorderColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(2);
        border.AddThemeStyleboxOverride("panel", style);
        border.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        b.AddChild(border);
        return b;
    }

    private static void BuildInventoryChildren(InventoryPanel inv) {
        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("separation", 6);
        inv.AddChild(vbox);

        // Search row.
        var searchRow = new HBoxContainer();
        searchRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        AddLabel(searchRow, "Search", 11);
        inv.searchField = new LineEdit();
        inv.searchField.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        inv.searchField.PlaceholderText = "name, type, rarity";
        searchRow.AddChild(inv.searchField);
        vbox.AddChild(searchRow);

        // Filter / Sort row.
        var filterRow = new HBoxContainer();
        AddLabel(filterRow, "Type", 11);
        inv.typeFilter = new OptionButton();
        filterRow.AddChild(inv.typeFilter);
        AddLabel(filterRow, "Rarity", 11);
        inv.rarityFilter = new OptionButton();
        filterRow.AddChild(inv.rarityFilter);
        AddLabel(filterRow, "Sort", 11);
        inv.sortSelect = new OptionButton();
        filterRow.AddChild(inv.sortSelect);
        vbox.AddChild(filterRow);

        // Scrollable grid.
        var scroll = new ScrollContainer();
        scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        vbox.AddChild(scroll);

        inv.grid = new GridContainer();
        inv.grid.Columns = 7;                       // smaller icons → more per row
        inv.grid.AddThemeConstantOverride("h_separation", 4);
        inv.grid.AddThemeConstantOverride("v_separation", 4);
        inv.grid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(inv.grid);
    }

    private static void BuildComparisonChildren(ComparisonPanel cmp) {
        var v = new VBoxContainer();
        v.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        v.AddThemeConstantOverride("separation", 6);
        cmp.AddChild(v);

        cmp.headerLabel = AddLabel(v, "Compare", 14, HorizontalAlignment.Center);

        var pair = new HBoxContainer();
        pair.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        v.AddChild(pair);

        var curCol = new VBoxContainer();
        curCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        pair.AddChild(curCol);
        AddLabel(curCol, "Current", 11, HorizontalAlignment.Center);
        cmp.currentIcon = MakeCompareIcon();
        curCol.AddChild(cmp.currentIcon);
        cmp.currentNameLabel = AddLabel(curCol, "", 11, HorizontalAlignment.Center);
        cmp.currentStatsLabel = AddLabel(curCol, "", 10);

        var incCol = new VBoxContainer();
        incCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        pair.AddChild(incCol);
        AddLabel(incCol, "Proposed", 11, HorizontalAlignment.Center);
        cmp.incomingIcon = MakeCompareIcon();
        incCol.AddChild(cmp.incomingIcon);
        cmp.incomingNameLabel = AddLabel(incCol, "", 11, HorizontalAlignment.Center);
        cmp.incomingStatsLabel = AddLabel(incCol, "", 10);

        cmp.deltaLabel = AddLabel(v, "", 11, HorizontalAlignment.Center);

        var actions = new HBoxContainer();
        actions.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        actions.Alignment = BoxContainer.AlignmentMode.Center;
        cmp.confirmButton = new Button();
        cmp.confirmButton.Text = "Equip";
        actions.AddChild(cmp.confirmButton);
        cmp.cancelButton = new Button();
        cmp.cancelButton.Text = "Cancel";
        actions.AddChild(cmp.cancelButton);
        v.AddChild(actions);
    }

    private static TextureRect MakeCompareIcon() {
        var t = new TextureRect();
        t.CustomMinimumSize = new Vector2(56, 56);
        t.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        t.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        return t;
    }

    private static Label AddLabel(Container parent, string text, int fontSize, HorizontalAlignment align = HorizontalAlignment.Left) {
        var l = new Label();
        l.Text = text;
        l.AddThemeFontSizeOverride("font_size", fontSize);
        l.HorizontalAlignment = align;
        parent.AddChild(l);
        return l;
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

    private void OnInventoryClicked(string instanceId) {
        Equipment item = GameManager.GetInstance(instanceId);
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
        Equipment incoming = GameManager.GetInstance(newInstanceId);
        if (incoming == null) return;
        Equipment outgoing = GetEquipped(currentPlayer, slot);

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
