using Godot;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public static class Pathfinding {

	public static PathGrid grid;

	public static List<PathNode> FindPath(PathGrid pg, Node2D startObj, Node2D targetObj) {
		grid = pg;
		PathNode startNode = grid.NodeFromObj(startObj);
		PathNode targetNode = grid.NodeFromObj(targetObj);

		Heap<PathNode> openSet = new Heap<PathNode>(grid.MaxSize);
		HashSet<PathNode> closedSet = new HashSet<PathNode>();
		openSet.Add(startNode);

		while (openSet.Count > 0) {
			PathNode currentNode = openSet.RemoveFirst();
			closedSet.Add(currentNode);

			if (currentNode == targetNode) {
				RetracePath(startNode,targetNode);
				return grid.path;
			}

			foreach (PathNode neighbour in grid.GetNeighbours(currentNode)) {
				if (!neighbour.walkable || closedSet.Contains(neighbour)) {
					continue;
				}

				int newMovementCostToNeighbour = currentNode.gCost + GetDistance(currentNode, neighbour);
				if (newMovementCostToNeighbour < neighbour.gCost || !openSet.Contains(neighbour)) {
					neighbour.gCost = newMovementCostToNeighbour;
					neighbour.hCost = GetDistance(neighbour, targetNode);
					neighbour.parent = currentNode;

					if (!openSet.Contains(neighbour))
						openSet.Add(neighbour);
					else {
						//openSet.UpdateItem(neighbour);
					}
				}
			}
		}
		return grid.path;
	}

	public static void RetracePath(PathNode startNode, PathNode endNode) {
		List<PathNode> path = new List<PathNode>();
		PathNode currentNode = endNode;

		while (currentNode != startNode) {
			path.Add(currentNode);
			currentNode = currentNode.parent;
		}
		path.Reverse();

		grid.path = path;
	}

	public static int GetDistance(PathNode nodeA, PathNode nodeB) {
		int dstX = Mathf.Abs(nodeA.gridX - nodeB.gridX);
		int dstY = Mathf.Abs(nodeA.gridY - nodeB.gridY);

		if (dstX > dstY)
			return dstY + (dstX-dstY);
		return dstX + (dstY-dstX);
	}
}
