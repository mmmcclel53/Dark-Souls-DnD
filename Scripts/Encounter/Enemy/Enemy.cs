using Godot;
using Godot.Collections;
using System.Collections.Generic;

public partial class Enemy : Node
{

    [Export] public Texture2D enemyInfoTexture;

    [Export] public int tier = 1;
    [Export] public int threatLevel = 1;
    [Export] public int health = 1;

    [Export] public int physicalDefense = 0;
    [Export] public int magicalDefense = 0;

    [Export(PropertyHint.ResourceType, "EnemyMove")] public Array<EnemyMove> moves;
    [Export] public Control healthNode;
    [Export] public Label healthLabel;
    [Export] public Control statusesNode;

    public List<PathNode> path;
    private int currentHealth;
    public int currentPathIndex = 0;

    private Queue<EnemyMove> moveQueue = new Queue<EnemyMove>();
    private Node2D aggroPlayer;
    private List<Node2D> nonAggroPlayers;

    public void DoMoves(Node2D aggroPlayer, List<Node2D> nonAggroPlayers) {
        this.aggroPlayer = aggroPlayer;
        this.nonAggroPlayers = nonAggroPlayers;
        moveQueue.Clear();
        foreach (EnemyMove move in moves) {
            moveQueue.Enqueue(move);
        }
        ProcessNextMove();
    }

    private void ProcessNextMove() {
        if (moveQueue.Count == 0) return;
        EnemyMove move = moveQueue.Dequeue();

        if (move.towardsAggro) {
            path = Pathfinding.FindPath(EncounterManager.pathGrid, (Node2D)GetParent(), aggroPlayer);
        } else {
            int shortestPathCount = 999;
            List<PathNode> shortestPath = null;
            foreach (Node2D nonAggroPlayer in nonAggroPlayers) {
                List<PathNode> tempPath = Pathfinding.FindPath(EncounterManager.pathGrid, (Node2D)GetParent(), nonAggroPlayer);
                if (tempPath.Count < shortestPathCount) {
                    shortestPath = tempPath;
                    shortestPathCount = tempPath.Count;
                }
            }
            path = shortestPath;
        }
    }

    public void ApplyDamage(int damage) {
        currentHealth = Mathf.Max(0, currentHealth - damage);
        healthLabel.Text = currentHealth.ToString();
        healthNode.GetNode<TextureProgressBar>("%Health").Value = currentHealth / health;
        if (currentHealth <= 0) {
            QueueFree();
        }
    }

    public void OnClick() {
        EncounterManager.enemyInfoModal.GetChild<TextureRect>(0).Texture = enemyInfoTexture;
        EncounterManager.enemyInfoModal.Visible = true;
    }

	public override void _Ready() {
        Connect("pressed", Callable.From(OnClick));

        if (tier > 1) {
            CanvasItem healthNodeCanvas = GetNode<CanvasItem>("%Health");
            healthNodeCanvas.Modulate = new Color(255f,149f,10f);

            health = (int)Mathf.Ceil(health * 1.5);
            physicalDefense += tier;
            magicalDefense += tier;
        }
        currentHealth = health;
        healthLabel.Text = currentHealth.ToString();
        healthNode.GetNode<TextureProgressBar>("%Health").MaxValue = currentHealth;
    }

	public override void _Process(double delta) {
        if (EncounterManager.showEnemyInfo) {
            healthNode.Visible = true;
            statusesNode.Visible = true;
        } else {
            healthNode.Visible = false;
            statusesNode.Visible = false;
        }

        if (path != null && path.Count > 0) {
            Node2D self = (Node2D)GetParent();
            Control parent = (Control)self.GetParent();
            float nodeSize = parent.Size.X;
            Vector2 centeredPos = new Vector2(nodeSize/4,nodeSize/4);
            if (!EncounterManager.isEnemyMoving) {
                EncounterManager.isEnemyMoving = true;
                self.Position = centeredPos;
            }

            PathNode pathNode = path[currentPathIndex];
            int child = (pathNode.gridY*EncounterManager.gridSize) + pathNode.gridX;
            GameNode node = EncounterManager.pathGrid.GetChild<GameNode>(child);
            Vector2 targetPos = node.GlobalPosition + centeredPos;

            if (self.GlobalPosition.DistanceTo(targetPos) > 1f) {
                Vector2 moveDir = (targetPos - self.GlobalPosition).Normalized();
                Vector2 time = new Vector2((float)(delta*250), (float)(delta*250));
                self.GlobalPosition += moveDir * time;
            } else {
                if (currentPathIndex >= path.Count-1) {
                    EncounterManager.MovePlayer(self, node, (Control)self.GetParent());
                    EncounterManager.isEnemyMoving = false;
                    path = null;
                    currentPathIndex = 0;
                    ProcessNextMove();
                } else {
                    DelayNextMove();
                }
            }
        }
    }

    private async void DelayNextMove() {
        List<PathNode> tempPath = path;
        path = null;
        await ToSignal(GetTree().CreateTimer(0.5f), "timeout");
        currentPathIndex++;
        path = tempPath;
    }
}
