using Godot;
using System.Collections.Generic;

// Turns a Move icon into a concrete list of nodes to walk.
//
// The icon's number is a node count, not "go to the target": an enemy with Move 2 takes two
// steps and stops, even if the character is five nodes away. A negative direction means the
// card put the count at the bottom of the icon, so the enemy retreats instead (p24).
public static class EnemyMovement {

	// Frost reduces an enemy's Move icon values by 1 (p21).
	public static int NodeCount(EnemyMove move, Enemy enemy) {
		int nodes = Mathf.Abs(move.direction);
		if (enemy != null && enemy.HasCondition(EncounterManager.StatusEffect.FROST)) nodes -= 1;
		return Mathf.Max(0, nodes);
	}

	public static List<PathNode> Plan(EnemyMove move, Enemy enemy, Node2D self, Node2D target) {
		int nodes = NodeCount(move, enemy);
		if (nodes == 0 || target == null) return null;

		return move.direction > 0 ? Towards(self, target, nodes) : AwayFrom(self, target, nodes);
	}

	// Stops on the target's node rather than trying to walk onto it forever (p24).
	private static List<PathNode> Towards(Node2D self, Node2D target, int nodes) {
		List<PathNode> full = Pathfinding.FindPath(EncounterManager.pathGrid, self, target);

		// A* only answers "can I stand on the target's node", so it comes back empty once
		// that node is full or walled off. The enemy should still close as far as it can,
		// so fall back to stepping greedily nearer rather than standing still.
		if (full == null || full.Count == 0) return Approach(self, target, nodes);

		List<PathNode> trimmed = new List<PathNode>();
		for (int i = 0; i < full.Count && i < nodes; i++) trimmed.Add(full[i]);
		return trimmed;
	}

	// Mirror of AwayFrom: take whichever neighbour is nearest the target, and stop early
	// when nothing gets closer.
	private static List<PathNode> Approach(Node2D self, Node2D target, int nodes) =>
		Step(self, target, nodes, closer: true);

	private static List<PathNode> Step(Node2D self, Node2D target, int nodes, bool closer) {
		PathGrid grid = EncounterManager.pathGrid;
		PathNode current = grid.NodeFromObj(self);
		PathNode goal = grid.NodeFromObj(target);

		List<PathNode> steps = new List<PathNode>();
		for (int step = 0; step < nodes; step++) {
			int bestDistance = Distance(current, goal);
			PathNode best = null;

			foreach (PathNode neighbour in grid.GetNeighbours(current)) {
				if (grid.IsBlocked(neighbour) || steps.Contains(neighbour)) continue;
				int distance = Distance(neighbour, goal);
				if (closer ? distance < bestDistance : distance > bestDistance) {
					bestDistance = distance;
					best = neighbour;
				}
			}

			if (best == null) break;
			steps.Add(best);
			current = best;
		}
		return steps.Count > 0 ? steps : null;
	}

	// Greedy retreat: each step takes whichever neighbour is farthest from the target, and
	// stops early when nothing is farther — which is what happens when it backs into a corner.
	private static List<PathNode> AwayFrom(Node2D self, Node2D target, int nodes) =>
		Step(self, target, nodes, closer: false);

	// Neighbours include diagonals, so node distance is the Chebyshev distance.
	public static int Distance(PathNode a, PathNode b) =>
		Mathf.Max(Mathf.Abs(a.gridX - b.gridX), Mathf.Abs(a.gridY - b.gridY));

	public static int Distance(Node2D a, Node2D b) {
		PathGrid grid = EncounterManager.pathGrid;
		return Distance(grid.NodeFromObj(a), grid.NodeFromObj(b));
	}

	public static GameNode GameNodeAt(PathNode node) {
		int child = (node.gridY * EncounterManager.gridSize) + node.gridX;
		return EncounterManager.pathGrid.GetChild<GameNode>(child);
	}

	// A push moves a character onto an adjacent node farther from the pusher (p21). Every
	// neighbour of the pushed-from node is exactly one node farther, so they are all equally
	// valid and the rules let the players pick; this takes the first with room. A node that
	// is full has none, so the push finds nowhere to go and the character stays put.
	public static GameNode PushDestination(PathNode from) {
		PathGrid grid = EncounterManager.pathGrid;

		// Prefer somewhere with room so a push does not immediately owe another push.
		PathNode fallback = null;
		foreach (PathNode neighbour in grid.GetNeighbours(from)) {
			if (grid.IsBlocked(neighbour)) continue;
			if (grid.HasRoom(neighbour)) return GameNodeAt(neighbour);
			fallback ??= neighbour;
		}
		return fallback == null ? null : GameNodeAt(fallback);
	}
}
