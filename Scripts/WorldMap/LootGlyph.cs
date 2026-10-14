using Godot;

// The two pieces of loot with no card or icon art, drawn in code for the reveal panel: an
// Estus Shard (a tall faceted amber crystal) and a key.
public partial class LootGlyph : Control {
	public enum Shape { SHARD, KEY }

	public Shape shape = Shape.SHARD;

	private static readonly Color AMBER_LIGHT = new Color(1f, 0.82f, 0.38f);
	private static readonly Color AMBER = new Color(0.93f, 0.55f, 0.12f);
	private static readonly Color AMBER_DARK = new Color(0.55f, 0.24f, 0.06f);
	private static readonly Color BRASS = new Color(0.86f, 0.72f, 0.38f);

	public override void _Draw() {
		if (shape == Shape.SHARD) DrawShard();
		else DrawKey();
	}

	private Vector2 At(float x, float y) => new Vector2(x * Size.X, y * Size.Y);

	private void DrawShard() {
		DrawCircle(At(0.5f, 0.5f), Size.X * 0.55f, AMBER with { A = 0.12f });
		DrawCircle(At(0.5f, 0.5f), Size.X * 0.4f, AMBER with { A = 0.14f });

		Vector2 top = At(0.5f, 0.02f), bottom = At(0.5f, 0.98f);
		Vector2 upperLeft = At(0.16f, 0.3f), upperRight = At(0.84f, 0.3f);
		Vector2 lowerLeft = At(0.3f, 0.8f), lowerRight = At(0.7f, 0.8f);
		Vector2 waistLeft = At(0.42f, 0.34f), waistRight = At(0.6f, 0.38f);

		DrawColoredPolygon(new[] { top, upperLeft, waistLeft }, AMBER_LIGHT);
		DrawColoredPolygon(new[] { top, waistLeft, waistRight }, AMBER_LIGHT.Lerp(AMBER, 0.5f));
		DrawColoredPolygon(new[] { top, waistRight, upperRight }, AMBER);
		DrawColoredPolygon(new[] { upperLeft, lowerLeft, bottom, waistLeft }, AMBER);
		DrawColoredPolygon(new[] { waistLeft, bottom, waistRight }, AMBER.Lerp(AMBER_DARK, 0.35f));
		DrawColoredPolygon(new[] { waistRight, bottom, lowerRight, upperRight }, AMBER_DARK);

		Color edge = AMBER_DARK.Darkened(0.5f);
		var outline = new[] { top, upperRight, lowerRight, bottom, lowerLeft, upperLeft, top };
		DrawPolyline(outline, edge, 2f, true);
		DrawLine(top, waistLeft, edge, 1.2f, true);
		DrawLine(top, waistRight, edge, 1.2f, true);
		DrawLine(waistLeft, bottom, edge, 1.2f, true);
		DrawLine(waistRight, bottom, edge, 1.2f, true);
		DrawLine(At(0.34f, 0.2f), At(0.42f, 0.1f), Colors.White with { A = 0.75f }, 2f, true);
	}

	private void DrawKey() {
		float w = Size.X;
		Vector2 bow = At(0.5f, 0.2f);
		float radius = w * 0.26f;
		DrawCircle(bow, radius * 1.5f, BRASS with { A = 0.1f });
		DrawArc(bow, radius, 0, Mathf.Tau, 40, BRASS, w * 0.1f, true);
		DrawArc(bow, radius - w * 0.05f, 0, Mathf.Tau, 40, BRASS.Darkened(0.5f), 1.5f, true);

		float shaft = w * 0.09f;
		Vector2 neck = bow + new Vector2(0, radius);
		DrawLine(neck, At(0.5f, 0.94f), BRASS, shaft, true);
		DrawLine(neck, At(0.5f, 0.94f), BRASS.Lightened(0.35f), shaft * 0.3f, true);
		foreach (float y in new[] { 0.94f, 0.8f }) {
			DrawLine(At(0.5f, y), At(0.78f, y), BRASS, shaft, true);
		}
		DrawLine(At(0.78f, 0.8f), At(0.78f, 0.94f), BRASS, shaft, true);
	}
}
