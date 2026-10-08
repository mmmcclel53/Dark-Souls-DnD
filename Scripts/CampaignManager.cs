using Godot;

public static class CampaignManager {
    // The scenes a save can be resumed in (SaveGame.scene).
    public const string BONFIRE = "Bonfire";
    public const string WORLD_MAP = "WorldMap";
    public const string ENCOUNTER = "Encounter";

    public static string ScenePath(string scene) => $"res://Scenes/{scene}.tscn";

    public static Player[] Players { get; private set; } = new Player[0];
    public static int SaveSlot { get; private set; } = -1;
    public static SaveGame CurrentSave { get; private set; }

    public static void StartNew(Player[] players, int slot, SaveGame save) {
        Players = players;
        SaveSlot = slot;
        CurrentSave = save;
        WorldMapManager.Reset();
    }

    public static bool LoadFromSlot(int slot) {
        var save = SaveGame.LoadSlot(slot);
        if (save == null || save.players == null || save.players.Length == 0) return false;
        // Restore the equipment pool BEFORE players reference instances by id.
        GameManager.EnsureCatalogLoaded();
        GameManager.DeserializeOwnedFlat(save.ownedEquipment);
        Players = save.players;
        save.RestoreEndurance();
        SaveSlot = slot;
        CurrentSave = save;
        WorldMapManager.Reset();
        return true;
    }

    // Loads a slot and returns the scene to open, wherever the party was: the Bonfire, the
    // World Map, or the encounter they were in (at its last turn boundary). Null on failure.
    public static string Resume(int slot) {
        if (!LoadFromSlot(slot)) return null;
        switch (CurrentSave.scene) {
            case ENCOUNTER:
                if (WorldMapManager.ResumeEncounter(CurrentSave.encounterNode)) return ScenePath(ENCOUNTER);
                return ScenePath(WORLD_MAP);
            case WORLD_MAP:
                return ScenePath(WORLD_MAP);
            default:
                return ScenePath(BONFIRE);
        }
    }

    // Every save is this: called on entering each scene, on moving across the map, on a rest,
    // on changing gear, at an encounter's end and at each of its turn boundaries. `scene`
    // records where the party now is; leaving an encounter drops its board. Does nothing
    // without a campaign, so a scene run on its own never writes one.
    public static void Autosave(string scene = null) {
        if (CurrentSave == null) return;
        if (scene != null) {
            CurrentSave.scene = scene;
            if (scene != ENCOUNTER) CurrentSave.encounter = null;
        }
        if (SaveSlot > 0) CurrentSave.SaveToSlot(SaveSlot);
    }

    // Resting at a bonfire clears every endurance bar. p20's ten boxes are HP and Stamina
    // together, so clearing them is both — and nothing else puts them back after a wipe,
    // since only a *win* clears the bars at the end of an encounter (p19).
    public static void RestParty() {
        if (Players == null) return;
        foreach (Player player in Players) {
            player?.endurance?.Clear();
            player?.RefreshTokens();
        }
    }

    public static void Clear() {
        Players = new Player[0];
        SaveSlot = -1;
        CurrentSave = null;
        GameManager.ResetOwnedPool();
        WorldMapManager.Reset();
    }
}
