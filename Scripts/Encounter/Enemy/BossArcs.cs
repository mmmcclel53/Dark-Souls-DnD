using Godot;
using System.Collections.Generic;

// Boss arcs (p27-28). A boss always faces an adjacent node, and the X on its base splits the
// tile into its front, right, back and left arcs. A node on one of the X's lines is in both
// arcs either side of it.
//
// Everything here is in grid steps. The grid is the board's lattice turned 45° (Board
// Geometry in CLAUDE.md), a rotation and a scale, so angles in grid space are the angles on
// the board and an arc test needs no screen coordinates. A facing is one of the eight
// compass steps; the four arcs are centred on it and on its quarter turns.
//
// A character on the boss's own node stands touching its base in one arc (p28):
// PlayerToken.bossSide, the compass step from the boss's centre to where it stands. It is a
// direction on the board, not an arc, so it stays put when the boss turns (p29: "characters on
// the node with a boss do not move when the boss is turned") and the arc it is in changes.
public static class BossArcs
{
	// The eight grid steps in turning order: each is 45° clockwise of the last on the board.
	public static readonly Vector2I[] COMPASS = {
		new Vector2I(1, 0), new Vector2I(1, 1), new Vector2I(0, 1), new Vector2I(-1, 1),
		new Vector2I(-1, 0), new Vector2I(-1, -1), new Vector2I(0, -1), new Vector2I(1, -1),
	};

	public static readonly BossAction.Arcs[] EACH = {
		BossAction.Arcs.FRONT, BossAction.Arcs.RIGHT, BossAction.Arcs.BACK, BossAction.Arcs.LEFT,
	};

	// `eighths` of a full turn, clockwise on the board: 2 is a quarter turn right (p29).
	public static Vector2I Turn(Vector2I direction, int eighths) {
		int index = System.Array.IndexOf(COMPASS, direction);
		if (index < 0) return direction;
		return COMPASS[((index + eighths) % 8 + 8) % 8];
	}

	// The arc's centre line, as a compass step from the boss.
	public static Vector2I Centre(Vector2I facing, BossAction.Arcs arc) => arc switch {
		BossAction.Arcs.RIGHT => Turn(facing, 2),
		BossAction.Arcs.BACK => Turn(facing, 4),
		BossAction.Arcs.LEFT => Turn(facing, -2),
		_ => facing,
	};

	// Which arcs a direction from the boss lies in: one, or two on a line between arcs.
	// The grid's turn to the board keeps handedness, so a positive cross is the boss's right.
	public static BossAction.Arcs Of(Vector2I offset, Vector2I facing) {
		if (offset == Vector2I.Zero || facing == Vector2I.Zero) return BossAction.Arcs.NONE;
		int dot = offset.X * facing.X + offset.Y * facing.Y;
		int cross = facing.X * offset.Y - facing.Y * offset.X;
		BossAction.Arcs arcs = BossAction.Arcs.NONE;
		if (dot > 0 && dot >= Mathf.Abs(cross)) arcs |= BossAction.Arcs.FRONT;
		if (dot < 0 && -dot >= Mathf.Abs(cross)) arcs |= BossAction.Arcs.BACK;
		if (cross > 0 && cross >= Mathf.Abs(dot)) arcs |= BossAction.Arcs.RIGHT;
		if (cross < 0 && -cross >= Mathf.Abs(dot)) arcs |= BossAction.Arcs.LEFT;
		return arcs;
	}

	public static IEnumerable<BossAction.Arcs> Split(BossAction.Arcs arcs) {
		foreach (BossAction.Arcs arc in EACH) {
			if ((arcs & arc) != 0) yield return arc;
		}
	}

	// ----- On the board -----

	public static Enemy BossOn(Control node) {
		if (node == null) return null;
		foreach (Node2D model in EncounterManager.GetPlayersInNode(node, "Enemy")) {
			Enemy enemy = EncounterManager.GetEnemy(model);
			if (enemy != null && enemy.isBoss) return enemy;
		}
		return null;
	}

	public static IEnumerable<Enemy> Bosses() {
		foreach (Node2D model in EncounterManager.enemies) {
			Enemy enemy = EncounterManager.GetEnemy(model);
			if (enemy != null && enemy.isBoss) yield return enemy;
		}
	}

	public static Vector2I GridOf(Node2D model) {
		PathNode node = EncounterManager.pathGrid?.NodeFromObj(model);
		return node == null ? Vector2I.Zero : new Vector2I(node.gridX, node.gridY);
	}

	// The grid's GameNodes are its first children, in row order (PathGrid.GameNodeAt).
	public static Vector2I GridOf(GameNode node) {
		int index = node.GetIndex();
		return new Vector2I(index % EncounterManager.gridSize, index / EncounterManager.gridSize);
	}

	// The node's direction from the boss's node.
	public static Vector2I Offset(Enemy boss, GameNode node) => GridOf(node) - GridOf((Node2D)boss.GetParent());

	public static BossAction.Arcs OfNode(Enemy boss, GameNode node) => Of(Offset(boss, node), boss.facing);

	// The arcs a character is in: by where it stands on the boss's node, or by its node.
	public static BossAction.Arcs OfCharacter(Enemy boss, PlayerToken token) {
		if (boss == null || token == null) return BossAction.Arcs.NONE;
		if (token.GetParent()?.GetParent() == boss.GetParent()?.GetParent()) {
			return Of(token.bossSide == Vector2I.Zero ? boss.facing : token.bossSide, boss.facing);
		}
		return Of(GridOf((Node2D)token.GetParent()) - GridOf((Node2D)boss.GetParent()), boss.facing);
	}

	// p28: attacking a boss from a weak arc of the last attack on the top card of its discard
	// pile gains one black die, however many weak arcs the character is in.
	public static bool InWeakArc(PlayerToken attacker, Enemy boss) =>
		boss != null && boss.isBoss && (OfCharacter(boss, attacker) & boss.behaviour.WeakArcs) != 0;

	// For the action bar's rows, which do not know their target yet: the one boss on the board.
	public static bool InAnyWeakArc(PlayerToken attacker) {
		foreach (Enemy boss in Bosses()) {
			if (InWeakArc(attacker, boss)) return true;
		}
		return false;
	}

	// Where a character stepping onto the boss's node may stand: the arcs it is in before
	// moving (p28), at each one's centre.
	public static List<Vector2I> EntrySides(Enemy boss, PlayerToken token) {
		List<Vector2I> sides = new List<Vector2I>();
		BossAction.Arcs arcs = Of(GridOf((Node2D)token.GetParent()) - GridOf((Node2D)boss.GetParent()), boss.facing);
		foreach (BossAction.Arcs arc in Split(arcs)) sides.Add(Centre(boss.facing, arc));
		if (sides.Count == 0) sides.Add(boss.facing);
		return sides;
	}

	// Moving around the boss instead of a walk or run node (p28): to an arc next to one it is in.
	public static List<Vector2I> AroundSides(Enemy boss, PlayerToken token) {
		BossAction.Arcs here = OfCharacter(boss, token);
		List<Vector2I> sides = new List<Vector2I>();
		foreach (BossAction.Arcs arc in Split(here)) {
			foreach (int quarter in new[] { -2, 2 }) {
				Vector2I side = Turn(Centre(boss.facing, arc), quarter);
				if ((Of(side, boss.facing) & here) == 0 && !sides.Contains(side)) sides.Add(side);
			}
		}
		return sides;
	}

	// A successful dodge on the boss's node may go to any arc (p28).
	public static List<Vector2I> AnySides(Enemy boss, PlayerToken token) {
		BossAction.Arcs here = OfCharacter(boss, token);
		List<Vector2I> sides = new List<Vector2I>();
		foreach (BossAction.Arcs arc in EACH) {
			if ((arc & here) == 0) sides.Add(Centre(boss.facing, arc));
		}
		return sides;
	}

	// Leaving the boss's node keeps the character in its arc (p28): only the neighbouring nodes
	// in an arc it is in are open to it.
	public static bool CanLeaveTo(Enemy boss, PlayerToken token, GameNode node) =>
		(OfNode(boss, node) & OfCharacter(boss, token)) != 0;

	// A side on the board, for drawing: the grid step turned to screen axes (y down), unit length.
	public static Vector2 ScreenDirection(Vector2I gridStep) {
		Vector2 screen = new Vector2(gridStep.X + gridStep.Y, gridStep.Y - gridStep.X);
		return screen == Vector2.Zero ? Vector2.Zero : screen.Normalized();
	}
}
