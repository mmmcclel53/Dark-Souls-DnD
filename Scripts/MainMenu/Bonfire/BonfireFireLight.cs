using Godot;

// The living fire on the Bonfire screen: a flickering light over the flames, a slow pulse of
// warmth across the floor, and sparks lifting off the pit. All of it hangs on one point in
// the floor art, `fireUv`, and follows it wherever the covered-aspect stretch puts it, so the
// fire stays lit at any window size.
//
// The flicker is a sum of sines at unrelated frequencies plus a little jitter, which is
// enough to read as fire without a noise texture.
public partial class BonfireFireLight : Control {

	[Export] public TextureRect art;
	[Export] public TextureRect glow;
	[Export] public TextureRect warmth;
	[Export] public CpuParticles2D sparks;

	// Where the flames are in the art, as a fraction of its width and height.
	[Export] public Vector2 fireUv = new Vector2(0.546f, 0.545f);

	// Sizes as fractions of the drawn art's height, so they scale with the window.
	[Export] public float glowSize = 0.34f;
	[Export] public float warmthSize = 1.3f;

	[Export] public float glowAlpha = 0.6f;
	[Export] public float glowFlicker = 0.22f;
	[Export] public float warmthAlpha = 0.28f;
	[Export] public float warmthPulse = 0.08f;
	[Export] public float jitter = 0.05f;

	private double time;
	private float jitterValue;
	private RandomNumberGenerator random = new RandomNumberGenerator();

	public override void _Ready() {
		MouseFilter = MouseFilterEnum.Ignore;
		if (sparks != null) sparks.Emitting = true;
	}

	public override void _Process(double delta) {
		if (art?.Texture == null) return;
		time += delta;

		// A fresh nudge a few times a second, eased toward, so the light never sits still.
		float target = (random.Randf() - 0.5f) * 2f * jitter;
		jitterValue = Mathf.Lerp(jitterValue, target, (float)delta * 6f);

		float t = (float)time;
		float flicker = Mathf.Sin(t * 7.3f) * 0.5f + Mathf.Sin(t * 13.1f + 1.3f) * 0.3f + Mathf.Sin(t * 23.7f + 0.4f) * 0.2f;
		float pulse = Mathf.Sin(t * 1.1f) * 0.6f + Mathf.Sin(t * 2.7f + 2f) * 0.4f;

		Rect2 drawn = DrawnArtRect();
		Vector2 fire = drawn.Position + drawn.Size * fireUv;
		float unit = drawn.Size.Y;

		if (glow != null) {
			float size = unit * glowSize * (1f + flicker * 0.06f);
			Place(glow, fire, size, glowAlpha + flicker * glowFlicker + jitterValue);
		}
		if (warmth != null) {
			Place(warmth, fire, unit * warmthSize, warmthAlpha + pulse * warmthPulse + flicker * 0.03f);
		}
		if (sparks != null) {
			sparks.Position = fire;
			sparks.Scale = Vector2.One * (unit / 1080f);
		}
	}

	private static void Place(TextureRect rect, Vector2 centre, float size, float alpha) {
		rect.Size = new Vector2(size, size);
		rect.Position = centre - rect.Size / 2f;
		rect.Modulate = new Color(rect.Modulate.R, rect.Modulate.G, rect.Modulate.B, Mathf.Clamp(alpha, 0f, 1f));
	}

	// Where KEEP_ASPECT_COVERED actually draws the texture inside the art rect. This control
	// is a full-rect child of the art, so the art's local space is its own.
	private Rect2 DrawnArtRect() {
		Vector2 rect = art.Size;
		Vector2 tex = art.Texture.GetSize();
		float scale = Mathf.Max(rect.X / tex.X, rect.Y / tex.Y);
		Vector2 drawnSize = tex * scale;
		return new Rect2((rect - drawnSize) / 2f, drawnSize);
	}
}
