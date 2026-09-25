using Godot;
using System.Collections.Generic;

// The Enemy Activation bar: every enemy in threat order, high to low.
//
// No Party entry — all the enemies act between every character activation, so the party's
// place in the order is fixed and showing it says nothing. The board tokens carry no chrome,
// so this is also where an enemy's threat, tier, health and conditions are read.
public partial class TurnQueue : Control
{

	[Export] public PackedScene entryScene;
	[Export] public Container entriesContainer;
	[Export] public Label roundLabel;
	[Export] public EnemyCardViewer cardViewer;

	private readonly List<TurnQueueEntry> enemyEntries = new List<TurnQueueEntry>();

	private int builtForCount = -1;
	private int shownIndex = -1;
	private int shownRound = -1;
	private EncounterManager.Action shownPhase = EncounterManager.Action.INACTIVE;

	public override void _Process(double delta) {
		if (EncounterManager.enemies.Count != builtForCount) {
			Build();
		}
		if (EncounterManager.activeEnemyIndex != shownIndex
			|| EncounterManager.round != shownRound
			|| EncounterManager.phase != shownPhase) {
			Refresh();
		}
	}

	private void Build() {
		foreach (Node child in entriesContainer.GetChildren()) {
			entriesContainer.RemoveChild(child);
			child.QueueFree();
		}
		enemyEntries.Clear();

		foreach (Node2D enemyObj in EncounterManager.enemies) {
			Enemy enemy = GetEnemy(enemyObj);
			if (enemy == null) continue;

			TurnQueueEntry entry = entryScene.Instantiate<TurnQueueEntry>();
			entriesContainer.AddChild(entry);
			entry.Bind(enemy);
			entry.CardRequested += () => cardViewer?.ShowCard(enemy.data);
			enemyEntries.Add(entry);
		}

		builtForCount = EncounterManager.enemies.Count;
		shownIndex = -1;
		Refresh();
	}

	private void Refresh() {
		bool enemyPhase = EncounterManager.phase == EncounterManager.Action.ENEMY_MOVE;

		for (int i = 0; i < enemyEntries.Count; i++) {
			bool activating = enemyPhase && i == EncounterManager.activeEnemyIndex;
			bool spent = enemyPhase && i < EncounterManager.activeEnemyIndex;
			enemyEntries[i].SetState(activating, spent);
		}

		if (roundLabel != null) roundLabel.Text = $"Round {EncounterManager.round}";

		shownIndex = EncounterManager.activeEnemyIndex;
		shownRound = EncounterManager.round;
		shownPhase = EncounterManager.phase;
	}

	public TurnQueueEntry EntryFor(Enemy enemy) {
		foreach (TurnQueueEntry entry in enemyEntries) {
			if (entry.boundEnemy == enemy) return entry;
		}
		return null;
	}

	// EncounterManager.enemies still holds the wrapper Node2D of a dead enemy, so the
	// Enemy child can already be freed by the time the queue rebuilds.
	private Enemy GetEnemy(Node2D enemyObj) {
		if (!GodotObject.IsInstanceValid(enemyObj) || enemyObj.GetChildCount() == 0) return null;
		return enemyObj.GetChild(0) as Enemy;
	}
}
