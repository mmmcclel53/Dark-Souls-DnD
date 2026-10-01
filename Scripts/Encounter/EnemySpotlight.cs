using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

// Shows what an enemy is about to do before it does it: the enemy is ringed, the nodes it
// will move through light up (the last one strongest), and whoever it is going after is
// ringed too. It holds for a moment and then gets out of the way; a click or Enter skips
// ahead. Movement is previewed one Move icon at a time, as each comes up, so the preview is
// always the move that is actually about to happen.
//
// Nodes may already be lit (the soul pile), so each node's highlight is put back afterwards
// rather than cleared.
public partial class EnemySpotlight : Node
{
	public float moveSeconds = 1.2f;
	public float attackSeconds = 0.8f;

	public Color enemyColour = new Color(0.96f, 0.86f, 0.55f);
	public Color targetColour = new Color(0.85f, 0.18f, 0.12f);
	public Color pathColour = new Color(0.95f, 0.55f, 0.2f, 0.4f);
	public Color destinationColour = new Color(0.95f, 0.55f, 0.2f, 0.85f);
	public Color attackNodeColour = new Color(0.85f, 0.18f, 0.12f, 0.6f);

	// The light runs along the path from the enemy towards where it is going, over and
	// over, so the direction reads without reading the icons.
	public float pulseNodesPerSecond = 4f;
	public float pulseStrength = 0.45f;

	private bool skip;
	private bool active;

	public Task PreviewMove(Enemy enemy, List<GameNode> route, PlayerToken target, bool towardsNearest) =>
		Preview(enemy, route, destinationColour, target, towardsNearest, moveSeconds);

	public Task PreviewAttack(Enemy enemy, GameNode targetNode, PlayerToken target, bool towardsNearest) =>
		Preview(enemy, new List<GameNode> { targetNode }, attackNodeColour, target, towardsNearest, attackSeconds);

	private async Task Preview(Enemy enemy, List<GameNode> nodes, Color lastColour, PlayerToken target,
			bool towardsNearest, float seconds) {
		if (enemy == null || !IsInstanceValid(enemy)) return;

		var rings = new List<TokenHighlight> { TokenHighlight.Attach(enemy, enemyColour) };
		if (target != null && IsInstanceValid(target)) rings.Add(TokenHighlight.Attach(target, targetColour));
		if (towardsNearest) NearestMarker.Pin(target);

		var previous = new Dictionary<GameNode, Color>();
		var lit = new List<(GameNode node, Color colour)>();
		for (int i = 0; i < nodes.Count; i++) {
			GameNode node = nodes[i];
			if (node == null || previous.ContainsKey(node)) continue;
			previous[node] = node.ClickTarget.Modulate;
			Color colour = i == nodes.Count - 1 ? lastColour : pathColour;
			node.Highlight(colour);
			lit.Add((node, colour));
		}

		await Hold(seconds, elapsed => Pulse(lit, elapsed));

		foreach (var pair in previous) {
			if (IsInstanceValid(pair.Key)) pair.Key.ClickTarget.Modulate = pair.Value;
		}
		foreach (TokenHighlight ring in rings) TokenHighlight.Detach(ring);
		NearestMarker.Pin(null);
	}

	// A bump of light travelling down the path, node by node, with a pause before it
	// starts again. A single node just breathes.
	private void Pulse(List<(GameNode node, Color colour)> lit, float elapsed) {
		float head = (elapsed * pulseNodesPerSecond) % (lit.Count + 1.5f);
		for (int i = 0; i < lit.Count; i++) {
			(GameNode node, Color colour) = lit[i];
			if (!IsInstanceValid(node)) continue;
			float d = i - head;
			float bump = Mathf.Exp(-d * d * 1.6f);
			node.ClickTarget.Modulate = new Color(colour, Mathf.Min(1f, colour.A + pulseStrength * bump));
		}
	}

	private async Task Hold(float seconds, System.Action<float> onFrame = null) {
		skip = false;
		active = true;
		float elapsed = 0f;
		while (elapsed < seconds && !skip) {
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			elapsed += (float)GetProcessDeltaTime();
			onFrame?.Invoke(elapsed);
		}
		active = false;
	}

	public override void _Input(InputEvent @event) {
		if (!active) return;
		bool click = @event is InputEventMouseButton button && button.Pressed && button.ButtonIndex == MouseButton.Left;
		if (!click && !@event.IsActionPressed("ui_accept")) return;
		skip = true;
		GetViewport().SetInputAsHandled();
	}
}
