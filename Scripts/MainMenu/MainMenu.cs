using Godot;

public partial class MainMenu : VBoxContainer
{
	[Export] public Button newCampaignButton;
	[Export] public Button loadGameButton;
	[Export] public Button quitButton;
	[Export] public OptionButton campaignSelect;

	private string[] campaignFiles = new string[0];

	public override void _Ready() {
		newCampaignButton.Pressed += () => { OnPressedNewCampaign(); };
		loadGameButton.Pressed += () => { OnPressedLoadGame(); };
		quitButton.Pressed += () => { OnPressedQuit(); };

		PopulateCampaigns();

		GetNode<CharacterPortraitPane>("/root/CharacterPortraitPane")?.Hide();
	}

	private void PopulateCampaigns() {
		campaignFiles = WorldMapManager.ListCampaignFiles();
		if (campaignFiles.Length == 0) {
			campaignSelect.AddItem("No campaigns found");
			campaignSelect.Disabled = true;
			newCampaignButton.Disabled = true;
			return;
		}
		foreach (string file in campaignFiles)
			campaignSelect.AddItem(System.IO.Path.GetFileNameWithoutExtension(file));
	}

	private void OnPressedNewCampaign() {
		if (campaignFiles.Length > 0)
			WorldMapManager.SelectedCampaignFile = campaignFiles[campaignSelect.Selected];
		GetTree().ChangeSceneToPacked(ResourceLoader.Load<PackedScene>("res://Scenes/CharacterCreation.tscn"));
	}

	private void OnPressedLoadGame() {
		GetTree().ChangeSceneToPacked(ResourceLoader.Load<PackedScene>("res://Scenes/LoadGame.tscn"));
	}

	private void OnPressedQuit() {
		GetTree().Quit();
	}


}
