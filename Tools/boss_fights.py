"""The bosses that are fought for real: each one's data card and behaviour cards, transcribed
from the board game folder's Images/Bosses/<tier>/<boss>/ (the data card and Moves/, with the
Heat Up cards in Moves/Heat Up/). Read by onboard_bosses.py, which writes them into the boss's
BossData. Every reading here was confirmed with Matt before it was coded.

A card is (file under the boss's folder, name, range, dodge, repeat, heat up, icons), its icons
left to right. Range None is the infinity symbol.

  move(nodes, arm, target, push)  the four-armed cross: the arm carrying the number ("front",
                                  "back", "left", "right"); the target icon beside it ("aggro"
                                  skull, "nearest" ring, or None for a shift without turning);
                                  push is the shield with an arrow
  leap(target, damage)            the curved Leap arrow under a Move cross with no number
  rotate()                        the big curved arrow: a 180° turn
  estus(health)                   the bottle icon: the boss heals
  attack(damage, target, ...)     area=True for the Area icon (the small crosshair), node=True
                                  for the Node icon; arcs and weak are the arc diagram's green
                                  and red arcs as letters F, R, B, L (top of the diagram is the
                                  boss's front)
"""


def move(nodes, arm="front", target=None, push=False, damage=0, weak="", condition=None):
    # damage: a number on the Push shield, an attack on each character on a node it steps onto;
    # weak: the arc diagram under it when the card has no attack icon (Skewering Charge);
    # condition: the icon after it, inflicted by that trample (the Asylum Demon's Stagger).
    return {"kind": "MOVE", "nodes": nodes, "arm": arm, "target": target, "push": push, "damage": damage, "weak": weak,
            "condition": condition}


def leap(target, damage=0, condition=None):
    # p29: straight to the target, without turning; every leap has the Push icon. A number on
    # its Push shield is damage to each character on the landing node before the push.
    return {"kind": "LEAP", "target": target, "push": True, "damage": damage, "condition": condition}


def summon(count):
    # The rat icon with its number: that many of the fight's "summon" enemy, round the boss.
    return {"kind": "SUMMON", "target": None, "push": False, "nodes": count}


def pillar():
    # The demon smashing a pillar (Pillars of Woe): the card smashes the pillars it reaches.
    # Not an action; onboard_bosses.py makes it the card's flag.
    return {"kind": "PILLAR"}


def estus(health):
    # The bottle icon: the boss heals this much (Matt).
    return {"kind": "HEAL", "target": None, "push": False, "damage": health}


def rotate():
    # p29: a full 180° turn in place.
    return {"kind": "ROTATE", "target": None, "push": False}


def attack(damage, target=None, magic=False, area=False, node=False, arcs="", weak="", condition=None, push=False):
    return {"kind": "ATTACK", "damage": damage, "target": target, "magic": magic, "area": area, "node": node,
            "arcs": arcs, "weak": weak, "condition": condition, "push": push}


FIGHTS = {
    # Heavy Blows: a Block or Resist roll of 3 or more against it puts a stamina cube on the bar.
    "Winged Knight": {
        "card": "Winged Knight.png",
        "threat": 10, "health": 24, "block": 3, "resist": 1, "heat_up": 12, "deck": 4,
        "ability": "HEAVY_BLOWS",
        "tile": "castle",
        "crop": (0.62, 0.27, 0.36),
        "cards": [
            ("Moves/tile008.png", "Backhand Shaft Strike", 1, 1, 1, False,
             [move(1, "front", "nearest", push=True), attack(5, area=True, arcs="FR", weak="L")]),
            ("Moves/tile009.png", "Charging Assault", 1, 2, 2, False,
             [move(1, "front", "aggro", push=True), attack(4, area=True, arcs="F", weak="R")]),
            ("Moves/tile010.png", "Diagonal Uppercut", 1, 1, 1, False,
             [attack(5, area=True, arcs="FR", weak="L"), move(1, "back", "nearest", push=True)]),
            ("Moves/tile012.png", "Sweeping Blade Swing", 1, 1, 1, False,
             [move(1, "front", "nearest", push=True), attack(5, area=True, arcs="FL", weak="R")]),
            ("Moves/tile013.png", "Overhand Smash", 1, 2, 1, False,
             [move(2, "front", "aggro", push=True), attack(5, target="aggro", node=True, weak="B")]),
            ("Moves/Heat Up/tile006.png", "Double Slash", 1, 2, 2, True,
             [move(1, "front", "nearest", push=True), attack(6, area=True, arcs="FLR", weak="B")]),
            ("Moves/Heat Up/tile007.png", "Whirlwind", 1, 1, 3, True,
             [move(1, "front", "nearest", push=True), attack(4, area=True, arcs="FRBL")]),
            ("Moves/Heat Up/tile011.png", "Pillars of Light", None, 1, 1, True,
             [attack(5, magic=True, area=True, arcs="FRBL")]),
        ],
    },
    # Flying High: after heat up, characters in its node pay +1 stamina to attack, free attacks
    # included. A Move 0 with a target only turns it to face (Matt).
    "Gargoyle": {
        "card": "Gargoyle.png",
        "threat": 10, "health": 26, "block": 2, "resist": 1, "heat_up": 12, "deck": 4,
        "ability": "FLYING_HIGH",
        "tile": "castle",
        "crop": (0.50, 0.28, 0.44),
        "cards": [
            ("Moves/tile026.png", "Electric Breath", 1, 2, 1, False,
             [move(0, "front", "nearest"), attack(4, magic=True, area=True, arcs="FL", weak="R")]),
            ("Moves/tile027.png", "Tail Sweep", 1, 2, 1, False,
             [move(1, "front", "nearest", push=True), attack(4, area=True, arcs="FL", weak="B")]),
            ("Moves/tile028.png", "Tail Whip", 1, 2, 1, False,
             [attack(4, area=True, arcs="RB", weak="F"), move(1, "back", "aggro", push=True)]),
            ("Moves/tile029.png", "Halberd Thrust", 1, 1, 1, False,
             [move(1, "front", "aggro", push=True), attack(6, target="aggro", weak="L")]),
            ("Moves/tile030.png", "Sweeping Strike", 1, 1, 1, False,
             [move(1, "front", "nearest", push=True), attack(5, area=True, arcs="FLR", weak="B")]),
            ("Moves/Heat Up/tile023.png", "Aerial Electric Breath", 2, 2, 1, True,
             [move(0, "front", "nearest"), attack(5, magic=True, area=True, arcs="F")]),
            ("Moves/Heat Up/tile024.png", "Flying Tail Whip", 1, 1, 1, True,
             [move(1, "front", "nearest", push=True), attack(6, area=True, arcs="FLR")]),
            ("Moves/Heat Up/tile025.png", "Swooping Cleave", 1, 2, 1, True,
             [move(2, "front", "nearest", push=True), attack(6, target="nearest", node=True)]),
        ],
    },
    # Ice in His Veins: never gains Frostbite or Stagger. A leap pushes even on its own node (Matt).
    "Boreal Outrider Knight": {
        "card": "Boreal Outrider Knight.png",
        "threat": 10, "health": 26, "block": 2, "resist": 3, "heat_up": 13, "deck": 4,
        "ability": "ICE_IN_HIS_VEINS",
        "tile": "generic",
        "crop": (0.58, 0.29, 0.38),
        "cards": [
            ("Moves/tile035.png", "Overhand Slash", 1, 1, 1, False,
             [move(1, "left", push=True), attack(5, area=True, arcs="RB", weak="F")]),
            ("Moves/tile036.png", "Chilling Thrust", 1, 2, 1, False,
             [move(2, "front", "aggro", push=True), attack(4, target="aggro", magic=True, weak="B", condition="frost")]),
            ("Moves/tile037.png", "Sweeping Strike", 1, 1, 1, False,
             [attack(5, area=True, arcs="FLB", weak="R"), move(1, "back", "aggro", push=True)]),
            ("Moves/tile038.png", "Backhand Slashes", 1, 2, 2, False,
             [move(1, "front", "nearest", push=True), attack(4, area=True, arcs="FL", weak="R", condition="frost")]),
            ("Moves/tile039.png", "Leaping Frost", 1, 1, 2, False,
             [attack(4, area=True, arcs="FR", weak="L", condition="frost"), leap("aggro")]),
            ("Moves/Heat Up/tile032.png", "Lunging Triple Slash", 1, 1, 3, True,
             [move(1, "front", "nearest", push=True), attack(4, area=True, arcs="F", weak="R")]),
            ("Moves/Heat Up/tile033.png", "Frost Breath", 1, 1, 1, True,
             [move(0, "front", "nearest"), attack(6, magic=True, area=True, arcs="FLR", weak="B", condition="frost")]),
            ("Moves/Heat Up/tile034.png", "Uppercut Slam", 1, 2, 2, True,
             [move(2, "front", "nearest", push=True), attack(5, target="nearest", weak="L", condition="frost")]),
        ],
    },
    # Titanite Construct: 3+ damage from one attack roll is 1 less (after Block/Resist, before
    # Bleed). A push on an attack sends each character hit away, out through its arc (Matt).
    "Titanite Demon": {
        "card": "Titanite Demon.png",
        "threat": 10, "health": 22, "block": 3, "resist": 2, "heat_up": 10, "deck": 4,
        "ability": "TITANITE_CONSTRUCT",
        "tile": "generic",
        "crop": (0.50, 0.285, 0.44),
        "cards": [
            ("Moves/tile018.png", "Lightning Bolt", None, 2, 1, False,
             [move(1, "back", push=True), attack(5, target="aggro", magic=True)]),
            ("Moves/tile019.png", "Tail Whip", 1, 2, 1, False,
             [move(2, "front", "nearest", push=True), attack(4, area=True, arcs="LRB", weak="F", push=True)]),
            ("Moves/tile020.png", "Grab & Smash", 1, 1, 1, False,
             [move(0, "front", "nearest"), attack(6, target="nearest", weak="R", push=True)]),
            ("Moves/tile021.png", "Vicious Swing", 1, 1, 1, False,
             [rotate(), attack(5, area=True, arcs="FRB", weak="L")]),
            ("Moves/tile022.png", "Double Swing", 1, 1, 2, False,
             [move(1, "front", "nearest", push=True), attack(4, area=True, arcs="F", weak="B")]),
            ("Moves/Heat Up/tile015.png", "Vaulting Slam", 0, 2, 1, True,
             [leap("aggro", damage=6), move(0, "front", "aggro")]),
            ("Moves/Heat Up/tile016.png", "Double Pole Crush", 2, 1, 2, True,
             [attack(5, area=True, arcs="F"), move(1, "back", push=True)]),
            ("Moves/Heat Up/tile017.png", "Sweeping Strike", 2, 1, 1, True,
             [move(2, "back", push=True), attack(6, area=True, arcs="FLR", weak="B")]),
        ],
    },
    # Parry: any attack on it in a character's turn that causes no wounds gives that character
    # Stagger at the end of the turn. Its older cards draw Stagger as a grey arrow.
    "Black Knight": {
        "card": "01_black_knight.png",
        "threat": 10, "health": 20, "block": 3, "resist": 2, "heat_up": 10, "deck": 4,
        "ability": "PARRY",
        "tile": "castle",
        "crop": (0.56, 0.26, 0.32),
        "cards": [
            ("Moves/02_overhead_swing.png", "Overhead Swing", 2, 2, 1, False,
             [attack(4, area=True, arcs="F", weak="B"), move(2, "front", "aggro", push=True)]),
            ("Moves/03_heavy_slash.png", "Heavy Slash", 2, 1, 1, False,
             [attack(5, target="nearest", node=True, weak="L", condition="stagger"), move(1, "front", "nearest", push=True)]),
            ("Moves/04_backswing.png", "Backswing", 2, 1, 1, False,
             [attack(5, target="nearest", node=True, weak="R", condition="stagger"), move(2, "back", "nearest", push=True)]),
            ("Moves/05_vicious_hack.png", "Vicious Hack", 2, 1, 1, False,
             [attack(5, area=True, arcs="FL", weak="R"), move(2, "front", "aggro", push=True)]),
            ("Moves/06_defensive_strike.png", "Defensive Strike", 2, 2, 1, False,
             [attack(5, target="nearest", node=True, weak="B"), move(1, "front", "nearest", push=True)]),
            ("Moves/07_wide_swing.png", "Wide Swing", 2, 2, 1, False,
             [attack(4, area=True, arcs="FR", weak="L", condition="stagger"), move(1, "front", "aggro", push=True)]),
            ("Moves/Heat Up/08_massive_swing.png", "Massive Swing", 2, 2, 1, True,
             [attack(5, area=True, arcs="FLR"), move(0, "front", "nearest", push=True)]),
            ("Moves/Heat Up/09_hacking_slash.png", "Hacking Slash", 2, 2, 1, True,
             [attack(5, area=True, arcs="F", condition="stagger", push=True), move(2, "front", "nearest", push=True, damage=4)]),
            ("Moves/Heat Up/10_charge.png", "Charge", 2, 2, 1, True,
             [attack(6, target="aggro", node=True, condition="stagger"), move(3, "front", "aggro", push=True)]),
        ],
    },
    # Brutal Impact: a Stagger attack hitting someone already Staggered also puts a stamina cube
    # on their bar. A Push shield before an attack is Push on the attack.
    "Heavy Knight": {
        "card": "01_heavy_knight.png",
        "threat": 10, "health": 25, "block": 2, "resist": 2, "heat_up": 15, "deck": 4,
        "ability": "BRUTAL_IMPACT",
        "tile": "generic",
        "crop": (0.50, 0.25, 0.38),
        "cards": [
            ("Moves/02_overhead_chop.png", "Overhead Chop", 1, 1, 1, False,
             [move(1, "front", "nearest", push=True), attack(5, target="nearest", node=True, weak="LR", condition="stagger", push=True)]),
            ("Moves/03_double_chop.png", "Double Chop", 1, 1, 2, False,
             [attack(5, target="nearest", node=True, weak="LR", condition="stagger", push=True), move(0, "front", "nearest")]),
            ("Moves/04_slashing_blade.png", "Slashing Blade", 1, 1, 1, False,
             [move(1, "front", "nearest", push=True), attack(4, area=True, arcs="FLR", weak="B", condition="stagger", push=True)]),
            ("Moves/05_double_slash.png", "Double Slash", 1, 1, 2, False,
             [attack(4, area=True, arcs="FLR", weak="B", condition="stagger", push=True), move(0, "front", "nearest")]),
            ("Moves/06_shield_swipe.png", "Shield Swipe", 1, 2, 1, False,
             [move(1, "front", "nearest", push=True), attack(4, area=True, arcs="FLR", weak="B", condition="stagger", push=True)]),
            ("Moves/07_shield_smash.png", "Shield Smash", 1, 3, 1, False,
             [move(1, "front", "nearest", push=True), attack(4, target="nearest", node=True, weak="LR", condition="stagger", push=True)]),
            ("Moves/Heat Up/08_charging_chop.png", "Charging Chop", 1, 2, 1, True,
             [move(2, "front", "aggro", push=True), attack(6, target="nearest", node=True, weak="LR", push=True), estus(4)]),
            ("Moves/Heat Up/09_defensive_chop.png", "Defensive Chop", 1, 2, 1, True,
             [attack(5, target="nearest", node=True, weak="B", push=True), move(2, "back", "aggro"), estus(4)]),
            ("Moves/Heat Up/10_defensive_swipe.png", "Defensive Swipe", 1, 3, 1, True,
             [attack(4, area=True, arcs="FLR", weak="B", push=True), move(2, "back", "aggro"), estus(4)]),
        ],
    },
    # Royal Protection: four Undead Rats spread round it as the fight starts, and immune to
    # Poison. Call for Help summons two more round it. The fight is won once the rats are dead
    # too (Matt). Its cards are a fan set (jpg, a struck-through DS badge).
    "Royal Rat Authority": {
        "card": "Royal Rat Authority.jpg",
        "threat": 10, "health": 25, "block": 1, "resist": 1, "heat_up": 15, "deck": 4,
        "ability": "ROYAL_PROTECTION",
        "tile": "river boss",
        "crop": (0.55, 0.31, 0.34),
        "summon": "res://Resources/Prefabs/Enemies/Undead Rat/Undead Rat.tres", "summon_at_start": 4,
        "cards": [
            ("Moves/Charge.jpg", "Charge", 0, 2, 1, False,
             [move(3, "front", "aggro", push=True, damage=4, weak="B")]),
            ("Moves/Double Swipe.jpg", "Double Swipe", 1, 1, 2, False,
             [move(1, "front", "nearest"), attack(4, target="nearest", area=True, arcs="FLR", weak="B", push=True)]),
            ("Moves/Stomp.jpg", "Stomp", 0, 2, 1, False,
             [move(2, "front", "nearest", push=True, damage=2), attack(5, target="nearest", area=True, arcs="F", weak="L", push=True)]),
            ("Moves/Heat Up/Acid Breath.jpg", "Acid Breath", 1, 2, 1, True,
             [attack(5, target="nearest", magic=True, area=True, arcs="FLR", weak="B", condition="poison")]),
            ("Moves/Heat Up/Call for help.jpg", "Call for Help", 0, 1, 1, True,
             [summon(2)]),
            ("Moves/Heat Up/Spin.jpg", "Spin", 1, 2, 1, True,
             [attack(6, target="nearest", area=True, arcs="FRBL", push=True)]),
        ],
    },
    # Pillars of Woe (Matt, Oct 2026): its tile has four pillars no one can stand on. A card with
    # the pillar icon smashes every pillar it reaches (a node it steps onto, a node its Area
    # attack covers, a node beside where it leaps); a smashed pillar is open floor for the rest
    # of the fight and deals 1 damage and Stagger to every character on the nodes around it.
    "Asylum Demon": {
        "card": "Asylum Demon.png",
        "threat": 10, "health": 34, "block": 1, "resist": 1, "heat_up": 16, "deck": 4,
        "ability": "PILLARS_OF_WOE",
        "tile": "Asylum Demon & Stray Demon",
        "crop": (0.48, 0.26, 0.40),
        "cards": [
            ("Moves/tile001.png", "Delayed Hammer Drive", 0, 1, 1, False,
             [pillar(), move(2, "front", "aggro", push=True, damage=5, weak="B", condition="stagger")]),
            ("Moves/tile002.png", "Leaping Hammer Smash", 0, 1, 1, False,
             [leap("aggro", damage=4), move(0, "front", "aggro")]),
            ("Moves/tile003.png", "Ground Pound", 1, 1, 1, False,
             [pillar(), attack(5, area=True, arcs="FRBL", push=True, condition="stagger")]),
            ("Moves/tile004.png", "Hammer Drive", 0, 2, 1, False,
             [move(2, "front", "aggro", push=True, damage=4, weak="B", condition="stagger")]),
            ("Moves/tile005.png", "Retreating Sweep", 1, 1, 1, False,
             [attack(4, area=True, arcs="FLR", weak="B", push=True), move(1, "back")]),
            ("Moves/tile006.png", "Mighty Hammer Smash", 0, 1, 1, False,
             [pillar(), leap("nearest", damage=5), move(0, "front", "nearest")]),
            ("Moves/Heat Up/tile007.png", "Crushing Leaps", 0, 1, 1, True,
             [leap("nearest", damage=5), leap("aggro", damage=5)]),
            ("Moves/Heat Up/tile008.png", "Lumbering Swings", 1, 1, 2, True,
             [pillar(), move(2, "front", "aggro", push=True), attack(4, area=True, arcs="FLR", weak="B", push=True)]),
            ("Moves/Heat Up/tile009.png", "Sweeping Strikes", 1, 2, 1, True,
             [move(1, "front", "aggro", push=True), attack(4, area=True, arcs="FL", weak="B"),
              attack(4, area=True, arcs="FR", weak="B")]),
        ],
    },
    # Corrupted Dragonslayer: no heat up point; the first three times one attack takes 4 or more
    # health off it (after Block/Resist, Bleed included), a random Heat Up card goes on top of
    # its deck. Darkness Falls is aimed at the aggro holder but hits its arc as any Area attack.
    "Old Dragonslayer": {
        "card": "Old Dragonslayer.png",
        "threat": 10, "health": 20, "block": 2, "resist": 2, "heat_up": 0, "deck": 4,
        "ability": "CORRUPTED_DRAGONSLAYER",
        "tile": "castle",
        "crop": (0.53, 0.285, 0.36),
        "cards": [
            ("Moves/tile001.png", "Leaping Darkness", 1, 1, 1, False,
             [leap("aggro"), attack(4, magic=True, area=True, arcs="FRBL")]),
            ("Moves/tile005.png", "Darkness Bolt", 3, 3, 1, False,
             [move(0, "front", "aggro"), attack(4, target="aggro", magic=True, weak="B")]),
            ("Moves/tile006.png", "Spear Sweep", 2, 2, 1, False,
             [attack(4, area=True, arcs="F", weak="L"), move(1, "front", push=True)]),
            ("Moves/tile007.png", "Spear Lunge", 2, 2, 1, False,
             [move(1, "front", "nearest", push=True), attack(5, target="nearest", weak="R")]),
            ("Moves/tile008.png", "Skewering Charge", 0, 1, 1, False,
             [move(2, "front", push=True, damage=4, weak="F")]),
            ("Moves/Heat Up/tile002.png", "Lunging Combo", 1, 2, 1, True,
             [attack(5, target="nearest"), move(1, "front", "nearest", push=True), attack(5, target="nearest", weak="B")]),
            ("Moves/Heat Up/tile003.png", "Massive Sweep", 2, 2, 1, True,
             [move(0, "front", "nearest"), attack(5, area=True, arcs="FLR", weak="B"), move(1, "front", push=True)]),
            ("Moves/Heat Up/tile004.png", "Darkness Falls", 2, 1, 1, True,
             [leap("aggro"), move(0, "front", "aggro"), attack(5, target="aggro", magic=True, area=True, arcs="F", weak="B")]),
        ],
    },
}
