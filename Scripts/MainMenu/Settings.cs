using Godot;

// On the Encounter title bar's Show Stats toggle. It must extend the node it sits on: as a
// plain Node, the hovered-control lookup (CursorPolicy) failed to cast it to Control.
public partial class Settings : CheckButton
{
	private void OnPressed() {
		EncounterManager.showEnemyInfo = !EncounterManager.showEnemyInfo;
	}
}
