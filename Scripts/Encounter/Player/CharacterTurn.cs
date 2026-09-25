using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

// Owns everything the player does during one character activation: stepping node to node,
// arming a weapon attack, and picking what it hits. ActionListener still owns the turn
// order and simply hands the activation over.
//
// Movement is deliberately one node per click (p22): the first step of an activation is the
// free Walk and each one after is a Run, so the cost only makes sense a step at a time.
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
	public PlayerMove armed => armedMove;

	private Weapon armedWeapon;
	private PlayerMove armedMove;
	private readonly List<GameNode> highlighted = new List<GameNode>();
	private readonly List<TokenHighlight> targetRings = new List<TokenHighlight>();

	// The character taking the free step a successful dodge grants (p22), while it lasts.
	private PlayerToken dodger;
	public bool isDodgeStepping => dodger != null;

	public override void _Ready() {
		EncounterManager.characterTurn = this;

		if (actionBar != null) {
			actionBar.AttackChosen += OnAttackChosen;
			actionBar.EndActivationPressed += () => EmitSignal(SignalName.ActivationEnded);
			actionBar.DodgeStepEnded += () => EmitSignal(SignalName.DodgeStepEnded);
			actionBar.ShowIdle();
		}
	}

	public void Begin(PlayerToken token) {
		active = token;
		Disarm();
		if (actionBar != null) actionBar.Refresh(token);
		ShowMoveOptions();
	}

	public void End() {
		Disarm();
		ClearHighlights();
		active = null;
		if (actionBar != null) actionBar.ShowIdle();
	}

	// ----- The dodge step (p22) -----

	// A character who dodged may move one node. Runs inside the enemy's activation, which
	// waits on it: the adjacent nodes light, the bar offers End Dodge, and either a node
	// click or that button finishes it. The stamina was paid with the dodge itself.
	public async Task OfferDodgeStep(PlayerToken token) {
		if (token == null || !GodotObject.IsInstanceValid(token)) return;

		dodger = token;
		ClearHighlights();
		foreach (GameNode node in AdjacentNodes(token)) {
			node.Highlight(moveColour);
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
		ClearHighlights();
		if (EncounterManager.MovePlayer(self, node, (Control)self.GetParent())) {
			EncounterManager.ApplyNodeHazard(node, self);
			await EncounterManager.ResolveOverflow(node, self);
		}
		EmitSignal(SignalName.DodgeStepEnded);
	}

	// ----- Movement -----

	private void ShowMoveOptions() {
		ClearHighlights();
		if (active == null || isTargeting || !active.CanStep()) return;

		foreach (GameNode node in AdjacentNodes(active)) {
			node.Highlight(moveColour);
			highlighted.Add(node);
		}
	}

	// Async because arriving on a full node opens the push prompt and waits on it.
	public async void OnNodeClicked(GameNode node) {
		if (active == null) return;

		if (isTargeting) {
			if (armedMove.isAOE) ResolveAgainstNode(node);
			else PickOnlyTargetOn(node);
			return;
		}
		if (!highlighted.Contains(node)) return;

		Node2D self = (Node2D)active.GetParent();
		if (!active.TakeStep()) return;
		if (!EncounterManager.MovePlayer(self, node, (Control)self.GetParent())) return;

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
		// Clicking the armed option again cancels it.
		if (armedMove == move) {
			Disarm();
			Refresh();
			return;
		}

		armedWeapon = weapon;
		armedMove = move;
		ShowTargets();
	}

	private void Disarm() {
		armedWeapon = null;
		armedMove = null;
	}

	// An option-specific range replaces the weapon's standard range for that attack (p23).
	private int RangeOf(Weapon weapon, PlayerMove move) =>
		move.attackRange > 0 ? move.attackRange : (weapon?.attackRange ?? 0);

	private bool InRange(Enemy enemy) {
		int distance = EnemyMovement.Distance((Node2D)active.GetParent(), (Node2D)enemy.GetParent());
		if (distance > RangeOf(armedWeapon, armedMove)) return false;

		// Shaft: bows and polearms cannot be used against a target at Range 0 (p23).
		return !(armedMove.isNotZeroRange && distance == 0);
	}

	// Nodes light up for every armed attack; a single-target attack also rings each enemy
	// it can reach, since that is what gets clicked. The Node icon picks a node, not a model.
	private void ShowTargets() {
		ClearHighlights();

		foreach (Enemy enemy in EnemiesInRange()) {
			if (!armedMove.isAOE) targetRings.Add(TokenHighlight.Attach(enemy, targetRingColour));

			if (enemy.GetParent().GetParent() is not GameNode node || highlighted.Contains(node)) continue;
			node.Highlight(targetColour);
			highlighted.Add(node);
		}
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
		if (!highlighted.Contains(node)) return;
		List<Enemy> candidates = EnemiesInRange(node);
		if (candidates.Count == 1) PickTarget(candidates[0]);
	}

	// Async because the roll is shown before it lands: the attack is disarmed and the
	// highlights cleared first, so nothing on the board answers a click during the reveal.
	public async void PickTarget(Enemy enemy) {
		if (!isTargeting || active == null || enemy == null || !InRange(enemy)) return;

		if (armedMove.isAOE) {
			if (enemy.GetParent()?.GetParent() is GameNode node) ResolveAgainstNode(node);
			return;
		}

		if (!PayFor(armedMove)) return;
		(Weapon weapon, PlayerMove move) = TakeArmed();
		await CombatPresenter.CharacterAttacks(move, weapon, enemy);
		FinishAttack(weapon);
	}

	// The Node icon rolls once and compares that total against every enemy there (p23).
	private async void ResolveAgainstNode(GameNode node) {
		if (!highlighted.Contains(node)) return;

		List<Enemy> targets = new List<Enemy>();
		foreach (Node2D occupant in EncounterManager.GetPlayersInNode(node, "Enemy")) {
			Enemy enemy = EncounterManager.GetEnemy(occupant);
			if (enemy != null) targets.Add(enemy);
		}
		if (targets.Count == 0) return;

		if (!PayFor(armedMove)) return;
		(Weapon weapon, PlayerMove move) = TakeArmed();
		await CombatPresenter.CharacterAttacksNode(move, weapon, targets);
		FinishAttack(weapon);
	}

	private bool PayFor(PlayerMove move) {
		int cost = CombatResolver.AttackStaminaCost(active, move);
		return active.CanSpend(cost) && active.SpendStamina(cost);
	}

	private (Weapon, PlayerMove) TakeArmed() {
		(Weapon weapon, PlayerMove move) = (armedWeapon, armedMove);
		Disarm();
		ClearHighlights();
		return (weapon, move);
	}

	private void FinishAttack(Weapon weapon) {
		// The activation may have been torn down while the roll was on screen.
		if (active == null || !GodotObject.IsInstanceValid(active)) return;

		active.RecordAttack(weapon);
		EncounterManager.PruneDeadEnemies();
		Refresh();

		// Last, because a win ends the activation and tears this turn down.
		EmitSignal(SignalName.AttackResolved);
	}

	// ----- Shared -----

	private void Refresh() {
		if (actionBar != null) actionBar.Refresh(active);
		if (isTargeting) ShowTargets(); else ShowMoveOptions();
	}

	private void ClearHighlights() {
		foreach (GameNode node in highlighted) {
			if (GodotObject.IsInstanceValid(node)) node.ClearHighlight();
		}
		highlighted.Clear();

		foreach (TokenHighlight ring in targetRings) TokenHighlight.Detach(ring);
		targetRings.Clear();
	}
}
