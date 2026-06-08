using Godot;

[GlobalClass]
public partial class SaveGame : Resource
{
    [Export] public string campaignName = "New Campaign";
    [Export] public string timestamp = "";
    [Export] public string[] playerNames = [];
    [Export] public string[] characterNames = [];

    [ExportGroup("Players")]
    [Export] public Player[] players = [];

    public SaveGame() { }

    public static string SlotPath(int slot) => $"user://savegame_{slot}.tres";
    public static bool SlotExists(int slot) => FileAccess.FileExists(SlotPath(slot));

    public static SaveGame LoadSlot(int slot) {
        if (!SlotExists(slot)) return null;
        return ResourceLoader.Load<SaveGame>(SlotPath(slot), "", ResourceLoader.CacheMode.Ignore);
    }

    public void SaveToSlot(int slot) {
        timestamp = System.DateTime.Now.ToString("MMM d, yyyy  h:mm tt");
        ResourceSaver.Save(this, SlotPath(slot));
    }
}