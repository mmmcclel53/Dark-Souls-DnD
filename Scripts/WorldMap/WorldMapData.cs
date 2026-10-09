using Godot;
using System.Collections.Generic;

public enum WorldTerrain { GRASS, MOUNTAIN, SNOW, CITY, SAND, VOLCANO, LAVA, WATER }

public enum WorldEncounterType { NONE, BONFIRE, ENCOUNTER, BOSS }

// A boss's weight, which sets its soul payout (SoulEconomy.BossSouls).
public enum WorldBossKind { MINI, MAIN, MEGA }

public class WorldNodeData {
	public string id = "";
	public string displayName = "";
	public int q;
	public int r;
	public WorldTerrain terrain = WorldTerrain.GRASS;
	public WorldEncounterType encounterType = WorldEncounterType.NONE;
	public int level = 1;
	// The campaign section this node belongs to, from 1; 0 when the map has no sections.
	public int section = 0;
	public WorldBossKind boss = WorldBossKind.MAIN;
	public int elevation = 1;
	public bool isStart = false;

	public bool IsImpassable => terrain == WorldTerrain.LAVA || terrain == WorldTerrain.WATER;
	public string Title => string.IsNullOrEmpty(displayName) ? id : displayName;
}

public class WorldMapData {
	public string name = "Unnamed Campaign";
	// How many sections the campaign has (Small 6, Medium 10, Large 14), which sets every
	// section's soul budget. 0 for a map without sections, whose encounters pay nothing.
	public int sections = 0;
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

	// Adds a node to the map, refusing one on a taken coordinate or with a taken id.
	public bool Add(WorldNodeData node) {
		var coord = new Vector2I(node.q, node.r);
		if (byCoord.ContainsKey(coord) || byId.ContainsKey(node.id)) return false;
		nodes.Add(node);
		byId[node.id] = node;
		byCoord[coord] = node;
		return true;
	}

	public static WorldMapData LoadFromFile(string path) {
		if (!FileAccess.FileExists(path)) {
			GD.PushError($"WorldMapData: campaign file not found: {path}");
			return null;
		}
		return Parse(FileAccess.GetFileAsString(path), path);
	}

	// A campaign as JSON: a file under res://Campaigns, or a generated one kept in the save.
	// `path` only names it in errors.
	public static WorldMapData Parse(string json, string path) {
		var parsed = Json.ParseString(json);
		if (parsed.VariantType != Variant.Type.Dictionary) {
			GD.PushError($"WorldMapData: '{path}' is not valid JSON (expected a top-level object)");
			return null;
		}
		var root = parsed.AsGodotDictionary();

		var map = new WorldMapData();
		map.name = GetString(root, "name", "Unnamed Campaign");
		map.sections = Mathf.Max(0, GetInt(root, "sections", 0));

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
			node.section = Mathf.Clamp(GetInt(dict, "section", 0), 0, map.sections);
			node.boss = GetEnum(dict, "boss", WorldBossKind.MAIN, path);
			node.elevation = Mathf.Clamp(GetInt(dict, "elevation", 1), 0, 8);
			node.isStart = GetBool(dict, "start", false);

			if (map.GetNodeAt(node.q, node.r) != null) {
				GD.PushError($"WorldMapData: duplicate node at q={node.q}, r={node.r} in '{path}' — skipping '{node.id}'");
				continue;
			}
			if (map.GetNode(node.id) != null) {
				GD.PushError($"WorldMapData: duplicate node id '{node.id}' in '{path}' — skipping");
				continue;
			}
			map.Add(node);
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

	// The same format Parse reads, so a generated campaign can be kept in the save.
	public string ToJson() {
		var list = new Godot.Collections.Array();
		foreach (WorldNodeData node in nodes) {
			var dict = new Godot.Collections.Dictionary {
				{ "id", node.id }, { "q", node.q }, { "r", node.r },
				{ "terrain", node.terrain.ToString() }, { "elevation", node.elevation },
			};
			if (!string.IsNullOrEmpty(node.displayName)) dict["name"] = node.displayName;
			if (node.encounterType != WorldEncounterType.NONE) dict["encounter"] = node.encounterType.ToString();
			if (node.encounterType == WorldEncounterType.ENCOUNTER || node.encounterType == WorldEncounterType.BOSS) dict["level"] = node.level;
			if (node.encounterType == WorldEncounterType.BOSS) dict["boss"] = node.boss.ToString();
			if (node.section > 0) dict["section"] = node.section;
			if (node.isStart) dict["start"] = true;
			list.Add(dict);
		}
		var root = new Godot.Collections.Dictionary { { "name", name }, { "sections", sections }, { "nodes", list } };
		return Json.Stringify(root, "	");
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
