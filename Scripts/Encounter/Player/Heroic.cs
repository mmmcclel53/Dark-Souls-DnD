using Godot;

// Each class's Heroic Action, from its character board. One use, then the token is flipped
// until a bonfire rest. Most are used from the token on the action bar during the
// character's own activation; two are reactions, offered where they happen: Stand Fast on
// the Block roll's reveal, Backstab after a successful dodge.
//
// The four that boost an attack (Berserk Charge, Spell Fury, Explosive Firepower, Rapid
// Strike) are armed on use and spent by the next attack they apply to; AttackTerms is where
// they change that attack. An armed boost not spent by the end of the activation is lost.
public static class Heroic
{
	// Appended only: the .tres files store the ordinal.
	public enum Kind {
		NONE,
		PERSEVERANCE,           // Herald
		BACKSTAB,               // Assassin
		BERSERK_CHARGE,         // Warrior
		STAND_FAST,             // Knight
		COMBAT_VERSATILITY,     // Deprived
		SPELL_FURY,             // Sorcerer
		EXPLOSIVE_FIREPOWER,    // Pyromancer
		KEEP_THE_FAITH,         // Cleric
		RAPID_STRIKE,           // Mercenary
		LUCKY_BREAK,            // Thief
	}

	public static string Name(Kind kind) => kind switch {
		Kind.PERSEVERANCE => "Perseverance",
		Kind.BACKSTAB => "Backstab",
		Kind.BERSERK_CHARGE => "Berserk Charge",
		Kind.STAND_FAST => "Stand Fast",
		Kind.COMBAT_VERSATILITY => "Combat Versatility",
		Kind.SPELL_FURY => "Spell Fury",
		Kind.EXPLOSIVE_FIREPOWER => "Explosive Firepower",
		Kind.KEEP_THE_FAITH => "Keep the Faith",
		Kind.RAPID_STRIKE => "Rapid Strike",
		Kind.LUCKY_BREAK => "Lucky Break",
		_ => "Heroic Action",
	};

	public static string Effect(Kind kind) => kind switch {
		Kind.PERSEVERANCE => "Every character gains 2 stamina.",
		Kind.BACKSTAB => "After a successful dodge, attack the enemy dodged, for free.",
		Kind.BERSERK_CHARGE => "Move 1 node for free; your next 0-range attack hits the whole node and costs no stamina.",
		Kind.STAND_FAST => "After blocking, add 1 blue die to the roll.",
		Kind.COMBAT_VERSATILITY => "Change equipment as if outside of combat.",
		Kind.SPELL_FURY => "Your next magic attack has unlimited range and costs no stamina.",
		Kind.EXPLOSIVE_FIREPOWER => "Your next magic attack gains 1 black die.",
		Kind.KEEP_THE_FAITH => "Heal 2 on every character within 1 range.",
		Kind.RAPID_STRIKE => "Make an additional attack, for free.",
		Kind.LUCKY_BREAK => "Gain 2 stamina, heal 2, and reset your Luck token.",
		_ => "",
	};

	// Used from the token on the action bar during the character's own activation. The
	// reactions are offered where they happen instead.
	public static bool FromBar(Kind kind) => kind is not (Kind.NONE or Kind.BACKSTAB or Kind.STAND_FAST);

	// Armed on use and spent by the next attack it applies to.
	public static bool IsBoost(Kind kind) =>
		kind is Kind.BERSERK_CHARGE or Kind.SPELL_FURY or Kind.EXPLOSIVE_FIREPOWER or Kind.RAPID_STRIKE;

	private static Dice blackDie;
	private static Dice blueDie;
	private static Dice orangeDie;
	public static Dice BlackDie => blackDie ??= GD.Load<Dice>("res://Resources/Prefabs/Dice/Black Dice.tres");
	public static Dice BlueDie => blueDie ??= GD.Load<Dice>("res://Resources/Prefabs/Dice/Blue Dice.tres");
	public static Dice OrangeDie => orangeDie ??= GD.Load<Dice>("res://Resources/Prefabs/Dice/Orange Dice.tres");
}
