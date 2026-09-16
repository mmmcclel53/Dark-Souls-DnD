using Godot;
using System.Collections.Generic;

// The end of an encounter (p19). A win advances the party onto the node; a wipe sends them
// back to the last bonfire, which is where the dropped souls have to be walked back from.
public partial class EncounterResultPanel : Control
{

	private const string WORLD_MAP_SCENE = "res://Scenes/WorldMap.tscn";
	private const string BONFIRE_SCENE = "res://Scenes/Bonfire.tscn";

	[Export] public Label titleLabel;
	[Export] public Label detailLabel;
	[Export] public Button continueButton;

	private bool won;

	public override void _Ready() {
		Visible = false;
		if (continueButton != null) continueButton.Pressed += OnContinue;
	}

	public void ShowResult(bool victory, List<string> lines) {
		won = victory;

		if (titleLabel != null) titleLabel.Text = victory ? "Encounter Won" : "Party Defeated";
		if (detailLabel != null) detailLabel.Text = string.Join("\n", lines);
		if (continueButton != null) continueButton.Text = victory ? "To the World Map" : "Back to the Bonfire";

		Visible = true;
	}

	private void OnContinue() {
		string scene = won ? WORLD_MAP_SCENE : BONFIRE_SCENE;
		GetTree().ChangeSceneToPacked(ResourceLoader.Load<PackedScene>(scene));
	}
}
