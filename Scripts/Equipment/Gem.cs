using Godot;

// A gem: a weapon upgrade (p12), worn in one of the weapon's upgrade slots and changing that
// weapon's attacks. Like Ring, it names its effect and AttackTerms.For applies it; several
// gems share an effect (four of them only add a black die). Generated from Matt's list by
// Tools/onboard_gems.py.
[GlobalClass]
public partial class Gem : Resource, Equipment {

	// Stored as an ordinal in each gem's .tres: only ever append.
	public enum Effect {
		NONE,
		BLACK_DIE,          // +1 black die (Blessed, Crystal, Heavy, Sharp)
		BLEED,              // the attacks inflict Bleed (Blood)
		BLACK_DIE_MAGIC,    // +1 black die, and the attacks are magic (Blue Titanite)
		DAMAGE_MAGIC,       // +1 damage, and the attacks are magic (Carthus Flame Arc, Crystal Magic Weapon)
		REACH_MAGIC,        // a range-0 attack gains 1 range and is magic (Faron Flashsword)
		HOLLOW,             // an attack that deals 0 damage resets the Luck token (Hollow)
		MAGIC,              // the attacks are magic (Lightning)
		POISON,             // the attacks inflict Poison (Poison)
		RAW,                // +2 damage, +1 stamina (Raw; its card edited from +2 stamina, Tools/CardEdits)
		SIMPLE,             // the weapon's highest-cost option costs 1 less (Simple)
		DAMAGE,             // +1 damage (Titanite Shard)
		DAMAGE_TWO,         // +2 damage (Titanite Scale; its card edited to read +2, Tools/CardEdits)
	}

	[Export] public string id { get; set; } = "";
	[Export] public string name { get; set; }
	[Export] public Texture2D image { get; set; }

	[Export] public Equipment.EquipmentType type { get; set; } = Equipment.EquipmentType.Gem;
	[Export] public Equipment.Rarity rarity { get; set; } = Equipment.Rarity.COMMON;
	[Export] public bool isUpgrade { get; set; } = true;

	[Export] public Effect effect = Effect.NONE;

	[ExportGroup("Gem Reqs")]
	[Export] public int strengthReq { get; set; }
	[Export] public int dexterityReq { get; set; }
	[Export] public int intelligenceReq { get; set; }
	[Export] public int faithReq { get; set; }

	// Sets nothing: Godot strips defaults on save and rebuilds through this (see CLAUDE.md).
	public Gem() {}
}
