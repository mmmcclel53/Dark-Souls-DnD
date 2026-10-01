using Godot;

// Autoloaded soul count in the top-right corner, styled after the Dark Souls games': a dark
// plate ruled top and bottom in fine silver double lines with small diamond ornaments, the
// soul icon in a framed square at the left end, and the number right-aligned. When
// the pool changes the number counts to its new value instead of jumping.
//
// It floats over every scene, so a scene that wants it calls ShowBelow with its title bar;
// the plate then hangs just under that bar's right end and follows it on resize. Scenes
// that do not want it call Hide (or simply never show it), and a scene leaving calls Release.
public partial class SoulCounter : CanvasLayer
{
	private const string ICON_PATH = "res://Resources/Images/Sprites/Soul.png";

	private static readonly Vector2 PLATE_SIZE = new Vector2(104, 34);
	private const float ICON_INSET = 7f;       // inside its framed square at the left end
	private const float EDGE_MARGIN = 16f;
	private const float DROP = 10f;
	private const float COUNT_SECONDS = 0.6f;

	private static readonly Color FILL_TOP = new Color(0.10f, 0.09f, 0.08f, 0.82f);
	private static readonly Color FILL_BOTTOM = new Color(0.03f, 0.03f, 0.03f, 0.82f);
	private static readonly Color SILVER = new Color(0.72f, 0.70f, 0.64f, 0.95f);
	private static readonly Color SILVER_DIM = new Color(0.46f, 0.44f, 0.40f, 0.85f);
	private static readonly Color NUMBER = new Color(0.87f, 0.85f, 0.80f);

	private Control plate;
	private Label value;
	private Control anchor;
	private int target = -1;
	private float displayed;
	private Tween counting;

	public override void _Ready() {
		Layer = 6;          // above the party pane (5), below the equipment modal (10)
		Visible = false;
		Build();
	}

	private void Build() {
		plate = new Control { CustomMinimumSize = PLATE_SIZE, Size = PLATE_SIZE, MouseFilter = Control.MouseFilterEnum.Ignore };
		plate.Draw += DrawPlate;
		AddChild(plate);

		float box = PLATE_SIZE.Y;
		var icon = new TextureRect {
			Texture = GD.Load<Texture2D>(ICON_PATH),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Position = new Vector2(ICON_INSET, ICON_INSET),
			Size = new Vector2(box - 2f * ICON_INSET, box - 2f * ICON_INSET),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		plate.AddChild(icon);

		float numberLeft = box + 4f;
		value = new Label {
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
			Position = new Vector2(numberLeft, 0),
			Size = new Vector2(PLATE_SIZE.X - numberLeft - 12f, PLATE_SIZE.Y),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		value.AddThemeFontSizeOverride("font_size", 20);
		value.AddThemeColorOverride("font_color", NUMBER);
		value.AddThemeConstantOverride("outline_size", 0);
		plate.AddChild(value);
	}

	public void ShowBelow(Control bar) {
		anchor = bar;
		Visible = true;
		Snap();
		Place();
	}

	public new void Hide() {
		Visible = false;
		anchor = null;
	}

	// For a scene on its way out. Scenes are swapped by adding the next one before freeing
	// the last, so the next scene has usually already shown the counter under its own bar;
	// only the scene that still owns it may take it down.
	public void Release(Control bar) {
		if (anchor == bar) Hide();
	}

	public override void _Process(double delta) {
		if (!Visible) return;
		Place();
		if (SoulCache.current != target) CountTo(SoulCache.current);
	}

	private void Snap() {
		counting?.Kill();
		target = SoulCache.current;
		displayed = target;
		value.Text = target.ToString();
	}

	private void CountTo(int souls) {
		target = souls;
		counting?.Kill();
		counting = CreateTween();
		counting.TweenMethod(Callable.From<float>(v => {
			displayed = v;
			value.Text = Mathf.RoundToInt(v).ToString();
		}), displayed, (float)souls, COUNT_SECONDS).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
	}

	// Hangs under the right end of the scene's title bar; the top-right of the viewport when
	// no bar was given or it has gone.
	private void Place() {
		Rect2 bar = IsInstanceValid(anchor)
			? anchor.GetGlobalRect()
			: new Rect2(Vector2.Zero, new Vector2(GetViewport().GetVisibleRect().Size.X, 0));
		plate.Position = new Vector2(bar.End.X - PLATE_SIZE.X - EDGE_MARGIN, bar.End.Y + DROP);
	}

	private void DrawPlate() {
		Vector2 size = PLATE_SIZE;
		float box = size.Y;

		// Dark plate, a touch lighter at the top.
		plate.DrawPolygon(
			new[] { Vector2.Zero, new Vector2(size.X, 0), size, new Vector2(0, size.Y) },
			new[] { FILL_TOP, FILL_TOP, FILL_BOTTOM, FILL_BOTTOM });

		// Double silver rules along the top and bottom of the number, closed at the far end.
		foreach (float y in new[] { 1.5f, size.Y - 1.5f }) {
			float inner = y < size.Y * 0.5f ? y + 3f : y - 3f;
			plate.DrawLine(new Vector2(box, y), new Vector2(size.X - 1, y), SILVER, 1f);
			plate.DrawLine(new Vector2(box + 7, inner), new Vector2(size.X - 7, inner), SILVER_DIM, 1f);
			Diamond(new Vector2(box + 7, inner), 2.2f);
			Diamond(new Vector2(size.X - 7, inner), 2.2f);
		}
		plate.DrawLine(new Vector2(size.X - 1, 1.5f), new Vector2(size.X - 1, size.Y - 1.5f), SILVER_DIM, 1f);

		// The icon's square at the left end: one silver frame.
		plate.DrawRect(new Rect2(0.5f, 0.5f, box - 1, box - 1), SILVER, false, 1f);
	}

	private void Diamond(Vector2 centre, float radius) {
		plate.DrawColoredPolygon(new[] {
			centre + new Vector2(0, -radius), centre + new Vector2(radius, 0),
			centre + new Vector2(0, radius), centre + new Vector2(-radius, 0),
		}, SILVER);
	}
}
