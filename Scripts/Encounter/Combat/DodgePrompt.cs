using Godot;
using System.Threading.Tasks;

// Asks the defending player whether to Block or Dodge an incoming enemy attack (p25).
//
// The choice has to interrupt the enemy's activation, so Ask is awaited by Enemy and the
// enemy simply stops mid-behaviour until a button comes back. Dodging is all or nothing:
// succeed and the character is not hit at all, fail and they take the full damage with no
// defence roll, which is exactly why this is the player's call and not a heuristic.
public partial class DodgePrompt : Control
{

	[Signal] public delegate void ResolvedEventHandler(bool dodge);

	[Export] public Label titleLabel;
	[Export] public Label detailLabel;
	[Export] public Button blockButton;
	[Export] public Button dodgeButton;

	public override void _Ready() {
		Visible = false;
		if (blockButton != null) blockButton.Pressed += () => EmitSignal(SignalName.Resolved, false);
		if (dodgeButton != null) dodgeButton.Pressed += () => EmitSignal(SignalName.Resolved, true);
	}

	public async Task<bool> Ask(Enemy attacker, EnemyMove move, PlayerToken target) {
		if (titleLabel != null) {
			titleLabel.Text = $"{attacker.data.enemyName} attacks {target.player.name}";
		}
		if (detailLabel != null) {
			string kind = move.isMagic ? "magical" : "physical";
			detailLabel.Text =
				$"{move.damage} {kind} damage  ·  dodge difficulty {move.dodgeDifficulty}\n" +
				$"Dodging avoids the hit entirely, or takes full damage if it fails.\n" +
				$"Endurance left: {target.endurance.free}";
		}

		// A character with no free boxes cannot pay the stamina, so there is nothing to ask.
		int cost = CombatResolver.DodgeStaminaCost(target);
		if (dodgeButton != null) {
			dodgeButton.Disabled = !target.CanSpend(cost);
			dodgeButton.Text = $"Dodge ({cost} sta)";
		}

		Visible = true;
		Variant[] result = await ToSignal(this, SignalName.Resolved);
		Visible = false;

		return result.Length > 0 && result[0].AsBool();
	}
}
