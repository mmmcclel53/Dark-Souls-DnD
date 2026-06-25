using Godot;

public partial class BonfireOptions : VBoxContainer
{
    [Export] public Button restButton;
    [Export] public Button equipmentButton;
    [Export] public Button readyButton;

    private CharacterPortraitPane pane;
    private EquipmentModal modal;

    public override void _Ready() {
        restButton.Pressed      += OnPressedRest;
        equipmentButton.Pressed += OnPressedEquipment;
        readyButton.Pressed     += OnPressedReady;

        pane = GetNodeOrNull<CharacterPortraitPane>("/root/CharacterPortraitPane");
        modal = GetNodeOrNull<EquipmentModal>("/root/EquipmentModal");

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

    private void OnPressedRest() {
        GD.Print("Rest");
    }

    private void OnPressedEquipment() {
        modal?.Open(null);
    }

    private void OnPressedReady() {
        GD.Print("Ready!");
    }
}
