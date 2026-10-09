using Godot;

public partial class BonfireOptions : VBoxContainer
{
    [Export] public Button restButton;
    [Export] public Button levelUpButton;
    [Export] public Button merchantButton;
    [Export] public Button readyButton;
    [Export] public Label restFeedback;
    // The soul counter hangs under this bar's right end.
    [Export] public Control titleBar;
    [Export] public BonfireRestAnimation restAnimation;
    [Export] public LevelUpModal levelUp;
    [Export] public MerchantModal merchant;

    // Set by a wipe on its way here: the party wakes at the bonfire and rests at once.
    // One-shot, so a later visit does not rest by itself.
    public static bool restOnArrival;

    private CharacterPortraitPane pane;
    private EquipmentModal modal;
    private bool resting;

    public override void _Ready() {
        restButton.Pressed      += OnPressedRest;
        levelUpButton.Pressed   += OnPressedLevelUp;
        merchantButton.Pressed  += () => merchant?.Open();
        readyButton.Pressed     += OnPressedReady;

        pane = GetNodeOrNull<CharacterPortraitPane>("/root/CharacterPortraitPane");
        modal = GetNodeOrNull<EquipmentModal>("/root/EquipmentModal");

        if (restFeedback != null) restFeedback.Visible = false;
        GetNodeOrNull<SoulCounter>("/root/SoulCounter")?.ShowBelow(titleBar);
        restButton.GrabFocus();

        // Show the floating portrait pane while we're at the bonfire.
        pane?.Show();

        // Route portrait clicks to the modal when the modal isn't already open.
        if (pane != null) pane.PortraitClicked += OnPortraitClicked;
        if (modal != null) modal.Closed += OnEquipmentClosed;

        CampaignManager.Autosave(CampaignManager.BONFIRE);

        if (restOnArrival) {
            restOnArrival = false;
            RestOnArrival();
        }
    }

    // A frame late: the rest animation is later in the tree, so its _Ready (which hides and
    // clears it) has not run yet, and nothing has been laid out for its flare to centre on.
    private async void RestOnArrival() {
        SetOptionsEnabled(false);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        OnPressedRest();
    }

    // The pane is an autoload and outlives this scene; left connected, every later scene's
    // portrait click would still open the equipment modal.
    public override void _ExitTree() {
        if (pane != null) pane.PortraitClicked -= OnPortraitClicked;
        if (modal != null) modal.Closed -= OnEquipmentClosed;
        GetNodeOrNull<SoulCounter>("/root/SoulCounter")?.Release(titleBar);
    }

    // Gear changed at the bonfire is kept the moment the modal closes.
    private void OnEquipmentClosed() {
        CampaignManager.Autosave();
    }

    // While the Level Up screen is open a portrait picks whose board it shows.
    private void OnPortraitClicked(int playerIndex) {
        var players = CampaignManager.Players;
        if (playerIndex < 0 || playerIndex >= players.Length) return;
        if (levelUp != null && levelUp.IsOpen()) {
            levelUp.Open(players[playerIndex]);
            return;
        }
        if (modal == null) return;
        if (modal.IsOpen()) return;                 // modal handles further clicks itself
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
        levelUpButton.Disabled = !enabled;
        merchantButton.Disabled = !enabled;
        readyButton.Disabled = !enabled;
    }

    // The title bar count is the party pool, not a per-character number (p19).

    private void OnPressedLevelUp() {
        var players = CampaignManager.Players;
        if (players.Length > 0) levelUp?.Open(players[0]);
    }

    private void OnPressedReady() {
        GetTree().ChangeSceneToPacked(ResourceLoader.Load<PackedScene>("res://Scenes/WorldMap.tscn"));
    }
}
