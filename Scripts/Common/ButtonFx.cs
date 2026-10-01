using Godot;

// Life for a button: a glint that sweeps across it when the pointer arrives, a ripple when
// it is pressed, and for an armed attack row a border that breathes in step with the
// pulsing target nodes. A full-rect child that ignores the mouse and draws over the
// button's face. A round button gets its glint as a spark running round the rim and its
// ripple as a ring spreading past the edge, so nothing squares off the circle.
public partial class ButtonFx : Control
{
	public const float BREATH_RATE = 0.6f;     // Hz, GameNode.pulseRate

	public Color colour = new Color(1f, 0.9f, 0.65f);
	public float glintSeconds = 0.45f;
	public float rippleSeconds = 0.35f;

	private BaseButton button;
	private bool round;
	private bool breathing;
	private float glint = -1f;
	private float ripple = -1f;
	private float time;

	public static ButtonFx Attach(BaseButton button, bool round = false, bool breathing = false) {
		if (button == null) return null;

		ButtonFx fx = new ButtonFx {
			button = button,
			round = round,
			breathing = breathing,
			MouseFilter = MouseFilterEnum.Ignore,
			ClipContents = !round,
		};
		button.AddChild(fx);
		fx.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		button.MouseEntered += fx.Glint;
		button.Pressed += fx.Ripple;
		return fx;
	}

	public void Glint() {
		if (button == null || button.Disabled) return;
		glint = 0f;
	}

	// A glint whatever the button's state: for when it has just come alive.
	public void Pulse() {
		glint = 0f;
	}

	public void Ripple() {
		ripple = 0f;
	}

	public override void _Process(double delta) {
		float dt = (float)delta;
		time += dt;
		if (glint >= 0f && (glint += dt / glintSeconds) >= 1f) glint = -1f;
		if (ripple >= 0f && (ripple += dt / rippleSeconds) >= 1f) ripple = -1f;
		if (glint >= 0f || ripple >= 0f || breathing) QueueRedraw();
	}

	public override void _Draw() {
		if (round) DrawRound(); else DrawRow();
	}

	private void DrawRound() {
		Vector2 centre = Size * 0.5f;
		float radius = Mathf.Min(Size.X, Size.Y) * 0.5f;

		if (glint >= 0f) {
			float start = -Mathf.Pi * 0.5f + glint * Mathf.Tau;
			float strength = Mathf.Sin(glint * Mathf.Pi);
			DrawArc(centre, radius - 2f, start, start + Mathf.Pi / 3f, 24, new Color(colour, 0.9f * strength), 3f, true);
			DrawCircle(centre + Vector2.FromAngle(start + Mathf.Pi / 3f) * (radius - 2f), 3f, new Color(1f, 1f, 1f, strength));
		}
		if (ripple >= 0f) {
			float fade = 1f - ripple;
			DrawArc(centre, radius * (1f + 0.35f * ripple), 0f, Mathf.Tau, 48, new Color(colour, 0.8f * fade), 1f + 3f * fade, true);
		}
	}

	private void DrawRow() {
		Rect2 rect = new Rect2(Vector2.Zero, Size);

		if (glint >= 0f) {
			float strength = Mathf.Sin(glint * Mathf.Pi);
			float x = -Size.X * 0.3f + glint * Size.X * 1.6f;
			float lean = Size.Y * 0.6f;
			Band(x, Size.X * 0.16f, lean, new Color(colour, 0.22f * strength));
			Band(x + Size.X * 0.05f, Size.X * 0.04f, lean, new Color(1f, 1f, 1f, 0.35f * strength));
		}
		if (ripple >= 0f) {
			float fade = 1f - ripple;
			DrawRect(rect, new Color(colour, 0.3f * fade * fade));
			DrawRect(rect.Grow(-1f), new Color(colour, 0.9f * fade), false, 2f);
		}
		if (breathing) {
			float breath = 0.5f + 0.5f * Mathf.Sin(time * Mathf.Tau * BREATH_RATE);
			DrawRect(rect.Grow(-1f), new Color(colour, 0.12f + 0.32f * breath), false, 2f);
		}
	}

	// A slanted band across the row, clipped to it.
	private void Band(float x, float width, float lean, Color fill) {
		Vector2[] points = {
			new Vector2(x, 0f), new Vector2(x + width, 0f),
			new Vector2(x + width - lean, Size.Y), new Vector2(x - lean, Size.Y),
		};
		DrawColoredPolygon(points, fill);
	}
}
