using Godot;
using Godot.Collections;
using System.Diagnostics;


public partial class Player : TextureRect
{
    [Export] public string name = "Mattmac53";
    [Export] public Character character;

    public int strength;
    public int dexterity;
    public int intelligence;
    public int faith;

    public Weapon backupSlot;
    public Weapon leftHand; 
    public Weapon rightHand;
    public Armour armour;

    // public Player(string name, Character character)
    // {
    //     this.name = name;
        
    //     this.strength = character.strengthTiers[0];
    //     this.dexterity = character.dexterityTiers[0];
    //     this.intelligence = character.intelligenceTiers[0];
    //     this.faith = character.faithTiers[0];

    //     this.backupSlot = character.backupSlotDefault;
    //     this.leftHand = character.leftHandDefault;
    //     this.rightHand = character.rightHandDefault;
    //     this.armour = character.armourDefault;
    // }

    public int stamina = 10;
    public int hits = 0;

    public string currentNode;
    public bool isAggro = false;
    public EncounterManager.StatusEffect statusEffect = EncounterManager.StatusEffect.NONE;

    // UI
    [Export] public TextureButton backupSlotButton;
    [Export] public TextureButton leftHandButton;
    [Export] public TextureButton rightHandButton;
    [Export] public TextureButton armourButton;

	public override void _Ready() {
		// backupSlotButton.Pressed += () => { OnPressedBackupSlot(); };

        Node currentScene = GetTree().CurrentScene;
        GD.Print(currentScene.Name);
        if (currentScene.Name == "MainMenu") {
            GuiInput += (@event) => { OnNewGameCharacterSelect(character); };
        }

        Texture = character.image;
        GetChild(0).GetChild<TextureButton>(2).TextureNormal = character.backupSlotDefault != null ? character.backupSlotDefault.image : null;
        GetChild(1).GetChild<TextureButton>(2).TextureNormal = character.leftHandDefault.image;
        GetChild(2).GetChild<TextureButton>(2).TextureNormal = character.rightHandDefault.image;
        GetChild(3).GetChild<TextureButton>(2).TextureNormal = character.armourDefault.image;
	}

    public Dictionary<string, Variant> Save() {
        return new Dictionary<string, Variant>() {
            { "name", name },
            { "character", character },
            
            { "strength", strength },
            { "dexterity", dexterity },
            { "intelligence", intelligence },
            { "faith", faith },

            { "stamina", stamina },
            { "hits", hits },
            { "isAggro", isAggro }
        };
    }
    
    // Main Menu
    public void OnNewGameCharacterSelect(Character character) {
        backupSlot = character.backupSlotDefault;
        leftHand = character.leftHandDefault;
        rightHand = character.rightHandDefault;
        armour = character.armourDefault;
    }

    // Bonfire
    public void SetBackupSlot(Weapon newWeapon) {
        backupSlot = newWeapon;
    }
    public void SetLeftHand(Weapon newWeapon) {
        leftHand = newWeapon;
    }
    public void SetRightHand(Weapon newWeapon) {
        rightHand = newWeapon;
    }
    public void SetArmour(Armour newArmour) {
        armour = newArmour;
    }

    // Encounter
    public void SetLocation(Vector2 pos) {
        Position = pos;
    }
    public void OnCharacterActivation() {
        isAggro = true;
    }
}
