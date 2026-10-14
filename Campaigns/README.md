# Campaign Files

Each `.json` file in this folder is one campaign world map. Every file here
automatically appears in the **Campaign** dropdown on the New Game screen.

After editing a campaign, sanity-check it with:

```bash
powershell -File Campaigns/Validate-Campaign.ps1 -Path Campaigns/DemoCampaign.json
```

It reports duplicate ids/coordinates, missing start nodes, floating scenery, edges
that do not fit their kind, roamer loops that break the loop rule (see Roamers), and any passable node that can't be reached from the
start under the climbing rule (treating encounters as clearable) even with every
edge open. It also prints how many hexes are reachable with the edges closed.

## Format

```json
{
	"name": "Demo Campaign",
	"sections": 6,
	"nodes": [
		{
			"id": "firelink",
			"name": "Firelink Shrine",
			"q": 0, "r": 0,
			"terrain": "Grass",
			"encounter": "Bonfire",
			"level": 1,
			"section": 1,
			"elevation": 1,
			"start": true
		}
	]
}
```

Root fields: `name`, `nodes`, optionally `edges` (see Edges below), optionally `roamers` (see Roamers below), and optionally `sections`, how many sections the campaign has (Small 6,
Medium 10, Large 14). It sets each section's soul budget; without it encounters pay nothing and the
Level Up has no cap.

## Node fields

| Field | Required | Meaning |
|---|---|---|
| `id` | no (defaults to `"q,r"`) | Unique key. Cleared-encounter tracking and save files reference it, so avoid renaming ids mid-campaign. |
| `name` | no | Display name shown on the map info panel. |
| `q`, `r` | yes | Axial hex coordinates (pointy-top). Two nodes are connected when they are hex-adjacent. |
| `terrain` | no (Grass) | `Grass`, `Mountain`, `Snow`, `City`, `Sand`, `Volcano`, `Lava`, `Water`. Lava and Water are impassable. |
| `encounter` | no (None) | `None`, `Bonfire`, `Encounter`, `Boss`. A boss is fought like an encounter at its `level` until real bosses exist. |
| `level` | no (1) | Encounter difficulty 1–4. Used by `Encounter` and `Boss`. |
| `section` | no (0) | The section this node belongs to, from 1 to `sections`; its encounters share that section's soul budget. |
| `boss` | no (Main) | `Mini`, `Main` or `Mega`. Only meaningful when `encounter` is `Boss`; it sets the souls paid and what the Level Up cap waits on. |
| `elevation` | no (1) | 0–8. Tiles render that many steps tall. The party may step down any amount (a drop cannot be climbed back) but climb at most 1. |
| `loot` | no (None) | `Estus_Shard`, `Treasure`, `Key`, `Souls` or `Homeward_Bone` (any case): something to pick up on the hex. On a hex with an `Encounter` or `Boss` it is *guarded* and taken on the win; otherwise on stepping there. Each hex pays once, ever. `Souls` only on a hex with no fight (the section's soul budget is split over it as well), and `Treasure` draws a card from the Merchant's deck. The map shows a glint on a hex with loot, never what it is. |
| `key` | no | The key's name, for `loot: "Key"`. Picked keys are kept in the save; a door whose edge names the same `key` can be unlocked from either side (see Edges). |
| `structure` | no (None) | `Castle`, `Cathedral`, `Library`, `Aqueduct`, `Tower`, `Crypt`, `Ruins` or `Slum` (any case): a building drawn over the hex once the party knows it, with its name in the tooltip. A `Tower` or `Castle` is also a vantage point: standing on it widens the party's sight by a hex. `Castle`, `Cathedral`, `Library` and `Tower` are landmarks: they show as a silhouette through the fog on an unexplored hex within 6 hexes of the party. Not on `Lava` or `Water`. |
| `start` | no (false) | Exactly one node should set this. The party starts here and it is the initial death checkpoint, so it should normally be a Bonfire. |

## Edges

An edge joins two hexes by something more than standing next to each other. It starts
closed; the party opens it from a hex it stands on, and it stays open for good, through
rests and wipes. **A shortcut is opened from its far end, `b`**: the end farther from the
start by walking with every edge closed, so the party has to get there the long way first.
The game turns a tree or door round on loading if `a` is the farther end (the validator warns),
and warns about a ladder whose high end is not the farther one. Root array `edges`, each `{ "a", "b", "kind", "key" }` with `a` and `b`
node ids (an edge naming a missing node is skipped with a warning):

```json
"edges": [
	{ "a": "foothills", "b": "old-switchbacks", "kind": "Ladder" },
	{ "a": "drake-perch", "b": "wind-shelf", "kind": "Tree" },
	{ "a": "drake-perch", "b": "summit-bonfire", "kind": "Door", "key": "Mountain Key" }
]
```

| Kind | Joins | Closed | Opened |
|---|---|---|---|
| `Ladder` | neighbours whose heights differ by 2 or more; `a` the low end, `b` the high, which must also be the far end | the drop down is allowed (as always), the climb is not | **Kick Down Ladder**, standing on `b`, the high hex |
| `Tree` | two hexes at distance 2 whose middle hex is impassable or has no node; `b` the far end | no way across | **Fell Tree**, standing on `b`; then the ends count as neighbours both ways (the height rule still applies) |
| `Door` | two neighbours; `b` the far, inside side | blocks the step both ways | **Open Door**, standing on `b`; or **Unlock Door** from `a` while the party holds the `key` (a door with no `key` opens only from `b`) |

A key is a hex with `"loot": "Key"` and `"key": "<name>"`; picking it up puts the name in the save.

## Roamers

A roamer is an enemy group that patrols a fixed loop of hexes, one hex per party step on the map, and
hunts the party once it is within 2 hexes. Root array `roamers`, each `{ "id", "name", "section", "loop" }`;
a roamer naming a hex that does not exist is skipped with a warning:

```json
"roamers": [
	{ "id": "snow-patrol", "name": "Snowline Patrol", "section": 4, "loop": ["snowline", "lake-shore"] }
]
```

| Field | Meaning |
|---|---|
| `id` | Unique, and not the id of a node. The roamer's rolled encounter and its saved state are keyed by it. |
| `name` | Shown as the encounter's title. |
| `section` | The section its hexes are in. Its encounter is rolled one level above that section's own encounters (to a cap of 4), from the family its terrain names. |
| `loop` | Node ids, walked in order and closed on itself. Two or more hexes. |

The loop rule: every hex is **plain** (no `encounter`, so no bonfire, encounter or boss, and not Lava or
Water), every hex is in `section`, and each step in order, the last back to the first included, is a legal
step with every edge closed: neighbours, up at most 1, not through a door. A path that cannot close is
written there and back (`A B C B`), which satisfies the rule, repeated hexes and all. A roamer starts on its
first hex, never enters a bonfire or an encounter hex (so those are safe to stand on), and may use an open
shortcut when it hunts but never opens one. It pays no souls; beaten, it drops one treasure card and stays gone
until the next rest or wipe. The validator checks all of this.

## Axial coordinates cheat sheet

The six neighbors of `(q, r)` are:
`(q+1, r)`, `(q-1, r)`, `(q, r+1)`, `(q, r-1)`, `(q+1, r-1)`, `(q-1, r+1)`.

Rows share `r`; each row down (`r+1`) shifts half a tile to the right, so to
keep a column visually straight, decrease `q` every second row.
