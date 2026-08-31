# Campaign Files

Each `.json` file in this folder is one campaign world map. Every file here
automatically appears in the **Campaign** dropdown on the New Game screen.

After editing a campaign, sanity-check it with:

```bash
powershell -File Campaigns/Validate-Campaign.ps1 -Path Campaigns/DemoCampaign.json
```

It reports duplicate ids/coordinates, missing start nodes, floating scenery,
and any passable node that can't be reached from the start under the ±1
elevation rule (treating encounters as clearable).

## Format

```json
{
	"name": "Demo Campaign",
	"nodes": [
		{
			"id": "firelink",
			"name": "Firelink Shrine",
			"q": 0, "r": 0,
			"terrain": "Grass",
			"encounter": "Bonfire",
			"level": 1,
			"elevation": 1,
			"start": true
		}
	]
}
```

## Node fields

| Field | Required | Meaning |
|---|---|---|
| `id` | no (defaults to `"q,r"`) | Unique key. Cleared-encounter tracking and save files reference it, so avoid renaming ids mid-campaign. |
| `name` | no | Display name shown on the map info panel. |
| `q`, `r` | yes | Axial hex coordinates (pointy-top). Two nodes are connected when they are hex-adjacent. |
| `terrain` | no (Grass) | `Grass`, `Mountain`, `Snow`, `City`, `Sand`, `Volcano`, `Lava`, `Water`. Lava and Water are impassable. |
| `encounter` | no (None) | `None`, `Bonfire`, `Encounter`, `Boss` (Boss is currently stubbed and behaves like None). |
| `level` | no (1) | Encounter difficulty 1–4. Only meaningful when `encounter` is `Encounter`. |
| `elevation` | no (1) | 0–8. Tiles render that many steps tall. The party can only move to a neighbor whose elevation differs by at most 1. |
| `start` | no (false) | Exactly one node should set this. The party starts here and it is the initial death checkpoint, so it should normally be a Bonfire. |

## Axial coordinates cheat sheet

The six neighbors of `(q, r)` are:
`(q+1, r)`, `(q-1, r)`, `(q, r+1)`, `(q, r-1)`, `(q+1, r-1)`, `(q-1, r+1)`.

Rows share `r`; each row down (`r+1`) shifts half a tile to the right, so to
keep a column visually straight, decrease `q` every second row.
