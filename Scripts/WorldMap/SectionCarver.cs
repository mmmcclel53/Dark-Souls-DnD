using Godot;
using System.Collections.Generic;
using System.Linq;

// An edge of a section being laid out, between hexes; Build turns it into a WorldEdgeData.
public class HexEdge {
	public Vector2I a;
	public Vector2I b;
	public WorldEdgeKind kind;
	public bool open;
	public bool keyed;

	public bool Joins(Vector2I x, Vector2I y) => (a == x && b == y) || (a == y && b == x);
}

// One section's ground while the generator shapes it: its hexes, their heights, the chasms and
// ridges carved into it and the edges placed across it. Steps follow WorldMapData.CanStep with
// every edge closed unless its `open` flag says otherwise, so the generator can ask what the
// party could walk before and after a shortcut is opened. Hexes are indexed so a walk is a
// pass over plain arrays.
public class SectionTerrain {
	public const int NEVER = 99;

	public readonly Vector2I entry;
	public readonly List<Vector2I> hexes;
	public readonly HashSet<Vector2I> chasms = new HashSet<Vector2I>();
	// Every raised hex of a ridge, its ramp included.
	public readonly HashSet<Vector2I> ridges = new HashSet<Vector2I>();
	public readonly List<HexEdge> edges = new List<HexEdge>();

	private readonly Dictionary<Vector2I, int> index = new Dictionary<Vector2I, int>();
	private readonly int[][] around;
	private readonly int[] baseElevation;
	private readonly int[] height;
	private readonly bool[] chasm;
	private readonly int[] queue;

	public SectionTerrain(List<Vector2I> hexes, Vector2I entry) {
		this.hexes = hexes;
		this.entry = entry;
		for (int i = 0; i < hexes.Count; i++) index[hexes[i]] = i;
		around = new int[hexes.Count][];
		for (int i = 0; i < hexes.Count; i++)
			around[i] = CampaignGenerator.Neighbours(hexes[i]).Where(index.ContainsKey).Select(n => index[n]).ToArray();
		baseElevation = new int[hexes.Count];
		height = new int[hexes.Count];
		chasm = new bool[hexes.Count];
		queue = new int[hexes.Count];
	}

	public bool Walkable(Vector2I hex) => index.TryGetValue(hex, out int i) && !chasm[i];

	public IEnumerable<Vector2I> Land => hexes.Where(Walkable);

	public int LandCount => hexes.Count - chasms.Count;

	public int ElevationAt(Vector2I hex) => height[index[hex]];

	// Heights run from the biome's own, give or take a step, but never more than one a hex away
	// from the entry's, so any two neighbours are within a step of each other and the section
	// joins the last one's height without cascading through it.
	public void SetElevations(int anchor, int biomeHeight, RandomNumberGenerator rng) {
		for (int i = 0; i < hexes.Count; i++) {
			int d = CampaignGenerator.Distance(hexes[i], entry);
			baseElevation[i] = Mathf.Clamp(biomeHeight + rng.RandiRange(0, 1), anchor - d, anchor + d);
		}
		Reset();
	}

	public void Reset() {
		chasms.Clear();
		ridges.Clear();
		edges.Clear();
		baseElevation.CopyTo(height, 0);
		System.Array.Clear(chasm);
	}

	public void Raise(Vector2I hex, int by) {
		height[index[hex]] += by;
		ridges.Add(hex);
	}

	public void SetChasm(Vector2I hex, bool on) {
		int i = index[hex];
		chasm[i] = on;
		if (on) {
			chasms.Add(hex);
			height[i] = 0;
		} else {
			chasms.Remove(hex);
		}
	}

	public void SetElevation(Vector2I hex, int value) => height[index[hex]] = value;

	public HexEdge EdgeBetween(Vector2I x, Vector2I y) {
		foreach (HexEdge edge in edges)
			if (edge.Joins(x, y)) return edge;
		return null;
	}

	private bool Step(int from, int to) {
		if (chasm[from] || chasm[to]) return false;
		bool adjacent = true;
		HexEdge edge = null;
		if (edges.Count > 0) {
			edge = EdgeBetween(hexes[from], hexes[to]);
			adjacent = CampaignGenerator.Distance(hexes[from], hexes[to]) == 1;
		}
		if (!adjacent && !(edge != null && edge.kind == WorldEdgeKind.TREE && edge.open)) return false;
		if (height[to] - height[from] > 1 && !(edge != null && edge.kind == WorldEdgeKind.LADDER && edge.open)) return false;
		return !(edge != null && edge.kind == WorldEdgeKind.DOOR && !edge.open);
	}

	// Steps from `start` to every hex it can reach (NEVER where it cannot); with reverse, steps
	// from every hex that can reach `start`. Indexed like `hexes`.
	public int[] Walk(Vector2I start, bool reverse = false) {
		var dist = new int[hexes.Count];
		System.Array.Fill(dist, NEVER);
		int head = 0, tail = 0, s = index[start];
		dist[s] = 0;
		queue[tail++] = s;
		while (head < tail) {
			int cur = queue[head++];
			foreach (int next in around[cur]) {
				if (dist[next] != NEVER || !(reverse ? Step(next, cur) : Step(cur, next))) continue;
				dist[next] = dist[cur] + 1;
				queue[tail++] = next;
			}
			foreach (HexEdge edge in edges) {
				if (edge.kind != WorldEdgeKind.TREE || !edge.open) continue;
				int other = index[edge.a] == cur ? index[edge.b] : index[edge.b] == cur ? index[edge.a] : -1;
				if (other < 0 || dist[other] != NEVER || !(reverse ? Step(other, cur) : Step(cur, other))) continue;
				dist[other] = dist[cur] + 1;
				queue[tail++] = other;
			}
		}
		return dist;
	}

	public int Dist(int[] walk, Vector2I hex) => walk[index[hex]];

	public bool Reached(int[] walk, Vector2I hex) => walk[index[hex]] != NEVER;

	// Every hex can be reached from the entry and can get back to it, so no drop or door ever
	// strands the party.
	public bool StronglyConnected() {
		int land = LandCount;
		return Count(Walk(entry)) == land && Count(Walk(entry, true)) == land;
	}

	private static int Count(int[] walk) {
		int n = 0;
		foreach (int d in walk) if (d != NEVER) n++;
		return n;
	}
}

// Winding sections (Phase 3a of Docs/World Design Plan.md): chasms and ridges are carved into a
// section until the walk from its bonfire to the Fog Gate is long, and then a ladder, tree or
// door is placed where it saves the most walking.
public static class SectionCarver {
	private const int CARVE_ATTEMPTS = 12;
	private const int PROPOSALS = 32;
	public const int MIN_WALK = 6;
	private const float CHASM_SHARE = 0.45f;
	public const int MIN_SAVING = 3;
	public const int FIRST_FEATURES = 2;
	private const int MIN_PAIR_WALK = 4;
	private const int NEAR_ENTRY = 3;
	private const float DOOR_LIFT = 2f;

	private class Feature {
		public bool ridge;
		public List<Vector2I> hexes = new List<Vector2I>();
	}

	// Carves one feature at a time, each the best of a batch of proposals for lengthening the
	// walk to the gate that pickGate would choose, until it is at least max(6, 2 × the hex
	// distance) or the attempts are spent. A feature is only kept if the section stays whole.
	// The first `forced` features are carved whatever the walk, so there is ground for an edge
	// to cross.
	public static void Carve(SectionTerrain t, int minLand, int forced, System.Func<int[], Vector2I?> pickGate, RandomNumberGenerator rng) {
		for (int attempt = 0; attempt < CARVE_ATTEMPTS; attempt++) {
			var walk = t.Walk(t.entry);
			Vector2I? gate = pickGate(walk);
			if (gate == null) return;
			int current = t.Dist(walk, gate.Value);
			bool longEnough = current >= Mathf.Max(MIN_WALK, 2 * CampaignGenerator.Distance(t.entry, gate.Value));
			if (longEnough && attempt >= forced) return;

			Feature best = null;
			int bestWalk = attempt < forced ? current - 1 : current;
			for (int i = 0; i < PROPOSALS; i++) {
				Feature feature = Propose(t, rng);
				if (feature == null) continue;
				System.Action undo = Apply(t, feature);
				int w = WalkAfter(t, minLand, pickGate);
				undo();
				if (w > bestWalk || (w == bestWalk && best != null && w >= 0 && rng.Randf() < 0.3f)) {
					best = feature;
					bestWalk = w;
				}
			}
			if (best != null) Apply(t, best);
		}
	}

	private static int WalkAfter(SectionTerrain t, int minLand, System.Func<int[], Vector2I?> pickGate) {
		if (t.LandCount < minLand || !t.StronglyConnected()) return -1;
		var walk = t.Walk(t.entry);
		Vector2I? gate = pickGate(walk);
		return gate == null ? -1 : t.Dist(walk, gate.Value);
	}

	// A chasm of one to three hexes in a run, or a ridge: a run of two to four hexes whose first
	// is a ramp one step up and the rest two up, so they cannot be climbed onto from beside.
	private static Feature Propose(SectionTerrain t, RandomNumberGenerator rng) {
		var free = t.Land.Where(h => h != t.entry && !t.ridges.Contains(h)).ToList();
		if (free.Count == 0) return null;
		var feature = new Feature { ridge = rng.Randf() >= CHASM_SHARE };
		feature.hexes.Add(free[rng.RandiRange(0, free.Count - 1)]);

		int length = feature.ridge ? rng.RandiRange(2, 4) : rng.RandiRange(1, 3);
		while (feature.hexes.Count < length) {
			var next = CampaignGenerator.Neighbours(feature.hexes[^1])
				.Where(h => free.Contains(h) && !feature.hexes.Contains(h)).ToList();
			if (next.Count == 0) break;
			feature.hexes.Add(next[rng.RandiRange(0, next.Count - 1)]);
		}
		return !feature.ridge || feature.hexes.Count >= 2 ? feature : null;
	}

	private static System.Action Apply(SectionTerrain t, Feature feature) {
		var before = feature.hexes.ToDictionary(h => h, h => t.ElevationAt(h));
		for (int i = 0; i < feature.hexes.Count; i++) {
			if (feature.ridge) t.Raise(feature.hexes[i], i == 0 ? 1 : 2);
			else t.SetChasm(feature.hexes[i], true);
		}
		return () => {
			foreach (var (hex, e) in before) {
				t.SetChasm(hex, false);
				t.SetElevation(hex, e);
				t.ridges.Remove(hex);
			}
		};
	}

	private class Candidate {
		public HexEdge edge;
		// Steps the edge saves between its own two ends, and that plus what it saves between the
		// bonfire and the gate, both ways.
		public int pair;
		public int score;
	}

	// Places up to `wanted` edges, each the one that saves the most walking once opened: between
	// its own two ends first (at least minSaving, so those ends were a walk of 4 apart), and then
	// between the bonfire and the gate, both ways. The first must have an end within three hexes
	// of the bonfire when any can, so it is a way back to it. Each is kept only if the section
	// stays whole with it closed and every edge still has b strictly farther from the bonfire
	// than a (see Oriented). False if not even one edge was worth placing.
	public static bool PlaceEdges(SectionTerrain t, Vector2I gate, int wanted, int minSaving, RandomNumberGenerator rng) {
		for (int n = 0; n < wanted; n++) {
			var pool = Candidates(t, gate).Where(c => c.pair >= minSaving).ToList();
			if (pool.Count == 0) return n > 0;
			if (n == 0) {
				var near = pool.Where(c => Mathf.Min(CampaignGenerator.Distance(c.edge.a, t.entry), CampaignGenerator.Distance(c.edge.b, t.entry)) <= NEAR_ENTRY).ToList();
				if (near.Count > 0) pool = near;
			}
			bool placed = false;
			foreach (Candidate pick in pool.OrderByDescending(c => c.score + Variety(t, c.edge.kind) + rng.Randf())) {
				t.edges.Add(pick.edge);
				if (Oriented(t)) {
					placed = true;
					break;
				}
				t.edges.Remove(pick.edge);
			}
			if (!placed) return n > 0;
		}
		return true;
	}

	// A shortcut opens from its far end b, so b must be strictly farther from the bonfire than a
	// by walking with every edge closed. A tree or door is turned round when it is the wrong way,
	// a ladder never (b is its high end), and a tie fails either. Nothing is changed unless every
	// edge passes, since a later edge's walls can lengthen the walk to an earlier one's ends.
	private static bool Oriented(SectionTerrain t) {
		var walk = t.Walk(t.entry);
		var turn = new List<HexEdge>();
		foreach (HexEdge edge in t.edges) {
			int da = t.Dist(walk, edge.a), db = t.Dist(walk, edge.b);
			if (da == db) return false;
			if (da < db) continue;
			if (edge.kind == WorldEdgeKind.LADDER) return false;
			turn.Add(edge);
		}
		foreach (HexEdge edge in turn) (edge.a, edge.b) = (edge.b, edge.a);
		return true;
	}

	// Doors are rare by nature, so they get a lift; a second edge prefers a kind not yet used.
	private static float Variety(SectionTerrain t, WorldEdgeKind kind) =>
		(kind == WorldEdgeKind.DOOR ? DOOR_LIFT : 0f) + (t.edges.Count > 0 && t.edges.All(e => e.kind != kind) ? 1.5f : 0f);

	private static List<Candidate> Candidates(SectionTerrain t, Vector2I gate) {
		var found = new List<Candidate>();
		foreach (Vector2I x in t.Land) {
			foreach (Vector2I off in WorldMapData.NEIGHBOR_OFFSETS) {
				Vector2I y = x + off;
				if (!t.Walkable(y) || !Before(x, y) || t.EdgeBetween(x, y) != null) continue;
				int diff = Mathf.Abs(t.ElevationAt(x) - t.ElevationAt(y));
				if (diff >= 2) {
					bool xLow = t.ElevationAt(x) < t.ElevationAt(y);
					Add(found, t, gate, xLow ? x : y, xLow ? y : x, WorldEdgeKind.LADDER);
				} else {
					Add(found, t, gate, x, y, WorldEdgeKind.DOOR);
				}
			}
			foreach (Vector2I y in t.Land) {
				if (CampaignGenerator.Distance(x, y) != 2 || !Before(x, y) || t.EdgeBetween(x, y) != null) continue;
				var middles = CampaignGenerator.Neighbours(x).Intersect(CampaignGenerator.Neighbours(y));
				if (middles.Any(t.Walkable)) continue;
				Add(found, t, gate, x, y, WorldEdgeKind.TREE);
			}
		}
		return found;
	}

	private static bool Before(Vector2I x, Vector2I y) => x.X != y.X ? x.X < y.X : x.Y < y.Y;

	// Scores one candidate by closing it, checking the section is still whole, then opening it.
	// A door is judged against the section with it shut, since shutting it is what it does.
	private static void Add(List<Candidate> found, SectionTerrain t, Vector2I gate, Vector2I x, Vector2I y, WorldEdgeKind kind) {
		var edge = new HexEdge { a = x, b = y, kind = kind };
		int pairWalk = SectionTerrain.NEVER;
		if (kind != WorldEdgeKind.DOOR) {
			pairWalk = t.Dist(t.Walk(x), y);
			if (kind == WorldEdgeKind.TREE) pairWalk = Mathf.Max(pairWalk, t.Dist(t.Walk(y), x));
			if (pairWalk < MIN_PAIR_WALK) return;
		}

		t.edges.Add(edge);
		try {
			if (!t.StronglyConnected()) return;
			if (kind == WorldEdgeKind.DOOR) {
				pairWalk = t.Dist(t.Walk(x), y);
				if (pairWalk < MIN_PAIR_WALK) return;
			}
			var fromEntry = t.Walk(t.entry);
			int da = t.Dist(fromEntry, edge.a), db = t.Dist(fromEntry, edge.b);
			if (da == db || (kind == WorldEdgeKind.LADDER && da > db)) return;
			if (da > db) (edge.a, edge.b) = (edge.b, edge.a);
			int forwardClosed = t.Dist(fromEntry, gate);
			int backClosed = t.Dist(t.Walk(gate), t.entry);
			edge.open = true;
			int forwardOpen = t.Dist(t.Walk(t.entry), gate);
			int backOpen = t.Dist(t.Walk(gate), t.entry);
			int pair = pairWalk - 1;
			found.Add(new Candidate { edge = edge, pair = pair, score = pair + 2 * ((forwardClosed - forwardOpen) + (backClosed - backOpen)) });
		} finally {
			t.edges.Remove(edge);
			edge.open = false;
		}
	}
}
