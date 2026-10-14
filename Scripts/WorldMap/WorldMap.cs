using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class WorldMap : VBoxContainer {
	private enum PendingAction { NONE, START_ENCOUNTER, REST, CLIMB, OPEN_EDGE, FIGHT_ROAMER }

	private const float ZOOM_MIN = 0.35f;
	private const float ZOOM_MAX = 3f;
	private const float ZOOM_FACTOR = 1.1f;

	[Export] public Label titleLabel;
	// The soul counter hangs under this bar's right end.
	[Export] public Control titleBar;
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
	private Dictionary<string, WorldMapRoamerMarker> roamerMarkers = new Dictionary<string, WorldMapRoamerMarker>();
	// Roamers whose marker is mid-step, which RefreshRoamers leaves to the tween.
	private HashSet<string> animating = new HashSet<string>();
	// Held from a step until its animation, loot and any contact have played out.
	private bool locked;
	private WorldMapEdges edges;
	private WorldEdgeData pendingEdge;
	private WorldMapNode selectedNode;
	private PendingAction pendingAction = PendingAction.NONE;
	// The bounds the map is fitted to: only the hexes the party has any sight of. Taken at load,
	// on Rotate and on Reset View, never on a move, so walking does not shift the player's view.
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
	private bool showingLoot;

	public override void _ExitTree() {
		GetNodeOrNull<SoulCounter>("/root/SoulCounter")?.Release(titleBar);
	}

	public override void _Ready() {
		actionButton.Pressed += OnActionPressed;
		rotateLeftButton.Pressed += () => { Rotate(-1); };
		rotateRightButton.Pressed += () => { Rotate(1); };
		resetViewButton.Pressed += OnResetView;
		if (OS.IsDebugBuild()) AddRevealToggle();
		board.Resized += UpdateMapTransform;
		board.GuiInput += OnBoardGuiInput;
		GetNode<CharacterPortraitPane>("/root/CharacterPortraitPane")?.Show();
		GetNodeOrNull<SoulCounter>("/root/SoulCounter")?.ShowBelow(titleBar);

		if (!WorldMapManager.EnsureLoaded()) {
			titleLabel.Text = "World Map — failed to load campaign file";
			infoPanel.Visible = false;
			return;
		}

		CampaignManager.Autosave(CampaignManager.WORLD_MAP);
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

		edges = new WorldMapEdges { anchorOf = NodeAnchor };
		mapView.AddChild(edges);

		marker = new WorldMapPartyMarker();
		mapView.AddChild(marker);

		foreach (RoamerState state in WorldMapManager.Roamers) {
			var roamerMarker = new WorldMapRoamerMarker();
			mapView.AddChild(roamerMarker);
			roamerMarker.SetAvatar(WorldMapManager.GetPlan(state.id)?.Toughest?.Data?.avatarTexture);
			roamerMarkers[state.id] = roamerMarker;
		}

		LayoutNodes();
		marker.Position = NodeAnchor(CurrentNode());

		SelectNode(nodeControls[WorldMapManager.CurrentNodeId]);
		RefreshNodeStates();
		Refit();
		UpdateMapTransform();
		_ = ShowPendingLoot();
	}

	private async Task ShowPendingLoot() {
		if (showingLoot) return;
		showingLoot = true;
		while (WorldMapManager.pendingLoot.Count > 0 && IsInsideTree()) {
			LootResult result = WorldMapManager.pendingLoot[0];
			WorldMapManager.pendingLoot.RemoveAt(0);
			if (!WorldMapLootPanel.CanShow(result)) continue;
			var panel = new WorldMapLootPanel();
			AddChild(panel);
			await panel.Present(result);
			panel.QueueFree();
		}
		showingLoot = false;
	}

	private void AddRevealToggle() {
		var reveal = new CheckButton { Text = "DEV: Reveal", ButtonPressed = WorldMapManager.revealAll };
		reveal.CustomMinimumSize = new Vector2(0, 36);
		reveal.AddThemeFontOverride("font", resetViewButton.GetThemeFont("font"));
		reveal.AddThemeFontSizeOverride("font_size", 16);
		reveal.Toggled += on => {
			WorldMapManager.revealAll = on;
			RefreshNodeStates();
			if (selectedNode != null) ShowInfo(selectedNode);
		};
		resetViewButton.GetParent().AddChild(reveal);

		var shard = new Button { Text = "DEV: Shard", CustomMinimumSize = new Vector2(0, 36) };
		shard.AddThemeFontOverride("font", resetViewButton.GetThemeFont("font"));
		shard.AddThemeFontSizeOverride("font_size", 16);
		shard.Pressed += PartyEstus.AddShard;
		resetViewButton.GetParent().AddChild(shard);
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

	// Positions every tile for the current rotation and rebuilds draw order
	// (back rows first so tall tiles overlap correctly).
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

		for (int i = 0; i < sorted.Count; i++) {
			var ctl = sorted[i];
			mapView.MoveChild(ctl, i);
			ctl.Position = NodeAnchor(ctl.data) - ctl.TopFaceCenter;
			ctl.QueueRedraw();
		}
		edges?.QueueRedraw();
	}

	private void Refit() {
		Rect2 bounds = new Rect2();
		bool first = true;
		foreach (var ctl in nodeControls.Values) {
			if (ctl.Sight == WorldSight.UNKNOWN && !ctl.LandmarkVisible) continue;
			var rect = new Rect2(ctl.Position, new Vector2(WorldMapNode.HEX_RADIUS * 2f,
				WorldMapNode.HEX_RADIUS * 2f + ctl.data.elevation * WorldMapNode.WALL_STEP));
			bounds = first ? rect : bounds.Merge(rect);
			first = false;
		}
		mapBounds = bounds.Grow(24f);
	}

	private void Rotate(int direction) {
		rotationSteps = ((rotationSteps + direction) % ROTATION_FULL_TURN + ROTATION_FULL_TURN) % ROTATION_FULL_TURN;
		LayoutNodes();
		Refit();
		if (marker != null) marker.Position = NodeAnchor(CurrentNode());
		RefreshRoamers();
		UpdateMapTransform();
	}

	private void OnResetView() {
		rotationSteps = 0;
		userZoom = 1f;
		panOffset = Vector2.Zero;
		LayoutNodes();
		Refit();
		if (marker != null) marker.Position = NodeAnchor(CurrentNode());
		RefreshRoamers();
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
			ctl.Sight = WorldMapManager.SightOf(ctl.data.id);
			ctl.LandmarkVisible = WorldMapManager.LandmarkShows(ctl.data);
			ctl.Cleared = WorldMapManager.IsCleared(ctl.data.id);
			ctl.LootUntaken = WorldMapManager.HasLoot(ctl.data.id);
			ctl.Reachable = IsReachable(cur, ctl.data);
			ctl.Selected = ctl == selectedNode;
			ctl.RefreshBloodstain();
		}
		RefreshRoamers();
		edges?.QueueRedraw();
	}

	// Each roamer shows where it is while that hex is in sight, as a ghost where it was last seen
	// when it is not, and not at all if it never was or has been beaten.
	private void RefreshRoamers() {
		foreach (RoamerState state in WorldMapManager.Roamers) {
			if (!roamerMarkers.TryGetValue(state.id, out var roamerMarker) || animating.Contains(state.id)) continue;
			bool live = WorldMapManager.RoamerInSight(state);
			string at = live ? state.nodeId : WorldMapManager.RoamerGhostNode(state);
			roamerMarker.Visible = !string.IsNullOrEmpty(at);
			if (!roamerMarker.Visible) continue;
			roamerMarker.Position = NodeAnchor(WorldMapManager.MapData.GetNode(at));
			roamerMarker.Ghost = !live;
			roamerMarker.Chasing = live && state.chasing;
		}
		foreach (var ctl in nodeControls.Values) {
			RoamerState there = WorldMapManager.RoamerAt(ctl.data.id);
			ctl.RoamerLine = there == null ? "" : WorldMapManager.RoamerLine(there);
		}
	}

	private bool IsReachable(WorldNodeData from, WorldNodeData to) =>
		WorldMapManager.CanStep(from, to) && WorldMapManager.SightOf(to.id) != WorldSight.UNKNOWN;

	private void OnNodeClicked(WorldMapNode ctl) {
		if (locked) return;
		var nd = ctl.data;
		var cur = CurrentNode();

		if (nd.id == cur.id) {
			SelectNode(ctl);
		} else if (ctl.Reachable) {
			bool mustFight = (WorldMapManager.IsFight(nd) && !WorldMapManager.IsCleared(nd.id)) || WorldMapManager.RoamerAt(nd.id) != null;
			if (ctl.Sight == WorldSight.GLIMPSED || mustFight)
				SelectNode(ctl);
			else
				MoveParty(ctl);
		} else {
			SelectNode(ctl);
		}
		RefreshNodeStates();
	}

	// One step: the party walks, then every roaming threat takes its own (WorldMapManager.TickRoamers),
	// all in the same quarter second. A roamer that ends on the party, or that the party walked
	// onto, starts its fight once the loot has been shown.
	private void MoveParty(WorldMapNode ctl) {
		WorldMapManager.SetCurrentNode(ctl.data.id);
		List<RoamerMove> moves = WorldMapManager.TickRoamers();
		var shown = new List<RoamerMove>();
		foreach (RoamerMove move in moves) {
			bool seen = WorldMapManager.SightOf(move.from) == WorldSight.SEEN || WorldMapManager.SightOf(move.to) == WorldSight.SEEN;
			if (!seen || !roamerMarkers.ContainsKey(move.id)) continue;
			shown.Add(move);
			animating.Add(move.id);
		}

		locked = true;
		selectedNode = ctl;
		RefreshNodeStates();
		ShowInfo(ctl);
		var tween = CreateTween().SetParallel(true);
		tween.TweenProperty(marker, "position", NodeAnchor(ctl.data), 0.25f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		foreach (RoamerMove move in shown) {
			WorldMapRoamerMarker roamerMarker = roamerMarkers[move.id];
			roamerMarker.Visible = true;
			roamerMarker.Ghost = false;
			roamerMarker.Chasing = WorldMapManager.GetRoamerState(move.id).chasing;
			roamerMarker.Position = NodeAnchor(WorldMapManager.MapData.GetNode(move.from));
			tween.TweenProperty(roamerMarker, "position", NodeAnchor(WorldMapManager.MapData.GetNode(move.to)), 0.25f)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		}
		tween.Finished += OnStepFinished;
		KeepOnScreen(NodeAnchor(ctl.data));
	}

	private async void OnStepFinished() {
		animating.Clear();
		RefreshRoamers();
		await ShowPendingLoot();
		if (!IsInsideTree()) return;
		if (ambushNode != null) await SpringAmbush();
		if (!IsInsideTree()) return;
		if (WorldMapManager.HasPendingRoamer) EnterEncounter();
		else locked = false;
	}

	// The party has just climbed onto a fight it could not see. Before the encounter opens, the
	// map holds for a moment with the fog rolled back from the new height, so what was there to
	// see, the enemy just bumped into first, is seen (Matt, Oct 2026).
	private const float AMBUSH_HOLD = 1.5f;

	private async Task SpringAmbush() {
		string nodeId = ambushNode;
		ambushNode = null;
		actionButton.Text = "Ambush";
		actionButton.Disabled = true;
		actionButton.Visible = true;
		await ToSignal(GetTree().CreateTimer(AMBUSH_HOLD), SceneTreeTimer.SignalName.Timeout);
		if (!IsInsideTree()) return;
		WorldMapManager.StartEncounter(nodeId);
		EnterEncounter();
	}

	private void EnterEncounter() {
		locked = true;
		GetTree().ChangeSceneToPacked(ResourceLoader.Load<PackedScene>("res://Scenes/Encounter.tscn"));
	}

	// A step can end off the edge of what is on screen. Pan, never rezoom, so the party's hex
	// sits inside the board with a margin; the player's zoom stays.
	private void KeepOnScreen(Vector2 anchor) {
		float scale = FitScale() * userZoom;
		Vector2 at = mapView.Position + anchor * scale;
		float margin = Mathf.Min(WorldMapNode.HEX_RADIUS * scale + 60f, Mathf.Min(board.Size.X, board.Size.Y) * 0.25f);
		var safe = new Rect2(Vector2.One * margin, board.Size - Vector2.One * margin * 2f);
		Vector2 shift = new Vector2(
			Mathf.Clamp(at.X, safe.Position.X, safe.End.X) - at.X,
			Mathf.Clamp(at.Y, safe.Position.Y, safe.End.Y) - at.Y);
		if (shift == Vector2.Zero) return;

		Vector2 target = panOffset + shift;
		CreateTween().TweenMethod(Callable.From<Vector2>(pan => {
			panOffset = pan;
			UpdateMapTransform();
		}), panOffset, target, 0.25f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
	}

	private void SelectNode(WorldMapNode ctl) {
		selectedNode = ctl;
		ShowInfo(ctl);
	}

	private void ShowInfo(WorldMapNode ctl) {
		var nd = ctl.data;
		var cur = CurrentNode();
		bool isCurrent = nd.id == cur.id;

		if (ctl.Sight == WorldSight.GLIMPSED) {
			ShowUnexplored(ctl);
			return;
		}
		nodeTitleLabel.Text = nd.Title;

		var lines = new List<string> {
			$"Terrain:  {WorldMapNode.PrettyTerrain(nd.terrain)}",
			$"Elevation:  {nd.elevation}",
		};
		RoamerState roamer = WorldMapManager.RoamerAt(nd.id);
		if (roamer != null) lines.Add(WorldMapManager.RoamerLine(roamer));
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
				lines.Add(WorldMapManager.IsCleared(nd.id)
					? $"{WorldMapNode.PrettyBoss(nd.boss)} — defeated"
					: $"{WorldMapNode.PrettyBoss(nd.boss)} — Level {nd.level}");
				break;
		}
		if (nd.structure != WorldStructure.NONE) lines.Add(StructureArt.Name(nd.structure));
		string loot = WorldMapManager.LootLine(nd.id);
		if (loot != "") lines.Add(loot);
		if (SoulCache.droppedAmount > 0 && SoulCache.droppedWorldNode == nd.id)
			lines.Add($"Bloodstain — {SoulCache.droppedAmount} souls");
		if (isCurrent)
			lines.Add("The party is here.");
		else if (!ctl.Reachable)
			lines.Add(BlockedReason(cur, nd));
		foreach (WorldEdgeData edge in WorldMapManager.MapData.EdgesAt(nd.id)) {
			string line = EdgeLine(edge);
			if (line != "") lines.Add(line);
		}

		nodeDetailsLabel.Text = string.Join("\n", lines);

		pendingAction = PendingAction.NONE;
		if (roamer != null && (isCurrent || ctl.Reachable)) {
			pendingAction = PendingAction.FIGHT_ROAMER;
			actionButton.Text = "Fight";
		} else if (isCurrent && nd.encounterType == WorldEncounterType.BONFIRE) {
			pendingAction = PendingAction.REST;
			actionButton.Text = "Rest at Bonfire";
		} else if (!isCurrent && ctl.Reachable && WorldMapManager.IsFight(nd) && !WorldMapManager.IsCleared(nd.id)) {
			pendingAction = PendingAction.START_ENCOUNTER;
			actionButton.Text = $"Start Encounter  (Level {nd.level})";
		} else if (isCurrent && (pendingEdge = WorldMapManager.OpenableFrom(nd.id)) != null) {
			pendingAction = PendingAction.OPEN_EDGE;
			actionButton.Text = WorldMapManager.VerbFor(pendingEdge, nd.id);
		}
		actionButton.Visible = pendingAction != PendingAction.NONE;
	}

	private void ShowUnexplored(WorldMapNode ctl) {
		nodeTitleLabel.Text = "Unexplored";
		nodeDetailsLabel.Text = $"Elevation:  {ctl.data.elevation}";
		pendingAction = ctl.Reachable ? PendingAction.CLIMB : PendingAction.NONE;
		actionButton.Text = "Climb";
		actionButton.Visible = pendingAction != PendingAction.NONE;
	}

	private string BlockedReason(WorldNodeData from, WorldNodeData to) {
		WorldMapData map = WorldMapManager.MapData;
		WorldEdgeData edge = map.EdgeBetween(from.id, to.id);
		if (to.IsImpassable) return "Impassable terrain.";
		if (!map.AreNeighbors(from, to))
			return edge != null && edge.kind == WorldEdgeKind.TREE ? "No way across." : "Too far away.";
		if (edge != null && edge.kind == WorldEdgeKind.DOOR && !WorldMapManager.IsOpen(edge)) return "Locked.";
		if (to.elevation - from.elevation > 1) return "Too steep to climb.";
		return "";
	}

	// A line for an edge at the selected hex, only once the hex across is on the map.
	private string EdgeLine(WorldEdgeData edge) {
		if (WorldMapManager.SightOf(edge.a) == WorldSight.UNKNOWN || WorldMapManager.SightOf(edge.b) == WorldSight.UNKNOWN) return "";
		bool open = WorldMapManager.IsOpen(edge);
		return edge.kind switch {
			WorldEdgeKind.LADDER => open ? "Ladder — open" : "Ladder — closed",
			WorldEdgeKind.TREE => open ? "Tree — felled" : "Tree — standing",
			_ => open ? "Door — open" : "Door — locked",
		};
	}

	// A step up into the unknown. What stands on the hex is only learnt on arriving, so a fight
	// there is an ambush: it starts after the step lands and a short look round (SpringAmbush),
	// with no chance to turn back.
	private string ambushNode;

	private void Climb() {
		WorldNodeData nd = selectedNode.data;
		if (WorldMapManager.IsFight(nd) && !WorldMapManager.IsCleared(nd.id)) ambushNode = nd.id;
		MoveParty(selectedNode);
	}

	private void OnActionPressed() {
		if (locked) return;
		switch (pendingAction) {
			case PendingAction.FIGHT_ROAMER:
				if (selectedNode.data.id == WorldMapManager.CurrentNodeId) {
					WorldMapManager.StartRoamerEncounter(WorldMapManager.RoamerAt(selectedNode.data.id).id);
					EnterEncounter();
				} else {
					MoveParty(selectedNode);
				}
				break;
			case PendingAction.START_ENCOUNTER:
				WorldMapManager.StartEncounter(selectedNode.data.id);
				EnterEncounter();
				break;
			case PendingAction.CLIMB:
				Climb();
				break;
			case PendingAction.OPEN_EDGE:
				WorldMapManager.OpenEdge(pendingEdge);
				RefreshNodeStates();
				ShowInfo(selectedNode);
				break;
			case PendingAction.REST:
				// Same call the Bonfire scene's own Rest button makes — resting is one
				// action with one implementation, wherever it is triggered from.
				WorldMapManager.RestAtBonfire();
				GetTree().ChangeSceneToPacked(ResourceLoader.Load<PackedScene>("res://Scenes/Bonfire.tscn"));
				break;
		}
	}
}
