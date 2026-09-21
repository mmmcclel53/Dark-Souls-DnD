using Godot;
using System.Collections.Generic;
using System.Linq;

// Static campaign-run world state: which node the party is on, which encounters
// are cleared, and the last bonfire checkpoint. Mirrors itself into
// CampaignManager.CurrentSave; disk writes happen when resting at a bonfire.
public static class WorldMapManager {
	public const string CAMPAIGN_DIR = "res://Campaigns";
	public const string DEFAULT_CAMPAIGN = "DemoCampaign.json";

	// Set by the MainMenu campaign dropdown; read when a new campaign save is created.
	public static string SelectedCampaignFile = "";

	public static WorldMapData MapData { get; private set; }
	public static string CurrentNodeId { get; private set; } = "";
	public static string LastBonfireId { get; private set; } = "";

	// Set when "Start Encounter" is clicked; consumed by the encounter result reports.
	public static string PendingEncounterNodeId { get; private set; } = "";
	public static bool HasPendingEncounter => !string.IsNullOrEmpty(PendingEncounterNodeId);

	private static HashSet<string> clearedNodes = new HashSet<string>();
	private static string loadedCampaignFile = "";

	public static void Reset() {
		MapData = null;
		CurrentNodeId = "";
		LastBonfireId = "";
		PendingEncounterNodeId = "";
		clearedNodes.Clear();
		loadedCampaignFile = "";
	}

	// Loads the campaign map named by the current save (or the default) and
	// restores world state from the save. Safe to call every time the WorldMap
	// scene opens; reloads only when the campaign file changes.
	public static bool EnsureLoaded() {
		var save = CampaignManager.CurrentSave;
		string file = save != null && !string.IsNullOrEmpty(save.campaignFile) ? save.campaignFile : DEFAULT_CAMPAIGN;
		if (MapData != null && loadedCampaignFile == file) return true;

		MapData = WorldMapData.LoadFromFile($"{CAMPAIGN_DIR}/{file}");
		if (MapData == null) return false;
		loadedCampaignFile = file;

		clearedNodes = new HashSet<string>(save?.worldClearedNodes ?? new string[0]);
		CurrentNodeId = save?.worldCurrentNode ?? "";
		LastBonfireId = save?.worldLastBonfire ?? "";
		PendingEncounterNodeId = "";

		var start = MapData.GetStartNode();
		if (MapData.GetNode(CurrentNodeId) == null)
			CurrentNodeId = start?.id ?? "";
		if (MapData.GetNode(LastBonfireId) == null)
			LastBonfireId = CurrentNodeId;

		WriteToSave();
		return true;
	}

	public static bool IsCleared(string nodeId) => clearedNodes.Contains(nodeId);

	public static void SetCurrentNode(string nodeId) {
		CurrentNodeId = nodeId;
		WriteToSave();
	}

	public static void StartEncounter(string nodeId) {
		PendingEncounterNodeId = nodeId;
	}

	public static WorldNodeData GetPendingEncounterNode() => MapData?.GetNode(PendingEncounterNodeId);

	// Encounter won: mark the node cleared and advance the party onto it.
	public static void ReportEncounterWon() {
		if (!HasPendingEncounter) return;
		clearedNodes.Add(PendingEncounterNodeId);
		CurrentNodeId = PendingEncounterNodeId;
		PendingEncounterNodeId = "";
		WriteToSave();
	}

	// Party wipe: back to the last bonfire, every encounter respawns.
	public static void ReportPartyDeath() {
		PendingEncounterNodeId = "";
		clearedNodes.Clear();
		if (!string.IsNullOrEmpty(LastBonfireId))
			CurrentNodeId = LastBonfireId;
		WriteToSave();
	}

	// The whole rest action, and the only one: clears the party's endurance bars, records the
	// checkpoint, respawns every non-boss encounter and saves to disk. A boss that has been
	// beaten stays beaten — it is the point of the level, not something to re-fight for souls.
	// Returns how many encounters came back, so the caller can say so.
	//
	// EnsureLoaded first, because the Bonfire scene never loads the map itself and the boss
	// check needs it. Returns 0 when there is no map to read.
	public static int RestAtBonfire() {
		CampaignManager.RestParty();
		if (!EnsureLoaded()) return 0;

		LastBonfireId = CurrentNodeId;
		int respawned = clearedNodes.RemoveWhere(id => !IsBoss(id));

		WriteToSave();
		var save = CampaignManager.CurrentSave;
		if (save != null && CampaignManager.SaveSlot > 0)
			save.SaveToSlot(CampaignManager.SaveSlot);
		return respawned;
	}

	private static bool IsBoss(string nodeId) =>
		MapData?.GetNode(nodeId)?.encounterType == WorldEncounterType.BOSS;

	private static void WriteToSave() {
		var save = CampaignManager.CurrentSave;
		if (save == null) return;
		save.campaignFile = loadedCampaignFile;
		save.worldCurrentNode = CurrentNodeId;
		save.worldLastBonfire = LastBonfireId;
		save.worldClearedNodes = clearedNodes.ToArray();
	}

	public static string[] ListCampaignFiles() {
		var result = new List<string>();
		var dir = DirAccess.Open(CAMPAIGN_DIR);
		if (dir == null) return result.ToArray();
		dir.ListDirBegin();
		string fileName = dir.GetNext();
		while (!string.IsNullOrEmpty(fileName)) {
			if (!dir.CurrentIsDir() && fileName.EndsWith(".json"))
				result.Add(fileName);
			fileName = dir.GetNext();
		}
		dir.ListDirEnd();
		result.Sort();
		return result.ToArray();
	}
}
