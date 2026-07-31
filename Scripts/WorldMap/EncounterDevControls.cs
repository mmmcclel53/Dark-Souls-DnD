using Godot;

// Temporary bridge between the world map and the encounter scene: sets the
// encounter title from the pending world node, and (debug builds only) shows
// Win/Die buttons so the campaign loop can be tested before combat resolution
// exists. Delete the buttons once real win/loss detection reports to
// WorldMapManager.ReportEncounterWon / ReportPartyDeath.
public partial class EncounterDevControls : Control {
	[Export] public Label encounterTitle;

	public override void _Ready() {
		var node = WorldMapManager.GetPendingEncounterNode();
		if (node != null && encounterTitle != null)
			encounterTitle.Text = $"{node.Title}  -  Level {node.level} Encounter";

		if (!OS.IsDebugBuild() || !WorldMapManager.HasPendingEncounter) {
			Visible = false;
			return;
		}

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		AddChild(row);

		var winButton = new Button { Text = "DEV: Win" };
		winButton.Pressed += () => {
			WorldMapManager.ReportEncounterWon();
			GetTree().ChangeSceneToPacked(ResourceLoader.Load<PackedScene>("res://Scenes/WorldMap.tscn"));
		};
		row.AddChild(winButton);

		var dieButton = new Button { Text = "DEV: Die" };
		dieButton.Pressed += () => {
			WorldMapManager.ReportPartyDeath();
			GetTree().ChangeSceneToPacked(ResourceLoader.Load<PackedScene>("res://Scenes/Bonfire.tscn"));
		};
		row.AddChild(dieButton);
	}
}
