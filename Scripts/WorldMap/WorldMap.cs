using Godot;
using System.Collections.Generic;

public partial class WorldMap : VBoxContainer {
	private enum PendingAction { NONE, START_ENCOUNTER, REST }

	[Export] public Label titleLabel;
	[Export] public Control board;
	[Export] public Control mapView;
	[Export] public PanelContainer infoPanel;
	[Export] public Label nodeTitleLabel;
	[Export] public Label nodeDetailsLabel;
	[Export] public Button actionButton;

	private Dictionary<string, WorldMapNode> nodeControls = new Dictionary<string, WorldMapNode>();
	private WorldMapPartyMarker marker;
	private WorldMapNode selectedNode;
	private PendingAction pendingAction = PendingAction.NONE;
	private Rect2 mapBounds;

	public override void _Ready() {
		actionButton.Pressed += OnActionPressed;
		board.Resized += UpdateMapTransform;
		GetNode<CharacterPortraitPane>("/root/CharacterPortraitPane")?.Show();

		if (!WorldMapManager.EnsureLoaded()) {
			titleLabel.Text = "World Map — failed to load campaign file";
			infoPanel.Visible = false;
			return;
		}

		titleLabel.Text = WorldMapManager.MapData.name;
		BuildMap();

		marker = new WorldMapPartyMarker();
		mapView.AddChild(marker);
		marker.Position = NodeAnchor(CurrentNode());

		SelectNode(nodeControls[WorldMapManager.CurrentNodeId]);
		RefreshNodeStates();
		UpdateMapTransform();
	}

	private WorldNodeData CurrentNode() => WorldMapManager.MapData.GetNode(WorldMapManager.CurrentNodeId);

	private void BuildMap() {
		var sorted = new List<WorldNodeData>(WorldMapManager.MapData.nodes);
		sorted.Sort((a, b) => a.r != b.r ? a.r - b.r : a.q - b.q);

		bool first = true;
		foreach (var nd in sorted) {
			var ctl = new WorldMapNode();
			ctl.data = nd;
			ctl.Clicked += OnNodeClicked;
			mapView.AddChild(ctl);
			ctl.Position = NodeAnchor(nd) - ctl.TopFaceCenter;
			nodeControls[nd.id] = ctl;

			var rect = new Rect2(ctl.Position, new Vector2(WorldMapNode.HEX_WIDTH,
				WorldMapNode.HEX_RADIUS * 2f + nd.elevation * WorldMapNode.WALL_STEP));
			mapBounds = first ? rect : mapBounds.Merge(rect);
			first = false;
		}
		mapBounds = mapBounds.Grow(24f);
	}

	// Map-space position of a node's top face center: axial-to-pixel for
	// pointy-top hexes, lifted by elevation.
	private static Vector2 NodeAnchor(WorldNodeData nd) {
		float x = WorldMapNode.HEX_WIDTH * (nd.q + nd.r * 0.5f);
		float y = WorldMapNode.HEX_RADIUS * 1.5f * nd.r - nd.elevation * WorldMapNode.WALL_STEP;
		return new Vector2(x, y);
	}

	// Scale the whole map down to fit the board (never up) and center it.
	private void UpdateMapTransform() {
		if (nodeControls.Count == 0) return;
		Vector2 boardSize = board.Size;
		if (boardSize.X <= 0 || boardSize.Y <= 0) return;
		float scale = Mathf.Min(1f, Mathf.Min(boardSize.X / mapBounds.Size.X, boardSize.Y / mapBounds.Size.Y));
		mapView.Scale = new Vector2(scale, scale);
		mapView.Position = (boardSize - mapBounds.Size * scale) / 2f - mapBounds.Position * scale;
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
