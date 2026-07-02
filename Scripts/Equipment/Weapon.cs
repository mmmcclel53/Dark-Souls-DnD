using Godot;
using Godot.Collections;

[GlobalClass]
public partial class Weapon : Resource, Equipment {

    [Export] public string id { get; set; } = "";
    [Export] public string name { get; set; }
    [Export] public Texture2D image { get; set; }

    [Export] public Equipment.EquipmentType type { get; set; }
    [Export] public Equipment.Rarity rarity { get; set; }
    [Export] public bool isUpgrade { get; set; }
    [Export] public int numHands = 1;   // hand slots required to wield (1 or 2)

    [ExportGroup("Attacks")]
    [Export] public int attackRange;
    [Export] public Array<PlayerMove> attacks;

    [ExportGroup("Defense")]
    [Export] public Array<Dice> physicalDefense;
    [Export] public Array<Dice> magicDefense;
    [Export] public int dodgeAbility;
    [Export] public int upgradeSlots;

    // Weapon-level passives (e.g. Vordt "Suffer Bleed / If Attack") and status
    // immunities. Shields are authored as Weapons, so their passives/immunities
    // (Bypass Two Hand Check, Immune to Poison & Bleed, etc.) live here too.
    // See EquipmentEffect.
    [ExportGroup("Passives")]
    [Export] public Array<EquipmentEffect> passives = new();
    [Export] public Array<EncounterManager.StatusEffect> immunities = new();

    [ExportGroup("Weapon Reqs")]
    [Export] public int strengthReq { get; set; }
    [Export] public int dexterityReq { get; set; }
    [Export] public int intelligenceReq { get; set; }
    [Export] public int faithReq { get; set; }

    public Weapon() : this("", Equipment.EquipmentType.Weapon,Equipment.Rarity.COMMON,false, 0,0,0,0, new Array<Dice>{}, new Array<Dice>{}, 0,0,0, new Array<PlayerMove>{}) {}

    public Weapon(string name, Equipment.EquipmentType type, Equipment.Rarity rarity, bool isUpgrade, int strengthReq, int dexterityReq, int intelligenceReq, int faithReq, Array<Dice> physicalDefense, Array<Dice> magicDefense, int dodgeAbility, int upgradeSlots, int attackRange, Array<PlayerMove> attacks)
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

        this.attackRange = attackRange;
        this.attacks = attacks;
    }
}
