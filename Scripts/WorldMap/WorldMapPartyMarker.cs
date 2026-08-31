using Godot;

// Pulsing glow ring plus a small banner marking the party's current hex.
public partial class WorldMapPartyMarker : Control {
	private float phase;

	public override void _Ready() {
		MouseFilter = MouseFilterEnum.Ignore;
		Size = Vector2.Zero;
		ZIndex = 100;
	}

	public override void _Process(double delta) {
		phase += (float)delta;
		QueueRedraw();
	}

	public override void _Draw() {
		float pulse = 0.55f + 0.35f * Mathf.Sin(phase * 3f);
		DrawHexOutline(WorldMapNode.HEX_RADIUS - 2f, new Color(1f, 0.84f, 0.35f, pulse), 3f);
		DrawHexOutline(WorldMapNode.HEX_RADIUS - 8f, new Color(1f, 0.84f, 0.35f, pulse * 0.35f), 5f);

		// Banner: shadow, pole, pennant.
		DrawCircle(new Vector2(0, 8), 5f, new Color(0, 0, 0, 0.3f));
		DrawLine(new Vector2(0, 8), new Vector2(0, -26), new Color(0.35f, 0.24f, 0.15f), 2.5f);
		DrawCircle(new Vector2(0, -26), 2f, new Color(0.8f, 0.7f, 0.4f));
		DrawColoredPolygon(new[] {
			new Vector2(1, -25), new Vector2(17, -19), new Vector2(1, -13),
		}, new Color(0.72f, 0.11f, 0.11f));
	}

	private void DrawHexOutline(float radius, Color color, float width) {
		var hex = WorldMapNode.HexPoints(Vector2.Zero, radius, WorldMapNode.FlatTop);
		var closed = new Vector2[hex.Length + 1];
		hex.CopyTo(closed, 0);
		closed[hex.Length] = hex[0];
		DrawPolyline(closed, color, width, true);
	}
}
