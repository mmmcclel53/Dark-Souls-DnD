using Godot;

[GlobalClass]
public partial class SaveGame : Resource
{
    [Export] public string name = "Save 1";

    [ExportGroup("Players")]
    [Export] public Player[] players;
    
    public SaveGame()
    {
        this.players = [];
    }
}