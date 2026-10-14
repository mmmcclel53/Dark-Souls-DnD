using Godot;
using System.Collections.Generic;
using System.Linq;

// Gives a generated campaign its roaming threats (Phase 4 of the World Design Plan): from
// section 2 on, one patrol per section, two in the last third of a Large campaign. A patrol is
// a closed loop of 4 to 8 plain hexes of its section, each step (the last back to the first
// included) one CanStep with every edge closed, so it holds however the party opens the map. A
// section with no such cycle gets a path of 3 to 5 hexes walked there and back (A B C B), and
// failing that a pair. Loops are drawn to the Fog Gate, where a patrol is hardest to avoid.
public static class RoamerGenerator {
	private const int MIN_CYCLE = 4;
	private const int MAX_CYCLE = 8;
	private const int MIN_PATH = 3;
	private const int MAX_PATH = 5;
	private const int VISIT_BUDGET = 30000;

	public static void Place(WorldMapData map, RandomNumberGenerator rng) {
		map.roamers.Clear();
		for (int section = 2; section <= map.sections; section++) {
			int count = map.sections >= CampaignGenerator.LARGE && section > map.sections * 2 / 3 ? 2 : 1;
			var used = new HashSet<string>();
			for (int k = 1; k <= count; k++) {
				List<string> loop = FindLoop(map, section, used, rng);
				if (loop == null) {
					GD.PushWarning($"RoamerGenerator: no room for a roamer in section {section}");
					continue;
				}
				used.UnionWith(loop);
				string region = map.GetNode(loop[0]).displayName;
				map.roamers.Add(new WorldRoamerData {
					id = $"s{section}-r{k}",
					name = k == 1 ? $"{region} Patrol" : $"{region} Patrol II",
					section = section,
					loop = loop,
				});
			}
		}
	}

	// The loop rule: two or more plain hexes of one section, each step, the last back to the
	// first, a legal step with every edge closed.
	public static bool IsValid(WorldMapData map, WorldRoamerData roamer) {
		if (roamer.loop.Count < 2) return false;
		for (int i = 0; i < roamer.loop.Count; i++) {
			WorldNodeData from = map.GetNode(roamer.loop[i]);
			WorldNodeData to = map.GetNode(roamer.loop[(i + 1) % roamer.loop.Count]);
			if (from == null || to == null || !from.IsPlain || from.section != roamer.section) return false;
			if (!map.CanStep(from, to, _ => false)) return false;
		}
		return true;
	}

	private static List<string> FindLoop(WorldMapData map, int section, HashSet<string> used, RandomNumberGenerator rng) {
		var plain = map.nodes.Where(n => n.section == section && n.IsPlain).ToList();
		var steps = new Dictionary<string, List<WorldNodeData>>();
		foreach (WorldNodeData from in plain) {
			steps[from.id] = plain.Where(to => map.CanStep(from, to, _ => false)).ToList();
		}
		WorldNodeData gate = map.GetNode($"s{section}-gate");

		var cycles = Search(plain, steps, MIN_CYCLE, MAX_CYCLE, true);
		if (cycles.Count > 0) return Best(map, cycles, gate, used, rng);

		var paths = Search(plain, steps, MIN_PATH, MAX_PATH, false)
			.Select(path => path.Concat(path.Skip(1).Take(path.Count - 2).Reverse()).ToList())
			.ToList();
		if (paths.Count > 0) return Best(map, paths, gate, used, rng);

		var pairs = Search(plain, steps, 2, 2, false);
		return pairs.Count > 0 ? Best(map, pairs, gate, used, rng) : null;
	}

	// Simple paths of min to max hexes: for a cycle, ones whose last hex steps back to the
	// first (each cycle found once per direction, from its first hex in map order); for a path,
	// ones every step of which can be taken both ways, so it can be walked there and back.
	private static List<List<string>> Search(List<WorldNodeData> plain, Dictionary<string, List<WorldNodeData>> steps,
		int min, int max, bool cycle) {
		var found = new List<List<string>>();
		var index = new Dictionary<string, int>();
		for (int i = 0; i < plain.Count; i++) index[plain[i].id] = i;
		int visits = 0;

		for (int start = 0; start < plain.Count; start++) {
			var path = new List<WorldNodeData> { plain[start] };
			Walk(start);

			void Walk(int first) {
				if (++visits > VISIT_BUDGET) return;
				WorldNodeData here = path[^1];
				if (path.Count >= min && !cycle && path.Count <= max && index[path[0].id] < index[here.id]) {
					found.Add(path.Select(n => n.id).ToList());
				}
				if (path.Count >= max) return;
				foreach (WorldNodeData next in steps[here.id]) {
					if (cycle && next == path[0] && path.Count >= min) {
						found.Add(path.Select(n => n.id).ToList());
						continue;
					}
					if (path.Contains(next) || (cycle && index[next.id] < first)) continue;
					if (!cycle && !steps[next.id].Contains(here)) continue;
					path.Add(next);
					Walk(first);
					path.RemoveAt(path.Count - 1);
				}
			}
		}
		return found;
	}

	// Nearest to the gate, a little shaken, and keeping off hexes another roamer of the
	// section already walks.
	private static List<string> Best(WorldMapData map, List<List<string>> candidates, WorldNodeData gate,
		HashSet<string> used, RandomNumberGenerator rng) {
		List<string> best = null;
		float bestScore = float.MinValue;
		foreach (List<string> loop in candidates) {
			var distances = gate == null
				? new List<int> { 0 }
				: loop.Select(id => WorldMapData.Distance(map.GetNode(id), gate)).ToList();
			float score = -distances.Min() - 0.25f * (float)distances.Average() + rng.Randf() - 100f * loop.Count(used.Contains);
			if (score > bestScore) {
				bestScore = score;
				best = loop;
			}
		}
		return best;
	}
}
