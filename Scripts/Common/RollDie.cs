using Godot;
using System.Collections.Generic;

// One die in the roll reveal: a six-sided cube wearing the printed die's six faces, thrown
// onto the tray. It flies in from the side tumbling about two axes, bounces twice, and
// comes to rest with the face it rolled towards the viewer. The throw is the same every
// time — the landing face is decided before it leaves the hand and put on the front of
// the cube, and the tumble winds down to the untilted pose — which is what gives a roll
// its weight without any physics, the way Baldur's Gate 3 casts its d20.
//
// The cube is drawn, not modelled: its corners are turned by a Basis, given a little
// perspective, and each side facing the viewer is drawn as a textured quad shaded by its
// angle to the light, with its edges inked. This control is the die's slot in the row and
// stays put; the cube and its shadow are drawn relative to it, off the slot while flying.
public partial class RollDie : Control
{

	public DiceUtility.DICE_TYPE type = DiceUtility.DICE_TYPE.BLACK;
	public int value;
	public int[] faces = { 0 };

	public float throwSeconds = 0.95f;
	public float edge = 0.76f;         // cube edge as a fraction of the slot
	public float perspective = 0.09f;  // how much nearer corners grow

	public bool settled { get; private set; }

	// Offered by the roll reveal while a Luck token can reroll a die: the die lights under
	// the cursor and a click picks it.
	public event System.Action Picked;
	private bool pickable;
	private bool hovered;

	private static readonly Vector3 LIGHT = new Vector3(-0.45f, -0.6f, 0.66f).Normalized();

	// Sides in the cube's own frame: +Z faces the viewer, Y runs down the screen. Each
	// side's corners run top-left, top-right, bottom-right, bottom-left as seen from
	// outside, to match the face texture's corners.
	private static readonly Vector3[] NORMALS = {
		new Vector3(0, 0, 1), new Vector3(0, 0, -1), new Vector3(1, 0, 0),
		new Vector3(-1, 0, 0), new Vector3(0, -1, 0), new Vector3(0, 1, 0),
	};
	private static readonly Vector3[][] CORNERS = {
		new[] { new Vector3(-1, -1, 1), new Vector3(1, -1, 1), new Vector3(1, 1, 1), new Vector3(-1, 1, 1) },
		new[] { new Vector3(1, -1, -1), new Vector3(-1, -1, -1), new Vector3(-1, 1, -1), new Vector3(1, 1, -1) },
		new[] { new Vector3(1, -1, 1), new Vector3(1, -1, -1), new Vector3(1, 1, -1), new Vector3(1, 1, 1) },
		new[] { new Vector3(-1, -1, -1), new Vector3(-1, -1, 1), new Vector3(-1, 1, 1), new Vector3(-1, 1, -1) },
		new[] { new Vector3(-1, -1, -1), new Vector3(1, -1, -1), new Vector3(1, -1, 1), new Vector3(-1, -1, 1) },
		new[] { new Vector3(-1, 1, 1), new Vector3(1, 1, 1), new Vector3(1, 1, -1), new Vector3(-1, 1, -1) },
	};

	private Texture2D sheet;
	private Vector2 sheetSize;
	private readonly int[] sides = new int[6];
	private float side;
	private float half;

	private bool thrown;
	private float clock;
	private Vector2 start;
	private float spinX;
	private float spinY;
	private float tilt;
	private Vector2 ground;
	private float height;
	private float pop = 1f;
	private Basis rotation = Basis.Identity;

	public static RollDie Create(RollReveal.Face face, float side) {
		RollDie die = new RollDie {
			type = face.type,
			value = face.value,
			faces = face.faces != null && face.faces.Length > 0 ? face.faces : new[] { face.value },
			side = side,
			CustomMinimumSize = new Vector2(side, side),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		die.Build();
		return die;
	}

	private void Build() {
		half = side * edge * 0.5f;
		sheet = DiceArt.Sheet(type);
		if (sheet != null) sheetSize = sheet.GetSize();

		// The rolled face goes on the front; the die's other five faces take the other sides.
		int front = DiceArt.CellFor(type, value);
		List<int> rest = new List<int>();
		for (int i = 0; i < DiceArt.CELLS; i++) {
			if (i != front) rest.Add(i);
		}
		for (int i = rest.Count - 1; i > 0; i--) {
			int j = GD.RandRange(0, i);
			(rest[i], rest[j]) = (rest[j], rest[i]);
		}
		sides[0] = front < 0 ? rest[0] : front;
		for (int i = 1; i < 6; i++) sides[i] = rest[Mathf.Min(i - (front < 0 ? 0 : 1), rest.Count - 1)];
		if (front < 0) sheet = null;    // no printed face for this value: a plain cube with the number

		// The same throw for every die, with just enough scatter that a handful reads as one.
		start = new Vector2(-2.4f - GD.Randf() * 0.4f, -1.5f - GD.Randf() * 0.4f) * side;
		spinX = Mathf.Tau * (1.5f + GD.Randf() * 0.8f) * (GD.Randf() < 0.5f ? 1f : -1f);
		spinY = Mathf.Tau * (1.0f + GD.Randf() * 0.8f) * (GD.Randf() < 0.5f ? 1f : -1f);
		tilt = (GD.Randf() - 0.5f) * 0.12f;
	}

	public override void _Ready() {
		MouseEntered += () => { hovered = true; QueueRedraw(); };
		MouseExited += () => { hovered = false; QueueRedraw(); };
	}

	public void SetPickable(bool on) {
		pickable = on;
		MouseFilter = on ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
		MouseDefaultCursorShape = on ? CursorShape.PointingHand : CursorShape.Arrow;
		if (!on) hovered = false;
		QueueRedraw();
	}

	public override void _GuiInput(InputEvent @event) {
		if (!pickable || @event is not InputEventMouseButton click || !click.Pressed || click.ButtonIndex != MouseButton.Left) return;
		AcceptEvent();
		Picked?.Invoke();
	}

	// Luck: the same die thrown again, landing on its new face.
	public void Reroll(int newValue) {
		value = newValue;
		settled = false;
		pop = 1f;
		Build();
		Throw(0f);
	}

	// Starts the throw after `delay` seconds, so a handful lands one after another.
	public void Throw(float delay) {
		clock = -delay;
		thrown = true;
	}

	public override void _Process(double delta) {
		if (!thrown || settled) return;
		clock += (float)delta;
		if (clock < 0f) return;

		float t = Mathf.Min(1f, clock / throwSeconds);
		Animate(t);
		if (t >= 1f) Land();
		QueueRedraw();
	}

	private void Animate(float t) {
		float ease = 1f - Mathf.Pow(1f - t, 2.4f);
		height = Height(t);
		ground = start * (1f - ease);

		// The tumble winds down to nothing, so whatever the spins were the front face ends
		// up facing the viewer, with just a little tilt for character.
		float spin = Mathf.Pow(1f - t, 2.2f);
		rotation = new Basis(Vector3.Right, spinX * spin) * new Basis(Vector3.Up, spinY * spin) * new Basis(Vector3.Back, tilt);
	}

	// The arc of the throw, then two bounces, each lower.
	private static float Height(float t) {
		if (t < 0.5f) return Mathf.Sin(t / 0.5f * Mathf.Pi);
		if (t < 0.78f) return 0.35f * Mathf.Sin((t - 0.5f) / 0.28f * Mathf.Pi);
		if (t < 0.94f) return 0.12f * Mathf.Sin((t - 0.78f) / 0.16f * Mathf.Pi);
		return 0f;
	}

	private void Land() {
		settled = true;
		height = 0f;
		ground = Vector2.Zero;
		rotation = new Basis(Vector3.Back, tilt);

		Tween tween = CreateTween();
		tween.TweenMethod(Callable.From<float>(v => { pop = v; QueueRedraw(); }), 1.25f, 1f, 0.22)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}

	public override void _Draw() {
		if (!thrown || clock < 0f) return;

		Vector2 slotCentre = new Vector2(side, side) * 0.5f;
		DrawShadow(ground + slotCentre);
		if (pickable) {
			Color glow = new Color(0.95f, 0.8f, 0.45f, hovered ? 0.9f : 0.3f);
			DrawArc(slotCentre, half * 1.75f, 0f, Mathf.Tau, 48, glow, hovered ? 3f : 2f, true);
		}

		Vector2 centre = ground + slotCentre + Vector2.Up * height * side * 0.35f;
		float scale = half * (1f + 0.3f * height) * pop;
		for (int f = 0; f < 6; f++) {
			Vector3 normal = rotation * NORMALS[f];
			if (normal.Z <= 0.02f) continue;
			DrawSide(f, centre, scale, normal);
		}
	}

	private void DrawSide(int f, Vector2 centre, float scale, Vector3 normal) {
		Vector2[] points = new Vector2[4];
		for (int k = 0; k < 4; k++) {
			Vector3 p = rotation * CORNERS[f][k];
			points[k] = centre + new Vector2(p.X, p.Y) * scale * (1f + p.Z * perspective);
		}
		// A side turned nearly edge-on projects to a sliver that cannot be triangulated; under
		// 2% of a full face it is barely visible anyway.
		if (Mathf.Abs(QuadArea(points)) < 0.02f * 4f * scale * scale) return;

		float shade = 0.55f + 0.45f * Mathf.Clamp(normal.Dot(LIGHT), 0f, 1f);
		Color light = new Color(shade, shade, shade);
		if (sheet != null) {
			DrawPolygon(points, new[] { light, light, light, light }, DiceArt.CellUvs(sides[f], sheetSize), sheet);
		} else {
			(Color fill, Color _, Color text) = Colours(type);
			DrawPolygon(points, new[] { fill * light, fill * light, fill * light, fill * light });
			if (settled && f == 0) DrawNumber(centre, text);
		}

		// Inked edges; the front face wears gold once the die has come to rest.
		bool front = settled && f == 0;
		Color ink = front ? new Color(0.95f, 0.8f, 0.45f) : new Color(0.05f, 0.04f, 0.03f, 0.75f);
		DrawPolyline(new[] { points[0], points[1], points[2], points[3], points[0] }, ink, front ? 2.5f : 1.2f, true);
	}

	private static float QuadArea(Vector2[] q) {
		float area = 0f;
		for (int k = 0; k < q.Length; k++) area += q[k].Cross(q[(k + 1) % q.Length]);
		return area * 0.5f;
	}

	private void DrawNumber(Vector2 centre, Color colour) {
		Font font = GetThemeDefaultFont();
		int size = Mathf.RoundToInt(half * 1.1f);
		float baseline = centre.Y + (font.GetAscent(size) - font.GetDescent(size)) * 0.5f;
		DrawString(font, new Vector2(centre.X - half, baseline), value.ToString(), HorizontalAlignment.Center, half * 2f, size, colour);
	}

	// The shadow on the tray under wherever the die is: it spreads and fades as the die
	// rises and gathers as it comes down.
	private void DrawShadow(Vector2 under) {
		float spread = 1f + 0.35f * height;
		DrawSetTransform(under + Vector2.Down * side * 0.08f, 0f, new Vector2(1f, 0.45f));
		DrawCircle(Vector2.Zero, half * 1.25f * spread, new Color(0f, 0f, 0f, 0.4f * (1f - 0.6f * height)));
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}

	private static (Color fill, Color edge, Color text) Colours(DiceUtility.DICE_TYPE type) => type switch {
		DiceUtility.DICE_TYPE.BLUE => (new Color(0.20f, 0.40f, 0.78f), new Color(0.55f, 0.70f, 0.95f), Colors.White),
		DiceUtility.DICE_TYPE.ORANGE => (new Color(0.90f, 0.52f, 0.13f), new Color(1f, 0.78f, 0.45f), new Color(0.12f, 0.08f, 0.04f)),
		DiceUtility.DICE_TYPE.DODGE => (new Color(0.30f, 0.62f, 0.30f), new Color(0.60f, 0.85f, 0.55f), Colors.White),
		_ => (new Color(0.12f, 0.11f, 0.10f), new Color(0.55f, 0.50f, 0.42f), Colors.White),
	};
}
