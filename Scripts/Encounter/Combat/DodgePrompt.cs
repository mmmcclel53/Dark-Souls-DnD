using Godot;
using System.Threading.Tasks;

// Asks the defending player whether to Block or Dodge an incoming enemy attack (p25).
//
// The choice has to interrupt the enemy's activation, so Ask is awaited by Enemy and the
// enemy simply stops mid-behaviour until an answer comes back. Dodging is all or nothing:
// succeed and the character is not hit at all, fail and they take the full damage with no
// defence roll, which is exactly why this is the player's call and not a heuristic.
//
// The question is put in the action bar rather than over the board: the bar swaps to the
// defender, lights their armour and shows the attack against their defence dice. This
// class only marks the two models — a red rim on the attacker's activation-bar entry and a
// ring on each token — and waits on the bar.
public partial class DodgePrompt : Control
{

	[Export] public TurnQueue turnQueue;
	[Export] public CharacterActionBar actionBar;

	[Export] public Color attackerColor = new Color(0.85f, 0.18f, 0.12f);
	[Export] public Color defenderColor = new Color(0.95f, 0.8f, 0.45f);

	private TurnQueueEntry attackerEntry;
	private TokenHighlight attackerRing;
	private TokenHighlight defenderRing;

	public async Task<bool> Ask(Enemy attacker, EnemyMove move, PlayerToken target) {
		Highlight(attacker, target);

		// With no bar to ask, the character blocks: the safe default that always rolls.
		bool dodge = actionBar != null && await actionBar.AskReaction(attacker, move, target);

		ClearHighlight();
		return dodge;
	}

	private void Highlight(Enemy attacker, PlayerToken target) {
		attackerRing = TokenHighlight.Attach(attacker, attackerColor);
		defenderRing = TokenHighlight.Attach(target, defenderColor);

		attackerEntry = turnQueue?.EntryFor(attacker);
		attackerEntry?.SetAttacking(true);
	}

	private void ClearHighlight() {
		TokenHighlight.Detach(attackerRing);
		TokenHighlight.Detach(defenderRing);
		attackerRing = defenderRing = null;

		if (attackerEntry != null && IsInstanceValid(attackerEntry)) attackerEntry.SetAttacking(false);
		attackerEntry = null;
	}
}
