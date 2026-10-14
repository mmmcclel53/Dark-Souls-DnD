using Godot;
using Godot.Collections;

[GlobalClass]
public partial class SaveGame : Resource
{
    [Export] public string campaignName = "New Campaign";
    [Export] public string timestamp = "";
    [Export] public string[] playerNames = new string[0];
    [Export] public string[] characterNames = new string[0];

    // Where the party was when this was written, so loading puts them back there: one of
    // CampaignManager's scene names. Every save is an autosave (CampaignManager.Autosave).
    [Export] public string scene = CampaignManager.BONFIRE;

    [ExportGroup("Players")]
    [Export] public Player[] players = new Player[0];
    // Each character's endurance bar, in players' order. It is encounter-scoped, but a wipe
    // leaves it on until a rest (p19), and a save in mid-fight has to keep it too.
    [Export] public int[] staminaSpent = new int[0];
    [Export] public int[] damageTaken = new int[0];

    [ExportGroup("Equipment Pool")]
    // Flat (id, templateName) pairs for the party-owned instance pool.
    [Export] public string[] ownedEquipment = new string[0];

    [ExportGroup("World Map")]
    // Campaign JSON file name inside res://Campaigns, for a hand-built campaign.
    [Export] public string campaignFile = "";
    // A generated campaign's map, in the same JSON (CampaignGenerator). Wins over campaignFile.
    [Export] public string campaignMap = "";
    // Its size in sections (CampaignGenerator.SMALL / MEDIUM / LARGE), for the Load Game slots.
    [Export] public int campaignSize = 0;
    [Export] public string worldCurrentNode = "";
    [Export] public string worldLastBonfire = "";
    [Export] public string[] worldClearedNodes = new string[0];
    // Fog of war (WorldMapManager.Look): the hexes seen at some point, and the ones only glimpsed.
    [Export] public string[] worldKnownNodes = new string[0];
    [Export] public string[] worldGlimpsedNodes = new string[0];
    // Map loot already picked up (WorldMapManager.TakeLoot), and the keys the party holds.
    [Export] public string[] worldLootTaken = new string[0];
    [Export] public string[] keys = new string[0];
    // Ladders, trees and doors the party has opened (WorldMapManager.OpenEdge), by edge id.
    [Export] public string[] worldOpenEdges = new string[0];
    // Every encounter on the map, rolled once (EncounterGenerator) so a re-fight is the same fight.
    [Export] public EncounterPlan[] encounterPlans = new EncounterPlan[0];
    // The encounter the party is in, or "".
    [Export] public string encounterNode = "";
    // The roaming threat it is against, when it is one: encounterNode is then the hex it met them on.
    [Export] public string encounterRoamer = "";
    // The roaming threats (WorldMapManager.TickRoamers), in the map's order.
    [Export] public Array<RoamerState> roamers = new Array<RoamerState>();
    // The board at the encounter's last turn boundary; null before the first, or outside a fight.
    [Export] public EncounterSnapshot encounter;

    // Souls (p19). The cache is the party's shared pool, kept as lots that remember the
    // encounter each came from (SoulCache). On a wipe it is dropped on the node where the
    // character died and has to be walked back to; a second death before that loses it, and
    // its souls go back to their encounters. The drop is pinned to a world node AND a grid
    // index inside it, because the encounter is rebuilt from scratch every time it is entered.
    [Export] public Array<SoulLot> heldSouls = new Array<SoulLot>();
    [Export] public Array<SoulLot> droppedSouls = new Array<SoulLot>();
    [Export] public string droppedSoulsWorldNode = "";
    [Export] public int droppedSoulsGridIndex = -1;

    // The Merchant (Merchant): the treasure deck left, by template name, and the cards on
    // show ("" for one bought), with how many paying wins there have been since they were put out.
    [ExportGroup("Merchant")]
    [Export] public string[] treasureDeck = new string[0];
    [Export] public string[] merchantStock = new string[0];
    [Export] public bool merchantStocked;
    [Export] public int winsSinceRestock;

    // The party's shared Estus flasks (PartyEstus). -1 is a save from before they existed, set
    // to the starting pool the first time it is read.
    [ExportGroup("Party Estus")]
    [Export] public int estusMax = -1;
    [Export] public int estusCharges = 0;
    [Export] public int estusShards = 0;

    // The party's Homeward Bones (HomewardBone), found as map loot.
    [ExportGroup("Homeward Bones")]
    [Export] public int homewardBones = 0;

    // A beaten boss's cards, waiting for the party to choose one (BossRoster). Kept here so
    // quitting before the choice does not lose it.
    [ExportGroup("Boss Treasure")]
    [Export] public string[] bossChoice = new string[0];
    [Export] public string bossChoiceNode = "";

    public SaveGame() { }

    public const int SLOTS = 4;

    public static string SlotPath(int slot) => $"user://savegame_{slot}.tres";
    public static bool SlotExists(int slot) => FileAccess.FileExists(SlotPath(slot));

    // The slot written last, for Continue; -1 when there are none.
    public static int MostRecentSlot() {
        int best = -1;
        ulong bestTime = 0;
        for (int slot = 1; slot <= SLOTS; slot++) {
            if (!SlotExists(slot)) continue;
            ulong time = FileAccess.GetModifiedTime(SlotPath(slot));
            if (best < 0 || time > bestTime) {
                best = slot;
                bestTime = time;
            }
        }
        return best;
    }

    public static SaveGame LoadSlot(int slot) {
        if (!SlotExists(slot)) return null;
        return ResourceLoader.Load<SaveGame>(SlotPath(slot), "", ResourceLoader.CacheMode.Ignore);
    }

    public void SaveToSlot(int slot) {
        timestamp = System.DateTime.Now.ToString("MMM d, yyyy  h:mm tt");
        ownedEquipment = GameManager.SerializeOwnedFlat();
        CaptureEndurance();
        ResourceSaver.Save(this, SlotPath(slot));
    }

    private void CaptureEndurance() {
        staminaSpent = new int[players.Length];
        damageTaken = new int[players.Length];
        for (int i = 0; i < players.Length; i++) {
            staminaSpent[i] = players[i]?.endurance.staminaSpent ?? 0;
            damageTaken[i] = players[i]?.endurance.damageTaken ?? 0;
        }
    }

    public void RestoreEndurance() {
        for (int i = 0; i < players.Length; i++) {
            if (players[i] == null) continue;
            int stamina = i < staminaSpent.Length ? staminaSpent[i] : 0;
            int damage = i < damageTaken.Length ? damageTaken[i] : 0;
            players[i].endurance.Restore(stamina, damage);
        }
    }
}
