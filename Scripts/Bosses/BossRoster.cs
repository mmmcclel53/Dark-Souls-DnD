using Godot;
using System.Collections.Generic;
using System.Linq;

// Every BossData under Resources/Prefabs/Bosses, and the rules for what a beaten boss pays
// (Matt, Oct 2026): **one** of its own cards, the party's choice, and when its best card is
// below its tier's floor (Mini Uncommon, Main Rare, Mega Epic) one ordinary treasure card at
// that floor as well. A boss with no cards of its own pays the floor card alone.
public static class BossRoster {
	private const string DIR = "res://Resources/Prefabs/Bosses";

	private static List<BossData> all;

	public static IReadOnlyList<BossData> All {
		get {
			if (all == null) Load();
			return all;
		}
	}

	public static BossData Get(string name) => All.FirstOrDefault(b => b.bossName == name);

	public static Equipment.Rarity Floor(WorldBossKind kind) => kind switch {
		WorldBossKind.MINI => Equipment.Rarity.UNCOMMON,
		WorldBossKind.MEGA => Equipment.Rarity.EPIC,
		_ => Equipment.Rarity.RARE,
	};

	// The boss's cards the catalogue knows, to choose one from.
	public static string[] Choices(BossData boss) =>
		boss?.treasure.Where(name => GameManager.GetTemplate(name) != null).ToArray() ?? new string[0];

	// Whether the floor card is owed on top: the boss's best card falls short of its tier.
	public static bool OwesFloorCard(BossData boss, WorldBossKind kind) {
		var cards = Choices(boss);
		return cards.Length == 0 || cards.Max(name => GameManager.GetTemplate(name).rarity) < Floor(kind);
	}

	// Picks the bosses for a campaign's boss nodes, in section order: each a boss of the node's
	// tier not used yet (repeats only once a tier runs out), the last section's main boss the
	// final one. Returns node id → boss name.
	public static Dictionary<string, string> Assign(IEnumerable<WorldNodeData> bossNodes, int sections, ISet<string> taken, RandomNumberGenerator rng) {
		var used = new HashSet<string>(taken);
		var result = new Dictionary<string, string>();
		foreach (WorldNodeData node in bossNodes.OrderBy(n => n.section)) {
			bool last = node.boss == WorldBossKind.MAIN && sections > 0 && node.section >= sections;
			var pool = All.Where(b => last ? b.final : !b.final && b.kind == node.boss).ToList();
			if (pool.Count == 0) continue;
			var fresh = pool.Where(b => !used.Contains(b.bossName)).ToList();
			if (fresh.Count > 0) pool = fresh;
			BossData pick = pool[rng.RandiRange(0, pool.Count - 1)];
			used.Add(pick.bossName);
			result[node.id] = pick.bossName;
		}
		return result;
	}

	private static void Load() {
		all = new List<BossData>();
		var dir = DirAccess.Open(DIR);
		if (dir == null) return;
		foreach (string file in dir.GetFiles()) {
			string name = file.EndsWith(".remap") ? file[..^6] : file;
			if (!name.EndsWith(".tres")) continue;
			if (ResourceLoader.Load($"{DIR}/{name}") is BossData boss) all.Add(boss);
		}
		all.Sort((a, b) => string.CompareOrdinal(a.bossName, b.bossName));
	}
}
