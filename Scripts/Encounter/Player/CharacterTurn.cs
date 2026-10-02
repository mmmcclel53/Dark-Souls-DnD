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

	// V2: the move after a successful dodge is a choice, and costs 1 stamina.
	private const int DODGE_STEP_COST = 1;

	// Backstab's choice of attack, while the Assassin is making it.
	private TaskCompletionSource<(Weapon, PlayerMove)> backstabChoice;

	// The character taking the free step a successful dodge grants (p22), while it lasts.
	private PlayerToken dodger;
	public bool isDodgeStepping => dodger != null;

	public override void _Ready() {
		EncounterManager.characterTurn = this;

		if (actionBar != null) {
			actionBar.AttackChosen += OnAttackChosen;
			// While an attack is under way the button reads Done and ends that stage of it.
			actionBar.EndActivationPressed += () => {
				if (underway != null) Done();
				else EmitSignal(SignalName.ActivationEnded);
			};
			actionBar.DodgeStepEnded += () => EmitSignal(SignalName.DodgeStepEnded);
			actionBar.HeroicChosen += UseHeroic;
			actionBar.BackstabPassed += () => backstabChoice?.TrySetResult((null, null));
			actionBar.ShowIdle();
		}
	}

	public void Begin(PlayerToken token) {
		active = token;
		DropAttack();
		if (actionBar != null) actionBar.Refresh(token);
		ShowMoveOptions();
	}

	public void End() {
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
		ClearHighlights();
		foreach (GameNode node in AdjacentNodes(token)) {
			node.Highlight(moveColour, true);
			highlighted.Add(node);
		}
		actionBar?.ShowDodgeStep(token);

		await ToSignal(this, SignalName.DodgeStepEnded);

		ClearHighlights();
		dodger = null;
		actionBar?.ShowIdle();
	}

	public async void OnDodgeStepNodeClicked(GameNode node) {
		if (dodger == null || !highlighted.Contains(node)) return;

		Node2D self = (Node2D)dodger.GetParent();
		if (!dodger.SpendStamina(DODGE_STEP_COST)) return;
		ClearHighlights();
		PathNode from = EncounterManager.pathGrid.NodeFromObj(self);
		if (EncounterManager.MovePlayer(self, node, (Control)self.GetParent())) {
			dodger.lastStep = Pushing.Step(from, EncounterManager.pathGrid.NodeFromObj(self));
			EncounterManager.ApplyNodeHazard(node, self);
			await EncounterManager.ResolveOverflow(node, self);
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
			Disarm();
			Refresh();
			return;
		}

		armedWeapon = weapon;
		armedMove = move;
		shiftLeft = move.ShiftBefore;
		// Rebuilt so the row shows armed, then the targets lit.
		Refresh();
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
		isTargeting && active != null && enemy != null && !armedMove.IsMovementOnly && InRange(enemy);

	// Nodes light up for every armed attack; a single-target attack also rings each enemy
	// it can reach, since that is what gets clicked. The Node icon picks a node, not a model.
	// Shift steps light the neighbours too: under a Node attack's targets, since clicking that
	// node is the attack, and over a single-target attack's, whose enemies are clicked instead.
	private void ShowTargets() {
		ClearHighlights();

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
			if (enemy == null || !InRange(enemy)) continue;
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
		await CombatPresenter.CharacterAttacks(active, move, weapon, enemy, underway.terms.extraDice);
		await PushHit(active, move, new[] { enemy });
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
		await CombatPresenter.CharacterAttacksNode(active, move, weapon, node, targets, underway.terms.extraDice);
		await PushHit(active, move, targets);
		AfterUse();
	}

	// An attack with the Push icon shoves every enemy it hit and did not kill, straight on
	// away from the attacker (p21, V2). Enemies cannot dodge, so every target was hit.
	private static async Task PushHit(PlayerToken attacker, PlayerMove move, IEnumerable<Enemy> hit) {
		if (!move.isPush || !GodotObject.IsInstanceValid(attacker)) return;
		Node2D from = (Node2D)attacker.GetParent();
		foreach (Enemy enemy in hit) {
			if (!GodotObject.IsInstanceValid(enemy) || enemy.GetParent() is not Node2D model) continue;
			if (EncounterManager.GetEnemy(model) == null) continue;
			await Pushing.Shove(model, Pushing.Away(from, model, attacker.lastStep));
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

	// The weapon's one attack (p22) and any Heroic boost are spent by the first use.
	private void RecordUse() {
		if (underway == null || underway.recorded || active == null) return;
		underway.recorded = true;
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

	// ----- Heroic Actions -----

	// The Heroic token on the action bar, during the character's own activation. The effects
	// land at once; the four that boost an attack are armed for the next one it applies to.
	private async void UseHeroic() {
		if (active == null || !active.CanUseHeroicNow) return;
		PlayerToken user = active;
		Heroic.Kind kind = user.heroic;
		user.player.heroicUsed = true;
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

		AttackTerms terms = AttackTerms.For(assassin, chosenWeapon, chosenMove, free: true);
		if (terms.aoe && enemy.GetParent()?.GetParent() is GameNode node) {
			List<Enemy> targets = EnemiesOn(node);
			await CombatPresenter.CharacterAttacksNode(assassin, chosenMove, chosenWeapon, node, targets, terms.extraDice);
			await PushHit(assassin, chosenMove, targets);
		} else {
			await CombatPresenter.CharacterAttacks(assassin, chosenMove, chosenWeapon, enemy, terms.extraDice);
			await PushHit(assassin, chosenMove, new[] { enemy });
		}
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
