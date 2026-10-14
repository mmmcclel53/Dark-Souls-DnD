using Godot;
using System.Collections.Generic;
using System.Linq;

// Static campaign-run world state: which node the party is on, which encounters
// are cleared, and the last bonfire checkpoint. Mirrors itself into
// CampaignManager.CurrentSave, and autosaves whenever the party moves.
public static partial class WorldMapManager {
	public const string CAMPAIGN_DIR = "res://Campaigns";
	public const string DEFAULT_CAMPAIGN = "DemoCampaign.json";

	public static WorldMapData MapData { get; private set; }
	public static string CurrentNodeId { get; private set; } = "";
	public static string LastBonfireId { get; private set; } = "";

	// Set when "Start Encounter" is clicked; consumed by the encounter result reports.
	public static string PendingEncounterNodeId { get; private set; } = "";
	public static bool HasPendingEncounter => !string.IsNullOrEmpty(PendingEncounterNodeId);

	private static HashSet<string> clearedNodes = new HashSet<string>();
	// Fog of war. Known hexes were seen once and stay on the map; glimpsed ones only show their
	// edge. Seen is what is in sight from where the party stands now, so it is never saved.
	private static HashSet<string> knownNodes = new HashSet<string>();
	private static HashSet<string> glimpsedNodes = new HashSet<string>();
	private static HashSet<string> seenNodes = new HashSet<string>();
	// Debug: the whole map as seen. Not saved.
	public static bool revealAll;
	// Map loot already picked up, for good: through rests and wipes. Results waiting to be shown
	// on the map are transient.
	private static HashSet<string> lootTaken = new HashSet<string>();
	// Ladders, trees and doors opened, for good: through rests and wipes.
	private static HashSet<string> openEdges = new HashSet<string>();
	public static readonly List<LootResult> pendingLoot = new List<LootResult>();
	private static Dictionary<string, EncounterPlan> plans = new Dictionary<string, EncounterPlan>();
	private static string loadedCampaignFile = "";

	public static void Reset() {
		MapData = null;
		CurrentNodeId = "";
		LastBonfireId = "";
		PendingEncounterNodeId = "";
		PendingRoamerId = "";
		roamerStates.Clear();
		clearedNodes.Clear();
		knownNodes.Clear();
		glimpsedNodes.Clear();
		seenNodes.Clear();
		lootTaken.Clear();
		openEdges.Clear();
		pendingLoot.Clear();
		plans.Clear();
		loadedCampaignFile = "";
	}

	public static WorldSight SightOf(string id) {
		if (revealAll || seenNodes.Contains(id)) return WorldSight.SEEN;
		if (knownNodes.Contains(id)) return WorldSight.KNOWN;
		return glimpsedNodes.Contains(id) ? WorldSight.GLIMPSED : WorldSight.UNKNOWN;
	}

	// Hexes away at which a landmark still shows through the fog.
	public const int LANDMARK_RANGE = 6;

	// A tower or a castle gives a wider view: one hex more on every step of the sight rule.
	public static int VantageBonus(WorldNodeData node) =>
		node != null && (node.structure == WorldStructure.TOWER || node.structure == WorldStructure.CASTLE) ? 1 : 0;

	// An unexplored hex whose tall structure stands out of the fog, close enough to the party.
	public static bool LandmarkShows(WorldNodeData node) {
		if (MapData == null || node == null || !StructureArt.IsLandmark(node.structure) || SightOf(node.id) != WorldSight.UNKNOWN) return false;
		WorldNodeData here = MapData.GetNode(CurrentNodeId);
		return here != null && WorldMapData.Distance(node, here) <= LANDMARK_RANGE;
	}

	// The sight rule: from a hex at elevation e, a hex diff higher or lower is seen out to
	// 1 - diff hexes (one on the level, one more per step downhill), its edge is glimpsed from
	// next door when one step up, and two or more up is hidden. The party's own hex is always seen.
	// Standing on a tower or castle (VantageBonus) widens every one of those ranges by a hex.
	public static void Look() {
		seenNodes.Clear();
		WorldNodeData here = MapData?.GetNode(CurrentNodeId);
		if (here == null) return;
		int vantage = VantageBonus(here);
		foreach (WorldNodeData n in MapData.nodes) {
			int diff = n.elevation - here.elevation;
			int distance = WorldMapData.Distance(n, here);
			if (n.id == here.id || (diff <= 0 && distance <= 1 + vantage - diff)) {
				seenNodes.Add(n.id);
				knownNodes.Add(n.id);
				glimpsedNodes.Remove(n.id);
			} else if (diff == 1 && distance <= 1 + vantage && !knownNodes.Contains(n.id)) {
				glimpsedNodes.Add(n.id);
			}
		}
		// A tree standing across a gap shows the far bank, so there is always somewhere to fell it to.
		foreach (WorldEdgeData edge in MapData.EdgesAt(here.id)) {
			if (edge.kind != WorldEdgeKind.TREE) continue;
			string far = edge.Other(here.id);
			seenNodes.Add(far);
			knownNodes.Add(far);
			glimpsedNodes.Remove(far);
		}
		NoteRoamersSeen();
	}

	public static bool IsOpen(WorldEdgeData edge) => openEdges.Contains(edge.Id);

	// The one movement rule (WorldMapData.CanStep) with this run's open edges.
	public static bool CanStep(WorldNodeData from, WorldNodeData to) =>
		MapData != null && MapData.CanStep(from, to, IsOpen);

	// The first closed edge at a hex that the party could open from there. A shortcut opens from
	// its far end, b (a ladder from its high end, which is b), so the party has walked the long
	// way first; a keyed door also opens from the near side with its key.
	public static WorldEdgeData OpenableFrom(string nodeId) {
		if (MapData == null) return null;
		WorldNodeData here = MapData.GetNode(nodeId);
		foreach (WorldEdgeData edge in MapData.EdgesAt(nodeId)) {
			if (IsOpen(edge)) continue;
			if (edge.b == nodeId) {
				if (edge.kind != WorldEdgeKind.LADDER || here.elevation > MapData.GetNode(edge.a).elevation) return edge;
			} else if (edge.kind == WorldEdgeKind.DOOR && !string.IsNullOrEmpty(edge.keyId) && HasKey(edge.keyId)) {
				return edge;
			}
		}
		return null;
	}

	// What pressing the action button at this hex does to the edge.
	public static string VerbFor(WorldEdgeData edge, string nodeId) => edge.kind switch {
		WorldEdgeKind.LADDER => "Kick Down Ladder",
		WorldEdgeKind.TREE => "Fell Tree",
		_ => edge.b == nodeId ? "Open Door" : "Unlock Door",
	};

	// Opening is permanent. The sight is worked out again, since a felled tree shows the far bank.
	public static void OpenEdge(WorldEdgeData edge) {
		if (edge == null || !openEdges.Add(edge.Id)) return;
		Look();
		WriteToSave();
		CampaignManager.Autosave();
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
		knownNodes = new HashSet<string>(save?.worldKnownNodes ?? new string[0]);
		glimpsedNodes = new HashSet<string>(save?.worldGlimpsedNodes ?? new string[0]);
		lootTaken = new HashSet<string>(save?.worldLootTaken ?? new string[0]);
		openEdges = new HashSet<string>(save?.worldOpenEdges ?? new string[0]);
		foreach (var (before, after) in MapData.NormaliseEdges())
			if (openEdges.Remove(before)) openEdges.Add(after);
		pendingLoot.Clear();
		QueueBossChoice();
		CurrentNodeId = save?.worldCurrentNode ?? "";
		LastBonfireId = save?.worldLastBonfire ?? "";
		PendingEncounterNodeId = "";
		PendingRoamerId = "";

		var start = MapData.GetStartNode();
		if (MapData.GetNode(CurrentNodeId) == null)
			CurrentNodeId = start?.id ?? "";
		if (MapData.GetNode(LastBonfireId) == null)
			LastBonfireId = CurrentNodeId;
		LoadRoamers(save);
		Look();

		bool rolled = LoadPlans(save);
		WriteToSave();
		// Straight to disk, so quitting cannot re-roll them.
		if (rolled) CampaignManager.Autosave();
		return true;
	}

	// Loading a save made in an encounter: back into it, unless it can no longer be fought.
	public static bool ResumeEncounter(string nodeId, string roamerId = "") {
		if (!EnsureLoaded()) return false;
		if (!string.IsNullOrEmpty(roamerId)) return ResumeRoamerEncounter(nodeId, roamerId);
		if (GetPlan(nodeId) == null || IsCleared(nodeId)) return false;
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
			if (plan != null && (MapData.GetNode(plan.worldNode) != null || MapData.GetRoamer(plan.worldNode) != null)) plans[plan.worldNode] = plan;
		}

		var rng = new RandomNumberGenerator();
		rng.Randomize();
		bool rolled = false;
		foreach (WorldNodeData node in MapData.nodes) {
			if (!IsFight(node) || plans.ContainsKey(node.id)) continue;
			plans[node.id] = EncounterGenerator.Roll(node, rng);
			rolled = true;
		}
		foreach (WorldNodeData node in MapData.nodes) {
			if (!IsSoulsHex(node) || plans.ContainsKey(node.id)) continue;
			plans[node.id] = new EncounterPlan { worldNode = node.id };
			rolled = true;
		}
		rolled |= AssignBosses(rng);
		foreach (WorldRoamerData roamer in MapData.roamers) {
			if (plans.ContainsKey(roamer.id)) continue;
			plans[roamer.id] = RollRoamerPlan(roamer, rng);
			rolled = true;
		}
		return AssignSouls() || rolled;
	}

	// Every boss node without a boss gets one from the roster, of its tier and not used yet.
	private static bool AssignBosses(RandomNumberGenerator rng) {
		var nodes = MapData.nodes.Where(n => n.encounterType == WorldEncounterType.BOSS && string.IsNullOrEmpty(plans[n.id].boss)).ToList();
		if (nodes.Count == 0) return false;
		var taken = new HashSet<string>(plans.Values.Select(p => p.boss).Where(b => !string.IsNullOrEmpty(b)));
		var picks = BossRoster.Assign(nodes, MapData.sections, taken, rng);
		foreach (var (nodeId, boss) in picks) plans[nodeId].boss = boss;
		return picks.Count > 0;
	}

	public static BossData BossAt(string nodeId) => BossRoster.Get(GetPlan(nodeId)?.boss ?? "");

	// Each section's budget (SoulEconomy) is split over its encounters by tiered health, so a
	// tougher fight pays more and the section adds up exactly. Done once, when they are rolled;
	// a section with an encounter that has no share yet is split again as a whole.
	private static bool AssignSouls() {
		bool assigned = false;
		for (int section = 1; section <= MapData.sections; section++) {
			var inSection = MapData.nodes
				.Where(n => (n.encounterType == WorldEncounterType.ENCOUNTER || IsSoulsHex(n)) && n.section == section)
				.Select(n => plans[n.id])
				.ToList();
			if (!inSection.Any(p => p.souls < 0)) continue;

			// A souls hex has no enemies to weigh: it counts as the section's lightest fight.
			var fights = inSection.Where(p => p.spawns.Count > 0).Select(p => p.TotalHealth).ToList();
			int lightest = Mathf.Max(1, fights.Count > 0 ? fights.Min() : 1);
			int[] shares = SoulEconomy.Split(SoulEconomy.SectionBudget(section, MapData.sections),
				inSection.Select(p => p.spawns.Count > 0 ? p.TotalHealth : lightest).ToArray());
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
	// without them. The Merchant's Uncommon cards and guarded loot's tier come in with it.
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
		if (HasPendingRoamer) return 0;
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

	public static EncounterPlan GetPendingPlan() => GetPlan(EncounterKey);

	public static bool IsCleared(string nodeId) => clearedNodes.Contains(nodeId);

	public static void SetCurrentNode(string nodeId) {
		CurrentNodeId = nodeId;
		Look();
		WriteToSave();
		WorldNodeData node = MapData?.GetNode(nodeId);
		// A pile dropped by a wipe against a roaming threat lies on plain ground, where no
		// encounter will ever put it back in the cache: walking onto it does.
		if (node != null && !IsFight(node) && SoulCache.droppedWorldNode == nodeId) SoulCache.Retrieve();
		if (node != null && HasLoot(nodeId) && (!IsFight(node) || IsCleared(nodeId))) TakeLoot(nodeId);
		else CampaignManager.Autosave();
	}

	public static bool HasLoot(string nodeId) {
		WorldNodeData node = MapData?.GetNode(nodeId);
		return node != null && node.loot != WorldLoot.NONE && !lootTaken.Contains(nodeId)
			&& !(node.loot == WorldLoot.SOULS && IsFight(node));
	}

	// Loot on a hex with an encounter is guarded: it is taken on the win, not on arriving.
	// (Souls on one are meaningless, the encounter pays already, so they do not count as loot.)
	public static bool IsGuarded(string nodeId) {
		WorldNodeData node = MapData?.GetNode(nodeId);
		return node != null && HasLoot(nodeId) && IsFight(node);
	}

	// The map's one line about loot: that there is some, never what.
	public static string LootLine(string nodeId) => !HasLoot(nodeId) ? "" : IsGuarded(nodeId) ? "Guarded loot" : "Loot";

	private static bool IsSoulsHex(WorldNodeData node) => node.loot == WorldLoot.SOULS && !IsFight(node);

	// Picks up a hex's loot, once per node ever. The result waits in pendingLoot for the map to
	// show. The caller saves to disk when it is about to anyway (the end of an encounter).
	public static LootResult TakeLoot(string nodeId, bool autosave = true) {
		if (!HasLoot(nodeId)) return null;
		WorldNodeData node = MapData.GetNode(nodeId);
		bool guarded = IsFight(node);
		var result = new LootResult { kind = node.loot, nodeId = nodeId };
		switch (node.loot) {
			case WorldLoot.ESTUS_SHARD:
				PartyEstus.AddShard();
				result.count = PartyEstus.Shards;
				break;
			case WorldLoot.TREASURE:
				result.item = Merchant.DrawLoot(Merchant.LootTier(guarded));
				break;
			case WorldLoot.SOULS:
				result.souls = ClaimSouls(nodeId);
				SoulCache.Award(result.souls, nodeId);
				break;
			case WorldLoot.KEY:
				AddKey(node.keyId);
				result.keyId = node.keyId;
				break;
			case WorldLoot.HOMEWARD_BONE:
				HomewardBone.Add();
				result.count = HomewardBone.Count;
				break;
		}
		lootTaken.Add(nodeId);
		pendingLoot.Add(result);
		WriteToSave();
		if (autosave) CampaignManager.Autosave();
		return result;
	}

	public static bool HasKey(string keyId) =>
		(CampaignManager.CurrentSave?.keys ?? new string[0]).Contains(keyId);

	private static void AddKey(string keyId) {
		var save = CampaignManager.CurrentSave;
		if (save == null || string.IsNullOrEmpty(keyId) || HasKey(keyId)) return;
		save.keys = save.keys.Append(keyId).ToArray();
	}

	public static void StartEncounter(string nodeId) {
		PendingEncounterNodeId = nodeId;
		PendingRoamerId = "";
		WriteToSave();
	}

	public static WorldNodeData GetPendingEncounterNode() => MapData?.GetNode(PendingEncounterNodeId);

	// Encounter won: mark the node cleared and advance the party onto it.
	public static void ReportEncounterWon() {
		if (!HasPendingEncounter) return;
		if (HasPendingRoamer) {
			ReportRoamerWon();
			return;
		}
		string won = PendingEncounterNodeId;
		clearedNodes.Add(won);
		CurrentNodeId = won;
		PendingEncounterNodeId = "";
		Look();
		TakeLoot(won, false);
		if (IsBoss(won)) PayBoss(won);
		WriteToSave();
	}

	// A beaten boss's treasure (BossRoster): its cards wait in the save for the party to take
	// one, and the floor card, when owed, is drawn and minted at once.
	private static void PayBoss(string nodeId) {
		var save = CampaignManager.CurrentSave;
		WorldNodeData node = MapData.GetNode(nodeId);
		BossData boss = BossAt(nodeId);
		if (save == null || node == null) return;
		string[] cards = BossRoster.Choices(boss);
		if (cards.Length > 0) {
			save.bossChoice = cards;
			save.bossChoiceNode = nodeId;
			QueueBossChoice();
		}
		if (!BossRoster.OwesFloorCard(boss, node.boss)) return;
		Equipment floor = Merchant.DrawLoot(BossRoster.Floor(node.boss));
		if (floor != null) pendingLoot.Add(new LootResult { kind = WorldLoot.TREASURE, nodeId = nodeId, item = floor });
	}

	private static void QueueBossChoice() {
		var save = CampaignManager.CurrentSave;
		if (save == null || save.bossChoice.Length == 0 || pendingLoot.Any(r => r.options.Length > 0)) return;
		pendingLoot.Insert(0, new LootResult { kind = WorldLoot.TREASURE, nodeId = save.bossChoiceNode, options = save.bossChoice });
	}

	// The party's pick of a beaten boss's cards, minted into the pool like any treasure.
	public static Equipment TakeBossCard(string name) {
		var save = CampaignManager.CurrentSave;
		if (save == null || !save.bossChoice.Contains(name)) return null;
		save.bossChoice = new string[0];
		save.bossChoiceNode = "";
		Equipment card = GameManager.MintInstance(name);
		CampaignManager.Autosave();
		return card;
	}

	// Homeward Bone: the party leaves the encounter for the last bonfire. Nothing is won or lost:
	// the fight stays as it was (uncleared, its plan kept), nothing respawns and no souls drop.
	public static void ReportHomewardBone() {
		PendingEncounterNodeId = "";
		PendingRoamerId = "";
		if (!string.IsNullOrEmpty(LastBonfireId))
			CurrentNodeId = LastBonfireId;
		Look();
		WriteToSave();
	}

	// Party wipe: back to the last bonfire, every encounter respawns. A beaten boss stays
	// beaten, as it does on a rest; it pays its souls once.
	public static void ReportPartyDeath() {
		PendingEncounterNodeId = "";
		PendingRoamerId = "";
		RespawnRoamers();
		clearedNodes.RemoveWhere(id => !IsBoss(id));
		if (!string.IsNullOrEmpty(LastBonfireId))
			CurrentNodeId = LastBonfireId;
		Look();
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
		RespawnRoamers();
		Merchant.OnRest();
		Look();

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
		save.worldKnownNodes = knownNodes.ToArray();
		save.worldGlimpsedNodes = glimpsedNodes.ToArray();
		save.worldLootTaken = lootTaken.ToArray();
		save.worldOpenEdges = openEdges.ToArray();
		save.encounterNode = PendingEncounterNodeId;
		save.encounterRoamer = PendingRoamerId;
		save.roamers = new Godot.Collections.Array<RoamerState>(roamerStates);
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

// What a hex's loot gave, waiting for the map to show it (WorldMapLootPanel).
public class LootResult {
	public WorldLoot kind;
	public string nodeId = "";
	// The card drawn from the treasure deck, minted into the party's pool; null if the deck was empty.
	public Equipment item;
	// Party souls paid by a souls hex.
	public int souls;
	public string keyId = "";
	// How many shards, or Homeward Bones, the party holds after one was picked up.
	public int count;
	// A beaten boss's cards to choose one from (BossRoster); item is unset until chosen.
	public string[] options = new string[0];
}
