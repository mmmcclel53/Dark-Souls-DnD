using Godot;

// Zoom and pan for the encounter board, the same controls as the world map: wheel zooms
// about the cursor, dragging pans, R resets.
//
// This view is the whole stage below the title bar; the activation bar, the action bar and
// the party pane float over it with transparent backgrounds. At rest the board is fitted
// into the clear space between them (the safe rect), so nothing covers it. Zoomed or
// panned, it runs on underneath the HUD the way the world map does.
//
// Only the board's Size, Scale and Position change, so everything anchored inside it (the
// grid, the tokens, RescaleModels) is untouched and clicks still land through the
// transform. The pan is clamped so some part of the board always stays under the middle
// of the safe rect — any corner can be brought to the centre, none can be lost.
//
// Left-drag pans only when it starts off a button, so clicking a node or token still does
// what it did; right- and middle-drag pan from anywhere.
public partial class BoardCamera : Control
{

	private const float ZOOM_MIN = 1f;
	private const float ZOOM_MAX = 3f;
	private const float ZOOM_FACTOR = 1.1f;
	private const float DRAG_THRESHOLD = 6f;

	[Export] public Control board;
	[Export] public Button resetButton;

	// The HUD the fitted board has to clear.
	[Export] public Control topHud;
	[Export] public Control bottomHud;
	[Export] public float leftInset = 168f;   // the floating party pane
	[Export] public float rightInset = 168f;  // matches it, so the board sits centred
	[Export] public float fitMargin = 6f;

	private float zoom = 1f;
	private Vector2 offset = Vector2.Zero;   // from the fitted position, in view pixels

	private bool dragging;
	private bool panning;
	private Vector2 dragDistance;

	public override void _Ready() {
		ClipContents = true;
		Resized += Apply;
		if (topHud != null) topHud.Resized += Apply;
		if (bottomHud != null) bottomHud.Resized += Apply;
		if (resetButton != null) resetButton.Pressed += ResetView;
		CallDeferred(nameof(Apply));
	}

	public void ResetView() {
		zoom = 1f;
		offset = Vector2.Zero;
		Apply();
	}

	public override void _Input(InputEvent @event) {
		if (@event is InputEventMouseButton mb) {
			OnMouseButton(mb);
		} else if (@event is InputEventMouseMotion mm && dragging) {
			dragDistance += mm.Relative;
			if (!panning && dragDistance.Length() >= DRAG_THRESHOLD) panning = true;
			if (panning) {
				offset += mm.Relative;
				Apply();
			}
		}
	}

	private void OnMouseButton(InputEventMouseButton mb) {
		if (!mb.Pressed) {
			// A drag that turned into a pan should not also count as a click on release.
			if (dragging && panning) GetViewport().SetInputAsHandled();
			dragging = panning = false;
			return;
		}
		if (!IsOverBoard()) return;

		switch (mb.ButtonIndex) {
			case MouseButton.WheelUp:
				ZoomAt(mb.Position, ZOOM_FACTOR);
				GetViewport().SetInputAsHandled();
				break;
			case MouseButton.WheelDown:
				ZoomAt(mb.Position, 1f / ZOOM_FACTOR);
				GetViewport().SetInputAsHandled();
				break;
			case MouseButton.Left:
				if (GetViewport().GuiGetHoveredControl() is BaseButton) return;
				StartDrag();
				break;
			case MouseButton.Right:
			case MouseButton.Middle:
				StartDrag();
				panning = true;
				break;
		}
	}

	private void StartDrag() {
		dragging = true;
		panning = false;
		dragDistance = Vector2.Zero;
	}

	public override void _UnhandledInput(InputEvent @event) {
		if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.R) {
			ResetView();
		}
	}

	// Only when the cursor is over the board itself, not a modal or a HUD control on top.
	// The HUD's containers ignore the mouse, so their empty space counts as board.
	private bool IsOverBoard() {
		Control hovered = GetViewport().GuiGetHoveredControl();
		return hovered != null && (hovered == this || IsAncestorOf(hovered));
	}

	private Vector2 ToLocal(Vector2 viewportPoint) =>
		GetGlobalTransformWithCanvas().AffineInverse() * viewportPoint;

	// The clear space between the HUD pieces, in this view's coordinates.
	private Rect2 SafeRect() {
		float top = topHud != null ? ToLocal(topHud.GetGlobalRect().End).Y : 0f;
		float bottom = bottomHud != null ? ToLocal(bottomHud.GetGlobalRect().Position).Y : Size.Y;
		Rect2 safe = new Rect2(leftInset, top, Size.X - leftInset - rightInset, bottom - top);
		return safe.Grow(-fitMargin);
	}

	private float FitSide(Rect2 safe) => Mathf.Max(1f, Mathf.Min(safe.Size.X, safe.Size.Y));

	private Vector2 FitOrigin(Rect2 safe, float side) =>
		safe.Position + (safe.Size - new Vector2(side, side)) * 0.5f;

	// Zoom keeping the board point under the cursor where it is.
	private void ZoomAt(Vector2 viewportPoint, float factor) {
		if (board == null) return;
		Vector2 local = ToLocal(viewportPoint);
		Vector2 boardPoint = (local - board.Position) / zoom;

		zoom = Mathf.Clamp(zoom * factor, ZOOM_MIN, ZOOM_MAX);

		Rect2 safe = SafeRect();
		Vector2 origin = FitOrigin(safe, FitSide(safe));
		offset = local - boardPoint * zoom - origin;
		Apply();
	}

	private void Apply() {
		if (board == null) return;

		Rect2 safe = SafeRect();
		float side = FitSide(safe);
		Vector2 origin = FitOrigin(safe, side);

		float span = side * zoom;
		Vector2 centre = safe.GetCenter();
		Vector2 position = origin + offset;
		position.X = Mathf.Clamp(position.X, centre.X - span, centre.X);
		position.Y = Mathf.Clamp(position.Y, centre.Y - span, centre.Y);
		offset = position - origin;

		board.Size = new Vector2(side, side);
		board.Scale = new Vector2(zoom, zoom);
		board.Position = position;
	}
}
