using Godot;
using System.Collections.Generic;

public enum WorldTerrain { GRASS, MOUNTAIN, SNOW, CITY, SAND, VOLCANO, LAVA, WATER }

public enum WorldEncounterType { NONE, BONFIRE, ENCOUNTER, BOSS }

// A boss's weight, which sets its soul payout (SoulEconomy.BossSouls).
public enum WorldBossKind { MINI, MAIN, MEGA }

// What the party knows of a hex (WorldMapManager.SightOf): never seen, only its edge glimpsed
// from below, seen once and remembered, or in sight from where the party stands now.
public enum WorldSight { UNKNOWN, GLIMPSED, KNOWN, SEEN }

// What a hex holds besides any encounter (WorldMapManager.TakeLoot). On an encounter hex it is
// guarded and taken on the win. Only ever append: the names are what the map JSON stores.
public enum WorldLoot { NONE, ESTUS_SHARD, TREASURE, KEY, SOULS, HOMEWARD_BONE }

// A link between two hexes besides plain adjacency (WorldMapManager.CanStep). A ladder joins
// neighbours two or more steps apart in height, a tree two hexes with an impassable or absent
// hex between, a door two neighbours with a wall between. Only ever append: the names are what
// the map JSON stores.
public enum WorldEdgeKind { LADDER, TREE, DOOR }

public class WorldEdgeData {
	// A shortcut is opened from its far end: b is the end farther from the campaign's start by
	// walking with every edge closed, so the party has to get there the long way first
	// (WorldMapData.NormaliseEdges; in a generated map this is the same as farther from the
	// section's bonfire, the only way in). Ladder: a is the low end, and b the high end, which
	// is also the far one. Door: b is the far, inside side; a keyed door also opens from a.
	public string a = "";
	public string b = "";
	public WorldEdgeKind kind = WorldEdgeKind.LADDER;
	// The key that opens a door from either side; empty means it opens only from b.
	public string keyId = "";

	public string Id => a + "|" + b;

	public bool Joins(string x, string y) => (a == x && b == y) || (a == y && b == x);

	public string Other(string x) => x == a ? b : a;
}

// An enemy group that patrols a fixed loop of plain hexes in one section, a hex per party step
// (WorldMapManager.TickRoamers). `loop` is walked in order and closes on itself; a path that
// could not close is stored there-and-back (A B C B), so the loop rule holds either way: every
// step, the last back to the first included, is one CanStep with every edge closed.
public class WorldRoamerData {
	public string id = "";
	public string name = "";
	public int section;
	public List<string> loop = new List<string>();
}

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
	public WorldLoot loot = WorldLoot.NONE;
	// Which door a WorldLoot.KEY opens; its name on the reveal panel.
	public string keyId = "";
	// What is built on the hex (StructureArt draws it once the party knows the hex).
	public WorldStructure structure = WorldStructure.NONE;

	public bool IsImpassable => terrain == WorldTerrain.LAVA || terrain == WorldTerrain.WATER;
	// Ground with nothing built on it: where a roaming threat may walk.
	public bool IsPlain => encounterType == WorldEncounterType.NONE && !IsImpassable;
	public string Title => string.IsNullOrEmpty(displayName) ? id : displayName;
}

public class WorldMapData {
	public string name = "Unnamed Campaign";
	// How many sections the campaign has (Small 6, Medium 10, Large 14), which sets every
	// section's soul budget. 0 for a map without sections, whose encounters pay nothing.
	public int sections = 0;
	public List<WorldNodeData> nodes = new List<WorldNodeData>();
	public List<WorldEdgeData> edges = new List<WorldEdgeData>();
	public List<WorldRoamerData> roamers = new List<WorldRoamerData>();

	private Dictionary<string, WorldNodeData> byId = new Dictionary<string, WorldNodeData>();
	private Dictionary<Vector2I, WorldNodeData> byCoord = new Dictionary<Vector2I, WorldNodeData>();
	private Dictionary<string, List<WorldEdgeData>> edgesByNode = new Dictionary<string, List<WorldEdgeData>>();

	// Axial (q, r) offsets of the six hex neighbors.
	public static readonly Vector2I[] NEIGHBOR_OFFSETS = {
		new Vector2I(1, 0), new Vector2I(-1, 0),
		new Vector2I(0, 1), new Vector2I(0, -1),
		new Vector2I(1, -1), new Vector2I(-1, 1),
	};

	public WorldNodeData GetNode(string id) =>
		!string.IsNullOrEmpty(id) && byId.TryGetValue(id, out var n) ? n : null;

	public WorldRoamerData GetRoamer(string id) =>
		!string.IsNullOrEmpty(id) ? roamers.Find(r => r.id == id) : null;

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

	// Hex steps between two nodes on the axial lattice.
	public static int Distance(WorldNodeData a, WorldNodeData b) {
		int dq = a.q - b.q;
		int dr = a.r - b.r;
		return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(dq + dr)) / 2;
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

	public IReadOnlyList<WorldEdgeData> EdgesAt(string nodeId) =>
		nodeId != null && edgesByNode.TryGetValue(nodeId, out var list) ? list : (IReadOnlyList<WorldEdgeData>)System.Array.Empty<WorldEdgeData>();

	public WorldEdgeData EdgeBetween(string a, string b) {
		foreach (var edge in EdgesAt(a))
			if (edge.Joins(a, b)) return edge;
		return null;
	}

	// Adds an edge whose two ends exist, refusing a second one between the same pair.
	public bool AddEdge(WorldEdgeData edge) {
		if (GetNode(edge.a) == null || GetNode(edge.b) == null || edge.a == edge.b) return false;
		if (EdgeBetween(edge.a, edge.b) != null) return false;
		edges.Add(edge);
		foreach (string end in new[] { edge.a, edge.b }) {
			if (!edgesByNode.TryGetValue(end, out var list)) edgesByNode[end] = list = new List<WorldEdgeData>();
			list.Add(edge);
		}
		return true;
	}

	// The whole movement rule: may the party step from one hex to another? isOpen says which
	// edges have been opened. Neighbours, or the two ends of an open tree; onto a passable hex;
	// up at most one step unless an open ladder joins them (any drop is free); and not through
	// a closed door.
	public bool CanStep(WorldNodeData from, WorldNodeData to, System.Func<WorldEdgeData, bool> isOpen) {
		if (from == null || to == null || from.id == to.id) return false;
		WorldEdgeData edge = EdgeBetween(from.id, to.id);
		bool open = edge != null && isOpen(edge);
		if (!AreNeighbors(from, to) && !(edge != null && edge.kind == WorldEdgeKind.TREE && open)) return false;
		if (to.IsImpassable) return false;
		if (to.elevation - from.elevation > 1 && !(edge != null && edge.kind == WorldEdgeKind.LADDER && open)) return false;
		return !(edge != null && edge.kind == WorldEdgeKind.DOOR && !open);
	}

	// Steps from `origin` to every hex it can reach with every edge closed (a closed ladder and
	// tree add no way, a closed door blocks). The origin is 0.
	public Dictionary<string, int> WalkFrom(WorldNodeData origin) {
		var dist = new Dictionary<string, int>();
		if (origin == null) return dist;
		var queue = new Queue<WorldNodeData>();
		dist[origin.id] = 0;
		queue.Enqueue(origin);
		while (queue.Count > 0) {
			WorldNodeData cur = queue.Dequeue();
			foreach (WorldNodeData next in GetNeighbors(cur)) {
				if (dist.ContainsKey(next.id) || !CanStep(cur, next, _ => false)) continue;
				dist[next.id] = dist[cur.id] + 1;
				queue.Enqueue(next);
			}
		}
		return dist;
	}

	// Puts b at the far end of every tree and door: if a is farther from the start than b by
	// walking (every edge closed), they swap. A ladder is never turned round, since it is kicked
	// down from its high end, but it is warned about when that end is not the far one. Edges with
	// an end the walk cannot reach, or ends the same walk away, are left as written. Idempotent.
	// Returns the ids of the swapped edges, before and after, for state kept under them.
	public List<(string before, string after)> NormaliseEdges() {
		var swapped = new List<(string before, string after)>();
		var walk = WalkFrom(GetStartNode());
		foreach (WorldEdgeData edge in edges) {
			if (!walk.TryGetValue(edge.a, out int da) || !walk.TryGetValue(edge.b, out int db)) continue;
			if (edge.kind == WorldEdgeKind.LADDER) {
				WorldNodeData low = GetNode(edge.a), high = GetNode(edge.b);
				if (high.elevation <= low.elevation || db <= da)
					GD.PushWarning($"WorldMapData: ladder '{edge.a}' - '{edge.b}' should have its high end '{edge.b}' as the far end of the walk, but it does not");
				continue;
			}
			if (da <= db) continue;
			string before = edge.Id;
			(edge.a, edge.b) = (edge.b, edge.a);
			swapped.Add((before, edge.Id));
		}
		return swapped;
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
			node.loot = GetEnum(dict, "loot", WorldLoot.NONE, path);
			node.keyId = GetString(dict, "key", "");
			node.structure = GetEnum(dict, "structure", WorldStructure.NONE, path);

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

		if (root.TryGetValue("edges", out var edgesVar) && edgesVar.VariantType == Variant.Type.Array) {
			foreach (var entry in edgesVar.AsGodotArray()) {
				if (entry.VariantType != Variant.Type.Dictionary) continue;
				var dict = entry.AsGodotDictionary();
				var edge = new WorldEdgeData {
					a = GetString(dict, "a", ""),
					b = GetString(dict, "b", ""),
					kind = GetEnum(dict, "kind", WorldEdgeKind.LADDER, path),
					keyId = GetString(dict, "key", ""),
				};
				if (!map.AddEdge(edge))
					GD.PushWarning($"WorldMapData: edge '{edge.a}' - '{edge.b}' in '{path}' has a missing end or repeats another — skipping");
			}
		}

		if (root.TryGetValue("roamers", out var roamersVar) && roamersVar.VariantType == Variant.Type.Array) {
			foreach (var entry in roamersVar.AsGodotArray()) {
				if (entry.VariantType != Variant.Type.Dictionary) continue;
				var roamer = ParseRoamer(entry.AsGodotDictionary(), map, path);
				if (roamer != null) map.roamers.Add(roamer);
			}
		}

		int startCount = map.nodes.FindAll(n => n.isStart).Count;
		if (startCount == 0)
			GD.PushWarning($"WorldMapData: '{path}' has no node with \"start\": true — using first node '{map.nodes[0].id}'");
		else if (startCount > 1)
			GD.PushWarning($"WorldMapData: '{path}' has {startCount} start nodes — using '{map.GetStartNode().id}'");

		return map;
	}

	// Null (with a warning) for a roamer that names a hex the map does not have, repeats an
	// id, or has no loop to walk.
	private static WorldRoamerData ParseRoamer(Godot.Collections.Dictionary dict, WorldMapData map, string path) {
		var roamer = new WorldRoamerData {
			id = GetString(dict, "id", ""),
			name = GetString(dict, "name", ""),
			section = Mathf.Max(0, GetInt(dict, "section", 0)),
		};
		if (dict.TryGetValue("loop", out var loopVar) && loopVar.VariantType == Variant.Type.Array) {
			foreach (var hex in loopVar.AsGodotArray()) roamer.loop.Add(hex.VariantType == Variant.Type.String ? hex.AsString() : "");
		}
		if (string.IsNullOrEmpty(roamer.id) || map.GetRoamer(roamer.id) != null || map.GetNode(roamer.id) != null) {
			GD.PushWarning($"WorldMapData: roamer '{roamer.id}' in '{path}' has no id, or repeats another id — skipping");
			return null;
		}
		if (roamer.loop.Count < 2 || roamer.loop.Exists(hex => map.GetNode(hex) == null)) {
			GD.PushWarning($"WorldMapData: roamer '{roamer.id}' in '{path}' has fewer than two hexes or names one that does not exist — skipping");
			return null;
		}
		return roamer;
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
			if (node.loot != WorldLoot.NONE) dict["loot"] = node.loot.ToString();
			if (!string.IsNullOrEmpty(node.keyId)) dict["key"] = node.keyId;
			if (node.structure != WorldStructure.NONE) dict["structure"] = node.structure.ToString();
			list.Add(dict);
		}
		var root = new Godot.Collections.Dictionary { { "name", name }, { "sections", sections }, { "nodes", list } };
		if (edges.Count > 0) {
			var edgeList = new Godot.Collections.Array();
			foreach (WorldEdgeData edge in edges) {
				var dict = new Godot.Collections.Dictionary { { "a", edge.a }, { "b", edge.b }, { "kind", edge.kind.ToString() } };
				if (!string.IsNullOrEmpty(edge.keyId)) dict["key"] = edge.keyId;
				edgeList.Add(dict);
			}
			root["edges"] = edgeList;
		}
		if (roamers.Count > 0) {
			var roamerList = new Godot.Collections.Array();
			foreach (WorldRoamerData roamer in roamers) {
				var hexes = new Godot.Collections.Array();
				foreach (string hex in roamer.loop) hexes.Add(hex);
				roamerList.Add(new Godot.Collections.Dictionary {
					{ "id", roamer.id }, { "name", roamer.name }, { "section", roamer.section }, { "loop", hexes },
				});
			}
			root["roamers"] = roamerList;
		}
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
