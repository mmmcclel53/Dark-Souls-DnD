using Godot;

// A row of the game's cubes: black for Stamina to be spent, red for damage. Drawn rather
// than built from ColorRects so a count of zero takes no width and the row can be resized
// with one number.
public partial class CubeRow : Control
{

	public int count;
	public float side = 10f;
	public float gap = 3f;
	public Color fill = new Color(0.10f, 0.09f, 0.07f);
	public Color edge = new Color(0.47f, 0.38f, 0.20f);

	public static CubeRow Stamina(int count, float side = 10f) => Make(count, side,
		new Color(0.10f, 0.09f, 0.07f), new Color(0.47f, 0.38f, 0.20f));

	public static CubeRow Damage(int count, float side = 10f) => Make(count, side,
		new Color(0.64f, 0.17f, 0.13f), new Color(0.86f, 0.35f, 0.27f));

	private static CubeRow Make(int count, float side, Color fill, Color edge) {
		CubeRow row = new CubeRow {
			side = side,
			fill = fill,
			edge = edge,
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		row.Set(count);
		return row;
	}

	public void Set(int newCount) {
		count = Mathf.Max(0, newCount);
		float width = count == 0 ? 0f : count * (side + gap) - gap;
		CustomMinimumSize = new Vector2(width, side);
		QueueRedraw();
	}

	public override void _Draw() {
		for (int i = 0; i < count; i++) {
			Rect2 box = new Rect2(i * (side + gap), (Size.Y - side) * 0.5f, side, side);
			DrawRect(box, fill);
			DrawRect(box, edge, false, 1f);
		}
	}
}
