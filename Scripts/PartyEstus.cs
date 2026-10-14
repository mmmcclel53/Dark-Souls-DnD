using Godot;

// The party's shared Estus flasks. A pool of `Max` charges, refilled by a rest, drunk by the
// active character in their own activation like their personal flask. Estus Shards found on
// the map raise `Max` by one apiece at a bonfire (Reinforce).
//
// All state lives on the campaign save, like SoulCache. Without a save (an encounter run on
// its own) there is no pool: Max, Charges and Shards read 0 and every call no-ops.
public static class PartyEstus {

	// The pool a new campaign starts with; Matt to tune.
	public const int PARTY_ESTUS_START = 1;

	private static SaveGame Save => CampaignManager.CurrentSave;

	public static int Max => Migrated()?.estusMax ?? 0;
	public static int Charges => Migrated()?.estusCharges ?? 0;
	public static int Shards => Save?.estusShards ?? 0;

	public static bool CanDrink => Charges > 0;
	public static bool CanReinforce => Shards > 0;

	public static void Drink() {
		SaveGame save = Migrated();
		if (save == null || save.estusCharges <= 0) return;
		save.estusCharges--;
	}

	public static void Refill() {
		SaveGame save = Migrated();
		if (save != null) save.estusCharges = save.estusMax;
	}

	// A shard becomes a flask: one more in the pool, and it comes full.
	public static void Reinforce() {
		SaveGame save = Migrated();
		if (save == null || save.estusShards <= 0) return;
		save.estusShards--;
		save.estusMax++;
		save.estusCharges++;
	}

	public static void AddShard() {
		if (Save != null) Save.estusShards++;
	}

	// A save from before the pool existed (estusMax is -1) gets the starting pool, full, once.
	public static void Migrate(SaveGame save) {
		if (save == null || save.estusMax >= 0) return;
		save.estusMax = PARTY_ESTUS_START;
		save.estusCharges = save.estusMax;
	}

	private static SaveGame Migrated() {
		Migrate(Save);
		return Save;
	}
}
