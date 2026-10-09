using Godot;
using System.Collections.Generic;
using System.Linq;

// Blacksmith Andre's treasure (p14, campaign p33), sold face up rather than drawn blind
// (Matt, Oct 2026): the Bonfire's Merchant shows a few cards from the treasure deck, each at
// SoulEconomy.CARD_PRICE, the price the campaign's soul budget allows three of per section.
// A card bought is minted into the party's pool and is gone from the deck for the campaign.
//
// The deck is every item someone in the party could wield at Tier 3, starting gear aside.
// Rarer cards come into the stock as the campaign goes on (MaxRarity). The stock turns over on
// a rest, but only after a win that paid souls since the last turnover, so resting cannot
// reroll it for free: unsold cards go back into the deck and fresh ones are drawn.
//
// All state lives on the campaign save; this is just the reader/writer. Every call no-ops
// without a save.
public static class Merchant {
	private static readonly Dictionary<Equipment.Rarity, float> WEIGHT = new() {
		{ Equipment.Rarity.COMMON, 3 }, { Equipment.Rarity.UNCOMMON, 3 },
		{ Equipment.Rarity.RARE, 2 }, { Equipment.Rarity.EPIC, 2 }, { Equipment.Rarity.LEGENDARY, 1 },
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

	// Pays and mints the card into the party's pool. False when it cannot be paid.
	public static bool Buy(int slot) {
		string[] stock = Stock();
		if (slot < 0 || slot >= stock.Length || GameManager.GetTemplate(stock[slot]) == null) return false;
		if (!SoulCache.Spend(SoulEconomy.CARD_PRICE)) return false;
		GameManager.MintInstance(stock[slot]);
		stock[slot] = "";
		return true;
	}

	private static void Restock() {
		if (!Save.merchantStocked) Save.treasureDeck = BuildDeck();
		var deck = Save.treasureDeck.ToList();
		deck.AddRange(Save.merchantStock.Where(name => !string.IsNullOrEmpty(name)));

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
			.Where(e => e.rarity != Equipment.Rarity.STARTER && CampaignManager.Players.Any(p => p.MeetsRequirements(e, TopStats(p))))
			.Select(e => e.name)
			.ToArray();

	public static int[] TopStats(Player player) =>
		Player.STATS.Select(stat => player.Tiers(stat)[Player.TOP_TIER]).ToArray();

	// Common and Uncommon from the start, then Rare, Epic and Legendary from a quarter, half
	// and three quarters of the way through the campaign's sections.
	private static Equipment.Rarity MaxRarity() {
		int step = (int)(WorldMapManager.Progress() * 4);
		return (Equipment.Rarity)Mathf.Min((int)Equipment.Rarity.LEGENDARY, (int)Equipment.Rarity.UNCOMMON + step);
	}
}
