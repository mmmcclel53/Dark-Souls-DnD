using Godot;

public interface Equipment {
    // Stored as an ordinal in every item's .tres. Epic and Legendary swapped names in Oct 2026
    // (Matt: Legendary is the rarer, as in most games); the ordinals and so every item's tier
    // stayed put.
    public enum Rarity { STARTER, COMMON, UNCOMMON, RARE, EPIC, LEGENDARY };
    public enum EquipmentType { Armour, Weapon, Shield, Spell, Ring, Gem, Item };

    // Unique runtime instance id. Empty/null on raw templates loaded from disk.
    string id { get; set; }

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
