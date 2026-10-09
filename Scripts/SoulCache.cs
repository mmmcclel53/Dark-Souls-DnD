using Godot;
using Godot.Collections;

// The party's shared soul pool and the pile left behind by a wipe (p19).
//
// The campaign holds a fixed number of souls (SoulEconomy), so nothing here ever destroys
// one an encounter paid. The pool is kept as lots that remember where they came from:
// spending takes the oldest first and those souls are gone from the map for good, while a
// pile lost to a second death goes back to its encounters (WorldMapManager.ReturnSouls),
// which light up to be won again. Boss souls are the exception: a boss stays dead, so lost
// boss souls are gone.
//
// All state lives on the campaign save so it survives the trip back to the bonfire; this
// is just the reader/writer. Every call no-ops without a save, so an encounter run on its
// own outside a campaign still works.
public static class SoulCache {

	private static SaveGame Save => CampaignManager.CurrentSave;

	public static int current => Total(Save?.heldSouls);

	public static int droppedAmount => Total(Save?.droppedSouls);
	public static string droppedWorldNode => Save?.droppedSoulsWorldNode ?? "";
	public static int droppedGridIndex => Save?.droppedSoulsGridIndex ?? -1;

	// source is the world node that paid them, or "" for souls with nowhere to go back to.
	public static void Award(int amount, string source) {
		if (Save == null || amount <= 0) return;
		Array<SoulLot> held = Save.heldSouls;
		if (held.Count > 0 && held[held.Count - 1].source == source) {
			held[held.Count - 1].amount += amount;
			return;
		}
		held.Add(new SoulLot { source = source ?? "", amount = amount });
	}

	// Oldest first. Spent souls stay claimed off the map: they bought something for good.
	public static bool Spend(int amount) {
		if (Save == null || amount <= 0 || current < amount) return false;
		Array<SoulLot> held = Save.heldSouls;
		while (amount > 0) {
			int taken = Mathf.Min(amount, held[0].amount);
			held[0].amount -= taken;
			amount -= taken;
			if (held[0].amount == 0) held.RemoveAt(0);
		}
		return true;
	}

	// A wipe drops the whole cache where the character fell. A pile already lying somewhere
	// from an earlier death is lost rather than stacking (p19), and goes back to its encounters.
	public static void DropOnDeath(string worldNodeId, int gridIndex) {
		if (Save == null) return;

		Lose(Save.droppedSouls);
		Save.droppedSouls = Save.heldSouls;
		Save.heldSouls = new Array<SoulLot>();
		Save.droppedSoulsWorldNode = worldNodeId ?? "";
		Save.droppedSoulsGridIndex = gridIndex;
	}

	public static bool HasDropIn(string worldNodeId) =>
		Save != null
		&& droppedAmount > 0
		&& Save.droppedSoulsGridIndex >= 0
		&& !string.IsNullOrEmpty(worldNodeId)
		&& Save.droppedSoulsWorldNode == worldNodeId;

	// Walking onto the pile puts it back in the cache. The pile was earned before anything
	// held now, so it goes in front and is spent first.
	public static int Retrieve() {
		if (Save == null || droppedAmount <= 0) return 0;

		int recovered = droppedAmount;
		Array<SoulLot> held = Save.droppedSouls;
		held.AddRange(Save.heldSouls);
		Save.heldSouls = held;
		Save.droppedSouls = new Array<SoulLot>();
		Save.droppedSoulsWorldNode = "";
		Save.droppedSoulsGridIndex = -1;
		return recovered;
	}

	private static void Lose(Array<SoulLot> lots) {
		foreach (SoulLot lot in lots) WorldMapManager.ReturnSouls(lot.source, lot.amount);
	}

	private static int Total(Array<SoulLot> lots) {
		int total = 0;
		if (lots == null) return total;
		foreach (SoulLot lot in lots) total += lot.amount;
		return total;
	}
}
