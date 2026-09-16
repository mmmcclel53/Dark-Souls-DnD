using Godot;
using System;
using System.Collections.Generic;

public static partial class EncounterManager {

    public enum StatusEffect { BLEED, POISON, FROST, STAGGER, NONE };

    public enum Action { INACTIVE, PICK_ENTRANCE, ENEMY_MOVE, CHARACTER_TURN, ENCOUNTER_WON, ENCOUNTER_LOST };

    // `action` is a one-shot command that ActionListener clears as soon as it handles it.
    // `phase` is the sticky record of where the encounter actually is, for the HUD to read.
    public static Action action = Action.INACTIVE;
    public static Action phase = Action.INACTIVE;

    public static int activeEnemyIndex = 0;
    public static int activeCharacterIndex = 0;
    public static int round = 1;

    // A node cannot contain more than three models (p10).
    public const int MAX_MODELS_PER_NODE = 3;

    public static int gridSize = 7;
    public static List<Control> nodes = new List<Control>();

    public static List<Node2D> players = new List<Node2D>();
    public static bool isPlayerMoving = false;

    // Rules p22: the activating character holds the Aggro token, so this follows activation
    // rather than being assigned on its own.
    public static PlayerToken aggroHolder;
    public static PlayerToken selectedPlayer;
    public static bool partyDefeated = false;

    // Grid index the first character died on, so the soul cache knows where to drop.
    public static int deathGridIndex = -1;

    // Grid index holding a soul pile left by an earlier wipe, or -1.
    public static int soulDropGridIndex = -1;

    public static List<Node2D> enemies = new List<Node2D>();
    public static bool isEnemyMoving = false;

    public static PathGrid pathGrid;
    public static AcceptDialog enemyInfoModal;
    public static EnemyInfoPanel enemyInfoPanel;
    public static DodgePrompt dodgePrompt;
    public static CharacterTurn characterTurn;
    public static PushPrompt pushPrompt;

    // Settings
    public static bool showEnemyInfo = true;

    public static void Reset() {
        nodes.Clear();
        players.Clear();
        enemies.Clear();
        action = Action.INACTIVE;
        phase = Action.INACTIVE;
        activeEnemyIndex = 0;
        activeCharacterIndex = 0;
        round = 1;
        isPlayerMoving = false;
        isEnemyMoving = false;
        aggroHolder = null;
        characterTurn = null;
        pushPrompt = null;
        selectedPlayer = null;
        partyDefeated = false;
        deathGridIndex = -1;
        soulDropGridIndex = -1;
    }

    public static void SetAggroHolder(PlayerToken token) {
        PlayerToken previous = aggroHolder;
        aggroHolder = token;
        if (previous != null && GodotObject.IsInstanceValid(previous)) previous.RefreshAggro();
        if (token != null) token.RefreshAggro();
    }

    public static int GridIndexOf(Node2D model) {
        if (pathGrid == null || model == null) return -1;
        PathNode node = pathGrid.NodeFromObj(model);
        return (node.gridY * gridSize) + node.gridX;
    }

    public static GameNode GameNodeAtIndex(int index) {
        if (pathGrid == null || index < 0 || index >= gridSize * gridSize) return null;
        return pathGrid.GetChild<GameNode>(index);
    }

    // Walking onto the pile a previous wipe left behind puts those souls back (p19).
    public static int TryRetrieveSouls(GameNode node, Node2D arrival) {
        if (soulDropGridIndex < 0 || GetPlayerToken(arrival) == null) return 0;
        if (node == null || node != GameNodeAtIndex(soulDropGridIndex)) return 0;

        int recovered = SoulCache.Retrieve();
        soulDropGridIndex = -1;
        node.ClearHighlight();
        return recovered;
    }

    public static Enemy GetEnemy(Node2D enemyObj) {
        if (!GodotObject.IsInstanceValid(enemyObj) || enemyObj.GetChildCount() == 0) return null;
        return enemyObj.GetChild(0) as Enemy;
    }

    // Enemy.ApplyDamage frees the Enemy but leaves its wrapper Node2D behind, so the list
    // has to be swept or activation order keeps counting corpses.
    public static void PruneDeadEnemies() {
        for (int i = enemies.Count - 1; i >= 0; i--) {
            if (GetEnemy(enemies[i]) != null) continue;
            if (GodotObject.IsInstanceValid(enemies[i])) enemies[i].QueueFree();
            enemies.RemoveAt(i);
            if (activeEnemyIndex > i) activeEnemyIndex--;
        }
    }

    // p10: arriving on a node that already holds three models pushes one of those three
    // off it. The players choose which, so this waits on the prompt whoever moved in.
    public static async System.Threading.Tasks.Task ResolveOverflow(GameNode node, Node2D arrival) {
        if (node == null || NodeOccupancy(node) <= MAX_MODELS_PER_NODE) return;

        List<Node2D> candidates = new List<Node2D>();
        foreach (Node2D occupant in GetAllPlayersInNode(node)) {
            if (occupant != arrival) candidates.Add(occupant);
        }
        if (candidates.Count == 0) return;

        Node2D pushed = pushPrompt != null
            ? await pushPrompt.Ask(candidates, ArrivalName(arrival))
            : candidates[0];
        if (pushed == null) return;

        GameNode destination = EnemyMovement.PushDestination(pathGrid.NodeFromObj(pushed));
        if (destination != null && destination != node) MovePlayer(pushed, destination, node);
    }

    private static string ArrivalName(Node2D arrival) {
        Enemy enemy = GetEnemy(arrival);
        if (enemy != null) return enemy.data.enemyName;
        PlayerToken token = GetPlayerToken(arrival);
        return token != null ? token.player.name : arrival.Name;
    }

    // A hazard node applies its condition to anything that steps onto it. Not a rule from
    // the book — GameNode.statusEffect is this project's own idea.
    public static void ApplyNodeHazard(GameNode node, Node2D arrival) {
        if (node == null || node.statusEffect == StatusEffect.NONE) return;

        GetEnemy(arrival)?.ApplyCondition(node.statusEffect);
        GetPlayerToken(arrival)?.ApplyCondition(node.statusEffect);
    }

    public static PlayerToken GetPlayerToken(Node2D playerObj) {
        if (!GodotObject.IsInstanceValid(playerObj) || playerObj.GetChildCount() == 0) return null;
        return playerObj.GetChild(0) as PlayerToken;
    }

    public static int NodeOccupancy(Control node) => GetAllPlayersInNode(node).Count;

    public static bool IsNodeFull(Control node) => NodeOccupancy(node) >= MAX_MODELS_PER_NODE;

    // Nothing refuses entry: a full node is entered and then something already there is
    // pushed off (p10). Kept so callers can ask whether arriving will owe a push.
    public static bool WillOverfill(Control node, Node2D mover) {
        if (node == null) return false;
        if (mover != null && mover.GetParent() == node) return false;
        return IsNodeFull(node);
    }

    public static List<Node2D> GetAllPlayersInNode(Control node) {
        List<Node2D> result = new List<Node2D>();
        foreach (Node child in node.GetChildren()) {
            if (child.IsInGroup("Player") || child.IsInGroup("Enemy")) {
                result.Add(child as Node2D);
            }
        }
        return result;
    }

    public static List<Node2D> GetPlayersInNode(Control node, string groupName) {
        List<Node2D> result = new List<Node2D>();
        foreach (Node child in node.GetChildren()) {
            if (child.IsInGroup(groupName)) {
                result.Add(child as Node2D);
            }
        }
        return result;
    }

    // Unelegent piece of shit code
    public static void FixPositioning(Control node) {
        float nodeSize = node.Size.X;
        List<Node2D> allPlayers = GetAllPlayersInNode(node);
        if (allPlayers.Count == 1) {
            allPlayers[0].Position = new Vector2(nodeSize/4,nodeSize/4);
        } else if (allPlayers.Count == 2) {
            allPlayers[0].Position = new Vector2(0,nodeSize/4);
            allPlayers[1].Position = new Vector2(nodeSize/2,nodeSize/4);
        } else if (allPlayers.Count == 3) {
            allPlayers[0].Position = new Vector2(0,0);
            allPlayers[1].Position = new Vector2(nodeSize/2,0);
            allPlayers[2].Position = new Vector2(nodeSize/4,nodeSize/2);
        }
    }

    public static bool MovePlayer(Node2D obj, Control newNode, Control oldNode) {
        if (newNode == null) return false;

        if (oldNode != null) {
            oldNode.RemoveChild(obj);
            FixPositioning(oldNode);
        }

        Node btn = newNode.GetChild(-1);
        newNode.AddChild(obj);
        newNode.MoveChild(btn, -1);
        FixPositioning(newNode);
        return true;
    }
}
