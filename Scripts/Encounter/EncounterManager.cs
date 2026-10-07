using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

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

    // PlayerToken.tscn and Enemy.tscn both draw their art at this size. A token is scaled
    // to half a node so three of them fit side by side, which is what FixPositioning packs,
    // and an enemy is then scaled up by its Presence.
    public const float TOKEN_ART_SIZE = 360f;

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
    public static DodgePrompt dodgePrompt;
    public static CharacterTurn characterTurn;
    public static PushPrompt pushPrompt;
    public static EnemySpotlight spotlight;
    // Registers itself on ready; it sits after the board in the scene so Reset cannot wipe it.
    public static RollReveal rollReveal;

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
        spotlight = null;
        NearestMarker.Reset();
        characterTurn = null;
        pushPrompt = null;
        rollReveal = null;
        selectedPlayer = null;
        partyDefeated = false;
        deathGridIndex = -1;
        soulDropGridIndex = -1;
        BoardFx.Reset();
    }

    // The token flies from the last holder to the new one and only shows on landing; with
    // no one to fly from it simply appears.
    public static void SetAggroHolder(PlayerToken token) {
        PlayerToken previous = aggroHolder;
        aggroHolder = token;
        if (previous != null && GodotObject.IsInstanceValid(previous)) previous.RefreshAggro();
        if (token == null) return;
        if (previous != token && AggroHandoff.Fly(previous, token)) return;
        token.RefreshAggro();
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

    // A killed enemy is only queued for deletion and stays valid until the frame ends, so
    // it counts as gone from the moment it is queued — otherwise the last kill of an
    // activation would not end the encounter until some later phase check.
    public static Enemy GetEnemy(Node2D enemyObj) {
        if (!GodotObject.IsInstanceValid(enemyObj) || enemyObj.GetChildCount() == 0) return null;
        Enemy enemy = enemyObj.GetChild(0) as Enemy;
        return enemy == null || enemy.IsQueuedForDeletion() || enemy.isDead ? null : enemy;
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
        if (destination != null && destination != node) MovePlayer(pushed, destination, node, true);
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

    // Draws a token at half the node it stands on, so three fit side by side. The node is
    // a fraction of the board, so this has to be redone whenever the board resizes.
    public static void ScaleToken(Node2D model, float nodeSize) {
        if (model == null || nodeSize <= 0f) return;
        float scale = nodeSize / (TOKEN_ART_SIZE * 2f) * Presence(model);
        model.Scale = new Vector2(scale, scale);
    }

    public static float Presence(Node2D model) =>
        model != null && model.GetChildCount() > 0 && model.GetChild(0) is Enemy enemy ? enemy.Presence : 1f;

    // Only something this large (a boss) may overlap what shares its node; it takes the middle
    // and the others stand along its front edge. That overlap is where a boss's arcs will come in.
    public const float OVERLAP_PRESENCE = 3f;

    // A model's area on the board: the diamond of points nearer its node than any neighbour,
    // reaching one node width from the centre each way (the grid is a lattice turned 45°).
    // Kept a touch inside it, and models a touch apart, so tokens never touch.
    private const float REACH = 0.96f;
    private const float GAP = 1.06f;

    // Up to three models share a node (p10). Below boss size none overlap: they are packed
    // by their real sizes into the node's diamond, and only if they cannot fit do they all
    // shrink together, so a big enemy is at its full size whenever it stands alone.
    public static void FixPositioning(Control node) {
        float nodeSize = node.Size.X;
        List<Node2D> models = GetAllPlayersInNode(node);
        if (models.Count == 0) return;
        models = models.OrderByDescending(Presence).ToList();

        // The biggest is drawn first, so anything that does overlap stands over it.
        int first = models.Min(m => m.GetIndex());
        for (int i = 0; i < models.Count; i++) node.MoveChild(models[i], first + i);

        float baseScale = nodeSize / (TOKEN_ART_SIZE * 2f);
        if (Presence(models[0]) >= OVERLAP_PRESENCE) {
            Place(models[0], Vector2.Zero, baseScale * Presence(models[0]), nodeSize);
            Vector2[] front = models.Count == 2
                ? new[] { new Vector2(0f, 0.35f) }
                : new[] { new Vector2(-0.25f, 0.35f), new Vector2(0.25f, 0.35f), new Vector2(0f, -0.35f) };
            for (int i = 1; i < models.Count && i - 1 < front.Length; i++) {
                Place(models[i], front[i - 1], baseScale * Presence(models[i]), nodeSize);
            }
            return;
        }

        float[] radii = models.Select(m => 0.25f * Presence(m)).ToArray();
        (Vector2[] centres, float shrink) = Pack(radii);
        for (int i = 0; i < models.Count; i++) {
            Place(models[i], centres[i], baseScale * Presence(models[i]) * shrink, nodeSize);
        }
    }

    // Centres (from the node's centre, in node widths) for discs of these radii, and how much
    // they all have to shrink to fit. Tries each arrangement and keeps the roomiest; with
    // equal tokens the first, the triangle, is the old quarters layout.
    private static (Vector2[] centres, float shrink) Pack(float[] radii) {
        Vector2[] best = null;
        float bestShrink = -1f;
        foreach (Vector2[] layout in Layouts(radii.Length)) {
            float spread = 0f;
            for (int i = 0; i < layout.Length; i++) {
                for (int j = i + 1; j < layout.Length; j++) {
                    spread = Mathf.Max(spread, (radii[i] + radii[j]) * GAP / layout[i].DistanceTo(layout[j]));
                }
            }
            float shrink = 1f;
            for (int i = 0; i < layout.Length; i++) {
                Vector2 at = layout[i] * spread;
                float reach = Mathf.Abs(at.X) + Mathf.Abs(at.Y) + radii[i] * Mathf.Sqrt2;
                shrink = Mathf.Min(shrink, REACH / reach);
            }
            if (shrink > bestShrink + 0.001f) {
                bestShrink = shrink;
                best = layout.Select(b => b * spread * shrink).ToArray();
            }
        }
        return (best, bestShrink);
    }

    // Directions for each model, in the order the models are sorted (biggest first).
    private static IEnumerable<Vector2[]> Layouts(int count) {
        Vector2 left = new Vector2(-1f, 0f), right = new Vector2(1f, 0f);
        switch (count) {
            case 1:
                yield return new[] { Vector2.Zero };
                break;
            case 2:
                yield return new[] { left, right };
                break;
            case 3:
                Vector2 topLeft = new Vector2(-1f, -1f), topRight = new Vector2(1f, -1f), bottom = new Vector2(0f, 1f);
                yield return new[] { topLeft, topRight, bottom };
                yield return new[] { bottom, topLeft, topRight };
                yield return new[] { Vector2.Zero, left, right };
                break;
            default:
                // Over-full for the moment before the push prompt resolves it.
                Vector2[] square = { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) };
                yield return Enumerable.Range(0, count).Select(i => square[i % 4] * (1f + i / 4)).ToArray();
                break;
        }
    }

    // `at` is from the node's centre in node widths.
    private static void Place(Node2D model, Vector2 at, float scale, float nodeSize) {
        model.Scale = new Vector2(scale, scale);
        model.Position = (new Vector2(0.5f, 0.5f) + at) * nodeSize - Vector2.One * (TOKEN_ART_SIZE * scale * 0.5f);
    }

    // The wrappers are placed at once; the pieces are seen to travel. Everyone this may
    // re-pack slides from where it was drawn, the mover lifted like a piece picked up and
    // put down, a pushed one (`stumble`) landing with an overshoot.
    public static bool MovePlayer(Node2D obj, Control newNode, Control oldNode, bool stumble = false) {
        if (newNode == null) return false;

        Dictionary<Node2D, Vector2> before = new Dictionary<Node2D, Vector2>();
        if (oldNode != null) {
            foreach (Node2D occupant in GetAllPlayersInNode(oldNode)) before[occupant] = TokenMotion.VisualPosition(occupant);
        }
        foreach (Node2D occupant in GetAllPlayersInNode(newNode)) before[occupant] = TokenMotion.VisualPosition(occupant);
        if (!obj.IsInsideTree()) before.Remove(obj);

        if (oldNode != null) {
            oldNode.RemoveChild(obj);
            FixPositioning(oldNode);
        }

        // The node's button stays first, under the models, so a model can be clicked.
        newNode.AddChild(obj);
        FixPositioning(newNode);

        foreach (KeyValuePair<Node2D, Vector2> pair in before) {
            TokenMotion.Glide(pair.Key, pair.Value, lift: pair.Key == obj, stumble: stumble && pair.Key == obj);
        }
        return true;
    }
}
