using Godot;
using System.Collections.Generic;
using System.Linq;

// One step a roaming threat took, for the map to animate.
public readonly record struct RoamerMove(string id, string from, string to);

// Roaming threats (Phase 4 of the World Design Plan): an enemy group walks a fixed loop of
// plain hexes, one hex per party step, and hunts the party once it comes within CHASE_RANGE.
// Meeting it, either way round, starts its encounter. Beaten, it is gone until the next rest or
// wipe. It pays no souls (the budget is fixed) but drops one treasure card when beaten.
public static partial class WorldMapManager {
	public const int CHASE_RANGE = 2;

	// Set with PendingEncounterNodeId (the hex they met on) when the party meets a roaming
	// threat; the encounter is then the roamer's own plan, not the hex's.
	public static string PendingRoamerId { get; private set; } = "";
	public static bool HasPendingRoamer => !string.IsNullOrEmpty(PendingRoamerId);
	// What a mid-fight snapshot is keyed by.
	public static string EncounterKey => HasPendingRoamer ? PendingRoamerId : PendingEncounterNodeId;

	private static List<RoamerState> roamerStates = new List<RoamerState>();
	public static IReadOnlyList<RoamerState> Roamers => roamerStates;

	public static RoamerState GetRoamerState(string id) => roamerStates.Find(r => r.id == id);

	// A roamer is one level above its section's own encounters, to a cap of 4.
	public static int RoamerLevel(WorldRoamerData roamer) {
		var levels = MapData.nodes
			.Where(n => n.encounterType == WorldEncounterType.ENCOUNTER && n.section == roamer.section)
			.Select(n => n.level)
			.ToList();
		return Mathf.Min(4, (levels.Count > 0 ? levels.Min() : 1) + 1);
	}

	// "Roaming — Level 2" or, once it has seen the party, "Hunting — Level 2".
	public static string RoamerLine(RoamerState state) {
		WorldRoamerData roamer = MapData?.GetRoamer(state.id);
		return roamer == null ? "" : $"{(state.chasing ? "Hunting" : "Roaming")} — Level {RoamerLevel(roamer)}";
	}

	// Shown for real only while its hex is in sight; otherwise as a ghost where it was last seen.
	public static bool RoamerInSight(RoamerState state) => !state.beaten && SightOf(state.nodeId) == WorldSight.SEEN;

	public static RoamerState RoamerAt(string nodeId) => roamerStates.Find(r => RoamerInSight(r) && r.nodeId == nodeId);

	public static string RoamerGhostNode(RoamerState state) =>
		state.beaten || RoamerInSight(state) ? "" : state.lastSeenNode;

	private static void LoadRoamers(SaveGame save) {
		roamerStates.Clear();
		foreach (WorldRoamerData roamer in MapData.roamers) {
			RoamerState state = save?.roamers.FirstOrDefault(s => s != null && s.id == roamer.id);
			if (state == null || MapData.GetNode(state.nodeId) == null) state = Respawned(new RoamerState { id = roamer.id }, roamer);
			state.loopIndex = Mathf.Clamp(state.loopIndex, 0, roamer.loop.Count - 1);
			roamerStates.Add(state);
		}
	}

	private static RoamerState Respawned(RoamerState state, WorldRoamerData roamer) {
		state.nodeId = roamer.loop[0];
		state.loopIndex = 0;
		state.chasing = false;
		state.beaten = false;
		state.lastSeenNode = "";
		return state;
	}

	// A rest and a wipe both bring every roamer back to the start of its loop.
	private static void RespawnRoamers() {
		foreach (RoamerState state in roamerStates) {
			WorldRoamerData roamer = MapData?.GetRoamer(state.id);
			if (roamer != null) Respawned(state, roamer);
		}
	}

	private static void NoteRoamersSeen() {
		foreach (RoamerState state in roamerStates) {
			if (state.beaten) {
				state.lastSeenNode = "";
			} else if (seenNodes.Contains(state.nodeId)) {
				state.lastSeenNode = state.nodeId;
			} else if (!string.IsNullOrEmpty(state.lastSeenNode) && seenNodes.Contains(state.lastSeenNode)) {
				state.lastSeenNode = "";
			}
		}
	}

	// Its own fight, rolled once like every encounter, at a level above its section's. It pays
	// no souls, so it stays out of the section's budget.
	private static EncounterPlan RollRoamerPlan(WorldRoamerData roamer, RandomNumberGenerator rng) {
		var stand = new WorldNodeData {
			id = roamer.id,
			terrain = MapData.GetNode(roamer.loop[0])?.terrain ?? WorldTerrain.GRASS,
			level = RoamerLevel(roamer),
			section = roamer.section,
		};
		EncounterPlan plan = EncounterGenerator.Roll(stand, rng);
		plan.worldNode = roamer.id;
		plan.souls = 0;
		return plan;
	}

	// Called once per party step on the map, after the step. The party stepping onto a roamer
	// is contact before anyone moves; otherwise every roamer takes its step, and one that ends
	// on the party's hex is contact too. Contact sets the pending encounter and stops the rest.
	public static List<RoamerMove> TickRoamers() {
		var moves = new List<RoamerMove>();
		WorldNodeData party = MapData?.GetNode(CurrentNodeId);
		if (party == null || HasPendingRoamer) return moves;

		RoamerState met = roamerStates.Find(r => !r.beaten && r.nodeId == party.id);
		if (met != null) {
			StartRoamerEncounter(met.id);
			return moves;
		}

		foreach (RoamerState state in roamerStates) {
			if (state.beaten) continue;
			string from = state.nodeId;
			StepRoamer(state, party);
			if (state.nodeId != from) moves.Add(new RoamerMove(state.id, from, state.nodeId));
			if (state.nodeId == party.id) {
				StartRoamerEncounter(state.id);
				break;
			}
		}
		NoteRoamersSeen();
		WriteToSave();
		CampaignManager.Autosave();
		return moves;
	}

	// Fighting a roamer that stands on the party's hex.
	public static void StartRoamerEncounter(string roamerId) {
		RoamerState state = GetRoamerState(roamerId);
		if (state == null || state.beaten) return;
		PendingRoamerId = roamerId;
		PendingEncounterNodeId = state.nodeId;
		WriteToSave();
	}

	private static bool ResumeRoamerEncounter(string nodeId, string roamerId) {
		RoamerState state = GetRoamerState(roamerId);
		if (state == null || state.beaten || GetPlan(roamerId) == null || MapData.GetNode(nodeId) == null) return false;
		PendingEncounterNodeId = nodeId;
		PendingRoamerId = roamerId;
		WriteToSave();
		return true;
	}

	// Beaten: gone until the next rest, the hex not cleared, one treasure card dropped (the
	// guarded-loot tier, Merchant.LootTier) to be shown on the map.
	private static void ReportRoamerWon() {
		string hex = PendingEncounterNodeId;
		RoamerState state = GetRoamerState(PendingRoamerId);
		if (state != null) {
			state.beaten = true;
			state.chasing = false;
			state.lastSeenNode = "";
		}
		PendingRoamerId = "";
		PendingEncounterNodeId = "";
		CurrentNodeId = hex;
		Look();

		Equipment card = Merchant.DrawLoot(Merchant.LootTier(true));
		if (card != null) pendingLoot.Add(new LootResult { kind = WorldLoot.TREASURE, nodeId = hex, item = card });
		WriteToSave();
	}

	private static void StepRoamer(RoamerState state, WorldNodeData party) {
		WorldRoamerData roamer = MapData.GetRoamer(state.id);
		WorldNodeData here = MapData.GetNode(state.nodeId);
		if (roamer == null || here == null) return;

		state.chasing = WorldMapData.Distance(here, party) <= CHASE_RANGE;
		if (state.chasing) {
			MoveRoamer(state, roamer, NextToward(here, ChaseGoals(party)));
			return;
		}

		int index = roamer.loop.IndexOf(state.nodeId);
		if (index < 0) {
			MoveRoamer(state, roamer, NextToward(here, new HashSet<string>(roamer.loop)));
			return;
		}
		if (state.loopIndex < roamer.loop.Count && roamer.loop[state.loopIndex] == state.nodeId) index = state.loopIndex;
		state.loopIndex = (index + 1) % roamer.loop.Count;
		state.nodeId = roamer.loop[state.loopIndex];
	}

	private static void MoveRoamer(RoamerState state, WorldRoamerData roamer, WorldNodeData next) {
		if (next == null) return;
		state.nodeId = next.id;
		int index = roamer.loop.IndexOf(next.id);
		if (index >= 0) state.loopIndex = index;
	}

	// The party's own hex when it is plain ground, or else the plain hexes beside it: a roamer
	// never walks onto a bonfire or an encounter, so those are safe to stand on.
	private static HashSet<string> ChaseGoals(WorldNodeData party) =>
		party.IsPlain
			? new HashSet<string> { party.id }
			: MapData.GetNeighbors(party).Where(n => n.IsPlain).Select(n => n.id).ToHashSet();

	private static IEnumerable<WorldNodeData> StepsFrom(WorldNodeData node) {
		foreach (WorldNodeData neighbour in MapData.GetNeighbors(node)) yield return neighbour;
		foreach (WorldEdgeData edge in MapData.EdgesAt(node.id)) {
			WorldNodeData other = MapData.GetNode(edge.Other(node.id));
			if (other != null && !MapData.AreNeighbors(node, other)) yield return other;
		}
	}

	// The first step of the shortest walk to the nearest goal, over plain hexes and the edges
	// the party has opened (never opening one); null when already there or no way.
	private static WorldNodeData NextToward(WorldNodeData from, HashSet<string> goals) {
		if (goals.Contains(from.id)) return null;
		var previous = new Dictionary<string, WorldNodeData> { { from.id, null } };
		var queue = new Queue<WorldNodeData>();
		queue.Enqueue(from);
		while (queue.Count > 0) {
			WorldNodeData here = queue.Dequeue();
			foreach (WorldNodeData next in StepsFrom(here)) {
				if (previous.ContainsKey(next.id) || !next.IsPlain || !CanStep(here, next)) continue;
				previous[next.id] = here;
				if (!goals.Contains(next.id)) {
					queue.Enqueue(next);
					continue;
				}
				WorldNodeData first = next;
				while (previous[first.id].id != from.id) first = previous[first.id];
				return first;
			}
		}
		return null;
	}
}
