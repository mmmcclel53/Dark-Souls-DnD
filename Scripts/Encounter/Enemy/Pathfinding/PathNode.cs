public class PathNode : IHeapItem<PathNode> {
	
	public bool walkable;
	public int gridX;
	public int gridY;

	public int gCost;
	public int hCost;
	public PathNode parent;
	int heapIndex;
	
	public PathNode(bool _walkable, int _gridX, int _gridY) {
		walkable = _walkable;
		gridX = _gridX;
		gridY = _gridY;
	}

	public int fCost {
		get {
			return gCost + hCost;
		}
	}

	public int HeapIndex {
		get {
			return heapIndex;
		}
		set {
			heapIndex = value;
		}
	}

	public int CompareTo(PathNode nodeToCompare) {
		int compare = fCost.CompareTo(nodeToCompare.fCost);
		if (compare == 0) {
			compare = hCost.CompareTo(nodeToCompare.hCost);
		}
		return -compare;
	}
}
