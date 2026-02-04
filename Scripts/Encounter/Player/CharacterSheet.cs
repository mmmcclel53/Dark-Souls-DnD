using Godot;

public partial class CharacterSheet : TextureRect
{
    [Export] public Player player;

    // UI
    public TextureButton backupSlotButton;
    public TextureButton leftHandButton;
    public TextureButton rightHandButton;
    public TextureButton armourButton;

	public override void _Ready() {
        // Texture = character.image;
        // GetChild(0).GetChild<TextureButton>(2).TextureNormal = character.backupSlotDefault != null ? character.backupSlotDefault.image : null;
        // GetChild(1).GetChild<TextureButton>(2).TextureNormal = character.leftHandDefault.image;
        // GetChild(2).GetChild<TextureButton>(2).TextureNormal = character.rightHandDefault.image;
        // GetChild(3).GetChild<TextureButton>(2).TextureNormal = character.armourDefault.image;
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
