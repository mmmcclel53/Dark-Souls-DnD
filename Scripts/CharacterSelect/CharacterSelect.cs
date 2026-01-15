using Godot;
using Godot.Collections;
using System.Text.Json;

public partial class CharacterSelect : Panel
{
	private const int MAX_NUM_PLAYERS = 4;

	[Export] public Panel rootPanel;
	[Export] public Button backButton;
	
	[Export] public OptionButton numPlayersSelect;
	[Export] public VBoxContainer playersContainer;
	[Export] public BoxContainer charactersContainer;

	[Export] public Button startButton;

	private Dictionary<int, Player> players = new Dictionary<int, Player>();
	private int selectedPlayer = 0;

	public override void _Ready() {
		numPlayersSelect.ItemSelected += (index) => { OnNumPlayersChange(index); };
		backButton.Pressed += () => { OnPressedBackButton(); };
		startButton.Pressed += () => { OnSaveGame(); };

		foreach (Control character in charactersContainer.GetChildren()) {
			Player p = (Player)character;
			Button btn = (Button)character.GetChild(character.GetChildCount()-1);
			btn.Pressed += () => { OnCharacterSelect(p.character); };
		}

		// Init
		Player firstPlayer = (Player)charactersContainer.GetChild(0);
		players.Add(0, firstPlayer);
	}

	public override void _Process(double delta) {
		foreach(var (key, p) in players) {
			int i = key;

			VBoxContainer playerContainer = (VBoxContainer) playersContainer.GetChild(i);
			playerContainer.Visible = true;
			
			Button btn = (Button)playerContainer.GetChild(0);
			TextureRect image = (TextureRect)btn.GetChild(0);
			TextEdit name = (TextEdit)playerContainer.GetChild(1);

			image.Texture = p.character.image;

			name.TextChanged += () => { OnPlayerNameChange(i, name.Text); };
			btn.Pressed += () => { OnPlayerSelect(i); };
		}
	}

	private void OnNumPlayersChange(long index) {
		// Reset
		players.Clear();
		for (int i=0;i<MAX_NUM_PLAYERS;i++) {
			VBoxContainer playerContainer = (VBoxContainer) playersContainer.GetChild(i);
			playerContainer.Visible = false;
		} 

		for (int i=0;i<index+1;i++) {
			Player p = (Player)charactersContainer.GetChild(i);
			players.Add(i, p);
			GD.Print(p.name);
		}
	}

	private void OnCharacterSelect(Character c) {
		Player p = players[selectedPlayer];
		p.character = c;
	}
	private void OnPlayerNameChange(int i, string newName) {
		Player p = players[i];
		p.name = newName;
	}
	private void OnPlayerSelect(int playerNum) {
		selectedPlayer = playerNum;
	}

	private void OnPressedBackButton() {
		rootPanel.Visible = false;
	}

	private void OnSaveGame() {
		rootPanel.Visible = false;

		Dictionary<string, Variant> savedPlayers = new Dictionary<string, Variant>();
		for (int i=0;i<players.Count;i++) {
			Dictionary<string, Variant> saveData = players[i].Save();
			savedPlayers.Add(players[i].name, saveData);
		}

		Dictionary<string, Variant> saveGame = new Dictionary<string, Variant>();
		saveGame.Add("players", savedPlayers);
        string jsonString = Json.Stringify(saveGame);
		using var saveFile = FileAccess.Open("user://savegame.save", FileAccess.ModeFlags.Write);
        saveFile.StoreLine(jsonString);
	}
}
