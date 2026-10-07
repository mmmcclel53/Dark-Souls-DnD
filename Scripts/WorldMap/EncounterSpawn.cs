using Godot;

// One enemy of a fixed encounter: which card, at what tier, on which spawn node.
// No constructor: see the Resource note in CLAUDE.md.
[GlobalClass]
public partial class EncounterSpawn : Resource {
	// Path of the EnemyData, not the resource itself, so the save holds a reference
	// rather than a copy of the card.
	[Export] public string enemy = "";
	[Export] public int tier = 1;
	// Index into the tile's spawn nodes, in scene order.
	[Export] public int spawnSlot = 0;

	private EnemyData data;

	public EnemyData Data {
		get {
			if (data == null && ResourceLoader.Exists(enemy)) data = ResourceLoader.Load<EnemyData>(enemy);
			return data;
		}
	}

	public int Health => Data == null ? 0 : Data.health + Enemy.TierHealthBonus(Data.health, tier);
}
