using Godot;

public partial class Settings : Node
{
	private void OnPressed() {
		EncounterManager.showEnemyInfo = !EncounterManager.showEnemyInfo;
	}
}
