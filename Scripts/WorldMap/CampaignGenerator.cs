using Godot;
using System.Collections.Generic;
using System.Linq;

// Builds a campaign map of a chosen size (Matt, Oct 2026): Small 6 sections, Medium 10,
// Large 14. Each section is a region of one biome, laid out as a blob of hexes with a
// bonfire where the party enters, its encounters, and a boss on a hex of its own beyond the
// Fog Gate: the section's toughest encounter, the boss's only neighbour inside the section.
// Each section is carved first (SectionCarver): chasms and ridges wind the walk from its
// bonfire to its Fog Gate, and a ladder, a tree or a door cuts it short. The boss is also the
// only way into the next section, whose bonfire is the hex behind it, so the sections run as
// a chain and none can be skipped. Hexes that would join two sections any other way are
// simply never placed, since the party can only walk between hexes that exist.
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
	private const int SHAPE_RETRIES = 8;
	private const float WALK_WEIGHT = 0.5f;
	private const float SPOT_NOISE = 0.15f;
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
		public SectionTerrain shape;
		public string keyName = "";
		public List<Vector2I> land;
		public Dictionary<Vector2I, bool> exits = new Dictionary<Vector2I, bool>();
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

		var elevation = new Dictionary<Vector2I, int>();
		Vector2 dir = Vector2.FromAngle(rng.RandfRange(0, Mathf.Tau));
		Vector2I entry = Vector2I.Zero;
		int anchor = BASE_ELEVATION[biomes[0]] + rng.RandiRange(0, 1);
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

			if (!Grow(owner, section, section.encounters * 3 + 4, dir, rng)) return null;
			bool last = s == sections;
			if (!Shape(owner, section, anchor, dir, last, sections, rng)) return null;
			owner[section.boss] = s;
			foreach (Vector2I hex in section.hexes) elevation[hex] = section.shape.ElevationAt(hex);

			int gateHeight = elevation[section.gate];
			if (last) {
				elevation[section.boss] = gateHeight;
			} else {
				int raw = BASE_ELEVATION[biomes[s]] + rng.RandiRange(0, 1);
				anchor = Mathf.Clamp(raw, gateHeight - 2, gateHeight + 2);
				elevation[section.boss] = gateHeight + Mathf.Clamp(anchor - gateHeight, -1, 1);
				entry = Exit(owner, section.boss, dir, rng);
			}
			chain.Add(section);
			dir = dir.Rotated(rng.RandfRange(-MAX_TURN, MAX_TURN));
		}

		var megas = PlaceMegas(owner, chain, elevation, rng);
		return Build(sections, owner, chain, megas, elevation, rng);
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

	// Shapes a section once it has grown: heights, then chasms and ridges until the walk to the
	// gate is long, the boss and gate, and the edges that cut the walk short. Where no edge is
	// worth placing, one more feature is carved and the edges looked for again, and halfway
	// through the retries the section is cleared and carved afresh.
	private static bool Shape(Dictionary<Vector2I, int> owner, Section section, int anchor, Vector2 dir, bool last, int sections, RandomNumberGenerator rng) {
		var t = new SectionTerrain(section.hexes, section.entry);
		t.SetElevations(anchor, BASE_ELEVATION[section.terrain], rng);
		section.shape = t;
		int wanted = section.index > sections / 2 ? 2 : 1;
		t.Reset();
		for (int retry = 0; retry < SHAPE_RETRIES; retry++) {
			if (retry == SHAPE_RETRIES / 2) t.Reset();
			int forced = retry == 0 || retry == SHAPE_RETRIES / 2 ? SectionCarver.FIRST_FEATURES : 1;
			SectionCarver.Carve(t, section.encounters + 3, forced, walk => BestSpot(owner, section, t, dir, last, walk, null)?.gate, rng);
			var walked = t.Walk(section.entry);
			(Vector2I boss, Vector2I gate)? spot = BestSpot(owner, section, t, dir, last, walked, rng);
			if (spot != null && t.Dist(walked, spot.Value.gate) < SectionCarver.MIN_WALK && retry < SHAPE_RETRIES - 1) continue;
			int minSaving = retry >= SHAPE_RETRIES - 2 ? 1 : SectionCarver.MIN_SAVING;
			bool placed = spot != null && SectionCarver.PlaceEdges(t, spot.Value.gate, wanted, minSaving, rng);
			if (!placed) continue;

			section.boss = spot.Value.boss;
			section.gate = spot.Value.gate;
			section.land = t.Land.ToList();
			KeyADoor(section, rng);
			return true;
		}
		return false;
	}

	// The boss stands just outside the section, touching exactly one of its walkable hexes (the
	// Fog Gate) and nothing else, as far ahead and as far to walk as it can. Unless it is the
	// last, it also needs a free hex behind it for the next section's bonfire. With no rng the
	// choice is plain (for the carver to measure by); with one, it is shaken a little.
	private static (Vector2I boss, Vector2I gate)? BestSpot(Dictionary<Vector2I, int> owner, Section section, SectionTerrain t,
		Vector2 dir, bool last, int[] walk, RandomNumberGenerator rng) {
		(Vector2I boss, Vector2I gate)? best = null;
		float bestScore = float.MinValue;
		var seen = new HashSet<Vector2I>();
		foreach (Vector2I hex in t.hexes) {
			if (hex == section.entry || !t.Walkable(hex) || t.ridges.Contains(hex) || !t.Reached(walk, hex)) continue;
			foreach (Vector2I boss in Neighbours(hex)) {
				if (owner.ContainsKey(boss) || !seen.Add(boss)) continue;
				int touching = 0;
				Vector2I only = default;
				foreach (Vector2I n in Neighbours(boss)) {
					if (!owner.ContainsKey(n) || t.chasms.Contains(n)) continue;
					touching++;
					only = n;
				}
				if (touching != 1 || only != hex) continue;
				if (!last && !HasExit(owner, section, boss)) continue;

				float score = (Pixel(boss) - Pixel(section.entry)).Dot(dir) + Distance(boss, section.entry) + WALK_WEIGHT * t.Dist(walk, hex)
					+ (rng?.Randf() * SPOT_NOISE ?? 0f);
				if (score > bestScore) {
					bestScore = score;
					best = (boss, hex);
				}
			}
		}
		return best;
	}

	// In the back of the campaign a door may need a key, found on one of the section's own
	// encounters (PlaceLoot); at most one a section.
	private static void KeyADoor(Section section, RandomNumberGenerator rng) {
		if (section.index < 3) return;
		foreach (HexEdge edge in section.shape.edges) {
			if (edge.kind != WorldEdgeKind.DOOR) continue;
			if (rng.RandiRange(0, 2) == 0) {
				edge.keyed = true;
				section.keyName = $"{section.region} Key";
			}
			return;
		}
	}

	private static bool HasExit(Dictionary<Vector2I, int> owner, Section section, Vector2I boss) {
		if (!section.exits.TryGetValue(boss, out bool found))
			section.exits[boss] = found = Neighbours(boss).Any(n => IsExit(owner, boss, n));
		return found;
	}

	// A free hex behind the boss that touches nothing else.
	private static bool IsExit(Dictionary<Vector2I, int> owner, Vector2I boss, Vector2I hex) =>
		!owner.ContainsKey(hex) && Neighbours(hex).All(n => n == boss || !owner.ContainsKey(n));

	private static Vector2I Exit(Dictionary<Vector2I, int> owner, Vector2I boss, Vector2 dir, RandomNumberGenerator rng) {
		var exits = Neighbours(boss).Where(n => IsExit(owner, boss, n)).ToList();
		return exits.OrderByDescending(n => (Pixel(n) - Pixel(boss)).Dot(dir) + rng.Randf() * 0.5f).First();
	}

	// Optional mega bosses: one in a Medium campaign, two in a Large, around the middle of the
	// chain, each on a dead end touching one ordinary hex of its section.
	private static Dictionary<Vector2I, Section> PlaceMegas(Dictionary<Vector2I, int> owner, List<Section> chain,
		Dictionary<Vector2I, int> elevation, RandomNumberGenerator rng) {
		var megas = new Dictionary<Vector2I, Section>();
		int count = chain.Count >= LARGE ? 2 : chain.Count >= MEDIUM ? 1 : 0;
		for (int i = 1; i <= count; i++) {
			Section section = chain[Mathf.RoundToInt(chain.Count * i / (float)(count + 1f)) - 1];
			var spots = new List<Vector2I>();
			foreach (Vector2I hex in section.land) {
				if (hex == section.entry || hex == section.gate || section.shape.ridges.Contains(hex)) continue;
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
			elevation[mega] = elevation[Neighbours(mega).First(owner.ContainsKey)];
		}
		return megas;
	}

	private static WorldMapData Build(int sections, Dictionary<Vector2I, int> owner, List<Section> chain,
		Dictionary<Vector2I, Section> megas, Dictionary<Vector2I, int> elevation, RandomNumberGenerator rng) {
		var map = new WorldMapData { name = $"{SizeName(sections)} Campaign", sections = sections };

		foreach (Section section in chain) {
			var places = new Queue<string>(Shuffled(PLACES, rng));
			var encounters = SpreadEncounters(section, rng);
			var loot = PlaceLoot(section, encounters, sections, rng);
			int path = 0, chasm = 0;
			var ids = new Dictionary<Vector2I, string>();
			foreach (Vector2I hex in section.hexes) {
				var node = NodeAt(hex, section, elevation);
				if (section.shape.chasms.Contains(hex)) {
					bool lava = section.terrain == WorldTerrain.VOLCANO;
					node.id = $"s{section.index}-c{++chasm}";
					node.displayName = lava ? "Lava Chasm" : "Chasm";
					node.terrain = lava ? WorldTerrain.LAVA : WorldTerrain.WATER;
					ids[hex] = node.id;
					map.Add(node);
					continue;
				}
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
				ids[hex] = node.id;
				if (loot.TryGetValue(hex, out WorldLoot kind)) {
					node.loot = kind;
					if (kind == WorldLoot.KEY) node.keyId = section.keyName;
					if (node.encounterType == WorldEncounterType.ENCOUNTER) node.level = Mathf.Min(MAX_LEVEL, section.level + 1);
				}
				map.Add(node);
			}

			foreach (HexEdge edge in section.shape.edges) {
				map.AddEdge(new WorldEdgeData {
					a = ids[edge.a], b = ids[edge.b], kind = edge.kind,
					keyId = edge.keyed ? section.keyName : "",
				});
			}

			var boss = NodeAt(section.boss, section, elevation);
			boss.id = $"s{section.index}-boss";
			boss.displayName = $"{section.region} Lair";
			boss.encounterType = WorldEncounterType.BOSS;
			boss.boss = section.index == sections || section.index % 3 == 0 ? WorldBossKind.MAIN : WorldBossKind.MINI;
			boss.level = Mathf.Min(MAX_LEVEL, section.level + 1);
			map.Add(boss);
		}

		int gate = 0;
		foreach (var (hex, section) in megas) {
			var mega = NodeAt(hex, section, elevation);
			mega.id = $"s{section.index}-mega";
			mega.displayName = $"{section.region} Abyss";
			mega.encounterType = WorldEncounterType.BOSS;
			mega.boss = WorldBossKind.MEGA;
			mega.level = MAX_LEVEL;
			map.Add(mega);
			LockMega(map, mega, hex, owner, MEGA_KEYS[gate++ % MEGA_KEYS.Length]);
		}

		AddDecor(map, owner, chain, rng);
		PlaceStructures(map, chain, rng);
		RoamerGenerator.Place(map, rng);
		return map;
	}

	// Mega boss gates (Matt, Oct 2026): a mega is optional, so the one door into its dead end is
	// locked, and the key is guarded by the boss of the section before (taken on that win, as
	// any guarded loot is). The keys are boss cards; a Large campaign has two megas, so the
	// Soldier Key waits for a third.
	private static readonly string[] MEGA_KEYS = { "Big Pilgrim's Key", "Blighttown Key", "Soldier Key" };

	private static void LockMega(WorldMapData map, WorldNodeData mega, Vector2I hex, Dictionary<Vector2I, int> owner, string key) {
		Vector2I outside = Neighbours(hex).First(owner.ContainsKey);
		WorldNodeData door = map.nodes.FirstOrDefault(n => n.q == outside.X && n.r == outside.Y);
		WorldNodeData guard = map.GetNode($"s{Mathf.Max(1, mega.section - 1)}-boss");
		if (door == null || guard == null) return;
		// b is the far side, inside, so it can only ever be opened with the key.
		map.AddEdge(new WorldEdgeData { a = door.id, b = mega.id, kind = WorldEdgeKind.DOOR, keyId = key });
		guard.loot = WorldLoot.KEY;
		guard.keyId = key;
	}

	private const float RUINS_CHANCE = 0.12f;
	private const float SLUM_CHANCE = 0.25f;
	private const float SLUM_RUINS_CHANCE = 0.08f;

	private static WorldStructure BossStructure(WorldNodeData boss) {
		if (boss.boss == WorldBossKind.MINI) return WorldStructure.TOWER;
		if (boss.boss == WorldBossKind.MEGA) return WorldStructure.CRYPT;
		return boss.terrain switch {
			WorldTerrain.SAND => WorldStructure.LIBRARY,
			WorldTerrain.MOUNTAIN or WorldTerrain.CITY or WorldTerrain.VOLCANO => WorldStructure.CASTLE,
			_ => WorldStructure.CATHEDRAL,
		};
	}

	// Per section, after loot, edges and decor: the boss's seat (by weight and biome), a gatehouse
	// tower at the Fog Gate, ruins at the bonfire, a castle behind every door, then on plain
	// hexes a lookout tower (not in section 1), from section 3 a crypt, an aqueduct beside
	// water, lava or a chasm, and ruins (hovels in a city) scattered over the rest. Encounter
	// hexes other than the gate stay bare, unless a door leads into them.
	private static void PlaceStructures(WorldMapData map, List<Section> chain, RandomNumberGenerator rng) {
		foreach (Section section in chain) {
			var nodes = map.nodes.Where(n => n.section == section.index).ToList();
			foreach (WorldNodeData boss in nodes.Where(n => n.encounterType == WorldEncounterType.BOSS)) boss.structure = BossStructure(boss);
			map.GetNode($"s{section.index}-gate").structure = WorldStructure.TOWER;
			map.GetNode($"s{section.index}-bonfire").structure = WorldStructure.RUINS;

			foreach (WorldEdgeData edge in map.edges) {
				WorldNodeData inside = map.GetNode(edge.b);
				if (edge.kind != WorldEdgeKind.DOOR || inside.section != section.index || inside.structure != WorldStructure.NONE) continue;
				inside.structure = inside.encounterType == WorldEncounterType.ENCOUNTER ? WorldStructure.RUINS : WorldStructure.CASTLE;
			}

			var bare = nodes.Where(n => n.IsPlain && n.structure == WorldStructure.NONE).ToList();
			WorldNodeData tower = null;
			if (section.index > 1 && bare.Count > 0) {
				Vector2I entry = section.entry;
				tower = bare.OrderByDescending(n => n.elevation)
					.ThenByDescending(n => Distance(new Vector2I(n.q, n.r), entry) + rng.Randf() * 0.9f).First();
				tower.structure = WorldStructure.TOWER;
				bare.Remove(tower);
			}
			if (section.index >= 3) {
				var spots = bare.Where(n => tower == null || !map.AreNeighbors(n, tower)).ToList();
				if (spots.Count > 0) Claim(spots[rng.RandiRange(0, spots.Count - 1)], WorldStructure.CRYPT, bare);
			}
			var wet = bare.Where(n => map.GetNeighbors(n).Any(m => m.IsImpassable)).ToList();
			if (wet.Count > 0) Claim(wet[rng.RandiRange(0, wet.Count - 1)], WorldStructure.AQUEDUCT, bare);

			bool city = section.terrain == WorldTerrain.CITY;
			foreach (WorldNodeData node in bare.ToList()) {
				float roll = rng.Randf();
				if (city && roll < SLUM_CHANCE) node.structure = WorldStructure.SLUM;
				else if (roll < (city ? SLUM_CHANCE + SLUM_RUINS_CHANCE : RUINS_CHANCE)) node.structure = WorldStructure.RUINS;
			}
		}
	}

	private static void Claim(WorldNodeData node, WorldStructure kind, List<WorldNodeData> bare) {
		node.structure = kind;
		bare.Remove(node);
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
		var free = section.land.Where(h => h != section.entry && h != section.gate).ToList();
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

	// Per section (Phase 2b): an Estus Shard guarding the encounter farthest from the bonfire, an
	// unguarded treasure and a pile of souls on plain hexes (and a Homeward Bone on one more in
	// every BONE_EVERY-th section), and in the back half a treasure
	// guarded by another encounter. A guarded encounter is a level above its section. Never on
	// the bonfire, Fog Gate, boss or mega hexes. A keyed door's key lies on one more encounter
	// that carries nothing yet; the section can always reach it without the door.
	// A Homeward Bone lies in every second section (sections 2, 4, ...); Claude's call, Oct 2026.
	private const int BONE_EVERY = 2;

	private static Dictionary<Vector2I, WorldLoot> PlaceLoot(Section section, List<Vector2I> encounters, int sections, RandomNumberGenerator rng) {
		var loot = new Dictionary<Vector2I, WorldLoot>();
		var guards = encounters.OrderByDescending(h => Distance(h, section.entry) + rng.Randf() * 0.5f).ToList();
		if (guards.Count > 0) loot[guards[0]] = WorldLoot.ESTUS_SHARD;
		if (section.index > sections / 2 && guards.Count > 1) loot[guards[rng.RandiRange(1, guards.Count - 1)]] = WorldLoot.TREASURE;

		if (section.keyName != "") {
			var free = guards.Where(h => !loot.ContainsKey(h)).ToList();
			if (free.Count > 0) {
				loot[free[rng.RandiRange(0, free.Count - 1)]] = WorldLoot.KEY;
			} else {
				section.keyName = "";
				foreach (HexEdge edge in section.shape.edges) edge.keyed = false;
			}
		}

		var plain = section.land.Where(h => h != section.entry && h != section.gate && !encounters.Contains(h)).ToList();
		var anchors = new List<Vector2I> { section.entry, section.gate };
		var onPlain = new List<WorldLoot> { WorldLoot.TREASURE, WorldLoot.SOULS };
		if (section.index % BONE_EVERY == 0) onPlain.Add(WorldLoot.HOMEWARD_BONE);
		foreach (WorldLoot kind in onPlain) {
			if (plain.Count == 0) break;
			var ranked = plain.OrderByDescending(h => anchors.Min(a => Distance(h, a))).Take(3).ToList();
			Vector2I spot = ranked[rng.RandiRange(0, ranked.Count - 1)];
			loot[spot] = kind;
			anchors.Add(spot);
			plain.Remove(spot);
		}
		return loot;
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

	internal static IEnumerable<Vector2I> Neighbours(Vector2I hex) {
		foreach (Vector2I off in WorldMapData.NEIGHBOR_OFFSETS) yield return hex + off;
	}

	internal static int Distance(Vector2I a, Vector2I b) {
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
