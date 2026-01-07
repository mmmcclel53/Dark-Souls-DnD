using Godot;

public interface Equipment {
    public enum Rarity { STARTER, COMMON, RARE, LEGENDARY, EPIC };
    public enum EquipmentType { Armour, Weapon, Shield, Spell, Ring, Gem };

    string name { get; set; }
    Texture2D image { get; set; }

    EquipmentType type { get; set; }
    Rarity rarity { get; set; }
    bool isUpgrade { get; set; }

    int strengthReq { get; set; }
    int dexterityReq { get; set; }
    int intelligenceReq { get; set; }
    int faithReq { get; set; }
}
