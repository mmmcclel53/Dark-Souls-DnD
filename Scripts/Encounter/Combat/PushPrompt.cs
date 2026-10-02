using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

// Asks which model gets shoved off a node that just went over its three-model limit (p10).
//
// The rule hands that choice to the players whoever walked in, so this prompts for an
// arriving enemy exactly as it does for an arriving character. It only ever appears when a
// node was already full, which is rare, and it is skipped entirely when there is no real
// choice to make.
//
// It also asks where a pushed model goes (ChooseNode): the nodes "away" from the pusher light
// up on the board, the model being pushed is ringed, and a click on one of the nodes decides.
// ActionListener routes board clicks here first while that is open.
public partial class PushPrompt : Control
{
	[Export] public Color choiceColour = new Color(0.95f, 0.72f, 0.3f, 0.6f);
	[Export] public Color pushedRingColour = new Color(0.96f, 0.86f, 0.55f);

	private TaskCompletionSource<GameNode> nodeChoice;
	private readonly List<GameNode> choices = new List<GameNode>();
	public bool isChoosingNode => nodeChoice != null;

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

	public async Task<GameNode> ChooseNode(Node2D model, List<GameNode> options) {
		if (options == null || options.Count == 0) return null;
		if (options.Count == 1) return options[0];

		Dictionary<GameNode, Color> previous = new Dictionary<GameNode, Color>();
		choices.Clear();
		foreach (GameNode node in options) {
			previous[node] = node.ClickTarget.Modulate;
			node.Highlight(choiceColour, true);
			choices.Add(node);
		}
		TokenHighlight ring = model.GetChildCount() > 0 && model.GetChild(0) is Control token
			? TokenHighlight.Attach(token, pushedRingColour) : null;

		nodeChoice = new TaskCompletionSource<GameNode>();
		GameNode chosen = await nodeChoice.Task;
		nodeChoice = null;

		TokenHighlight.Detach(ring);
		foreach (KeyValuePair<GameNode, Color> pair in previous) {
			if (!IsInstanceValid(pair.Key)) continue;
			pair.Key.ClearHighlight();
			pair.Key.ClickTarget.Modulate = pair.Value;
		}
		choices.Clear();
		return chosen;
	}

	public void OnNodeClicked(GameNode node) {
		if (nodeChoice != null && choices.Contains(node)) nodeChoice.TrySetResult(node);
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
