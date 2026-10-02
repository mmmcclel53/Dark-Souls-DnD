using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

// Rolls, shows and applies one attack. CombatResolver stays pure arithmetic; this is the
// layer that rolls each die by hand so the faces can be shown, puts the roll through
// RollReveal, and only then lets the outcome land. Every hit the turn loop or an enemy
// makes goes through here, so nothing is ever applied before the player has seen it.
//
// The stroke on the board (AttackSlash) is timed around the reveal: it winds up and holds
// before the dice, comes down as they are dismissed, and the outcome is applied at the
// moment of contact so the hit and the numbers land together. A character's swing starts
// here; an enemy's is handed in already held, because it has to wait through the
// Block-or-Dodge choice first.
//
// Without a RollReveal in the scene the reveal is skipped and the outcome lands at once.
public static class CombatPresenter
{

	private static readonly Color BLOCK = new Color(0.4f, 0.62f, 0.96f);
	private static readonly Color RESIST = new Color(0.62f, 0.5f, 0.93f);
	private static readonly Color GOLD = new Color(0.788f, 0.635f, 0.294f);
	private static readonly Color DODGE = new Color(0.45f, 0.82f, 0.42f);

	// ----- Character attacks (p22) -----

	public static async Task<CombatResolver.AttackOutcome> CharacterAttacks(PlayerToken attacker, PlayerMove move, Weapon weapon, Enemy target, IEnumerable<Dice> extraDice = null) {
		RollReveal.View view = AttackView(weapon, move, Roll(WithExtra(move.damage, extraDice)), attacker?.player);
		CombatResolver.AttackOutcome outcome = default;
		view.recompute = v => {
			v.total = Sum(v.faces) + move.modifier;
			outcome = CombatResolver.ResolveAgainstEnemy(v.total, move, target);
			v.lines.Clear();
			v.lines.Add(EnemyLine(target, move, outcome));
		};
		view.recompute(view);

		AttackSlash swing = AttackSlash.Begin(attacker, target, AttackSlash.Style.CHARACTER, FormFor(move, weapon));
		await Windup(swing);
		await Reveal(view);
		await Land(swing, new[] { BlowFor(target, outcome) });

		CombatResolver.Apply(outcome, target);
		await Settle(swing);
		return outcome;
	}

	// The Node icon: one roll, compared separately against each enemy on the node (p23).
	// One stroke across the node, and an impact on each enemy it reaches.
	public static async Task<List<CombatResolver.AttackOutcome>> CharacterAttacksNode(PlayerToken attacker, PlayerMove move, Weapon weapon, GameNode node, List<Enemy> targets, IEnumerable<Dice> extraDice = null) {
		RollReveal.View view = AttackView(weapon, move, Roll(WithExtra(move.damage, extraDice)), attacker?.player);
		List<(Enemy target, CombatResolver.AttackOutcome outcome)> results = new List<(Enemy, CombatResolver.AttackOutcome)>();
		view.recompute = v => {
			v.total = Sum(v.faces) + move.modifier;
			results.Clear();
			v.lines.Clear();
			foreach (Enemy target in targets) {
				if (!GodotObject.IsInstanceValid(target)) continue;
				CombatResolver.AttackOutcome outcome = CombatResolver.ResolveAgainstEnemy(v.total, move, target);
				results.Add((target, outcome));
				v.lines.Add(EnemyLine(target, move, outcome));
			}
		};
		view.recompute(view);

		AttackSlash swing = AttackSlash.Begin(attacker, node, AttackSlash.Style.CHARACTER, FormFor(move, weapon));
		await Windup(swing);
		await Reveal(view);

		List<AttackSlash.Blow> blows = new List<AttackSlash.Blow>();
		foreach ((Enemy target, CombatResolver.AttackOutcome outcome) in results) blows.Add(BlowFor(target, outcome));
		await Land(swing, blows);

		List<CombatResolver.AttackOutcome> outcomes = new List<CombatResolver.AttackOutcome>();
		foreach ((Enemy target, CombatResolver.AttackOutcome outcome) in results) {
			CombatResolver.Apply(outcome, target);
			outcomes.Add(outcome);
		}
		await Settle(swing);
		return outcomes;
	}

	// ----- Enemy attacks (p25) -----

	// Block or Resist: the defender rolls every equipped piece's dice against a fixed hit.
	// `armourOnly` is the V2 failed dodge: the armour slot's dice alone, not the full Block.
	public static async Task<CombatResolver.AttackOutcome> EnemyAttacks(Enemy attacker, EnemyMove move, PlayerToken target, AttackSlash swing = null, bool armourOnly = false) {
		Player player = target.player;
		(List<Dice> pool, int modifier) = armourOnly ? player.GetArmourPool(move.isMagic) : player.GetDefensePool(move.isMagic);
		RollReveal reveal = EncounterManager.rollReveal;
		RollReveal.View view = new RollReveal.View {
			actorArt = move.isMagic ? reveal?.resistIcon : reveal?.blockIcon,
			actorTint = move.isMagic ? RESIST : BLOCK,
			actorName = move.isMagic ? "Resist" : "Block",
			faces = Roll(pool),
			modifier = modifier,
			againstCharacter = true,
			luckOwner = player,
		};
		// The Knight's Stand Fast: after blocking, one more blue die on the roll.
		if (target.heroic == Heroic.Kind.STAND_FAST && !player.heroicUsed) {
			view.heroicOwner = player;
			view.heroicDie = Heroic.BlueDie;
		}
		CombatResolver.AttackOutcome outcome = default;
		view.recompute = v => {
			v.total = Sum(v.faces) + modifier;
			outcome = CombatResolver.ResolveAgainstCharacter(move, attacker, v.total);
			v.lines.Clear();
			v.lines.Add(AttackerLine(attacker, move, outcome.damage, false));
		};
		view.recompute(view);
		await Reveal(view);
		await Land(swing, new[] { BlowFor(target, outcome) });

		CombatResolver.Apply(outcome, target);
		await Settle(swing);
		return outcome;
	}

	// Enough icons and the character is not hit at all (p25). Too few and, under the V2
	// rules, they still block with their armour's dice alone rather than taking the full
	// damage: a second roll, on its own reveal. The caller has paid the stamina.
	public static async Task<CombatResolver.AttackOutcome> EnemyAttacksDodging(Enemy attacker, EnemyMove move, PlayerToken target, AttackSlash swing = null) {
		int pool = target.player?.GetDodge() ?? 0;
		List<RollReveal.Face> faces = new List<RollReveal.Face>();
		for (int i = 0; i < pool; i++) faces.Add(RollOne(DodgeDice.Standard));
		int strength = CombatResolver.AttackStrength(move, attacker);

		RollReveal reveal = EncounterManager.rollReveal;
		RollReveal.View view = new RollReveal.View {
			actorArt = reveal?.dodgeIcon,
			actorTint = DODGE,
			actorName = "Dodge",
			faces = faces,
			againstCharacter = true,
			luckOwner = target.player,
		};
		bool dodged = false;
		CombatResolver.AttackOutcome outcome = default;
		view.recompute = v => {
			v.total = Sum(v.faces);
			dodged = v.total >= move.dodgeDifficulty;
			outcome = dodged
				? new CombatResolver.AttackOutcome(strength, strength, 0, false, EncounterManager.StatusEffect.NONE)
				: new CombatResolver.AttackOutcome(strength, 0, strength, true, move.statusEffect);
			v.lines.Clear();
			v.lines.Add(new RollReveal.Line {
				portrait = attacker?.data?.GetPortrait(),
				badge = reveal?.dodgeIcon,
				badgeTint = DODGE,
				badgeValue = move.dodgeDifficulty,
				badgeDrop = 0.1f,
				damage = outcome.damage,
				dodged = dodged,
			});
		};
		view.recompute(view);
		await Reveal(view);
		if (!dodged) return await EnemyAttacks(attacker, move, target, swing, armourOnly: true);

		await Land(swing, new[] { BlowFor(target, outcome) });
		await Settle(swing);
		return outcome;
	}

	// ----- The stroke on the board -----

	// A spell card or a magic attack casts; a weapon that reaches two nodes shoots; the
	// rest swing. The option's own range replaces the weapon's when it has one (p23).
	private static AttackSlash.Form FormFor(PlayerMove move, Weapon weapon) {
		bool magic = move.isMagic || weapon?.type == Equipment.EquipmentType.Spell;
		int range = move.attackRange > 0 ? move.attackRange : (weapon?.attackRange ?? 0);
		return AttackSlash.FormFor(magic, range);
	}

	private static AttackSlash.Blow BlowFor(Control target, CombatResolver.AttackOutcome outcome) {
		AttackSlash.Result result = !outcome.hit ? AttackSlash.Result.DODGED
			: outcome.damage > 0 ? AttackSlash.Result.HIT
			: AttackSlash.Result.BLOCKED;
		return new AttackSlash.Blow(target, result, outcome.damage);
	}

	private static bool Alive(AttackSlash swing) => swing != null && GodotObject.IsInstanceValid(swing);

	private static async Task Windup(AttackSlash swing) {
		if (Alive(swing)) await swing.Windup();
	}

	private static async Task Land(AttackSlash swing, IEnumerable<AttackSlash.Blow> blows) {
		if (Alive(swing)) await swing.Land(blows);
	}

	private static async Task Settle(AttackSlash swing) {
		if (Alive(swing)) await swing.Settle();
	}

	// ----- Views -----

	private static RollReveal.View AttackView(Weapon weapon, PlayerMove move, List<RollReveal.Face> faces, Player roller) {
		return new RollReveal.View {
			actorArt = weapon == null ? null : EquipmentSlot.CropOf(weapon, EquipmentSlot.DefaultRegionFor(weapon)),
			actorIsCard = true,
			actorName = weapon?.name ?? "",
			faces = faces,
			modifier = move.modifier,
			luckOwner = roller,
		};
	}

	private static RollReveal.Line EnemyLine(Enemy target, PlayerMove move, CombatResolver.AttackOutcome outcome) {
		RollReveal reveal = EncounterManager.rollReveal;
		return new RollReveal.Line {
			portrait = target?.data?.GetPortrait(),
			badge = move.isMagic ? reveal?.resistIcon : reveal?.blockIcon,
			badgeTint = move.isMagic ? RESIST : BLOCK,
			badgeValue = outcome.mitigation,
			damage = outcome.damage,
		};
	}

	private static RollReveal.Line AttackerLine(Enemy attacker, EnemyMove move, int damage, bool dodged) {
		RollReveal reveal = EncounterManager.rollReveal;
		return new RollReveal.Line {
			portrait = attacker?.data?.GetPortrait(),
			badge = move.isMagic ? reveal?.magicIcon : reveal?.physicalIcon,
			badgeTint = GOLD,
			badgeValue = CombatResolver.AttackStrength(move, attacker),
			badgeDrop = move.isMagic ? 0f : 0.08f,
			damage = damage,
			dodged = dodged,
		};
	}

	// ----- Rolling -----

	private static List<Dice> WithExtra(IEnumerable<Dice> dice, IEnumerable<Dice> extra) {
		List<Dice> all = new List<Dice>();
		if (dice != null) all.AddRange(dice);
		if (extra != null) all.AddRange(extra);
		return all;
	}

	private static List<RollReveal.Face> Roll(IEnumerable<Dice> dice) {
		List<RollReveal.Face> faces = new List<RollReveal.Face>();
		if (dice == null) return faces;
		foreach (Dice die in dice) {
			if (die?.dice == null || die.dice.Length == 0) continue;
			faces.Add(RollOne(die));
		}
		return faces;
	}

	private static RollReveal.Face RollOne(Dice die) =>
		new RollReveal.Face { type = die.diceType, value = DiceUtility.Roll(die), faces = die.dice };

	private static int Sum(List<RollReveal.Face> faces) {
		int total = 0;
		foreach (RollReveal.Face face in faces) total += face.value;
		return total;
	}

	private static async Task Reveal(RollReveal.View view) {
		RollReveal reveal = EncounterManager.rollReveal;
		if (reveal == null || !GodotObject.IsInstanceValid(reveal)) return;
		await reveal.Present(view);
	}
}
