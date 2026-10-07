using Godot;
using System.Collections.Generic;
using System.Linq;

// Rolls the fixed encounters of a campaign map (Matt, Oct 2026). An encounter's level is a
// band of total enemy health, counted at each enemy's tier; its enemies all come from the
// family its terrain names, 1 to 6 of them and at most 2 of any one card.
public static class EncounterGenerator {
	public const string ENEMY_DIR = "res://Resources/Prefabs/Enemies";
	public const string DEFAULT_TILE = "Tile 2";
	// Red circles on the tile, three models each (p10).
	public const int SPAWN_SLOTS = 4;

	private const int MIN_ENEMIES = 1;
	private const int MAX_ENEMIES = 6;
	private const int MAX_OF_A_KIND = 2;
	private const int ATTEMPTS = 4000;
	private const float BIG_PRESENCE = 1.8f;

	// Total tiered health per level, inclusive.
	private static readonly Vector2I[] HEALTH_BANDS = {
		new Vector2I(1, 5), new Vector2I(6, 10), new Vector2I(11, 20), new Vector2I(21, 40),
	};
	private static readonly int[] MAX_TIER = { 1, 2, 3, 3 };

	public static readonly Dictionary<WorldTerrain, EnemyData.Family> FAMILY_BY_TERRAIN = new() {
		{ WorldTerrain.GRASS, EnemyData.Family.DARKROOT },
		{ WorldTerrain.VOLCANO, EnemyData.Family.IRON_KEEP },
		{ WorldTerrain.SAND, EnemyData.Family.HOLLOWS },
		{ WorldTerrain.MOUNTAIN, EnemyData.Family.TOMB_OF_GIANTS },
		{ WorldTerrain.SNOW, EnemyData.Family.PAINTED_WORLD },
		{ WorldTerrain.CITY, EnemyData.Family.ANOR_LONDO },
	};

	private struct Option {
		public string path;
		public EnemyData data;
		public int tier;
		public int health;
	}

	private static List<(string path, EnemyData data)> roster;

	public static EncounterPlan Roll(WorldNodeData node, RandomNumberGenerator rng) {
		var plan = new EncounterPlan { worldNode = node.id, tile = DEFAULT_TILE };
		int level = Mathf.Clamp(node.level, 1, HEALTH_BANDS.Length);
		Vector2I band = HEALTH_BANDS[level - 1];
		List<Option> options = OptionsFor(FamilyFor(node, rng), MAX_TIER[level - 1]);
		if (options.Count == 0) {
			GD.PushError($"EncounterGenerator: no enemies to roll for '{node.id}'");
			return plan;
		}

		List<Option> best = null;
		int bestMiss = int.MaxValue;
		for (int attempt = 0; attempt < ATTEMPTS && bestMiss > 0; attempt++) {
			List<Option> picks = Pick(options, rng.RandiRange(MIN_ENEMIES, MAX_ENEMIES), band.Y, rng);
			int total = picks.Sum(p => p.health);
			int miss = Mathf.Max(band.X - total, 0) + Mathf.Max(total - band.Y, 0)
				+ (picks.Count < MIN_ENEMIES ? 100 : 0);
			if (miss < bestMiss) {
				best = picks;
				bestMiss = miss;
			}
		}
		if (bestMiss > 0) {
			GD.PushWarning($"EncounterGenerator: '{node.id}' (level {level}) could not fit {band.X}–{band.Y} health; using the closest roll");
		}

		Place(plan, best, rng);
		return plan;
	}

	// Each pick favours a card not yet in the encounter, three to one, so the mix stays varied.
	private static List<Option> Pick(List<Option> options, int count, int budget, RandomNumberGenerator rng) {
		var picks = new List<Option>();
		var perKind = new Dictionary<string, int>();
		int total = 0;
		for (int i = 0; i < count; i++) {
			var fits = options.Where(o => total + o.health <= budget
				&& perKind.GetValueOrDefault(o.path) < MAX_OF_A_KIND).ToList();
			if (fits.Count == 0) break;

			var weights = fits.Select(o => perKind.ContainsKey(o.path) ? 1f : 3f).ToArray();
			Option chosen = fits[(int)rng.RandWeighted(weights)];
			picks.Add(chosen);
			perKind[chosen.path] = perKind.GetValueOrDefault(chosen.path) + 1;
			total += chosen.health;
		}
		return picks;
	}

	// Like the printed encounter cards, enemies stand together on a few spawn nodes rather
	// than one to a node: as few as three to a node allows, up to one each. A big enemy
	// (8+ health, drawn nearly twice a normal token) gets a node to itself while there are
	// nodes to spare, so two of them never fill the same circle.
	private static void Place(EncounterPlan plan, List<Option> picks, RandomNumberGenerator rng) {
		int count = picks.Count;
		if (count == 0) return;
		Shuffle(picks, rng);
		picks = picks.OrderByDescending(IsBig).ToList();

		int big = picks.Count(IsBig);
		int fewest = Mathf.Max(Mathf.CeilToInt(count / 3f), Mathf.Min(big + (count > big ? 1 : 0), SPAWN_SLOTS));
		int used = rng.RandiRange(fewest, Mathf.Min(SPAWN_SLOTS, count));

		var slots = Enumerable.Range(0, SPAWN_SLOTS).ToList();
		Shuffle(slots, rng);
		var load = new int[used];
		var holdsBig = new bool[used];
		foreach (Option pick in picks) {
			int slot = ChooseSlot(load, holdsBig, IsBig(pick));
			load[slot]++;
			holdsBig[slot] |= IsBig(pick);
			plan.spawns.Add(new EncounterSpawn { enemy = pick.path, tier = pick.tier, spawnSlot = slots[slot] });
		}
	}

	private static bool IsBig(Option option) => Enemy.PresenceFor(option.health) >= BIG_PRESENCE;

	// A big enemy takes an empty node if there is one; anything else the least crowded node
	// without a big enemy on it, and only then shares one.
	private static int ChooseSlot(int[] load, bool[] holdsBig, bool big) {
		int best = -1;
		for (int i = 0; i < load.Length; i++) {
			if (load[i] >= EncounterManager.MAX_MODELS_PER_NODE) continue;
			if (best < 0 || Rank(i) < Rank(best)) best = i;
		}
		return best;

		int Rank(int i) => big
			? (load[i] == 0 ? 0 : 10) + load[i]
			: (holdsBig[i] ? 10 : 0) + load[i];
	}

	private static EnemyData.Family FamilyFor(WorldNodeData node, RandomNumberGenerator rng) {
		if (FAMILY_BY_TERRAIN.TryGetValue(node.terrain, out var family)) return family;
		var all = FAMILY_BY_TERRAIN.Values.Distinct().ToList();
		return all[rng.RandiRange(0, all.Count - 1)];
	}

	private static List<Option> OptionsFor(EnemyData.Family family, int maxTier) {
		var options = new List<Option>();
		foreach (var (path, data) in Roster()) {
			if (data.family != family) continue;
			for (int tier = 1; tier <= maxTier; tier++) {
				options.Add(new Option {
					path = path, data = data, tier = tier,
					health = data.health + Enemy.TierHealthBonus(data.health, tier),
				});
			}
		}
		return options;
	}

	// Every EnemyData one folder down from ENEMY_DIR. An exported build lists them as .remap.
	private static List<(string, EnemyData)> Roster() {
		if (roster != null) return roster;
		roster = new List<(string, EnemyData)>();
		foreach (string folder in DirAccess.GetDirectoriesAt(ENEMY_DIR)) {
			foreach (string file in DirAccess.GetFilesAt($"{ENEMY_DIR}/{folder}")) {
				string name = file.TrimSuffix(".remap");
				if (!name.EndsWith(".tres")) continue;
				string path = $"{ENEMY_DIR}/{folder}/{name}";
				if (ResourceLoader.Load(path) is EnemyData data) roster.Add((path, data));
			}
		}
		return roster;
	}

	private static void Shuffle<T>(List<T> list, RandomNumberGenerator rng) {
		for (int i = list.Count - 1; i > 0; i--) {
			int j = rng.RandiRange(0, i);
			(list[i], list[j]) = (list[j], list[i]);
		}
	}
}
