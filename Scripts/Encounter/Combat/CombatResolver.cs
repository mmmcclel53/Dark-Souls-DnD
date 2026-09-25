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

		foreach (Weapon weapon in new[] { defender.GetLeftHand(), defender.GetRightHand(), defender.GetBackupSlot() }) {
			total += RollPips(magic ? weapon?.magicDefense : weapon?.physicalDefense);
		}
		return total;
	}

	// ----- Resolution -----

	// A character's attack against one enemy, given an already-made roll.
	public static AttackOutcome ResolveAgainstEnemy(int attackRoll, PlayerMove move, Enemy target) {
		int mitigation = move.isIgnoreDefense
			? 0
			: (move.isMagic ? target.magicalDefense : target.physicalDefense);

		int damage = Mathf.Max(0, attackRoll - mitigation);
		return new AttackOutcome(attackRoll, mitigation, damage, true, move.statusEffect);
	}

	// An enemy's attack. Enemy damage is a fixed value the defender rolls to reduce (p25).
	public static AttackOutcome ResolveAgainstCharacter(EnemyMove move, Enemy attacker, int defenceRoll) {
		int strength = AttackStrength(move, attacker);
		int damage = Mathf.Max(0, strength - defenceRoll);
		return new AttackOutcome(strength, defenceRoll, damage, true, move.statusEffect);
	}

	// Stagger on the attacker knocks 1 off its attack damage values (p21).
	public static int AttackStrength(EnemyMove move, Enemy attacker) {
		int strength = move.damage;
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

	public static void Apply(AttackOutcome outcome, PlayerToken target) {
		if (!outcome.hit || !GodotObject.IsInstanceValid(target)) return;
		if (outcome.damage > 0) target.ApplyDamage(outcome.damage);
		target.ApplyCondition(outcome.condition);
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
	public static int DodgeStaminaCost(PlayerToken dodger) {
		int cost = 1;
		if (dodger != null && dodger.HasCondition(EncounterManager.StatusEffect.FROST)) cost += 1;
		return cost;
	}

	public static int MoveStaminaCost(PlayerToken mover, int nodes) {
		int perNode = 1;
		if (mover != null && mover.HasCondition(EncounterManager.StatusEffect.FROST)) perNode += 1;
		return nodes * perNode;
	}

}
