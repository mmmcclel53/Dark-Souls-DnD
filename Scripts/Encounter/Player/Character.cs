using Godot;

[GlobalClass]
public partial class Character : Resource
{
    [Export] public string name;
    [Export] public Texture2D image;
    [Export] public Texture2D avatar;

    [ExportGroup("Equipment")]
    [Export] public Weapon backupSlotDefault;
    [Export] public Weapon leftHandDefault;
    [Export] public Weapon rightHandDefault;
    [Export] public Armour armourDefault;
    
    [ExportGroup("Leveling")]
    [Export] public int threatLevel;
    [Export] public int[] strengthTiers = new int[] { 10, 20, 30, 40 };
    [Export] public int[] dexterityTiers = new int[] { 10, 20, 30, 40 };
    [Export] public int[] intelligenceTiers = new int[] { 10, 20, 30, 40 };
    [Export] public int[] faithTiers = new int[] { 10, 20, 30, 40 };

    public Character() : this("Unknown", 1, new int[] { 10, 20, 30, 40 }, new int[] { 10, 20, 30, 40 }, new int[] { 10, 20, 30, 40 }, new int[] { 10, 20, 30, 40 }) {}

    public Character(string name, int threatLevel, int[] strengthTiers, int[] dexterityTiers, int[] intelligenceTiers, int[] faithTiers)
    {
        this.name = name;
        this.threatLevel = threatLevel;
        this.strengthTiers = strengthTiers;
        this.dexterityTiers = dexterityTiers;
        this.intelligenceTiers = intelligenceTiers;
        this.faithTiers = faithTiers;
    }
}