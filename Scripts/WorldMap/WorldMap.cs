using Godot;
using System.Collections.Generic;

public partial class WorldMap : VBoxContainer {
	private enum PendingAction { NONE, START_ENCOUNTER, REST }

	private const float ZOOM_MIN = 0.35f;
	private const float ZOOM_MAX = 3f;
	private const float ZOOM_FACTOR = 1.1f;

	[Export] public Label titleLabel;
	[Export] public Control board;
	[Export] public Control mapView;
	[Export] public PanelContainer infoPanel;
	[Export] public Label nodeTitleLabel;
	[Export] public Label nodeDetailsLabel;
	[Export] public Button actionButton;
	[Export] public Button rotateLeftButton;
	[Export] public Button rotateRightButton;
	[Export] public Button resetViewButton;

	private Dictionary<string, WorldMapNode> nodeControls = new Dictionary<string, WorldMapNode>();
	private WorldMapPartyMarker marker;
	private WorldMapNode selectedNode;
	private PendingAction pendingAction = PendingAction.NONE;
	private Rect2 mapBounds;

	// View state: rotation in 30° steps clockwise around the start node
	// (even steps are 60° lattice rotations; odd steps add a 30° twist that
	// flips tiles to flat-top orientation), plus user pan/zoom on top of the
	// auto fit-and-center transform.
	private const int ROTATION_FULL_TURN = 12;
	private int rotationSteps;
	private Vector2I rotationPivot;
	private float userZoom = 1f;
	private Vector2 panOffset = Vector2.Zero;
	private bool panning;

	public override void _Ready() {
		actionButton.Pressed += OnActionPressed;
		rotateLeftButton.Pressed += () => { Rotate(-1); };
		rotateRightButton.Pressed += () => { Rotate(1); };
		resetViewButton.Pressed += OnResetView;
		board.Resized += UpdateMapTransform;
		board.GuiInput += OnBoardGuiInput;
		GetNode<CharacterPortraitPane>("/root/CharacterPortraitPane")?.Show();

		if (!WorldMapManager.EnsureLoaded()) {
			titleLabel.Text = "World Map — failed to load campaign file";
			infoPanel.Visible = false;
			return;
		}

		titleLabel.Text = WorldMapManager.MapData.name;
		var start = WorldMapManager.MapData.GetStartNode();
		rotationPivot = new Vector2I(start.q, start.r);

		foreach (var nd in WorldMapManager.MapData.nodes) {
			var ctl = new WorldMapNode();
			ctl.data = nd;
			ctl.Clicked += OnNodeClicked;
			mapView.AddChild(ctl);
			nodeControls[nd.id] = ctl;
		}

		marker = new WorldMapPartyMarker();
		mapView.AddChild(marker);

		LayoutNodes();
		marker.Position = NodeAnchor(CurrentNode());

		SelectNode(nodeControls[WorldMapManager.CurrentNodeId]);
		RefreshNodeStates();
		UpdateMapTransform();
	}

	private WorldNodeData CurrentNode() => WorldMapManager.MapData.GetNode(WorldMapManager.CurrentNodeId);

	// Axial coords rotated around the pivot by whole 60° clockwise lattice
	// steps (cube-coordinate rotation). View-only: game logic keeps original coords.
	private Vector2I DisplayCoords(WorldNodeData nd, int latticeSteps) {
		int x = nd.q - rotationPivot.X;
		int z = nd.r - rotationPivot.Y;
		for (int i = 0; i < latticeSteps; i++) {
			int nq = -z;
			int nr = x + z;
			x = nq;
			z = nr;
		}
		return new Vector2I(x + rotationPivot.X, z + rotationPivot.Y);
	}

	private static Vector2 AxialToPixel(Vector2I coord) {
		return new Vector2(
			WorldMapNode.HEX_WIDTH * (coord.X + coord.Y * 0.5f),
			WorldMapNode.HEX_RADIUS * 1.5f * coord.Y);
	}

	// Map-space position of a node's top face center. Odd rotation steps add a
	// 30° clockwise twist around the pivot (tiles switch to flat-top to match);
	// the elevation lift stays screen-vertical.
	private Vector2 NodeAnchor(WorldNodeData nd) {
		Vector2 p = AxialToPixel(DisplayCoords(nd, rotationSteps / 2));
		if (rotationSteps % 2 == 1) {
			Vector2 pivot = AxialToPixel(rotationPivot);
			Vector2 rel = p - pivot;
			const float COS = 0.8660254f, SIN = 0.5f;
			p = pivot + new Vector2(rel.X * COS - rel.Y * SIN, rel.X * SIN + rel.Y * COS);
		}
		p.Y -= nd.elevation * WorldMapNode.WALL_STEP;
		return p;
	}

	// Positions every tile for the current rotation, rebuilds draw order
	// (back rows first so tall tiles overlap correctly), recomputes bounds.
	private void LayoutNodes() {
		WorldMapNode.FlatTop = rotationSteps % 2 == 1;

		var sorted = new List<WorldMapNode>(nodeControls.Values);
		var anchors = new Dictionary<WorldMapNode, Vector2>();
		foreach (var ctl in sorted)
			anchors[ctl] = NodeAnchor(ctl.data) + new Vector2(0, ctl.data.elevation * WorldMapNode.WALL_STEP);
		sorted.Sort((a, b) => {
			int byY = anchors[a].Y.CompareTo(anchors[b].Y);
			return byY != 0 ? byY : anchors[a].X.CompareTo(anchors[b].X);
		});

		bool first = true;
		for (int i = 0; i < sorted.Count; i++) {
			var ctl = sorted[i];
			mapView.MoveChild(ctl, i);
			ctl.Position = NodeAnchor(ctl.data) - ctl.TopFaceCenter;
			ctl.QueueRedraw();
			var rect = new Rect2(ctl.Position, new Vector2(WorldMapNode.HEX_RADIUS * 2f,
				WorldMapNode.HEX_RADIUS * 2f + ctl.data.elevation * WorldMapNode.WALL_STEP));
			mapBounds = first ? rect : mapBounds.Merge(rect);
			first = false;
		}
		mapBounds = mapBounds.Grow(24f);
	}

	private void Rotate(int direction) {
		rotationSteps = ((rotationSteps + direction) % ROTATION_FULL_TURN + ROTATION_FULL_TURN) % ROTATION_FULL_TURN;
		LayoutNodes();
		if (marker != null) marker.Position = NodeAnchor(CurrentNode());
		UpdateMapTransform();
	}

	private void OnResetView() {
		rotationSteps = 0;
		userZoom = 1f;
		panOffset = Vector2.Zero;
		LayoutNodes();
		if (marker != null) marker.Position = NodeAnchor(CurrentNode());
		UpdateMapTransform();
	}

	// Board-relative position where the map would sit with no user pan.
	private Vector2 CenteredPosition(float scale) {
		return (board.Size - mapBounds.Size * scale) / 2f - mapBounds.Position * scale;
	}

	private float FitScale() {
		Vector2 boardSize = board.Size;
		if (boardSize.X <= 0 || boardSize.Y <= 0 || mapBounds.Size.X <= 0) return 1f;
		return Mathf.Min(1f, Mathf.Min(boardSize.X / mapBounds.Size.X, boardSize.Y / mapBounds.Size.Y));
	}

	private void UpdateMapTransform() {
		if (nodeControls.Count == 0) return;
		float scale = FitScale() * userZoom;
		mapView.Scale = new Vector2(scale, scale);
		mapView.Position = CenteredPosition(scale) + panOffset;
	}

	private void OnBoardGuiInput(InputEvent @event) {
		if (@event is InputEventMouseButton mb) {
			if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed) {
				if (mb.ShiftPressed) Rotate(-1);
				else ZoomAt(mb.Position, ZOOM_FACTOR);
			} else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed) {
				if (mb.ShiftPressed) Rotate(1);
				else ZoomAt(mb.Position, 1f / ZOOM_FACTOR);
			} else if (mb.ButtonIndex == MouseButton.Left || mb.ButtonIndex == MouseButton.Middle || mb.ButtonIndex == MouseButton.Right) {
				panning = mb.Pressed;
			}
		} else if (@event is InputEventMouseMotion mm && panning) {
			panOffset += mm.Relative;
			UpdateMapTransform();
		}
	}

	public override void _UnhandledInput(InputEvent @event) {
		if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;
		switch (key.Keycode) {
			case Key.Q: Rotate(-1); break;
			case Key.E: Rotate(1); break;
			case Key.R: OnResetView(); break;
		}
	}

	// Zoom keeping the map point under the cursor stationary.
	private void ZoomAt(Vector2 boardPoint, float factor) {
		float oldScale = FitScale() * userZoom;
		userZoom = Mathf.Clamp(userZoom * factor, ZOOM_MIN, ZOOM_MAX);
		float newScale = FitScale() * userZoom;
		Vector2 mapPoint = (boardPoint - mapView.Position) / oldScale;
		panOffset = boardPoint - mapPoint * newScale - CenteredPosition(newScale);
		UpdateMapTransform();
	}

	private void RefreshNodeStates() {
		var cur = CurrentNode();
		foreach (var ctl in nodeControls.Values) {
			ctl.Cleared = WorldMapManager.IsCleared(ctl.data.id);
			ctl.Reachable = IsReachable(cur, ctl.data);
			ctl.Selected = ctl == selectedNode;
		}
	}

	private bool IsReachable(WorldNodeData from, WorldNodeData to) {
		if (from == null || to.id == from.id) return false;
		if (!WorldMapManager.MapData.AreNeighbors(from, to)) return false;
		if (to.IsImpassable) return false;
		if (Mathf.Abs(to.elevation - from.elevation) > 1) return false;
		return true;
	}

	private void OnNodeClicked(WorldMapNode ctl) {
		var nd = ctl.data;
		var cur = CurrentNode();

		if (nd.id == cur.id) {
			SelectNode(ctl);
		} else if (ctl.Reachable) {
			bool mustFight = nd.encounterType == WorldEncounterType.ENCOUNTER && !WorldMapManager.IsCleared(nd.id);
			if (mustFight)
				SelectNode(ctl);
			else
				MoveParty(ctl);
		} else {
			SelectNode(ctl);
		}
		RefreshNodeStates();
	}

	private void MoveParty(WorldMapNode ctl) {
		WorldMapManager.SetCurrentNode(ctl.data.id);
		selectedNode = ctl;
		ShowInfo(ctl);
		var tween = CreateTween();
		tween.TweenProperty(marker, "position", NodeAnchor(ctl.data), 0.25f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
	}

	private void SelectNode(WorldMapNode ctl) {
		selectedNode = ctl;
		ShowInfo(ctl);
	}

	private void ShowInfo(WorldMapNode ctl) {
		var nd = ctl.data;
		var cur = CurrentNode();
		bool isCurrent = nd.id == cur.id;

		nodeTitleLabel.Text = nd.Title;

		var lines = new List<string> {
			$"Terrain:  {WorldMapNode.PrettyTerrain(nd.terrain)}",
			$"Elevation:  {nd.elevation}",
		};
		switch (nd.encounterType) {
			case WorldEncounterType.ENCOUNTER:
				lines.Add(WorldMapManager.IsCleared(nd.id)
					? "Encounter — cleared"
					: $"Encounter — Level {nd.level}");
				break;
			case WorldEncounterType.BONFIRE:
				lines.Add("Bonfire");
				break;
			case WorldEncounterType.BOSS:
				lines.Add("Boss (not yet implemented)");
				break;
		}
		if (isCurrent)
			lines.Add("The party is here.");
		else if (!ctl.Reachable)
			lines.Add(BlockedReason(cur, nd));

		nodeDetailsLabel.Text = string.Join("\n", lines);

		pendingAction = PendingAction.NONE;
		if (isCurrent && nd.encounterType == WorldEncounterType.BONFIRE) {
			pendingAction = PendingAction.REST;
			actionButton.Text = "Rest at Bonfire";
		} else if (!isCurrent && ctl.Reachable && nd.encounterType == WorldEncounterType.ENCOUNTER && !WorldMapManager.IsCleared(nd.id)) {
			pendingAction = PendingAction.START_ENCOUNTER;
			actionButton.Text = $"Start Encounter  (Level {nd.level})";
		}
		actionButton.Visible = pendingAction != PendingAction.NONE;
	}

	private string BlockedReason(WorldNodeData from, WorldNodeData to) {
		if (to.IsImpassable) return "Impassable terrain.";
		if (!WorldMapManager.MapData.AreNeighbors(from, to)) return "Too far away.";
		if (Mathf.Abs(to.elevation - from.elevation) > 1) return "Elevation difference too steep.";
		return "";
	}

	private void OnActionPressed() {
		switch (pendingAction) {
			case PendingAction.START_ENCOUNTER:
				WorldMapManager.StartEncounter(selectedNode.data.id);
				GetTree().ChangeSceneToPacked(ResourceLoader.Load<PackedScene>("res://Scenes/Encounter.tscn"));
				break;
			case PendingAction.REST:
				WorldMapManager.RestAtBonfire();
				GetTree().ChangeSceneToPacked(ResourceLoader.Load<PackedScene>("res://Scenes/Bonfire.tscn"));
				break;
		}
	}
}
