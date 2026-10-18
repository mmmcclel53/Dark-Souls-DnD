using Godot;
using System;
using System.Collections.Generic;
using System.IO;

public partial class PathGrid : Control {

	[Export] public Node nodesParent;
	public List<PathNode> path;
	public PathNode[,] grid;

	public override void _Ready() {
		int mapSize = (int) Math.Sqrt(nodesParent.GetChildCount());
		EncounterManager.gridSize = mapSize;
		CreateGrid();

		// The nodes themselves are anchored to fractions of the board, so the engine keeps
		// them on the printed circles at any window size. The models standing on them are
		// not, so they are re-sized here whenever the board changes.
		// Deferred either way: the child nodes re-anchor during the same layout pass, so
		// their new size is only readable once it has finished.
		Resized += () => { CallDeferred(nameof(RescaleModels)); };
		CallDeferred(nameof(RescaleModels));
	}

	// A token is drawn at half the node it stands on, and the node only knows its real size
	// once the board has been laid out — which is after every spawn in ActionListener._Ready.
	public void RescaleModels() {
		foreach (Node child in nodesParent.GetChildren()) {
			if (child is not GameNode node || node.Size.X <= 0f) continue;
			foreach (Node2D model in EncounterManager.GetAllPlayersInNode(node)) {
				EncounterManager.ScaleToken(model, node.Size.X);
			}
			EncounterManager.FixPositioning(node);
		}
	}

	// A printed boss tile: every node it lists goes on its circle at its size, with its entrance
	// and spawn flags, and every other node is taken off the board. The scene's own anchors are
	// Tile 2's (Board Geometry in CLAUDE.md).
	public void ApplyLayout(BoardLayout layout) {
		if (layout == null) return;
		var centres = new System.Collections.Generic.Dictionary<int, Vector2>();
		for (int i = 0; i < layout.cells.Length && i < layout.centres.Length; i++) centres[layout.cells[i]] = layout.centres[i];

		float half = layout.nodeSize * 0.5f;
		for (int index = 0; index < nodesParent.GetChildCount(); index++) {
			if (nodesParent.GetChild(index) is not GameNode node) continue;
			bool on = centres.TryGetValue(index, out Vector2 centre);
			node.isDisabled = !on;
			node.isEntrance = on && System.Array.IndexOf(layout.entrances, index) >= 0;
			node.isEnemySpawn = on && System.Array.IndexOf(layout.enemySpawns, index) >= 0;
			node.statusEffect = EncounterManager.StatusEffect.NONE;
			node.SetPillar(on && System.Array.IndexOf(layout.pillars, index) >= 0 ? GameNode.Pillar.STANDING : GameNode.Pillar.NONE);
			// Hidden, not just transparent: Tile 2's anchors overlap this tile's circles, and an
			// invisible button there would still take their clicks.
			node.Visible = on;
			if (!on) continue;
			node.AnchorLeft = centre.X - half;
			node.AnchorRight = centre.X + half;
			node.AnchorTop = centre.Y - half;
			node.AnchorBottom = centre.Y + half;
			node.OffsetLeft = node.OffsetRight = node.OffsetTop = node.OffsetBottom = 0f;
		}
		CreateGrid();
		CallDeferred(nameof(RescaleModels));
	}

	public int MaxSize {
		get {
			return EncounterManager.gridSize * EncounterManager.gridSize;
		}
	}

	void CreateGrid() {
		int gridSize = EncounterManager.gridSize;
		grid = new PathNode[gridSize,gridSize];
		for (int y = 0; y < gridSize; y++) {
			for (int x = 0; x < gridSize; x++) {
				int child = (y*gridSize) + x;
				GameNode node = nodesParent.GetChild<GameNode>(child);
				Control control = (Control)nodesParent.GetChild(child).GetChild(0);
				if (node.isDisabled) {
					control.Modulate = new Color(0,0,0,0);
				}
				bool walkable = !node.isDisabled && node.pillar != GameNode.Pillar.STANDING;
				grid[x,y] = new PathNode(walkable, x, y);
			}
		}
	}

	public List<PathNode> GetNeighbours(PathNode node) {
		List<PathNode> neighbours = new List<PathNode>();

		for (int x = -1; x <= 1; x++) {
			for (int y = -1; y <= 1; y++) {
				if (x == 0 && y == 0)
					continue;

				int checkX = node.gridX + x;
				int checkY = node.gridY + y;

				if (checkX >= 0 && checkX < EncounterManager.gridSize && checkY >= 0 && checkY < EncounterManager.gridSize) {
					neighbours.Add(grid[checkX,checkY]);
				}
			}
		}

		return neighbours;
	}
	

	public GameNode GameNodeAt(PathNode node) {
		int child = (node.gridY * EncounterManager.gridSize) + node.gridX;
		return nodesParent.GetChild<GameNode>(child);
	}

	// Only terrain blocks movement. A full node is still enterable — arriving on one pushes
	// a model that was already there off it (p10), so it must stay pathable.
	public bool IsBlocked(PathNode node) => !node.walkable;

	public bool IsStandingPillar(PathNode node) => GameNodeAt(node).pillar == GameNode.Pillar.STANDING;

	public bool HasRoom(PathNode node) => !EncounterManager.IsNodeFull(GameNodeAt(node));

	public PathNode NodeFromObj(Node2D obj) {
		Node n = obj.GetParent();
		string name = n.Name;
		int nodeNbr = System.Int32.Parse(name.Replace("Node", "")) - 1;

		int x =  nodeNbr % EncounterManager.gridSize;
		int y = (int)Math.Floor((decimal) nodeNbr / EncounterManager.gridSize);
		return grid[x,y];
	}
}
