# Dark Souls DnD — Godot Project

## Project Overview

A turn-based tactical game in Godot 4 (C#) inspired by **Dark Souls: The Board Game** (Steamforged Games). Players build parties of 1–4 characters and fight through encounters on a grid, culminating in boss fights. Core board game systems being implemented: node-based grid movement, A* enemy pathfinding, dice-based combat, stamina/health tracking, status effects, equipment with stat requirements, and bonfire resting.

## Tech Stack

- **Engine**: Godot 4.5, C# (.NET)
- **Target**: Windows desktop (single-machine local multiplayer)
- **Scene format**: `.tscn` text files, `.tres` resource files

## Key Source Files

```
Scripts/
  Encounter/
    ActionListener.cs      — Turn orchestrator; spawns enemies, drives the action state machine
    EncounterResultPanel.cs— Win/defeat modal; exits to World Map or Bonfire
    EncounterManager.cs    — Static global state (players, enemies, nodes, action enum)
    TurnQueue.cs           — Threat-ordered activation strip above the board
    TurnQueueEntry.cs      — Single face in that strip
    BoardCamera.cs         — Zoom/pan for the board: wheel, drag, R / Reset View
    TokenHighlight.cs      — Pulsing ring laid over a board token (attacker red, defender gold)
    EnemySpotlight.cs      — Previews each enemy move/attack (rings, lit path) before it happens
    NearestMarker.cs       — Decides which character wears the Nearest marker (hover / spotlight)
    GameNode.cs            — Individual grid tile; flags for entrance/enemy-spawn/disabled
    HUD/
      EquipmentCross.cs    — The DS-style equipment cross floating left of the action bar; the backup slot is a carousel
      EquipmentSlot.cs     — One slot of it: fixed crop of the printed card, glow, spent veil
      CircleAvatar.cs      — Face in a gold ring with damage rising as a red fill (shader)
      RollReveal.cs        — Centre-screen modal: dice tumble, land, then the arithmetic; click to continue
      PhaseBanner.cs       — "Enemy Turn" / "<Name>'s Turn" banner the turn loop awaits
      CardPreview.cs       — The full printed card while a cross slot or the raised weapon's art is hovered
    Player/
      Player.cs            — Campaign-persistent character (stats, equipment, level)
      PlayerToken.cs       — Character on the board; endurance, conditions, aggro, activation state
      CharacterTurn.cs     — Owns one activation: stepping, arming an attack, picking targets
      CharacterActionBar.cs— The bottom HUD: cross, avatar, endurance, defence dice, tokens, card-style attack rows, Block/Dodge reaction, End Turn
      Endurance.cs         — The 10-box shared stamina/health bar (pure rules logic)
      Character.cs         — Character class template (tiers, starting gear, avatar, heroic action)
      Heroic.cs            — The ten Heroic Actions: names, effects, which are boosts, their dice
      PlayerMove.cs        — Single weapon action (cost, dice, range, flags)
      CharacterSheet.cs    — UI component showing character stats and equipment slots
      PlayerSheet.cs       — In-encounter player status strip
    Enemy/
      Enemy.cs             — Enemy token; behaviour execution, health, conditions, detail levels
      EnemyMovement.cs     — Turns a Move icon into a node list (towards/away, distance, push)
      EnemyData.cs         — Per-enemy data card as a Resource (stats + moves + art)
      EnemyMove.cs         — Enemy action definition (direction, damage, flags)
      EnemyCardViewer.cs   — Full-size printed card beside the board while a face in the activation bar is hovered
      Pathfinding/
        Pathfinding.cs     — A* algorithm (static)
        PathGrid.cs        — Grid wrapper used by pathfinding
        PathNode.cs        — Per-cell pathfinding data
        Heap.cs            — Min-heap for A* open set
    Combat/
      CombatResolver.cs    — Dice + arithmetic for one attack (no turn order, no UI)
      AttackTerms.cs       — An attack's effective cost, range, AOE and extra dice (Stagger, Heroic boosts)
      CombatPresenter.cs   — Rolls each die, shows the roll in RollReveal, then applies the outcome
      AttackSlash.cs       — The stroke on the board: blade, arrow or orb; winds up and holds, lands with the outcome
      DodgePrompt.cs       — Rings attacker/defender and awaits the action bar's Block/Dodge answer
      PushPrompt.cs        — Picks which model is shoved off an over-full node
    Fx/
      BoardFx.cs           — Hub for board-space effects: the board to parent under, the camera, the punch layer
      BoardFloat.cs        — A number or icon rising from a token (damage, stamina back, a condition)
      EnemyDeath.cs        — Ghost of a killed enemy: greys and shrinks, wisp lifts, embers scatter
      TokenMotion.cs       — Pieces travel: glide on the token's button after MovePlayer, enemy walk steps
      AggroHandoff.cs      — The Aggro token hopping from the last holder to the new one
      ConditionBurst.cs    — Tinted rings spreading from a token as a condition lands
      ConditionArt.cs      — Icon and colour per condition
      ScreenPunch.cs       — Red edge vignette + board jolt on a heavy hit to a character
  Equipment/
    Equipment.cs           — Interface: name, image, type, rarity, stat reqs
    Weapon.cs              — Weapon resource (attacks, defense dice, upgrade slots)
    Armour.cs              — Armour resource (defense dice, upgrade slots)
    Ring.cs                — Ring resource: an armour upgrade naming its one effect (Tools/onboard_rings.py)
    Gem.cs                 — Gem resource: a weapon upgrade naming its effect (Tools/onboard_gems.py)
    Dice/
      Dice.cs              — Dice definition (type, possible values)
      DiceUtility.cs       — Roll helper
  MainMenu/
    MainMenu.cs            — New Game / Continue / Quit
    Bonfire/
      BonfireOptions.cs    — Rest / Equipment / Ready buttons
      BonfireRestAnimation.cs — Flare/blackout/fade-back transition the rest happens behind
      BonfireFireLight.cs   — The living fire on the Bonfire screen: flicker, floor warmth, sparks
  Common/
    CharacterPortraitPane.cs  — Autoloaded floating party pane (Bonfire/Encounter)
    SoulCounter.cs            — Autoloaded DS-style soul count, top-right under each scene's title bar
    CharacterPortrait.cs      — Single portrait: avatar bg, name, HP, stamina, status strip
    TickBar.cs                — Ten-tick endurance row with ghost previews; solid (continuous) bar for enemy health
    DiceChip.cs               — A die type and count as a coloured chip
    TokenArt.cs               — Estus / Heroic / Luck token art, ready and spent (flipped)
    RuleIcons.cs              — The rulebook's Push / Node / Shaft / Repeat / Shift icons (Tools/RuleIcons/extract.py)
    CubeRow.cs                — A row of black (stamina) or red (damage) cubes
    ButtonFx.cs               — Hover glint, press ripple and (armed) breathing border drawn over a button
    PopIn.cs                  — A control settling in from large, for icons that appear
    RollDie.cs                — One die in the roll reveal, thrown onto the tray and landing on its rolled face
    DiceArt.cs                — The printed dice faces: one sheet per die, pip count per cell, cell for a rolled value
    CursorPolicy.cs           — Autoload: every button gets the pointing hand; disabled ones the arrow while hovered
    EquipmentModal.cs         — Autoloaded modal: Summary | Equipment | Inventory
    CharacterSummaryPanel.cs  — Modal column: stats from currently-equipped gear
    CharacterEquipmentPanel.cs — Modal column: slot + upgrade buttons, no StaminaHealth
    InventoryPanel.cs         — Modal column: search/filter/sort grid over the owned pool
    ComparisonPanel.cs        — Slide-in current-vs-proposed stat compare
  CharacterSelect/
    CharacterSelect.cs     — 1–4 player setup; character class picker, name input
  SaveGame/
    SaveGame.cs            — Campaign save data resource
  SoulCache.cs             — Party soul pool + the pile a wipe leaves behind
  WorldMap/
    EncounterGenerator.cs  — Rolls each map encounter once: health band per level, terrain family, tiers, spawn nodes
    EncounterPlan.cs       — One fixed encounter as saved: tile + its EncounterSpawns; Toughest for the map icon
    EncounterSpawn.cs      — One enemy of a plan: EnemyData path, tier, spawn slot
```

## Board Game Rules Reference

The physical game rules are in `Dark Souls Board Game Rules.pdf`. Key mechanics being implemented:

- **Nodes** (p10): Grid tiles. Types: basic, spawn (enemy start), terrain, entrance (player start). Max 3 models per node — a fourth may still move on, and doing so forces the players to push one of the three already there.
- **Encounters** (p19): **all** enemies activate together as one phase (ordered by threat, high to low), then **one** character activates. Alternates until all enemies are dead or a character dies.
- **Character activation** (p22): gain 2 stamina, **gain the Aggro token**, may swap backup ↔ hand slots. Then move and attack — movement happens entirely before *or* entirely after the attack, never split. Walk free once, run costs 1 stamina/node, dodge costs 1 stamina + roll.
- **Enemy activation**: Follow behavior icons left-to-right. Move towards/away from aggro or nearest character.
- **Aggro token**: The active character always holds aggro. Enemies prioritize the aggro holder. Ties for "nearest" break to the aggro holder, then to the higher **Taunt** (`Character.taunt` — characters have Taunt, only enemies have Threat).
- **Combat (player attacks)**: Roll dice per weapon attack option, subtract enemy Block (physical) or Resist (magic). Result = damage.
- **Combat (enemy attacks)**: Fixed damage value. Player rolls defense dice to reduce. Or spend 1 stamina to attempt dodge roll vs dodge difficulty.
- **Endurance bar** (p20): 10 boxes shared by Stamina and Health. Spending 1 stamina adds a black cube from the left; suffering 1 damage adds a red cube from the right. Uncovered boxes are the remaining capacity to do *either*. ⚠️ **Death (Matt's ruling, replaces p20's "all ten covered"):** a character dies only when **damage overflows** the bar, needing more red cubes than there are boxes left, from an enemy attack or a condition such as Poison. A bar filled exactly, whether by their own stamina or by damage that just fits, is still standing. Stamina can never kill, since nothing spends more than the free boxes. The party is then immediately defeated (p19). Gaining stamina/health **removes** cubes and does nothing when there are none to remove. Cleared on encounter victory.
- **Status effects** (rules p21): conditions apply to **any model — characters and enemies alike**. BLEED (2 extra damage next time the model is damaged, then remove), POISON (1 damage at end of the model's activation), FROST/Frostbite (character: +1 stamina to walk/run/dodge; enemy: Move icon values −1), STAGGER (character: +1 stamina to use weapon actions; enemy: attack damage values −1). All of this is implemented — see Conditions under Architecture Notes.
- **Bonfire rest**: Refills estus, heroic action, luck. Resets all encounters (enemies respawn) — **except bosses, which stay beaten** (this project's own call; see Bonfire Rest under Architecture Notes). ⚠️ The printed rule also costs 1 **spark** — sparks are **deliberately cut from this project**. Do not implement them, do not gate resting on them, and do not use p19's spark-based boss soul formula.
- **Souls**: Currency. Earned 2 per character per non-boss encounter win. Spent on treasure (1 soul) and leveling up stats. Implemented — see Souls under Architecture Notes. Spending is not wired to anything yet.

### V2 Rules (official revision; these are deliberate, not mistakes)

Steamforged issued V2 rules that change some of the printed (V1) rulebook. Where they differ, this project follows **V2**. Code that looks like it contradicts the page numbers above is following these:

- **Dodge (changes p22 / p25).** A **failed** dodge no longer takes the full damage: the character still blocks, rolling **only the dice on their armour slot** (its Block or Resist for the attack's type, plus that armour's own flat modifier) instead of their full Block. A **successful** dodge lets the character **choose** to spend **1 stamina** to move 1 node; in V1 the move came free with every dodge, before the roll. The dodge's own stamina is still paid up front. Code: `CombatPresenter.EnemyAttacksDodging` → `EnemyAttacks(armourOnly: true)`, `Player.GetArmourPool`, `CharacterTurn.DODGE_STEP_COST`.
- **Push (changes p21).** A pushed model can no longer go to any adjacent node of the players' choosing. It goes **away**, in the opposite direction from where the pusher came: away from the attacker, or onward along the enemy's step. "Away" is the node straight on and the two 45° either side of it, and **the players choose** among those (Matt's reading, Oct 2026), on either side's turn. Code: `Pushing`, `PushPrompt.ChooseNode`.

### Combat Values & Clarifications

Concrete numbers and rules confirmed while analysing the equipment `.tres` data. Use these when reasoning about balance or implementing combat resolution:

- **Dice**: Only three types (`DiceUtility.DICE_TYPE`). Values and expected averages:
  - BLACK (0): faces `0,1,1,1,2,2` → avg **1.167**
  - BLUE (1): faces `1,1,2,2,2,3` → avg **1.833**
  - ORANGE (2): faces `1,2,3,3,4` → avg **2.5**
  - Damage/defense are arrays of these dice plus a flat `modifier`; expected value = sum of dice averages + modifier.
- **Action economy** (p22): a character makes up to **one attack with each weapon in a hand slot** — so two one-handers means two attacks in an activation, and a two-hander means one. Movement is one block, before or after the attacking.
- **Stamina is recovered capacity, not a pool**: there is no stamina number. What a character can spend is whatever the endurance bar still has uncovered, so an undamaged character with a clean bar can spend up to 10 in a single activation. The +2 at activation start *removes black cubes*, so it is a recovery rate, not income to bank. Burst is available immediately; the +2 governs only the long-run average, so sustained damage ≈ `attackDamage × min(1, 2/cost)` while a one-off spike can far exceed it.
- **Stamina and damage compete for the same boxes**: every point spent is a box that can no longer absorb a hit, and spending competes with running and dodging. Spending your bar full does not kill you, but it leaves no room, so the next point of damage does (see Death above).
- **Upgrade slots** (`upgradeSlots`) hold upgrade **cards** (p12): rings on armour, gems on weapons. Each card has its own effect. An early guess here that a slot itself gave "+1 damage or +1 black die" / "+1 stamina or +1 health" was wrong and is not implemented. Both are done: see Rings and Gems.
- **Status effects** (`EncounterManager.StatusEffect` = `BLEED=0, POISON=1, FROST=2, STAGGER=3, NONE=4`):
  - Applied to **any model hit by an attack carrying a condition icon** — enemies inflict them on characters just as weapons inflict them on enemies. Auto-applied on **any** hit, regardless of whether damage gets past Block/Resist. **No immunity.**
  - A model can carry multiple *different* conditions at once, but a single condition does **not** stack (no double BLEED).
  - **POISON, FROST and STAGGER are removed at the end of the afflicted model's own activation**, and any remaining conditions are cleared at the end of the encounter. BLEED persists until the model next suffers damage (+2 on that damage, then removed — and reapplied by that same hit if the attack carries BLEED).
  - Effect magnitudes differ by target: BLEED +2 damage for both. POISON 1 dmg at end of activation for both. FROST — character pays +1 stamina to walk/run/dodge, enemy has its Move icon values reduced by 1. STAGGER — character pays +1 stamina for weapon actions, enemy has its attack damage values reduced by 1.
  - ⚠️ `BLEED = 0` collides with "falsy"/default-int handling — when parsing `statusEffect`, default missing values to `NONE (4)`, not `0`, or bleed weapons get silently misread.

## Architecture Notes

### Global State
`EncounterManager` is a static class holding encounter-wide state (active players list, enemies list, node list, current action). It needs an explicit `Reset()` call when starting a new encounter because static state persists across scene loads.

### Action State Machine
States: `INACTIVE`, `PICK_ENTRANCE`, `ENEMY_MOVE`, `CHARACTER_TURN`, `ENCOUNTER_WON`, `ENCOUNTER_LOST`.

`action` is a one-shot command; `EncounterManager.phase` is the sticky companion holding the current phase, alongside `activeEnemyIndex`, `activeCharacterIndex` and `round`. The HUD reads those; only `ActionListener` writes them.

`_Process` reads `action`, **clears it to `INACTIVE` before dispatching**, then acts. That ordering matters: an activation with nothing to do finishes synchronously and queues the next step from inside the handler, and clearing afterwards would stamp `INACTIVE` back over it and hang the loop.

**Placement and order (p19).** During `PICK_ENTRANCE` the selected character goes on the next entrance clicked: the first in the party, then the next unplaced, and a portrait click picks someone else. The order they are placed in is the activation order for this encounter, since `EncounterManager.players` is filled in that order and `activeCharacterIndex` starts at 0. Whoever is placed first led the way in and takes the Aggro token at once, so the first enemy phase has a holder. The order is chosen afresh every encounter (Matt's call), so the book's First Activation token is deliberately not implemented.

Rules p19: every enemy activates in threat order, then exactly one character. Enemy movement is an awaited walk (`Enemy.Walk`, one eased step per node) rather than something that returns at once, so the loop is driven by the `Enemy.ActivationFinished` signal rather than running straight through. `Enemy.ProcessNextMove` skips any behaviour it cannot execute rather than stalling, so an activation always terminates and the signal always arrives. The character phase then waits on the action bar's End Turn button (`CharacterActionBar.EndActivationPressed`).

### Character Activation
`CharacterTurn` owns everything the player does during their activation; `ActionListener` still owns turn order and just hands over via `Begin`/`End`. `CharacterActionBar` is the bottom HUD, shaped after the Dark Souls games' with BG3's readouts (Matt's brief, Sept 2026): the **equipment cross** floats off its left end over the board (backup on top, armour under it edge to edge, hands either side of the seam — `EquipmentCross`, DS1 geometry from one exported slot size), then the character's face in a ring (`CircleAvatar`), name, ten-tick endurance bar, Block/Resist dice chips, the Estus/Heroic/Luck tokens (display only until those systems exist), the **raised weapon's attack options as the rows of its printed card** (`[cost]` bracket, black stamina cubes, dice chips, flag icons), and a round **End Turn**. The souls moved out of the bar into `SoulCounter`. Clicking a hand slot raises that weapon; the first usable one is raised automatically. Hovering a row ghosts its stamina cost onto the endurance bar (`TickBar.SetGhost`). Rows are rebuilt on every change rather than diffed — a stale button would let a player take an illegal turn. The bar has three states and never leaves the layout (idle / activation / reaction, plus the dodge step), and its weapon column has a fixed minimum height so switching state never resizes the board. It is never blank: it always shows a **selected** character (`CharacterActionBar.Select`) — the first in the party on load, whoever is clicked in the party pane (`ActionListener.SelectCharacter`), and the active character whenever an activation begins. Anyone but the active character is shown read-only: their hands can be raised to read the options, but every row and End Turn are disabled. A reaction or dodge step still takes the bar over and hands it back to the selection.

Slot art is a **crop of the printed card framed on the item** (`EquipmentSlot.SubjectRegion`), so no card is ever sliced into its own resource; `wholeCard` shows the entire card instead. Cards come in two layouts: a weapon with attack rows sits high above a parchment table, while shields, spells and armour without one sit lower and larger. So one fixed crop cannot fit both, and each card is measured once and cached instead. The detector finds where the parchment starts, finds the item against the card's own background between the title and there, and frames it at the slot's aspect, kept clear of the stat and icon columns (x 0.2–0.8). The thresholds were tuned by eye against the whole card set, including the class starting gear under `Player/<Class>/Equipment`, whose cleaner scans sit on a lighter patterned background. That is why every brightness test is relative to the card itself. A table is only accepted after 8% of the card height of lit rows: an item's edge lining up with the bottoms of the stat and icon columns fooled a shorter test. The old fixed regions (`weaponRegion` / `armourRegion`) remain as the fallback when detection fails or `autoFrame` is off. It needs the card textures imported lossless (`compress/mode=0`) so `GetImage` can read them. A two-hander occupies one hand slot and greys the other.

Movement is **one node per click** (p22): the first step of an activation is the free Walk, each later one a Run at 1 stamina, and Frostbite adds 1 to every step including the walk. Legal neighbours are highlighted; `EncounterManager.CanEnter` keeps the node cap honest. Any node that can be clicked (move, entrance pick, dodge step, attack target) is highlighted with `GameNode.Highlight(colour, pulse: true)`: the marker breathes gently in brightness and a faint ring drifts out from under it (never in size — scaling the texture shimmers), drawn in `GameNode._Draw`. Markers that are not click targets, such as the soul drop, do not pulse.

p22's "move before *or* after attacking, never both" is enforced by latching `PlayerToken.movementLocked = hasMoved` on the **first** attack. Move-then-attack locks movement for the rest of the activation; attack-then-move leaves it open, and continuing to move after that is still one contiguous block. Each hand-slot weapon may attack once (`usedWeapons`).

Targeting: an option-specific `attackRange` replaces the weapon's standard range (p23); Shaft (`isNotZeroRange`) excludes range 0; the Node icon (`isAOE`) resolves one roll against every enemy on the chosen node. While a single-target attack is armed, every enemy it can reach gets a pulsing red `TokenHighlight` ring and clicking that enemy's token attacks it; clicking a lit node also works when exactly one reachable enemy stands there. An AOE attack lights nodes only, and clicking either the node or any enemy on it hits the whole node. Outside targeting, a token click is passed to its node (`GameNode.Press`), so moving onto an occupied node still works.

⚠️ `EncounterManager.Reset()` clears `characterTurn`, and `ActionListener._Ready` (which calls it) runs *after* `CharacterTurn._Ready` registers itself — so `ActionListener` re-registers it straight after `Reset`. Without that, every enemy click saw no armed attack, which is why picking a target never worked.

Attack buttons show their dice as `DiceChip`s — a rounded square in the die's colour with the count inside — instead of "1B 1U". After the dice come the option's icons as the printed card shows them: magic, its condition, then the rulebook's keyword icons (`RuleIcons`: Push, with "x2" when it pushes two nodes; Node; Shaft; Repeat and Shift with their number inside; a Shift before the dice sits before the dice chips, as on the card), the range badge when it differs, and the Heroic token when a boost applies. A modifier shows only when it is not 0, so an option with no dice shows no "+0". The row's tooltip spells all of it out, including the option's bonus effects (marked "not implemented" until equipment effects are resolved in combat). The icons were cut from the rulebook PDF (p21, p23) by `Tools/RuleIcons/extract.py`, white on transparent so the bar tints them, with Shift's and Repeat's example numbers blanked out.

Hovering any slot of the equipment cross, or the raised weapon's art above the rows, shows the whole printed card just above it (`CardPreview`, a `CanvasLayer` at 14). It re-checks every frame that the cursor is still over that control instead of trusting `MouseExited`, because the bar rebuilds its header freely and a freed control never reports the mouse leaving.

**Shift and Repeat** (p23). An attack is *armed* by its row, then *committed* by its first hit or first Shift step, which is when its stamina is paid: **once for every use** (Matt's call; the book says Repeat repeats "that entire weapon option"). From there it runs as `CharacterTurn.Underway` until its uses are made or the player stops it, and End Turn reads **Done** (the armed row's own click does the same). Each use is the option in full, as the book's wording reads, so Shift comes again with each Repeat:
- **Shift before the dice**: while armed, the neighbours light as free steps alongside the targets. A node click is a step and an enemy click is the roll (`Enemy.OnClick` only targets an enemy `CanTarget` accepts and otherwise presses its node). Under a Node attack a node that is also a target is the attack, not a step.
- **The roll**, then **Shift after the dice**: nodes only, Done ends it early.
- **The next use** re-arms with no cost, held to `repeatConstraint`: ONE_ENEMY only the enemy the first use hit, ONE_NODE only enemies on its node. It ends by itself when no uses are left, or when nothing is in reach and there is no Shift to get there.

Shift steps are free and are not the Walk or a Run, so they ignore the movement lock and never set `hasMoved`. Pushes, hazards and the over-full-node prompt still apply. Where a Shift sits is not in the equipment sheet, so it was read off the card art: `PlayerMove.shiftAfter` is how many of `bonusMovement` come after the dice (Carthus Curved Sword, Lucerne, Dark Silver Tracer, Gold Tracer; Painting Guardian Curved Sword has one on each side). `Tools/onboard_equipment.py` keeps the same table (`SHIFT_AFTER`). An option that is only a Shift (`IsMovementOnly`: Carthus's and Lucerne's [0]) asks for no target. It still uses the weapon's one attack. The weapon's attack (p22) and any Heroic boost are spent by the first use. Backstab makes one attack and ignores both.

### Ending an Encounter
`ActionListener.CheckEncounterOver` sets `ENCOUNTER_WON` / `ENCOUNTER_LOST` and calls `EndEncounter`, which clears every model's conditions and then settles the outcome. `WorldMapManager` is told **last**, because `ReportEncounterWon` / `ReportPartyDeath` consume `PendingEncounterNodeId` and both branches need it first.

A win clears every endurance bar (p19) and awards `2 × party size` souls. **Sparks are cut from this project**, so p19's boss formula (1 soul per character per remaining spark) is unusable and boss wins currently award nothing — the panel says so. Boss rewards need a replacement rule, not a spark implementation.

The win is checked the moment the last enemy dies: `CharacterTurn` emits `AttackResolved` after every attack and `ActionListener.OnAttackResolved` runs `CheckEncounterOver` there, not at End Activation. It relies on `EncounterManager.GetEnemy` treating an enemy queued for deletion as already gone.

The outcome is announced first by the phase banner in its outcome form (`PhaseBanner.ShowOutcome`): gold **Victory**, or the games' red **You Died**, larger, held longer, the band growing slowly the whole time it is up (the band's rect grows, never the label, which would shimmer). `ActionListener.ShowOutcome` awaits it and only then shows the panel; the outcome itself is settled and reported before the banner starts. `EncounterResultPanel` shows the outcome and exits to the World Map on a win or the Bonfire on a wipe, matching where `WorldMapManager` has just put the party. It takes numbers, not text (`ShowVictory(earned, total)` / `ShowDefeat(dropped, total)`): a centred title, the change as "+X" / "−X" with the soul icon, "Total: X" with the icon, on a translucent panel that fades in. Keep it that way — no explanatory sentences (Matt's standing preference: no obvious or superfluous UI text).

### Bonfire Rest
`WorldMapManager.RestAtBonfire` is the whole rest action and the only one: it clears the party's endurance bars (`CampaignManager.RestParty`), records the checkpoint, respawns every cleared encounter **except bosses**, writes the save to disk, and returns how many encounters came back so the caller can say so.

A beaten boss staying beaten is **this project's call**, not a printed rule — it is the point of the level, not something to re-fight for souls. It is also why rest calls `EnsureLoaded` first: the Bonfire scene never loads the campaign map itself, and without `MapData` there is no `encounterType` to check.

Endurance is encounter-scoped and only a *win* clears it (p19), so after a wipe the party arrives at the bonfire with cubes still on the bars and resting is the only thing that takes them off. That is why `BonfireOptions.OnPressedRest` rebuilds the floating portrait pane — the portraits read `Player.endurance` and would otherwise still show the wounds.

Both entry points call that one method: the World Map's "Rest at Bonfire" action button and the Bonfire scene's own Rest button. Resting is idempotent, so resting twice on the way in costs nothing.

The soul count is `SoulCounter`, an autoload drawn after the Dark Souls games' counter: a compact dark plate with silver double rules and diamond ornaments, the soul icon in a singly framed square at its left end, and the number counting to each new value. Bonfire, World Map and Encounter each call `ShowBelow(titleBar)` in `_Ready`, which hangs it under the right end of their title bar, and `Release(titleBar)` in `_ExitTree`. It is `Release`, not `Hide`, because scenes are swapped by adding the next one before freeing the last: the outgoing scene's exit runs *after* the incoming scene's show, and must not take down a counter it no longer owns. It polls `SoulCache.current`, so nothing has to push changes to it. The World Map's view buttons sit 50px lower to make room for it.

`BonfireRestAnimation` plays the transition, styled after the games: a vignette shader closes the world in around the fire, a radial glow flares in two beats like a heartbeat (with a looping flicker tween on top), embers rise from the bottom edge (`CPUParticles2D`, additive), the screen burns out to black, and "Rested" fades onto a dark band with a warm glow pooled behind it; the band creeps up in scale the whole time it is on screen — the same slow growth the games' banners have — while the words stay still, because a Label under a slowly changing scale re-samples its glyphs every frame and shimmers. The black then lifts on the dying glow. About four seconds, every timing exported. It is deliberately **two awaitable halves** (`FadeOut` / `FadeIn`) rather than one call with a callback, so `BonfireOptions.OnPressedRest` simply does the resting between them. That is the whole point of covering the screen: bars refilling and encounters reappearing happen unseen instead of popping. `FadeOut` waits on a timer for its total length rather than on the tween finishing, because the banner's scale tween runs on through the hold and into the fade back. It lives on its own `CanvasLayer` at layer 20 — above the portrait pane (5) and the equipment modal (10) — and its root Control blocks input for the duration, with the three buttons disabled as well. Every part is null-checked, so an unwired animation just means an instant rest. Rest grabs focus on load, so Enter rests.

The band and its glow are one shader (`Resources/Shaders/Banner.gdshader`) on a `ColorRect` rather than stacked gradient textures: the glow is an ellipse that falls to nothing well inside the rect, so it can never be cut off by an edge, and the band's top and bottom soften into whatever is behind. The vignette (`Resources/Shaders/Vignette.gdshader`) measures distance in half-screen-heights with an aspect correction from `SCREEN_PIXEL_SIZE`, so it stays round on a widescreen viewport; its `radius` is what the tweens drive. The embers are a `Node2D` inside a `Control`, so nothing anchors them — `PlaceEmbers` puts them along the bottom edge each time from the overlay's size.

The floor art (`Resources/Images/Backgrounds/BonfireFloor.jpg`, 1920×1080) is generated, not painted: `Tools/BonfireFloor/generate.py` draws a ring-paved slate floor lit by the fire and composites the bowl cut out of `Tools/BonfireFloor/bowl_reference.jpg` (the backdrop is colour-keyed, then clipped to an ellipse plus a stroke for the sword). `ZOOM` in the generator is how far back the camera stands (0.55 now; 1.0 was the original framing where the bowl filled a third of the screen) and scales every size in the picture together — ring width, bowl, light falloff, shadow. The pit is painted right of centre so the portrait pane on the left never covers it, and the options column sits to its right. The generator prints the flames' UV, which `BonfireFireLight.fireUv` has to match. The old `Bonfire.png` board tile is no longer used by the scene but is still in the project. The scene draws the art full-bleed with `KEEP_ASPECT_COVERED`, so nothing depends on where the pit lands on screen.

`BonfireFireLight` keeps the fire alive while the screen idles: an additive glow over the flames flickering on a sum of sines plus eased random jitter, a wide additive warmth over the floor on a slow pulse, and `CPUParticles2D` sparks lifting off the pit. All three hang on `fireUv`, one point in the floor art; every frame it works out where `KEEP_ASPECT_COVERED` actually drew the texture inside the `TextureRect` and places the light there, sizing the glows as fractions of the drawn height and scaling the particle node the same way, so the fire stays lit at any window size. It lives as a full-rect child of the art itself (so their local spaces coincide) and before `Options` in the tree so the buttons draw over it.

⚠️ `TextureRect.expand_mode = FIT_HEIGHT_PROPORTIONAL` (5) derives a **minimum height** from the node's width. The bonfire art is 1464×1472, so it claimed a 925px minimum, the `VBoxContainer` above it inherited it, and the `AspectRatioContainer` — which cannot shrink below its child's minimum — ended up 987px tall in a 648px viewport, centred, with the title bar 169px off the top of the screen. Background art wants `IGNORE_SIZE` (1) plus a stretch mode; a proportional expand mode on a full-bleed image will push the rest of the scene off-screen. Both `AspectRatioContainer`s now also use `stretch_mode = 2` (FIT) so they can never exceed the viewport.

### Souls
`SoulCache` is the party's shared pool, stored on `SaveGame` so it survives the trip back to the bonfire. Every call no-ops without a save, so the encounter still runs standalone.

A wipe drops the **whole** cache on the node where the first character fell (`EncounterManager.deathGridIndex`, captured in `PlayerToken.CheckDeath`). The drop is pinned to both a world node id and a grid index, because the encounter is rebuilt from scratch every time it is entered — `ActionListener.RestoreSoulDrop` puts the pile back on re-entry and highlights it, and walking a character onto it calls `EncounterManager.TryRetrieveSouls`. A second death before retrieval discards the old pile rather than stacking it (p19).

### Conditions
Everything in the p21 lifecycle is wired: applied on any hit through `CombatResolver.Apply` (a 0-damage hit still applies), consumed or expired on the right schedule, and reflected by the token icons. There is no separate condition readout — the icons are the UI.

- **Bleed** lives in `ApplyDamage` on both `Enemy` and `PlayerToken`, fires only when damage actually lands, and the apply order in `CombatResolver.Apply` (damage first, condition second) is what lets a bleed attack consume and reapply in the same hit.
- **Poison** deals its 1 damage inside `ClearVolatileConditions`, i.e. at the end of the afflicted model's *own* activation.
- **Frost** costs the character +1 on every walk, run **and dodge** (`PlayerToken.NextStepCost`, `CombatResolver.DodgeStaminaCost`) and trims an enemy's Move icon by 1 (`EnemyMovement.NodeCount`).
- **Stagger** costs the character +1 per weapon action (`CombatResolver.AttackStaminaCost`) and drops an enemy's attack damage by 1 (`CombatResolver.AttackStrength`).
- `ClearVolatileConditions` (end of activation) drops poison/frost/stagger; `ClearAllConditions` (end of encounter, from `ActionListener.EndEncounter`) drops everything including bleed.

Equipment immunities are honoured for characters only (`PlayerToken.ApplyCondition` checks `Player.GetImmunities()`); enemies carry no equipment so nothing grants them immunity.

**What counts as a hit** (Matt's ruling, Oct 2026): every attack hits unless it is **successfully dodged**. A Block or Resist that brings the damage to 0 is still a hit, and so is the V2 failed dodge's armour block. Enemies never dodge, so a character's attack always hits. On every hit the attack's condition is applied **and** any Push happens, whatever the damage (`AttackOutcome.hit`, `CombatResolver.Apply`).

**Only the armour and the two hands count** (p25: "the character's armour and hand slots"). `Player.HeldWeapons` is the one list every gear reading uses: dodge dice, Block/Resist dice, immunities, push immunity, passives and the summary panel's numbers, plus `CombatResolver.RollDefence`, the cross's Block highlight and `Character`'s template summaries. A weapon in the backup slot is carried, not used, until it is swapped into a hand.

**Push immunity** is `pushImmune` on `Weapon` / `Armour` (`Player.IsPushImmune`, checked by `Pushing.Shove`), not a `StatusEffect`. Push is not a condition, and adding it to that enum would renumber every value already saved in the `.tres` files. It once was in the enum (`…STAGGER, PUSH, NONE`); when it was taken out, Black Iron Greatshield's stored `[4]` silently became `NONE`. `Tools/onboard_equipment.py` now uses the enum's real numbering and writes `pushImmune = true` for "Immune to Push".

`GameNode.statusEffect` makes a node a hazard: `EncounterManager.ApplyNodeHazard` applies it to anything that steps on. **This is not a rule from the book** — the book's node-level hazard is the Trap token (p18), which damages characters and ignores enemies. Treat hazard nodes as this project's own idea.

### Enemy Behaviour Execution
`Enemy.ProcessNextMove` walks the behaviour list left to right (p24). A behaviour is movement when `isLeap || direction != 0` and an attack otherwise. `EnemyMovement.Plan` turns a Move icon into a node list: the icon's number is a **node count**, not "go to the target", so Move 2 takes two steps and stops. Negative `direction` retreats greedily, stopping early when no neighbour is farther (the corner case the rules call out). Frost trims the count by 1.

Attacks resolve in place: out of range misses entirely and has no effect (p25), the Node icon hits every character sharing the target node, and pushes shove characters off each node the enemy enters, dealing the movement attack's damage first. Targeting follows the skull/ring icon, and ties on "nearest" go to the aggro holder, then to the higher `Character.taunt` (p24).

Attacks are `async` because each one asks `DodgePrompt` and waits: the enemy's whole activation suspends until the defender picks Block or Dodge, then again on the roll reveal, and on a successful dodge again on the defender's free step.

The Block-or-Dodge question lives **in the action bar**, not over the board: `DodgePrompt.Ask` only rings the two tokens (`TokenHighlight`, red attacker / gold defender), gives the attacker's activation-bar entry a red rim (`TurnQueueEntry.SetAttacking`) and awaits `CharacterActionBar.AskReaction`. The bar swaps to the defender, lights every piece of gear that rolls defence on the equipment cross (`EquipmentCross.SetDefending`), shows the attacker's face with the attack icon carrying its real strength (`CombatResolver.AttackStrength`, so Stagger shows), the damage as red cubes and any condition icon, and turns the rows into **Block** (icon, its dice chips, the damage range that can still get through) over **Dodge** (the dodge dice pool, the difficulty, the stamina cube, the chance). Dodge is greyed when the pool cannot reach the difficulty or the stamina cannot be paid — paying to fail is not a choice. Hovering either ghosts its consequence onto the endurance bar: expected damage pulsing hard, worst case softly beyond it. The icons in `Resources/Images/Sprites/Combat/` are the rulebook's own vector icons (p25), rendered white so the scene tints them. `Enemy.Walk` awaits each node it enters for the same reason — arriving on a node can open that prompt.

A successful dodge offers the one-node step (V2: **1 stamina**, optional, only offered if it can be paid): `Enemy.Strike` awaits `CharacterTurn.OfferDodgeStep`, which lights the adjacent nodes and puts the defender on the bar with the End button relabelled **End Dodge**; a node click pays and takes the step (through `MovePlayer`, hazards and the push prompt as usual) and either that or the button ends it. `ActionListener.OnNodeClicked` routes clicks to it before checking the phase, because it happens inside the enemy phase.

⚠️ In a hand-edited `.tres`, every property of `[resource]` must come **after** its `script = ` line. Godot applies them in order, and a property set before the script exists is silently dropped. `heroicAction` was once inserted above it and every class loaded as `NONE`.

⚠️ **Never give a `Resource` subclass a parameterless constructor that sets anything.** Godot strips every property matching the *field-initializer* default when saving a `.tres`, then reconstructs through the parameterless constructor on load. `EnemyMove` chained to its full constructor (`towardsAggro=true, damage=1, direction=1`), so a saved `direction = 0` was stripped and came back as `1` — silently turning every enemy attack into a second move. `PlayerMove` had the same trap with `repeat`. Both are now `{}` and defaults live only in field initializers.

### Combat Resolution
`CombatResolver` is deliberately free of turn order, targeting and UI — the turn loop decides who swings at whom and pays the Stamina, then calls in. Rolling and resolving are separate methods because the Node icon rolls **once** and compares that single total against every enemy on the node (p23), so the roll has to exist without being spent.

Character attacks roll pips + `modifier` and subtract the enemy's Block or Resist. Enemy attacks are a fixed damage value the defender rolls to reduce. A hit at 0 damage is still a hit (p20) and still applies its condition. Bleed is applied inside `Enemy.ApplyDamage` / `PlayerToken.ApplyDamage`, and only fires when damage actually lands — 0 damage is not "suffering damage".

Dodging (p25) uses `DodgeDice`, which extends `Dice` with faces `0,0,0,1,1,1` — three blanks and three icons, so each die is a coin flip and a roll *sums* to the number of icons. Summing rather than flagging is what makes a dodge difficulty of 2 or more work. `dodgeAbility` on armour and weapons is the size of that pool. A dodge replaces the Block/Resist roll entirely and is all or nothing: succeed and the character is not hit at all (no damage, no push, no condition); fail and they take the **full** damage with no defence roll. `DiceUtility.DICE_TYPE` gained a `DODGE` member, appended so existing BLACK/BLUE/ORANGE ordinals in `.tres` files still hold.

### Roll Reveal
Nothing lands before the player has seen the dice. `CombatPresenter` is the layer between the turn loop and `CombatResolver`: it rolls each die by hand (so the faces exist), builds a `RollReveal.View` — the actor (weapon art, or the Block/Resist/Dodge icon), the dice, the modifier and total, and a line per model affected (portrait, the Block/strength/difficulty badge, the damage as red cubes or the dodge icon on a success) — awaits `RollReveal.Present`, and only then calls `CombatResolver.Apply`. `CharacterTurn.PickTarget` / `ResolveAgainstNode` and `Enemy.Strike` all go through it; `CharacterTurn` disarms and clears the highlights *before* awaiting so nothing on the board answers a click during the reveal, and `FinishAttack` re-checks that the activation still exists afterwards.

`RollReveal` is a `CanvasLayer` at 12 (above the portrait pane, below the card viewer) with a scrim that blocks the board: the dice (`RollDie`) are **thrown onto the tray** a beat apart (`settleStagger`): each is a six-sided cube wearing the printed die's six faces, which flies in from the upper left tumbling about two axes, bounces twice, and comes to rest with the face it rolled towards the viewer, a shadow underneath the whole way — then the total counts up and pops, the lines appear and **Continue** (focused, so Enter works) hands control back. The throw is the same choreography every time (with a little scatter per die) and only the landing face is decided, before the throw, the way Baldur's Gate 3 casts its d20: the rolled face is put on the front of the cube and the two spins wind down to nothing, so whatever they were the front ends up facing out. The cube is drawn, not modelled: its corners are turned by a `Basis`, given a little weak perspective, and each side facing the viewer is drawn as a textured quad (`DrawPolygon` with UVs into the sheet) shaded by its angle to a fixed light, with inked edges and a gold front once landed. The faces are the printed dice's own: `DiceArt` reads `Resources/Images/Sprites/Dice/<Black|Blue|Orange|Green>.png`, 3×2 sheets of 100px cells cut by `Tools/DiceFaces/generate.py` from Matt's photos of the real dice (kept in `Tools/DiceFaces/source/`), untouched — a sharpening pass was tried and looked worse than the photos. Each cell's pip count is listed in `DiceArt`; a rolled value picks a random cell showing that many for the front, the other five cells take the other sides, and a value with no printed face falls back to a plain cube with the number. When the roll is an enemy's attack on a character (`View.againstCharacter`) and damage is coming, the screen edges flash red as the numbers land (`ScreenPunch.Flash`, lighter than the contact punch and without the jolt). It registers itself in `EncounterManager.rollReveal` on ready; it sits after the board in `Encounter.tscn`, so the registration lands after `ActionListener`'s `Reset` rather than being wiped by it. With no reveal in the scene the presenter skips straight to applying.

### Attack Animation
`AttackSlash` is the stroke an attack makes on the board, drawn additively in board space (a child of the Encounter control, so it zooms, pans and clips with the board). It has **two awaitable halves**. `Windup` brings the attack to the brink and holds it there, quivering, with the attacker's token leaning in. `Land` lets it go and returns **at the moment of contact**, having fired the impacts — a white burst with streaks, a shake and a red tint for a hit; a Block/Resist-coloured ring for a hit fully absorbed; a sidestep for a dodge — and `Settle` waits for the follow-through and fade. It frees itself afterwards.

It takes one of three **forms**, picked from the data by `AttackSlash.FormFor(magic, range)`: a magic attack or a Spell-type weapon casts an **orb** (gathers in the caster's hand with sparks spiralling in, pulses through the hold, streaks to the target and bursts); a physical attack with range 2 or more looses an **arrow** (nocked and drawn back at the archer with a faint aim line, the hold is the aim, then it flies the line and stays in the target); anything else swings a **blade** (a drawn sword on an arc pivoting at the attacker's hand, trailing a crescent of light, held just short of the target). Nothing is per-weapon; a character's range is the option's own range or the weapon's (p23), an enemy's is the behaviour's. Enemy strokes are ember red, character strokes gold-white, spells violet either way. The projectile flight is timed by distance (`flightSpeed`, clamped 0.1–0.3s), and a dodging defender's hop is timed to be clear at contact.

The hold is the point. `Enemy.Strike` begins the swing **before** `DodgePrompt.Ask`, so the attack waits through the Block-or-Dodge choice and the roll; the presenter begins a character's swing itself. While an enemy's attack is held the camera creeps in on the two tokens (`BoardCamera.Focus`, a slow ease over `focusSeconds` to `focusZoom`) and eases back out on Land (`Release`). The view the player had is kept and restored, unless they zoom or pan meanwhile, which ends the focus and keeps their view (`TakeOver`). In every path the order is `Windup → Reveal → Land → Apply → Settle`, so the outcome is applied in the same frame the impact fires and nothing lands before the dice have been seen. An enemy's swing is passed into `CombatPresenter` as a parameter for that reason. `Begin` returns null when there is no board, and every caller null-checks.

Token offsets (the lean, the shake, the sidestep) are tweened on the token's **button**, never its wrapper `Node2D`: `FixPositioning` owns the wrapper's position and `RescaleModels` its scale, and a tween there would fight both. The tweens are created on the token, so they die with it — a killed enemy is `QueueFree`d at contact and vanishes under the burst. The badges at `ZIndex = 1` draw over the stroke, the same trade the highlight rings make.

### Board Effects
Everything drawn on the board beyond the tokens is procedural, lives under `Scripts/Encounter/Fx/`, and is parented under the Encounter control through `BoardFx.Spawn` so it zooms, pans and clips with the board and draws over the tokens (the badges at `ZIndex = 1` excepted). Each effect is a `Node2D` that draws itself in `_Draw`, runs on its own clock and frees itself. `BoardFx` also holds the camera and the punch layer, registered on ready and cleared by `EncounterManager.Reset` like the prompts. No sprite files: keep new effects that way unless art is asked for.

- **Numbers** (`BoardFloat`): damage rises red from `Enemy.ApplyDamage` / `PlayerToken.ApplyDamage` (the amount actually applied, so bleed's +2 and the last-box clamp are honoured, and poison's tick shows), stamina recovered rises gold from `BeginActivation`. Numbers, never words.
- **Enemy death** (`EnemyDeath`): the real token is still `QueueFree`d the frame it dies — `GetEnemy`'s "queued for deletion is gone" contract is untouched — so a ghost copies its art and global transform and does the dying: greys and shrinks, a pale wisp lifts off, embers scatter and sink. No flight to the soul counter, because souls are paid on the win (p19), not per kill.
- **Piece movement** (`TokenMotion`): `FixPositioning` still places wrappers instantly. `EncounterManager.MovePlayer` records where every occupant of both nodes was drawn, does the reparent and re-pack as before, then glides each one's **button** from there to rest — the mover lifted (scale 1.14 and back) like a piece picked up and put down, a pushed one (`stumble`) overshooting. The intermediate nodes of an enemy's walk are crossed by `TokenMotion.Travel`, which moves the wrapper itself so nothing on the way is re-packed (and so a 4th model never lands on a full node, which `FixPositioning` cannot place); the last step goes through `MovePlayer`. Anything that wants to own a button's offset (the attack lean) calls `TokenMotion.Settle` first.
- **Aggro handoff** (`AggroHandoff`): `SetAggroHolder` hides the old badge at once and flies a copy to the new holder, which only shows its own badge on landing (`RefreshAggro` + `PopAggro`). With no previous holder the badge just appears.
- **Conditions** (`ConditionBurst`, `PopIn`): `ApplyCondition` on either model bursts only when `HashSet.Add` says the condition is new — tinted rings from the token and the icon rising after it. The portrait strip and the activation bar pop an icon that has just become visible, but not on their first fill.
- **Heavy hit** (`ScreenPunch`, `BoardCamera.Shake`): 3+ damage to a character flashes a red edge vignette (the bonfire's shader, held wide) and jolts the board. The shake is an offset added on top of the clamped pan in `Apply`, never folded into it.
- **Buttons** (`ButtonFx`): every attack row and the End Turn button get a hover glint and a press ripple; the armed row's border breathes at `GameNode.pulseRate` so it beats with the lit target nodes. `CharacterTurn.OnAttackChosen` now refreshes the bar so the armed row is rebuilt as armed. End Turn glints once as it becomes enabled.
- **Endurance cubes** (`TickBar`): a cube slides in from its side (black from the left, red from the right, an enemy's lost health from the left) and the covered tick flashes; an uncovered cube slides back out. Only a change animates — the first fill, and a bar handed a different `Endurance`, is silent, so switching the action bar between characters does not shower cubes.

### Character Tokens
Each character has three tokens on their board: **Estus Flask**, **Heroic Action** and **Luck**. Each is spent once, shows flipped (`TokenArt.Used`, the `*Used.png` art beside the ready art) and comes back only on a bonfire rest (`CampaignManager.RestParty` → `Player.RefreshTokens`). The spent flags live on `Player` and are `[Export]`ed so they survive between encounters, unlike the endurance bar. The action bar shows all three as they stand for whoever it is showing.

- **Estus** clears the whole endurance bar, stamina and damage, as a rest does (`PlayerToken.DrinkEstus`). It is drunk from its token on the action bar by the **active** character during their own activation, and is only offered with something to restore, so it cannot be wasted on a clean bar.
- **Luck** rerolls one die of a roll the character made: their attack, or their Block/Resist or Dodge against an enemy. Each `RollReveal.View` names its `luckOwner` and carries a `recompute` callback. After the dice land, the owner's Luck token appears at the end of the dice row and every die lights under the cursor (`RollDie.SetPickable`). Clicking one flips the token and re-throws that die onto its new face (`RollDie.Reroll`). `recompute` then rebuilds the total and lines, and the presenter reads the outcome from the view once the reveal closes. So nothing is decided before the player has passed on Luck. Continue passes on it.
- **Heroic Action** is set per class as `Character.heroicAction` (`Heroic.Kind`, stored as an ordinal, so only ever append to the enum). Each class has its own, as given by Matt from the character boards (Sept 2026):

| Class | Heroic Action | Effect |
|---|---|---|
| Herald | Perseverance | Each character gains 2 stamina |
| Assassin | Backstab | Only after making a successful dodge, may attack the enemy dodged, for free |
| Warrior | Berserk Charge | May move 1 node for free, and one of their 0-range attacks gains AOE (Node icon) and costs 0 stamina for that turn |
| Knight | Stand Fast | After blocking, may add 1 blue die to the blocking roll |
| Deprived | Combat Versatility | Change equipment as if outside of combat |
| Sorcerer | Spell Fury | A magic attack gains infinite range and costs 0 stamina |
| Pyromancer | Explosive Firepower | A magic attack gains 1 black die |
| Cleric | Keep the Faith | Heal 2 damage from every character within 1 range of the Cleric |
| Mercenary | Rapid Strike | May make an additional attack, for free |
| Thief | Lucky Break | Gain 2 stamina, heal 2, and reset their Luck token as if rested at a bonfire |

  How they are played (Matt's calls, Sept 2026):
  - **From the Heroic token on the action bar, during the character's own activation** (`CharacterTurn.UseHeroic`): Perseverance, Keep the Faith (Chebyshev distance ≤ 1, the Cleric included), Lucky Break, and Combat Versatility. Combat Versatility opens the `EquipmentModal` locked to that character (`Open(player, lockToPlayer: true)`: no portrait or arrow switching) and awaits its `Closed` signal.
  - **Boosts** (Berserk Charge, Spell Fury, Explosive Firepower, Rapid Strike) are armed on use (`PlayerToken.pendingHeroic`) and spent by the **next** attack they apply to: a magic attack for Spell Fury and Explosive Firepower, a 0-range option for Berserk Charge, any attack for Rapid Strike. An unspent boost is lost at the end of the activation. `AttackTerms.For` is the one place a boost changes an attack. The action bar rows, targeting (`CharacterTurn.ArmedTerms`), payment and the roll all read it, so a boosted row shows its real cost, range (∞ for unlimited) and dice, and carries the Heroic token as a marker. Rapid Strike's attack may reuse a weapon already swung this activation, and does not use up the weapon's one attack (`RecordAttack(weapon, usesWeapon: false)`). Berserk Charge's free node (`PlayerToken.freeSteps`) costs nothing, is not the Walk, and may be taken even after attacking.
  - **Stand Fast** is offered on the Block/Resist roll reveal after the dice land, next to Luck (`RollReveal.View.heroicOwner` / `heroicDie`, `RollReveal.OfferTokens`). Clicking it throws one more blue die and the result is recomputed. It is offered on Resist as well as Block, because the reaction the player chose is the same one.
  - **Backstab** is offered after a successful dodge **and** its free step (`Enemy.Strike` → `CharacterTurn.OfferBackstab`). The bar goes into a Backstab mode (`CharacterActionBar.ShowBackstab`): the Assassin's rows at 0 stamina, lit only where an option reaches the enemy dodged, and End relabelled Pass. Weapons already used do not matter; it is outside their activation.
  - ⚠️ Backstab can kill an enemy **during its own activation**, while its behaviour loop is still running on it. So `Enemy.ApplyDamage` does not free an enemy that dies while `isActivating`. It sets `isDead`, hides it, and `ProcessNextMove` / `Walk` / `Attack` / `EnterNode` check `isDead` after every await, finish the activation (the signal still arrives) and only then free it. `GetEnemy` treats `isDead` as gone, so targeting, pruning and the win check already ignore it. The next `BeginEnemyActivation` prunes it and checks for the win.

### Test Bench
Running `Encounter.tscn` on its own (no campaign party) uses its `demoParty` and `enemies`, and starting gear skips the stat check there. Both are set up to test dodge and push:
- **Party: `Tester`** (`Resources/Prefabs/Player/Tester/Tester.tres`), not a real class. ⚠️ **Currently swapped to test Shift, Repeat and the backup swap:** **Dancer's Enchanted Swords** (two-handed, Shift 1 before the dice, x2) in the hands, and **Force** and the **Estoc** in backup (via `Character.extraBackupDefaults`), so the carousel has two weapons and a two-hander can be swapped both ways. The dodge/push loadout it is described with below is Force + Target Shield in the hands and the Estoc in backup. An Assassin holding **Force** (option 1: 0 stamina, range 2, no dice, Push, so it shows a 0-damage hit still pushes) and the **Target Shield**, with the **Estoc** in backup and **Assassin Armour**. That gives 2 dodge dice (armour + shield), 1 black die Block/Resist on the armour for the V2 failed-dodge roll, and Backstab. No class can equip a Push weapon at starting stats (Force needs Intelligence 12; the best start is 11), which is why this exists.
- **Enemies:** **Skeleton Soldier** (Move 1 with Push for 2, dodge difficulty 1, 1 health: movement push and dodging it), **Silver Knight Swordsman** (Move 2, then a 5-damage Push attack at range 0, dodge difficulty 2: attack push and failed dodges), **Sentinel** (Move with Push for 0 damage, 10 health: starting-node push).

To go back to a normal standalone run, set `demoParty` to a real class (it was the Knight) and `enemies` back to two Hollow Soldiers.

### Player Model
`Player` is the campaign-persistent Resource (stats, equipment ids, level) and `PlayerToken : TextureButton` is the character on the board, exactly mirroring the `EnemyData` / `Enemy` split. `ActionListener` spawns one shared `PlayerToken.tscn` per party member and assigns the `Player` before parenting it.

Encounter state lives on `Player.endurance` and `Player.conditions`, both deliberately **not** `[Export]`ed — the bar clears on victory (p19), so it must never be written into the saved character. It is a plain `Endurance` object rather than a Resource so it stays out of serialisation entirely. `Player` is per-character (one per party member from `CampaignManager.Players`), so mutable runtime state on it is safe in a way it would not be on a shared template like `EnemyData`.

The party comes from `CampaignManager.Players`; when that is empty the encounter falls back to `ActionListener.demoParty` (a list of `Character` resources) so the scene can be run standalone. The portrait pane only knows `CampaignManager.Players`, so a standalone run hands it that fallback party through `CharacterPortraitPane.standaloneParty` (cleared in `_ExitTree`). Without it the test bench drew no portraits, and since tokens carry no badges, a character's conditions showed nowhere.

### Pacing and Targeting Readouts
Each phase opens on a `PhaseBanner` (BG3-style: fades in, holds, fades out; a dimmed red glow for "Enemy Turn", dimmed gold for "<Name>'s Turn" with the activating character's name). `ActionListener.BeginEnemyActivation` / `BeginCharacterActivation` are `async void` and await it, so nothing moves until the banner is gone. The enemy banner shows once per enemy phase (at `activeEnemyIndex == 0`), the adventurer banner once per activation.

`EnemySpotlight` previews every enemy behaviour **as it comes up**, not the whole activation up front, so the preview is always accurate even after a push or dodge step. It shows the enemy ringed, the nodes of the planned path lit (the destination strongest) with a bump of light running down the path from the enemy towards its destination over and over (`pulseNodesPerSecond`), and the target ringed red. Attacks light the target's node. It is awaited from `Enemy.ProcessNextMove` (before `path` is set, so `_Process` does not start walking), `Leap` and `Attack`. Out-of-range attacks are not previewed, since nothing happens. It holds `moveSeconds` / `attackSeconds` and a click or Enter skips it. It stores each node's highlight and puts it back afterwards rather than clearing, because the soul pile lives in the same highlight.

Tokens: the character token wears the printed **Aggro token** at its top-left (`PlayerToken.RefreshAggro`, already called by `EncounterManager.SetAggroHolder`). The scan has black corners, so `Resources/Shaders/CircleMask.gdshader` cuts it to a disc, through one `PlayerToken.AggroMask` material shared by every place it shows. It also wears a drawn silver reticle **Nearest marker** at its top-right. Aggro also shows in the portrait's condition row and as a badge on the action bar's avatar. `NearestMarker` decides who wears Nearest. Hovering an enemy, on the board or its face in the activation bar, shows who that enemy counts as nearest (`Enemy.NearestCharacter`, the same `NearestOf` with the same aggro-then-Taunt tie-breaks its behaviours use). During a spotlight whose behaviour targets "nearest" the marker is pinned on that target. A hover wins over the pin. Both badges are in the `TokenHighlight.ABOVE_RINGS` group, and `TokenHighlight.Attach` inserts a ring *before* them in child order, so a ring never draws over them. ⚠️ Not `ZIndex`: a raised z-index also lifts them over the Push Prompt and every other modal that shares the board's canvas layer.

### Encounter HUD
**Board tokens carry no chrome** beyond the Aggro and Nearest badges. `Enemy.tscn` and `PlayerToken.tscn` are an avatar and nothing else — a token is roughly half a grid cell, and three can share a node, so there is no room to read anything there. Every readout moved to the two bars, and the tokens keep only the state:
`Enemy.isActivating` / `PlayerToken.isActivating` are flags the bars read, not rims they draw.

`TurnQueue` is the **Enemy Activation** bar: the round count in the top-left corner and one `TurnQueueEntry` per enemy in threat order, centred. It has no title and no background — the board runs under it when zoomed. There is no Party entry — every enemy acts between every character activation, so the party's place in the order is fixed and drawing it says nothing.

Each entry carries what the token used to: a 3:4 portrait cropped from the card art (`EnemyData.GetPortrait`, region `portraitRegion` in card fractions — nudge it per enemy when the art is off-centre), a **diamond** threat badge (rotated 45° with the label counter-rotated, matching the printed card's corner), an **ember rim at tier 2+**, a **solid** health bar with the value over it (`TickBar.continuous`: ticks would make a boss's health unreadable; a hit leaves the lost part lingering pale before it drains, as the games' bars do, and it drains from the right), condition icons, and a damage veil. The name is one size smaller than it was (8) and may run 10px past each side of its 60px entry into the 22px gap (`Name Box`), so names up to ~80px fit. Only the three longest Silver Knight names still end in an ellipsis. Spent enemies dim and grey.

`TurnQueue` is still a pure mirror of the turn loop, but the entries **poll** their enemy: health and conditions change from attacks, pushes and poison ticking at end of activation, and a missed push-refresh would leave a dead enemy looking healthy.

End Turn lives on `CharacterActionBar`, not here, so the bar stays free of controls. **Hovering** a face (Matt, Oct 2026; it used to be a click) shows `EnemyCardViewer` (a `CanvasLayer` at 15 inside `TurnQueue.tscn`) with the full printed card in the right-hand 30% of the screen, beside the board. It also puts a steady pale-gold `TokenHighlight` (`pulseSpeed = 0`, so it never reads as a target's pulsing red) on that enemy's board token, and shows its Nearest marker as before. The viewer takes no input and dims nothing, or it would steal the hover and hide the ringed token. Hovering the enemy's token on the board shows the card too (`EnemyCardViewer.current`, since tokens hold no reference to the bar; `Enemy._ExitTree` closes it if one dies under the cursor). Leaving the face closes it, and so does the entry leaving the tree (`TurnQueueEntry._ExitTree`), because the bar rebuilds freely and a freed face never reports the mouse leaving. There is no written-out inspect dialog any more; the card is the only enemy readout.

### Party Pane
The pane is now a small BG3-style strip (60×80 portraits, 68×90 when active): the active character's full readout lives on the action bar, so the strip only says who is in the party, how hurt they are and whose turn it is. `BoardCamera`'s insets in `Encounter.tscn` shrank with it.

`CharacterPortrait` is where a character's endurance, conditions and turn are read. Name **above** the art and the bars **below** it, so nothing sits on top of the portrait. The active character's art grows from `restingSize` to `activeSize`, gets a white frame (`activeBorder`), and a drawn white arrow bobs beside it on the right. The frame and arrow are children of `Frame`, not of the clipped `Art`, so neither is cut off by the portrait.

Damage is a translucent red layer that climbs the art from the bottom in proportion to `damageTaken` (the same treatment on `TurnQueueEntry` for enemies), so how hurt someone is reads without counting ticks.

⚠️ The endurance readout is **one** ten-tick bar, not a health bar plus a stamina bar. There is only one bar in the rules (p20): black ticks fill from the left as stamina is spent, red from the right as damage lands, and the uncovered middle is the capacity to do either. Two bars would imply two pools and hide exactly the tension the rule exists to create.

A portrait click means whatever the current scene says: in the Encounter it selects that character for the action bar (`ActionListener.SelectCharacter`), at the Bonfire it opens the equipment modal. The pane is an autoload that outlives every scene, so each subscriber **must** unsubscribe in `_ExitTree`. `BonfireOptions` once forgot to, and every portrait click in a later Encounter opened the character sheet.

The pane has a `Player`, never a `PlayerToken`, which is why `Player.conditions` sits beside `Player.endurance` and why `CharacterPortrait` asks `EncounterManager.characterTurn?.active` whose turn it is. Portraits **poll** rather than being pushed at, for the same reason the activation bar entries do.

### Pushing
Three things push, all through `Pushing` and all by the V2 rule: the pushed model goes straight on, away from the pusher.
- **Enemy movement with Push** (`Enemy.EnterNode`): each node it enters, every character there is struck by the movement attack if it has damage, then pushed onward along the enemy's step. A character who dodged that attack is not hit, so is not pushed. Before moving, a Move with Push also clears the enemy's **starting** node (p21), back against its first step and without damage (p25: movement attacks never hit the starting node).
- **Enemy attacks with Push** (`Enemy.Attack` → `PushAway`): after the roll, each character hit is pushed away from the enemy (p25). A successful dodge is not a hit (p20).
- **Character attacks with Push** (`CharacterTurn.PushHit`, from `PlayerMove.isPush`): every enemy hit and still standing is pushed away from the attacker. Enemies never dodge, so every target is hit.

When the pusher and the pushed share a node there is no "away", so the pusher's **last step** carries on (`Enemy.lastStep`, reset each activation; `PlayerToken.lastStep`, set by every move and dodge step). The candidates are the node straight on and the two 45° either side, kept only if walkable and with room (`Pushing.Destinations`). With two or three, **the players choose**: `PushPrompt.ChooseNode` lights them (pulsing, so they read as clickable), rings the model being pushed, and waits for a click on one. `ActionListener.OnNodeClicked` sends board clicks there first while it is open, before even the dodge step. With one candidate it is taken without asking; with none the model is not moved. `Pushing.Shove` is awaited for that reason, so an enemy's activation or a character's attack waits on the choice. While the choice is open the action bar holds End Turn disabled (`CharacterActionBar.LockEndForPush`, polled; it restores whatever the button was before), so a turn cannot end mid-push. A push never lands on a full node, so it never owes a further push.

The over-full-node push (p10, below) has no pusher, so it keeps its own destination rule (`EnemyMovement.PushDestination`).

### Node Occupancy
A node holds at most `EncounterManager.MAX_MODELS_PER_NODE` (3) models — but a full node is **enterable**, not blocked. p10: "If there are already three models on a node and another model moves onto that node, the players must push one of the three models already on the node." So `PathGrid.IsBlocked` is terrain only and full nodes stay pathable; `PathGrid.HasRoom` is the separate occupancy question.

`EncounterManager.ResolveOverflow` runs after any arrival and, if the node is now over the limit, asks `PushPrompt` which of the models that were *already there* gets shoved off. The rule gives that choice to the players whoever walked in, so an arriving enemy prompts exactly as an arriving character does; it auto-resolves when only one model could be pushed, and only ever fires on a node that was already at three.

For this over-full push the **destination** is auto-picked (first adjacent node with room, falling back to any walkable one). p21 gives the players that choice too — prompting for both would double the clicks, so only the "who" is asked. Pushes from attacks and enemy movement follow the V2 direction instead (see Pushing).

⚠️ An earlier pass had this wrong, treating full nodes as impassable. That is what made enemies deadlock around a cornered character. Do not reintroduce occupancy into `IsBlocked`.

### Node-to-World Mapping
Characters and enemies are children of `GameNode` (Control) nodes. `EncounterManager.MovePlayer` reparents a unit from one `GameNode` to another and calls `FixPositioning` to manually place up to 3 occupants. The grid is a 7×7 set of `Control` nodes, each with a `TextureButton` child named `Button` for click handling. `GameNode.ClickTarget` finds it **by name** — the models are children of the same node, so any positional lookup breaks as soon as something stands there. The button stays *under* the models (first child) so a model can be clicked; the node's only click handler is `ActionListener.OnNodeClicked`, which also handles entrance picking by phase (entrances used to add a handler per pick and never remove it, so clicking an entrance later spawned past the end of the party).

### Board Geometry
The 7×7 grid is a square lattice **turned 45°**: grid `(x, y)` sits `(x + y) - 6` steps right and `(y - x)` steps down from the centre of the map art. Only the diamond `|u| ≤ 3, |v| ≤ 3` — 25 cells — lands on the board; the 24 corner cells fall outside it and are flagged `isDisabled` and hidden. That diamond matches the 25 circles printed on `Tile 2.jpg` one for one, including their colour: the four **purple** circles are terrain on the printed tile, and the four **red** circles are the `isEnemySpawn` cells. The purple cells (`Node18`, `Node25`, `Node26`, `Node33`) were `isDisabled` but are **enabled for now**, at Matt's request. Only the 24 hidden off-board corner cells keep `isDisabled`. Re-flag the purple ones if terrain comes back.

Measured against the art (which the scene draws `flip_h`), a lattice step is **0.12162** of the board square and the lattice centre is **(0.49396, 0.49601)**. Each `GameNode` is anchored to those fractions with a box one step wide, so the engine keeps the grid on the printed circles at any window size; the highlight `TextureButton` inside is inset to 14% so the marker reads smaller than the cell, and needs `ignore_texture_size` or the 36px sprite sets a floor on how small a node can get.

**Presence** (Matt, Oct 2026): a beefier enemy stands larger. Its size is tiered health ^ 0.3 times a normal half-node token (`Enemy.Presence`, `PRESENCE_EXPONENT`): ×1.6 at 5 health, ×2 at 10, ×2.5 at 20, ×3 at 40. It is capped at ×4 (`MAX_PRESENCE`), a disc two lattice steps across. That cap is set by the bosses to come: neighbouring nodes sit √2 steps away, so even a capped token clears their circles. `Presence` reads the card and tier, not `maxHealth`, because the token is scaled before `_Ready`.

`FixPositioning` owns every model's position **and** scale. Below ×3 (`OVERLAP_PRESENCE`) nothing overlaps (Matt). Each model gets the diamond of points nearer its node than any neighbour, one node width from the centre each way. `Pack` lays the models out by their real sizes (in a triangle, a triangle with the big one at the front, or a row with it in the middle) and keeps whichever needs least shrinking. When they cannot fit, all of them shrink together, so a big enemy is at full size whenever it stands alone. With equal tokens the triangle is the old quarters layout. From ×3 up (bosses), the boss takes the middle at full size and the others stand along its front edge, over it. That overlap is deliberate, for the boss-arc mechanic to come. The biggest model is drawn first.

`Enemy._HasPoint` is the art's disc, because a big token's square corners lie over the neighbouring nodes and would steal their clicks (`OnClick` passes a click to the enemy's *own* node). The generator gives each 8+ health enemy (`BIG_PRESENCE` 1.8) a spawn node of its own while nodes are spare. On the world map the toughest enemy's disc grows the same way, compressed to radius 20–28.

**Looming** (from ×1.5, i.e. 5 health), all scaled by `TokenMotion.Weight` (0 at ×1, 1 from ×2.5):
- **Shadow** (`LoomingShadow`): a child of the enemy's button with `ShowBehindParent`, so it travels, leans, hides and frees with the token. It is a dark violet-black pool and a fog of soft puffs that well up from under the token's edge, drift outwards, swell and thin away, each on its own slow cycle (Matt preferred fog to the first version's tendrils). The token's printed rim covers its inner part, so everything has to reach well past `r` to be seen.
- **Heavy walk** (`TokenMotion.StepSeconds`): a heavy enemy's own steps take up to 0.35 × (presence − 1) longer and barely lift (`HEAVY_LIFT`); models re-packed around it keep the normal pace. `Travel` walks at the enemy's alone size, centred, so one squeezed onto a crowded node grows back as it walks off.
- **Footfalls** (`Footfall.Land`, after each walk step and a leap): a squashed dust ring and motes, plus a board jolt of 1–3.5px.
- **Heavy blows**: `ScreenPunch.Hit(damage, presence)` jolts harder and longer by the attacker's size. From ×2 even a 1–2 damage hit punches (`PlayerToken.HEAVY_BLOW_PRESENCE`). The token shake on impact grows by √presence. The attacker's presence reaches `PlayerToken.ApplyDamage` through `CombatResolver.Apply(…, attackerPresence)`.

The square board `BoardCamera` fits is what makes those fractions safe — without a square board the lattice would shear. The models on the nodes are **not** anchored, so `PathGrid.RescaleModels` re-scales them (via `EncounterManager.ScaleToken`, half a node each) and re-packs them on every resize.

**Zoom and pan** live on `Board View` (`BoardCamera`), a clipping `Control` covering the whole Stage under the title bar, with the `Encounter` node inside it. At rest the board is a square fitted into the *safe rect* — the space between the Turn Queue, the Action Bar and the two side insets — so no HUD covers it. `BoardCamera` sets the board's `Size`, `Scale` and `Position` itself (the `Encounter` node is not anchored), which leaves the grid's fractions, `RescaleModels` and clicks unaffected. Zoom runs 1×–3× about the cursor and the board then runs under the HUD like the world map; the pan is clamped so some of the board always stays under the centre of the safe rect. Left-drag pans only when it starts off a button, so node and token clicks keep working; right/middle-drag pan from anywhere; `R` or the title bar's Reset View resets. Anything that moves a model in global space must go through the node's transform (`node.GetGlobalTransform() * localPoint`) — adding a local offset to a `GlobalPosition` is wrong once the board is scaled, which is what the enemy's old frame-by-frame walk used to do. `TokenMotion.Travel` recomputes both ends from their nodes every frame for that reason. `ActionListener` extends `Control` (it sits on one) so it can be the camera's target.

⚠️ The grid was originally hand-placed in pixel offsets against a differently-sized board, which is why nothing sat on a circle. Do not go back to fixed offsets — anything authored in pixels breaks the moment the window is not the size it was authored at.

### Encounter Layout
`Encounter.tscn` is Title Panel over a `Stage` that fills the rest. The Stage holds two full-rect layers: `Board View` (the board, see zoom and pan under Board Geometry) and on top of it `HUD`, a `VBoxContainer` of Turn Queue → spacer → Action Bar. The Turn Queue and Action Bar have **transparent** backgrounds (label outlines come from a small `Theme` on each), so the zoomed board shows underneath them; only the title bar stays opaque.

Everything in the HUD that is only layout has `mouse_filter = IGNORE` — including the rows `CharacterActionBar` builds at runtime and the anchor the equipment cross hangs from — so wheel and drag in the empty parts of the HUD reach the board. The action bar's translucent panel is the one exception: it looks like a panel, so it stops input. Anything new added elsewhere needs IGNORE, or it silently blocks zoom and pan over its area.

The Action Bar is an `HBoxContainer`: a `Cross Anchor` (IGNORE, sized to the cross, which hangs off its bottom-left and stands taller than the bar, over the board's bottom-left corner — deliberately, the way the games' HUD does) and then the translucent `Panel` holding avatar, character block, weapon column, souls and End Turn. It stays in the layout between activations, dimmed via `ShowIdle` rather than hidden, because its height is part of what `BoardCamera` fits the board around — a bar appearing and disappearing would resize the board, and every token on it, twice a round. The panel is a **pill**: bordered all round, with its corner radius set to half its height on every resize (`CharacterActionBar.RoundPill`), so the ends stay round at any size. The 112px avatar sets that height, and the avatar and End Turn sit concentric with the round ends. Everything else must fit inside it. The weapon column's minimum height equals the avatar, the header is 28px, and attack rows are 32px, dropping to 24px (`compactRowHeight`) when a weapon has three options. That is what keeps the reaction state and every weapon the same height as an activation. A `Right Gap` in the bar and a `Bottom Gap` under it in the HUD keep the pill off the screen edges. The Block and Dodge rows share three left-aligned columns (`Columns`): what rolls, the dice, then the outcome.

`CharacterPortraitPane` is an autoload floating over the whole window, so it has no idea what scene is up. Its portrait column is stretched most of the window's height on a canvas layer above the HUD, so it and each portrait's root are `mouse_filter = IGNORE`. As `PASS` the empty column under the portraits swallowed every click there, which is what stopped the equipment cross's left hand slot from being clicked. `ActionListener.PlacePortraitPane` tells it where this scene's HUD ends (`SetTopMargin`); `BoardCamera.leftInset` is what keeps the fitted board clear of it (`rightInset` matches it so the board stays centred). `EncounterDevControls` adds its Win/Die buttons to the title bar's `HBoxContainer` for the same reason — an anchored overlay landed on top of the Show Stats toggle.

### Pathfinding
A* is implemented in `Pathfinding.cs` using `Heap<PathNode>` for the open set. `PathGrid` (a Node in the scene) initializes the walkability grid from `GameNode.isDisabled` flags. Known issue: `openSet.UpdateItem` is commented out, which can produce suboptimal paths when a node's cost improves mid-search.

### Equipment System
`Equipment` is a C# interface with shared properties. `Weapon` and `Armour` are `[GlobalClass]` Resources implementing it. `PlayerMove` defines individual attack options within a weapon (stamina cost, dice array, range, flags). Characters can hold: 1 armour, 2 hand slots, and a backup slot that holds every weapon not in a hand (p12), at most `Player.MAX_WEAPONS` (3) weapons in all.

**Backup slot.** `Player.backupIds` is a list. `backupUpgradeIds` holds two per backup weapon in the same order, so upgrades travel with the weapon. The old single `backupSlotId` is only read, once, to migrate an old save into the list. `Player.shownBackup` is which one the cross and the loadout are showing: a view, not saved. `GetBackupSlot()` returns that one, so the single-slot UI kept working. Both show it as a carousel with arrows either side and dots for how many. The loadout's carousel has one more stop, an empty place to add a weapon while there is room. Filling an empty hand is refused when the character already carries three (`EquipmentModal.SlotAccepts`).

**Swap (p22).** During their own activation, until they first move or attack (`CharacterActionBar.CanSwapNow`, and not mid-Shift/Repeat), a character may click the backup slot (it and both hands light), then a hand. `Player.SwapBackupIntoHand` puts the backup weapon in that hand and what the hand held into the backup slot in its place. A two-hander coming in sends the other hand's weapon to backup, and filling the spare hand of a held two-hander puts the two-hander away (p12). The weapon count never changes. An armed attack is dropped, since its weapon may have gone.

### Rings
The 16 rings (Matt typed out 18 from the cards, Oct 2026; two were cut, below) are `Ring` resources under `Resources/Prefabs/Equipment/Rings/<Name>/`, written with their card art by `Tools/onboard_rings.py`, which holds the list and the requirements. Covetous Gold Serpent Ring, Life Ring and Binoculars are deliberately left out, and so are **Bellowing Dragoncrest** and **Great Swamp**, which only ever helped the Sorcerer's and Pyromancer's Heroic Actions (Matt: no class-specific rings). Matt moved all five cards' art to a "not implemented" folder; the two cut rings' `Ring.Effect` values stay in the enum, marked unused, so no saved ordinal shifts. A ring is worn in an armour upgrade slot (`Player.armourUpgradeIds`); `Player.GetRings()` only counts the slots the armour actually has. Equipping needs the ring's own stat requirements (the inventory greys it out otherwise). Each ring names one `Ring.Effect` (an ordinal in the `.tres`, so only append), and the code it changes asks `Player.HasRing` / `RingCount` right there:

| Ring | Where | Notes |
|---|---|---|
| Blue Tearstone | `CombatResolver.Apply(…, PlayerToken)` | 1 stamina after an enemy attack actually deals damage (not Poison) |
| Carthus Milkring | `CombatResolver.DodgeStaminaCost` | dodging costs 0, Frostbite included |
| Chloranthy | `PlayerToken.StartGain` | 4 instead of 2 at activation start, for the wearer and anyone on their node (Matt: replaces the 2) |
| Covetous Silver Serpent | `ActionListener` win payout | +1 soul per ring worn; boss wins still pay nothing |
| Dark Wood Grain | `CharacterTurn.OfferDodgeStep` | the dodge step may be 2 nodes for its 1 stamina (Matt) |
| Divine Blessing | action bar, beside the tokens | once per rest, in the wearer's activation: all damage and conditions off. Spent like a token (`Player.divineBlessingUsed`, refreshed by `RefreshTokens` on a rest) and shown darkened; the card was edited to say "once per rest, use this card to" in place of "permanently discard", and its Faith requirement raised from 22 to 30 (Matt) |
| Dusk Crown | `AttackTerms.For` | magic attacks cost 2 less; 1 self-damage once per attack (`PlayerToken.SufferOwnDamage`, which cannot kill: only enemy attacks and conditions do) |
| Hornet | `AttackTerms.For` | +1 orange die and −2 on every attack, Backstab included |
| Knight Slayer's | `CharacterTurn.KnightSlayer` | 1 stamina when the roll beats Block/Resist by 3+; once per attack, even on a Node attack |
| Magic Stoneplate | `Player.AddRingDefence` | +1 black die to Resist, in the armour-only (failed dodge) roll too |
| Obscuring | `CombatResolver.DodgePool` | +2 dodge dice against an attacker 2+ nodes away (range distance) |
| Red Tearstone | `AttackTerms.For` | +1 damage while 4+ damage is on the bar |
| Ring of Favour | `PlayerToken.EndActivation` | 1 stamina when 2+ attack rolls were made in the activation (`attackRolls`: each Repeat roll counts, a Node attack is one, Luck is not a new roll, Backstab is outside it) |
| Sun Princess | `PlayerToken.EndActivation` | 1 health at the end of the activation, before Poison ticks |
| Tiny Being's | `CharacterTurn.Begin` → `CharacterActionBar.AskStartGain` | with damage on the bar, the start gain waits (`pendingStartGain`) on a choice: all stamina, or one of it as health (Matt: asked each activation) |
| Wolf | `Enemy.Strike` → `DodgePrompt.Ask(peek)` | the dodge dice are rolled before Block/Dodge is asked; the Dodge row shows the icons, Dodge is greyed if they fall short, and choosing it uses that same roll (Matt: peek, then choose) |

**Rarity** (Claude's ranking, agreed with Matt, Oct 2026; in `onboard_rings.py` / `onboard_gems.py`). Rings: Legendary Chloranthy, Wolf, Divine Blessing; Epic Carthus Milkring, Sun Princess; Rare Dusk Crown, Obscuring; Uncommon Blue Tearstone, Knight Slayer's, Ring of Favour, Covetous Silver Serpent, Magic Stoneplate; Common Red Tearstone, Tiny Being's, Hornet, Dark Wood Grain. Gems: Epic Titanite Scale, Faron Flashsword, Blue Titanite; Rare Simple, Raw, Carthus Flame Arc, Crystal Magic Weapon; Uncommon Blood, Blessed, Crystal, Heavy, Sharp; Common Titanite Shard, Lightning, Hollow, Poison. Rarity only drives the frame colour and the inventory filter until treasure exists. The tiers run Common → Uncommon → Rare → Epic → Legendary: Epic and Legendary swapped names (Matt, Oct 2026), not ordinals, so every saved item kept its tier.

The ring bonus on an attack is `AttackTerms.bonus`, added to the roll and shown in the row's modifier and in the roll reveal; the rings' dice are `AttackTerms.extraDice` as before. A ring shown on the bar (Divine Blessing, the Tiny Being choice) uses its card art cut to a disc (`CharacterActionBar.RING_ART`, `PlayerToken.AggroMask`).

### Gems
The 16 gems Matt typed out (Oct 2026) are `Gem` resources (rarities under Rings) under `Resources/Prefabs/Equipment/Gems/<Name>/`, written with their card art by `Tools/onboard_gems.py`. A gem sits in a weapon's upgrade slot (`leftHandUpgradeIds` / `rightHandUpgradeIds`, two per weapon in backup), moves with its weapon on a swap, and only counts on a **held** weapon and only up to its `upgradeSlots` (`Player.GemsOn`). Several gems share one `Gem.Effect`.

All of it lives in `AttackTerms.For`, applied **first** so the Heroic boosts and rings see the attack as the gems made it. Gems with the same effect stack.
- **+1 black die** (Blessed, Crystal, Heavy, Sharp) → `extraDice`. **+1 damage** (Titanite Shard) and **+2 damage** (Titanite Scale, `DAMAGE_TWO`) → `bonus`. Three printed values were changed by Matt (Oct 2026), so the cards were edited to match: the Scale reads **+2 damage** and needs **Str 20 / Dex 20** (it was +1 with no requirements, which made it beat every black-die gem for free), and Raw Gem costs **+1 stamina** instead of +2 (otherwise the Scale made it pointless) and needs **Str 15** (a brute's gem, for flavour more than balance). `Tools/CardEdits/edit_cards.py` does it with no computer font: each new digit is a printed one lifted from a card of the same face, size and scan resolution (Raw Gem's "2", Titanite Shard's "1", Red Tearstone Ring's "20", Great Swamp Ring's "15", Carthus Milkring's "30"; Divine Blessing's new words are its own letters), the old digit comes off over restored paper grain, and the new one sits on the old baseline in the card's own ink with its source's spacing. `onboard_gems.py` uses anything in `Tools/CardEdits/` in place of the board game folder's card, so a re-run keeps the edits.
- **Magic** (Lightning; Blue Titanite with a black die; Carthus Flame Arc and Crystal Magic Weapon with +1 damage) → `AttackTerms.magic`. That is what rolls against Resist (`ResolveAgainstEnemy(…, magic)`), shows the magic icon on the row and the Resist badge in the reveal, casts the orb stroke, and counts as a magic attack for Spell Fury, Explosive Firepower and the Dusk Crown Ring.
- **Faron Flashsword**: an option whose range comes out at 0 gets range 1 and is magic. Applied before Berserk Charge, which then no longer sees it as range 0.
- **Blood / Poison** → `AttackTerms.conditions`, applied after the option's own condition and the damage (`CombatPresenter.ApplyAdded`), so a bleed attack still consumes and reapplies. Both apply when the option has its own condition (Matt).
- **Raw**: +2 damage and +1 stamina (edited card; on top of Stagger; a 0-cost Heroic boost still zeroes it).
- **Simple**: −1 stamina on every option tied at the weapon's highest printed cost (Matt).
- **Hollow**: an attack that dealt 0 damage to everything it hit resets the Luck token, automatically (`CharacterTurn.AfterRoll`, beside Knight Slayer). Each Repeat roll is checked on its own.

The summary panel's attack ranges (`Player.GetBestAttackPool`) do not include gems yet.

### Equipment Effects
The weapon and armour effects (`EquipmentEffect`, Oct 2026) are wired, read off the printed cards where the spreadsheet transcription was lossy. Only what is **held** counts (`Player.Passives()`: the armour and both hands, each with the item it is on).

**Weapon options** (`PlayerMove.bonusEffects`):
- **Support options** (`PlayerMove.IsSupport`: no dice, nothing inflicted, only effects on characters: Heal, Great Heal, Replenishment, Magic Barrier, Great Magic Weapon, Dragon Tooth's and Saint Bident's [0]...) pick characters instead of an enemy (`CharacterTurn.CastSupport` → `PickCharacters`). "Within range" is the option's or weapon's range from the caster. One / up to two characters are clicked; "all" options light everyone in range and one click casts; Great Heal's [4] picks a node; Bountiful Sunlight's [3] excludes the caster; Sunlight Straight Sword's "party bonus" reaches the whole party (Matt). A support option **is** that weapon's attack and locks movement like one (Matt). Its row or the End button (Cancel) backs out before it is paid.
- **Riders** on an attack (`CharacterTurn.Riders`): the attacker's own heal/stamina (Smough's Hammer), Lothric's Holy Sword's stamina to a character within 1 (picked), the Mace moving the Aggro token (picked, or Skip), Vordt's Great Hammer giving its wielder Bleed. Bewitched Alonne Sword's "You lose 4 Health" (`PlayerToken.LoseHealth`) is not damage (no Bleed) but **can kill** (Matt); its row marks it with "!" when it would.
- **Rapport** (`DIRECT_DAMAGE`): only an enemy sharing its node with another; no roll, no Block, 3 damage (`CombatPresenter.DirectDamage`). Force's "Push x2" pushes twice (`PlayerMove.PushNodes`).
- Party defence buffs live on `Player.defenceBuffs` and are cleared when the next character activation begins. Great Magic Weapon sets `PlayerToken.magicThisActivation` / `bonusThisActivation`, read by `AttackTerms`.
- Healing from any of a character's equipment (spells, rings, armour) goes through `PlayerToken.GearHeal`, which is where Cleric Armour's +1 lives.

**Passives** (armour and held weapons): Havel's Armour (no Walk, no dodge), the four cannot-dodge items (`CombatResolver.CanDodge`), Dancer Armour (Walk 2 nodes) and Catarina Armour (Run 2 stamina) in `PlayerToken.NextStepCost`, Shadow Armour (dodge step 2 nodes), dodge costs (`CombatResolver.DodgeStaminaCost(dodger, attacker)`: Black Leather and Alonne-vs-Alonne free, Eastern 2), Dark Armour (+1 dodge die vs Hollow), Mask of the Child (3 stamina at start; Chloranthy's 4 wins), Steel Armour (+1 black Block and Resist with a ring in), Black Knight Armour (ring requirements −2, `Player.RingRequirementCut`, in the inventory's check), Xanthous Robes (magic attacks −1) and Black Iron Armour (Node attacks −1) in `AttackTerms`, Hollow Soldier Shield (+1 vs Hollow, per target in the presenter), Armour of Thorns (1 damage to an enemy in your node after a Block/Resist roll against it, `CombatPresenter.Thorns`), Lothric Knight Armour (end of activation: 1 health per enemy on your node), the three Heroic-triggered robes/armour (`PlayerToken.OnHeroicUsed`; Crimson Robes asks which condition, `CharacterTurn.CrimsonRobes`, except mid-roll for Stand Fast, where the worst goes).

**Asked on the bar** (`CharacterActionBar.AskChoice`, a row per option and Skip): Sunset Shield's push of a Hollow that attacked from your node (`Enemy.SunsetShield`), Sunlight Shield taking an ally's damage on its node (the bearer suffers it; the condition stays with who was hit), Faraam Armour taking Aggro at the end of another character's activation, once per encounter (`ActionListener.OfferFaraam`, so `EndCharacterActivation` is async with a re-entry guard), Crimson Robes' choice.

**Two-handers** (p12) are now enforced: the loadout refuses a second weapon beside a two-hander, and the swap stows one, except for the eight small shields that "can be equipped in one hand while you have a two-handed weapon in your other hand" (`Weapon.SitsBesideTwoHander`, from `BYPASS_TWO_HAND_CHECK`).

**Enemy types**: `EnemyData.kind` (`NONE`, `HOLLOW`, `ALONNE`), by name (Matt): Hollow Soldier, Large Hollow Soldier, Hollow Crossbow, Firebomb Hollow, Phalanx Hollow; the three Alonne knights.

**Data fixed from the cards** (hand edits to the `.tres`; `Tools/onboard_equipment.py` would undo them, and says so at its top): Rapport is `DIRECT_DAMAGE` 3 to `ONE_ENEMY` with no modifier; Great Heal's [4] is `ONE_NODE`; Bountiful Sunlight's [3] is the appended `ALL_OTHER_CHARACTERS`. Several stored types read oddly but are handled as their cards say: Xanthous Robes' `LOSE_STAMINA` is a cost reduction, Armour of Thorns' `BONUS_DAMAGE IF_BLOCKING` is the thorns damage, `DODGE_STAMINA_MOD` magnitude 0 means free.

**Inert, waiting on systems that do not exist**: Abyss Greatsword (If Embered), Adventurer's Armour (If Trap Activated), Painting Guardian Armour (weak arc). Their tooltips say "(not in the game yet)".

### Fixed Encounters
Every world map encounter is rolled **once** and kept in the save (`SaveGame.encounterPlans`), so going back after a wipe or a rest is the same fight: the same enemies, tiers, spawn nodes and tile (Matt, Oct 2026). `WorldMapManager.EnsureLoaded` rolls any `ENCOUNTER` node without a plan and writes the save to disk straight away. A new campaign does this in `CharacterSelect` before its first save, and an old save gets its plans the first time it is loaded. Boss nodes get no plan yet.

`EncounterGenerator` rules (Matt's):
- **Level = total tiered health** (`EncounterSpawn.Health`, i.e. `Enemy.TierHealthBonus`): L1 1–5, L2 6–10, L3 11–20, L4 21–40.
- **Tiers allowed**: L1 tier 1 only, L2 up to 2, L3 and L4 up to 3.
- **1–6 enemies, at most 2 of one card** (tiers of the same card count together). Each pick favours a card not yet in the encounter, three to one.
- **One family per encounter, chosen by terrain** (`EnemyData.family`, `FAMILY_BY_TERRAIN`): Grass → Darkroot, Volcano → Iron Keep (Alonne + Ironclad), Sand → Hollows, Mountain → Tomb of Giants (skeletons + Necromancer), Snow → Painted World, City → Anor Londo (Silver Knights + Sentinel). Any other terrain picks a family at random. Necromancer and Crystal Lizard are in the pool without their unimplemented abilities.
- **Placement**: after the printed encounter cards, enemies stand together on a few of the tile's 4 spawn nodes (at most 3 each) rather than one to a node. `spawnSlot` indexes the tile's `isEnemySpawn` nodes in scene order.
- Rejection sampling, 4000 tries; if nothing fits the band it keeps the closest roll and warns. A probe of 9,300 rolls over the demo map had no misses. A lone enemy is allowed at any level (Matt), as on some printed cards: a Large Hollow Soldier can be a Level 1 fight, a Sentinel a Level 2 one.

`ActionListener.SpawnEnemies` spawns the pending plan (`WorldMapManager.GetPendingPlan`), setting `Enemy.tier` before the token enters the tree, and puts the plan's tile on `boardArt`. Only Tile 2 has its grid mapped, so every plan names `Tile 2`. The `enemies` export is only for standalone runs.

On the map (`WorldMapNode.DrawEncounter`) an encounter shows its toughest enemy (`EncounterPlan.Toughest`: most tiered health, ties to higher threat). The avatar is cut to a disc, inside a rim coloured by tier (`Enemy.TierRimColour`: bronze, ember, crimson), with the level icon over the bottom of the ring. The activation bar's tier rim now uses the same colours.

### Scene Navigation
Scenes are swapped by instantiating the next scene, adding it to root, then freeing the first child. Prefer `GetTree().ChangeSceneToPacked()` for new scene transitions — it's cleaner and avoids root-child-index assumptions.

## Coding Style

- **Formatting**: K&R braces (`{` on same line), 4-space indent via tabs
- **Naming**: `camelCase` for fields and locals, `PascalCase` for methods and classes, `SCREAMING_SNAKE` for constants
- **No comments on obvious code** — only where something non-obvious needs explanation
- **Exported node refs over hardcoded paths**: Prefer `[Export] public SomeNode ref;` wired in the editor rather than `GetNode("path/to/node")`
- **Lambda signal handlers**: `button.Pressed += () => { Handler(); };` — watch for closure-in-loop bugs (capture loop variable before the lambda)
- **Resources for data**: Character classes, weapons, armour, dice, moves are all `.tres` Resources edited in the Godot inspector — not hardcoded in C#
- **Short methods**: Methods stay focused; UI update logic lives in the relevant UI class
- **Cursors**: never set `mouse_default_cursor_shape` on an ordinary button — the `CursorPolicy` autoload gives every button the pointing hand as it enters the tree and the arrow while it is disabled and hovered. Only clickables that are not buttons (world map hexes) or whose clickability changes by state (grid nodes, enemy tokens) set their own, and the board's tokens and nodes are excluded from the policy for that reason

## Known Issues / Active Work

- `CharacterSheet.cs`: `_Ready()` always loads the Assassin, ignoring the `[Export] character` property
- `Pathfinding.cs`: `openSet.UpdateItem` is commented out — may produce suboptimal paths
- Weapon and armour effects were verified by build and a runtime probe (walk/run/dodge costs, cannot-dodge, Steel Armour, Black Knight, Force's two pushes, two-hander and Buckler swaps and loadout rule, which options count as support), not yet by playing through them; the board picking, the Skip prompts (Sunset, Sunlight, Faraam, Crimson), the Mace and Rapport are untested in play
- Shift and Repeat were verified by build, a load check of `shiftAfter` / `IsMovementOnly` and code review only, not yet by playing through them
- Fixed encounters were verified by build, a 9,300-roll probe of the generator, a save round trip of a plan and a screenshot of the map icons. Spawning a plan in the Encounter scene has not been played through yet
- Boss encounters award no souls — p19's formula needs sparks, which are cut, so boss rewards need their own rule
- Souls can be earned but never spent — treasure and levelling do not consume them
- Rings were verified by build, an import check and a runtime probe of their terms, defence dice, dodge cost and the Red Tearstone threshold, not yet by playing through them. Gems were verified the same way (a runtime probe of Raw, Simple, Blood, Heavy, Faron and Hollow on Dancer's Enchanted Swords and of a backup weapon), not yet by playing through them. The summary panel's attack ranges ignore gems
- Placement order, starting Aggro, the backup carousel and the swap were verified by build, a runtime check of `SwapBackupIntoHand` (one-hander and two-hander both ways) and code review only, not yet by playing through them
- Characters are not placed on the Bonfire tile on a wipe; the result panel just returns to the Bonfire scene
- "Push x2" (a Push effect with magnitude 2, e.g. Force's second option) still pushes one node; the row shows x2 and the tooltip says so
- The over-full-node push (p10) auto-picks its destination; p21 gives the players that choice. Attack and movement pushes follow the V2 direction rule instead
- The V2 dodge, all three pushes and the starting-node push were verified by build, a load check, a direction check of `Pushing.Destination` against the board edge, and code review only, not yet by playing through them
- Enemy tokens are bare avatars while character tokens are clipped discs with a rim; they do not match visually
- Save/load is scaffolded but non-functional
- Only five classes have a `Character` resource (Herald, Assassin, Knight, Deprived, Cleric). Mercenary, Pyromancer, Sorcerer, Thief and Warrior have art folders but no class resource, so their Heroic Actions are coded but cannot be played until the resources exist. Set `heroicAction` on each when made
- Heroic Actions, Estus and Luck were verified by build, a load check and code review only, not yet by playing through them
- The dodge step, the roll reveal and its thrown cube dice, the attack stroke (`AttackSlash`), the outcome banners, the held-attack zoom, the spotlight pulse, the cursor policy and every board effect under `Fx/` were verified by build and code review only, not yet by playing through them
- Presence, the no-overlap packing, the shadow, footfalls, heavy walk and heavy blows were verified by build and screenshots of a probe board (a ×3.4 stand-in boss, a crowded tier-3 Sentinel, singles); the walk, footfalls and blows have not been watched in play
- **Boss backlog** (Matt, Oct 2026), for when bosses exist: a larger activation-bar portrait and a long boss health bar under the title as in the games; a boss intro banner with the name and a low vignette; the boss arcs (which side of a boss a character stands on), which the ×3+ overlap is there for
- `WorldMapManager.ReportPartyDeath` still respawns **every** encounter including bosses, where resting spares them

### Enemy data (`Resources/Prefabs/Enemies/*/<Name>.tres`)

All 32 non-boss enemies are transcribed from their data cards into `EnemyData` resources. Card → field mapping:
threat top-left, health top-right (heart), Block/Resist on the centre shield, attack range in the left circle,
dodge difficulty in the right circle, behaviour icons along the bottom resolved left to right.
`EnemyMove.direction` is signed — positive moves towards the target, negative away (the card puts the node count
at the top of the Move icon for towards, the bottom for away). `EnemyData.UNLIMITED_RANGE` (99) stands in for the
∞ range symbol. Enemies whose range circle shows `–` have no attack behaviour at all, so range never applies.
Enemies with a Repeat icon (Bonewheel Skeleton, Skeleton Beast, Shears Scarecrow) have their behaviour list
duplicated in `moves` rather than carrying a repeat count.

All thirty-two share one scene, `Resources/Prefabs/Enemies/Enemy.tscn`. `ActionListener` holds
`Array<EnemyData> enemies` plus a single `enemyScene`, instantiates that scene per spawn and assigns `data`
**before** `EncounterManager.MovePlayer` parents it — that call is what fires `_Ready`, which builds the token
from the resource. Adding an enemy to an encounter means adding a resource, never a scene.

Godot rewrites these `.tres` files the first time it loads one, stripping every property that equals its C#
default and retyping the `moves` array. That is lossless *because* `EnemyMove.statusEffect` is initialised to
`NONE`, so a stripped value reloads as `NONE` rather than `BLEED = 0`. Keep that initialiser.

**Tier scaling** (Matt, Oct 2026; this project's own rule). Each tier above 1 adds 1 to every attack's damage (`Enemy.TierDamageBonus`, through `CombatResolver.AttackStrength`, so every readout shows it; a 0-damage movement push stays 0) and scales health to `max(⌈health × (1 + 0.5 × (tier − 1))⌉, 2 × tier − 1)` (`Enemy.TierHealthBonus`): ×1.5 at tier 2 and ×2 at tier 3, with a floor so a 1-health enemy still grows. 1 / 5 / 10 health become 3 / 8 / 15 at tier 2 and 5 / 10 / 20 at tier 3. Block and Resist do **not** change: Block comes off every swing, so +2 turned a Sentinel into ~50 swings. Tiers are meant for later in the campaign.

Tier is a per-instance export defaulting to 1. Campaign encounters set it from their saved plan (see Fixed Encounters).

Two card abilities are transcribed nowhere because nothing models them yet:

- **Necromancer**: the raised-skeletal-hand icon (summons additional enemies) is not implemented — its `.tres` only carries the magic AOE attack.
- **Crystal Lizard**: rules text "If the only enemies left are Crystal Lizards, they escape" is not implemented.

## What NOT to Do

- Don't add Unity-isms (`GetComponent`, `Start`, `Update` terminology) — this is Godot
- Don't hardcode absolute node paths like `GetNode("/root/Game/CanvasLayer/...")` — use exports or groups
- Don't add features beyond the current task; there's a long backlog, address one thing at a time
- Don't invent stats or equipment not in the board game rules without confirming with the user
