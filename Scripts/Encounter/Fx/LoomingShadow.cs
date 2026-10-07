using Godot;

// The dark a heavy enemy stands in: a soft pool under its token, and a dark mist that keeps
// welling up from beneath it, drifting slowly outwards, spreading and thinning away. Each
// puff runs its own slow cycle, so the fog is never still and never repeats in step.
// Subtle at ×1.5 presence (5 health), thicker and wider as the enemy grows.
// A child of the enemy's button drawn behind it, so it travels, leans and hides with the
// token and frees with it; drawn in the button's own space, so it scales with it.
public partial class LoomingShadow : Node2D
{
	public const float MIN_PRESENCE = 1.5f;

	public float weight;        // 0 at ×1, 1 from ×2.5
	public Color shade = new Color(0.035f, 0.02f, 0.055f);

	// Each puff is drawn as these nested discs, so it is densest in the middle and soft-edged.
	private static readonly float[] LAYERS = { 1f, 0.75f, 0.5f };

	private Vector2 centre;
	private float r;
	private float time;
	private (float angle, float drift, float offset, float seconds, float reach)[] puffs;

	public override void _Ready() {
		ShowBehindParent = true;
		Vector2 size = GetParent<Control>().Size;
		centre = size * 0.5f + new Vector2(0f, size.Y * 0.025f);
		r = Mathf.Min(size.X, size.Y) * 0.5f;
		time = GD.Randf() * 100f;

		int count = 12 + Mathf.RoundToInt(8f * weight);
		puffs = new (float, float, float, float, float)[count];
		for (int i = 0; i < count; i++) {
			puffs[i] = (
				Mathf.Tau * (i + GD.Randf() * 0.8f) / count,
				(GD.Randf() - 0.5f) * 0.25f,
				GD.Randf(),
				4f + GD.Randf() * 3f,
				0.35f + GD.Randf() * 0.25f);
		}
	}

	public override void _Process(double delta) {
		time += (float)delta;
		QueueRedraw();
	}

	public override void _Draw() {
		float strength = 0.55f + 0.45f * weight;

		// The pool: stacked soft discs, darkest at the middle, fading out well past the
		// token's edge (the printed rim covers the first of it).
		for (int k = 5; k >= 0; k--) {
			DrawCircle(centre, r * (1f + 0.07f * k), new Color(shade, 0.12f * strength));
		}

		foreach (var puff in puffs) DrawPuff(puff, strength);
	}

	// Rises from under the token's edge, drifts out turning a little, swells and fades.
	private void DrawPuff((float angle, float drift, float offset, float seconds, float reach) p, float strength) {
		float life = Mathf.PosMod(time / p.seconds + p.offset, 1f);
		float angle = p.angle + p.drift * life + Mathf.Sin(time * 0.3f + p.angle * 3f) * 0.05f;
		float distance = r * (0.92f + p.reach * (0.7f + 0.5f * weight) * life);
		float size = r * (0.16f + 0.24f * life) * (0.8f + 0.3f * weight);
		float alpha = 0.2f * strength * Mathf.Sin(Mathf.Pi * life);

		Vector2 at = centre + Vector2.FromAngle(angle) * distance;
		foreach (float layer in LAYERS) DrawCircle(at, size * layer, new Color(shade, alpha / LAYERS.Length));
	}
}
