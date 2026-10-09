using Godot;

// Souls the party holds (or has dropped), remembering the world node they came from, so a
// lost pile can go back to the encounters that paid it (SoulCache.Lose).
// No constructor: see the Resource note in CLAUDE.md.
[GlobalClass]
public partial class SoulLot : Resource {
	// The encounter or boss that paid them; "" for souls with nowhere to go back to.
	[Export] public string source = "";
	[Export] public int amount = 0;
}
