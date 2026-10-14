using Godot;
using System.Collections.Generic;
using System.Linq;

// Blacksmith Andre's treasure (p14, campaign p33), sold face up rather than drawn blind
// (Matt, Oct 2026): the Bonfire's Merchant shows a few cards from the treasure deck, each at
// SoulEconomy.CardPrice for its rarity. A card bought is minted into the party's pool and is
// gone from the deck for the campaign.
//
// The deck is every item someone in the party could wield at Tier 3, starting gear and boss
// gear (bossTreasure, reserved for what a boss pays out) aside. The Merchant sells Common only,
// and Uncommon from the campaign's halfway point (UNCOMMON_AT); anything rarer reaches the
// party through map loot and roaming drops (LootTier) only. The stock turns over on
// a rest, but only after a win that paid souls since the last turnover, so resting cannot
// reroll it for free: unsold cards go back into the deck and fresh ones are drawn.
//
// All state lives on the campaign save; this is just the reader/writer. Every call no-ops
// without a save.
public static class Merchant {
	// Share of the campaign's sections cleared at which Uncommon cards come in. Matt's call was
	// "halfway"; souls spent was the alternative he floated.
	public const float UNCOMMON_AT = 0.5f;

	private static readonly Dictionary<Equipment.Rarity, float> WEIGHT = new() {
		{ Equipment.Rarity.COMMON, 2 }, { Equipment.Rarity.UNCOMMON, 1 },
	};

	private static SaveGame Save => CampaignManager.CurrentSave;

	// A few more cards than the party has characters, so there is a choice for everyone.
	public static int StockSize => 3 + CampaignManager.Players.Length;

	// The cards on show, by template name; "" is one already bought. Stocked on the first look.
	public static string[] Stock() {
		if (Save == null) return new string[0];
		if (!Save.merchantStocked) Restock();
		return Save.merchantStock;
	}

	public static void NoteWin() {
		if (Save != null) Save.winsSinceRestock++;
	}

	public static void OnRest() {
		if (Save != null && (!Save.merchantStocked || Save.winsSinceRestock > 0)) Restock();
	}

	public static int Price(Equipment card) => SoulEconomy.CardPrice(card.rarity);

	// Pays and mints the card into the party's pool. False when it cannot be paid.
	public static bool Buy(int slot) {
		string[] stock = Stock();
		if (slot < 0 || slot >= stock.Length || GameManager.GetTemplate(stock[slot]) == null) return false;
		if (!SoulCache.Spend(Price(GameManager.GetTemplate(stock[slot])))) return false;
		GameManager.MintInstance(stock[slot]);
		stock[slot] = "";
		return true;
	}

	// Map loot (WorldMapManager.TakeLoot): one random card of the tier from the treasure deck,
	// minted into the party's pool like a purchase. The nearest lower tier stands in when the tier
	// is empty, then higher ones. Null when the deck has nothing left.
	public static Equipment DrawLoot(Equipment.Rarity tier) {
		if (Save == null) return null;
		Stock();
		var deck = Save.treasureDeck.Where(Sellable).ToList();
		int top = (int)Equipment.Rarity.LEGENDARY;
		int first = Mathf.Clamp((int)tier, (int)Equipment.Rarity.COMMON, top);
		var order = Enumerable.Range((int)Equipment.Rarity.COMMON, first - (int)Equipment.Rarity.COMMON + 1).Reverse()
			.Concat(Enumerable.Range(first + 1, top - first));
		foreach (int rank in order) {
			var options = deck.Where(name => (int)GameManager.GetTemplate(name).rarity == rank).ToList();
			if (options.Count == 0) continue;

			var rng = new RandomNumberGenerator();
			rng.Randomize();
			string pick = options[rng.RandiRange(0, options.Count - 1)];
			var remaining = Save.treasureDeck.ToList();
			remaining.Remove(pick);
			Save.treasureDeck = remaining.ToArray();
			return GameManager.MintInstance(pick);
		}
		return null;
	}

	// The rarity map loot and roaming drops draw. Unguarded loot is the best the Merchant sells
	// right now; guarded loot (and a roamer's drop) climbs with the campaign: Uncommon, then Rare
	// from a quarter of the way, Epic from a half, Legendary from three quarters.
	public static Equipment.Rarity LootTier(bool guarded) {
		if (!guarded) return MaxRarity();
		float progress = WorldMapManager.Progress();
		if (progress < 0.25f) return Equipment.Rarity.UNCOMMON;
		if (progress < 0.5f) return Equipment.Rarity.RARE;
		if (progress < 0.75f) return Equipment.Rarity.EPIC;
		return Equipment.Rarity.LEGENDARY;
	}

	private static bool Sellable(string name) {
		Equipment card = GameManager.GetTemplate(name);
		return card != null && !card.bossTreasure;
	}

	private static void Restock() {
		if (!Save.merchantStocked) Save.treasureDeck = BuildDeck();
		var deck = Save.treasureDeck.ToList();
		deck.AddRange(Save.merchantStock.Where(name => !string.IsNullOrEmpty(name)));
		deck.RemoveAll(name => !Sellable(name));

		Equipment.Rarity rarest = MaxRarity();
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		var stock = new List<string>();
		while (stock.Count < StockSize) {
			var options = deck.Where(name => GameManager.GetTemplate(name)?.rarity <= rarest).ToList();
			if (options.Count == 0) break;
			float[] weights = options.Select(name => WEIGHT.GetValueOrDefault(GameManager.GetTemplate(name).rarity, 1f)).ToArray();
			string pick = options[(int)rng.RandWeighted(weights)];
			stock.Add(pick);
			deck.Remove(pick);
		}

		Save.treasureDeck = deck.ToArray();
		Save.merchantStock = stock.ToArray();
		Save.merchantStocked = true;
		Save.winsSinceRestock = 0;
	}

	private static string[] BuildDeck() =>
		GameManager.GetAllTemplates()
			.Where(e => e.rarity != Equipment.Rarity.STARTER && !e.bossTreasure && CampaignManager.Players.Any(p => p.MeetsRequirements(e, TopStats(p))))
			.Select(e => e.name)
			.ToArray();

	public static int[] TopStats(Player player) =>
		Player.STATS.Select(stat => player.Tiers(stat)[Player.TOP_TIER]).ToArray();

	// Common at first, Uncommon from UNCOMMON_AT of the way through the sections; never rarer.
	public static Equipment.Rarity MaxRarity() =>
		WorldMapManager.Progress() >= UNCOMMON_AT ? Equipment.Rarity.UNCOMMON : Equipment.Rarity.COMMON;
}
