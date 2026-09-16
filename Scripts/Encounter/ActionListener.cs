using Godot;
using Godot.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public partial class ActionListener : Node
{
	private const float MAGIC_SIZE = 360 * 2;

	// Characters share one scene the same way enemies do; the Player resource assigned at
	// spawn is what makes it a Knight rather than a Herald.
	[Export] public PackedScene playerScene;

	// Used only when the encounter is run on its own, outside a campaign.
	[Export] public Array<Character> demoParty;

	// Every enemy shares one scene; the resource assigned at spawn is what makes it a
	// Hollow Soldier rather than a Sentinel.
	[Export] public PackedScene enemyScene;
	[Export] public Array<EnemyData> enemies;
	[Export] public Node nodesParent;
	[Export] public PathGrid pathGrid;
	[Export] public TurnQueue turnQueue;
	[Export] public CharacterTurn characterTurn;
	[Export] public EncounterResultPanel resultPanel;

	[Export] public Color soulDropColour = new Color(0.95f, 0.85f, 0.35f, 0.5f);

	private int playersSpawned = 0;
	private Player[] fallbackParty;
	private List<GameNode> entrances = new List<GameNode>();

	
	// Wired once for every node. Which clicks matter is decided by CharacterTurn, so the
	// entrance picker's own handlers stay separate.
	private void WireNodeClicks() {
	    foreach (Node child in nodesParent.GetChildren()) {
	        if (child is not GameNode gameNode) continue;
	        GameNode captured = gameNode;
	        if (captured.ClickTarget is BaseButton button) {
	            button.Pressed += () => OnNodeClicked(captured);
	        }
	    }
	}

	private void OnNodeClicked(GameNode node) {
	    if (EncounterManager.phase != EncounterManager.Action.CHARACTER_TURN) return;
	    characterTurn?.OnNodeClicked(node);
	}

	public void ToggleAllNodesOff() {
		foreach (Node node in nodesParent.GetChildren()) {
			Control button = (Control)node.GetChild(0);
			button.Modulate = new Color(0,0,0,0);
		}
	}

	public override void _Ready() {
		EncounterManager.Reset();
		EncounterManager.pathGrid = pathGrid;
		EncounterManager.enemyInfoModal = GetNode<AcceptDialog>("%Enemy Info Dialog");
		EncounterManager.enemyInfoPanel = GetNode<EnemyInfoPanel>("%Enemy Info Panel");
		EncounterManager.dodgePrompt = GetNode<DodgePrompt>("%Dodge Prompt");
		EncounterManager.pushPrompt = GetNode<PushPrompt>("%Push Prompt");

		ToggleAllNodesOff();
	    SpawnEnemies();

	    if (characterTurn != null) characterTurn.ActivationEnded += EndCharacterActivation;
	    WireNodeClicks();

	}

	public void SpawnEnemies() {
	    Random random = new Random();
	    List<GameNode> enemyNodes = new List<GameNode>();

	    foreach (GameNode gameNode in nodesParent.GetChildren()) {
	        EncounterManager.nodes.Add(gameNode);
	        if (gameNode.isEnemySpawn) {
	            enemyNodes.Add(gameNode);
	        }

	        // Needs choice here
	        if (gameNode.isEntrance) {
	            entrances.Add(gameNode);
	        }
	    }

	    // Spawn randomly
	    foreach (EnemyData enemyData in enemies) {
	        int i = random.Next(enemyNodes.Count);
	        Control node = enemyNodes[i];
			Node2D enemy = (Node2D)enemyScene.Instantiate();
			// Must be set before the node enters the tree, since _Ready builds the
			// token's visuals from it.
			Enemy spawned = enemy.GetChild<Enemy>(0);
			spawned.data = enemyData;
			spawned.ActivationFinished += OnEnemyActivationFinished;

			float nodeSize = node.Size.X;
			enemy.Scale = new Vector2(nodeSize / MAGIC_SIZE, nodeSize / MAGIC_SIZE);
			EncounterManager.MovePlayer(enemy, node, null);
	        EncounterManager.enemies.Add(enemy);
	    }

	    EncounterManager.enemies = EncounterManager.enemies.OrderByDescending(e => e.GetChild<Enemy>(0).threatLevel).ToList();
	    EncounterManager.action = EncounterManager.Action.PICK_ENTRANCE;
	    EncounterManager.phase = EncounterManager.Action.PICK_ENTRANCE;
	    RestoreSoulDrop();
	}

	// Souls dropped here by an earlier wipe reappear on the node they fell on.
	private void RestoreSoulDrop() {
	    string worldNodeId = WorldMapManager.PendingEncounterNodeId;
	    if (!SoulCache.HasDropIn(worldNodeId)) return;

	    EncounterManager.soulDropGridIndex = SoulCache.droppedGridIndex;
	    EncounterManager.GameNodeAtIndex(EncounterManager.soulDropGridIndex)?.Highlight(soulDropColour);
	}

	public void PickEntrance() {
	    if (playersSpawned >= GetParty().Length) {
	        return;
	    }

	    foreach (GameNode entrance in entrances) {
			entrance.ToggleButton(true);
			BaseButton button = (BaseButton)entrance.GetChild(0);
			button.Pressed += () => { SpawnPlayer(entrance); };
	    }
	}
	public void SpawnPlayer(Control entrance) {
	    Player[] party = GetParty();
		Node2D player = (Node2D)playerScene.Instantiate();
		// Assigned before the node enters the tree, since _Ready builds the token from it.
		player.GetChild<PlayerToken>(0).player = party[playersSpawned];

		float nodeSize = entrance.Size.X;
		player.Scale = new Vector2(nodeSize / MAGIC_SIZE, nodeSize / MAGIC_SIZE);
		EncounterManager.MovePlayer(player, entrance, null);
	    EncounterManager.players.Add(player);
	    playersSpawned++;

	    if (playersSpawned == party.Length) {
	        foreach (Node e in entrances) {
	            (e as GameNode).ToggleButton(false);
	        }
	        EncounterManager.action = EncounterManager.Action.ENEMY_MOVE;
	        EncounterManager.phase = EncounterManager.Action.ENEMY_MOVE;
	    }
	}

	// The campaign party when there is one, otherwise a throwaway party built from
	// demoParty so the encounter scene can be run and tested on its own.
	private Player[] GetParty() {
	    if (CampaignManager.Players != null && CampaignManager.Players.Length > 0) {
	        return CampaignManager.Players;
	    }
	    if (demoParty == null || demoParty.Count == 0) return new Player[0];

	    if (fallbackParty == null) {
	        // CharacterSelect mints an equipment instance per starting slot; without that a
	        // demo character has no weapons and nothing to attack with.
	        GameManager.EnsureCatalogLoaded();
	        GameManager.ResetOwnedPool();

	        fallbackParty = new Player[demoParty.Count];
	        for (int i = 0; i < demoParty.Count; i++) {
	            Character c = demoParty[i];
	            Player p = new Player(c.name, c);
	            p.leftHandId   = MintAndId(c.leftHandDefault);
	            p.rightHandId  = MintAndId(c.rightHandDefault);
	            p.backupSlotId = MintAndId(c.backupSlotDefault);
	            p.armourId     = MintAndId(c.armourDefault);
	            fallbackParty[i] = p;
	        }
	    }
	    return fallbackParty;
	}

	private static string MintAndId(Equipment template) {
	    if (template == null || string.IsNullOrEmpty(template.name)) return "";
	    return GameManager.MintInstance(template.name)?.id ?? "";
	}

	public void EnemyMove() {
	    Node2D aggroPlayer = EncounterManager.players[0];
	    List<Node2D> nonAggroPlayers = new List<Node2D>();
	    foreach (Node2D playerObj in EncounterManager.players) {
	        PlayerToken token = EncounterManager.GetPlayerToken(playerObj);
	        if (token != null && token.hasAggro) {
	            aggroPlayer = playerObj;
	        }
	        // "Nearest character" means any character, so the aggro holder belongs here too.
	        nonAggroPlayers.Add(playerObj);
	    }

	    Enemy enemy = EncounterManager.GetEnemy(EncounterManager.enemies[EncounterManager.activeEnemyIndex]);
	    if (enemy == null) {
	        OnEnemyActivationFinished();
	        return;
	    }
	    enemy.SetActivating(true);
	    enemy.DoMoves(aggroPlayer, nonAggroPlayers);
	}

	// ----- Turn loop -----
	//
	// Rules p19: every enemy activates, in threat order, then exactly one character
	// activates. Enemy movement resolves over several frames inside Enemy._Process, so the
	// loop is driven by ActivationFinished rather than by running straight through.

	private void BeginEnemyActivation() {
	    EncounterManager.PruneDeadEnemies();
	    if (CheckEncounterOver()) return;

	    if (EncounterManager.activeEnemyIndex >= EncounterManager.enemies.Count) {
	        BeginCharacterPhase();
	        return;
	    }
	    EnemyMove();
	}

	private void OnEnemyActivationFinished() {
	    if (EncounterManager.activeEnemyIndex < EncounterManager.enemies.Count) {
	        EncounterManager.GetEnemy(EncounterManager.enemies[EncounterManager.activeEnemyIndex])?.EndActivation();
	    }
	    EncounterManager.activeEnemyIndex++;
	    EncounterManager.action = EncounterManager.Action.ENEMY_MOVE;
	}

	private void BeginCharacterPhase() {
	    EncounterManager.activeEnemyIndex = 0;
	    EncounterManager.phase = EncounterManager.Action.CHARACTER_TURN;
	    EncounterManager.action = EncounterManager.Action.CHARACTER_TURN;
	}

	private void BeginCharacterActivation() {
	    if (CheckEncounterOver()) return;

	    PlayerToken token = ActiveCharacter();
	    if (token == null) {
	        EndCharacterActivation();
	        return;
	    }
	    token.BeginActivation();
	    characterTurn?.Begin(token);
	}

	// Wired to the HUD's End Activation button; the character phase waits on the player.
	public void EndCharacterActivation() {
	    if (EncounterManager.phase != EncounterManager.Action.CHARACTER_TURN) return;

	    characterTurn?.End();
	    ActiveCharacter()?.EndActivation();

	    if (EncounterManager.players.Count > 0) {
	        EncounterManager.activeCharacterIndex =
	            (EncounterManager.activeCharacterIndex + 1) % EncounterManager.players.Count;
	    }
	    EncounterManager.round++;

	    if (CheckEncounterOver()) return;
	    EncounterManager.phase = EncounterManager.Action.ENEMY_MOVE;
	    EncounterManager.action = EncounterManager.Action.ENEMY_MOVE;
	}

	private PlayerToken ActiveCharacter() {
	    if (EncounterManager.players.Count == 0) return null;
	    int i = Mathf.Clamp(EncounterManager.activeCharacterIndex, 0, EncounterManager.players.Count - 1);
	    return EncounterManager.GetPlayerToken(EncounterManager.players[i]);
	}

	// A character dying defeats the party immediately (p19/p20).
	private bool CheckEncounterOver() {
	    if (EncounterManager.partyDefeated) {
	        EncounterManager.phase = EncounterManager.Action.ENCOUNTER_LOST;
	        EndEncounter();
	        return true;
	    }
	    if (EncounterManager.enemies.Count == 0) {
	        EncounterManager.phase = EncounterManager.Action.ENCOUNTER_WON;
	        EndEncounter();
	        return true;
	    }
	    return false;
	}

	// p19/p21. Conditions come off every model, then the outcome is settled: a win clears
	// the endurance bars and pays the party, a wipe drops the whole cache where the
	// character fell. The world map is told last, because reporting consumes the pending
	// encounter id that both branches need.
	private void EndEncounter() {
	    characterTurn?.End();
	    bool won = EncounterManager.phase == EncounterManager.Action.ENCOUNTER_WON;

	    foreach (Node2D playerObj in EncounterManager.players) {
	        EncounterManager.GetPlayerToken(playerObj)?.ClearAllConditions();
	    }
	    foreach (Node2D enemyObj in EncounterManager.enemies) {
	        EncounterManager.GetEnemy(enemyObj)?.ClearAllConditions();
	    }

	    WorldNodeData node = WorldMapManager.GetPendingEncounterNode();
	    string worldNodeId = WorldMapManager.PendingEncounterNodeId;
	    List<string> lines = new List<string>();

	    if (won) {
	        AwardVictory(node, lines);
	        WorldMapManager.ReportEncounterWon();
	    } else {
	        SettleDefeat(worldNodeId, lines);
	        WorldMapManager.ReportPartyDeath();
	    }

	    lines.Add($"Soul cache: {SoulCache.current}");
	    resultPanel?.ShowResult(won, lines);
	}

	private void AwardVictory(WorldNodeData node, List<string> lines) {
	    // p19: every black and red cube comes off the endurance bars on a win.
	    foreach (Node2D playerObj in EncounterManager.players) {
	        EncounterManager.GetPlayerToken(playerObj)?.RestoreEndurance();
	    }
	    lines.Add("Endurance bars cleared.");

	    // A boss win pays 1 soul per character per remaining bonfire spark (p19), and
	    // sparks do not exist yet — so pay nothing rather than bake in a wrong number.
	    if (node != null && node.encounterType == WorldEncounterType.BOSS) {
	        lines.Add("Boss rewards need the spark system; no souls awarded.");
	        return;
	    }

	    int earned = EncounterManager.players.Count * SoulCache.SOULS_PER_CHARACTER;
	    SoulCache.Award(earned);
	    lines.Add($"{earned} souls earned.");
	}

	private void SettleDefeat(string worldNodeId, List<string> lines) {
	    int carried = SoulCache.current;
	    int lost = SoulCache.droppedAmount;

	    SoulCache.DropOnDeath(worldNodeId, EncounterManager.deathGridIndex);

	    if (lost > 0) lines.Add($"{lost} souls left from an earlier death were lost.");
	    lines.Add(carried > 0
	        ? $"{carried} souls dropped where you fell — walk back to reclaim them."
	        : "No souls were being carried.");
	}

	public override void _Process(double delta) {
	    // Cleared before dispatching, so a handler that finishes synchronously can queue the
	    // next step without this method stamping INACTIVE back over it.
	    EncounterManager.Action pending = EncounterManager.action;
	    if (pending == EncounterManager.Action.INACTIVE) return;
	    EncounterManager.action = EncounterManager.Action.INACTIVE;

	    switch (pending) {
	        case EncounterManager.Action.PICK_ENTRANCE:
	            PickEntrance();
	            break;
	        case EncounterManager.Action.ENEMY_MOVE:
	            BeginEnemyActivation();
	            break;
	        case EncounterManager.Action.CHARACTER_TURN:
	            BeginCharacterActivation();
	            break;
	    }
	}
}
