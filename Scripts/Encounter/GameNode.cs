using Godot;

public partial class GameNode : Control
{
	[Export] public bool isDisabled = false;
	[Export] public bool isEntrance = false;
	[Export] public bool isEnemySpawn = false;
	
	[Export] public EncounterManager.StatusEffect statusEffect = EncounterManager.StatusEffect.NONE;

	// Looked up by name, never by position: models standing on the node are parented under
	// it too, so "the last child" stops being the button as soon as anything moves on.
	public Control ClickTarget => GetNode<Control>("Button");

	// How a clickable node draws the eye: the marker breathes in brightness and a faint ring
	// drifts out from under it. Never in size: scaling the texture makes it shimmer.
	[Export] public float pulseRate = 0.6f;
	[Export] public float pulseDepth = 0.2f;
	[Export] public float rippleRate = 0.4f;
	[Export] public float rippleReach = 0.4f;
	[Export] public float rippleAlpha = 0.35f;

	private bool pulsing;
	private Color pulseColour;
	private float pulseTime;

	// A pillar on a boss tile (Pillars of Woe): standing, nothing may enter the node; once a boss
	// smashes it (EncounterManager.SmashPillar) it is rubble and open floor for the rest of the fight.
	public enum Pillar { NONE, STANDING, BROKEN }
	public Pillar pillar { get; private set; }
	// Seconds since the pillar fell, for the collapse; negative once it has settled.
	private float collapse = -1f;
	private const float COLLAPSE_SECONDS = 0.9f;

	public void SetPillar(Pillar state, bool animate = false) {
		pillar = state;
		collapse = animate && state == Pillar.BROKEN ? 0f : -1f;
		if (collapse >= 0f) SetProcess(true);
		QueueRedraw();
	}

	// A boss hanging over this node in mid-leap (LeapMotion): its shadow spreads on the floor and
	// a red ring breathes round where it will land. Seconds since it took off; negative for none.
	private float leapMark = -1f;
	private const float LEAP_SHADOW_SECONDS = 0.6f;
	private static readonly Color LEAP_RING = new Color(0.9f, 0.16f, 0.1f);

	public void MarkLeap(bool on) {
		leapMark = on ? 0f : -1f;
		SetProcess(pulsing || collapse >= 0f || leapMark >= 0f);
		QueueRedraw();
	}

	// Enemy phases an Abyss token has left on this node (EncounterManager.LayAbyss); 0 for none.
	public int abyss { get; private set; }

	public void SetAbyss(int phases) {
		abyss = Mathf.Max(0, phases);
		QueueRedraw();
	}

	// Drawn white and tinted by whatever lights it, so every highlight shows its own colour:
	// the scene's green placeholder circle turned gold olive and blue teal.
	private const string MARKER = "res://Resources/Images/Sprites/node_marker.png";
	private static Texture2D marker;

	public override void _Ready() {
		SetProcess(false);
		// The node is only a box for its button and the models on it. Left at Stop, its square
		// swallowed clicks meant for a boss's arc spots on the node beside it, and did nothing.
		MouseFilter = MouseFilterEnum.Ignore;
		marker ??= ResourceLoader.Exists(MARKER) ? GD.Load<Texture2D>(MARKER) : null;
		if (ClickTarget is TextureButton button && marker != null) {
			button.TextureNormal = marker;
			// Only the round marker takes clicks: the button's square corners reach over a boss's
			// arc spots on the next node, and nodes drawn later would swallow those clicks.
			clickMask ??= RoundMask();
			button.TextureClickMask = clickMask;
		}
	}

	private static Bitmap clickMask;

	private static Bitmap RoundMask() {
		Bitmap mask = new Bitmap();
		mask.CreateFromImageAlpha(marker.GetImage(), 0.5f);
		return mask;
	}

	// Movement targets and attack targets need to read differently, so the colour is the
	// caller's choice. Pulse only what can be clicked — a soul drop is a marker, not a target.
	public void Highlight(Color colour, bool pulse = false) {
		ClickTarget.Modulate = colour;
		SetPulse(pulse, colour);
	}

	// ----- Pillars -----

	private static readonly Color STONE_LIGHT = new Color(0.78f, 0.73f, 0.63f);
	private static readonly Color STONE = new Color(0.55f, 0.5f, 0.42f);
	private static readonly Color STONE_DARK = new Color(0.28f, 0.25f, 0.21f);
	private static readonly Color INK = new Color(0.08f, 0.07f, 0.06f, 0.9f);
	private static readonly Color DUST = new Color(0.66f, 0.6f, 0.5f);

	// Seen from above, lit from the upper left like the board art: a fluted drum on a square
	// plinth casting its shadow down and right. Broken, a jagged stump in a spill of rubble.
	private void DrawPillar() {
		if (pillar == Pillar.NONE) return;
		Vector2 c = Size * 0.5f;
		float r = Mathf.Min(Size.X, Size.Y) * 0.36f;
		float t = collapse >= 0f ? Mathf.Clamp(collapse / COLLAPSE_SECONDS, 0f, 1f) : 1f;

		if (pillar == Pillar.STANDING || t < 0.45f) {
			// Still standing, or toppling: it sinks and fades as the dust rises.
			float fall = pillar == Pillar.STANDING ? 0f : t / 0.45f;
			DrawStanding(c, r * (1f - 0.25f * fall), 1f - fall);
		}
		if (pillar == Pillar.BROKEN) DrawRubble(c, r, Mathf.Clamp((t - 0.25f) / 0.4f, 0f, 1f));
		if (collapse >= 0f) DrawDust(c, r, t);
	}

	private void DrawStanding(Vector2 c, float r, float alpha) {
		Color A(Color colour) => new Color(colour, colour.A * alpha);
		// Shadow, then the plinth's square, then the drum.
		DrawCircle(c + new Vector2(r * 0.28f, r * 0.32f), r * 1.12f, new Color(0, 0, 0, 0.45f * alpha));
		Rect2 plinth = new Rect2(c - Vector2.One * r * 1.08f, Vector2.One * r * 2.16f);
		DrawRect(plinth, A(STONE_DARK));
		DrawRect(plinth.Grow(-r * 0.1f), A(STONE));
		DrawRect(plinth, A(INK), false, Mathf.Max(1f, r * 0.05f));
		DrawCircle(c, r * 0.98f, A(STONE_DARK));
		// The flutes: alternating light and dark wedges round the rim, lit on the upper left.
		const int FLUTES = 16;
		for (int i = 0; i < FLUTES; i++) {
			float a0 = Mathf.Tau * i / FLUTES, a1 = Mathf.Tau * (i + 0.55f) / FLUTES;
			float light = 0.5f + 0.5f * Mathf.Cos((a0 + a1) * 0.5f - Mathf.Pi * 1.25f);
			Color flute = STONE_DARK.Lerp(STONE_LIGHT, 0.25f + 0.6f * light);
			Vector2[] wedge = { c + Vector2.FromAngle(a0) * r * 0.72f, c + Vector2.FromAngle(a0) * r * 0.95f,
				c + Vector2.FromAngle(a1) * r * 0.95f, c + Vector2.FromAngle(a1) * r * 0.72f };
			DrawColoredPolygon(wedge, A(flute));
		}
		// The top, a little lighter towards the light.
		DrawCircle(c, r * 0.72f, A(STONE));
		DrawCircle(c + new Vector2(-r * 0.12f, -r * 0.14f), r * 0.5f, A(STONE.Lerp(STONE_LIGHT, 0.45f)));
		DrawArc(c, r * 0.72f, 0f, Mathf.Tau, 40, A(INK), Mathf.Max(1f, r * 0.04f), true);
		DrawArc(c, r * 0.97f, 0f, Mathf.Tau, 48, A(INK), Mathf.Max(1f, r * 0.06f), true);
		// A crack across the top.
		Vector2[] crack = { c + new Vector2(-0.5f, -0.25f) * r, c + new Vector2(-0.15f, -0.05f) * r,
			c + new Vector2(0.05f, -0.2f) * r, c + new Vector2(0.4f, 0.15f) * r };
		DrawPolyline(crack, A(new Color(INK, 0.6f)), Mathf.Max(1f, r * 0.035f), true);
	}

	// Fixed shapes per node, so the rubble does not jump about on every redraw.
	private void DrawRubble(Vector2 c, float r, float shown) {
		if (shown <= 0f) return;
		RandomNumberGenerator rng = new RandomNumberGenerator { Seed = (ulong)GetIndex() * 7919UL + 17UL };
		DrawCircle(c + new Vector2(r * 0.15f, r * 0.2f), r * 0.75f, new Color(0, 0, 0, 0.35f * shown));
		// The stump, ragged round its rim.
		const int POINTS = 14;
		Vector2[] stump = new Vector2[POINTS];
		for (int i = 0; i < POINTS; i++) stump[i] = c + Vector2.FromAngle(Mathf.Tau * i / POINTS) * r * rng.RandfRange(0.42f, 0.62f);
		DrawColoredPolygon(stump, new Color(STONE_DARK, shown));
		DrawPolyline(Close(stump), new Color(INK, shown), Mathf.Max(1f, r * 0.05f), true);
		DrawCircle(c + new Vector2(-r * 0.08f, -r * 0.1f), r * 0.28f, new Color(STONE, shown));
		// Chunks thrown out round it.
		for (int i = 0; i < 9; i++) {
			Vector2 at = c + Vector2.FromAngle(rng.RandfRange(0f, Mathf.Tau)) * r * rng.RandfRange(0.7f, 1.25f);
			float size = r * rng.RandfRange(0.1f, 0.2f);
			float spin = rng.RandfRange(0f, Mathf.Tau);
			Vector2[] chunk = new Vector2[5];
			for (int k = 0; k < 5; k++) chunk[k] = at + Vector2.FromAngle(spin + Mathf.Tau * k / 5) * size * rng.RandfRange(0.7f, 1.2f);
			float light = rng.RandfRange(0.2f, 0.8f);
			DrawColoredPolygon(chunk, new Color(STONE_DARK.Lerp(STONE_LIGHT, light), shown));
			DrawPolyline(Close(chunk), new Color(INK, 0.8f * shown), Mathf.Max(1f, r * 0.03f), true);
		}
	}

	private static Vector2[] Close(Vector2[] points) {
		Vector2[] closed = new Vector2[points.Length + 1];
		points.CopyTo(closed, 0);
		closed[points.Length] = points[0];
		return closed;
	}

	private void DrawDust(Vector2 c, float r, float t) {
		float fade = 1f - t;
		float ease = 1f - (1f - t) * (1f - t);
		DrawCircle(c, r * (0.8f + 0.9f * ease), new Color(DUST, 0.35f * fade * fade));
		DrawArc(c, r * (1f + 1.1f * ease), 0f, Mathf.Tau, 48, new Color(DUST, 0.5f * fade), r * 0.12f * fade + 1f, true);
	}

	public void ClearHighlight() {
		ClickTarget.Modulate = new Color(0,0,0,0);
		SetPulse(false, default);
	}

	// Models sit above the button so they can be clicked; a model click that is not
	// picking a target lands here instead, so moving onto an occupied node still works.
	public void Press() {
		if (ClickTarget is BaseButton button) button.EmitSignal(BaseButton.SignalName.Pressed);
	}

	public void ToggleButton(bool isActive) {
		// The green the entrances always had.
		if (isActive) Highlight(new Color(0.45f, 0.79f, 0.29f, 0.5f), true);
		else ClearHighlight();
	}

	private void SetPulse(bool pulse, Color colour) {
		pulsing = pulse;
		pulseColour = colour;
		pulseTime = 0f;
		SetProcess(pulse || collapse >= 0f || leapMark >= 0f);
		ClickTarget.MouseDefaultCursorShape = pulse ? CursorShape.PointingHand : CursorShape.Arrow;
		QueueRedraw();
	}

	public override void _Process(double delta) {
		if (collapse >= 0f || leapMark >= 0f) {
			if (collapse >= 0f) {
				collapse += (float)delta;
				if (collapse >= COLLAPSE_SECONDS) collapse = -1f;
			}
			if (leapMark >= 0f) leapMark += (float)delta;
			QueueRedraw();
			if (!pulsing) {
				if (collapse < 0f && leapMark < 0f) SetProcess(false);
				return;
			}
		}
		pulseTime += (float)delta;
		float wave = (Mathf.Sin(pulseTime * Mathf.Tau * pulseRate) + 1f) * 0.5f;

		Color colour = pulseColour;
		colour.A = Mathf.Clamp(pulseColour.A + pulseDepth * (wave - 0.5f), 0f, 1f);
		ClickTarget.Modulate = colour;
		QueueRedraw();
	}

	// Drawn by the node itself, so it sits under the marker and the models standing here.
	public override void _Draw() {
		DrawPillar();
		DrawAbyss();
		DrawLeapMark();
		if (!pulsing) return;
		Control marker = ClickTarget;
		float progress = pulseTime * rippleRate % 1f;
		float radius = marker.Size.X * 0.5f * (1f + rippleReach * progress);
		// Eased so the ring fades in as well as out, rather than popping in at full strength.
		float fade = Mathf.Sin(progress * Mathf.Pi);
		Color colour = new Color(pulseColour.R, pulseColour.G, pulseColour.B, fade * rippleAlpha);
		float width = Mathf.Max(1f, marker.Size.X * 0.03f);
		DrawArc(marker.Position + marker.Size * 0.5f, radius, 0f, Mathf.Tau, 48, colour, width, true);
	}

	// The shadow is about the size of the boss coming down (two lattice steps at most).
	private void DrawLeapMark() {
		if (leapMark < 0f) return;
		float t = Mathf.Clamp(leapMark / LEAP_SHADOW_SECONDS, 0f, 1f);
		float grow = 1f - (1f - t) * (1f - t);
		float breath = 0.5f + 0.5f * Mathf.Sin(leapMark * Mathf.Tau * 1.2f);
		Vector2 c = Size * 0.5f;
		float r = Mathf.Min(Size.X, Size.Y) * 0.5f;
		DrawCircle(c, r * (0.55f + 0.45f * grow), new Color(0f, 0f, 0f, 0.42f * grow));
		DrawCircle(c, r * (0.35f + 0.3f * grow), new Color(0f, 0f, 0f, 0.25f * grow));
		DrawArc(c, r * (0.98f - 0.08f * breath), 0f, Mathf.Tau, 48, new Color(LEAP_RING, (0.55f + 0.35f * breath) * grow),
			Mathf.Max(1.5f, r * 0.07f), true);
	}

	// The token lies under the models (they are children, drawn after), fainter on its last phase.
	private void DrawAbyss() {
		if (abyss <= 0) return;
		Texture2D icon = ConditionArt.Icon(EncounterManager.StatusEffect.ABYSS);
		if (icon == null) return;
		float side = Mathf.Min(Size.X, Size.Y) * 0.62f;
		Color tint = new Color(1, 1, 1, abyss >= EncounterManager.ABYSS_PHASES ? 0.85f : 0.5f);
		DrawTextureRect(icon, new Rect2((Size - new Vector2(side, side)) * 0.5f, new Vector2(side, side)), false, tint);
	}

	// [Signal]
	// public delegate void SpawnPlayerEventHandler(Node entrance);

	// public override void _Ready() {
		// EmitSignal(SignalName.SpawnPlayer);
	// }

	// void OnDisable() {
	// 	this.GetComponent<Button>().onClick.RemoveAllListeners();
	//     GetNode<TextureButton>("Button")
	// }
}
