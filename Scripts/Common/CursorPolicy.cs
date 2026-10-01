using Godot;

// Every button shows the pointing hand, without each scene having to say so. Any button
// that enters the tree with the default arrow is given the hand, and a disabled one shows
// the arrow again while it is hovered (Godot keeps a disabled button's cursor as it is).
// The board's own clickables — the grid nodes, and the enemy and character tokens — set
// their cursor by state and are left alone.
public partial class CursorPolicy : Node
{
	public override void _Ready() {
		GetTree().NodeAdded += OnNodeAdded;
		Sweep(GetTree().Root);
	}

	private void Sweep(Node node) {
		OnNodeAdded(node);
		foreach (Node child in node.GetChildren()) Sweep(child);
	}

	private static void OnNodeAdded(Node node) {
		if (node is not BaseButton button || !Managed(button)) return;
		if (button.MouseDefaultCursorShape == Control.CursorShape.Arrow) {
			button.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		}
	}

	private static bool Managed(BaseButton button) =>
		button is not Enemy && button is not PlayerToken && button.GetParent() is not GameNode;

	public override void _Process(double delta) {
		if (GetViewport().GuiGetHoveredControl() is not BaseButton button || !Managed(button)) return;
		Control.CursorShape wanted = button.Disabled ? Control.CursorShape.Arrow : Control.CursorShape.PointingHand;
		if (button.MouseDefaultCursorShape != wanted) button.MouseDefaultCursorShape = wanted;
	}
}
