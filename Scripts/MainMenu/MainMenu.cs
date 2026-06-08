using Godot;

public partial class MainMenu : VBoxContainer
{
	[Export] public Button newCampaignButton;
	[Export] public Button loadGameButton;
	[Export] public Button quitButton;

	public override void _Ready() {
		newCampaignButton.Pressed += () => { OnPressedNewCampaign(); };
		loadGameButton.Pressed += () => { OnPressedLoadGame(); };
		quitButton.Pressed += () => { OnPressedQuit(); };
	}

	private void OnPressedNewCampaign() {
		GetTree().ChangeSceneToPacked(ResourceLoader.Load<PackedScene>("res://Scenes/CharacterCreation.tscn"));
	}

	private void OnPressedLoadGame() {
		GetTree().ChangeSceneToPacked(ResourceLoader.Load<PackedScene>("res://Scenes/LoadGame.tscn"));
	}

	private void OnPressedQuit() {
		GetTree().Quit();
	}


}
