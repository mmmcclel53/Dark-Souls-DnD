using Godot;

// The party's shared soul pool and the pile left behind by a wipe (p19).
//
// All state lives on the campaign save so it survives the trip back to the bonfire; this
// is just the reader/writer. Every call no-ops without a save, so an encounter run on its
// own outside a campaign still works.
public static class SoulCache {

	public const int SOULS_PER_CHARACTER = 2;

	private static SaveGame Save => CampaignManager.CurrentSave;

	public static int current => Save?.souls ?? 0;

	public static int droppedAmount => Save?.droppedSoulsAmount ?? 0;
	public static string droppedWorldNode => Save?.droppedSoulsWorldNode ?? "";
	public static int droppedGridIndex => Save?.droppedSoulsGridIndex ?? -1;

	public static void Award(int amount) {
		if (Save == null || amount <= 0) return;
		Save.souls += amount;
	}

	public static bool Spend(int amount) {
		if (Save == null || amount <= 0 || Save.souls < amount) return false;
		Save.souls -= amount;
		return true;
	}

	// A wipe drops the whole cache where the character fell. Anything already lying around
	// from an earlier death is discarded rather than stacking (p19).
	public static void DropOnDeath(string worldNodeId, int gridIndex) {
		if (Save == null) return;

		Save.droppedSoulsWorldNode = worldNodeId ?? "";
		Save.droppedSoulsGridIndex = gridIndex;
		Save.droppedSoulsAmount = Save.souls;
		Save.souls = 0;
	}

	public static bool HasDropIn(string worldNodeId) =>
		Save != null
		&& Save.droppedSoulsAmount > 0
		&& Save.droppedSoulsGridIndex >= 0
		&& !string.IsNullOrEmpty(worldNodeId)
		&& Save.droppedSoulsWorldNode == worldNodeId;

	// Walking onto the pile puts it back in the cache.
	public static int Retrieve() {
		if (Save == null || Save.droppedSoulsAmount <= 0) return 0;

		int recovered = Save.droppedSoulsAmount;
		Save.souls += recovered;
		ClearDrop();
		return recovered;
	}

	public static void ClearDrop() {
		if (Save == null) return;
		Save.droppedSoulsWorldNode = "";
		Save.droppedSoulsGridIndex = -1;
		Save.droppedSoulsAmount = 0;
	}
}
