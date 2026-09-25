using Godot;

// A pulsing ring laid over a board token, so the models in an exchange can be picked out
// without the tokens carrying any chrome of their own. Attached for the length of the
// exchange and freed afterwards; the token never knows it is there.
public partial class TokenHighlight : Control
{

	public Color color = Colors.Red;
	public float pulseSpeed = 5f;

	private float time;

	public static TokenHighlight Attach(Control token, Color color) {
		if (token == null || !IsInstanceValid(token)) return null;

		TokenHighlight ring = new TokenHighlight { color = color, MouseFilter = MouseFilterEnum.Ignore };
		token.AddChild(ring);
		ring.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		return ring;
	}

	public static void Detach(TokenHighlight ring) {
		if (ring != null && IsInstanceValid(ring)) ring.QueueFree();
	}

	public override void _Process(double delta) {
		time += (float)delta;
		QueueRedraw();
	}

	// Sized from the token's own rect, so it follows the token's scale on any board size.
	public override void _Draw() {
		float radius = Mathf.Min(Size.X, Size.Y) * 0.5f;
		if (radius <= 0f) return;

		// Tokens are drawn at a fraction of their art size, so these widths have to be
		// generous in local units to survive the scale; both sit outside the token's edge.
		float width = radius * 0.2f;
		float pulse = 0.5f + 0.5f * Mathf.Sin(time * pulseSpeed);
		Vector2 centre = Size * 0.5f;

		Color glow = new Color(color, 0.25f + 0.35f * pulse);
		Color ring = new Color(color, 0.8f + 0.2f * pulse);
		DrawArc(centre, radius + width * 1.6f, 0f, Mathf.Tau, 64, glow, width * 1.6f, true);
		DrawArc(centre, radius + width * 0.5f, 0f, Mathf.Tau, 64, ring, width, true);
	}
}
