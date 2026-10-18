using Godot;

// One enemy on the board in an EncounterSnapshot. A boss also keeps its facing and its
// behaviour deck, by card index (BossBehaviour).
[GlobalClass]
public partial class EnemyState : Resource
{
	[Export] public string dataPath = "";
	[Export] public int tier = 1;
	[Export] public int gridIndex = -1;
	[Export] public int health;
	[Export] public int[] conditions = new int[0];

	[Export] public string bossPath = "";
	[Export] public Vector2I facing;
	[Export] public int[] deck = new int[0];
	[Export] public int[] discard = new int[0];
	[Export] public int lastCard = -1;
	[Export] public bool heated;

	public EnemyData Data => ResourceLoader.Exists(dataPath) ? ResourceLoader.Load<EnemyData>(dataPath) : null;
	public BossData Boss => !string.IsNullOrEmpty(bossPath) && ResourceLoader.Exists(bossPath) ? ResourceLoader.Load<BossData>(bossPath) : null;

	public BossBehaviour Behaviour(BossData boss) => BossBehaviour.Restore(boss, facing, deck, discard, lastCard, heated);

	public static EnemyState Of(Enemy enemy) {
		EnemyState state = new EnemyState {
			dataPath = enemy.isBoss ? "" : enemy.data.ResourcePath,
			tier = enemy.tier,
			gridIndex = EncounterManager.GridIndexOf((Node2D)enemy.GetParent()),
			health = enemy.currentHealth,
			conditions = EncounterSnapshot.ConditionsOf(enemy.Conditions),
		};
		if (enemy.isBoss) {
			state.bossPath = enemy.boss.ResourcePath;
			state.facing = enemy.facing;
			state.deck = enemy.behaviour.DeckIndices;
			state.discard = enemy.behaviour.DiscardIndices;
			state.lastCard = enemy.behaviour.LastIndex;
			state.heated = enemy.behaviour.heated;
		}
		return state;
	}
}
