using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

// Pushes (p21, as changed by the V2 rules). A pushed model goes straight on, away from the
// pusher: onto the node in the direction the pusher came from, carried on through the
// pushed model's node. V2 replaced p21's "any adjacent node farther away, players choose".
//
// The direction is a grid step, each component -1, 0 or 1. When pusher and pushed share a
// node there is no "away", so the pusher's own last step carries on instead. "Away" is the
// node straight on and the two 45° either side of it; of those that are walkable and have
// room, the players choose (p21 gives them the choice; V2 only narrows it to that
// direction), through PushPrompt.ChooseNode, whichever side is pushing. With one
// candidate it is taken without asking, and with none the model is not moved.
//
// A character whose gear is immune to Push (Black Iron Greatshield) is never moved, and a
// boss is never pushed by a character's attack (p21).
//
// Pushed by a boss, a character stays in the arc it was in (p28): of the nodes ahead, only
// those in that arc. When a wall leaves none, it goes to an open neighbouring node along the
// wall instead (p28), the players choosing (Matt, Oct 2026: both rules at once).
//
// The over-full node push (p10) is not this: it has no pusher, and keeps
// EnemyMovement.PushDestination.
public static class Pushing
{
	// The eight grid steps in turning order, so neighbours in this array are 45° apart.
	private static readonly Vector2I[] COMPASS = {
		new Vector2I(1, 0), new Vector2I(1, 1), new Vector2I(0, 1), new Vector2I(-1, 1),
		new Vector2I(-1, 0), new Vector2I(-1, -1), new Vector2I(0, -1), new Vector2I(1, -1),
	};

	public static Vector2I Step(PathNode from, PathNode to) =>
		from == null || to == null ? Vector2I.Zero
			: new Vector2I(System.Math.Sign(to.gridX - from.gridX), System.Math.Sign(to.gridY - from.gridY));

	// Away from the pusher; the pusher's last step when they share a node.
	public static Vector2I Away(Node2D pusher, Node2D pushed, Vector2I pushersLastStep) {
		PathGrid grid = EncounterManager.pathGrid;
		if (grid == null || pusher == null || pushed == null) return Vector2I.Zero;
		Vector2I away = Step(grid.NodeFromObj(pusher), grid.NodeFromObj(pushed));
		return away == Vector2I.Zero ? pushersLastStep : away;
	}

	// Straight on first, then the two flanks.
	public static List<GameNode> Destinations(PathNode at, Vector2I direction) {
		List<GameNode> options = new List<GameNode>();
		PathGrid grid = EncounterManager.pathGrid;
		if (grid == null || at == null || direction == Vector2I.Zero) return options;

		int index = System.Array.IndexOf(COMPASS, direction);
		if (index < 0) return options;
		foreach (int turn in new[] { 0, 1, -1 }) {
			Vector2I step = COMPASS[(index + turn + COMPASS.Length) % COMPASS.Length];
			foreach (PathNode neighbour in grid.GetNeighbours(at)) {
				if (neighbour.gridX - at.gridX != step.X || neighbour.gridY - at.gridY != step.Y) continue;
				if (!grid.IsBlocked(neighbour) && grid.HasRoom(neighbour)) options.Add(grid.GameNodeAt(neighbour));
			}
		}
		return options;
	}

	// Returns false when there was nowhere to go and the model stayed put. Awaited, because
	// with more than one way to go the players are asked.
	public static async Task<bool> Shove(Node2D model, Vector2I direction, Enemy boss = null) {
		PathGrid grid = EncounterManager.pathGrid;
		if (grid == null || !GodotObject.IsInstanceValid(model)) return false;
		if (EncounterManager.GetPlayerToken(model)?.player?.IsPushImmune() ?? false) return false;
		if (EncounterManager.GetEnemy(model)?.isBoss ?? false) return false;

		List<GameNode> options = Destinations(grid.NodeFromObj(model), direction);
		if (boss != null) options = InArc(boss, model, options);
		if (options.Count == 0) return false;
		GameNode destination = options[0];
		if (options.Count > 1 && EncounterManager.pushPrompt != null) {
			destination = await EncounterManager.pushPrompt.ChooseNode(model, options);
		}
		if (destination == null || !GodotObject.IsInstanceValid(model)) return false;
		if (!EncounterManager.MovePlayer(model, destination, (Control)model.GetParent(), stumble: true)) return false;
		EncounterManager.ApplyNodeHazard(destination, model);
		return true;
	}

	// A boss's leap (p29): onto any open neighbouring node, the players choosing, never back onto
	// `avoid` (the node the boss leapt from, which it still stands on). False when it stayed.
	public static async Task<bool> ShoveAnywhere(Node2D model, GameNode avoid) {
		PathGrid grid = EncounterManager.pathGrid;
		if (grid == null || !GodotObject.IsInstanceValid(model)) return false;
		if (EncounterManager.GetPlayerToken(model)?.player?.IsPushImmune() ?? false) return false;
		List<GameNode> options = new List<GameNode>();
		foreach (PathNode neighbour in grid.GetNeighbours(grid.NodeFromObj(model))) {
			GameNode node = grid.GameNodeAt(neighbour);
			if (node != avoid && BossArcs.BossOn(node) == null && !grid.IsBlocked(neighbour) && grid.HasRoom(neighbour)) options.Add(node);
		}
		if (options.Count == 0) return false;
		GameNode destination = options[0];
		if (options.Count > 1 && EncounterManager.pushPrompt != null) destination = await EncounterManager.pushPrompt.ChooseNode(model, options);
		if (destination == null || !GodotObject.IsInstanceValid(model)) return false;
		if (!EncounterManager.MovePlayer(model, destination, (Control)model.GetParent(), stumble: true)) return false;
		EncounterManager.ApplyNodeHazard(destination, model);
		return true;
	}

	private static List<GameNode> InArc(Enemy boss, Node2D model, List<GameNode> ahead) {
		PlayerToken token = EncounterManager.GetPlayerToken(model);
		if (token == null || !GodotObject.IsInstanceValid(boss)) return ahead;
		BossAction.Arcs arcs = BossArcs.OfCharacter(boss, token);
		GameNode bossNode = boss.GetParent()?.GetParent() as GameNode;
		List<GameNode> inArc = ahead.FindAll(n => n != bossNode && (BossArcs.OfNode(boss, n) & arcs) != 0);
		if (inArc.Count > 0) return inArc;

		List<GameNode> open = new List<GameNode>();
		List<GameNode> byWall = new List<GameNode>();
		PathGrid grid = EncounterManager.pathGrid;
		foreach (PathNode neighbour in grid.GetNeighbours(grid.NodeFromObj(model))) {
			GameNode node = grid.GameNodeAt(neighbour);
			if (node == bossNode || grid.IsBlocked(neighbour) || !grid.HasRoom(neighbour)) continue;
			open.Add(node);
			if (ByWall(neighbour)) byWall.Add(node);
		}
		return byWall.Count > 0 ? byWall : open;
	}

	// A node at the tile's edge: something next to it is off the board.
	private static bool ByWall(PathNode node) {
		PathGrid grid = EncounterManager.pathGrid;
		List<PathNode> neighbours = grid.GetNeighbours(node);
		return neighbours.Count < 8 || neighbours.Exists(n => grid.IsBlocked(n));
	}
}
