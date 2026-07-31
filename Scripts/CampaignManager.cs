public static class CampaignManager {
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
        SaveSlot = slot;
        CurrentSave = save;
        WorldMapManager.Reset();
        return true;
    }

    public static void Clear() {
        Players = new Player[0];
        SaveSlot = -1;
        CurrentSave = null;
        GameManager.ResetOwnedPool();
        WorldMapManager.Reset();
    }
}
