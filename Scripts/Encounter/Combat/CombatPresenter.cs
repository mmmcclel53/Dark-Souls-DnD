using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

// Rolls, shows and applies one attack. CombatResolver stays pure arithmetic; this is the
// layer that rolls each die by hand so the faces can be shown, puts the roll through
// RollReveal, and only then lets the outcome land. Every hit the turn loop or an enemy
// makes goes through here, so nothing is ever applied before the player has seen it.
//
// Without a RollReveal in the scene the reveal is skipped and the outcome lands at once.
public static class CombatPresenter
{

	private static readonly Color BLOCK = new Color(0.4f, 0.62f, 0.96f);
	private static readonly Color RESIST = new Color(0.62f, 0.5f, 0.93f);
	private static readonly Color GOLD = new Color(0.788f, 0.635f, 0.294f);
	private static readonly Color DODGE = new Color(0.45f, 0.82f, 0.42f);

	// ----- Character attacks (p22) -----

	public static async Task<CombatResolver.AttackOutcome> CharacterAttacks(PlayerMove move, Weapon weapon, Enemy target) {
		List<RollReveal.Face> faces = Roll(move.damage);
		int total = Sum(faces) + move.modifier;
		CombatResolver.AttackOutcome outcome = CombatResolver.ResolveAgainstEnemy(total, move, target);

		RollReveal.View view = AttackView(weapon, move, faces, total);
		view.lines.Add(EnemyLine(target, move, outcome));
		await Reveal(view);

		CombatResolver.Apply(outcome, target);
		return outcome;
	}

	// The Node icon: one roll, compared separately against each enemy on the node (p23).
	public static async Task<List<CombatResolver.AttackOutcome>> CharacterAttacksNode(PlayerMove move, Weapon weapon, List<Enemy> targets) {
		List<RollReveal.Face> faces = Roll(move.damage);
		int total = Sum(faces) + move.modifier;
		RollReveal.View view = AttackView(weapon, move, faces, total);

		List<(Enemy target, CombatResolver.AttackOutcome outcome)> results = new List<(Enemy, CombatResolver.AttackOutcome)>();
		foreach (Enemy target in targets) {
			if (!GodotObject.IsInstanceValid(target)) continue;
			CombatResolver.AttackOutcome outcome = CombatResolver.ResolveAgainstEnemy(total, move, target);
			results.Add((target, outcome));
			view.lines.Add(EnemyLine(target, move, outcome));
		}
		await Reveal(view);

		List<CombatResolver.AttackOutcome> outcomes = new List<CombatResolver.AttackOutcome>();
		foreach ((Enemy target, CombatResolver.AttackOutcome outcome) in results) {
			CombatResolver.Apply(outcome, target);
			outcomes.Add(outcome);
		}
		return outcomes;
	}

	// ----- Enemy attacks (p25) -----

	// Block or Resist: the defender rolls every equipped piece's dice against a fixed hit.
	public static async Task<CombatResolver.AttackOutcome> EnemyAttacks(Enemy attacker, EnemyMove move, PlayerToken target) {
		Player player = target.player;
		List<Dice> pool = CharacterActionBar.DefencePool(player, move.isMagic);
		int modifier = CharacterActionBar.DefenceModifier(player, move.isMagic);
		List<RollReveal.Face> faces = Roll(pool);
		int defence = Sum(faces) + modifier;
		CombatResolver.AttackOutcome outcome = CombatResolver.ResolveAgainstCharacter(move, attacker, defence);

		RollReveal reveal = EncounterManager.rollReveal;
		RollReveal.View view = new RollReveal.View {
			actorArt = move.isMagic ? reveal?.resistIcon : reveal?.blockIcon,
			actorTint = move.isMagic ? RESIST : BLOCK,
			actorName = move.isMagic ? "Resist" : "Block",
			faces = faces,
			modifier = modifier,
			total = defence,
		};
		view.lines.Add(AttackerLine(attacker, move, outcome.damage, false));
		await Reveal(view);

		CombatResolver.Apply(outcome, target);
		return outcome;
	}

	// Dodging is all or nothing: enough icons and the character is not hit at all, too few
	// and the full damage lands with no defence roll (p25). The caller has paid the stamina.
	public static async Task<CombatResolver.AttackOutcome> EnemyAttacksDodging(Enemy attacker, EnemyMove move, PlayerToken target) {
		int pool = target.player?.GetDodge() ?? 0;
		List<RollReveal.Face> faces = new List<RollReveal.Face>();
		for (int i = 0; i < pool; i++) faces.Add(RollOne(DodgeDice.Standard));
		int icons = Sum(faces);

		int strength = CombatResolver.AttackStrength(move, attacker);
		bool dodged = icons >= move.dodgeDifficulty;
		CombatResolver.AttackOutcome outcome = dodged
			? new CombatResolver.AttackOutcome(strength, strength, 0, false, EncounterManager.StatusEffect.NONE)
			: new CombatResolver.AttackOutcome(strength, 0, strength, true, move.statusEffect);

		RollReveal reveal = EncounterManager.rollReveal;
		RollReveal.View view = new RollReveal.View {
			actorArt = reveal?.dodgeIcon,
			actorTint = DODGE,
			actorName = "Dodge",
			faces = faces,
			total = icons,
		};
		view.lines.Add(new RollReveal.Line {
			portrait = attacker?.data?.GetPortrait(),
			badge = reveal?.dodgeIcon,
			badgeTint = DODGE,
			badgeValue = move.dodgeDifficulty,
			badgeDrop = 0.1f,
			damage = outcome.damage,
			dodged = dodged,
		});
		await Reveal(view);

		if (!dodged) CombatResolver.Apply(outcome, target);
		return outcome;
	}

	// ----- Views -----

	private static RollReveal.View AttackView(Weapon weapon, PlayerMove move, List<RollReveal.Face> faces, int total) {
		return new RollReveal.View {
			actorArt = weapon == null ? null : EquipmentSlot.CropOf(weapon, EquipmentSlot.DefaultRegionFor(weapon)),
			actorIsCard = true,
			actorName = weapon?.name ?? "",
			faces = faces,
			modifier = move.modifier,
			total = total,
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
