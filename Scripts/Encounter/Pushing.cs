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
// A character whose gear is immune to Push (Black Iron Greatshield) is never moved.
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
	public static async Task<bool> Shove(Node2D model, Vector2I direction) {
		PathGrid grid = EncounterManager.pathGrid;
		if (grid == null || !GodotObject.IsInstanceValid(model)) return false;
		if (EncounterManager.GetPlayerToken(model)?.player?.IsPushImmune() ?? false) return false;

		List<GameNode> options = Destinations(grid.NodeFromObj(model), direction);
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
}
