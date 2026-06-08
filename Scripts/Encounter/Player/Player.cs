using Godot;

[GlobalClass]
public partial class Player : Resource
{
    [Export] public string name = "Mattmac53";
    [Export] public Character character;

    [Export] public int strength;
    [Export] public int dexterity;
    [Export] public int intelligence;
    [Export] public int faith;

    [Export] public Weapon backupSlot;
    [Export] public Weapon leftHand; 
    [Export] public Weapon rightHand;
    [Export] public Armour armour;

    [Export] public int stamina = 10;
    [Export] public int hits = 0;

    public Player() { }

    public Player(string name, Character character)
    {
        this.name = name;
        
        this.strength = character.strengthTiers[0];
        this.dexterity = character.dexterityTiers[0];
        this.intelligence = character.intelligenceTiers[0];
        this.faith = character.faithTiers[0];

        this.backupSlot = character.backupSlotDefault;
        this.leftHand = character.leftHandDefault;
        this.rightHand = character.rightHandDefault;
        this.armour = character.armourDefault;
    }

    // public string currentNode;
    // public bool isAggro = false;
    // public EncounterManager.StatusEffect statusEffect = EncounterManager.StatusEffect.NONE;
}
