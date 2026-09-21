using Godot;

// Temporary bridge between the world map and the encounter scene: sets the
// encounter title from the pending world node, and (debug builds only) shows
// Win/Die buttons so the campaign loop can be tested before combat resolution
// exists. Delete the buttons once real win/loss detection reports to
// WorldMapManager.ReportEncounterWon / ReportPartyDeath.
//
// The buttons are handed to a container in the title bar rather than anchored over the
// screen, so they take their own space instead of landing on top of what is already there.
public partial class EncounterDevControls : Node {
	[Export] public Label encounterTitle;
	[Export] public Container buttonHost;

	public override void _Ready() {
		var node = WorldMapManager.GetPendingEncounterNode();
		if (node != null && encounterTitle != null)
			encounterTitle.Text = $"{node.Title}  -  Level {node.level} Encounter";

		if (!OS.IsDebugBuild() || !WorldMapManager.HasPendingEncounter || buttonHost == null) return;

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		row.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		buttonHost.AddChild(row);
		// After the title, before the Show Stats toggle.
		buttonHost.MoveChild(row, buttonHost.GetChildCount() - 2);

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
