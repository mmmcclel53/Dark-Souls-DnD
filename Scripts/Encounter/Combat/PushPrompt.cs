using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

// Asks which model gets shoved off a node that just went over its three-model limit (p10).
//
// The rule hands that choice to the players whoever walked in, so this prompts for an
// arriving enemy exactly as it does for an arriving character. It only ever appears when a
// node was already full, which is rare, and it is skipped entirely when there is no real
// choice to make.
public partial class PushPrompt : Control
{

	[Signal] public delegate void ChosenEventHandler(int index);

	[Export] public Label titleLabel;
	[Export] public Container optionRows;

	public override void _Ready() {
		Visible = false;
	}

	public async Task<Node2D> Ask(List<Node2D> candidates, string arrivalName) {
		if (candidates == null || candidates.Count == 0) return null;
		if (candidates.Count == 1) return candidates[0];

		if (titleLabel != null) {
			titleLabel.Text = $"{arrivalName} moves in — the node is full. Push which model off?";
		}
		BuildOptions(candidates);

		Visible = true;
		Variant[] result = await ToSignal(this, SignalName.Chosen);
		Visible = false;

		int index = result.Length > 0 ? result[0].AsInt32() : 0;
		return index >= 0 && index < candidates.Count ? candidates[index] : candidates[0];
	}

	private void BuildOptions(List<Node2D> candidates) {
		if (optionRows == null) return;
		foreach (Node child in optionRows.GetChildren()) child.QueueFree();

		for (int i = 0; i < candidates.Count; i++) {
			Button button = new Button();
			button.Text = NameOf(candidates[i]);
			button.AddThemeFontSizeOverride("font_size", 13);

			// Capture before the lambda, or every button reports the last index.
			int captured = i;
			button.Pressed += () => EmitSignal(SignalName.Chosen, captured);
			optionRows.AddChild(button);
		}
	}

	private static string NameOf(Node2D model) {
		Enemy enemy = EncounterManager.GetEnemy(model);
		if (enemy != null) return $"{enemy.data.enemyName}  ({enemy.currentHealth} hp)";

		PlayerToken token = EncounterManager.GetPlayerToken(model);
		if (token != null) return $"{token.player.name}  ({token.endurance.free} endurance)";

		return model.Name;
	}
}
