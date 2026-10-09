using Godot;
using System.Collections.Generic;
using System.Linq;

// Static campaign-run world state: which node the party is on, which encounters
// are cleared, and the last bonfire checkpoint. Mirrors itself into
// CampaignManager.CurrentSave, and autosaves whenever the party moves.
public static class WorldMapManager {
	public const string CAMPAIGN_DIR = "res://Campaigns";
	public const string DEFAULT_CAMPAIGN = "DemoCampaign.json";

	public static WorldMapData MapData { get; private set; }
	public static string CurrentNodeId { get; private set; } = "";
	public static string LastBonfireId { get; private set; } = "";

	// Set when "Start Encounter" is clicked; consumed by the encounter result reports.
	public static string PendingEncounterNodeId { get; private set; } = "";
	public static bool HasPendingEncounter => !string.IsNullOrEmpty(PendingEncounterNodeId);

	private static HashSet<string> clearedNodes = new HashSet<string>();
	private static Dictionary<string, EncounterPlan> plans = new Dictionary<string, EncounterPlan>();
	private static string loadedCampaignFile = "";

	public static void Reset() {
		MapData = null;
		CurrentNodeId = "";
		LastBonfireId = "";
		PendingEncounterNodeId = "";
		clearedNodes.Clear();
		plans.Clear();
		loadedCampaignFile = "";
	}

	// Loads the campaign map of the current save (generated and kept in it, or a file, or the
	// default) and restores world state from the save. Safe to call every time the WorldMap
	// scene opens; reloads only when the campaign changes. A generated map has no file name,
	// but switching saves resets this first (CampaignManager), so it is never stale.
	public static bool EnsureLoaded() {
		var save = CampaignManager.CurrentSave;
		bool generated = !string.IsNullOrEmpty(save?.campaignMap);
		string file = generated ? "" : save != null && !string.IsNullOrEmpty(save.campaignFile) ? save.campaignFile : DEFAULT_CAMPAIGN;
		if (MapData != null && loadedCampaignFile == file) return true;

		MapData = generated
			? WorldMapData.Parse(save.campaignMap, "generated campaign")
			: WorldMapData.LoadFromFile($"{CAMPAIGN_DIR}/{file}");
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

		bool rolled = LoadPlans(save);
		WriteToSave();
		// Straight to disk, so quitting cannot re-roll them.
		if (rolled) CampaignManager.Autosave();
		return true;
	}

	// Loading a save made in an encounter: back into it, unless it can no longer be fought.
	public static bool ResumeEncounter(string nodeId) {
		if (!EnsureLoaded() || GetPlan(nodeId) == null || IsCleared(nodeId)) return false;
		PendingEncounterNodeId = nodeId;
		WriteToSave();
		return true;
	}

	// Plans come from the save. Any encounter or boss without one (a new campaign, or a save
	// from before encounters were fixed) is rolled now and kept from then on. Until bosses
	// exist, a boss is a stand-in: an encounter rolled at the boss node's level.
	private static bool LoadPlans(SaveGame save) {
		plans.Clear();
		foreach (EncounterPlan plan in save?.encounterPlans ?? new EncounterPlan[0]) {
			if (plan != null && MapData.GetNode(plan.worldNode) != null) plans[plan.worldNode] = plan;
		}

		var rng = new RandomNumberGenerator();
		rng.Randomize();
		bool rolled = false;
		foreach (WorldNodeData node in MapData.nodes) {
			if (!IsFight(node) || plans.ContainsKey(node.id)) continue;
			plans[node.id] = EncounterGenerator.Roll(node, rng);
			rolled = true;
		}
		return AssignSouls() || rolled;
	}

	// Each section's budget (SoulEconomy) is split over its encounters by tiered health, so a
	// tougher fight pays more and the section adds up exactly. Done once, when they are rolled;
	// a section with an encounter that has no share yet is split again as a whole.
	private static bool AssignSouls() {
		bool assigned = false;
		for (int section = 1; section <= MapData.sections; section++) {
			var inSection = MapData.nodes
				.Where(n => n.encounterType == WorldEncounterType.ENCOUNTER && n.section == section)
				.Select(n => plans[n.id])
				.ToList();
			if (!inSection.Any(p => p.souls < 0)) continue;

			int[] shares = SoulEconomy.Split(SoulEconomy.SectionBudget(section, MapData.sections),
				inSection.Select(p => p.TotalHealth).ToArray());
			for (int i = 0; i < inSection.Count; i++) inSection[i].souls = shares[i];
			assigned = true;
		}
		return assigned;
	}

	private static int PartySize => CampaignManager.Players.Length;

	// Everything this node pays the party, all at once on its first win: an encounter's share
	// of its section, or a boss's bonus. 0 for a node with nothing to pay (a map without
	// sections, the last main boss).
	public static int SoulsPaidBy(string nodeId) {
		WorldNodeData node = MapData?.GetNode(nodeId);
		if (node == null) return 0;
		if (node.encounterType == WorldEncounterType.BOSS) {
			return SoulEconomy.BossSouls(node.boss, node.section, MapData.sections) * PartySize;
		}
		return Mathf.Max(0, GetPlan(nodeId)?.souls ?? 0) * PartySize;
	}

	// Party souls still waiting on the map at this node: a fresh encounter's whole payout, a
	// won one's nothing until a lost pile gives some back. Bosses pay once, when beaten.
	public static int SoulsOnMap(string nodeId) {
		WorldNodeData node = MapData?.GetNode(nodeId);
		if (node == null) return 0;
		if (node.encounterType == WorldEncounterType.BOSS) return IsCleared(nodeId) ? 0 : SoulsPaidBy(nodeId);
		EncounterPlan plan = GetPlan(nodeId);
		return plan == null ? 0 : Mathf.Max(0, SoulsPaidBy(nodeId) - plan.soulsClaimed);
	}

	// The highest level the Firekeeper allows now: it climbs with every section whose boss (a
	// mini or main boss; megas are optional) is beaten. A map without such a boss in every
	// section, like the Demo Campaign, has no cap.
	public static int LevelCap() {
		var progress = SectionProgress();
		return progress == null ? SoulEconomy.MAX_LEVEL : SoulEconomy.LevelCap(progress.Value.cleared, progress.Value.sections);
	}

	// How far through its sections the campaign is, 0 to 1, by the same bosses; 1 for a map
	// without them. The Merchant's rarer cards come in with it.
	public static float Progress() {
		var progress = SectionProgress();
		return progress == null ? 1f : progress.Value.cleared / (float)progress.Value.sections;
	}

	// Sections whose boss (mini or main) is beaten, of how many; null for a map without such a
	// boss in every section, which has nothing to gate on.
	private static (int cleared, int sections)? SectionProgress() {
		if (!EnsureLoaded() || MapData.sections <= 0) return null;
		var gates = MapData.nodes.Where(n => n.encounterType == WorldEncounterType.BOSS && n.boss != WorldBossKind.MEGA).ToList();
		if (gates.Select(n => n.section).Distinct().Count() < MapData.sections) return null;
		return (gates.Count(n => IsCleared(n.id)), MapData.sections);
	}

	// A win takes whatever this node still has on the map. Called before ReportEncounterWon,
	// so a boss is not yet cleared.
	public static int ClaimSouls(string nodeId) {
		int souls = SoulsOnMap(nodeId);
		EncounterPlan plan = GetPlan(nodeId);
		if (plan != null && !IsBoss(nodeId)) plan.soulsClaimed += souls;
		if (souls > 0) Merchant.NoteWin();
		return souls;
	}

	// A lost pile's souls go back to the encounter that paid them, lighting it again. A boss
	// stays dead, so its souls (and any with no source) are gone.
	public static void ReturnSouls(string nodeId, int amount) {
		if (!EnsureLoaded() || IsBoss(nodeId)) return;
		EncounterPlan plan = GetPlan(nodeId);
		if (plan != null) plan.soulsClaimed = Mathf.Max(0, plan.soulsClaimed - amount);
	}

	public static EncounterPlan GetPlan(string nodeId) =>
		!string.IsNullOrEmpty(nodeId) && plans.TryGetValue(nodeId, out var plan) ? plan : null;

	public static EncounterPlan GetPendingPlan() => GetPlan(PendingEncounterNodeId);

	public static bool IsCleared(string nodeId) => clearedNodes.Contains(nodeId);

	public static void SetCurrentNode(string nodeId) {
		CurrentNodeId = nodeId;
		WriteToSave();
		CampaignManager.Autosave();
	}

	public static void StartEncounter(string nodeId) {
		PendingEncounterNodeId = nodeId;
		WriteToSave();
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

	// Party wipe: back to the last bonfire, every encounter respawns. A beaten boss stays
	// beaten, as it does on a rest; it pays its souls once.
	public static void ReportPartyDeath() {
		PendingEncounterNodeId = "";
		clearedNodes.RemoveWhere(id => !IsBoss(id));
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
		Merchant.OnRest();

		WriteToSave();
		CampaignManager.Autosave();
		return respawned;
	}

	public static bool IsFight(WorldNodeData node) =>
		node.encounterType == WorldEncounterType.ENCOUNTER || node.encounterType == WorldEncounterType.BOSS;

	private static bool IsBoss(string nodeId) =>
		MapData?.GetNode(nodeId)?.encounterType == WorldEncounterType.BOSS;

	private static void WriteToSave() {
		var save = CampaignManager.CurrentSave;
		if (save == null) return;
		save.campaignFile = loadedCampaignFile;
		save.worldCurrentNode = CurrentNodeId;
		save.worldLastBonfire = LastBonfireId;
		save.worldClearedNodes = clearedNodes.ToArray();
		save.encounterNode = PendingEncounterNodeId;
		save.encounterPlans = plans.Values.ToArray();
	}

	// A campaign file's name as shown: "DemoCampaign.json" reads "Demo Campaign".
	public static string CampaignTitle(string file) =>
		System.Text.RegularExpressions.Regex.Replace(System.IO.Path.GetFileNameWithoutExtension(file), "(?<=[a-z])(?=[A-Z])", " ");

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
