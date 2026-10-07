using Godot;
using System.Collections.Generic;

// What one attack option actually costs and does for this attacker right now: its stamina,
// its range, whether it hits the whole node, whether it is magic, its conditions, and any
// dice and damage added to it. The printed option is the starting point; Stagger, an armed
// Heroic Action, the weapon's gems and the attacker's rings change it. Everything that
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
	// Flat damage added to the roll, on top of the option's own modifier (rings).
	public readonly int bonus;
	// Damage the attacker suffers for making it (Dusk Crown Ring).
	public readonly int selfDamage;
	// Magic after gems: rolled against Resist, and what Spell Fury, Explosive Firepower and
	// the Dusk Crown Ring ask about.
	public readonly bool magic;
	// Conditions the gems add on top of the option's own (Blood, Poison). A hit applies all.
	public readonly List<EncounterManager.StatusEffect> conditions;
	// Hollow Gem: an attack that deals 0 damage resets the Luck token.
	public readonly bool hollow;
	// Damage added against a Hollow target (Hollow Soldier Shield).
	public readonly int vsHollow;

	private AttackTerms(int cost, int range, bool aoe, bool notZeroRange, List<Dice> extraDice, bool reusesWeapon, bool boosted, int bonus, int selfDamage,
			bool magic, List<EncounterManager.StatusEffect> conditions, bool hollow, int vsHollow) {
		this.cost = cost;
		this.range = range;
		this.aoe = aoe;
		this.notZeroRange = notZeroRange;
		this.extraDice = extraDice;
		this.reusesWeapon = reusesWeapon;
		this.boosted = boosted;
		this.bonus = bonus;
		this.selfDamage = selfDamage;
		this.magic = magic;
		this.conditions = conditions;
		this.hollow = hollow;
		this.vsHollow = vsHollow;
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
		int bonus = 0;
		int selfDamage = 0;
		Player wearer = attacker?.player;
		bool magic = move.isMagic;
		List<EncounterManager.StatusEffect> conditions = new List<EncounterManager.StatusEffect>();
		bool hollow = false;

		// Gems first: they decide whether the attack is magic and its range, which the Heroic
		// boosts and rings below then ask about.
		foreach (Gem gem in wearer?.GemsOn(weapon) ?? new List<Gem>()) {
			switch (gem.effect) {
				case Gem.Effect.BLACK_DIE:
					if (Heroic.BlackDie != null) extra.Add(Heroic.BlackDie);
					break;
				case Gem.Effect.BLACK_DIE_MAGIC:
					if (Heroic.BlackDie != null) extra.Add(Heroic.BlackDie);
					magic = true;
					break;
				case Gem.Effect.DAMAGE_MAGIC:
					bonus += 1;
					magic = true;
					break;
				case Gem.Effect.REACH_MAGIC when range == 0:
					range = 1;
					magic = true;
					break;
				case Gem.Effect.MAGIC:
					magic = true;
					break;
				case Gem.Effect.BLEED:
					conditions.Add(EncounterManager.StatusEffect.BLEED);
					break;
				case Gem.Effect.POISON:
					conditions.Add(EncounterManager.StatusEffect.POISON);
					break;
				case Gem.Effect.RAW:
					bonus += 2;
					cost += 1;
					break;
				case Gem.Effect.SIMPLE when IsCostliest(weapon, move):
					cost = Mathf.Max(0, cost - 1);
					break;
				case Gem.Effect.HOLLOW:
					hollow = true;
					break;
				case Gem.Effect.DAMAGE:
					bonus += 1;
					break;
				case Gem.Effect.DAMAGE_TWO:
					bonus += 2;
					break;
			}
		}

		switch (attacker?.pendingHeroic ?? Heroic.Kind.NONE) {
			case Heroic.Kind.SPELL_FURY when magic:
				range = EnemyData.UNLIMITED_RANGE;
				cost = 0;
				boosted = true;
				break;
			case Heroic.Kind.EXPLOSIVE_FIREPOWER when magic:
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
		if (attacker != null && attacker.magicThisActivation) magic = true;
		if (attacker != null) bonus += attacker.bonusThisActivation;
		if (wearer != null) {
			// Xanthous Robes: magic attacks cost 1 less. Black Iron Armour: Node attacks do.
			if (magic && wearer.HasPassive(EquipmentEffect.EffectType.LOSE_STAMINA, EquipmentEffect.Condition.IF_MAGIC_ATTACK)) cost = Mathf.Max(0, cost - 1);
			if (aoe && wearer.HasPassive(EquipmentEffect.EffectType.ATTACK_STAMINA_COST_MOD, EquipmentEffect.Condition.IF_AOE_ATTACK)) cost = Mathf.Max(0, cost - 1);
			if (magic && wearer.HasRing(Ring.Effect.DUSK_CROWN)) {
				cost = Mathf.Max(0, cost - 2);
				selfDamage = 1;
			}
			for (int i = 0; i < wearer.RingCount(Ring.Effect.HORNET); i++) {
				if (Heroic.OrangeDie != null) extra.Add(Heroic.OrangeDie);
				bonus -= 2;
			}
			if (wearer.HasRing(Ring.Effect.RED_TEARSTONE) && wearer.endurance.damageTaken >= 4) bonus += 1;
		}
		if (free) cost = 0;
		// Hollow Soldier Shield: "this weapon gains +1 damage against Hollows".
		int vsHollow = 0;
		if (weapon?.passives != null) {
			foreach (EquipmentEffect e in weapon.passives) {
				if (e != null && e.type == EquipmentEffect.EffectType.BONUS_DAMAGE && e.condition == EquipmentEffect.Condition.IF_ATTACKER_HOLLOW) vsHollow += e.magnitude;
			}
		}
		return new AttackTerms(cost, range, aoe, move.isNotZeroRange, extra, reuses, boosted, bonus, selfDamage, magic, conditions, hollow, vsHollow);
	}

	// Simple Gem: the weapon's highest printed cost, every option tied at it (Matt).
	private static bool IsCostliest(Weapon weapon, PlayerMove move) {
		if (weapon?.attacks == null) return false;
		foreach (PlayerMove other in weapon.attacks) {
			if (other != null && other.staminaCost > move.staminaCost) return false;
		}
		return true;
	}

	public bool Reaches(PlayerToken attacker, Enemy enemy) {
		if (attacker == null || enemy == null) return false;
		int distance = EnemyMovement.Distance((Node2D)attacker.GetParent(), (Node2D)enemy.GetParent());
		if (distance > range) return false;
		// Shaft: bows and polearms cannot be used against a target at Range 0 (p23).
		return !(notZeroRange && distance == 0);
	}
}
