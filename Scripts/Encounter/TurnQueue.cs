using Godot;
using System.Collections.Generic;

// Enemies activate in threat order, high to low, then one character activates.
// The queue is the only place that ordering is visible before it happens.
public partial class TurnQueue : Control
{

	[Export] public PackedScene entryScene;
	[Export] public Container entriesContainer;
	[Export] public Label phaseLabel;
	[Export] public Label roundLabel;

	private readonly List<TurnQueueEntry> enemyEntries = new List<TurnQueueEntry>();
	private TurnQueueEntry partyEntry;

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
			child.QueueFree();
		}
		enemyEntries.Clear();
		partyEntry = null;

		foreach (Node2D enemyObj in EncounterManager.enemies) {
			Enemy enemy = GetEnemy(enemyObj);
			if (enemy == null) continue;

			TurnQueueEntry entry = entryScene.Instantiate<TurnQueueEntry>();
			entriesContainer.AddChild(entry);
			entry.Fill(enemy.data.avatarTexture, enemy.data.enemyName, enemy.threatLevel);
			enemyEntries.Add(entry);
		}

		entriesContainer.AddChild(new VSeparator());

		partyEntry = entryScene.Instantiate<TurnQueueEntry>();
		entriesContainer.AddChild(partyEntry);
		partyEntry.Fill(null, "Party", -1);

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
		bool characterPhase = EncounterManager.phase == EncounterManager.Action.CHARACTER_TURN;
		if (partyEntry != null) partyEntry.SetState(characterPhase, false);

		if (phaseLabel != null) {
			phaseLabel.Text = EncounterManager.phase switch {
				EncounterManager.Action.PICK_ENTRANCE => "Choose Entrance",
				EncounterManager.Action.ENEMY_MOVE => "Enemy Activation",
				EncounterManager.Action.CHARACTER_TURN => "Character Activation",
				EncounterManager.Action.ENCOUNTER_WON => "Encounter Won",
				EncounterManager.Action.ENCOUNTER_LOST => "Party Defeated",
				_ => "",
			};
		}
		if (roundLabel != null) roundLabel.Text = $"Round {EncounterManager.round}";

		shownIndex = EncounterManager.activeEnemyIndex;
		shownRound = EncounterManager.round;
		shownPhase = EncounterManager.phase;
	}

	// EncounterManager.enemies still holds the wrapper Node2D of a dead enemy, so the
	// Enemy child can already be freed by the time the queue rebuilds.
	private Enemy GetEnemy(Node2D enemyObj) {
		if (!GodotObject.IsInstanceValid(enemyObj) || enemyObj.GetChildCount() == 0) return null;
		return enemyObj.GetChild(0) as Enemy;
	}
}
