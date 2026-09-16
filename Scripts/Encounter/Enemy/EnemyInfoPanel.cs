using Godot;
using System.Collections.Generic;

// Renders an enemy's data card as text. Everything here comes from EnemyData except
// current health and tier-adjusted defence, which the printed card cannot show.
public partial class EnemyInfoPanel : Control
{

	[Export] public TextureRect avatar;
	[Export] public Label nameLabel;
	[Export] public Label subLabel;

	[Export] public Label healthValue;
	[Export] public Label blockValue;
	[Export] public Label resistValue;
	[Export] public Label rangeValue;

	[Export] public Container behaviours;
	[Export] public PackedScene behaviourRowScene;
	[Export] public TextureRect cardImage;

	public void ShowEnemy(Enemy enemy) {
		EnemyData data = enemy.data;

		if (avatar != null) avatar.Texture = data.avatarTexture;
		if (cardImage != null) cardImage.Texture = data.cardTexture;
		if (nameLabel != null) nameLabel.Text = data.enemyName;
		if (subLabel != null) subLabel.Text = $"Threat {data.threatLevel}  ·  Tier {enemy.tier}";

		if (healthValue != null) healthValue.Text = $"{enemy.currentHealth}/{enemy.maxHealth}";
		if (blockValue != null) blockValue.Text = enemy.physicalDefense.ToString();
		if (resistValue != null) resistValue.Text = enemy.magicalDefense.ToString();
		if (rangeValue != null) rangeValue.Text = CardRange(data);

		BuildBehaviours(data);
	}

	private void BuildBehaviours(EnemyData data) {
		if (behaviours == null) return;
		foreach (Node child in behaviours.GetChildren()) {
			child.QueueFree();
		}

		int order = 1;
		foreach (EnemyMove move in data.moves) {
			EnemyBehaviourRow row = behaviourRowScene.Instantiate<EnemyBehaviourRow>();
			behaviours.AddChild(row);
			row.Fill(order, Describe(move), Traits(move), Stats(move));
			order++;
		}
	}

	// A behaviour is movement if it travels at all; everything else is an attack in place.
	private static bool IsMovement(EnemyMove move) => move.isLeap || move.direction != 0;

	private static string Target(EnemyMove move) =>
		move.towardsAggro ? "the aggro holder" : "the nearest character";

	private static string Describe(EnemyMove move) {
		if (move.isLeap) {
			return $"Leap to {Target(move)}";
		}
		if (move.direction != 0) {
			int nodes = Mathf.Abs(move.direction);
			string way = move.direction > 0 ? "toward" : "away from";
			string unit = nodes == 1 ? "node" : "nodes";
			return $"Move {nodes} {unit} {way} {Target(move)}";
		}
		string damageType = move.isMagic ? "magical" : "physical";
		return $"Attack {move.damage} {damageType}, {Target(move)}";
	}

	// The modifiers that change what the behaviour does.
	private static string Traits(EnemyMove move) {
		List<string> traits = new List<string>();

		if (move.isPush) {
			traits.Add(IsMovement(move) && move.damage > 0 ? $"Push {move.damage} damage" : "Push");
		}
		if (move.isAOE) traits.Add("Whole node");
		if (move.statusEffect != EncounterManager.StatusEffect.NONE) {
			traits.Add(ConditionName(move.statusEffect));
		}
		return string.Join("  ·  ", traits);
	}

	// The values the card prints once for every behaviour.
	private static string Stats(EnemyMove move) {
		List<string> stats = new List<string>();

		if (!IsMovement(move)) stats.Add($"Range {RangeText(move.attackRange)}");
		if (move.dodgeDifficulty > 0) stats.Add($"Dodge {move.dodgeDifficulty}");
		return string.Join("  ·  ", stats);
	}

	private static string RangeText(int range) =>
		range >= EnemyData.UNLIMITED_RANGE ? "∞" : range.ToString();

	// Enemies whose range circle shows a dash have no attack behaviour at all.
	private static string CardRange(EnemyData data) {
		foreach (EnemyMove move in data.moves) {
			if (!IsMovement(move)) return RangeText(move.attackRange);
		}
		return "—";
	}

	private static string ConditionName(EncounterManager.StatusEffect condition) => condition switch {
		EncounterManager.StatusEffect.BLEED => "Bleed",
		EncounterManager.StatusEffect.POISON => "Poison",
		EncounterManager.StatusEffect.FROST => "Frostbite",
		EncounterManager.StatusEffect.STAGGER => "Stagger",
		_ => "",
	};
}
