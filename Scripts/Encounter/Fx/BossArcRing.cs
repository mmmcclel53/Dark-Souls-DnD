using Godot;

// The X on a boss's base (p27), drawn round its token: four arcs with the lines between them,
// a gold point at the front, and the arcs of the last attack on the top card of the discard
// pile coloured as the card draws them, green for attacked and red for weak (p28). The ring
// turns with the boss, easing round; the art inside it never turns.
public partial class BossArcRing : Control
{
	public Enemy boss;

	private static readonly Color IRON = new Color(0.13f, 0.11f, 0.1f, 0.85f);
	private static readonly Color BRONZE = new Color(0.62f, 0.5f, 0.3f, 0.9f);
	private static readonly Color ATTACKED = new Color(0.28f, 0.7f, 0.38f, 0.9f);
	private static readonly Color WEAK = new Color(0.86f, 0.2f, 0.13f, 0.95f);
	private static readonly Color GOLD = new Color(0.93f, 0.76f, 0.36f);

	private float angle;
	private float time;

	public override void _Ready() {
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		angle = Facing();
	}

	private float Facing() => BossArcs.ScreenDirection(boss?.facing ?? new Vector2I(-1, 1)).Angle();

	public override void _Process(double delta) {
		if (boss == null || !IsInstanceValid(boss)) return;
		time += (float)delta;
		angle = Mathf.LerpAngle(angle, Facing(), 1f - Mathf.Exp(-9f * (float)delta));
		QueueRedraw();
	}

	public override void _Draw() {
		if (boss?.behaviour == null) return;
		Vector2 centre = Size * 0.5f;
		float r = Mathf.Min(Size.X, Size.Y) * 0.5f;
		float width = r * 0.11f;
		float mid = r * 1.02f + width * 0.5f;
		BossAction.Arcs attacked = boss.behaviour.AttackedArcs;
		BossAction.Arcs weak = boss.behaviour.WeakArcs;
		float breathe = 0.75f + 0.25f * Mathf.Sin(time * Mathf.Tau * 0.8f);

		// Front, right, back, left: clockwise on the board, as the angle grows.
		for (int i = 0; i < 4; i++) {
			BossAction.Arcs arc = BossArcs.EACH[i];
			float at = angle + i * Mathf.Pi * 0.5f;
			Color colour = (weak & arc) != 0 ? new Color(WEAK, WEAK.A * breathe)
				: (attacked & arc) != 0 ? ATTACKED
				: IRON;
			DrawArc(centre, mid, at - Mathf.Pi * 0.25f, at + Mathf.Pi * 0.25f, 24, colour, width, true);
		}
		DrawArc(centre, mid + width * 0.5f, 0f, Mathf.Tau, 64, BRONZE, Mathf.Max(1f, width * 0.12f), true);
		DrawArc(centre, mid - width * 0.5f, 0f, Mathf.Tau, 64, BRONZE, Mathf.Max(1f, width * 0.12f), true);

		// The X's lines, across the ring.
		for (int i = 0; i < 4; i++) {
			Vector2 along = Vector2.FromAngle(angle + Mathf.Pi * 0.25f + i * Mathf.Pi * 0.5f);
			DrawLine(centre + along * (mid - width * 0.8f), centre + along * (mid + width * 0.8f), BRONZE, Mathf.Max(1.5f, width * 0.22f), true);
		}

		// The front: a gold point just outside the ring.
		Vector2 front = Vector2.FromAngle(angle);
		Vector2 across = front.Orthogonal();
		Vector2 tip = centre + front * (mid + width * 1.9f);
		Vector2 heel = centre + front * (mid + width * 0.6f);
		DrawColoredPolygon(new[] { tip, heel + across * width * 0.9f, heel - across * width * 0.9f }, GOLD);
	}
}
