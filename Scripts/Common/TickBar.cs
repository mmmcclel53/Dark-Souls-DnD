using Godot;

// A row of discrete ticks, one per point. Used for both the character endurance bar and
// enemy health, because at these sizes (1, 5 or 10 points) a segmented row reads as a
// countable number where a continuous bar just reads as "some".
//
// The count is data — ten boxes for a character, but an enemy's Health, which is 1, 5 or 10
// depending on the card. The ticks are drawn rather than laid out as child rects: a container
// splitting, say, 42px between ten children rounds each one separately and leaves some a
// pixel wider than the rest, which is plain to see at this size. Drawn, every tick gets the
// same whole-pixel width and the spare pixels go to the ends.
//
// An endurance bar can also carry a ghost: the cost of a hovered action or the damage an
// incoming attack could do, tinted onto the free ticks and pulsing. The expected damage
// pulses hard, the worst case softly beyond it, so both read at once.
//
// Cubes arrive and leave rather than appear: a black one slides in from the left, a red one
// from the right (an enemy's lost health from the left, where it drains), and the tick it
// covers flashes. Only a change animates — the first fill of a bar, and a bar handed a
// different character, is silent.
public partial class TickBar : HBoxContainer
{

	[Export] public int separation = 2;
	[Export] public int tickHeight = 6;

	// Endurance: spent stamina fills black from the left, damage fills red from the right,
	// and what is left in the middle is the capacity to do either (p20).
	[Export] public Color spentColour = new Color(0.10f, 0.09f, 0.07f);
	[Export] public Color damageColour = new Color(0.64f, 0.17f, 0.13f);
	[Export] public Color freeColour = new Color(0.72f, 0.66f, 0.52f);

	// Enemy health drains right to left, matching the endurance bar's damage direction.
	[Export] public Color healthColour = new Color(0.72f, 0.23f, 0.18f);
	[Export] public Color lostColour = new Color(0.16f, 0.12f, 0.10f);

	[Export] public float pulseSpeed = 4f;
	[Export] public float slideSeconds = 0.22f;
	[Export] public float flashSeconds = 0.35f;

	private enum Kind { FREE, SPENT, DAMAGE, LOST }

	private Endurance endurance;
	private int ghostSpent;
	private int ghostExpected;
	private int ghostWorst;
	private float phase;
	private Color[] ticks = System.Array.Empty<Color>();

	// Per-tick animation: how far a cube still is from rest (1 = fully away), the kind of a
	// cube on its way out (FREE when none) and its colour, and the flash left on the tick.
	private Kind[] kinds = System.Array.Empty<Kind>();
	private float[] slide = System.Array.Empty<float>();
	private Kind[] leaving = System.Array.Empty<Kind>();
	private Color[] leavingColour = System.Array.Empty<Color>();
	private float[] flash = System.Array.Empty<float>();
	private bool animating;
	private bool silent = true;

	private bool hasGhost => ghostSpent > 0 || ghostExpected > 0 || ghostWorst > 0;

	public override void _Ready() {
		CustomMinimumSize = new Vector2(CustomMinimumSize.X, Mathf.Max(CustomMinimumSize.Y, tickHeight));
		Resized += QueueRedraw;
	}

	public void ShowEndurance(Endurance shown) {
		if (shown == null) return;
		if (shown != endurance) silent = true;
		endurance = shown;
		Recolour();
	}

	// Stamina about to be spent from the left; damage that might land from the right, with
	// the expected amount inside the worst case.
	public void SetGhost(int spent, int expectedDamage, int worstDamage) {
		ghostSpent = Mathf.Max(0, spent);
		ghostExpected = Mathf.Max(0, expectedDamage);
		ghostWorst = Mathf.Max(ghostExpected, worstDamage);
		Recolour();
	}

	public void ClearGhost() {
		ghostSpent = ghostExpected = ghostWorst = 0;
		if (endurance != null) Recolour();
	}

	public override void _Process(double delta) {
		if (animating) Advance((float)delta);
		if (endurance == null || !hasGhost) return;
		phase += (float)delta * pulseSpeed;
		Recolour();
	}

	private void Recolour() {
		if (endurance == null) return;
		int spent = endurance.staminaSpent;
		int damage = endurance.damageTaken;
		float pulse = 0.5f + 0.5f * Mathf.Sin(phase);

		Build(Endurance.BOXES, i => {
			if (i < spent) return spentColour;
			if (i >= Endurance.BOXES - damage) return damageColour;

			if (i < spent + ghostSpent) return freeColour.Lerp(spentColour, 0.45f + 0.35f * pulse);
			if (i >= Endurance.BOXES - damage - ghostExpected) return freeColour.Lerp(damageColour, 0.5f + 0.4f * pulse);
			if (i >= Endurance.BOXES - damage - ghostWorst) return freeColour.Lerp(damageColour, 0.18f + 0.17f * pulse);
			return freeColour;
		}, i => i < spent ? Kind.SPENT : i >= Endurance.BOXES - damage ? Kind.DAMAGE : Kind.FREE);
	}

	public void ShowHealth(int current, int max) {
		if (max <= 0) return;
		if (endurance != null) silent = true;
		endurance = null;
		Build(max, i => i < max - current ? lostColour : healthColour, i => i < max - current ? Kind.LOST : Kind.FREE);
	}

	private void Build(int count, System.Func<int, Color> colourOf, System.Func<int, Kind> kindOf) {
		if (ticks.Length != count) {
			ticks = new Color[count];
			kinds = new Kind[count];
			slide = new float[count];
			leaving = new Kind[count];
			leavingColour = new Color[count];
			flash = new float[count];
			silent = true;
		}

		for (int i = 0; i < count; i++) {
			Kind kind = kindOf(i);
			if (kind != kinds[i] && !silent) {
				if (kind != Kind.FREE) {
					slide[i] = 1f;
					leaving[i] = Kind.FREE;
					flash[i] = 1f;
				} else {
					leaving[i] = kinds[i];
					leavingColour[i] = ticks[i];
					slide[i] = 0f;
				}
				animating = true;
			}
			kinds[i] = kind;
			ticks[i] = colourOf(i);
		}
		silent = false;
		QueueRedraw();
	}

	private void Advance(float dt) {
		bool any = false;
		for (int i = 0; i < ticks.Length; i++) {
			if (leaving[i] != Kind.FREE) {
				slide[i] += dt / slideSeconds;
				if (slide[i] >= 1f) {
					slide[i] = 0f;
					leaving[i] = Kind.FREE;
				} else {
					any = true;
				}
			} else if (slide[i] > 0f) {
				slide[i] = Mathf.Max(0f, slide[i] - dt / slideSeconds);
				if (slide[i] > 0f) any = true;
			}
			if (flash[i] > 0f) {
				flash[i] = Mathf.Max(0f, flash[i] - dt / flashSeconds);
				if (flash[i] > 0f) any = true;
			}
		}
		animating = any;
		QueueRedraw();
	}

	public override void _Draw() {
		int count = ticks.Length;
		if (count == 0) return;

		int gaps = separation * (count - 1);
		int width = Mathf.Max(1, Mathf.FloorToInt((Size.X - gaps) / count));
		int used = width * count + gaps;
		float left = Mathf.Floor((Size.X - used) * 0.5f);
		float top = Mathf.Floor((Size.Y - tickHeight) * 0.5f);
		float travel = width + separation;
		Color under = endurance != null ? freeColour : healthColour;

		for (int i = 0; i < count; i++) {
			Rect2 rect = new Rect2(left + i * (width + separation), top, width, tickHeight);

			if (leaving[i] != Kind.FREE) {
				DrawRect(rect, ticks[i]);
				float away = Mathf.Pow(slide[i], 1.5f);
				DrawRect(Shifted(rect, Direction(leaving[i]) * travel * away), new Color(leavingColour[i], 1f - away));
			} else if (slide[i] > 0f) {
				DrawRect(rect, under);
				float away = Mathf.Pow(slide[i], 1.5f);
				DrawRect(Shifted(rect, Direction(kinds[i]) * travel * away), new Color(ticks[i], 1f - away));
			} else {
				DrawRect(rect, ticks[i]);
			}

			if (flash[i] > 0f) DrawRect(rect, new Color(1f, 1f, 1f, flash[i] * flash[i] * 0.55f));
		}
	}

	// Damage comes from the right; spent stamina and lost health from the left.
	private static float Direction(Kind kind) => kind == Kind.DAMAGE ? 1f : -1f;

	private static Rect2 Shifted(Rect2 rect, float dx) => new Rect2(rect.Position + new Vector2(dx, 0f), rect.Size);
}
