using Godot;

// Where one roaming threat stands (WorldMapManager.TickRoamers): its hex, its place in the
// loop, whether it is hunting the party, and whether it has been beaten since the last rest.
// No constructor: see the Resource note in CLAUDE.md.
[GlobalClass]
public partial class RoamerState : Resource
{
	[Export] public string id = "";
	[Export] public string nodeId = "";
	// Index into the roamer's loop of the hex it last patrolled onto.
	[Export] public int loopIndex;
	[Export] public bool chasing;
	[Export] public bool beaten;
	// The hex the party last saw it on, drawn as a grey ghost while the roamer is out of sight;
	// "" when never seen, or when the party has since looked and found the hex empty.
	[Export] public string lastSeenNode = "";
}
