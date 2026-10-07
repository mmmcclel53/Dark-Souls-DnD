using Godot;

// A ring: an armour upgrade (p12), worn in one of the armour's upgrade slots. Each ring's
// rule is its own and too particular for EquipmentEffect's shared vocabulary, so the ring
// just names its effect and the code it changes asks for it there (Player.HasRing).
// Generated from Matt's list by Tools/onboard_rings.py.
[GlobalClass]
public partial class Ring : Resource, Equipment {

	// Stored as an ordinal in each ring's .tres: only ever append.
	public enum Effect {
		NONE,
		BELLOWING_DRAGONCREST,  // unused: cut (Oct 2026), only ever helped one class's Heroic Action
		BLUE_TEARSTONE,         // after suffering damage from an enemy attack, gain 1 stamina
		CARTHUS_MILKRING,       // dodging costs no stamina
		CHLORANTHY,             // the wearer and anyone on their node gain 4 stamina, not 2, at activation start
		COVETOUS_SILVER_SERPENT,// a won encounter pays the party 1 more soul
		DARK_WOOD_GRAIN,        // the dodge step may be 2 nodes
		DIVINE_BLESSING,        // once per rest, in the wearer's activation: remove all damage and conditions
		DUSK_CROWN,             // magic attacks cost 2 less; making one deals the wearer 1 damage
		GREAT_SWAMP,            // unused: cut (Oct 2026), only ever helped one class's Heroic Action
		HORNET,                 // every attack gains 1 orange die and -2
		KNIGHT_SLAYER,          // an attack that beats Block/Resist by 3 or more gives 1 stamina
		MAGIC_STONEPLATE,       // +1 black die to Resist
		OBSCURING,              // +2 dodge dice against an attacker 2 or more nodes away
		RED_TEARSTONE,          // +1 damage on every attack while 4 or more damage is on the bar
		RING_OF_FAVOUR,         // two or more attack rolls in an activation give 1 stamina at its end
		SUN_PRINCESS,           // 1 health at the end of the wearer's activation
		TINY_BEING,             // may take 1 stamina and 1 health instead of 2 stamina at activation start
		WOLF,                   // the dodge dice are rolled before choosing to dodge or block
	}

	[Export] public string id { get; set; } = "";
	[Export] public string name { get; set; }
	[Export] public Texture2D image { get; set; }

	[Export] public Equipment.EquipmentType type { get; set; } = Equipment.EquipmentType.Ring;
	[Export] public Equipment.Rarity rarity { get; set; } = Equipment.Rarity.COMMON;
	[Export] public bool isUpgrade { get; set; } = true;

	[Export] public Effect effect = Effect.NONE;

	[ExportGroup("Ring Reqs")]
	[Export] public int strengthReq { get; set; }
	[Export] public int dexterityReq { get; set; }
	[Export] public int intelligenceReq { get; set; }
	[Export] public int faithReq { get; set; }

	// Sets nothing: Godot strips defaults on save and rebuilds through this (see CLAUDE.md).
	public Ring() {}
}
