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
    GameNode.cs            — Individual grid tile; flags for entrance/enemy-spawn/disabled
    Player/
      Player.cs            — Campaign-persistent character (stats, equipment, level)
      PlayerToken.cs       — Character on the board; endurance, conditions, aggro, activation state
      CharacterTurn.cs     — Owns one activation: stepping, arming an attack, picking targets
      CharacterActionBar.cs— Weapon/option buttons + End Activation for the active character
      Endurance.cs         — The 10-box shared stamina/health bar (pure rules logic)
      Character.cs         — Character class template (tiers, starting gear, avatar)
      PlayerMove.cs        — Single weapon action (cost, dice, range, flags)
      CharacterSheet.cs    — UI component showing character stats and equipment slots
      PlayerSheet.cs       — In-encounter player status strip
    Enemy/
      Enemy.cs             — Enemy token; behaviour execution, health, conditions, detail levels
      EnemyMovement.cs     — Turns a Move icon into a node list (towards/away, distance, push)
      EnemyData.cs         — Per-enemy data card as a Resource (stats + moves + art)
      EnemyMove.cs         — Enemy action definition (direction, damage, flags)
      EnemyInfoPanel.cs    — Click-through inspect panel rendered from EnemyData
      EnemyBehaviourRow.cs — One behaviour icon written out as text
      Pathfinding/
        Pathfinding.cs     — A* algorithm (static)
        PathGrid.cs        — Grid wrapper used by pathfinding
        PathNode.cs        — Per-cell pathfinding data
        Heap.cs            — Min-heap for A* open set
    Combat/
      CombatResolver.cs    — Dice + arithmetic for one attack (no turn order, no UI)
      DodgePrompt.cs       — Block-or-Dodge modal that suspends an enemy's activation
      PushPrompt.cs        — Picks which model is shoved off an over-full node
  Equipment/
    Equipment.cs           — Interface: name, image, type, rarity, stat reqs
    Weapon.cs              — Weapon resource (attacks, defense dice, upgrade slots)
    Armour.cs              — Armour resource (defense dice, upgrade slots)
    Dice/
      Dice.cs              — Dice definition (type, possible values)
      DiceUtility.cs       — Roll helper
  MainMenu/
    MainMenu.cs            — New Game / Continue / Quit
    Bonfire/
      BonfireOptions.cs    — Rest / Equipment / Ready buttons
  Common/
    CharacterPortraitPane.cs  — Autoloaded floating party pane (Bonfire/Encounter)
    CharacterPortrait.cs      — Single portrait: avatar bg, name, HP, stamina, status strip
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
- **Endurance bar** (p20): 10 boxes shared by Stamina and Health. Spending 1 stamina adds a black cube from the left; suffering 1 damage adds a red cube from the right. Uncovered boxes are the remaining capacity to do *either*. All ten covered = character dead and the party immediately defeated. Gaining stamina/health **removes** cubes and does nothing when there are none to remove. Cleared on encounter victory.
- **Status effects** (rules p21): conditions apply to **any model — characters and enemies alike**. BLEED (2 extra damage next time the model is damaged, then remove), POISON (1 damage at end of the model's activation), FROST/Frostbite (character: +1 stamina to walk/run/dodge; enemy: Move icon values −1), STAGGER (character: +1 stamina to use weapon actions; enemy: attack damage values −1). All of this is implemented — see Conditions under Architecture Notes.
- **Bonfire rest**: Refills estus, heroic action, luck. Resets all encounters (enemies respawn). ⚠️ The printed rule also costs 1 **spark** — sparks are **deliberately cut from this project**. Do not implement them, do not gate resting on them, and do not use p19's spark-based boss soul formula.
- **Souls**: Currency. Earned 2 per character per non-boss encounter win. Spent on treasure (1 soul) and leveling up stats. Implemented — see Souls under Architecture Notes. Spending is not wired to anything yet.

### Combat Values & Clarifications

Concrete numbers and rules confirmed while analysing the equipment `.tres` data. Use these when reasoning about balance or implementing combat resolution:

- **Dice**: Only three types (`DiceUtility.DICE_TYPE`). Values and expected averages:
  - BLACK (0): faces `0,1,1,1,2,2` → avg **1.167**
  - BLUE (1): faces `1,1,2,2,2,3` → avg **1.833**
  - ORANGE (2): faces `1,2,3,3,4` → avg **2.5**
  - Damage/defense are arrays of these dice plus a flat `modifier`; expected value = sum of dice averages + modifier.
- **Action economy** (p22): a character makes up to **one attack with each weapon in a hand slot** — so two one-handers means two attacks in an activation, and a two-hander means one. Movement is one block, before or after the attacking.
- **Stamina is recovered capacity, not a pool**: there is no stamina number. What a character can spend is whatever the endurance bar still has uncovered, so an undamaged character with a clean bar can spend up to 10 in a single activation. The +2 at activation start *removes black cubes*, so it is a recovery rate, not income to bank. Burst is available immediately; the +2 governs only the long-run average, so sustained damage ≈ `attackDamage × min(1, 2/cost)` while a one-off spike can far exceed it.
- **Stamina and damage compete for the same boxes**: every point spent is a box that can no longer absorb a hit, and spending competes with running and dodging. Overspending while wounded kills you outright.
- **Upgrade slots** (`upgradeSlots`):
  - *Weapon slot* — each grants **+1 damage OR +1 black die** (avg 1.167) to the weapon's attacks.
  - *Armour slot* — each grants **+1 stamina OR +1 health at the start of every activation** (persistent regen; stamina slots effectively raise the +2 income).
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

Rules p19: every enemy activates in threat order, then exactly one character. Enemy movement resolves over several frames inside `Enemy._Process`, so the loop is driven by the `Enemy.ActivationFinished` signal rather than running straight through. `Enemy.ProcessNextMove` skips any behaviour it cannot execute rather than leaving `path` null, so an activation always terminates and the signal always arrives. The character phase then waits on the HUD's End Activation button (`TurnQueue.EndActivationPressed`).

### Character Activation
`CharacterTurn` owns everything the player does during their activation; `ActionListener` still owns turn order and just hands over via `Begin`/`End`. `CharacterActionBar` renders one row per hand-slot weapon with a button per attack option, rebuilt on every change rather than diffed — a stale button would let a player take an illegal turn.

Movement is **one node per click** (p22): the first step of an activation is the free Walk, each later one a Run at 1 stamina, and Frostbite adds 1 to every step including the walk. Legal neighbours are highlighted; `EncounterManager.CanEnter` keeps the node cap honest.

p22's "move before *or* after attacking, never both" is enforced by latching `PlayerToken.movementLocked = hasMoved` on the **first** attack. Move-then-attack locks movement for the rest of the activation; attack-then-move leaves it open, and continuing to move after that is still one contiguous block. Each hand-slot weapon may attack once (`usedWeapons`).

Targeting: an option-specific `attackRange` replaces the weapon's standard range (p23); Shaft (`isNotZeroRange`) excludes range 0; the Node icon (`isAOE`) resolves one roll against every enemy on the chosen node. Clicking an enemy while an attack is armed targets it instead of opening the inspect panel.

Not implemented, deliberately: `repeat`/`repeatConstraint` (xN uses with free / one-enemy / one-node constraints) and `bonusMovement` (Shift — free nodes that don't consume walk/run). Both need their own interaction; the action bar's tooltip marks them "(not implemented)" so they aren't mistaken for working.

### Ending an Encounter
`ActionListener.CheckEncounterOver` sets `ENCOUNTER_WON` / `ENCOUNTER_LOST` and calls `EndEncounter`, which clears every model's conditions and then settles the outcome. `WorldMapManager` is told **last**, because `ReportEncounterWon` / `ReportPartyDeath` consume `PendingEncounterNodeId` and both branches need it first.

A win clears every endurance bar (p19) and awards `2 × party size` souls. **Sparks are cut from this project**, so p19's boss formula (1 soul per character per remaining spark) is unusable and boss wins currently award nothing — the panel says so. Boss rewards need a replacement rule, not a spark implementation.

`EncounterResultPanel` shows the outcome and exits to the World Map on a win or the Bonfire on a wipe, matching where `WorldMapManager` has just put the party.

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

`GameNode.statusEffect` makes a node a hazard: `EncounterManager.ApplyNodeHazard` applies it to anything that steps on. **This is not a rule from the book** — the book's node-level hazard is the Trap token (p18), which damages characters and ignores enemies. Treat hazard nodes as this project's own idea.

### Enemy Behaviour Execution
`Enemy.ProcessNextMove` walks the behaviour list left to right (p24). A behaviour is movement when `isLeap || direction != 0` and an attack otherwise — the same split `EnemyInfoPanel` uses for its wording. `EnemyMovement.Plan` turns a Move icon into a node list: the icon's number is a **node count**, not "go to the target", so Move 2 takes two steps and stops. Negative `direction` retreats greedily, stopping early when no neighbour is farther (the corner case the rules call out). Frost trims the count by 1.

Attacks resolve in place: out of range misses entirely and has no effect (p25), the Node icon hits every character sharing the target node, and pushes shove characters off each node the enemy enters, dealing the movement attack's damage first. Targeting follows the skull/ring icon, and ties on "nearest" go to the aggro holder, then to the higher `Character.taunt` (p24).

Attacks are `async` because each one opens `DodgePrompt` and waits: the enemy's whole activation suspends until the defender picks Block or Dodge. `Enemy.ResolveArrival` exists for the same reason — arriving on a node can open that prompt, so it clears `path` before awaiting to stop `_Process` re-entering mid-await.

⚠️ **Never give a `Resource` subclass a parameterless constructor that sets anything.** Godot strips every property matching the *field-initializer* default when saving a `.tres`, then reconstructs through the parameterless constructor on load. `EnemyMove` chained to its full constructor (`towardsAggro=true, damage=1, direction=1`), so a saved `direction = 0` was stripped and came back as `1` — silently turning every enemy attack into a second move. `PlayerMove` had the same trap with `repeat`. Both are now `{}` and defaults live only in field initializers.

### Enemy Inspect Panel
Clicking a token fills `EnemyInfoPanel` (inside the `%Enemy Info Dialog` AcceptDialog) rather than showing a flat scan of the card. Everything comes from `EnemyData` except current health and tier-adjusted Block/Resist, which the printed card cannot show. The scan lives on a second tab and stays the authority when a transcribed value looks wrong. `EnemyInfoPanel` owns all the card-to-prose formatting; `EnemyMove` stays pure data. A behaviour counts as movement when `isLeap || direction != 0`, and as an attack otherwise — that split is what decides the wording and which chips appear.

### Combat Resolution
`CombatResolver` is deliberately free of turn order, targeting and UI — the turn loop decides who swings at whom and pays the Stamina, then calls in. Rolling and resolving are separate methods because the Node icon rolls **once** and compares that single total against every enemy on the node (p23), so the roll has to exist without being spent.

Character attacks roll pips + `modifier` and subtract the enemy's Block or Resist. Enemy attacks are a fixed damage value the defender rolls to reduce. A hit at 0 damage is still a hit (p20) and still applies its condition. Bleed is applied inside `Enemy.ApplyDamage` / `PlayerToken.ApplyDamage`, and only fires when damage actually lands — 0 damage is not "suffering damage".

Dodging (p25) uses `DodgeDice`, which extends `Dice` with faces `0,0,0,1,1,1` — three blanks and three icons, so each die is a coin flip and a roll *sums* to the number of icons. Summing rather than flagging is what makes a dodge difficulty of 2 or more work. `dodgeAbility` on armour and weapons is the size of that pool. A dodge replaces the Block/Resist roll entirely and is all or nothing: succeed and the character is not hit at all (no damage, no push, no condition); fail and they take the **full** damage with no defence roll. `DiceUtility.DICE_TYPE` gained a `DODGE` member, appended so existing BLACK/BLUE/ORANGE ordinals in `.tres` files still hold.

### Player Model
`Player` is the campaign-persistent Resource (stats, equipment ids, level) and `PlayerToken : TextureButton` is the character on the board, exactly mirroring the `EnemyData` / `Enemy` split. `ActionListener` spawns one shared `PlayerToken.tscn` per party member and assigns the `Player` before parenting it.

Encounter state lives on `Player.endurance`, which is deliberately **not** `[Export]`ed — the bar clears on victory (p19), so it must never be written into the saved character. It is a plain `Endurance` object rather than a Resource so it stays out of serialisation entirely. `Player` is per-character (one per party member from `CampaignManager.Players`), so mutable runtime state on it is safe in a way it would not be on a shared template like `EnemyData`.

The party comes from `CampaignManager.Players`; when that is empty the encounter falls back to `ActionListener.demoParty` (a list of `Character` resources) so the scene can be run standalone.

### Encounter HUD
`TurnQueue` (instanced in `Encounter.tscn` between the title bar and the board) is a pure mirror — it renders activation order from `EncounterManager.enemies`, which `ActionListener` has already sorted by threat descending. It rebuilds when the enemy count changes and repaints when phase, round or active index changes — it never drives the turn loop, only reflects it. The trailing separator and Party entry encode the rule that one character activates after every enemy. End Activation lives on `CharacterActionBar`, not here, so the queue stays free of controls.

### Node Occupancy
A node holds at most `EncounterManager.MAX_MODELS_PER_NODE` (3) models — but a full node is **enterable**, not blocked. p10: "If there are already three models on a node and another model moves onto that node, the players must push one of the three models already on the node." So `PathGrid.IsBlocked` is terrain only and full nodes stay pathable; `PathGrid.HasRoom` is the separate occupancy question.

`EncounterManager.ResolveOverflow` runs after any arrival and, if the node is now over the limit, asks `PushPrompt` which of the models that were *already there* gets shoved off. The rule gives that choice to the players whoever walked in, so an arriving enemy prompts exactly as an arriving character does; it auto-resolves when only one model could be pushed, and only ever fires on a node that was already at three.

The push **destination** is auto-picked (first adjacent node with room, falling back to any walkable one). p21 gives the players that choice too — prompting for both would double the clicks, so only the "who" is asked.

⚠️ An earlier pass had this wrong, treating full nodes as impassable. That is what made enemies deadlock around a cornered character. Do not reintroduce occupancy into `IsBlocked`.

### Node-to-World Mapping
Characters and enemies are children of `GameNode` (Control) nodes. `EncounterManager.MovePlayer` reparents a unit from one `GameNode` to another and calls `FixPositioning` to manually place up to 3 occupants. The grid is a 7×7 set of `MarginContainer` nodes, each with a `TextureButton` child for click handling.

### Pathfinding
A* is implemented in `Pathfinding.cs` using `Heap<PathNode>` for the open set. `PathGrid` (a Node in the scene) initializes the walkability grid from `GameNode.isDisabled` flags. Known issue: `openSet.UpdateItem` is commented out, which can produce suboptimal paths when a node's cost improves mid-search.

### Equipment System
`Equipment` is a C# interface with shared properties. `Weapon` and `Armour` are `[GlobalClass]` Resources implementing it. `PlayerMove` defines individual attack options within a weapon (stamina cost, dice array, range, flags). Characters can hold: 1 armour, 2 hand slots, 1 backup slot.

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

## Known Issues / Active Work

- `CharacterSelect.cs`: Player button loop has a closure bug — all buttons fire with the last index value
- `CharacterSheet.cs`: `_Ready()` always loads the Assassin, ignoring the `[Export] character` property
- `Pathfinding.cs`: `openSet.UpdateItem` is commented out — may produce suboptimal paths
- `PlayerMove.repeat` / `repeatConstraint` (xN attacks with FREE / ONE_ENEMY / ONE_NODE targeting) is parsed but never executed — an attack always resolves exactly once
- `PlayerMove.bonusMovement` (Shift icon: free nodes that do not consume the walk/run budget, usable before or after the dice) is parsed but never executed
- Characters cannot swap backup ↔ hand slots during their activation (p22)
- The first character activation should be the players' choice of character (p19); `activeCharacterIndex` just cycles from index 0
- Boss encounters award no souls — p19's formula needs sparks, which are cut, so boss rewards need their own rule
- Souls can be earned but never spent — treasure and levelling do not consume them
- The First Activation token (p19) is unimplemented; `activeCharacterIndex` always starts at 0 rather than rotating between encounters
- Characters are not placed on the Bonfire tile on a wipe; the result panel just returns to the Bonfire scene
- The push **destination** is auto-picked; p21 gives the players that choice
- Save/load is scaffolded but non-functional
- Bonfire "Rest" button does nothing

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

Tokens drop their condition column and tier pips below `Enemy.compactWidthPx` (64px rendered), keeping only the
threat badge and health bar. The threshold is exported because a token is scaled to the grid cell, so its real
on-screen size depends on resolution — tune it against the running game, not the design size.

Tier is still a per-instance export defaulting to 1; nothing selects it per encounter yet. That needs an
encounter definition resource pairing an `EnemyData` with a tier and a count.

Two card abilities are transcribed nowhere because nothing models them yet:

- **Necromancer**: the raised-skeletal-hand icon (summons additional enemies) is not implemented — its `.tres` only carries the magic AOE attack.
- **Crystal Lizard**: rules text "If the only enemies left are Crystal Lizards, they escape" is not implemented.

## What NOT to Do

- Don't add Unity-isms (`GetComponent`, `Start`, `Update` terminology) — this is Godot
- Don't hardcode absolute node paths like `GetNode("/root/Game/CanvasLayer/...")` — use exports or groups
- Don't add features beyond the current task; there's a long backlog, address one thing at a time
- Don't invent stats or equipment not in the board game rules without confirming with the user
