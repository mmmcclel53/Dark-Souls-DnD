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

	// `terms` is the attack as made: its extra dice, bonus, magic and added conditions.
	public static async Task<CombatResolver.AttackOutcome> CharacterAttacks(PlayerToken attacker, PlayerMove move, Weapon weapon, Enemy target, AttackTerms terms) {
		RollReveal.View view = AttackView(weapon, move, Roll(WithExtra(move.damage, terms.extraDice)), attacker?.player, terms.bonus);
		CombatResolver.AttackOutcome outcome = default;
		view.recompute = v => {
			v.total = Sum(v.faces) + move.modifier + terms.bonus;
			outcome = CombatResolver.ResolveAgainstEnemy(v.total + VersusKind(terms, target), move, target, terms.magic);
			v.lines.Clear();
			v.lines.Add(EnemyLine(target, terms.magic, outcome));
		};
		view.recompute(view);

		AttackSlash swing = AttackSlash.Begin(attacker, target, AttackSlash.Style.CHARACTER, FormFor(terms, weapon));
		await Windup(swing);
		await Reveal(view);
		await Land(swing, new[] { BlowFor(target, outcome) });

		CombatResolver.Apply(outcome, target);
		ApplyAdded(terms, target);
		await Settle(swing);
		return outcome;
	}

	// Rapport: no dice and no Block, the enemy simply suffers the damage. The stroke still
	// lands on it.
	public static async Task DirectDamage(PlayerToken attacker, Weapon weapon, Enemy target, AttackTerms terms, int damage) {
		AttackSlash swing = AttackSlash.Begin(attacker, target, AttackSlash.Style.CHARACTER, FormFor(terms, weapon));
		await Windup(swing);
		await Land(swing, new[] { new AttackSlash.Blow(target, AttackSlash.Result.HIT, damage) });
		if (GodotObject.IsInstanceValid(target)) target.ApplyDamage(damage);
		await Settle(swing);
	}

	private static int VersusKind(AttackTerms terms, Enemy target) =>
		target?.data?.kind == EnemyData.Kind.HOLLOW ? terms.vsHollow : 0;

	// The gems' conditions, after the option's own: every attack on an enemy hits.
	private static void ApplyAdded(AttackTerms terms, Enemy target) {
		if (terms.conditions == null || !GodotObject.IsInstanceValid(target)) return;
		foreach (EncounterManager.StatusEffect condition in terms.conditions) target.ApplyCondition(condition);
	}

	// The Node icon: one roll, compared separately against each enemy on the node (p23).
	// One stroke across the node, and an impact on each enemy it reaches.
	public static async Task<List<CombatResolver.AttackOutcome>> CharacterAttacksNode(PlayerToken attacker, PlayerMove move, Weapon weapon, GameNode node, List<Enemy> targets, AttackTerms terms) {
		RollReveal.View view = AttackView(weapon, move, Roll(WithExtra(move.damage, terms.extraDice)), attacker?.player, terms.bonus);
		List<(Enemy target, CombatResolver.AttackOutcome outcome)> results = new List<(Enemy, CombatResolver.AttackOutcome)>();
		view.recompute = v => {
			v.total = Sum(v.faces) + move.modifier + terms.bonus;
			results.Clear();
			v.lines.Clear();
			foreach (Enemy target in targets) {
				if (!GodotObject.IsInstanceValid(target)) continue;
				CombatResolver.AttackOutcome outcome = CombatResolver.ResolveAgainstEnemy(v.total + VersusKind(terms, target), move, target, terms.magic);
				results.Add((target, outcome));
				v.lines.Add(EnemyLine(target, terms.magic, outcome));
			}
		};
		view.recompute(view);

		AttackSlash swing = AttackSlash.Begin(attacker, node, AttackSlash.Style.CHARACTER, FormFor(terms, weapon));
		await Windup(swing);
		await Reveal(view);

		List<AttackSlash.Blow> blows = new List<AttackSlash.Blow>();
		foreach ((Enemy target, CombatResolver.AttackOutcome outcome) in results) blows.Add(BlowFor(target, outcome));
		await Land(swing, blows);

		List<CombatResolver.AttackOutcome> outcomes = new List<CombatResolver.AttackOutcome>();
		foreach ((Enemy target, CombatResolver.AttackOutcome outcome) in results) {
			CombatResolver.Apply(outcome, target);
			ApplyAdded(terms, target);
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

		// Sunlight Shield: a character on the target's node may take the damage instead.
		PlayerToken shield = await SunlightShield(target, outcome.damage);
		if (shield != null) {
			CombatResolver.Apply(new CombatResolver.AttackOutcome(outcome.roll, outcome.mitigation, 0, outcome.hit, outcome.condition), target);
			shield.ApplyDamage(outcome.damage, attacker.Presence);
		} else {
			CombatResolver.Apply(outcome, target, attacker.Presence);
		}
		Thorns(target, attacker);
		await Settle(swing);
		return outcome;
	}

	// Sunlight Shield: "when a character in your node would suffer damage, you can suffer the
	// damage instead". The bearer is asked; the hit's condition stays with who was hit.
	private static async Task<PlayerToken> SunlightShield(PlayerToken target, int damage) {
		if (damage <= 0 || !GodotObject.IsInstanceValid(target)) return null;
		CharacterActionBar bar = EncounterManager.characterTurn?.actionBar;
		if (bar == null || target.GetParent()?.GetParent() is not Control node) return null;
		foreach (Node2D model in EncounterManager.GetPlayersInNode(node, "Player")) {
			PlayerToken bearer = EncounterManager.GetPlayerToken(model);
			if (bearer == null || bearer == target || !bearer.player.HasPassive(EquipmentEffect.EffectType.REDIRECT_DAMAGE, EquipmentEffect.Condition.IF_ALLY_DAMAGED_SAME_NODE)) continue;
			int choice = await bar.AskChoice(bearer.player, ShieldArt(bearer.player),
				new List<(Control, string)> { (CubeRow.Damage(damage, 10f), $"Suffer {damage} damage instead of {target.player.name}") }, "Skip");
			if (choice == 0) return bearer;
		}
		return null;
	}

	private static Texture2D ShieldArt(Player player) {
		foreach (Weapon w in new[] { player.GetLeftHand(), player.GetRightHand() }) {
			if (w?.passives == null) continue;
			foreach (EquipmentEffect e in w.passives) {
				if (e != null && e.type == EquipmentEffect.EffectType.REDIRECT_DAMAGE) return EquipmentSlot.CropOf(w, EquipmentSlot.DefaultRegionFor(w));
			}
		}
		return null;
	}

	// Armour of Thorns: after a Block or Resist roll against an enemy in your node, it suffers
	// 1 damage. The V2 failed dodge's armour roll is a Block or Resist roll too.
	private static void Thorns(PlayerToken defender, Enemy attacker) {
		if (!GodotObject.IsInstanceValid(defender) || !GodotObject.IsInstanceValid(attacker) || attacker.isDead) return;
		EquipmentEffect thorns = defender.player.Passive(EquipmentEffect.EffectType.BONUS_DAMAGE, EquipmentEffect.Condition.IF_BLOCKING);
		if (thorns == null || attacker.GetParent()?.GetParent() != defender.GetParent()?.GetParent()) return;
		attacker.ApplyDamage(Mathf.Max(1, thorns.magnitude));
	}

	// Enough icons and the character is not hit at all (p25). Too few and, under the V2
	// rules, they still block with their armour's dice alone rather than taking the full
	// damage: a second roll, on its own reveal. The caller has paid the stamina.
	// `rolled` is the Wolf Ring's: dodge dice already rolled before the choice was made.
	public static async Task<CombatResolver.AttackOutcome> EnemyAttacksDodging(Enemy attacker, EnemyMove move, PlayerToken target, AttackSlash swing = null, List<RollReveal.Face> rolled = null) {
		List<RollReveal.Face> faces = rolled ?? RollDodge(target, attacker);
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
	private static AttackSlash.Form FormFor(AttackTerms terms, Weapon weapon) {
		bool magic = terms.magic || weapon?.type == Equipment.EquipmentType.Spell;
		return AttackSlash.FormFor(magic, Mathf.Min(terms.range, 99));
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

	private static RollReveal.View AttackView(Weapon weapon, PlayerMove move, List<RollReveal.Face> faces, Player roller, int bonus) {
		return new RollReveal.View {
			actorArt = weapon == null ? null : EquipmentSlot.CropOf(weapon, EquipmentSlot.DefaultRegionFor(weapon)),
			actorIsCard = true,
			actorName = weapon?.name ?? "",
			faces = faces,
			modifier = move.modifier + bonus,
			luckOwner = roller,
		};
	}

	private static RollReveal.Line EnemyLine(Enemy target, bool magic, CombatResolver.AttackOutcome outcome) {
		RollReveal reveal = EncounterManager.rollReveal;
		return new RollReveal.Line {
			portrait = target?.data?.GetPortrait(),
			badge = magic ? reveal?.resistIcon : reveal?.blockIcon,
			badgeTint = magic ? RESIST : BLOCK,
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

	public static List<RollReveal.Face> RollDodge(PlayerToken target, Enemy attacker) {
		List<RollReveal.Face> faces = new List<RollReveal.Face>();
		int pool = CombatResolver.DodgePool(target, attacker);
		for (int i = 0; i < pool; i++) faces.Add(RollOne(DodgeDice.Standard));
		return faces;
	}

	public static int Icons(List<RollReveal.Face> faces) => faces == null ? 0 : Sum(faces);

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
