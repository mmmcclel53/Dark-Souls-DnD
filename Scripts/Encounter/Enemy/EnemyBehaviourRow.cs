using Godot;

// One behaviour icon from the data card, written out. EnemyInfoPanel fills it.
public partial class EnemyBehaviourRow : HBoxContainer
{

	[Export] public Label indexLabel;
	[Export] public Label textLabel;
	[Export] public Label traitsLabel;
	[Export] public Label statsLabel;

	public void Fill(int order, string text, string traits, string stats) {
		if (indexLabel != null) indexLabel.Text = order.ToString();
		if (textLabel != null) textLabel.Text = text;

		if (traitsLabel != null) {
			traitsLabel.Text = traits;
			traitsLabel.Visible = !string.IsNullOrEmpty(traits);
		}
		if (statsLabel != null) {
			statsLabel.Text = stats;
			statsLabel.Visible = !string.IsNullOrEmpty(stats);
		}
	}
}
