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

	[Export] public CharacterActionBar actionBar;

	[Export] public Color moveColour = new Color(0.79f, 0.64f, 0.29f, 0.45f);
	[Export] public Color targetColour = new Color(0.64f, 0.17f, 0.13f, 0.55f);

	public PlayerToken active { get; private set; }
	public bool isTargeting => armedMove != null;

	private Weapon armedWeapon;
	private PlayerMove armedMove;
	private readonly List<GameNode> highlighted = new List<GameNode>();

	public override void _Ready() {
		EncounterManager.characterTurn = this;

		if (actionBar != null) {
			actionBar.AttackChosen += OnAttackChosen;
			actionBar.EndActivationPressed += () => EmitSignal(SignalName.ActivationEnded);
			actionBar.Visible = false;
		}
	}

	public void Begin(PlayerToken token) {
		active = token;
		Disarm();
		if (actionBar != null) {
			actionBar.Visible = true;
			actionBar.Refresh(token);
		}
		ShowMoveOptions();
	}

	public void End() {
		Disarm();
		ClearHighlights();
		active = null;
		if (actionBar != null) actionBar.Visible = false;
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

	private void ShowTargets() {
		ClearHighlights();

		foreach (Node2D enemyObj in EncounterManager.enemies) {
			Enemy enemy = EncounterManager.GetEnemy(enemyObj);
			if (enemy == null || !InRange(enemy)) continue;
			if (enemyObj.GetParent() is not GameNode node || highlighted.Contains(node)) continue;

			node.Highlight(targetColour);
			highlighted.Add(node);
		}
	}

	public void PickTarget(Enemy enemy) {
		if (!isTargeting || active == null || enemy == null || !InRange(enemy)) return;

		if (armedMove.isAOE) {
			if (enemy.GetParent() is GameNode node) ResolveAgainstNode(node);
			return;
		}

		if (!PayFor(armedMove)) return;
		CombatResolver.CharacterAttacks(armedMove, enemy);
		FinishAttack();
	}

	// The Node icon rolls once and compares that total against every enemy there (p23).
	private void ResolveAgainstNode(GameNode node) {
		if (!highlighted.Contains(node)) return;

		List<Enemy> targets = new List<Enemy>();
		foreach (Node2D occupant in EncounterManager.GetPlayersInNode(node, "Enemy")) {
			Enemy enemy = EncounterManager.GetEnemy(occupant);
			if (enemy != null) targets.Add(enemy);
		}
		if (targets.Count == 0) return;

		if (!PayFor(armedMove)) return;
		CombatResolver.CharacterAttacksNode(armedMove, targets);
		FinishAttack();
	}

	private bool PayFor(PlayerMove move) {
		int cost = CombatResolver.AttackStaminaCost(active, move);
		return active.CanSpend(cost) && active.SpendStamina(cost);
	}

	private void FinishAttack() {
		active.RecordAttack(armedWeapon);
		Disarm();
		EncounterManager.PruneDeadEnemies();
		Refresh();
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
	}
}
