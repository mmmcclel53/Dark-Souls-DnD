using Godot;
using System.Collections.Generic;

public partial class CharacterSelect : Control
{
	private const int MAX_NUM_PLAYERS = 4;

	[Export] public Character DEFAULT_CHARACTER;
	[Export] public Character[] characters = [];

	[Export] public Button backButton;
	[Export] public CharacterSheet characterSheet;
	[Export] public BoxContainer playerButtonsContainer;
	[Export] public BoxContainer characterButtonsContainer;
	[Export] public Button startButton;

	[ExportGroup("Name Modal")]
	[Export] public Control nameModal;
	[Export] public TextEdit nameModalTextEdit;
	[Export] public Button nameModalConfirm;

	[ExportGroup("Character Summary")]
	[Export] public Label summaryDescLabel;
	[Export] public Label summaryPhysDamageLabel;
	[Export] public Label summaryMagicDamageLabel;
	[Export] public Label summaryPhysDefenseLabel;
	[Export] public Label summaryMagicDefenseLabel;
	[Export] public Label summaryDodgeLabel;

	private List<Player> players = new List<Player>();
	private int selectedPlayer = 0;
	private int selectedCharacterIndex = -1;
	private Texture2D addIconTexture;
	private Character pendingCharacter;
	private StyleBoxFlat playerSelectedStyle;
	private StyleBoxFlat charSelectedStyle;
	private StyleBoxFlat charUnselectedStyle;

	public override void _Ready() {
		addIconTexture = ((Button)playerButtonsContainer.GetChild(0)).GetNode<TextureRect>("AddIcon").Texture;
		pendingCharacter = DEFAULT_CHARACTER;

		var lightBlue = new Color(0.4f, 0.75f, 1.0f, 1f);
		var darkBg = new Color(0.1254902f, 0.1254902f, 0.1254902f, 1f);
		var greyBorder = new Color(0.5019608f, 0.5019608f, 0.5019608f, 1f);
		playerSelectedStyle = MakeStyleBox(new Color(0, 0, 0, 0), lightBlue, 2, 4);
		charSelectedStyle = MakeStyleBox(darkBg, lightBlue, 2, 4);
		charUnselectedStyle = MakeStyleBox(darkBg, greyBorder, 2, 4);

		backButton.Pressed += () => { OnPressedBackButton(); };
		startButton.Pressed += () => { OnStartCampaign(); };
		nameModalConfirm.Pressed += () => { OnConfirmName(); };

		for (int i = 0; i < playerButtonsContainer.GetChildCount(); i++) {
			Button playerButton = (Button)playerButtonsContainer.GetChild(i);
			int captured = i;
			playerButton.Pressed += () => { OnPlayerSelect(captured); };
			Button removeButton = playerButton.GetNode<Button>("RemoveButton");
			removeButton.Pressed += () => { OnRemovePlayer(captured); };

			var selectionBorder = new Panel();
			selectionBorder.Name = "SelectionBorder";
			selectionBorder.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			selectionBorder.MouseFilter = Control.MouseFilterEnum.Ignore;
			selectionBorder.AddThemeStyleboxOverride("panel", playerSelectedStyle);
			selectionBorder.Visible = false;
			playerButton.AddChild(selectionBorder);
		}

		for (int i = 0; i < characterButtonsContainer.GetChildCount(); i++) {
			Button characterButton = (Button)characterButtonsContainer.GetChild(i);
			int captured = i;
			characterButton.Pressed += () => { OnCharacterSelect(captured, characters[captured]); };
		}

		nameModal.Visible = true;
		RefreshSummary(DEFAULT_CHARACTER);
		RefreshSelectionStyles();

		GetNode<CharacterPortraitPane>("/root/CharacterPortraitPane")?.Hide();
	}

	private void OnConfirmName() {
		Player p = new Player(nameModalTextEdit.Text, pendingCharacter);
		players.Add(p);
		nameModal.Visible = false;
		nameModalTextEdit.Clear();

		UpdatePlayerButton(selectedPlayer, p);
		characterSheet.SetCharacter(p.character);
		RefreshSummary(p.character);

		if (selectedPlayer + 1 < MAX_NUM_PLAYERS)
			((Button)playerButtonsContainer.GetChild(selectedPlayer + 1)).Visible = true;

		RefreshSelectionStyles();
	}

	private void OnPressedBackButton() {
		GetTree().ChangeSceneToPacked(ResourceLoader.Load<PackedScene>("res://Scenes/MainMenu.tscn"));
	}

	private void OnPlayerSelect(int selected) {
		selectedPlayer = selected;
		if (selected < players.Count) {
			characterSheet.SetCharacter(players[selected].character);
			RefreshSummary(players[selected].character);
		} else
			nameModal.Visible = true;
		RefreshSelectionStyles();
	}

	private void OnRemovePlayer(int index) {
		if (index >= players.Count) return;
		players.RemoveAt(index);

		for (int i = 0; i < MAX_NUM_PLAYERS; i++) {
			Button btn = (Button)playerButtonsContainer.GetChild(i);
			if (i < players.Count) {
				UpdatePlayerButton(i, players[i]);
				btn.Visible = true;
			} else {
				ResetPlayerButton(i);
				btn.Visible = i <= players.Count;
			}
		}

		selectedPlayer = Mathf.Min(selectedPlayer, Mathf.Max(0, players.Count - 1));
		Character shownChar = players.Count > 0 ? players[selectedPlayer].character : DEFAULT_CHARACTER;
		characterSheet.SetCharacter(shownChar);
		RefreshSummary(shownChar);
		RefreshSelectionStyles();
	}

	private void OnCharacterSelect(int index, Character c) {
		selectedCharacterIndex = index;
		pendingCharacter = c;
		if (selectedPlayer < players.Count) {
			players[selectedPlayer].character = c;
			UpdatePlayerButton(selectedPlayer, players[selectedPlayer]);
		}
		characterSheet.SetCharacter(c);
		RefreshSummary(c);
		RefreshSelectionStyles();
	}

	private void UpdatePlayerButton(int index, Player p) {
		Button btn = (Button)playerButtonsContainer.GetChild(index);
		var icon = btn.GetNode<TextureRect>("AddIcon");
		icon.Texture = p.character?.avatar ?? addIconTexture;
		Label label = btn.GetNode<Label>("Name");
		label.Text = p.name;
		label.Visible = true;
		btn.GetNode<Button>("RemoveButton").Visible = true;
	}

	private void RefreshSummary(Character c) {
		summaryDescLabel.Text = c?.description ?? "";
		var (pdMin, pdMax) = c?.GetBestPhysicalDamage() ?? (0, 0);
		summaryPhysDamageLabel.Text = $"{pdMin}~{pdMax} Physical Damage";
		var (mdMin, mdMax) = c?.GetBestMagicDamage() ?? (0, 0);
		summaryMagicDamageLabel.Text = $"{mdMin}~{mdMax} Magic Damage";
		var (pfMin, pfMax) = c?.GetPhysicalDefense() ?? (0, 0);
		summaryPhysDefenseLabel.Text = $"{pfMin}~{pfMax} Physical Defense";
		var (mfMin, mfMax) = c?.GetMagicDefense() ?? (0, 0);
		summaryMagicDefenseLabel.Text = $"{mfMin}~{mfMax} Magic Defense";
		summaryDodgeLabel.Text = $"{c?.GetDodge() ?? 0} Dodge";
	}

	private void ResetPlayerButton(int index) {
		Button btn = (Button)playerButtonsContainer.GetChild(index);
		btn.GetNode<TextureRect>("AddIcon").Texture = addIconTexture;
		Label label = btn.GetNode<Label>("Name");
		label.Text = "";
		label.Visible = false;
		btn.GetNode<Button>("RemoveButton").Visible = false;
	}

	private void RefreshSelectionStyles() {
		for (int i = 0; i < playerButtonsContainer.GetChildCount(); i++) {
			Button btn = (Button)playerButtonsContainer.GetChild(i);
			btn.GetNode<Panel>("SelectionBorder").Visible = (i == selectedPlayer);
		}
		for (int i = 0; i < characterButtonsContainer.GetChildCount(); i++) {
			Button btn = (Button)characterButtonsContainer.GetChild(i);
			btn.AddThemeStyleboxOverride("normal", i == selectedCharacterIndex ? charSelectedStyle : charUnselectedStyle);
		}
	}

	private static StyleBoxFlat MakeStyleBox(Color bg, Color border, int borderWidth, int cornerRadius) {
		var s = new StyleBoxFlat();
		s.BgColor = bg;
		s.BorderColor = border;
		s.BorderWidthLeft = s.BorderWidthTop = s.BorderWidthRight = s.BorderWidthBottom = borderWidth;
		s.CornerRadiusTopLeft = s.CornerRadiusTopRight = s.CornerRadiusBottomRight = s.CornerRadiusBottomLeft = cornerRadius;
		return s;
	}

	private void OnStartCampaign() {
		GameManager.EnsureCatalogLoaded();
		GameManager.ResetOwnedPool();

		// Mint a unique instance for each starting slot of each chosen character.
		// Per design: equipment instances are unique-id'd; two characters with the
		// same starting weapon get two separate instances.
		for (int i = 0; i < players.Count; i++) {
			Player p = players[i];
			Character c = p.character;
			if (c == null) continue;
			p.leftHandId    = MintAndId(c.leftHandDefault);
			p.rightHandId   = MintAndId(c.rightHandDefault);
			p.backupSlotId  = MintAndId(c.backupSlotDefault);
			p.armourId      = MintAndId(c.armourDefault);
		}

		var save = new SaveGame();
		save.campaignName = "Campaign " + System.DateTime.Now.ToString("M/d/yy");
		save.campaignFile = string.IsNullOrEmpty(WorldMapManager.SelectedCampaignFile)
			? WorldMapManager.DEFAULT_CAMPAIGN : WorldMapManager.SelectedCampaignFile;
		save.players = players.ToArray();
		save.playerNames = new string[players.Count];
		save.characterNames = new string[players.Count];
		for (int i = 0; i < players.Count; i++) {
			save.playerNames[i] = players[i].name;
			save.characterNames[i] = players[i].character?.name ?? "";
		}

		int slot = 1;
		while (slot <= 4 && SaveGame.SlotExists(slot)) slot++;
		if (slot > 4) slot = 1;
		save.SaveToSlot(slot);

		CampaignManager.StartNew(save.players, slot, save);
		GetTree().ChangeSceneToPacked(ResourceLoader.Load<PackedScene>("res://Scenes/Bonfire.tscn"));
	}

	private static string MintAndId(Equipment template) {
		if (template == null || string.IsNullOrEmpty(template.name)) return "";
		var inst = GameManager.MintInstance(template.name);
		return inst?.id ?? "";
	}
}
