using Godot;
using System.Collections.Generic;

// The board as it stood at the last turn boundary: the start of an enemy phase or of a
// character activation (Matt, Oct 2026). The turn loop is a chain of awaited steps (walks,
// the Block-or-Dodge prompt, the roll reveal) that cannot be resumed from the middle, so a
// fight is only ever saved between them. Loading puts the board back and begins that phase
// again; quitting mid-activation replays the activation from its start.
//
// Endurance is not here: SaveGame keeps every character's bar, in and out of encounters.
[GlobalClass]
public partial class EncounterSnapshot : Resource
{
	[Export] public string worldNode = "";
	// False: the enemy phase is next. True: the activation of activeCharacterIndex is.
	[Export] public bool characterPhase;
	[Export] public int round = 1;
	[Export] public int activeCharacterIndex;
	// Index into characters, or -1.
	[Export] public int aggroIndex = -1;
	// In activation order, which is placement order (p19).
	[Export] public CharacterState[] characters = new CharacterState[0];
	// In activation order, which is threat order as it was sorted on entry.
	[Export] public EnemyState[] enemies = new EnemyState[0];
	// Abyss tokens on the board, as grid index / enemy phases left pairs.
	[Export] public int[] abyss = new int[0];
	// Grid indices of the pillars smashed so far (Pillars of Woe).
	[Export] public int[] pillarsDown = new int[0];

	public static EncounterSnapshot Capture(string worldNode, bool characterPhase, Player[] party) {
		EncounterSnapshot snapshot = new EncounterSnapshot {
			worldNode = worldNode,
			characterPhase = characterPhase,
			round = EncounterManager.round,
			activeCharacterIndex = EncounterManager.activeCharacterIndex,
		};

		List<CharacterState> characters = new List<CharacterState>();
		foreach (Node2D model in EncounterManager.players) {
			PlayerToken token = EncounterManager.GetPlayerToken(model);
			if (token == null) continue;
			if (token == EncounterManager.aggroHolder) snapshot.aggroIndex = characters.Count;
			characters.Add(CharacterState.Of(token, System.Array.IndexOf(party, token.player)));
		}
		snapshot.characters = characters.ToArray();

		List<EnemyState> enemies = new List<EnemyState>();
		foreach (Node2D model in EncounterManager.enemies) {
			Enemy enemy = EncounterManager.GetEnemy(model);
			if (enemy != null) enemies.Add(EnemyState.Of(enemy));
		}
		snapshot.enemies = enemies.ToArray();

		List<int> abyss = new List<int>();
		for (int i = 0; i < EncounterManager.gridSize * EncounterManager.gridSize; i++) {
			GameNode node = EncounterManager.GameNodeAtIndex(i);
			if (node != null && node.abyss > 0) abyss.AddRange(new[] { i, node.abyss });
		}
		snapshot.abyss = abyss.ToArray();
		List<int> down = new List<int>();
		for (int i = 0; i < EncounterManager.gridSize * EncounterManager.gridSize; i++) {
			if (EncounterManager.GameNodeAtIndex(i)?.pillar == GameNode.Pillar.BROKEN) down.Add(i);
		}
		snapshot.pillarsDown = down.ToArray();
		return snapshot;
	}

	public static int[] ConditionsOf(IEnumerable<EncounterManager.StatusEffect> conditions) {
		List<int> result = new List<int>();
		foreach (EncounterManager.StatusEffect condition in conditions) result.Add((int)condition);
		return result.ToArray();
	}
}
