using Godot;
using System.Collections.Generic;

// What a killed enemy leaves behind. The real token is freed the frame it dies — the turn
// loop counts on that — so a ghost copies its art and transform and does the dying in its
// place: the body greys and shrinks away, a pale wisp lifts off it, embers scatter and sink.
public partial class EnemyDeath : Node2D
{
	public float collapseSeconds = 0.55f;
	public float wispSeconds = 1.0f;

	private Texture2D art;
	private Vector2 size;
	private float age;
	private float seed;
	private readonly List<Vector2> embers = new List<Vector2>();

	public static void Play(Enemy enemy) {
		if (enemy == null || !IsInstanceValid(enemy) || !enemy.IsInsideTree()) return;

		EnemyDeath ghost = BoardFx.Spawn(new EnemyDeath { art = enemy.TextureNormal, size = enemy.Size, seed = GD.Randf() * Mathf.Tau });
		if (ghost == null) return;

		ghost.GlobalTransform = enemy.GetGlobalTransform();
		for (int i = 0; i < 9; i++) {
			ghost.embers.Add(Vector2.FromAngle(GD.Randf() * Mathf.Tau) * (0.6f + GD.Randf() * 0.9f));
		}
	}

	public override void _Process(double delta) {
		age += (float)delta;
		if (age >= Mathf.Max(collapseSeconds, wispSeconds)) {
			QueueFree();
			return;
		}
		QueueRedraw();
	}

	public override void _Draw() {
		Vector2 centre = size * 0.5f;
		float r = size.X * 0.5f;

		float c = Mathf.Clamp(age / collapseSeconds, 0f, 1f);
		if (c < 1f && art != null) {
			// The life drains before the body goes: grey first, then smaller, then gone.
			float scale = 1f - 0.45f * c * c;
			float grey = Mathf.Lerp(1f, 0.35f, Mathf.Min(1f, c * 1.6f));
			float alpha = 1f - Mathf.Pow(c, 2.2f);
			Vector2 drawn = FitInside(art, size) * scale;
			DrawTextureRect(art, new Rect2(centre - drawn * 0.5f, drawn), false, new Color(grey, grey, grey, alpha));
		}

		float w = Mathf.Clamp(age / wispSeconds, 0f, 1f);
		float lift = r * 1.8f * (1f - Mathf.Pow(1f - w, 2f));
		float sway = Mathf.Sin(age * 5f + seed) * r * 0.12f;
		Vector2 wisp = centre + Vector2.Up * lift + Vector2.Right * sway;
		float wispAlpha = w < 0.15f ? w / 0.15f : 1f - (w - 0.15f) / 0.85f;
		float wispSize = r * (0.45f - 0.2f * w);
		DrawCircle(wisp, wispSize * 2.2f, new Color(0.7f, 0.85f, 1f, 0.15f * wispAlpha));
		DrawCircle(wisp, wispSize, new Color(0.85f, 0.93f, 1f, 0.55f * wispAlpha));
		DrawCircle(wisp, wispSize * 0.45f, new Color(1f, 1f, 1f, 0.9f * wispAlpha));

		foreach (Vector2 ember in embers) {
			Vector2 flung = ember * r * 1.6f * (1f - Mathf.Pow(1f - w, 2f));
			Vector2 at = centre + flung + Vector2.Down * r * 0.9f * w * w;
			DrawCircle(at, r * 0.045f * (1f - w * 0.5f), new Color(1f, 0.55f, 0.2f, (1f - w) * 0.9f));
		}
	}

	// The token draws its art keep-aspect-centred; this is the rect that gives.
	private static Vector2 FitInside(Texture2D texture, Vector2 box) {
		Vector2 art = texture.GetSize();
		if (art.X <= 0f || art.Y <= 0f) return box;
		return art * Mathf.Min(box.X / art.X, box.Y / art.Y);
	}
}
