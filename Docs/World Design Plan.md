# World Design Plan (2026-10-09)

Source: `souls-hex-rpg-world-design.md` (Matt's design doc) plus Matt's additions. Decisions below are
Matt's unless marked (assumption). Phases are ordered; each lands on its own.

## Decisions

- **Sight rule.** From a hex at elevation e, a hex at elevation t and hex distance d is
  *seen* when `diff = t - e <= 0` and `d <= 1 - diff` (1 hex on the level, one more per step downhill).
  A hex with `diff == 1` at `d == 1` is *glimpsed*: its edge is known (shape, elevation) but not what is on it.
  `diff >= 2` is never seen from here. Once seen a hex stays *known*; once glimpsed it stays *glimpsed*
  until seen. Enemies on a known hex show only while the hex is currently in sight.
- **Stale fight hexes** (known, not in sight): the toughest enemy as a black silhouette plus the level icon.
- **One-way drops.** The party may descend any elevation difference; climbing stays capped at +1.
- **Wins no longer heal.** Victory removes stamina cubes only; damage stays until Estus or a rest.
- **Party Estus pool.** `estusMax` flasks shared by the party, refilled on rest, drinkable by the active
  character in their own activation like their personal Estus. **Estus Shards** found on the map are
  spent at a bonfire with **Reinforce Estus Flask** (shown only while a shard is held): +1 max, free.
  (assumption) The pool starts at 1 flask, constant `PARTY_ESTUS_START`, for Matt to tune.
- **Map loot** hexes hold one of: Estus Shard, a treasure card from the Merchant's deck, a key, souls.
  Loot is handed out from lowest rarity to highest as the campaign progresses (`Merchant.LootTier`):
  unguarded loot is the best the Merchant sells (Common, Uncommon from halfway); guarded loot and roaming
  drops are Uncommon below a quarter of the way, Rare below a half, Epic below three quarters, Legendary after.
  The Merchant sells Common only, Uncommon (dearer) from halfway, never rarer. Boss gear is in no deck: it is
  reserved for boss payouts, which are not built yet.
  Guarded loot sits on an encounter a level above its section and is taken on the win.
- **Sections become winding**: ridges, chasms and water inside the blob so the bonfire→fog gate walk
  snakes, and one or two **shortcuts** per section cut it: a *ladder* (kicked down from the high side
  of a 2+ elevation step) and a *felled tree* (kicked across a 1-hex gap of water/lava/void, from either side).
  A *door* is a locked edge opened by a key or from the far side; doors come with structures.
- **Structures** (castles, cathedrals, crypts, bridges, towers, ruins) are code-drawn silhouettes over
  the hex, in the hex art's style, until real art replaces them.
- **Roaming threats**: an enemy group patrols a fixed loop, one hex per party move; within 2 hexes
  of the party it chases. Contact starts its encounter. Beaten, it is gone until the next rest.
- **Bloodstain** already drops on the encounter node where the first character fell (done).
- Later: hexes with a *below* layer (catacombs), mimics on map loot, illusory walls, world changes after
  a boss, lore fragments. No player-style messages.

## Phases

1. **Fog of war** — sight/known/glimpsed state on `WorldMapManager`, saved; hexes drawn by sight;
   one-way drops; DEV reveal toggle; validator + README updated.
2. **Wounds persist + party Estus** — (a) win clears stamina only, party pool, Reinforce at the bonfire,
   flask on the action bar; (b) map loot hexes (shard / treasure / key / souls) in the data, generator,
   drawing and pickup.
3. **Winding sections + shortcuts + structures** — generator carving, edge data (`ladder`, `tree`, `door`),
   unlocking from the right side, keys, code-drawn structures and doors.
4. **Roaming threats** — patrol loops in the save, movement per party step, chase, contact → encounter.
