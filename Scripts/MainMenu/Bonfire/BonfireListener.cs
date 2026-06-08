using Godot;

public partial class BonfireListener : Node {
    [Export] public PackedScene characterSheetScene;

    public override void _Ready() {
        var players = CampaignManager.Players;
        if (players == null || players.Length == 0) return;

        for (int i = 0; i < players.Length; i++) {
            var sheet = characterSheetScene.Instantiate<CharacterSheet>();
            // Children: 0 = left VBoxContainer, 1 = Bonfire graphic, 2 = right VBoxContainer
            Control spawnPoint = (Control)GetChild(i >= 2 ? 2 : 0);
            spawnPoint.AddChild(sheet);
            sheet.SetCharacter(players[i].character);
        }
    }
}
