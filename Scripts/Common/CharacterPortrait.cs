using Godot;

// Vertically-arranged character portrait used by the floating pane.
// Shows avatar background, name top-center, HP "X / 10" and stamina at the bottom,
// and a strip on the right border for status effect icons.
public partial class CharacterPortrait : Control
{
    [Signal] public delegate void PortraitClickedEventHandler();

    [Export] public TextureButton avatarButton;
    [Export] public Label nameLabel;
    [Export] public Label hpLabel;
    [Export] public Label staminaLabel;
    [Export] public Panel selectionBorder;
    [Export] public Container statusEffectsContainer;

    private Player player;
    private bool selected;

    public override void _Ready() {
        if (avatarButton != null)
            avatarButton.Pressed += () => EmitSignal(SignalName.PortraitClicked);
        if (selectionBorder != null) selectionBorder.Visible = false;
    }

    public void SetPlayer(Player p) {
        player = p;
        Refresh();
    }

    public Player GetPlayer() => player;

    public void SetSelected(bool isSelected) {
        selected = isSelected;
        if (selectionBorder != null) selectionBorder.Visible = isSelected;
    }

    public void Refresh() {
        if (player == null) return;
        if (avatarButton != null) {
            avatarButton.TextureNormal = player.character?.avatar ?? player.character?.image;
        }
        if (nameLabel != null) nameLabel.Text = player.name;
        if (hpLabel != null)   hpLabel.Text   = $"HP {player.GetRemainingHP()} / {player.GetMaxEndurance()}";
        if (staminaLabel != null) staminaLabel.Text = $"STA {player.GetCurrentStamina()}";
        // TODO: status effect icons once status system lands.
    }
}
