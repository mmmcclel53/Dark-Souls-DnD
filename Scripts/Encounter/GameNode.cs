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

	// How a clickable node draws the eye: the marker breathes in brightness and a faint ring
	// drifts out from under it. Never in size: scaling the texture makes it shimmer.
	[Export] public float pulseRate = 0.6f;
	[Export] public float pulseDepth = 0.2f;
	[Export] public float rippleRate = 0.4f;
	[Export] public float rippleReach = 0.4f;
	[Export] public float rippleAlpha = 0.35f;

	private bool pulsing;
	private Color pulseColour;
	private float pulseTime;

	public override void _Ready() {
		SetProcess(false);
	}

	// Movement targets and attack targets need to read differently, so the colour is the
	// caller's choice. Pulse only what can be clicked — a soul drop is a marker, not a target.
	public void Highlight(Color colour, bool pulse = false) {
		ClickTarget.Modulate = colour;
		SetPulse(pulse, colour);
	}

	public void ClearHighlight() {
		ClickTarget.Modulate = new Color(0,0,0,0);
		SetPulse(false, default);
	}

	// Models sit above the button so they can be clicked; a model click that is not
	// picking a target lands here instead, so moving onto an occupied node still works.
	public void Press() {
		if (ClickTarget is BaseButton button) button.EmitSignal(BaseButton.SignalName.Pressed);
	}

	public void ToggleButton(bool isActive) {
		if (isActive) Highlight(new Color(1,1,1,0.5f), true);
		else ClearHighlight();
	}

	private void SetPulse(bool pulse, Color colour) {
		pulsing = pulse;
		pulseColour = colour;
		pulseTime = 0f;
		SetProcess(pulse);
		ClickTarget.MouseDefaultCursorShape = pulse ? CursorShape.PointingHand : CursorShape.Arrow;
		QueueRedraw();
	}

	public override void _Process(double delta) {
		pulseTime += (float)delta;
		float wave = (Mathf.Sin(pulseTime * Mathf.Tau * pulseRate) + 1f) * 0.5f;

		Color colour = pulseColour;
		colour.A = Mathf.Clamp(pulseColour.A + pulseDepth * (wave - 0.5f), 0f, 1f);
		ClickTarget.Modulate = colour;
		QueueRedraw();
	}

	// Drawn by the node itself, so it sits under the marker and the models standing here.
	public override void _Draw() {
		if (!pulsing) return;
		Control marker = ClickTarget;
		float progress = pulseTime * rippleRate % 1f;
		float radius = marker.Size.X * 0.5f * (1f + rippleReach * progress);
		// Eased so the ring fades in as well as out, rather than popping in at full strength.
		float fade = Mathf.Sin(progress * Mathf.Pi);
		Color colour = new Color(pulseColour.R, pulseColour.G, pulseColour.B, fade * rippleAlpha);
		float width = Mathf.Max(1f, marker.Size.X * 0.03f);
		DrawArc(marker.Position + marker.Size * 0.5f, radius, 0f, Mathf.Tau, 48, colour, width, true);
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
