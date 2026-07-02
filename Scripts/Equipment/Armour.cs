using Godot;
using Godot.Collections;

[GlobalClass]
public partial class Armour : Resource, Equipment {

    [Export] public string id { get; set; } = "";
    [Export] public string name { get; set; }
    [Export] public Texture2D image { get; set; }

    [Export] public Equipment.EquipmentType type { get; set; }
    [Export] public Equipment.Rarity rarity { get; set; }
    [Export] public bool isUpgrade { get; set; }

    [ExportGroup("Defense")]
    [Export] public Array<Dice> physicalDefense;
    [Export] public int physicalDefenseModifier = 0;
    [Export] public Array<Dice> magicDefense;
    [Export] public int magicDefenseModifier = 0;
    [Export] public int dodgeAbility;
    [Export] public int upgradeSlots;

    // Passive abilities (dodge/move modifiers, conditional bonuses) and status
    // immunities. Shared effect type with weapon attacks; see EquipmentEffect.
    [ExportGroup("Passives")]
    [Export] public Array<EquipmentEffect> passives = new();
    [Export] public Array<EncounterManager.StatusEffect> immunities = new();

    [ExportGroup("Armour Reqs")]
    [Export] public int strengthReq { get; set; }
    [Export] public int dexterityReq { get; set; }
    [Export] public int intelligenceReq { get; set; }
    [Export] public int faithReq { get; set; }

    public Armour() : this("", Equipment.EquipmentType.Armour,Equipment.Rarity.COMMON,false, 0,0,0,0, new Array<Dice>{}, new Array<Dice>{}, 0,0) {}

    public Armour(string name, Equipment.EquipmentType type, Equipment.Rarity rarity, bool isUpgrade, int strengthReq, int dexterityReq, int intelligenceReq, int faithReq, Array<Dice> physicalDefense, Array<Dice> magicDefense, int dodgeAbility, int upgradeSlots)
    {
        this.name = name;

        this.type = type;
        this.rarity = rarity;
        this.isUpgrade = isUpgrade;

        this.strengthReq = strengthReq;
        this.dexterityReq = dexterityReq;
        this.intelligenceReq = intelligenceReq;
        this.faithReq = faithReq;

        this.physicalDefense = physicalDefense;
        this.magicDefense = magicDefense;
        this.dodgeAbility = dodgeAbility;
        this.upgradeSlots = upgradeSlots;
    }
}
