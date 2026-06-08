using Godot;

public partial class CharacterSheet : TextureRect
{
    [Export] public Character character;

    // UI
    [Export] public TextureButton backupSlotButton;
    [Export] public TextureButton leftHandButton;
    [Export] public TextureButton rightHandButton;
    [Export] public TextureButton armourButton;

    public CharacterSheet() {}
    public CharacterSheet(Character character) {
        this.character = character;
    }

	public override void _Ready() {
        if (character != null) {
            Refresh();
        }
	}

    public void SetCharacter(Character c) {
        character = c;
        Refresh();
    }

    public void Refresh() {
        if (character == null) return;
        Texture = character.image;
        backupSlotButton.TextureNormal = character.backupSlotDefault != null ? character.backupSlotDefault.image : null;
        leftHandButton.TextureNormal = character.leftHandDefault != null ? character.leftHandDefault.image : null;
        rightHandButton.TextureNormal = character.rightHandDefault != null ? character.rightHandDefault.image : null;
        armourButton.TextureNormal = character.armourDefault != null ? character.armourDefault.image : null;
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
