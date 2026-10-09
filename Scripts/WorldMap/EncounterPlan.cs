using Godot;
using Godot.Collections;

// A world map encounter, rolled once when the campaign is made and kept in the save, so
// going back after a wipe is the same fight: the same enemies, tiers, spawn nodes and tile.
// No constructor: see the Resource note in CLAUDE.md.
[GlobalClass]
public partial class EncounterPlan : Resource {
	[Export] public string worldNode = "";
	// Board art under Resources/Images/Backgrounds, by name. Only Tile 2 has its grid mapped.
	[Export] public string tile = "";
	[Export] public Array<EncounterSpawn> spawns = new Array<EncounterSpawn>();
	// What the first win pays each character: this encounter's share of its section's budget
	// (SoulEconomy), worked out once when the map's plans are rolled. -1 until then.
	[Export] public int souls = -1;
	// Party souls already taken off the map: held, dropped or spent. A lost pile gives its
	// share back, which lights the encounter again (SoulCache.Lose).
	[Export] public int soulsClaimed = 0;

	public int TotalHealth {
		get {
			int total = 0;
			foreach (EncounterSpawn spawn in spawns) total += spawn.Health;
			return total;
		}
	}

	// Shown on the world map: the most tiered health, ties to the higher threat.
	public EncounterSpawn Toughest {
		get {
			EncounterSpawn best = null;
			foreach (EncounterSpawn spawn in spawns) {
				if (spawn.Data == null) continue;
				if (best == null || spawn.Health > best.Health
					|| (spawn.Health == best.Health && spawn.Data.threatLevel > best.Data.threatLevel)) {
					best = spawn;
				}
			}
			return best;
		}
	}

	public string TilePath => $"res://Resources/Images/Backgrounds/{tile}.jpg";
}
