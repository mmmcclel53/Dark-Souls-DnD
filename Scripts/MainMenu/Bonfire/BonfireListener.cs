using Godot;
using Godot.Collections;

public partial class BonfireListener : Node {

	[Export] public Array<PackedScene> players;
	
	public override void _Ready() {
		int index = 0;
		foreach (PackedScene p in players) {
			Control player = (Control)p.Instantiate();
			Control spawnPoint = (Control)GetChild(index > 1 ? 2 : 0);
			spawnPoint.AddChild(player);
			index++;
		}
	}
}
