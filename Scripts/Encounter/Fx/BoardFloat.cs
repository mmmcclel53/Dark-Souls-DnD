using Godot;

// A number or an icon rising from a token and fading: damage as it lands, stamina as it
// comes back, a condition as it takes hold. Numbers, not words. Drawn in board space so it
// scales and pans with the board, and frees itself once it has gone.
public partial class BoardFloat : Node2D
{
	public static readonly Color DAMAGE = new Color(0.95f, 0.25f, 0.2f);
	public static readonly Color STAMINA = new Color(0.95f, 0.8f, 0.45f);

	public float riseSeconds = 0.95f;
	public float rise = 1.6f;          // in token radii
	public float popSeconds = 0.14f;

	private string text;
	private Texture2D icon;
	private Color colour;
	private float r;
	private float delay;
	private float age;
	private Vector2 start;

	public static void Number(Control token, int amount, Color colour, float delay = 0f) {
		if (amount == 0) return;
		string text = amount > 0 ? $"+{amount}" : $"−{-amount}";
		Show(token, text, null, colour, delay);
	}

	public static void Icon(Control token, Texture2D icon, Color tint, float delay = 0f) {
		if (icon != null) Show(token, null, icon, tint, delay);
	}

	private static void Show(Control token, string text, Texture2D icon, Color colour, float delay) {
		if (token == null || !IsInstanceValid(token) || !token.IsInsideTree()) return;

		BoardFloat effect = BoardFx.Spawn(new BoardFloat { text = text, icon = icon, colour = colour, delay = delay });
		if (effect == null) return;

		Transform2D toLocal = effect.GetGlobalTransform().AffineInverse();
		effect.r = BoardFx.LocalRadius(effect, token);
		effect.start = toLocal * BoardFx.GlobalCentre(token) + Vector2.Up * effect.r * 0.7f;
		// A little sideways lean at random, so two landing together do not stack exactly.
		effect.start += Vector2.Right * effect.r * (GD.Randf() - 0.5f) * 0.6f;
	}

	public override void _Process(double delta) {
		age += (float)delta;
		if (age >= delay + riseSeconds) {
			QueueFree();
			return;
		}
		QueueRedraw();
	}

	public override void _Draw() {
		float lived = age - delay;
		if (lived < 0f) return;
		float t = lived / riseSeconds;
		float eased = 1f - Mathf.Pow(1f - t, 2f);
		float fade = t < 0.55f ? 1f : 1f - (t - 0.55f) / 0.45f;
		float pop = lived < popSeconds ? 1f + 0.5f * (1f - lived / popSeconds) : 1f;
		Vector2 at = start + Vector2.Up * r * rise * eased;

		if (icon != null) {
			float side = r * 0.9f * pop;
			DrawTextureRect(icon, new Rect2(at - new Vector2(side, side) * 0.5f, new Vector2(side, side)), false, new Color(colour, fade));
			return;
		}

		Font font = ThemeDB.FallbackFont;
		int size = Mathf.Max(8, Mathf.RoundToInt(r * pop));
		float width = r * 4f;
		Vector2 origin = new Vector2(at.X - width * 0.5f, at.Y + (font.GetAscent(size) - font.GetDescent(size)) * 0.5f);
		int outline = Mathf.Max(2, Mathf.RoundToInt(size * 0.18f));
		DrawStringOutline(font, origin, text, HorizontalAlignment.Center, width, size, outline, new Color(0f, 0f, 0f, 0.9f * fade));
		DrawString(font, origin, text, HorizontalAlignment.Center, width, size, new Color(colour, fade));
	}
}
