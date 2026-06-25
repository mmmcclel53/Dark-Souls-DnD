using Godot;

public partial class PlayerSheet : TextureRect
{
    [Export] public Player player;

    // UI
    [Export] public TextureButton backupSlotButton;
    [Export] public TextureButton leftHandButton;
    [Export] public TextureButton rightHandButton;
    [Export] public TextureButton armourButton;

    public override void _Ready() {
        if (player == null) return;
        Texture = player.character?.image;
        var backup = player.GetBackupSlot();
        var left   = player.GetLeftHand();
        var right  = player.GetRightHand();
        var armour = player.GetArmour();
        backupSlotButton.TextureNormal = backup?.image;
        leftHandButton.TextureNormal   = left?.image;
        rightHandButton.TextureNormal  = right?.image;
        armourButton.TextureNormal     = armour?.image;

        // TODO: Add upgrades
    }
}
