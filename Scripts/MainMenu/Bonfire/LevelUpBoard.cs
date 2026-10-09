using Godot;

// The character's printed board with its Level Up cubes (p15), the board game's own way of
// levelling: a white cube on every tier square the character holds, a gold one on each square
// queued to buy, and the next square of any row that can be bought now lit and breathing.
// Clicking a lit square queues it; clicking a queued cube takes it back, with any queued above
// it in the row, since a row has no gaps. The
// squares are found from Character.tierSquares (Tools/LevelSquares/measure.py); the board is
// drawn aspect-fit, so they are placed on whatever rect the art lands in.
public partial class LevelUpBoard : Control {
	[Signal] public delegate void SquareClickedEventHandler(int stat, int tier);

	private static readonly Color CUBE = new Color(0.94f, 0.93f, 0.89f);
	private static readonly Color QUEUED = new Color(1f, 0.85f, 0.45f);
	private static readonly Color LIT = new Color(1f, 0.8f, 0.35f);

	public Player player;
	// Per stat, in Player.STATS order: the highest tier the character's own cubes reach, the
	// highest with the queue on top (-1 for an empty row), and whether the row's next square
	// can be bought now.
	public int[] placed = new int[4];
	public int[] queued = new int[4];
	public bool[] canRaise = new bool[4];

	private float time;
	private Vector2I hovered = new Vector2I(-1, -1);

	public override void _Ready() {
		MouseFilter = MouseFilterEnum.Stop;
		MouseExited += () => SetHovered(new Vector2I(-1, -1));
	}

	public override void _Process(double delta) {
		time += (float)delta;
		QueueRedraw();
	}

	public override void _Draw() {
		Texture2D art = player?.character?.image;
		if (art == null) return;
		Rect2 rect = ArtRect(art);
		DrawTextureRect(art, rect, false);

		float side = player.character.tierSquareSize * rect.Size.X;
		for (int row = 0; row < 4; row++) {
			for (int tier = 0; tier <= Player.TOP_TIER; tier++) {
				Vector2 centre = SquareCentre(rect, row, tier);
				if (tier <= placed[row]) DrawCube(centre, side, CUBE, 1f);
				else if (tier <= queued[row]) DrawQueued(centre, side);
				else if (tier == queued[row] + 1 && canRaise[row]) DrawLit(centre, side, hovered == new Vector2I(row, tier));
			}
		}
	}

	public override void _GuiInput(InputEvent @event) {
		if (@event is InputEventMouseMotion motion) {
			SetHovered(Clickable(SquareAt(motion.Position)));
		} else if (@event is InputEventMouseButton click && click.ButtonIndex == MouseButton.Left && click.Pressed) {
			Vector2I square = Clickable(SquareAt(click.Position));
			if (square.X < 0) return;
			AcceptEvent();
			EmitSignal(SignalName.SquareClicked, square.X, square.Y);
		}
	}

	private void SetHovered(Vector2I square) {
		hovered = square;
		MouseDefaultCursorShape = square.X >= 0 ? CursorShape.PointingHand : CursorShape.Arrow;
	}

	// A square that does something: the lit next square of a row, or any of its queued cubes.
	private Vector2I Clickable(Vector2I square) {
		if (square.X < 0) return square;
		int row = square.X, tier = square.Y;
		bool next = tier == queued[row] + 1 && canRaise[row];
		bool takeBack = tier <= queued[row] && tier > placed[row];
		return next || takeBack ? square : new Vector2I(-1, -1);
	}

	private Vector2I SquareAt(Vector2 point) {
		Texture2D art = player?.character?.image;
		if (art == null) return new Vector2I(-1, -1);
		Rect2 rect = ArtRect(art);
		float half = player.character.tierSquareSize * rect.Size.X * 0.6f;
		for (int row = 0; row < 4; row++) {
			for (int tier = 0; tier <= Player.TOP_TIER; tier++) {
				Vector2 offset = point - SquareCentre(rect, row, tier);
				if (Mathf.Abs(offset.X) <= half && Mathf.Abs(offset.Y) <= half) return new Vector2I(row, tier);
			}
		}
		return new Vector2I(-1, -1);
	}

	private Rect2 ArtRect(Texture2D art) {
		Vector2 size = art.GetSize();
		float scale = Mathf.Min(Size.X / size.X, Size.Y / size.Y);
		Vector2 drawn = size * scale;
		return new Rect2((Size - drawn) / 2f, drawn);
	}

	private Vector2 SquareCentre(Rect2 rect, int row, int tier) {
		Rect2 squares = player.character.tierSquares;
		var fraction = new Vector2(squares.Position.X + squares.Size.X * tier / 3f, squares.Position.Y + squares.Size.Y * row / 3f);
		return rect.Position + fraction * rect.Size;
	}

	// A white Level Up cube seen from above: a soft shadow, the face, a lit top-left edge and a
	// shaded bottom-right one.
	private void DrawCube(Vector2 centre, float side, Color colour, float alpha) {
		float s = side * 0.8f;
		var face = new Rect2(centre - new Vector2(s, s) / 2f, new Vector2(s, s));
		DrawRect(new Rect2(face.Position + new Vector2(s, s) * 0.08f, face.Size), new Color(0, 0, 0, 0.45f * alpha));
		DrawRect(face, colour with { A = alpha });
		float edge = Mathf.Max(1f, s * 0.12f);
		DrawRect(new Rect2(face.Position, new Vector2(s, edge)), colour.Lightened(0.5f) with { A = alpha });
		DrawRect(new Rect2(face.Position, new Vector2(edge, s)), colour.Lightened(0.35f) with { A = alpha });
		DrawRect(new Rect2(face.Position + new Vector2(0, s - edge), new Vector2(s, edge)), colour.Darkened(0.3f) with { A = alpha });
		DrawRect(new Rect2(face.Position + new Vector2(s - edge, 0), new Vector2(edge, s)), colour.Darkened(0.2f) with { A = alpha });
	}

	private void DrawQueued(Vector2 centre, float side) {
		float pulse = 0.5f + 0.5f * Mathf.Sin(time * 4f);
		DrawGlow(centre, side, QUEUED, 0.35f + 0.25f * pulse);
		DrawCube(centre, side, QUEUED, 1f);
	}

	private void DrawLit(Vector2 centre, float side, bool hover) {
		float pulse = 0.5f + 0.5f * Mathf.Sin(time * 3f);
		DrawGlow(centre, side, LIT, hover ? 0.9f : 0.35f + 0.35f * pulse);
		if (hover) DrawCube(centre, side, QUEUED, 0.55f);
	}

	private void DrawGlow(Vector2 centre, float side, Color colour, float alpha) {
		float s = side * 1.08f;
		var rect = new Rect2(centre - new Vector2(s, s) / 2f, new Vector2(s, s));
		DrawRect(rect.Grow(side * 0.12f), colour with { A = alpha * 0.25f });
		DrawRect(rect, colour with { A = alpha }, false, Mathf.Max(1.5f, side * 0.07f));
	}
}
