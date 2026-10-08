using Godot;

// The endurance bar (rules p20). Ten boxes track Stamina and Health together:
// spending Stamina fills black cubes from the left, suffering damage fills red cubes
// from the right. The character dies only when damage overflows the bar, needing more red
// cubes than there are boxes left (Matt's ruling): a bar filled exactly, by stamina or by
// damage that just fits, is still standing.
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
	// Set by damage that would not fit; nothing else kills, and only Clear takes it back.
	public bool isDead { get; private set; }

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

	// Returns the damage actually absorbed, which stops at the last free box. Damage beyond
	// that is the overflow that kills.
	public int TakeDamage(int damage) {
		if (damage > free) isDead = true;
		int applied = Mathf.Clamp(damage, 0, free);
		damageTaken += applied;
		return applied;
	}

	public void GainHealth(int health) {
		damageTaken = Mathf.Max(0, damageTaken - health);
	}

	// A saved bar coming back. Nothing saves a dead character's overflow: a death ends the encounter.
	public void Restore(int stamina, int damage) {
		damageTaken = Mathf.Clamp(damage, 0, BOXES);
		staminaSpent = Mathf.Clamp(stamina, 0, BOXES - damageTaken);
		isDead = false;
	}

	public void Clear() {
		staminaSpent = 0;
		damageTaken = 0;
		isDead = false;
	}
}
