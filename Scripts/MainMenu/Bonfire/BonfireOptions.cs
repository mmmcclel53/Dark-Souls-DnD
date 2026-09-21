using Godot;

public partial class BonfireOptions : VBoxContainer
{
    [Export] public Button restButton;
    [Export] public Button equipmentButton;
    [Export] public Button readyButton;
    [Export] public Label restFeedback;
    [Export] public Label soulsLabel;
    [Export] public BonfireRestAnimation restAnimation;

    private CharacterPortraitPane pane;
    private EquipmentModal modal;
    private bool resting;

    public override void _Ready() {
        restButton.Pressed      += OnPressedRest;
        equipmentButton.Pressed += OnPressedEquipment;
        readyButton.Pressed     += OnPressedReady;

        pane = GetNodeOrNull<CharacterPortraitPane>("/root/CharacterPortraitPane");
        modal = GetNodeOrNull<EquipmentModal>("/root/EquipmentModal");

        if (restFeedback != null) restFeedback.Visible = false;
        RefreshSouls();
        restButton.GrabFocus();

        // Show the floating portrait pane while we're at the bonfire.
        pane?.Show();

        // Route portrait clicks to the modal when the modal isn't already open.
        if (pane != null) pane.PortraitClicked += OnPortraitClicked;
    }

    private void OnPortraitClicked(int playerIndex) {
        if (modal == null) return;
        if (modal.IsOpen()) return;                 // modal handles further clicks itself
        var players = CampaignManager.Players;
        if (playerIndex < 0 || playerIndex >= players.Length) return;
        modal.Open(players[playerIndex]);
    }

    // Resting is the only thing that puts endurance back after a defeat, and the only thing
    // that respawns encounters, so the portraits have to be rebuilt to show it happened.
    // It all happens behind the blackout, so nothing pops while the player is looking.
    private async void OnPressedRest() {
        if (resting) return;
        resting = true;
        SetOptionsEnabled(false);

        if (restAnimation != null) await restAnimation.FadeOut();

        int respawned = WorldMapManager.RestAtBonfire();
        pane?.Refresh();
        RefreshSouls();
        ShowRestFeedback(respawned);

        if (restAnimation != null) await restAnimation.FadeIn();

        SetOptionsEnabled(true);
        resting = false;
    }

    private void ShowRestFeedback(int respawned) {
        if (restFeedback == null) return;
        restFeedback.Text = respawned > 0
            ? $"Rested. {respawned} encounter{(respawned == 1 ? "" : "s")} respawned."
            : "Rested.";
        restFeedback.Visible = true;
    }

    private void SetOptionsEnabled(bool enabled) {
        restButton.Disabled = !enabled;
        equipmentButton.Disabled = !enabled;
        readyButton.Disabled = !enabled;
    }

    // The title bar count is the party pool, not a per-character number (p19).
    private void RefreshSouls() {
        if (soulsLabel != null) soulsLabel.Text = SoulCache.current.ToString();
    }

    private void OnPressedEquipment() {
        modal?.Open(null);
    }

    private void OnPressedReady() {
        GetTree().ChangeSceneToPacked(ResourceLoader.Load<PackedScene>("res://Scenes/WorldMap.tscn"));
    }
}
