using Godot;

public partial class GameNode : Control
{
	[Export] public bool isDisabled = false;
	[Export] public bool isEntrance = false;
	[Export] public bool isEnemySpawn = false;
	
	[Export] public EncounterManager.StatusEffect statusEffect = EncounterManager.StatusEffect.NONE;

	// Looked up by name, never by position: models standing on the node are parented under
	// it too, so "the last child" stops being the button as soon as anything moves on.
	public Control ClickTarget => GetNode<Control>("Button");

	// Movement targets and attack targets need to read differently, so the colour is the
	// caller's choice; ToggleButton stays as the plain on/off the entrance picker uses.
	public void Highlight(Color colour) {
		ClickTarget.Modulate = colour;
	}

	public void ClearHighlight() {
		ClickTarget.Modulate = new Color(0,0,0,0);
	}

	// Models sit above the button so they can be clicked; a model click that is not
	// picking a target lands here instead, so moving onto an occupied node still works.
	public void Press() {
		if (ClickTarget is BaseButton button) button.EmitSignal(BaseButton.SignalName.Pressed);
	}

	public void ToggleButton(bool isActive) {
		Control button = ClickTarget;
		if (isActive) {
			button.Modulate = new Color(1,1,1,0.5f);
		} else {
			button.Modulate = new Color(0,0,0,0);
		}
	}

	// [Signal]
	// public delegate void SpawnPlayerEventHandler(Node entrance);

	// public override void _Ready() {
		// EmitSignal(SignalName.SpawnPlayer);
	// }

	// void OnDisable() {
	// 	this.GetComponent<Button>().onClick.RemoveAllListeners();
	//     GetNode<TextureButton>("Button")
	// }
}
