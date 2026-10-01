using Godot;
using System.Collections.Generic;

// What one attack option actually costs and does for this attacker right now: its stamina,
// its range, whether it hits the whole node, and any dice added to it. The printed option
// is the starting point; Stagger and an armed Heroic Action change it. Everything that
// shows, targets, pays for or rolls a character's attack reads these terms, so an attack
// the bar shows as free with unlimited range is exactly the attack that gets made.
public readonly struct AttackTerms
{
	public readonly int cost;
	public readonly int range;
	public readonly bool aoe;
	public readonly bool notZeroRange;
	public readonly List<Dice> extraDice;
	// Rapid Strike: an additional attack, which the weapon's one-attack limit (p22) ignores.
	public readonly bool reusesWeapon;
	// An armed Heroic Action shaped this attack, and making it spends the boost.
	public readonly bool boosted;

	private AttackTerms(int cost, int range, bool aoe, bool notZeroRange, List<Dice> extraDice, bool reusesWeapon, bool boosted) {
		this.cost = cost;
		this.range = range;
		this.aoe = aoe;
		this.notZeroRange = notZeroRange;
		this.extraDice = extraDice;
		this.reusesWeapon = reusesWeapon;
		this.boosted = boosted;
	}

	// An option-specific range replaces the weapon's standard range for that attack (p23).
	public static int BaseRange(Weapon weapon, PlayerMove move) =>
		move.attackRange > 0 ? move.attackRange : (weapon?.attackRange ?? 0);

	// `free` is Backstab's attack: no stamina, whatever else applies.
	public static AttackTerms For(PlayerToken attacker, Weapon weapon, PlayerMove move, bool free = false) {
		int cost = CombatResolver.AttackStaminaCost(attacker, move);
		int range = BaseRange(weapon, move);
		bool aoe = move.isAOE;
		List<Dice> extra = new List<Dice>();
		bool reuses = false;
		bool boosted = false;

		switch (attacker?.pendingHeroic ?? Heroic.Kind.NONE) {
			case Heroic.Kind.SPELL_FURY when move.isMagic:
				range = EnemyData.UNLIMITED_RANGE;
				cost = 0;
				boosted = true;
				break;
			case Heroic.Kind.EXPLOSIVE_FIREPOWER when move.isMagic:
				if (Heroic.BlackDie != null) extra.Add(Heroic.BlackDie);
				boosted = true;
				break;
			case Heroic.Kind.BERSERK_CHARGE when range == 0:
				aoe = true;
				cost = 0;
				boosted = true;
				break;
			case Heroic.Kind.RAPID_STRIKE:
				cost = 0;
				reuses = true;
				boosted = true;
				break;
		}
		if (free) cost = 0;
		return new AttackTerms(cost, range, aoe, move.isNotZeroRange, extra, reuses, boosted);
	}

	public bool Reaches(PlayerToken attacker, Enemy enemy) {
		if (attacker == null || enemy == null) return false;
		int distance = EnemyMovement.Distance((Node2D)attacker.GetParent(), (Node2D)enemy.GetParent());
		if (distance > range) return false;
		// Shaft: bows and polearms cannot be used against a target at Range 0 (p23).
		return !(notZeroRange && distance == 0);
	}
}
