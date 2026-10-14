using Godot;
using System.Collections.Generic;

// The ladders, trees and doors of the map (WorldEdgeData), drawn over the hexes: a ladder up a
// wall, a trunk standing or fallen across a gap, an arched door in the wall between two hexes.
// Code-drawn in the hex art's palette, placed from the hexes' own anchors so rotation carries
// them. An edge shows once both its ends are on the map (anything but UNKNOWN).
public partial class WorldMapEdges : Control {
	// A hex's top-face centre in map space; WorldMap supplies its NodeAnchor.
	public System.Func<WorldNodeData, Vector2> anchorOf;

	private static readonly Color WOOD = new Color(0.52f, 0.36f, 0.19f);
	private static readonly Color WOOD_DARK = new Color(0.20f, 0.13f, 0.07f);
	private static readonly Color PALE = new Color(0.88f, 0.82f, 0.70f, 0.55f);
	private static readonly Color LEAF = new Color(0.16f, 0.30f, 0.15f);
	private static readonly Color STONE = new Color(0.20f, 0.19f, 0.22f);
	private static readonly Color STONE_LIGHT = new Color(0.62f, 0.60f, 0.55f);
	private static readonly Color GOLD = new Color(1f, 0.82f, 0.32f);
	private static readonly Color SHADOW = new Color(0f, 0f, 0f, 0.28f);

	// A Control is only drawn while its own rect is in view, so this one is made big enough to
	// hold the whole map, and everything is drawn offset back to the map's own coordinates.
	private const float EXTENT = 40000f;
	private const float DOOR_HALF = 13f;
	private const float DOOR_POST = 13f;
	private const int ARC_STEPS = 10;

	public override void _Ready() {
		MouseFilter = MouseFilterEnum.Ignore;
		Position = new Vector2(-EXTENT, -EXTENT);
		Size = new Vector2(EXTENT * 2f, EXTENT * 2f);
	}

	public override void _Draw() {
		WorldMapData map = WorldMapManager.MapData;
		if (anchorOf == null || map == null) return;

		DrawSetTransform(new Vector2(EXTENT, EXTENT), 0, Vector2.One);
		var shown = new List<(WorldEdgeData edge, WorldNodeData a, WorldNodeData b, float y)>();
		foreach (WorldEdgeData edge in map.edges) {
			WorldNodeData a = map.GetNode(edge.a), b = map.GetNode(edge.b);
			if (a == null || b == null) continue;
			if (WorldMapManager.SightOf(a.id) == WorldSight.UNKNOWN || WorldMapManager.SightOf(b.id) == WorldSight.UNKNOWN) continue;
			shown.Add((edge, a, b, (anchorOf(a).Y + anchorOf(b).Y) * 0.5f));
		}
		shown.Sort((x, y) => x.y.CompareTo(y.y));

		foreach (var (edge, a, b, _) in shown) {
			bool open = WorldMapManager.IsOpen(edge);
			switch (edge.kind) {
				case WorldEdgeKind.LADDER: DrawLadder(a, b, open); break;
				case WorldEdgeKind.TREE: DrawTree(a, b, open); break;
				default: DrawDoor(a, b, open, !string.IsNullOrEmpty(edge.keyId)); break;
			}
		}
	}

	private static float Wall(WorldNodeData node) => node.elevation * WorldMapNode.WALL_STEP;

	// Where the shared edge of two neighbours is, on the ground of each: the midpoint of their
	// centres in the flat plane, lifted to each hex's own height.
	private (Vector2 onA, Vector2 onB) SharedEdge(WorldNodeData a, WorldNodeData b) {
		Vector2 mid = ((anchorOf(a) + new Vector2(0, Wall(a))) + (anchorOf(b) + new Vector2(0, Wall(b)))) * 0.5f;
		return (mid - new Vector2(0, Wall(a)), mid - new Vector2(0, Wall(b)));
	}

	// Closed, a pale ladder lies on the high hex's face; open, it spans from the low hex's face
	// up the wall to the high one's.
	private void DrawLadder(WorldNodeData a, WorldNodeData b, bool open) {
		WorldNodeData low = a.elevation <= b.elevation ? a : b;
		WorldNodeData high = low == a ? b : a;
		var (bottom, top) = SharedEdge(low, high);

		Vector2 from, to;
		Color rail, rung;
		if (open) {
			from = bottom;
			to = top;
			rail = WOOD;
			rung = WOOD.Lightened(0.15f);
		} else {
			Vector2 inward = anchorOf(high) - top;
			Vector2 dir = inward.Normalized();
			from = top;
			to = top + dir * Mathf.Min(34f, inward.Length());
			rail = PALE;
			rung = PALE;
		}

		Vector2 along = (to - from).Normalized();
		Vector2 side = new Vector2(-along.Y, along.X) * 5f;
		float length = from.DistanceTo(to);
		if (open) {
			DrawLine(from + side, to + side, WOOD_DARK, 5f, true);
			DrawLine(from - side, to - side, WOOD_DARK, 5f, true);
		} else {
			DrawLine(from + side + new Vector2(2, 3), to + side + new Vector2(2, 3), SHADOW, 4f, true);
			DrawLine(from - side + new Vector2(2, 3), to - side + new Vector2(2, 3), SHADOW, 4f, true);
		}
		DrawLine(from + side, to + side, rail, 3f, true);
		DrawLine(from - side, to - side, rail, 3f, true);
		int rungs = Mathf.Max(2, Mathf.RoundToInt(length / 8f));
		for (int i = 0; i <= rungs; i++) {
			Vector2 at = from.Lerp(to, i / (float)rungs);
			DrawLine(at - side, at + side, rung, 2.2f, true);
		}
	}

	// Closed, a trunk stands on one end's hex with its crown above; open, it lies foot to foot
	// across the gap between them.
	private void DrawTree(WorldNodeData a, WorldNodeData b, bool open) {
		Vector2 pa = anchorOf(a), pb = anchorOf(b);
		Vector2 dir = (pb - pa).Normalized();
		float reach = WorldMapNode.HEX_RADIUS * 0.5f;

		if (!open) {
			Vector2 foot = pa + dir * reach;
			DrawEllipse(foot + new Vector2(0, 3), 12f, 5f, SHADOW);
			Vector2 top = foot + new Vector2(0, -30);
			DrawLine(foot, top, WOOD_DARK, 7f, true);
			DrawLine(foot + new Vector2(-1.5f, 0), top + new Vector2(-1.5f, 0), WOOD, 2.2f, true);
			DrawLine(foot + new Vector2(0, -14), foot + new Vector2(-9, -22), WOOD_DARK, 3f, true);
			DrawLine(foot + new Vector2(0, -18), foot + new Vector2(9, -27), WOOD_DARK, 3f, true);
			DrawCircle(top + new Vector2(0, -2), 12f, LEAF.Darkened(0.2f));
			DrawCircle(top + new Vector2(-7, 4), 8f, LEAF);
			DrawCircle(top + new Vector2(7, 3), 8.5f, LEAF.Darkened(0.1f));
			DrawCircle(top + new Vector2(-2, -6), 7f, LEAF.Lightened(0.12f));
			return;
		}

		Vector2 fa = pa + dir * reach, fb = pb - dir * reach;
		DrawLine(fa + new Vector2(2, 4), fb + new Vector2(2, 4), SHADOW, 9f, true);
		DrawLine(fa, fb, WOOD_DARK, 9f, true);
		DrawLine(fa + new Vector2(0, -1.5f), fb + new Vector2(0, -1.5f), WOOD, 4f, true);
		DrawCircle(fb, 4.5f, WOOD);
		DrawArc(fb, 2.6f, 0, Mathf.Tau, 12, WOOD_DARK, 1.2f, true);
		DrawCircle(fa + dir * -3f + new Vector2(0, -4), 9f, LEAF.Darkened(0.2f));
		DrawCircle(fa + dir * -9f + new Vector2(0, 1), 6.5f, LEAF);
	}

	// An arch standing on the edge between the two hexes. Closed it is a shut wooden door in
	// dark stone, with a gold keyhole if it takes a key; open, only the stone, with light inside.
	private void DrawDoor(WorldNodeData a, WorldNodeData b, bool open, bool keyed) {
		var (onA, onB) = SharedEdge(a, b);
		Vector2 at = a.elevation >= b.elevation ? onA : onB;

		DrawEllipse(at + new Vector2(0, 2), DOOR_HALF + 3f, 5f, SHADOW);

		DrawColoredPolygon(Arch(at, DOOR_HALF + 3f, DOOR_POST), STONE);
		Vector2[] inner = Arch(at, DOOR_HALF - 1f, DOOR_POST - 1f);
		DrawColoredPolygon(inner, open ? new Color(1f, 0.90f, 0.60f, 0.65f) : new Color(0.27f, 0.17f, 0.09f));
		var outline = new List<Vector2>(Arch(at, DOOR_HALF + 3f, DOOR_POST));
		outline.Add(outline[0]);
		DrawPolyline(outline.ToArray(), STONE_LIGHT, 1.4f, true);

		if (open) return;
		Vector2 centre = at + new Vector2(0, -DOOR_POST);
		DrawLine(centre + new Vector2(0, -DOOR_HALF + 2f), at + new Vector2(0, -1), WOOD_DARK, 1.6f, true);
		DrawLine(at + new Vector2(-DOOR_HALF + 1f, -DOOR_POST + 3f), at + new Vector2(DOOR_HALF - 1f, -DOOR_POST + 3f), STONE_LIGHT with { A = 0.8f }, 1.4f, true);
		if (!keyed) return;
		Vector2 hole = at + new Vector2(0, -DOOR_POST + 8f);
		DrawCircle(hole, 3.4f, GOLD);
		DrawColoredPolygon(new[] { hole + new Vector2(-1.5f, 1f), hole + new Vector2(1.5f, 1f), hole + new Vector2(2.4f, 7f), hole + new Vector2(-2.4f, 7f) }, GOLD);
		DrawCircle(hole, 1.3f, WOOD_DARK);
	}

	private void DrawEllipse(Vector2 centre, float rx, float ry, Color colour) {
		const int STEPS = 16;
		var points = new Vector2[STEPS];
		for (int i = 0; i < STEPS; i++) {
			float angle = Mathf.Tau * i / STEPS;
			points[i] = centre + new Vector2(Mathf.Cos(angle) * rx, Mathf.Sin(angle) * ry);
		}
		DrawColoredPolygon(points, colour);
	}

	private static Vector2[] Arch(Vector2 at, float half, float post) {
		var points = new List<Vector2> { at + new Vector2(-half, 0), at + new Vector2(-half, -post) };
		for (int i = 1; i < ARC_STEPS; i++) {
			float angle = Mathf.Pi + Mathf.Pi * i / ARC_STEPS;
			points.Add(at + new Vector2(0, -post) + Vector2.FromAngle(angle) * half);
		}
		points.Add(at + new Vector2(half, -post));
		points.Add(at + new Vector2(half, 0));
		return points.ToArray();
	}
}
