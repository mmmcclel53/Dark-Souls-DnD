using Godot;
using Godot.Collections;

[GlobalClass]
public partial class EnemyData : Resource {

	// Cards that show the infinity symbol in their range circle. The grid is only
	// ever 7x7, so any distance check against this always passes.
	public const int UNLIMITED_RANGE = 99;

	[Export] public string enemyName = "";
	[Export] public Texture2D cardTexture;
	[Export] public Texture2D avatarTexture;

	[Export] public int threatLevel = 1;
	[Export] public int health = 1;

	[Export] public int physicalDefense = 0;
	[Export] public int magicalDefense = 0;

	[Export(PropertyHint.ResourceType, "EnemyMove")] public Array<EnemyMove> moves = new Array<EnemyMove>();
}
