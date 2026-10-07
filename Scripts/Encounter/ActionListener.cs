using Godot;
using Godot.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public partial class ActionListener : Control
{
	// Characters share one scene the same way enemies do; the Player resource assigned at
	// spawn is what makes it a Knight rather than a Herald.
	[Export] public PackedScene playerScene;

	// Used only when the encounter is run on its own, outside a campaign.
	[Export] public Array<Character> demoParty;
	// The soul counter hangs under this bar's right end.
	[Export] public Control titleBar;

	// Every enemy shares one scene; the resource assigned at spawn is what makes it a
	// Hollow Soldier rather than a Sentinel.
	[Export] public PackedScene enemyScene;
	// Standalone runs only: a campaign encounter spawns its saved EncounterPlan instead.
	[Export] public Array<EnemyData> enemies;
	[Export] public Node nodesParent;
	// The printed tile under the grid; a plan names its own.
	[Export] public TextureRect boardArt;
	[Export] public PathGrid pathGrid;
	[Export] public TurnQueue turnQueue;
	[Export] public CharacterTurn characterTurn;
	[Export] public EncounterResultPanel resultPanel;

	[Export] public Color soulDropColour = new Color(0.95f, 0.85f, 0.35f, 0.5f);

	private int playersSpawned = 0;
	// Placement (p19): the selected character goes on the next entrance clicked. The order they
	// are placed in is the activation order for this encounter (Matt's call: chosen afresh
	// every encounter, so there is no First Activation token), and whoever is placed first led
	// the way in and starts with the Aggro token.
	private int placing = 0;
	private readonly HashSet<int> placed = new HashSet<int>();
	private Player[] fallbackParty;
	private List<GameNode> entrances = new List<GameNode>();
	private CharacterPortraitPane portraitPane;
	private PhaseBanner phaseBanner;

	[Export] public Color enemyBannerGlow = new Color(0.62f, 0.15f, 0.09f);
	[Export] public Color adventurerBannerGlow = new Color(0.72f, 0.46f, 0.2f);
	[Export] public Color victoryBannerGlow = new Color(0.85f, 0.62f, 0.25f);
	[Export] public Color victoryTextColour = new Color(0.93f, 0.85f, 0.62f);
	[Export] public Color deathBannerGlow = new Color(0.42f, 0.05f, 0.03f);
	[Export] public Color deathTextColour = new Color(0.72f, 0.1f, 0.06f);
	private SoulCounter soulCounter;

	
	// Wired once for every node, and the only click handler nodes have: the phase decides
	// what a click means. Entrances used to add their own handler per pick and never remove
	// it, so any later click on an entrance node tried to spawn past the end of the party.
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
	    // Where a pushed model goes is asked mid-attack, on either side's turn.
	    if (EncounterManager.pushPrompt != null && EncounterManager.pushPrompt.isChoosingNode) {
	        EncounterManager.pushPrompt.OnNodeClicked(node);
	        return;
	    }
	    // A heal or a rider choosing characters takes a node click as a pick.
	    if (characterTurn != null && characterTurn.isPicking) {
	        characterTurn.OnPickNode(node);
	        return;
	    }
	    // A dodge's free step happens inside the enemy phase, so it is asked before the phase.
	    if (characterTurn != null && characterTurn.isDodgeStepping) {
	        characterTurn.OnDodgeStepNodeClicked(node);
	        return;
	    }
	    if (EncounterManager.phase == EncounterManager.Action.PICK_ENTRANCE) {
	        if (entrances.Contains(node)) SpawnPlayer(node);
	        return;
	    }
	    if (EncounterManager.phase != EncounterManager.Action.CHARACTER_TURN) return;
	    characterTurn?.OnNodeClicked(node);
	}

	public void ToggleAllNodesOff() {
		foreach (Node node in nodesParent.GetChildren()) {
			if (node is GameNode gameNode) gameNode.ClearHighlight();
		}
	}

	public override void _Ready() {
		EncounterManager.Reset();
		EncounterManager.pathGrid = pathGrid;
		// CharacterTurn registers itself in its own _Ready, which runs before this one and
		// so gets wiped by Reset; without this every enemy click saw no armed attack.
		EncounterManager.characterTurn = characterTurn;
		EncounterManager.dodgePrompt = GetNode<DodgePrompt>("%Dodge Prompt");
		EncounterManager.pushPrompt = GetNode<PushPrompt>("%Push Prompt");
		EncounterManager.spotlight = new EnemySpotlight();
		AddChild(EncounterManager.spotlight);
		phaseBanner = new PhaseBanner();
		AddChild(phaseBanner);
		BoardFx.punch = new ScreenPunch();
		AddChild(BoardFx.punch);

		ToggleAllNodesOff();
	    SpawnEnemies();

	    if (characterTurn != null) {
	        characterTurn.ActivationEnded += EndCharacterActivation;
	        characterTurn.AttackResolved += OnAttackResolved;
	    }
	    WireNodeClicks();

	    portraitPane = GetNodeOrNull<CharacterPortraitPane>("/root/CharacterPortraitPane");
	    if (portraitPane != null && (CampaignManager.Players == null || CampaignManager.Players.Length == 0)) {
	        portraitPane.standaloneParty = GetParty();
	    }
	    portraitPane?.Show();
	    soulCounter = GetNodeOrNull<SoulCounter>("/root/SoulCounter");
	    soulCounter?.ShowBelow(titleBar);
	    if (portraitPane != null) portraitPane.PortraitClicked += SelectCharacter;
	    // Deferred: the action bar sits later in the tree and builds its row styles in its own _Ready.
	    CallDeferred(nameof(SelectCharacter), 0);
	    if (turnQueue != null) turnQueue.Resized += () => { CallDeferred(nameof(PlacePortraitPane)); };
	    CallDeferred(nameof(PlacePortraitPane));
	}


	// The pane is an autoload and outlives this scene, so its signal has to let go of us.
	public override void _ExitTree() {
	    if (portraitPane != null) {
	        portraitPane.PortraitClicked -= SelectCharacter;
	        portraitPane.standaloneParty = null;
	    }
	    soulCounter?.Release(titleBar);
	}

	// The action bar always shows someone: the first character until a portrait is clicked
	// or an activation brings its character forward.
	private void SelectCharacter(int index) {
	    Player[] party = GetParty();
	    if (index < 0 || index >= party.Length) return;
	    if (EncounterManager.phase == EncounterManager.Action.PICK_ENTRANCE && !placed.Contains(index)) placing = index;
	    characterTurn?.actionBar?.Select(party[index]);
	    portraitPane?.SetSelectedIndex(index);
	}

	// The pane is an autoload floating over the whole window with no idea what scene is up,
	// so it has to be told where this one's HUD ends or it sits on top of the turn queue.
	public void PlacePortraitPane() {
	    if (portraitPane == null || turnQueue == null) return;

	    float hudBottom = turnQueue.GetGlobalRect().End.Y;
	    if (hudBottom > 0f) portraitPane.SetTopMargin((int)hudBottom + 12);
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

	    EncounterPlan plan = WorldMapManager.GetPendingPlan();
	    if (plan != null && enemyNodes.Count > 0) {
	        if (boardArt != null && ResourceLoader.Exists(plan.TilePath)) boardArt.Texture = ResourceLoader.Load<Texture2D>(plan.TilePath);
	        foreach (EncounterSpawn spawn in plan.spawns) {
	            if (spawn.Data == null) continue;
	            SpawnEnemy(spawn.Data, spawn.tier, enemyNodes[spawn.spawnSlot % enemyNodes.Count]);
	        }
	    } else {
	        foreach (EnemyData enemyData in enemies) {
	            SpawnEnemy(enemyData, 1, enemyNodes[random.Next(enemyNodes.Count)]);
	        }
	    }

	    EncounterManager.enemies = EncounterManager.enemies.OrderByDescending(e => e.GetChild<Enemy>(0).threatLevel).ToList();
	    EncounterManager.action = EncounterManager.Action.PICK_ENTRANCE;
	    EncounterManager.phase = EncounterManager.Action.PICK_ENTRANCE;
	    RestoreSoulDrop();
	}

	private void SpawnEnemy(EnemyData enemyData, int tier, Control node) {
		Node2D enemy = (Node2D)enemyScene.Instantiate();
		// Must be set before the node enters the tree, since _Ready builds the
		// token's visuals and health from them.
		Enemy spawned = enemy.GetChild<Enemy>(0);
		spawned.data = enemyData;
		spawned.tier = tier;
		spawned.ActivationFinished += OnEnemyActivationFinished;

		EncounterManager.ScaleToken(enemy, node.Size.X);
		EncounterManager.MovePlayer(enemy, node, null);
		EncounterManager.enemies.Add(enemy);
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
	    }
	}
	public void SpawnPlayer(Control entrance) {
	    Player[] party = GetParty();
	    if (playersSpawned >= party.Length) return;
	    int index = placed.Contains(placing) || placing < 0 || placing >= party.Length ? NextUnplaced(party) : placing;
	    if (index < 0) return;

		Node2D player = (Node2D)playerScene.Instantiate();
		PlayerToken token = player.GetChild<PlayerToken>(0);
		// Assigned before the node enters the tree, since _Ready builds the token from it.
		token.player = party[index];

		EncounterManager.ScaleToken(player, entrance.Size.X);
		EncounterManager.MovePlayer(player, entrance, null);
	    EncounterManager.players.Add(player);
	    placed.Add(index);
	    playersSpawned++;
	    if (playersSpawned == 1) EncounterManager.SetAggroHolder(token);

	    // The next to place comes up on the bar and the pane; a portrait click changes it.
	    int next = NextUnplaced(party);
	    if (next >= 0) SelectCharacter(next);

	    if (playersSpawned == party.Length) {
	        foreach (Node e in entrances) {
	            (e as GameNode).ToggleButton(false);
	        }
	        EncounterManager.action = EncounterManager.Action.ENEMY_MOVE;
	        EncounterManager.phase = EncounterManager.Action.ENEMY_MOVE;
	    }
	}

	private int NextUnplaced(Player[] party) {
	    for (int i = 0; i < party.Length; i++) {
	        if (!placed.Contains(i)) return i;
	    }
	    return -1;
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
	            p.backupIds = Player.OneBackup(MintAndId(c.backupSlotDefault));
	            foreach (Weapon extra in c.extraBackupDefaults) {
	                string id = MintAndId(extra);
	                if (!string.IsNullOrEmpty(id)) p.backupIds = Player.Append(p.backupIds, id);
	            }
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

	// Each phase opens on its banner, and nothing moves until it has faded.
	private async void BeginEnemyActivation() {
	    EncounterManager.PruneDeadEnemies();
	    if (CheckEncounterOver()) return;

	    if (EncounterManager.activeEnemyIndex >= EncounterManager.enemies.Count) {
	        BeginCharacterPhase();
	        return;
	    }
	    if (EncounterManager.activeEnemyIndex == 0 && phaseBanner != null) {
	        await phaseBanner.Show("Enemy Turn", enemyBannerGlow);
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

	private async void BeginCharacterActivation() {
	    if (CheckEncounterOver()) return;

	    PlayerToken token = ActiveCharacter();
	    if (token == null) {
	        EndCharacterActivation();
	        return;
	    }
	    if (phaseBanner != null) await phaseBanner.Show($"{token.player?.name}'s Turn", adventurerBannerGlow);
	    if (!GodotObject.IsInstanceValid(token)) return;
	    // Magic Barrier and the like last "until the next character activation".
	    foreach (Player member in GetParty()) member.defenceBuffs.Clear();
	    token.BeginActivation();
	    portraitPane?.SetSelectedIndex(EncounterManager.activeCharacterIndex);
	    characterTurn?.Begin(token);
	}

	// Wired to the HUD's End Activation button; the character phase waits on the player.
	// Async for Faraam Armour, whose wearer is asked as another character's activation ends.
	private bool endingActivation;

	public async void EndCharacterActivation() {
	    if (EncounterManager.phase != EncounterManager.Action.CHARACTER_TURN || endingActivation) return;
	    endingActivation = true;

	    characterTurn?.End();
	    PlayerToken ended = ActiveCharacter();
	    ended?.EndActivation();
	    await OfferFaraam(ended);
	    endingActivation = false;

	    if (EncounterManager.players.Count > 0) {
	        EncounterManager.activeCharacterIndex =
	            (EncounterManager.activeCharacterIndex + 1) % EncounterManager.players.Count;
	    }
	    EncounterManager.round++;

	    if (CheckEncounterOver()) return;
	    EncounterManager.phase = EncounterManager.Action.ENEMY_MOVE;
	    EncounterManager.action = EncounterManager.Action.ENEMY_MOVE;
	}

	// Faraam Armour: "once per encounter at the end of another character's activation, you
	// may take the Aggro token".
	private async System.Threading.Tasks.Task OfferFaraam(PlayerToken ended) {
	    CharacterActionBar bar = characterTurn?.actionBar;
	    if (bar == null || EncounterManager.partyDefeated) return;
	    foreach (Node2D model in EncounterManager.players) {
	        PlayerToken wearer = EncounterManager.GetPlayerToken(model);
	        if (wearer == null || wearer == ended || wearer.faraamUsed) continue;
	        if (!wearer.player.HasPassive(EquipmentEffect.EffectType.MAY_TAKE_AGGRO, EquipmentEffect.Condition.ON_END_ACTIVATION)) continue;
	        TextureRect aggro = new TextureRect {
	            Texture = bar.AggroArt, CustomMinimumSize = new Vector2(22, 22), Material = PlayerToken.AggroMask,
	            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
	            MouseFilter = MouseFilterEnum.Ignore,
	        };
	        int choice = await bar.AskChoice(wearer.player, null, new List<(Control, string)> { (aggro, "Take the Aggro token") }, "Skip");
	        if (choice != 0 || !GodotObject.IsInstanceValid(wearer)) continue;
	        wearer.faraamUsed = true;
	        EncounterManager.SetAggroHolder(wearer);
	        return;
	    }
	}

	// The last enemy dying ends the encounter there and then, not at End Activation.
	private void OnAttackResolved() {
	    if (EncounterManager.phase != EncounterManager.Action.CHARACTER_TURN) return;
	    EncounterManager.PruneDeadEnemies();
	    CheckEncounterOver();
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
	    portraitPane?.SetSelectedIndex(-1);
	    bool won = EncounterManager.phase == EncounterManager.Action.ENCOUNTER_WON;

	    foreach (Node2D playerObj in EncounterManager.players) {
	        EncounterManager.GetPlayerToken(playerObj)?.ClearAllConditions();
	    }
	    foreach (Node2D enemyObj in EncounterManager.enemies) {
	        EncounterManager.GetEnemy(enemyObj)?.ClearAllConditions();
	    }

	    WorldNodeData node = WorldMapManager.GetPendingEncounterNode();
	    string worldNodeId = WorldMapManager.PendingEncounterNodeId;

	    if (won) {
	        int? earned = AwardVictory(node);
	        WorldMapManager.ReportEncounterWon();
	        ShowOutcome(true, earned, SoulCache.current);
	    } else {
	        int dropped = SettleDefeat(worldNodeId);
	        WorldMapManager.ReportPartyDeath();
	        ShowOutcome(false, dropped, SoulCache.current);
	    }
	}

	// The banner first — gold "Victory", or the games' red "You Died" — and the numbers once
	// it has gone. The outcome itself is already settled and reported by then.
	private async void ShowOutcome(bool won, int? change, int total) {
	    if (phaseBanner != null) {
	        if (won) await phaseBanner.ShowOutcome("Victory", victoryBannerGlow, victoryTextColour);
	        else await phaseBanner.ShowOutcome("You Died", deathBannerGlow, deathTextColour);
	    }
	    if (!IsInsideTree() || resultPanel == null) return;
	    if (won) resultPanel.ShowVictory(change, total);
	    else resultPanel.ShowDefeat(change ?? 0, total);
	}

	// Returns the souls paid, or null when the win pays nothing yet.
	private int? AwardVictory(WorldNodeData node) {
	    // p19: every black and red cube comes off the endurance bars on a win.
	    foreach (Node2D playerObj in EncounterManager.players) {
	        EncounterManager.GetPlayerToken(playerObj)?.RestoreEndurance();
	    }

	    // A boss win pays 1 soul per character per remaining bonfire spark (p19), and
	    // sparks are cut — so pay nothing rather than bake in a wrong number.
	    if (node != null && node.encounterType == WorldEncounterType.BOSS) return null;

	    int earned = EncounterManager.players.Count * SoulCache.SOULS_PER_CHARACTER;
	    // Covetous Silver Serpent Ring: 1 more soul for each worn.
	    foreach (Node2D playerObj in EncounterManager.players) {
	        earned += EncounterManager.GetPlayerToken(playerObj)?.player?.RingCount(Ring.Effect.COVETOUS_SILVER_SERPENT) ?? 0;
	    }
	    SoulCache.Award(earned);
	    return earned;
	}

	// Returns how many souls were dropped where the character fell. An older pile still
	// lying somewhere is discarded by DropOnDeath (p19).
	private int SettleDefeat(string worldNodeId) {
	    int carried = SoulCache.current;
	    SoulCache.DropOnDeath(worldNodeId, EncounterManager.deathGridIndex);
	    return carried;
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
