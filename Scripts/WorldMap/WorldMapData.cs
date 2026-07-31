using Godot;
using System.Collections.Generic;

public enum WorldTerrain { GRASS, MOUNTAIN, SNOW, CITY, SAND, VOLCANO, LAVA, WATER }

public enum WorldEncounterType { NONE, BONFIRE, ENCOUNTER, BOSS }

public class WorldNodeData {
	public string id = "";
	public string displayName = "";
	public int q;
	public int r;
	public WorldTerrain terrain = WorldTerrain.GRASS;
	public WorldEncounterType encounterType = WorldEncounterType.NONE;
	public int level = 1;
	public int elevation = 1;
	public bool isStart = false;

	public bool IsImpassable => terrain == WorldTerrain.LAVA || terrain == WorldTerrain.WATER;
	public string Title => string.IsNullOrEmpty(displayName) ? id : displayName;
}

public class WorldMapData {
	public string name = "Unnamed Campaign";
	public List<WorldNodeData> nodes = new List<WorldNodeData>();

	private Dictionary<string, WorldNodeData> byId = new Dictionary<string, WorldNodeData>();
	private Dictionary<Vector2I, WorldNodeData> byCoord = new Dictionary<Vector2I, WorldNodeData>();

	// Axial (q, r) offsets of the six hex neighbors.
	public static readonly Vector2I[] NEIGHBOR_OFFSETS = {
		new Vector2I(1, 0), new Vector2I(-1, 0),
		new Vector2I(0, 1), new Vector2I(0, -1),
		new Vector2I(1, -1), new Vector2I(-1, 1),
	};

	public WorldNodeData GetNode(string id) =>
		!string.IsNullOrEmpty(id) && byId.TryGetValue(id, out var n) ? n : null;

	public WorldNodeData GetNodeAt(int q, int r) =>
		byCoord.TryGetValue(new Vector2I(q, r), out var n) ? n : null;

	public WorldNodeData GetStartNode() {
		foreach (var n in nodes)
			if (n.isStart) return n;
		return nodes.Count > 0 ? nodes[0] : null;
	}

	public bool AreNeighbors(WorldNodeData a, WorldNodeData b) {
		if (a == null || b == null) return false;
		foreach (var off in NEIGHBOR_OFFSETS)
			if (a.q + off.X == b.q && a.r + off.Y == b.r) return true;
		return false;
	}

	public IEnumerable<WorldNodeData> GetNeighbors(WorldNodeData node) {
		foreach (var off in NEIGHBOR_OFFSETS) {
			var n = GetNodeAt(node.q + off.X, node.r + off.Y);
			if (n != null) yield return n;
		}
	}

	public static WorldMapData LoadFromFile(string path) {
		if (!FileAccess.FileExists(path)) {
			GD.PushError($"WorldMapData: campaign file not found: {path}");
			return null;
		}
		var parsed = Json.ParseString(FileAccess.GetFileAsString(path));
		if (parsed.VariantType != Variant.Type.Dictionary) {
			GD.PushError($"WorldMapData: '{path}' is not valid JSON (expected a top-level object)");
			return null;
		}
		var root = parsed.AsGodotDictionary();

		var map = new WorldMapData();
		map.name = GetString(root, "name", "Unnamed Campaign");

		if (!root.TryGetValue("nodes", out var nodesVar) || nodesVar.VariantType != Variant.Type.Array) {
			GD.PushError($"WorldMapData: '{path}' has no \"nodes\" array");
			return null;
		}

		foreach (var entry in nodesVar.AsGodotArray()) {
			if (entry.VariantType != Variant.Type.Dictionary) continue;
			var dict = entry.AsGodotDictionary();

			var node = new WorldNodeData();
			node.q = GetInt(dict, "q", 0);
			node.r = GetInt(dict, "r", 0);
			node.id = GetString(dict, "id", $"{node.q},{node.r}");
			node.displayName = GetString(dict, "name", "");
			node.terrain = GetEnum(dict, "terrain", WorldTerrain.GRASS, path);
			node.encounterType = GetEnum(dict, "encounter", WorldEncounterType.NONE, path);
			node.level = Mathf.Clamp(GetInt(dict, "level", 1), 1, 4);
			node.elevation = Mathf.Clamp(GetInt(dict, "elevation", 1), 0, 8);
			node.isStart = GetBool(dict, "start", false);

			var coord = new Vector2I(node.q, node.r);
			if (map.byCoord.ContainsKey(coord)) {
				GD.PushError($"WorldMapData: duplicate node at q={node.q}, r={node.r} in '{path}' — skipping '{node.id}'");
				continue;
			}
			if (map.byId.ContainsKey(node.id)) {
				GD.PushError($"WorldMapData: duplicate node id '{node.id}' in '{path}' — skipping");
				continue;
			}

			map.nodes.Add(node);
			map.byId[node.id] = node;
			map.byCoord[coord] = node;
		}

		if (map.nodes.Count == 0) {
			GD.PushError($"WorldMapData: '{path}' contains no valid nodes");
			return null;
		}

		int startCount = map.nodes.FindAll(n => n.isStart).Count;
		if (startCount == 0)
			GD.PushWarning($"WorldMapData: '{path}' has no node with \"start\": true — using first node '{map.nodes[0].id}'");
		else if (startCount > 1)
			GD.PushWarning($"WorldMapData: '{path}' has {startCount} start nodes — using '{map.GetStartNode().id}'");

		return map;
	}

	private static string GetString(Godot.Collections.Dictionary dict, string key, string fallback) =>
		dict.TryGetValue(key, out var v) && v.VariantType == Variant.Type.String ? v.AsString() : fallback;

	private static int GetInt(Godot.Collections.Dictionary dict, string key, int fallback) =>
		dict.TryGetValue(key, out var v) && (v.VariantType == Variant.Type.Float || v.VariantType == Variant.Type.Int)
			? v.AsInt32() : fallback;

	private static bool GetBool(Godot.Collections.Dictionary dict, string key, bool fallback) =>
		dict.TryGetValue(key, out var v) && v.VariantType == Variant.Type.Bool ? v.AsBool() : fallback;

	private static T GetEnum<T>(Godot.Collections.Dictionary dict, string key, T fallback, string path) where T : struct {
		if (!dict.TryGetValue(key, out var v) || v.VariantType != Variant.Type.String) return fallback;
		string raw = v.AsString();
		if (System.Enum.TryParse<T>(raw, true, out var result)) return result;
		GD.PushWarning($"WorldMapData: unknown {key} value '{raw}' in '{path}' — using {fallback}");
		return fallback;
	}
}
