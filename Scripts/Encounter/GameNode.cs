using Godot;

public partial class GameNode : Control
{
	[Export] public bool isDisabled = false;
	[Export] public bool isEntrance = false;
	[Export] public bool isEnemySpawn = false;
	
	[Export] public EncounterManager.StatusEffect statusEffect = EncounterManager.StatusEffect.NONE;

	// Movement targets and attack targets need to read differently, so the colour is the
	// caller's choice; ToggleButton stays as the plain on/off the entrance picker uses.
	public void Highlight(Color colour) {
		GetChild<Control>(-1).Modulate = colour;
	}

	public void ClearHighlight() {
		GetChild<Control>(-1).Modulate = new Color(0,0,0,0);
	}

	public Control ClickTarget => GetChild<Control>(-1);

	public void ToggleButton(bool isActive) {
		Control button = GetChild<Control>(-1);
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
