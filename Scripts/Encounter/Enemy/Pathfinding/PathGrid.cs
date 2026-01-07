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
				bool walkable = !node.isDisabled;
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
	

	public PathNode NodeFromObj(Node2D obj) {
		Node n = obj.GetParent();
		string name = n.Name;
		int nodeNbr = System.Int32.Parse(name.Replace("Node", "")) - 1;

		int x =  nodeNbr % EncounterManager.gridSize;
		int y = (int)Math.Floor((decimal) nodeNbr / EncounterManager.gridSize);
		return grid[x,y];
	}
}
