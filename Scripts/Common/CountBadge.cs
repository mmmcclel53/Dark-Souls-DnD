using Godot;

// A number in a small circle, drawn rather than a Label in a round StyleBox: a Label cannot
// be smaller than its text and padding, so a 15px badge came out wider than it was tall.
// This one is exactly `Diameter` across, with the digit centred on the circle.
public partial class CountBadge : Control {
	public float Diameter = 14f;
	public string Text = "";
	public Color Edge = new Color(1f, 0.84f, 0.3f);
	public Color Fill = new Color(0.078f, 0.07f, 0.055f);
	public Color Ink = new Color(0.9f, 0.85f, 0.7f);
	public int FontSize = 9;

	// One font for every badge, held for the life of the game: fetching the theme's font on
	// every redraw handed the GC a fresh wrapper each time, and a wrapper finalised after the
	// engine had let the font go is a fatal "gchandle.is_released()" crash.
	private static Font font;

	public override void _Ready() {
		MouseFilter = MouseFilterEnum.Ignore;
		CustomMinimumSize = new Vector2(Diameter, Diameter);
		Size = CustomMinimumSize;
		font ??= GetThemeDefaultFont() ?? ThemeDB.FallbackFont;
	}

	public override void _Draw() {
		Vector2 c = new Vector2(Diameter, Diameter) * 0.5f;
		float r = Diameter * 0.5f;
		DrawCircle(c, r, Fill, true, -1f, true);
		DrawArc(c, r - 0.75f, 0f, Mathf.Tau, 48, Edge, 1.2f, true);
		if (font == null || Text.Length == 0) return;
		float ascent = font.GetAscent(FontSize);
		float descent = font.GetDescent(FontSize);
		Vector2 at = new Vector2(0f, c.Y + (ascent - descent) * 0.5f);
		DrawString(font, at, Text, HorizontalAlignment.Center, Diameter, FontSize, Ink);
	}
}
