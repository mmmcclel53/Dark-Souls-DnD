using Godot;

// The endurance bar (rules p20). Ten boxes track Stamina and Health together:
// spending Stamina fills black cubes from the left, suffering damage fills red cubes
// from the right, and the character dies when the two meet.
//
// There is no separate stamina pool — what a character can still spend IS the uncovered
// part of the bar, so a wound and a sprint compete for the same boxes. Gaining Stamina or
// Health removes cubes rather than adding a resource, and does nothing when there are none
// to remove. Encounter-scoped: the bar clears on victory (p19).
public class Endurance {

	public const int BOXES = 10;

	public int staminaSpent { get; private set; }
	public int damageTaken { get; private set; }

	public int free => BOXES - staminaSpent - damageTaken;
	public bool isDead => free <= 0;

	// Boxes not covered by a red cube — the conventional "health remaining" reading.
	public int healthRemaining => BOXES - damageTaken;

	public bool CanSpend(int stamina) => stamina >= 0 && stamina <= free;

	public bool SpendStamina(int stamina) {
		if (!CanSpend(stamina)) return false;
		staminaSpent += stamina;
		return true;
	}

	public void GainStamina(int stamina) {
		staminaSpent = Mathf.Max(0, staminaSpent - stamina);
	}

	// Returns the damage actually absorbed, which stops at the last free box.
	public int TakeDamage(int damage) {
		int applied = Mathf.Clamp(damage, 0, free);
		damageTaken += applied;
		return applied;
	}

	public void GainHealth(int health) {
		damageTaken = Mathf.Max(0, damageTaken - health);
	}

	public void Clear() {
		staminaSpent = 0;
		damageTaken = 0;
	}
}
