using Godot;

public partial class MainMenu : VBoxContainer
{
	[Export] public Button newCampaignButton;
	[Export] public Panel characterSelectPanel;

	[Export] public Button continueButton;
	// [Export] public Panel loadGamePanel;

	[Export] public Button quitButton;

	public override void _Ready() {
		newCampaignButton.Pressed += () => { OnPressedNewCampaign(); };
		quitButton.Pressed += () => { OnPressedQuit(); };

		if (!FileAccess.FileExists("user://savegame.save")) {
			continueButton.Disabled = true;
		} else {
			continueButton.Disabled = false;
			continueButton.Pressed += () => { OnPressedContinue(); };
		}
	}

	private void OnPressedNewCampaign() {
		characterSelectPanel.Visible = true;
	}

	private void OnPressedContinue() {
		Node BonfireScene = ResourceLoader.Load<PackedScene>("res://Scenes/PrepareToDie.tscn").Instantiate();
		GetTree().Root.AddChild(BonfireScene);
		GetTree().Root.GetChild(0).QueueFree();
		// loadGamePanel.Visible = true;
	}

	private void OnPressedQuit() {
		GetTree().Quit();
	}

	private void OnLoadGame() {
		using var saveGame = FileAccess.Open("user://savegame.save", FileAccess.ModeFlags.Read);

		while (saveGame.GetPosition() < saveGame.GetLength())
		{
			var jsonString = saveGame.GetLine();

			// Creates the helper class to interact with JSON
			var json = new Json();
			var parseResult = json.Parse(jsonString);
			if (parseResult != Error.Ok)
			{
				GD.Print($"JSON Parse Error: {json.GetErrorMessage()} in {jsonString} at line {json.GetErrorLine()}");
				continue;
			}

			// Get the data from the JSON object
			var nodeData = new Godot.Collections.Dictionary<string, Variant>((Godot.Collections.Dictionary)json.Data);

			// Firstly, we need to create the object and add it to the tree and set its position.
			var newObjectScene = GD.Load<PackedScene>(nodeData["Filename"].ToString());
			var newObject = newObjectScene.Instantiate<Node>();
			GetNode(nodeData["Parent"].ToString()).AddChild(newObject);
			newObject.Set(Node2D.PropertyName.Position, new Vector2((float)nodeData["PosX"], (float)nodeData["PosY"]));

			// Now we set the remaining variables.
			foreach (var (key, value) in nodeData)
			{
				if (key == "Filename" || key == "Parent" || key == "PosX" || key == "PosY")
				{
					continue;
				}
				newObject.Set(key, value);
			}
		}
	}
}
