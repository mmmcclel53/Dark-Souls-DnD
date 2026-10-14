using Godot;

// One boss of the board game: its tier and the treasure cards it pays out. Written by
// Tools/onboard_bosses.py from the board game's Images/Bosses folder, which also holds this
// project's tier swaps. The fight itself is not modelled yet: a boss node is fought as a
// stand-in encounter (EncounterGenerator) and pays this boss's treasure (BossRoster.Payout).
// No constructor: see the Resource note in CLAUDE.md.
[GlobalClass]
public partial class BossData : Resource {
	[Export] public string bossName = "";
	[Export] public WorldBossKind kind = WorldBossKind.MINI;
	// The campaign's last boss (Soul of Cinder), kept for the last section's main boss.
	[Export] public bool final;
	// Catalogue names (GameManager.GetTemplate) of the cards it pays, one of the party's choice.
	[Export] public string[] treasure = new string[0];
}
