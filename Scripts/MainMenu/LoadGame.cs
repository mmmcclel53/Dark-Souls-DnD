using Godot;

public partial class LoadGame : Node
{
    [Export] public Button backButton;

    [ExportGroup("Slot 1")]
    [Export] public PanelContainer slot1Panel;
    [Export] public Label slot1Name;
    [Export] public Label slot1Campaign;
    [Export] public Label slot1Timestamp;
    [Export] public Label slot1Party;
    [Export] public Button slot1Load;
    [Export] public Button slot1Delete;

    [ExportGroup("Slot 2")]
    [Export] public PanelContainer slot2Panel;
    [Export] public Label slot2Name;
    [Export] public Label slot2Campaign;
    [Export] public Label slot2Timestamp;
    [Export] public Label slot2Party;
    [Export] public Button slot2Load;
    [Export] public Button slot2Delete;

    [ExportGroup("Slot 3")]
    [Export] public PanelContainer slot3Panel;
    [Export] public Label slot3Name;
    [Export] public Label slot3Campaign;
    [Export] public Label slot3Timestamp;
    [Export] public Label slot3Party;
    [Export] public Button slot3Load;
    [Export] public Button slot3Delete;

    [ExportGroup("Slot 4")]
    [Export] public PanelContainer slot4Panel;
    [Export] public Label slot4Name;
    [Export] public Label slot4Campaign;
    [Export] public Label slot4Timestamp;
    [Export] public Label slot4Party;
    [Export] public Button slot4Load;
    [Export] public Button slot4Delete;

    [ExportGroup("Delete Modal")]
    [Export] public Panel deleteModal;
    [Export] public Label deleteModalLabel;
    [Export] public Button confirmDeleteButton;
    [Export] public Button cancelDeleteButton;

    private PanelContainer[] panels;
    private Label[] nameLabels;
    private Label[] campaignLabels;
    private Label[] timestampLabels;
    private Label[] partyLabels;
    private Button[] loadButtons;
    private Button[] deleteButtons;

    private int pendingDeleteSlot = -1;

    public override void _Ready() {
        panels = new[] { slot1Panel, slot2Panel, slot3Panel, slot4Panel };
        nameLabels = new[] { slot1Name, slot2Name, slot3Name, slot4Name };
        campaignLabels = new[] { slot1Campaign, slot2Campaign, slot3Campaign, slot4Campaign };
        timestampLabels = new[] { slot1Timestamp, slot2Timestamp, slot3Timestamp, slot4Timestamp };
        partyLabels = new[] { slot1Party, slot2Party, slot3Party, slot4Party };
        loadButtons = new[] { slot1Load, slot2Load, slot3Load, slot4Load };
        deleteButtons = new[] { slot1Delete, slot2Delete, slot3Delete, slot4Delete };

        ApplySlotStyles();

        backButton.Pressed += () => GetTree().ChangeSceneToPacked(
            ResourceLoader.Load<PackedScene>("res://Scenes/MainMenu.tscn"));

        for (int i = 0; i < 4; i++) {
            int slot = i + 1;
            loadButtons[i].Pressed += () => OnLoadPressed(slot);
            deleteButtons[i].Pressed += () => OnDeletePressed(slot);
        }

        confirmDeleteButton.Pressed += OnConfirmDelete;
        cancelDeleteButton.Pressed += () => { deleteModal.Visible = false; };

        deleteModal.Visible = false;
        RefreshAllSlots();

        GetNode<CharacterPortraitPane>("/root/CharacterPortraitPane")?.Hide();
    }

    private void ApplySlotStyles() {
        var slotStyle = new StyleBoxFlat();
        slotStyle.BgColor = new Color(0.1f, 0.12f, 0.15f, 0.88f);
        slotStyle.BorderColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        slotStyle.SetBorderWidthAll(2);
        slotStyle.SetCornerRadiusAll(6);
        slotStyle.ContentMarginLeft = slotStyle.ContentMarginRight = 14;
        slotStyle.ContentMarginTop = slotStyle.ContentMarginBottom = 10;

        var emptySlotStyle = new StyleBoxFlat();
        emptySlotStyle.BgColor = new Color(0.07f, 0.08f, 0.1f, 0.75f);
        emptySlotStyle.BorderColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        emptySlotStyle.SetBorderWidthAll(1);
        emptySlotStyle.SetCornerRadiusAll(6);
        emptySlotStyle.ContentMarginLeft = emptySlotStyle.ContentMarginRight = 14;
        emptySlotStyle.ContentMarginTop = emptySlotStyle.ContentMarginBottom = 10;

        for (int i = 0; i < 4; i++)
            panels[i].AddThemeStyleboxOverride("panel", slotStyle);

        var overlayStyle = new StyleBoxFlat();
        overlayStyle.BgColor = new Color(0f, 0f, 0f, 0.72f);
        deleteModal.AddThemeStyleboxOverride("panel", overlayStyle);
    }

    private void RefreshAllSlots() {
        for (int i = 0; i < 4; i++)
            RefreshSlot(i + 1);
    }

    private void RefreshSlot(int slot) {
        int i = slot - 1;
        if (SaveGame.SlotExists(slot)) {
            var save = SaveGame.LoadSlot(slot);
            if (save == null) {
                SetEmptySlot(i, slot);
                return;
            }
            nameLabels[i].Text = string.IsNullOrEmpty(save.campaignName) ? $"Save {slot}" : save.campaignName;
            nameLabels[i].Modulate = Colors.White;
            campaignLabels[i].Text = CampaignLine(save);
            campaignLabels[i].Visible = true;
            timestampLabels[i].Text = $"Last saved:  {save.timestamp}";
            timestampLabels[i].Visible = true;
            if (save.playerNames.Length > 0) {
                string partyStr = string.Join("   |   ", save.playerNames);
                if (save.characterNames.Length == save.playerNames.Length) {
                    string[] parts = new string[save.playerNames.Length];
                    for (int j = 0; j < parts.Length; j++)
                        parts[j] = $"{save.playerNames[j]}  ({save.characterNames[j]})";
                    partyStr = string.Join("   |   ", parts);
                }
                partyLabels[i].Text = partyStr;
            } else {
                partyLabels[i].Text = "No party data";
            }
            partyLabels[i].Visible = true;
            loadButtons[i].Disabled = false;
            deleteButtons[i].Disabled = false;
        } else {
            SetEmptySlot(i, slot);
        }
    }

    // A generated campaign by its size and bosses, a hand-built one by its file.
    private static string CampaignLine(SaveGame save) {
        if (save.campaignSize > 0)
            return $"{CampaignGenerator.SizeName(save.campaignSize)}  —  {save.campaignSize} Bosses";
        return WorldMapManager.CampaignTitle(string.IsNullOrEmpty(save.campaignFile) ? WorldMapManager.DEFAULT_CAMPAIGN : save.campaignFile);
    }

    private void SetEmptySlot(int i, int slot) {
        nameLabels[i].Text = $"— Empty Slot {slot} —";
        nameLabels[i].Modulate = new Color(0.55f, 0.55f, 0.55f, 1f);
        campaignLabels[i].Text = "";
        campaignLabels[i].Visible = false;
        timestampLabels[i].Text = "";
        timestampLabels[i].Visible = false;
        partyLabels[i].Text = "";
        partyLabels[i].Visible = false;
        loadButtons[i].Disabled = true;
        deleteButtons[i].Disabled = true;
    }

    // Back to wherever the party was, as Continue does.
    private void OnLoadPressed(int slot) {
        string scene = CampaignManager.Resume(slot);
        if (scene == null) {
            GD.PrintErr($"Failed to load save slot {slot}");
            return;
        }
        GetTree().ChangeSceneToPacked(ResourceLoader.Load<PackedScene>(scene));
    }

    private void OnDeletePressed(int slot) {
        pendingDeleteSlot = slot;
        string saveName = nameLabels[slot - 1].Text;
        deleteModalLabel.Text = $"Delete \"{saveName}\"?\n\nThis cannot be undone.";
        deleteModal.Visible = true;
    }

    private void OnConfirmDelete() {
        if (pendingDeleteSlot < 1) return;
        var dir = DirAccess.Open("user://");
        dir?.Remove($"savegame_{pendingDeleteSlot}.tres");
        deleteModal.Visible = false;
        int refreshSlot = pendingDeleteSlot;
        pendingDeleteSlot = -1;
        RefreshSlot(refreshSlot);
    }
}
