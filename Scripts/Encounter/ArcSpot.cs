using Godot;

// A place against a boss's base that a character can step to (p28): into an arc from a
// neighbouring node, round the boss to the next arc, or anywhere round it on a dodge. Drawn
// and clicked like a lit node, on the boss's own node where the character would stand.
public partial class ArcSpot : Control
{
	public System.Action onPick;
	private Color colour;
	private float time;

	public static ArcSpot Show(Enemy boss, Vector2I side, Color colour, System.Action onPick) {
		if (boss?.GetParent()?.GetParent() is not GameNode node) return null;
		ArcSpot spot = new ArcSpot {
			colour = colour,
			onPick = onPick,
			MouseFilter = MouseFilterEnum.Stop,
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		node.AddChild(spot);
		float size = node.Size.X * 0.5f;
		spot.Size = new Vector2(size, size);
		spot.Position = node.Size * 0.5f + EncounterManager.SideOffset(boss, side) * node.Size.X - spot.Size * 0.5f;
		return spot;
	}

	public override void _GuiInput(InputEvent @event) {
		if (@event is not InputEventMouseButton button || !button.Pressed || button.ButtonIndex != MouseButton.Left) return;
		AcceptEvent();
		onPick?.Invoke();
	}

	// Only the drawn disc, which stops short of the boss's rim, so a click on the boss is never
	// taken for a step round it.
	public override bool _HasPoint(Vector2 point) => point.DistanceTo(Size * 0.5f) <= Size.X * 0.5f * DISC;

	private const float DISC = 0.72f;

	public bool ContainsGlobal(Vector2 global) => _HasPoint(GetGlobalTransform().AffineInverse() * global);

	public override void _Process(double delta) {
		time += (float)delta;
		QueueRedraw();
	}

	public override void _Draw() {
		Vector2 centre = Size * 0.5f;
		float r = Size.X * 0.5f;
		float wave = 0.5f + 0.5f * Mathf.Sin(time * Mathf.Tau * 0.6f);
		DrawCircle(centre, r * DISC, new Color(colour, colour.A * (0.55f + 0.45f * wave)));
		DrawArc(centre, r * DISC, 0f, Mathf.Tau, 40, new Color(colour.R, colour.G, colour.B, 0.95f), Mathf.Max(1f, r * 0.08f), true);
		float ripple = time * 0.4f % 1f;
		DrawArc(centre, r * (DISC + 0.35f * ripple), 0f, Mathf.Tau, 40, new Color(colour.R, colour.G, colour.B, Mathf.Sin(ripple * Mathf.Pi) * 0.4f), Mathf.Max(1f, r * 0.04f), true);
	}
}
