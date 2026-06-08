public static class CampaignManager {
    public static Player[] Players { get; private set; } = [];
    public static int SaveSlot { get; private set; } = -1;
    public static SaveGame CurrentSave { get; private set; }

    public static void StartNew(Player[] players, int slot, SaveGame save) {
        Players = players;
        SaveSlot = slot;
        CurrentSave = save;
    }

    public static bool LoadFromSlot(int slot) {
        var save = SaveGame.LoadSlot(slot);
        if (save == null || save.players == null || save.players.Length == 0) return false;
        Players = save.players;
        SaveSlot = slot;
        CurrentSave = save;
        return true;
    }

    public static void Clear() {
        Players = [];
        SaveSlot = -1;
        CurrentSave = null;
    }
}
