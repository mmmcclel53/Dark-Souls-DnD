using Godot;

// One enemy on the board in an EncounterSnapshot.
[GlobalClass]
public partial class EnemyState : Resource
{
	[Export] public string dataPath = "";
	[Export] public int tier = 1;
	[Export] public int gridIndex = -1;
	[Export] public int health;
	[Export] public int[] conditions = new int[0];

	public EnemyData Data => ResourceLoader.Exists(dataPath) ? ResourceLoader.Load<EnemyData>(dataPath) : null;

	public static EnemyState Of(Enemy enemy) => new EnemyState {
		dataPath = enemy.data.ResourcePath,
		tier = enemy.tier,
		gridIndex = EncounterManager.GridIndexOf((Node2D)enemy.GetParent()),
		health = enemy.currentHealth,
		conditions = EncounterSnapshot.ConditionsOf(enemy.Conditions),
	};
}
