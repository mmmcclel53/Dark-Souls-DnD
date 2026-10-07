using Godot;
using Godot.Collections;
using System.Collections.Generic;

// Dice and arithmetic for a single attack. Deliberately free of turn order, targeting and
// UI: the turn loop decides who swings at whom and pays the Stamina, then calls in here.
//
// Rolling and resolving are separate on purpose. An attack with the Node icon rolls once
// and compares that one total against every enemy on the node (p23), so the roll has to be
// available without being spent.
public static class CombatResolver
{

	public readonly struct AttackOutcome {

		public readonly int roll;          // dice total plus the attack's flat modifier
		public readonly int mitigation;    // Block/Resist, or the defender's defence roll
		public readonly int damage;        // what actually lands after mitigation
		public readonly bool hit;          // p20: a hit at 0 damage still pushes and still applies conditions
		public readonly EncounterManager.StatusEffect condition;

		public AttackOutcome(int roll, int mitigation, int damage, bool hit, EncounterManager.StatusEffect condition) {
			this.roll = roll;
			this.mitigation = mitigation;
			this.damage = damage;
			this.hit = hit;
			this.condition = condition;
		}
	}

	// ----- Rolling -----

	// Each pip rolled is 1 damage (p22).
	public static int RollPips(Array<Dice> dice) {
		if (dice == null) return 0;
		int total = 0;
		foreach (Dice die in dice) {
			if (die?.dice != null && die.dice.Length > 0) total += DiceUtility.Roll(die);
		}
		return total;
	}

	public static int RollAttack(PlayerMove move) => RollPips(move.damage) + move.modifier;

	// Block (physical) or Resist (magic) gathered from every equipped piece (p25).
	public static int RollDefence(Player defender, bool magic) {
		if (defender == null) return 0;

		Armour armour = defender.GetArmour();
		int total = magic
			? RollPips(armour?.magicDefense) + (armour?.magicDefenseModifier ?? 0)
			: RollPips(armour?.physicalDefense) + (armour?.physicalDefenseModifier ?? 0);

		foreach (Weapon weapon in new[] { defender.GetLeftHand(), defender.GetRightHand() }) {
			total += RollPips(magic ? weapon?.magicDefense : weapon?.physicalDefense);
		}
		return total;
	}

	// ----- Resolution -----

	// A character's attack against one enemy, given an already-made roll.
	// `magic` is the attack's after gems; null means the option as printed.
	public static AttackOutcome ResolveAgainstEnemy(int attackRoll, PlayerMove move, Enemy target, bool? magic = null) {
		int mitigation = move.isIgnoreDefense
			? 0
			: ((magic ?? move.isMagic) ? target.magicalDefense : target.physicalDefense);

		int damage = Mathf.Max(0, attackRoll - mitigation);
		return new AttackOutcome(attackRoll, mitigation, damage, true, move.statusEffect);
	}

	// An enemy's attack. Enemy damage is a fixed value the defender rolls to reduce (p25).
	public static AttackOutcome ResolveAgainstCharacter(EnemyMove move, Enemy attacker, int defenceRoll) {
		int strength = AttackStrength(move, attacker);
		int damage = Mathf.Max(0, strength - defenceRoll);
		return new AttackOutcome(strength, defenceRoll, damage, true, move.statusEffect);
	}

	// The attacker's tier adds to its attack damage values; Stagger knocks 1 off them (p21).
	// A 0-damage movement push stays harmless at any tier.
	public static int AttackStrength(EnemyMove move, Enemy attacker) {
		int strength = move.damage;
		if (attacker != null && strength > 0) {
			strength += attacker.TierDamageBonus;
		}
		if (attacker != null && attacker.HasCondition(EncounterManager.StatusEffect.STAGGER)) {
			strength = Mathf.Max(0, strength - 1);
		}
		return strength;
	}

	// ----- Roll, resolve and apply -----

	public static AttackOutcome CharacterAttacks(PlayerMove move, Enemy target) {
		AttackOutcome outcome = ResolveAgainstEnemy(RollAttack(move), move, target);
		Apply(outcome, target);
		return outcome;
	}

	// The Node icon: one roll, compared separately against each enemy on the node (p23).
	public static List<AttackOutcome> CharacterAttacksNode(PlayerMove move, List<Enemy> targets) {
		int attackRoll = RollAttack(move);
		List<AttackOutcome> outcomes = new List<AttackOutcome>();

		foreach (Enemy target in targets) {
			if (!GodotObject.IsInstanceValid(target)) continue;
			AttackOutcome outcome = ResolveAgainstEnemy(attackRoll, move, target);
			Apply(outcome, target);
			outcomes.Add(outcome);
		}
		return outcomes;
	}

	public static AttackOutcome EnemyAttacks(Enemy attacker, EnemyMove move, PlayerToken target) {
		AttackOutcome outcome = ResolveAgainstCharacter(move, attacker, RollDefence(target.player, move.isMagic));
		Apply(outcome, target);
		return outcome;
	}

	public static void Apply(AttackOutcome outcome, Enemy target) {
		if (!outcome.hit || !GodotObject.IsInstanceValid(target)) return;
		if (outcome.damage > 0) target.ApplyDamage(outcome.damage);
		target.ApplyCondition(outcome.condition);
	}

	public static void Apply(AttackOutcome outcome, PlayerToken target, float attackerPresence = 1f) {
		if (!outcome.hit || !GodotObject.IsInstanceValid(target)) return;
		int before = target.player.endurance.damageTaken;
		if (outcome.damage > 0) target.ApplyDamage(outcome.damage, attackerPresence);
		target.ApplyCondition(outcome.condition);
		// Blue Tearstone Ring: damage suffered from an enemy attack gives 1 stamina back.
		bool hurt = target.player.endurance.damageTaken > before;
		if (hurt && !target.player.endurance.isDead && target.player.HasRing(Ring.Effect.BLUE_TEARSTONE)) target.RecoverStamina(1);
	}

	// The dodge dice: armour and hands, and the Obscuring Ring's 2 more against an attacker
	// 2 or more nodes away (the same distance as range).
	public static int DodgePool(PlayerToken target, Enemy attacker) {
		Player player = target?.player;
		if (player == null) return 0;
		int pool = player.GetDodge();
		if (attacker != null && player.HasRing(Ring.Effect.OBSCURING)
			&& EnemyMovement.Distance((Node2D)attacker.GetParent(), (Node2D)target.GetParent()) >= 2) pool += 2;
		// Dark Armour: +1 die against a Hollow.
		if (attacker?.data?.kind == EnemyData.Kind.HOLLOW) {
			pool += player.Passive(EquipmentEffect.EffectType.DODGE_DICE, EquipmentEffect.Condition.IF_ATTACKER_HOLLOW)?.magnitude ?? 0;
		}
		return pool;
	}

	// Tower Shield, Havel's and Stone Greatshield, Smough's Armour: "cannot dodge"; Havel's
	// Armour: "cannot dodge or walk".
	public static bool CanDodge(PlayerToken target) {
		Player player = target?.player;
		return player != null && !player.HasPassive(EquipmentEffect.EffectType.CANNOT_DODGE)
			&& !player.HasPassive(EquipmentEffect.EffectType.CANNOT_MOVE);
	}

	// ----- Dodging (p25) -----

	// Each equipped item contributes dodge dice; the faces are 0/1 so the total IS the
	// number of dodge icons rolled.
	public static int RollDodge(Player defender) {
		int pool = defender?.GetDodge() ?? 0;
		int icons = 0;
		for (int i = 0; i < pool; i++) icons += DiceUtility.Roll(DodgeDice.Standard);
		return icons;
	}

	// Dodging replaces the Block/Resist roll and is all or nothing: succeed and the
	// character is not hit at all, so no damage, no push and no condition (p20). Fail and
	// they take the attack's FULL damage with no defence roll to soften it.
	//
	// The caller pays the 1 Stamina and may move the character 1 node; that is a decision
	// made during the enemy's activation, not part of the arithmetic.
	public static AttackOutcome EnemyAttacksDodging(Enemy attacker, EnemyMove move, PlayerToken target) {
		int strength = AttackStrength(move, attacker);

		if (RollDodge(target.player) >= move.dodgeDifficulty) {
			return new AttackOutcome(strength, strength, 0, false, EncounterManager.StatusEffect.NONE);
		}

		AttackOutcome outcome = new AttackOutcome(strength, 0, strength, true, move.statusEffect);
		Apply(outcome, target);
		return outcome;
	}

	// ----- Condition cost modifiers (p21) -----

	public static int AttackStaminaCost(PlayerToken attacker, PlayerMove move) {
		int cost = move.staminaCost;
		if (attacker != null && attacker.HasCondition(EncounterManager.StatusEffect.STAGGER)) cost += 1;
		return cost;
	}

	// Frostbite adds 1 to each walk, run or dodge (p21).
	// A dodge's stamina. Free with the Carthus Milkring or Black Leather Armour, and with the
	// Alonne armours against an Alonne, Frostbite included ("without spending stamina").
	// Eastern Armour: 2 instead of 1.
	public static int DodgeStaminaCost(PlayerToken dodger, Enemy attacker = null) {
		Player player = dodger?.player;
		if (player != null) {
			if (player.HasRing(Ring.Effect.CARTHUS_MILKRING)) return 0;
			EquipmentEffect always = player.Passive(EquipmentEffect.EffectType.DODGE_STAMINA_MOD);
			if (always != null && always.magnitude == 0) return 0;
			if (attacker?.data?.kind == EnemyData.Kind.ALONNE
				&& player.HasPassive(EquipmentEffect.EffectType.DODGE_STAMINA_MOD, EquipmentEffect.Condition.IF_ATTACKER_ALONNE)) return 0;
		}
		int cost = 1 + (player?.Passive(EquipmentEffect.EffectType.DODGE_STAMINA_MOD)?.magnitude ?? 0);
		if (dodger != null && dodger.HasCondition(EncounterManager.StatusEffect.FROST)) cost += 1;
		return cost;
	}

	public static int MoveStaminaCost(PlayerToken mover, int nodes) {
		int perNode = 1;
		if (mover != null && mover.HasCondition(EncounterManager.StatusEffect.FROST)) perNode += 1;
		return nodes * perNode;
	}

}
