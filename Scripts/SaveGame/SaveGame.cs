using Godot;

[GlobalClass]
public partial class SaveGame : Resource
{
    [Export] public string campaignName = "New Campaign";
    [Export] public string timestamp = "";
    [Export] public string[] playerNames = new string[0];
    [Export] public string[] characterNames = new string[0];

    [ExportGroup("Players")]
    [Export] public Player[] players = new Player[0];

    [ExportGroup("Equipment Pool")]
    // Flat (id, templateName) pairs for the party-owned instance pool.
    [Export] public string[] ownedEquipment = new string[0];

    [ExportGroup("World Map")]
    // Campaign JSON file name inside res://Campaigns.
    [Export] public string campaignFile = "";
    [Export] public string worldCurrentNode = "";
    [Export] public string worldLastBonfire = "";
    [Export] public string[] worldClearedNodes = new string[0];

    public SaveGame() { }

    public static string SlotPath(int slot) => $"user://savegame_{slot}.tres";
    public static bool SlotExists(int slot) => FileAccess.FileExists(SlotPath(slot));

    public static SaveGame LoadSlot(int slot) {
        if (!SlotExists(slot)) return null;
        return ResourceLoader.Load<SaveGame>(SlotPath(slot), "", ResourceLoader.CacheMode.Ignore);
    }

    public void SaveToSlot(int slot) {
        timestamp = System.DateTime.Now.ToString("MMM d, yyyy  h:mm tt");
        ownedEquipment = GameManager.SerializeOwnedFlat();
        ResourceSaver.Save(this, SlotPath(slot));
    }
}
