using Godot;

// A row of discrete ticks, one per point. Used for both the character endurance bar and
// enemy health, because at these sizes (1, 5 or 10 points) a segmented row reads as a
// countable number where a continuous bar just reads as "some".
//
// Builds its ticks in code rather than in the scene: the count is data — ten boxes for a
// character, but an enemy's Health, which is 1, 5 or 10 depending on the card.
//
// An endurance bar can also carry a ghost: the cost of a hovered action or the damage an
// incoming attack could do, tinted onto the free ticks and pulsing. The expected damage
// pulses hard, the worst case softly beyond it, so both read at once.
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

	private Endurance endurance;
	private int ghostSpent;
	private int ghostExpected;
	private int ghostWorst;
	private float phase;

	private bool hasGhost => ghostSpent > 0 || ghostExpected > 0 || ghostWorst > 0;

	public override void _Ready() {
		AddThemeConstantOverride("separation", separation);
	}

	public void ShowEndurance(Endurance shown) {
		if (shown == null) return;
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
		});
	}

	public void ShowHealth(int current, int max) {
		if (max <= 0) return;
		endurance = null;
		Build(max, i => i < max - current ? lostColour : healthColour);
	}

	private void Build(int count, System.Func<int, Color> colourOf) {
		// Rebuild only when the number of ticks changes; usually just the colours move.
		// RemoveChild before QueueFree so GetChildCount is accurate straight away.
		if (GetChildCount() != count) {
			foreach (Node child in GetChildren()) {
				RemoveChild(child);
				child.QueueFree();
			}
			for (int i = 0; i < count; i++) {
				ColorRect tick = new ColorRect();
				tick.CustomMinimumSize = new Vector2(0, tickHeight);
				tick.SizeFlagsHorizontal = SizeFlags.ExpandFill;
				tick.MouseFilter = MouseFilterEnum.Ignore;
				AddChild(tick);
			}
		}

		for (int i = 0; i < count; i++) {
			if (GetChild(i) is ColorRect tick) tick.Color = colourOf(i);
		}
	}
}
