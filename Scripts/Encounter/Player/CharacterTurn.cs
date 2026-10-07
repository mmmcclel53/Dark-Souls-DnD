using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

// Owns everything the player does during one character activation: stepping node to node,
// arming a weapon attack, and picking what it hits. ActionListener still owns the turn
// order and simply hands the activation over.
//
// Movement is deliberately one node per click (p22): the first step of an activation is the
// free Walk and each one after is a Run, so the cost only makes sense a step at a time.
//
// An attack is armed, then committed by its first hit or Shift step, which is when it is paid
// for, once however many times it Repeats. From there it runs until its uses are made or the
// player says Done: each use is the Shift before the dice (a node click is a step, an enemy
// click the roll), the roll, then the Shift after the dice (p23). Shift steps are free and
// are not the Walk or a Run, so the movement lock (p22) does not apply to them.
public partial class CharacterTurn : Node
{

	[Signal] public delegate void ActivationEndedEventHandler();
	[Signal] public delegate void AttackResolvedEventHandler();
	[Signal] public delegate void DodgeStepEndedEventHandler();

	[Export] public CharacterActionBar actionBar;

	[Export] public Color moveColour = new Color(0.79f, 0.64f, 0.29f, 0.45f);
	[Export] public Color targetColour = new Color(0.64f, 0.17f, 0.13f, 0.55f);
	[Export] public Color targetRingColour = new Color(0.85f, 0.18f, 0.12f);
	[Export] public Color pickRingColour = new Color(0.95f, 0.8f, 0.4f);
	[Export] public Color pickedRingColour = new Color(1f, 1f, 1f);

	public PlayerToken active { get; private set; }
	public bool isTargeting => armedMove != null;
	public PlayerMove armed => armedMove ?? underway?.move;
	public bool isCommitted => underway != null;
	public Weapon committedWeapon => underway?.weapon;

	private Weapon armedWeapon;
	private PlayerMove armedMove;
	private readonly List<GameNode> highlighted = new List<GameNode>();
	private readonly List<TokenHighlight> targetRings = new List<TokenHighlight>();
	private readonly List<Enemy> targetable = new List<Enemy>();
	private readonly List<GameNode> targetNodes = new List<GameNode>();
	private readonly List<GameNode> shiftNodes = new List<GameNode>();

	// The attack under way, from the moment it is paid for until its last use.
	private class Underway
	{
		public Weapon weapon;
		public PlayerMove move;
		public AttackTerms terms;
		public int usesLeft;
		public bool recorded;
		// Repeat's targeting constraint, fixed by the first use.
		public Enemy lockedEnemy;
		public GameNode lockedNode;
	}
	private Underway underway;
	// Shift nodes left at this stage of the attack, and whether it is the Shift after the dice.
	private int shiftLeft;
	private bool shiftingAfter;

	// Choosing characters for a heal, a buff or a rider: the candidates are ringed on the board
	// and a click on one (or on its node) picks it. "All" options pick everyone with one
	// click; "one node" options pick a node's worth. The End button is Done or Cancel.
	private TaskCompletionSource<List<PlayerToken>> pick;
	private List<PlayerToken> pickCandidates = new List<PlayerToken>();
	private readonly List<PlayerToken> picked = new List<PlayerToken>();
	private int pickMax;
	private bool pickAll;
	private bool pickByNode;
	private string pickCancelLabel;
	private readonly List<TokenHighlight> pickRings = new List<TokenHighlight>();
	private readonly List<GameNode> pickNodes = new List<GameNode>();
	public bool isPicking => pick != null;
	public string endLabel => pick == null ? null : picked.Count > 0 ? "Done" : pickCancelLabel;

	// V2: the move after a successful dodge is a choice, and costs 1 stamina.
	private const int DODGE_STEP_COST = 1;

	// Backstab's choice of attack, while the Assassin is making it.
	private TaskCompletionSource<(Weapon, PlayerMove)> backstabChoice;

	// The character taking the free step a successful dodge grants (p22), while it lasts.
	private PlayerToken dodger;
	public bool isDodgeStepping => dodger != null;
	// Nodes left in this dodge step: 2 with the Dark Wood Grain Ring, paid once.
	private int dodgeStepsLeft;
	private bool dodgeStepPaid;

	public override void _Ready() {
		EncounterManager.characterTurn = this;

		if (actionBar != null) {
			actionBar.AttackChosen += OnAttackChosen;
			// While an attack is under way the button reads Done and ends that stage of it.
			actionBar.EndActivationPressed += () => {
				if (pick != null) FinishPick(picked.Count > 0 ? new List<PlayerToken>(picked) : null);
				else if (underway != null) Done();
				else EmitSignal(SignalName.ActivationEnded);
			};
			actionBar.DodgeStepEnded += () => EmitSignal(SignalName.DodgeStepEnded);
			actionBar.HeroicChosen += UseHeroic;
			actionBar.EquipmentSwapped += OnEquipmentSwapped;
			actionBar.BackstabPassed += () => backstabChoice?.TrySetResult((null, null));
			actionBar.ShowIdle();
		}
	}

	// Async for the Tiny Being's Ring, whose wearer chooses how the activation's gain comes.
	public async void Begin(PlayerToken token) {
		active = token;
		DropAttack();
		if (token.pendingStartGain > 0) {
			ClearHighlights();
			bool asHealth = actionBar != null && await actionBar.AskStartGain(token, token.pendingStartGain);
			if (active != token || !GodotObject.IsInstanceValid(token)) return;
			token.TakeStartGain(asHealth);
		}
		if (actionBar != null) actionBar.Refresh(token);
		ShowMoveOptions();
	}

	public void End() {
		FinishPick(null);
		DropAttack();
		ClearHighlights();
		active = null;
		if (actionBar != null) actionBar.ShowIdle();
	}

	// ----- The dodge step (p22) -----

	// A character who dodged may move one node. Runs inside the enemy's activation, which
	// waits on it: the adjacent nodes light, the bar offers End Dodge, and either a node
	// click or that button finishes it. The stamina was paid with the dodge itself.
	public async Task OfferDodgeStep(PlayerToken token) {
		if (token == null || !GodotObject.IsInstanceValid(token) || !token.CanSpend(DODGE_STEP_COST)) return;

		dodger = token;
		// Dark Wood Grain Ring, Shadow Armour: "you may move 2 nodes (instead of 1)".
		dodgeStepsLeft = token.player.HasRing(Ring.Effect.DARK_WOOD_GRAIN) || token.player.HasPassive(EquipmentEffect.EffectType.DODGE_RANGE) ? 2 : 1;
		dodgeStepPaid = false;
		LightDodgeStep(token);
		actionBar?.ShowDodgeStep(token);

		await ToSignal(this, SignalName.DodgeStepEnded);

		ClearHighlights();
		dodger = null;
		actionBar?.ShowIdle();
	}

	private void LightDodgeStep(PlayerToken token) {
		ClearHighlights();
		foreach (GameNode node in AdjacentNodes(token)) {
			node.Highlight(moveColour, true);
			highlighted.Add(node);
		}
	}

	public async void OnDodgeStepNodeClicked(GameNode node) {
		if (dodger == null || !highlighted.Contains(node)) return;

		PlayerToken stepping = dodger;
		Node2D self = (Node2D)stepping.GetParent();
		if (!dodgeStepPaid && !stepping.SpendStamina(DODGE_STEP_COST)) return;
		dodgeStepPaid = true;
		ClearHighlights();
		PathNode from = EncounterManager.pathGrid.NodeFromObj(self);
		if (EncounterManager.MovePlayer(self, node, (Control)self.GetParent())) {
			stepping.lastStep = Pushing.Step(from, EncounterManager.pathGrid.NodeFromObj(self));
			EncounterManager.ApplyNodeHazard(node, self);
			await EncounterManager.ResolveOverflow(node, self);
		}
		dodgeStepsLeft--;
		if (dodgeStepsLeft > 0 && dodger == stepping && GodotObject.IsInstanceValid(stepping)) {
			LightDodgeStep(stepping);
			return;
		}
		EmitSignal(SignalName.DodgeStepEnded);
	}

	// ----- Movement -----

	private void ShowMoveOptions() {
		ClearHighlights();
		if (active == null || isTargeting || underway != null || !active.CanStep()) return;

		foreach (GameNode node in AdjacentNodes(active)) {
			node.Highlight(moveColour, true);
			highlighted.Add(node);
		}
	}

	// Async because arriving on a full node opens the push prompt and waits on it.
	public async void OnNodeClicked(GameNode node) {
		if (active == null) return;

		if (shiftNodes.Contains(node)) {
			ShiftStep(node);
			return;
		}
		if (isTargeting) {
			if (ArmedTerms.aoe) ResolveAgainstNode(node);
			else PickOnlyTargetOn(node);
			return;
		}
		if (underway != null || !highlighted.Contains(node)) return;

		Node2D self = (Node2D)active.GetParent();
		if (!active.TakeStep()) return;
		PathNode from = EncounterManager.pathGrid.NodeFromObj(self);
		if (!EncounterManager.MovePlayer(self, node, (Control)self.GetParent())) return;
		active.lastStep = Pushing.Step(from, EncounterManager.pathGrid.NodeFromObj(self));

		ClearHighlights();
		EncounterManager.ApplyNodeHazard(node, self);
		await EncounterManager.ResolveOverflow(node, self);

		if (active != null) Refresh();
	}

	private static List<GameNode> AdjacentNodes(PlayerToken token) {
		List<GameNode> nodes = new List<GameNode>();
		PathGrid grid = EncounterManager.pathGrid;
		PathNode here = grid.NodeFromObj((Node2D)token.GetParent());

		foreach (PathNode neighbour in grid.GetNeighbours(here)) {
			if (!neighbour.walkable) continue;
			nodes.Add(EnemyMovement.GameNodeAt(neighbour));
		}
		return nodes;
	}

	// ----- Attacking -----

	private void OnAttackChosen(Weapon weapon, PlayerMove move) {
		if (backstabChoice != null) {
			backstabChoice.TrySetResult((weapon, move));
			return;
		}
		// Once paid for, the armed option's own row is another way to say Done.
		if (underway != null) {
			if (move == underway.move) Done();
			return;
		}
		// Clicking the armed option again cancels it.
		if (armedMove == move) {
			FinishPick(null);
			Disarm();
			Refresh();
			return;
		}

		FinishPick(null);
		armedWeapon = weapon;
		armedMove = move;
		shiftLeft = move.ShiftBefore;
		// Rebuilt so the row shows armed, then the targets lit.
		Refresh();
		if (move.IsSupport) CastSupport(weapon, move);
	}

	private void Disarm() {
		armedWeapon = null;
		armedMove = null;
		if (underway == null) shiftLeft = 0;
	}

	private void DropAttack() {
		underway = null;
		shiftingAfter = false;
		Disarm();
	}

	// Range, cost and AOE come from the terms, so an armed Heroic boost counts here too. Once
	// paid for, the attack keeps the terms it was paid on.
	private AttackTerms ArmedTerms => underway != null ? underway.terms : AttackTerms.For(active, armedWeapon, armedMove);

	private bool InRange(Enemy enemy) => ArmedTerms.Reaches(active, enemy) && MeetsRepeatConstraint(enemy);

	// "One Enemy" repeats must strike the enemy the first use hit; "One Node" ones, an enemy
	// on the node it hit.
	private bool MeetsRepeatConstraint(Enemy enemy) {
		if (underway?.lockedEnemy != null) return enemy == underway.lockedEnemy;
		if (underway?.lockedNode != null) return enemy.GetParent()?.GetParent() == underway.lockedNode;
		return true;
	}

	public bool CanTarget(Enemy enemy) =>
		isTargeting && active != null && enemy != null && !armedMove.IsMovementOnly && !armedMove.IsSupport
		&& InRange(enemy) && DirectDamageAllows(armedMove, enemy);

	// Rapport: "choose an enemy in a node with another enemy".
	private static bool DirectDamageAllows(PlayerMove move, Enemy enemy) {
		EquipmentEffect direct = move.Effect(EquipmentEffect.EffectType.DIRECT_DAMAGE);
		if (direct == null || direct.condition != EquipmentEffect.Condition.IF_MULTIPLE_ENEMIES_ON_NODE) return true;
		return enemy.GetParent()?.GetParent() is GameNode node && EnemiesOn(node).Count >= 2;
	}

	// Nodes light up for every armed attack; a single-target attack also rings each enemy
	// it can reach, since that is what gets clicked. The Node icon picks a node, not a model.
	// Shift steps light the neighbours too: under a Node attack's targets, since clicking that
	// node is the attack, and over a single-target attack's, whose enemies are clicked instead.
	private void ShowTargets() {
		ClearHighlights();
		if (armedMove.IsSupport) return;     // it picks characters (CastSupport), not enemies

		bool aoe = ArmedTerms.aoe;
		if (!aoe) ShowTargetsOnly(false);
		ShowShiftOptions();
		if (aoe) ShowTargetsOnly(true);
	}

	private void ShowTargetsOnly(bool aoe) {
		if (armedMove.IsMovementOnly) return;
		foreach (Enemy enemy in EnemiesInRange()) {
			if (!aoe) targetRings.Add(TokenHighlight.Attach(enemy, targetRingColour));
			enemy.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
			targetable.Add(enemy);

			if (enemy.GetParent().GetParent() is not GameNode node || targetNodes.Contains(node)) continue;
			node.Highlight(targetColour, true);
			targetNodes.Add(node);
			if (aoe) shiftNodes.Remove(node);
			if (!highlighted.Contains(node)) highlighted.Add(node);
		}
	}

	private void ShowShiftOptions() {
		if (shiftLeft <= 0 || active == null) return;
		foreach (GameNode node in AdjacentNodes(active)) {
			node.Highlight(moveColour, true);
			shiftNodes.Add(node);
			if (!highlighted.Contains(node)) highlighted.Add(node);
		}
	}

	// A Shift step: free, and not the Walk or a Run. The first one commits the attack.
	private async void ShiftStep(GameNode node) {
		if (active == null || shiftLeft <= 0 || (underway == null && !Commit())) return;

		Node2D self = (Node2D)active.GetParent();
		PathNode from = EncounterManager.pathGrid.NodeFromObj(self);
		ClearHighlights();
		if (EncounterManager.MovePlayer(self, node, (Control)self.GetParent())) {
			active.lastStep = Pushing.Step(from, EncounterManager.pathGrid.NodeFromObj(self));
			EncounterManager.ApplyNodeHazard(node, self);
			await EncounterManager.ResolveOverflow(node, self);
		}
		if (active == null || underway == null) return;

		shiftLeft--;
		if (shiftLeft > 0) Refresh();
		else if (shiftingAfter) NextUse();
		else if (underway.move.IsMovementOnly) EndAttack();
		else Refresh();
	}

	private List<Enemy> EnemiesInRange(GameNode onNode = null) {
		List<Enemy> enemies = new List<Enemy>();
		foreach (Node2D enemyObj in EncounterManager.enemies) {
			Enemy enemy = EncounterManager.GetEnemy(enemyObj);
			if (enemy == null || !InRange(enemy) || (armedMove != null && !DirectDamageAllows(armedMove, enemy))) continue;
			if (onNode != null && enemyObj.GetParent() != onNode) continue;
			enemies.Add(enemy);
		}
		return enemies;
	}

	// Clicking a lit node rather than a model is unambiguous when only one enemy there can
	// be hit; with more than one, the player has to pick the model.
	private void PickOnlyTargetOn(GameNode node) {
		if (!targetNodes.Contains(node)) return;
		List<Enemy> candidates = EnemiesInRange(node);
		if (candidates.Count == 1) PickTarget(candidates[0]);
	}

	// Async because the roll is shown before it lands: the attack is disarmed and the
	// highlights cleared first, so nothing on the board answers a click during the reveal.
	public async void PickTarget(Enemy enemy) {
		if (!CanTarget(enemy)) return;

		if (ArmedTerms.aoe) {
			if (enemy.GetParent()?.GetParent() is GameNode node) ResolveAgainstNode(node);
			return;
		}

		if (!Commit()) return;
		(Weapon weapon, PlayerMove move) = TakeArmed();
		if (move.repeatConstraint == PlayerMove.RepeatTarget.ONE_ENEMY) underway.lockedEnemy ??= enemy;
		if (move.repeatConstraint == PlayerMove.RepeatTarget.ONE_NODE) underway.lockedNode ??= enemy.GetParent()?.GetParent() as GameNode;
		RecordUse();
		PlayerToken attacker = active;
		if (move.IsDirectDamage) {
			await CombatPresenter.DirectDamage(attacker, weapon, enemy, underway.terms, move.Effect(EquipmentEffect.EffectType.DIRECT_DAMAGE).magnitude);
		} else {
			attacker.attackRolls++;
			CombatResolver.AttackOutcome outcome = await CombatPresenter.CharacterAttacks(attacker, move, weapon, enemy, underway.terms);
			AfterRoll(attacker, underway.terms, new[] { outcome });
			await PushHit(attacker, move, new[] { enemy });
		}
		await Riders(attacker, weapon, move, interactive: true);
		AfterUse();
	}

	// The Node icon rolls once and compares that total against every enemy there (p23).
	private async void ResolveAgainstNode(GameNode node) {
		if (!targetNodes.Contains(node)) return;

		List<Enemy> targets = EnemiesOn(node);
		if (targets.Count == 0 || !Commit()) return;

		(Weapon weapon, PlayerMove move) = TakeArmed();
		if (move.repeatConstraint != PlayerMove.RepeatTarget.FREE) underway.lockedNode ??= node;
		RecordUse();
		PlayerToken attacker = active;
		attacker.attackRolls++;
		List<CombatResolver.AttackOutcome> outcomes = await CombatPresenter.CharacterAttacksNode(attacker, move, weapon, node, targets, underway.terms);
		AfterRoll(attacker, underway.terms, outcomes);
		await PushHit(attacker, move, targets);
		await Riders(attacker, weapon, move, interactive: true);
		AfterUse();
	}

	// An attack with the Push icon shoves every enemy it hit and did not kill, straight on
	// away from the attacker (p21, V2). Enemies cannot dodge, so every target was hit.
	// "Push x2" (Force) pushes twice, each straight on again.
	private static async Task PushHit(PlayerToken attacker, PlayerMove move, IEnumerable<Enemy> hit) {
		if (!move.isPush || !GodotObject.IsInstanceValid(attacker)) return;
		Node2D from = (Node2D)attacker.GetParent();
		foreach (Enemy enemy in hit) {
			for (int n = 0; n < move.PushNodes; n++) {
				if (!GodotObject.IsInstanceValid(enemy) || enemy.GetParent() is not Node2D model) break;
				if (EncounterManager.GetEnemy(model) == null) break;
				if (!await Pushing.Shove(model, Pushing.Away(from, model, attacker.lastStep))) break;
			}
		}
	}

	private static List<Enemy> EnemiesOn(GameNode node) {
		List<Enemy> targets = new List<Enemy>();
		foreach (Node2D occupant in EncounterManager.GetPlayersInNode(node, "Enemy")) {
			Enemy enemy = EncounterManager.GetEnemy(occupant);
			if (enemy != null) targets.Add(enemy);
		}
		return targets;
	}

	private bool PayFor(AttackTerms terms) =>
		terms.cost == 0 || (active.CanSpend(terms.cost) && active.SpendStamina(terms.cost));

	private (Weapon, PlayerMove) TakeArmed() {
		(Weapon weapon, PlayerMove move) = (armedWeapon, armedMove);
		Disarm();
		ClearHighlights();
		return (weapon, move);
	}

	// Pays for the armed attack, once for all its uses (Matt's call: the Repeat icon repeats
	// the option, not its cost).
	private bool Commit() {
		if (underway != null) return true;
		if (armedMove == null) return false;
		AttackTerms terms = ArmedTerms;
		if (!PayFor(terms)) return false;
		underway = new Underway { weapon = armedWeapon, move = armedMove, terms = terms, usesLeft = armedMove.Uses };
		return true;
	}

	// What a roll pays back once it has landed.
	// Knight Slayer's Ring: beating an enemy's Block or Resist by 3 or more gives 1 stamina,
	// once for the attack however many enemies on a node it beat.
	// Hollow Gem: an attack that dealt no damage to anything it hit resets the Luck token.
	private static void AfterRoll(PlayerToken attacker, AttackTerms terms, IEnumerable<CombatResolver.AttackOutcome> outcomes) {
		if (!GodotObject.IsInstanceValid(attacker)) return;
		bool beatBy3 = false;
		bool anyDamage = false;
		foreach (CombatResolver.AttackOutcome outcome in outcomes) {
			if (outcome.roll - outcome.mitigation >= 3) beatBy3 = true;
			if (outcome.damage > 0) anyDamage = true;
		}
		if (beatBy3 && attacker.player.HasRing(Ring.Effect.KNIGHT_SLAYER)) attacker.RecoverStamina(1);
		if (terms.hollow && !anyDamage) attacker.player.luckUsed = false;
	}

	// The weapon's one attack (p22) and any Heroic boost are spent by the first use, and the
	// Dusk Crown Ring's damage is suffered once for it.
	private void RecordUse() {
		if (underway == null || underway.recorded || active == null) return;
		underway.recorded = true;
		active.SufferOwnDamage(underway.terms.selfDamage);
		active.RecordAttack(underway.weapon, !underway.terms.reusesWeapon);
		if (underway.terms.boosted) active.SpendHeroicBoost();
	}

	private void AfterUse() {
		// The activation may have been torn down while the roll was on screen.
		if (active == null || !GodotObject.IsInstanceValid(active) || underway == null) return;

		underway.usesLeft--;
		EncounterManager.PruneDeadEnemies();
		// A win ends the activation and tears this turn down.
		EmitSignal(SignalName.AttackResolved);
		if (active == null || underway == null) return;

		if (underway.move.ShiftAfter > 0) {
			shiftingAfter = true;
			shiftLeft = underway.move.ShiftAfter;
			Refresh();
			return;
		}
		NextUse();
	}

	// The next Repeat, armed again without paying; or the end of the attack when there are no
	// uses left or nothing it could still do.
	private void NextUse() {
		shiftingAfter = false;
		shiftLeft = 0;
		if (underway == null || active == null) return;
		if (underway.usesLeft <= 0) {
			EndAttack();
			return;
		}
		armedWeapon = underway.weapon;
		armedMove = underway.move;
		shiftLeft = armedMove.ShiftBefore;
		if (shiftLeft == 0 && EnemiesInRange().Count == 0) {
			EndAttack();
			return;
		}
		Refresh();
	}

	// Done: ends the Shift after the dice, or else the rest of the attack.
	private void Done() {
		if (underway == null) return;
		if (shiftingAfter) NextUse();
		else EndAttack();
	}

	private void EndAttack() {
		RecordUse();
		DropAttack();
		if (active != null && GodotObject.IsInstanceValid(active)) Refresh();
	}

	// ----- Support options and riders -----

	// A heal, refresh, shield or empowerment: the characters it reaches are ringed and picked
	// on the board, then it is paid for and lands. It is the weapon's attack for the activation
	// and locks movement like one (Matt). Cancelled by its row or the End button.
	private async void CastSupport(Weapon weapon, PlayerMove move) {
		if (active == null) return;
		PlayerToken caster = active;
		AttackTerms terms = ArmedTerms;
		EquipmentEffect lead = null;
		foreach (EquipmentEffect e in move.bonusEffects) if (e != null && !OnCaster(e)) { lead = e; break; }

		List<PlayerToken> candidates;
		int max = int.MaxValue;
		bool all = true, byNode = false;
		if (lead == null) {
			candidates = new List<PlayerToken> { caster };
		} else if (lead.type == EquipmentEffect.EffectType.DEFENSE_DICE && lead.duration == EquipmentEffect.Duration.UNTIL_END_OF_ENEMY_ACTIVATION) {
			// Sunlight Straight Sword's "party bonus": everyone, wherever they are (Matt).
			candidates = PartyTokens();
		} else {
			candidates = CharactersWithin(caster, terms.range, lead.scope == EquipmentEffect.TargetScope.ALL_OTHER_CHARACTERS ? caster : null);
			switch (lead.scope) {
				case EquipmentEffect.TargetScope.ONE_CHARACTER: max = 1; all = false; break;
				case EquipmentEffect.TargetScope.TWO_CHARACTERS: max = 2; all = false; break;
				case EquipmentEffect.TargetScope.ONE_NODE: byNode = true; all = false; break;
			}
		}
		if (candidates.Count == 0) return;

		List<PlayerToken> chosen = await PickCharacters(candidates, max, all, byNode, "Cancel");
		if (armedMove != move || active != caster) return;
		if (chosen == null || chosen.Count == 0 || !Commit()) {
			Disarm();
			Refresh();
			return;
		}
		TakeArmed();
		RecordUse();
		ApplyToCharacters(caster, move, chosen, casterOnly: false);
		await Riders(caster, weapon, move, interactive: true, supportDone: true);
		AfterUse();
	}

	// Characters within range of the caster (the caster too, unless excluded): "within range"
	// as the card says, with the option's or weapon's range (p12, p23).
	private static List<PlayerToken> CharactersWithin(PlayerToken caster, int range, PlayerToken except) {
		List<PlayerToken> within = new List<PlayerToken>();
		foreach (PlayerToken member in PartyTokens()) {
			if (member == except) continue;
			if (EnemyMovement.Distance((Node2D)caster.GetParent(), (Node2D)member.GetParent()) <= range) within.Add(member);
		}
		return within;
	}

	private static bool OnCaster(EquipmentEffect e) =>
		e.scope is EquipmentEffect.TargetScope.TARGET or EquipmentEffect.TargetScope.SELF;

	// What an option does to characters. With casterOnly, just the effects that are the
	// attacker's own (Smough's Hammer's health, Dragon Tooth's stamina, a sword's lost health).
	private static void ApplyToCharacters(PlayerToken caster, PlayerMove move, List<PlayerToken> targets, bool casterOnly) {
		if (move.bonusEffects == null || !GodotObject.IsInstanceValid(caster)) return;
		foreach (EquipmentEffect e in move.bonusEffects) {
			if (e == null || (casterOnly && !OnCaster(e)) || e.scope == EquipmentEffect.TargetScope.ONE_CHARACTER_IN_RANGE) continue;
			List<PlayerToken> who = OnCaster(e) ? new List<PlayerToken> { caster } : targets;
			switch (e.type) {
				case EquipmentEffect.EffectType.HEAL:
					foreach (PlayerToken t in who) caster.GearHeal(t, e.magnitude);
					break;
				case EquipmentEffect.EffectType.GAIN_STAMINA:
					foreach (PlayerToken t in who) t.RecoverStamina(e.magnitude);
					break;
				case EquipmentEffect.EffectType.DEFENSE_DICE:
					Dice die = e.diceColour switch {
						DiceUtility.DICE_TYPE.BLUE => Heroic.BlueDie,
						DiceUtility.DICE_TYPE.ORANGE => Heroic.OrangeDie,
						_ => Heroic.BlackDie,
					};
					foreach (PlayerToken t in who) {
						for (int i = 0; i < Mathf.Max(1, e.magnitude); i++) t.player.defenceBuffs.Add((die, e.defenseKind));
					}
					break;
				case EquipmentEffect.EffectType.GRANT_MAGIC:
					caster.magicThisActivation = true;
					break;
				case EquipmentEffect.EffectType.BONUS_DAMAGE when e.duration == EquipmentEffect.Duration.UNTIL_END_OF_ACTIVATION:
					caster.bonusThisActivation += e.magnitude;
					break;
				case EquipmentEffect.EffectType.LOSE_HEALTH:
					caster.LoseHealth(e.magnitude);
					break;
			}
		}
	}

	// After an option lands: its own effects on the attacker, then the ones that choose a
	// character (Lothric's Holy Sword's stamina, the Mace's Aggro), then the weapon's
	// "if attack" passive (Vordt's Great Hammer). Outside the character's activation
	// (Backstab) nothing is asked: the stamina goes to the attacker and the Aggro stays.
	private async Task Riders(PlayerToken attacker, Weapon weapon, PlayerMove move, bool interactive, bool supportDone = false) {
		if (!GodotObject.IsInstanceValid(attacker)) return;
		if (!supportDone) ApplyToCharacters(attacker, move, null, casterOnly: true);

		if (move.bonusEffects != null) {
			foreach (EquipmentEffect e in move.bonusEffects) {
				if (e?.scope != EquipmentEffect.TargetScope.ONE_CHARACTER_IN_RANGE) continue;
				List<PlayerToken> near = CharactersWithin(attacker, e.scopeRange, null);
				PlayerToken who = attacker;
				if (interactive && near.Count > 1) {
					List<PlayerToken> chosen = await PickCharacters(near, 1, false, false, "Skip");
					who = chosen?.Count > 0 ? chosen[0] : null;
				}
				if (who == null || !GodotObject.IsInstanceValid(who)) continue;
				if (e.type == EquipmentEffect.EffectType.GAIN_STAMINA) who.RecoverStamina(e.magnitude);
				else if (e.type == EquipmentEffect.EffectType.HEAL) attacker.GearHeal(who, e.magnitude);
			}
		}

		// The Mace: "you may move the Aggro token to another character after this attack".
		if (interactive && move.HasEffect(EquipmentEffect.EffectType.MAY_MOVE_AGGRO)) {
			List<PlayerToken> others = PartyTokens();
			others.Remove(attacker);
			if (others.Count > 0) {
				List<PlayerToken> chosen = await PickCharacters(others, 1, false, false, "Skip");
				if (chosen?.Count > 0) EncounterManager.SetAggroHolder(chosen[0]);
			}
		}

		if (weapon?.passives != null && GodotObject.IsInstanceValid(attacker)) {
			foreach (EquipmentEffect e in weapon.passives) {
				if (e != null && e.type == EquipmentEffect.EffectType.APPLY_STATUS && e.condition == EquipmentEffect.Condition.ON_ATTACK) {
					attacker.ApplyCondition(e.inflictedStatus);
				}
			}
		}
	}

	// ----- Picking characters -----

	private Task<List<PlayerToken>> PickCharacters(List<PlayerToken> candidates, int max, bool all, bool byNode, string cancelLabel) {
		FinishPick(null);
		pick = new TaskCompletionSource<List<PlayerToken>>();
		pickCandidates = new List<PlayerToken>(candidates);
		picked.Clear();
		pickMax = max;
		pickAll = all;
		pickByNode = byNode;
		pickCancelLabel = cancelLabel;
		ShowPick();
		if (active != null) actionBar?.Refresh(active);
		return pick.Task;
	}

	private void ShowPick() {
		ClearPickMarks();
		foreach (PlayerToken t in pickCandidates) {
			if (!GodotObject.IsInstanceValid(t)) continue;
			pickRings.Add(TokenHighlight.Attach(t, picked.Contains(t) ? pickedRingColour : pickRingColour));
			if (pickByNode && t.GetParent()?.GetParent() is GameNode node && !pickNodes.Contains(node)) {
				node.Highlight(pickRingColour, true);
				pickNodes.Add(node);
			}
		}
	}

	private void ClearPickMarks() {
		foreach (TokenHighlight ring in pickRings) TokenHighlight.Detach(ring);
		pickRings.Clear();
		foreach (GameNode node in pickNodes) if (GodotObject.IsInstanceValid(node)) node.ClearHighlight();
		pickNodes.Clear();
	}

	public void OnCharacterPicked(PlayerToken token) {
		if (pick == null || !pickCandidates.Contains(token)) return;
		if (pickAll) {
			FinishPick(new List<PlayerToken>(pickCandidates));
			return;
		}
		if (pickByNode) {
			OnPickNode(token.GetParent()?.GetParent() as GameNode);
			return;
		}
		if (!picked.Remove(token)) picked.Add(token);
		if (picked.Count >= pickMax) {
			FinishPick(new List<PlayerToken>(picked));
			return;
		}
		ShowPick();
		if (active != null) actionBar?.Refresh(active);
	}

	// A node click while picking: a node's worth for a node option, otherwise the one
	// candidate standing there.
	public void OnPickNode(GameNode node) {
		if (pick == null || node == null) return;
		List<PlayerToken> there = pickCandidates.FindAll(t => GodotObject.IsInstanceValid(t) && t.GetParent()?.GetParent() == node);
		if (there.Count == 0) return;
		if (pickByNode) FinishPick(there);
		else if (there.Count == 1) OnCharacterPicked(there[0]);
	}

	private void FinishPick(List<PlayerToken> result) {
		if (pick == null) return;
		TaskCompletionSource<List<PlayerToken>> done = pick;
		pick = null;
		picked.Clear();
		ClearPickMarks();
		done.TrySetResult(result);
		if (active != null && GodotObject.IsInstanceValid(active)) actionBar?.Refresh(active);
	}

	// Crimson Robes: "remove 1 condition" after the Heroic Action. One goes without asking;
	// with more, the wearer picks which.
	private async Task CrimsonRobes(PlayerToken user) {
		if (!GodotObject.IsInstanceValid(user)
			|| !user.player.HasPassive(EquipmentEffect.EffectType.REMOVE_STATUS, EquipmentEffect.Condition.IF_HEROIC_ABILITY_ACTIVATED)) return;
		List<EncounterManager.StatusEffect> held = user.Conditions();
		if (held.Count == 0) return;
		if (held.Count == 1 || actionBar == null) {
			user.RemoveCondition(held[0]);
			return;
		}
		List<(Control, string)> options = new List<(Control, string)>();
		foreach (EncounterManager.StatusEffect c in held) {
			TextureRect icon = new TextureRect {
				Texture = ConditionArt.Icon(c), CustomMinimumSize = new Vector2(20, 20),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			options.Add((icon, $"Remove {c.ToString().ToLower()}"));
		}
		int choice = await actionBar.AskChoice(user.player, null, options, "Skip");
		if (choice >= 0 && GodotObject.IsInstanceValid(user)) user.RemoveCondition(held[choice]);
	}

	// A swap changes what the hands hold, so an attack armed from the old weapon goes.
	private void OnEquipmentSwapped() {
		if (active == null) return;
		if (underway == null) Disarm();
		Refresh();
	}

	// ----- Heroic Actions -----

	// The Heroic token on the action bar, during the character's own activation. The effects
	// land at once; the four that boost an attack are armed for the next one it applies to.
	private async void UseHeroic() {
		if (active == null || !active.CanUseHeroicNow) return;
		PlayerToken user = active;
		Heroic.Kind kind = user.heroic;
		user.player.heroicUsed = true;
		user.OnHeroicUsed();
		await CrimsonRobes(user);
		if (underway == null) Disarm();

		switch (kind) {
			case Heroic.Kind.PERSEVERANCE:
				foreach (PlayerToken member in PartyTokens()) member.RecoverStamina(2);
				break;
			case Heroic.Kind.KEEP_THE_FAITH:
				Node2D here = (Node2D)user.GetParent();
				foreach (PlayerToken member in PartyTokens()) {
					if (EnemyMovement.Distance(here, (Node2D)member.GetParent()) <= 1) member.Heal(2);
				}
				break;
			case Heroic.Kind.LUCKY_BREAK:
				user.RecoverStamina(2);
				user.Heal(2);
				user.player.luckUsed = false;
				break;
			case Heroic.Kind.COMBAT_VERSATILITY:
				await ChangeEquipment(user.player);
				break;
			default:
				if (Heroic.IsBoost(kind)) user.ArmHeroic(kind);
				break;
		}
		if (active == user) Refresh();
	}

	// Combat Versatility: the equipment screen, as at a bonfire, for this character only.
	private async Task ChangeEquipment(Player player) {
		EquipmentModal modal = GetNodeOrNull<EquipmentModal>("/root/EquipmentModal");
		if (modal == null) return;
		modal.Open(player, lockToPlayer: true);
		await ToSignal(modal, EquipmentModal.SignalName.Closed);
	}

	private static List<PlayerToken> PartyTokens() {
		List<PlayerToken> tokens = new List<PlayerToken>();
		foreach (Node2D model in EncounterManager.players) {
			PlayerToken token = EncounterManager.GetPlayerToken(model);
			if (token != null) tokens.Add(token);
		}
		return tokens;
	}

	// The Assassin's Backstab, after a successful dodge (and its free step): pick any attack
	// that reaches the enemy dodged and make it at no stamina, or pass. Runs inside that
	// enemy's activation, which waits on it; the enemy may die of it (Enemy.isDead).
	public async Task OfferBackstab(PlayerToken assassin, Enemy enemy) {
		if (actionBar == null || !GodotObject.IsInstanceValid(assassin) || !GodotObject.IsInstanceValid(enemy) || enemy.isDead) return;
		if (assassin.heroic != Heroic.Kind.BACKSTAB || assassin.player.heroicUsed) return;

		System.Func<Weapon, PlayerMove, bool> reaches = (weapon, move) =>
			GodotObject.IsInstanceValid(enemy) && AttackTerms.For(assassin, weapon, move, free: true).Reaches(assassin, enemy);
		if (!AnyAttack(assassin.player, reaches)) return;

		backstabChoice = new TaskCompletionSource<(Weapon, PlayerMove)>();
		actionBar.ShowBackstab(assassin, reaches);
		(Weapon chosenWeapon, PlayerMove chosenMove) = await backstabChoice.Task;
		backstabChoice = null;
		actionBar.ShowIdle();

		if (chosenMove == null || !GodotObject.IsInstanceValid(assassin) || !GodotObject.IsInstanceValid(enemy) || enemy.isDead) return;
		assassin.player.heroicUsed = true;
		assassin.OnHeroicUsed();
		await CrimsonRobes(assassin);

		AttackTerms terms = AttackTerms.For(assassin, chosenWeapon, chosenMove, free: true);
		assassin.SufferOwnDamage(terms.selfDamage);
		if (terms.aoe && enemy.GetParent()?.GetParent() is GameNode node) {
			List<Enemy> targets = EnemiesOn(node);
			List<CombatResolver.AttackOutcome> outcomes = await CombatPresenter.CharacterAttacksNode(assassin, chosenMove, chosenWeapon, node, targets, terms);
			AfterRoll(assassin, terms, outcomes);
			await PushHit(assassin, chosenMove, targets);
		} else {
			CombatResolver.AttackOutcome outcome = await CombatPresenter.CharacterAttacks(assassin, chosenMove, chosenWeapon, enemy, terms);
			AfterRoll(assassin, terms, new[] { outcome });
			await PushHit(assassin, chosenMove, new[] { enemy });
		}
		await Riders(assassin, chosenWeapon, chosenMove, interactive: false);
	}

	private static bool AnyAttack(Player player, System.Func<Weapon, PlayerMove, bool> reaches) {
		foreach (Weapon weapon in new[] { player.GetLeftHand(), player.GetRightHand() }) {
			if (weapon?.attacks == null) continue;
			foreach (PlayerMove move in weapon.attacks) {
				if (move != null && reaches(weapon, move)) return true;
			}
		}
		return false;
	}

	// ----- Shared -----

	private void Refresh() {
		if (actionBar != null) actionBar.Refresh(active);
		if (shiftingAfter) {
			ClearHighlights();
			ShowShiftOptions();
		} else if (isTargeting) ShowTargets();
		else ShowMoveOptions();
	}

	private void ClearHighlights() {
		foreach (GameNode node in highlighted) {
			if (GodotObject.IsInstanceValid(node)) node.ClearHighlight();
		}
		highlighted.Clear();
		targetNodes.Clear();
		shiftNodes.Clear();

		foreach (TokenHighlight ring in targetRings) TokenHighlight.Detach(ring);
		targetRings.Clear();
		foreach (Enemy enemy in targetable) {
			if (GodotObject.IsInstanceValid(enemy)) enemy.MouseDefaultCursorShape = Control.CursorShape.Arrow;
		}
		targetable.Clear();
	}
}
