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
        Texture = player.character.image;
        backupSlotButton.TextureNormal = player.backupSlot != null ? player.backupSlot.image : null;
        leftHandButton.TextureNormal = player.leftHand != null ? player.leftHand.image : null;
        rightHandButton.TextureNormal = player.rightHand != null ? player.rightHand.image : null;
        armourButton.TextureNormal = player.armour != null ? player.armour.image : null;

        // TODO: Add upgrades
	}

    // Bonfire
    // public void SetBackupSlot(Weapon newWeapon) {
    //     backupSlot = newWeapon;
    // }
    // public void SetLeftHand(Weapon newWeapon) {
    //     leftHand = newWeapon;
    // }
    // public void SetRightHand(Weapon newWeapon) {
    //     rightHand = newWeapon;
    // }
    // public void SetArmour(Armour newArmour) {
    //     armour = newArmour;
    // }
}
