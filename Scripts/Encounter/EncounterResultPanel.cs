using Godot;

// The end of an encounter (p19). A win advances the party onto the node; a wipe sends them
// back to the last bonfire, which is where the dropped souls have to be walked back from.
//
// Numbers with the soul icon, no sentences: the change to the cache ("+4", or "−4" for
// what a wipe dropped) and the total it leaves.
public partial class EncounterResultPanel : Control
{

	private const string WORLD_MAP_SCENE = "res://Scenes/WorldMap.tscn";
	private const string BONFIRE_SCENE = "res://Scenes/Bonfire.tscn";

	[Export] public Label titleLabel;
	[Export] public Control changeRow;
	[Export] public Label changeSign;   // half the number's size, so the value reads first
	[Export] public Label changeLabel;
	[Export] public Label totalLabel;
	[Export] public Button continueButton;

	[Export] public Color victoryColour = new Color(0.91f, 0.82f, 0.58f);
	[Export] public Color defeatColour = new Color(0.78f, 0.22f, 0.16f);
	[Export] public float fadeTime = 0.45f;

	private bool won;

	public override void _Ready() {
		Visible = false;
		if (continueButton != null) continueButton.Pressed += OnContinue;
	}

	// earned is null when nothing was paid (a boss win has no soul rule yet), so no row shows.
	public void ShowVictory(int? earned, int total) {
		Show(true, "Encounter Won", victoryColour, "+", earned, total);
	}

	public void ShowDefeat(int dropped, int total) {
		Show(false, "Party Defeated", defeatColour, "−", dropped > 0 ? dropped : null, total);
	}

	private void Show(bool victory, string title, Color colour, string sign, int? change, int total) {
		won = victory;

		if (titleLabel != null) {
			titleLabel.Text = title;
			titleLabel.AddThemeColorOverride("font_color", colour);
		}
		if (changeRow != null) changeRow.Visible = change.HasValue;
		if (changeSign != null) changeSign.Text = sign;
		if (changeLabel != null && change.HasValue) changeLabel.Text = change.Value.ToString();
		if (totalLabel != null) totalLabel.Text = $"Total: {total}";
		if (continueButton != null) continueButton.Text = victory ? "To the World Map" : "Back to the Bonfire";

		Visible = true;
		Modulate = new Color(1, 1, 1, 0);
		CreateTween().TweenProperty(this, "modulate:a", 1f, fadeTime);
		continueButton?.GrabFocus();
	}

	private void OnContinue() {
		string scene = won ? WORLD_MAP_SCENE : BONFIRE_SCENE;
		GetTree().ChangeSceneToPacked(ResourceLoader.Load<PackedScene>(scene));
	}
}
