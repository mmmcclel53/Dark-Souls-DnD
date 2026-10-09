using Godot;
using System.Linq;

// The campaign's fixed soul budget (Matt, Oct 2026; this project's own rule, not the book's).
// Every number here is per character: the party's pool gets it times the party size.
//
// A character starts with 2 base cubes and has 14 level-ups to make (Tier 3 in all four
// stats; there is no Tier 4). A level costs the level being reached, so Level 2 costs 2 and
// Level 15 costs 15: 119 souls in all, whatever the campaign's size. Each section's
// encounters pay for that section's share of the levels plus three treasure cards.
public static class SoulEconomy {
	public const int LEVEL_UPS = 14;
	public const int CARDS_PER_SECTION = 3;
	public const int CARD_PRICE = 2;
	public const int TREASURE_PER_SECTION = CARDS_PER_SECTION * CARD_PRICE;
	public const int MAX_LEVEL = 1 + LEVEL_UPS;

	// The price of a level is the level being reached: Level 2 costs 2, Level 15 costs 15.
	public static int LevelCost(int toLevel) => toLevel;

	// What the next `count` levels cost a character now at `level`.
	public static int LevelsCost(int level, int count) {
		int total = 0;
		for (int i = 1; i <= count; i++) total += LevelCost(level + i);
		return total;
	}

	// The Firekeeper's cap while the party is in section `sectionsCleared + 1`: every level-up
	// up to that section's end, so its levels can be bought before its boss (floor(14 × s /
	// sections), the same steps the section budgets pay for).
	public static int LevelCap(int sectionsCleared, int sections) {
		if (sections <= 0) return MAX_LEVEL;
		int section = Mathf.Clamp(sectionsCleared + 1, 1, sections);
		return 1 + LEVEL_UPS * section / sections;
	}

	// What the first x level-ups cost: 2 + 3 + … + (x + 1). x may be fractional, which keeps
	// a section's share smooth when the levels do not divide evenly into the sections.
	private static double LevelUpSouls(double x) => x * (x + 3) / 2;

	// Section s's share of the levels: what they cost between the end of the last section and
	// the end of this one, at 14 / sections levels a section. Rounding the running total rather
	// than each share keeps the campaign's total exactly 119.
	public static int SectionLevelSouls(int section, int sections) {
		if (sections <= 0 || section < 1 || section > sections) return 0;
		double perSection = (double)LEVEL_UPS / sections;
		return Round(LevelUpSouls(perSection * section)) - Round(LevelUpSouls(perSection * (section - 1)));
	}

	// What a section's encounters pay between them.
	public static int SectionBudget(int section, int sections) {
		if (sections <= 0 || section < 1 || section > sections) return 0;
		return SectionLevelSouls(section, sections) + TREASURE_PER_SECTION;
	}

	// A boss pays on top of the section's budget, so it is a bonus: lost, it is gone for good.
	// A mini boss pays a quarter of the budget, a main boss half, a mega boss all of it. The
	// last section's main boss ends the campaign, so it pays treasure only.
	public static int BossSouls(WorldBossKind kind, int section, int sections) {
		int budget = SectionBudget(section, sections);
		switch (kind) {
			case WorldBossKind.MINI: return Round(budget / 4.0);
			case WorldBossKind.MEGA: return budget;
			default: return section >= sections ? 0 : Round(budget / 2.0);
		}
	}

	// Splits a budget in proportion to the weights (an encounter's tiered health), so a
	// tougher fight pays more. Largest remainder, ties to the earlier one, so the shares add
	// up to the budget exactly. With no weight to go on, the shares are equal. No share is 0
	// while the budget can go round: a fight that pays nothing would be a fight for nothing,
	// so a share that rounds to 0 takes 1 from the biggest.
	public static int[] Split(int budget, int[] weights) {
		int[] shares = Proportional(budget, weights);
		if (budget < shares.Length) return shares;
		for (int i = 0; i < shares.Length; i++) {
			if (shares[i] > 0) continue;
			int biggest = System.Array.IndexOf(shares, shares.Max());
			shares[biggest]--;
			shares[i]++;
		}
		return shares;
	}

	private static int[] Proportional(int budget, int[] weights) {
		int[] shares = new int[weights.Length];
		if (weights.Length == 0) return shares;

		double total = 0;
		foreach (int weight in weights) total += Mathf.Max(0, weight);

		double[] exact = new double[weights.Length];
		int given = 0;
		for (int i = 0; i < weights.Length; i++) {
			exact[i] = total > 0 ? budget * Mathf.Max(0, weights[i]) / total : (double)budget / weights.Length;
			shares[i] = (int)System.Math.Floor(exact[i]);
			given += shares[i];
		}
		for (; given < budget; given++) {
			int best = 0;
			for (int i = 1; i < weights.Length; i++) {
				if (exact[i] - shares[i] > exact[best] - shares[best]) best = i;
			}
			shares[best]++;
		}
		return shares;
	}

	private static int Round(double x) => (int)System.Math.Floor(x + 0.5);
}
