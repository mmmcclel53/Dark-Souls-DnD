using Godot;
using System.Collections.Generic;
using System.Linq;

// Builds a campaign map of a chosen size (Matt, Oct 2026): Small 6 sections, Medium 10,
// Large 14. Each section is a region of one biome, laid out as a blob of hexes with a
// bonfire where the party enters, its encounters, and a boss on a hex of its own beyond the
// Fog Gate: the section's toughest encounter, the boss's only neighbour inside the section.
// The boss is also the only way into the next section, whose bonfire is the hex behind it,
// so the sections run as a chain and none can be skipped. Hexes that would join two
// sections any other way are simply never placed, since the party can only walk between
// hexes that exist.
//
// Bosses run mini, mini, main and repeat, and the last is always a main boss. Medium and
// Large also hide optional mega bosses (one and two) on dead ends off their sections.
// Encounter levels climb from 1 to 4 across the campaign; the Fog Gate and the boss are a
// level above the section's own. The soul budget then follows from the sections alone
// (SoulEconomy, WorldMapManager.AssignSouls).
public static class CampaignGenerator {
	public const int SMALL = 6;
	public const int MEDIUM = 10;
	public const int LARGE = 14;

	private const int ATTEMPTS = 200;
	private const int MAX_LEVEL = 4;
	private const float DECOR_CHANCE = 0.35f;
	private const float MAX_TURN = Mathf.Pi / 3f;

	private static readonly WorldTerrain[] BIOMES = {
		WorldTerrain.GRASS, WorldTerrain.SAND, WorldTerrain.MOUNTAIN,
		WorldTerrain.SNOW, WorldTerrain.CITY, WorldTerrain.VOLCANO,
	};

	// At most three sections share a biome, even in a Large campaign, so four names each.
	private static readonly Dictionary<WorldTerrain, string[]> REGIONS = new() {
		{ WorldTerrain.GRASS, new[] { "Darkroot", "Mossfell", "Hunter's Wood", "Greywillow" } },
		{ WorldTerrain.SAND, new[] { "Undead Burg", "Dustreach", "Hollow Steppe", "Sunken Road" } },
		{ WorldTerrain.MOUNTAIN, new[] { "Giants' Tomb", "Bone Ridge", "Ossuary Peaks", "Gravecrag" } },
		{ WorldTerrain.SNOW, new[] { "Ariamis", "Pale Reach", "Rimecliff", "Frozen Gallery" } },
		{ WorldTerrain.CITY, new[] { "Anor Londo", "Gilded Court", "Sunlit Spires", "Cathedral Ward" } },
		{ WorldTerrain.VOLCANO, new[] { "Iron Keep", "Cinder Forge", "Smelter Ridge", "Ash Bastion" } },
	};

	private static readonly Dictionary<WorldTerrain, int> BASE_ELEVATION = new() {
		{ WorldTerrain.GRASS, 1 }, { WorldTerrain.SAND, 1 }, { WorldTerrain.MOUNTAIN, 3 },
		{ WorldTerrain.SNOW, 2 }, { WorldTerrain.CITY, 2 }, { WorldTerrain.VOLCANO, 2 },
	};

	private static readonly string[] PLACES = {
		"Ruins", "Crossing", "Hollow", "Watch", "Steps", "Bridge", "Yard", "Shrine", "Cellar", "Ridge",
	};

	private class Section {
		public int index;
		public WorldTerrain terrain;
		public string region;
		public int level;
		public int encounters;
		public Vector2I entry;
		public Vector2I gate;
		public Vector2I boss;
		public List<Vector2I> hexes = new List<Vector2I>();
	}

	public static string SizeName(int sections) => sections switch {
		SMALL => "Small",
		MEDIUM => "Medium",
		LARGE => "Large",
		_ => $"{sections}-Section",
	};

	public static WorldMapData Generate(int sections, RandomNumberGenerator rng) {
		for (int attempt = 0; attempt < ATTEMPTS; attempt++) {
			WorldMapData map = TryGenerate(sections, rng);
			if (map != null) return map;
		}
		GD.PushError($"CampaignGenerator: could not lay out {sections} sections in {ATTEMPTS} attempts");
		return null;
	}

	// Null when the chain grows into a dead end, and the caller tries again.
	private static WorldMapData TryGenerate(int sections, RandomNumberGenerator rng) {
		// Every walkable hex, with the section it belongs to.
		var owner = new Dictionary<Vector2I, int>();
		var chain = new List<Section>();
		List<WorldTerrain> biomes = BiomeOrder(sections, rng);
		var names = new Dictionary<WorldTerrain, Queue<string>>();
		foreach (var (terrain, pool) in REGIONS) names[terrain] = new Queue<string>(Shuffled(pool, rng));

		Vector2 dir = Vector2.FromAngle(rng.RandfRange(0, Mathf.Tau));
		Vector2I entry = Vector2I.Zero;
		for (int s = 1; s <= sections; s++) {
			var section = new Section {
				index = s,
				terrain = biomes[s - 1],
				level = 1 + (MAX_LEVEL * (s - 1)) / sections,
				encounters = 5 + (s > sections / 2 ? 1 : 0),
				entry = entry,
			};
			section.region = names[section.terrain].Dequeue();
			owner[entry] = s;
			section.hexes.Add(entry);

			if (!Grow(owner, section, section.encounters * 2 + 4, dir, rng)) return null;
			bool last = s == sections;
			if (!PlaceBoss(owner, section, dir, last, rng)) return null;
			if (!last) entry = Exit(owner, section.boss, dir, rng);
			chain.Add(section);
			dir = dir.Rotated(rng.RandfRange(-MAX_TURN, MAX_TURN));
		}

		var megas = PlaceMegas(owner, chain, rng);
		return Build(sections, owner, chain, megas, rng);
	}

	// Each pass through the six biomes is shuffled, and never repeats the last one back to back.
	private static List<WorldTerrain> BiomeOrder(int sections, RandomNumberGenerator rng) {
		var order = new List<WorldTerrain>();
		while (order.Count < sections) {
			List<WorldTerrain> pass = Shuffled(BIOMES, rng);
			if (order.Count > 0 && pass[0] == order[^1]) (pass[0], pass[1]) = (pass[1], pass[0]);
			order.AddRange(pass);
		}
		return order.GetRange(0, sections);
	}

	// Grows the section hex by hex from its entry, preferring hexes with more of the section
	// around them (so it stays compact) and lying ahead along the chain's direction. A hex may
	// only touch its own section, which is what keeps the sections apart.
	private static bool Grow(Dictionary<Vector2I, int> owner, Section section, int size, Vector2 dir, RandomNumberGenerator rng) {
		while (section.hexes.Count < size) {
			var candidates = new List<Vector2I>();
			var weights = new List<float>();
			foreach (Vector2I hex in section.hexes) {
				foreach (Vector2I next in Neighbours(hex)) {
					if (owner.ContainsKey(next) || candidates.Contains(next)) continue;
					if (Neighbours(next).Any(n => owner.TryGetValue(n, out int o) && o != section.index)) continue;
					int same = Neighbours(next).Count(n => owner.TryGetValue(n, out int o) && o == section.index);
					float ahead = (Pixel(next) - Pixel(section.entry)).Normalized().Dot(dir);
					candidates.Add(next);
					weights.Add(same * same * (1.5f + ahead));
				}
			}
			if (candidates.Count == 0) return false;
			Vector2I chosen = candidates[(int)rng.RandWeighted(weights.ToArray())];
			owner[chosen] = section.index;
			section.hexes.Add(chosen);
		}
		return true;
	}

	// The boss stands just outside the section, touching exactly one of its hexes (the Fog
	// Gate) and nothing else, as far ahead as it can. Unless it is the last, it also needs a
	// free hex behind it for the next section's bonfire.
	private static bool PlaceBoss(Dictionary<Vector2I, int> owner, Section section, Vector2 dir, bool last, RandomNumberGenerator rng) {
		Vector2I best = default;
		float bestScore = float.MinValue;
		var seen = new HashSet<Vector2I>();
		foreach (Vector2I hex in section.hexes) {
			foreach (Vector2I boss in Neighbours(hex)) {
				if (owner.ContainsKey(boss) || !seen.Add(boss)) continue;
				var touching = Neighbours(boss).Where(owner.ContainsKey).ToList();
				if (touching.Count != 1 || touching[0] == section.entry) continue;
				if (!last && !HasExit(owner, boss)) continue;

				float score = (Pixel(boss) - Pixel(section.entry)).Dot(dir) + Distance(boss, section.entry) + rng.Randf();
				if (score > bestScore) {
					bestScore = score;
					best = boss;
				}
			}
		}
		if (bestScore == float.MinValue) return false;

		section.boss = best;
		section.gate = Neighbours(best).First(owner.ContainsKey);
		owner[best] = section.index;
		return true;
	}

	private static bool HasExit(Dictionary<Vector2I, int> owner, Vector2I boss) =>
		Neighbours(boss).Any(n => IsExit(owner, boss, n));

	// A free hex behind the boss that touches nothing else.
	private static bool IsExit(Dictionary<Vector2I, int> owner, Vector2I boss, Vector2I hex) =>
		!owner.ContainsKey(hex) && Neighbours(hex).All(n => n == boss || !owner.ContainsKey(n));

	private static Vector2I Exit(Dictionary<Vector2I, int> owner, Vector2I boss, Vector2 dir, RandomNumberGenerator rng) {
		var exits = Neighbours(boss).Where(n => IsExit(owner, boss, n)).ToList();
		return exits.OrderByDescending(n => (Pixel(n) - Pixel(boss)).Dot(dir) + rng.Randf() * 0.5f).First();
	}

	// Optional mega bosses: one in a Medium campaign, two in a Large, around the middle of the
	// chain, each on a dead end touching one ordinary hex of its section.
	private static Dictionary<Vector2I, Section> PlaceMegas(Dictionary<Vector2I, int> owner, List<Section> chain, RandomNumberGenerator rng) {
		var megas = new Dictionary<Vector2I, Section>();
		int count = chain.Count >= LARGE ? 2 : chain.Count >= MEDIUM ? 1 : 0;
		for (int i = 1; i <= count; i++) {
			Section section = chain[Mathf.RoundToInt(chain.Count * i / (float)(count + 1f)) - 1];
			var spots = new List<Vector2I>();
			foreach (Vector2I hex in section.hexes) {
				if (hex == section.entry || hex == section.gate) continue;
				foreach (Vector2I spot in Neighbours(hex)) {
					if (owner.ContainsKey(spot) || spots.Contains(spot)) continue;
					if (Neighbours(spot).Count(owner.ContainsKey) == 1) spots.Add(spot);
				}
			}
			if (spots.Count == 0) {
				GD.PushWarning($"CampaignGenerator: no room for a mega boss in section {section.index}");
				continue;
			}
			Vector2I mega = spots.OrderByDescending(s => Distance(s, section.entry) + rng.Randf()).First();
			owner[mega] = section.index;
			megas[mega] = section;
		}
		return megas;
	}

	private static WorldMapData Build(int sections, Dictionary<Vector2I, int> owner, List<Section> chain,
		Dictionary<Vector2I, Section> megas, RandomNumberGenerator rng) {
		var map = new WorldMapData { name = $"{SizeName(sections)} Campaign", sections = sections };
		Dictionary<Vector2I, int> elevation = Elevations(owner, chain, rng);

		foreach (Section section in chain) {
			var places = new Queue<string>(Shuffled(PLACES, rng));
			var encounters = SpreadEncounters(section, rng);
			int path = 0;
			foreach (Vector2I hex in section.hexes) {
				var node = NodeAt(hex, section, elevation);
				if (hex == section.entry) {
					node.id = $"s{section.index}-bonfire";
					node.displayName = $"{section.region} Bonfire";
					node.encounterType = WorldEncounterType.BONFIRE;
					node.isStart = section.index == 1;
				} else if (hex == section.gate) {
					node.id = $"s{section.index}-gate";
					node.displayName = $"{section.region} Fog Gate";
					node.encounterType = WorldEncounterType.ENCOUNTER;
					node.level = Mathf.Min(MAX_LEVEL, section.level + 1);
				} else if (encounters.Contains(hex)) {
					node.id = $"s{section.index}-e{encounters.IndexOf(hex) + 1}";
					node.displayName = $"{section.region} {places.Dequeue()}";
					node.encounterType = WorldEncounterType.ENCOUNTER;
					node.level = section.level;
				} else {
					node.id = $"s{section.index}-p{++path}";
					node.displayName = section.region;
				}
				map.Add(node);
			}

			var boss = NodeAt(section.boss, section, elevation);
			boss.id = $"s{section.index}-boss";
			boss.displayName = $"{section.region} Lair";
			boss.encounterType = WorldEncounterType.BOSS;
			boss.boss = section.index == sections || section.index % 3 == 0 ? WorldBossKind.MAIN : WorldBossKind.MINI;
			boss.level = Mathf.Min(MAX_LEVEL, section.level + 1);
			map.Add(boss);
		}

		foreach (var (hex, section) in megas) {
			var mega = NodeAt(hex, section, elevation);
			mega.id = $"s{section.index}-mega";
			mega.displayName = $"{section.region} Abyss";
			mega.encounterType = WorldEncounterType.BOSS;
			mega.boss = WorldBossKind.MEGA;
			mega.level = MAX_LEVEL;
			map.Add(mega);
		}

		AddDecor(map, owner, chain, rng);
		return map;
	}

	private static WorldNodeData NodeAt(Vector2I hex, Section section, Dictionary<Vector2I, int> elevation) =>
		new WorldNodeData {
			q = hex.X, r = hex.Y,
			terrain = section.terrain,
			section = section.index,
			elevation = elevation[hex],
		};

	// The Fog Gate is one encounter; the rest are spread out over the section, each as far as
	// it can be from the bonfire, the gate and those already placed (picked among the three
	// farthest, so no two maps come out the same).
	private static List<Vector2I> SpreadEncounters(Section section, RandomNumberGenerator rng) {
		var placed = new List<Vector2I> { section.entry, section.gate };
		var free = section.hexes.Where(h => h != section.entry && h != section.gate).ToList();
		var chosen = new List<Vector2I>();
		while (chosen.Count < section.encounters - 1 && free.Count > 0) {
			var ranked = free.OrderByDescending(h => placed.Min(p => Distance(h, p))).Take(3).ToList();
			Vector2I pick = ranked[rng.RandiRange(0, ranked.Count - 1)];
			chosen.Add(pick);
			placed.Add(pick);
			free.Remove(pick);
		}
		return chosen;
	}

	// Each hex starts at its biome's height, give or take a step, then is lowered wherever it
	// stands more than one step above a walkable neighbour, so every step can be climbed.
	private static Dictionary<Vector2I, int> Elevations(Dictionary<Vector2I, int> owner, List<Section> chain, RandomNumberGenerator rng) {
		var elevation = new Dictionary<Vector2I, int>();
		foreach (var (hex, s) in owner) elevation[hex] = BASE_ELEVATION[chain[s - 1].terrain] + rng.RandiRange(0, 1);

		bool changed = true;
		while (changed) {
			changed = false;
			foreach (Vector2I hex in owner.Keys) {
				foreach (Vector2I n in Neighbours(hex)) {
					if (!elevation.TryGetValue(n, out int other) || elevation[hex] <= other + 1) continue;
					elevation[hex] = other + 1;
					changed = true;
				}
			}
		}
		return elevation;
	}

	// Water (lava beside a volcano) on some of the empty hexes around the chain, low and
	// impassable, so the map reads as land rather than a path in a void.
	private static void AddDecor(WorldMapData map, Dictionary<Vector2I, int> owner, List<Section> chain, RandomNumberGenerator rng) {
		var done = new HashSet<Vector2I>();
		int count = 0;
		foreach (Vector2I hex in owner.Keys) {
			foreach (Vector2I spot in Neighbours(hex)) {
				if (owner.ContainsKey(spot) || !done.Add(spot) || rng.Randf() >= DECOR_CHANCE) continue;
				bool volcanic = chain[owner[hex] - 1].terrain == WorldTerrain.VOLCANO;
				map.Add(new WorldNodeData {
					id = $"d{++count}",
					displayName = volcanic ? "Lava" : "Deep Water",
					q = spot.X, r = spot.Y,
					terrain = volcanic ? WorldTerrain.LAVA : WorldTerrain.WATER,
					elevation = 0,
				});
			}
		}
	}

	private static IEnumerable<Vector2I> Neighbours(Vector2I hex) {
		foreach (Vector2I off in WorldMapData.NEIGHBOR_OFFSETS) yield return hex + off;
	}

	private static int Distance(Vector2I a, Vector2I b) {
		int dq = a.X - b.X, dr = a.Y - b.Y;
		return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(dq + dr)) / 2;
	}

	// The same pointy-top layout the world map draws with, in hex widths.
	private static Vector2 Pixel(Vector2I hex) => new Vector2(Mathf.Sqrt(3f) * (hex.X + hex.Y * 0.5f), 1.5f * hex.Y);

	private static List<T> Shuffled<T>(IEnumerable<T> items, RandomNumberGenerator rng) {
		var list = items.ToList();
		for (int i = list.Count - 1; i > 0; i--) {
			int j = rng.RandiRange(0, i);
			(list[i], list[j]) = (list[j], list[i]);
		}
		return list;
	}
}
