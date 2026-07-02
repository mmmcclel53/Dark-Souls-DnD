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
    EncounterManager.cs    — Static global state (players, enemies, nodes, action enum)
    GameNode.cs            — Individual grid tile; flags for entrance/enemy-spawn/disabled
    Player/
      Player.cs            — Player runtime data (stats, equipment, stamina/health)
      Character.cs         — Character class template (tiers, starting gear, avatar)
      PlayerMove.cs        — Single weapon action (cost, dice, range, flags)
      CharacterSheet.cs    — UI component showing character stats and equipment slots
      PlayerSheet.cs       — In-encounter player status strip
    Enemy/
      Enemy.cs             — Enemy node; pathfinding movement, health, click-info modal
      EnemyMove.cs         — Enemy action definition (direction, damage, flags)
      Pathfinding/
        Pathfinding.cs     — A* algorithm (static)
        PathGrid.cs        — Grid wrapper used by pathfinding
        PathNode.cs        — Per-cell pathfinding data
        Heap.cs            — Min-heap for A* open set
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
```

## Board Game Rules Reference

The physical game rules are in `Dark Souls Board Game Rules.pdf`. Key mechanics being implemented:

- **Nodes**: Grid tiles. Types: basic, spawn (enemy start), terrain, entrance (player start). Max 3 models per node.
- **Encounters**: Enemies activate first (ordered by threat level, high to low), then one character activates per round. Alternates until all enemies dead or party wiped.
- **Character activation**: Gain 2 stamina. Walk free once, run costs 1 stamina/node, dodge costs 1 stamina + roll.
- **Enemy activation**: Follow behavior icons left-to-right. Move towards/away from aggro or nearest character.
- **Aggro token**: The active character always holds aggro. Enemies prioritize the aggro holder.
- **Combat (player attacks)**: Roll dice per weapon attack option, subtract enemy Block (physical) or Resist (magic). Result = damage.
- **Combat (enemy attacks)**: Fixed damage value. Player rolls defense dice to reduce. Or spend 1 stamina to attempt dodge roll vs dodge difficulty.
- **Endurance bar**: 10 boxes. Black cubes = stamina spent (left to right). Red cubes = damage taken (right to left). All boxes filled = dead.
- **Status effects**: BLEED (extra 2 damage on next hit, then remove), POISON (1 damage at end of activation), FROST (+1 stamina cost to move), STAGGER (+1 stamina to attack / -1 enemy damage).
- **Bonfire rest**: Costs 1 spark. Refills estus, heroic action, luck. Resets all encounters (enemies respawn).
- **Souls**: Currency. Earned 2 per character per non-boss encounter win. Spent on treasure (1 soul) and leveling up stats.

### Combat Values & Clarifications

Concrete numbers and rules confirmed while analysing the equipment `.tres` data. Use these when reasoning about balance or implementing combat resolution:

- **Dice**: Only three types (`DiceUtility.DICE_TYPE`). Values and expected averages:
  - BLACK (0): faces `0,1,1,1,2,2` → avg **1.167**
  - BLUE (1): faces `1,1,2,2,2,3` → avg **1.833**
  - ORANGE (2): faces `1,2,3,3,4` → avg **2.5**
  - Damage/defense are arrays of these dice plus a flat `modifier`; expected value = sum of dice averages + modifier.
- **One action economy**: A character gets **one move and one attack per activation** — attacks do not chain within a turn.
- **Stamina is income, not a refill**: You gain **+2 stamina at the start of each activation** (not a full reset). Stamina is a persistent pool on the shared endurance bar, so an attack costing >2 must be *banked* over multiple turns (you attack less often). Sustainable damage ≈ `attackDamage × min(1, 2/cost)`.
- **Endurance bar is shared for stamina AND health** (10 boxes): every stamina point spent is a box unavailable to absorb damage. Overspending while wounded can kill you; spending competes with running/dodging.
- **Upgrade slots** (`upgradeSlots`):
  - *Weapon slot* — each grants **+1 damage OR +1 black die** (avg 1.167) to the weapon's attacks.
  - *Armour slot* — each grants **+1 stamina OR +1 health at the start of every activation** (persistent regen; stamina slots effectively raise the +2 income).
- **Status effects** (`EncounterManager.StatusEffect` = `BLEED=0, POISON=1, FROST=2, STAGGER=3, NONE=4`):
  - Auto-applied on **any** hit, regardless of whether damage gets past enemy Block/Resist. **No enemy immunity.** Only ever applied to enemies (never turned on the player).
  - An enemy can carry multiple *different* statuses at once, but a single status does **not** stack (no double BLEED).
  - **All statuses except BLEED are removed at the end of the enemy's activation.** BLEED persists until a hit consumes it (+2 damage on that next hit, then removed — and reapplied by that same hit).
  - Effect magnitudes: BLEED +2 on next hit; POISON 1 dmg at end of enemy activation; FROST +1 stamina cost to enemy movement; STAGGER +1 stamina to enemy attack / −1 enemy damage.
  - ⚠️ `BLEED = 0` collides with "falsy"/default-int handling — when parsing `statusEffect`, default missing values to `NONE (4)`, not `0`, or bleed weapons get silently misread.

## Architecture Notes

### Global State
`EncounterManager` is a static class holding encounter-wide state (active players list, enemies list, node list, current action). It needs an explicit `Reset()` call when starting a new encounter because static state persists across scene loads.

### Action State Machine
`ActionListener._Process` polls `EncounterManager.action` each frame. States: `INACTIVE`, `PICK_ENTRANCE`, `ENEMY_MOVE`. After enemies finish moving, the state should advance to player turn (not yet implemented).

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
- `Enemy.Connect("pressed", ...)`: Enemy extends Node, not Button — this signal never fires
- `Enemy.DoMoves()`: Multi-move enemies lose all but the last path (loop overwrites `path` each iteration)
- `Pathfinding.cs`: `openSet.UpdateItem` is commented out — may produce suboptimal paths
- Aggro system is stubbed out (commented `isAggro` references everywhere)
- Player turn / combat resolution not yet implemented — `ENEMY_MOVE` state has no follow-through
- Save/load is scaffolded but non-functional
- Bonfire "Rest" button does nothing

## What NOT to Do

- Don't add Unity-isms (`GetComponent`, `Start`, `Update` terminology) — this is Godot
- Don't hardcode absolute node paths like `GetNode("/root/Game/CanvasLayer/...")` — use exports or groups
- Don't add features beyond the current task; there's a long backlog, address one thing at a time
- Don't invent stats or equipment not in the board game rules without confirming with the user
