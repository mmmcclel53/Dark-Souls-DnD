using Godot;

// Where the nodes are on a printed boss tile. The board is the same 7x7 grid of GameNodes as
// ever (a square lattice turned 45°, see Board Geometry in CLAUDE.md), but each boss tile draws
// its circles somewhere else: a 13-node tile uses only the inner diamond, and their spacing is
// wider than Tile 2's and not quite square. So a layout lists every grid cell that is on the
// tile with the centre of its printed circle, measured from the art (Tools/BossTiles).
// Cells it does not list are off the board.
// No constructor: see the Resource note in CLAUDE.md.
[GlobalClass]
public partial class BoardLayout : Resource {
	public const string DIR = "res://Resources/Prefabs/BossTiles";

	// The art under Resources/Images/Backgrounds/Boss, by name.
	[Export] public string tile = "";
	// A node's box, as a fraction of the board: one lattice step.
	[Export] public float nodeSize = 0.12f;
	// Grid indices (y * 7 + x) on the tile, and each one's centre as a fraction of the board.
	[Export] public int[] cells = new int[0];
	[Export] public Vector2[] centres = new Vector2[0];
	[Export] public int[] entrances = new int[0];
	[Export] public int[] enemySpawns = new int[0];
	// Pillars (the Asylum and Stray Demons' tile): no one stands on one until a boss card with
	// the pillar icon smashes it (Enemy.SmashPillar).
	[Export] public int[] pillars = new int[0];
	[Export] public int bossSpawn = -1;
	// The grid step the boss faces as the fight starts: towards the door the party comes in by.
	[Export] public Vector2I bossFacing = new Vector2I(-1, 1);

	public string TilePath => $"res://Resources/Images/Backgrounds/Boss/{tile}.jpg";

	public static BoardLayout Load(string tile) {
		string path = $"{DIR}/{tile}.tres";
		return !string.IsNullOrEmpty(tile) && ResourceLoader.Exists(path) ? ResourceLoader.Load<BoardLayout>(path) : null;
	}
}
