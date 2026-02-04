using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Text.Json;

public partial class CharacterSelect : Panel
{
	private const int MAX_NUM_PLAYERS = 4;

	private Character Assassin = ResourceLoader.Load<Character>("res://Resources/Prefabs/Player/Assassin/Assassin.tres");
	private Character Cleric = ResourceLoader.Load<Character>("res://Resources/Prefabs/Player/Cleric/Cleric.tres");
	private Character Deprived = ResourceLoader.Load<Character>("res://Resources/Prefabs/Player/Deprived/Deprived.tres");
	private Character Herald = ResourceLoader.Load<Character>("res://Resources/Prefabs/Player/Herald/Herald.tres");
	private Character Knight = ResourceLoader.Load<Character>("res://Resources/Prefabs/Player/Knight/Knight.tres");

	[Export] public Button backButton;
	[Export] public TextureRect characterSheet;
	[Export] public VBoxContainer playerButtonsContainer;
	[Export] public BoxContainer characterButtonsContainer;
	[Export] public Button startButton;

	[Export] public Control nameModal;
	[Export] public TextEdit nameModalTextEdit;
	[Export] public Button nameModalConfirm;

	private List<Player> players;
	private int selectedPlayer = 0;

	public override void _Ready() {
		backButton.Pressed += () => { OnPressedBackButton(); };
		startButton.Pressed += () => { OnStart(); };
		nameModalConfirm.Pressed += () => { OnConfirmName(); };

		for (int i=0; i<playerButtonsContainer.GetChildCount(); i++) {
			TextureButton playerButton = (TextureButton)playerButtonsContainer.GetChild(i); 
			playerButton.Pressed += () => { OnPlayerSelect(i); };
		}

		foreach (Button playerButton in playerButtonsContainer.GetChildren()) {
			// Button playerButton = 
			// playerButton.Pressed += () => { OnCharacterSelect(); };
		}

		// // Init
		// Player firstPlayer = (Player)charactersContainer.GetChild(0);
		// players.Add(0, firstPlayer);
	}

	private void OnPlayerSelect(int selected) {
		selectedPlayer = selected;
		if (players.Count < selected+1) {
			nameModal.Visible = true;
		}
	}

	private void OnConfirmName() {
		Player p = new Player(nameModalTextEdit.Text, Assassin);
		players.Add(p);

		nameModal.Visible = false;
		nameModalTextEdit.Clear();

		int numPlayers = players.Count;
		TextureButton currPlayerButton = (TextureButton)playerButtonsContainer.GetChild(numPlayers);
		currPlayerButton.TextureNormal = p.character.avatar;
		Label currLabel = (Label)currPlayerButton.GetChild(0);
		currLabel.Text = p.name;

		if (numPlayers < MAX_NUM_PLAYERS){
			TextureButton nextPlayerButton = (TextureButton)playerButtonsContainer.GetChild(numPlayers+1);
			nextPlayerButton.Visible = true;
		}
	}

	private void OnCharacterSelect(Character c) {
		Player p = players[selectedPlayer];
		p.character = c;
		// characterSheet.
	}

	private void OnPressedBackButton() {
		Node MainMenuScene = ResourceLoader.Load<PackedScene>("res://Scenes/MainMenu.tscn").Instantiate();
		GetTree().Root.AddChild(MainMenuScene);
		GetTree().Root.GetChild(0).QueueFree();
	}

	private void OnStart() {
		// Dictionary<string, Variant> savedPlayers = new Dictionary<string, Variant>();
		// for (int i=0;i<players.Count;i++) {
		// 	// Dictionary<string, Variant> saveData = players[i].Save();
		// 	// savedPlayers.Add(players[i].name, saveData);
		// }

		// Dictionary<string, Variant> saveGame = new Dictionary<string, Variant>();
		// saveGame.Add("players", savedPlayers);
        // string jsonString = Json.Stringify(saveGame);
		// using var saveFile = FileAccess.Open("user://savegame.tres", FileAccess.ModeFlags.Write);
        // saveFile.StoreLine(jsonString);

		Node BonfireScene = ResourceLoader.Load<PackedScene>("res://Scenes/BonfireScene.tscn").Instantiate();
		GetTree().Root.AddChild(BonfireScene);
		GetTree().Root.GetChild(0).QueueFree();
	}
}
