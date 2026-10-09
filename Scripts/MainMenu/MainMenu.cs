using Godot;

public partial class MainMenu : VBoxContainer
{
	[Export] public Button continueButton;
	[Export] public Button newCampaignButton;
	[Export] public Button loadGameButton;
	[Export] public Button quitButton;

	public override void _Ready() {
		continueButton.Pressed += () => { OnPressedContinue(); };
		newCampaignButton.Pressed += () => { OnPressedNewCampaign(); };
		loadGameButton.Pressed += () => { OnPressedLoadGame(); };
		quitButton.Pressed += () => { OnPressedQuit(); };

		continueButton.Disabled = SaveGame.MostRecentSlot() < 0;
		if (!continueButton.Disabled) continueButton.GrabFocus();

		GetNode<CharacterPortraitPane>("/root/CharacterPortraitPane")?.Hide();
	}

	// The save written last, wherever the party was.
	private void OnPressedContinue() {
		string scene = CampaignManager.Resume(SaveGame.MostRecentSlot());
		if (scene == null) {
			GD.PrintErr("Failed to continue the last save");
			return;
		}
		GetTree().ChangeSceneToPacked(ResourceLoader.Load<PackedScene>(scene));
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
