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
        backupSlotButton.TextureNormal = character.backupSlotDefault?.image;
        leftHandButton.TextureNormal   = character.leftHandDefault?.image;
        rightHandButton.TextureNormal  = character.rightHandDefault?.image;
        armourButton.TextureNormal     = character.armourDefault?.image;
    }
}
