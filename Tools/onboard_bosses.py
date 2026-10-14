"""Writes a BossData resource for every boss in the board game's Images/Bosses folder.

    python Tools/onboard_bosses.py

A boss's tier is the folder it is printed under (1 - Mini, 2 - Major, 3 - Mega, 4 - Soul of
Cinder), except for this project's swaps (Matt, Oct 2026, from a threat model of the cards:
health against Block/Resist, damage per activation):

  Smough & Ornstein  Major -> Mega   two bosses acting every turn; it rates with the top Megas
  Vordt              Mega  -> Major  a third of its deck is charges; it rates with Dancer

TREASURE lists each boss's cards by catalogue name, read off its folder. Keys (now the mega
gates' keys), the Homeward Bone (map loot) and the Bequeathed Lord Soul (cut) are not
treasure. Every name must be the `name` of an equipment resource; the script stops if one
is not, rather than write a card the payout could never mint.

Writes Resources/Prefabs/Bosses/<Name>.tres.
"""
import glob
import os
import re
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
BOSSES = "C:/Users/Matt McClelland/Documents/Game Dev/Dark Souls Board Game/Images/Bosses/"
OUT = os.path.join(ROOT, "Resources", "Prefabs", "Bosses")
KIND = {"1": 0, "2": 1, "3": 2, "4": 1}          # WorldBossKind: MINI, MAIN, MEGA; Cinder is a main boss
SWAPS = {"Smough & Ornstein": 2, "Vordt": 1}

# boss folder -> (name shown, treasure)
TREASURE = {
    "Asylum Demon": ("Asylum Demon", ["Demon's Great Hammer"]),
    "Black Knight": ("Black Knight", ["Black Knight Halberd", "Black Knight Shield (Boss)", "Blue Titanite"]),
    "Boreal Outrider Knight": ("Boreal Outrider Knight", ["Irithyll Rapier", "Irithyll Straight Sword", "Outrider Armour"]),
    "Gargoyle": ("Gargoyle", ["Gargoyle Tail Axe", "Gargoyle's Halberd", "Gargoyle's Shield"]),
    "Heavy Knight": ("Heavy Knight", ["Greatsword", "Steel Armour", "Tower Shield"]),
    "Old Dragonslayer": ("Old Dragonslayer", ["Old Dragonslayer Spear", "Old Leo Ring"]),
    "Royal Rat Authority": ("Royal Rat Authority", []),
    "Titanite Demon": ("Titanite Demon", ["Titanite Catch Pole", "Titanite"]),
    "Winged Knight": ("Winged Knight", ["Winged Knight Armour", "Winged Knight Halberd", "Winged Knight Twin Axes"]),
    "Artorias": ("Artorias", ["Abyss Greatsword", "Dark Silver Tracer", "Gold Tracer"]),
    "Dancer of the Boreal Valley": ("Dancer of the Boreal Valley", ["Dancer Armour", "Dancer's Enchanted Swords", "Soothing Sunlight"]),
    "Gravelord Nito": ("Gravelord Nito", ["Gravelord Sword", "Gravelord Sword Dance"]),
    "Grey Wolf Sif": ("Great Grey Wolf Sif", ["Cursed Greatsword of Artorias", "Greatshield of Artorias", "Greatsword of Artorias"]),
    "Priscilla": ("Crossbreed Priscilla", ["Lifehunt Scythe", "Priscilla's Dagger"]),
    "Sanctuary Guardian": ("Sanctuary Guardian", []),
    "Sir Alonne": ("Sir Alonne", ["Alonne Armour", "Bewitched Alonne Sword"]),
    "Smelter Demon": ("Smelter Demon", ["Aged Smelter Sword", "Smelter Demon Armour", "Smelter Sword"]),
    "Smough & Ornstein": ("Smough & Ornstein", ["Dragonslayer Armour", "Dragonslayer Spear", "Smough's Armour", "Smough's Hammer"]),
    "The Pursuer": ("The Pursuer", ["Pursuer's Greatshield", "Pursuer's Ultra Greatsword", "Ring of Blades"]),
    "Black Dragon Kalameet": ("Black Dragon Kalameet", ["Obsidian Greatsword", "Calamity Ring"]),
    "Gaping Dragon": ("Gaping Dragon", ["Dragon King Greataxe"]),
    "Guardian Dragon": ("Guardian Dragon", ["Drakewing Ultra Greatsword", "Spitfire Spear"]),
    "Gundyr": ("Champion Gundyr", []),
    "Manus": ("Manus, Father of the Abyss", []),
    "Old Iron King": ("Old Iron King", ["Blinding Bolt", "Iron King Hammer"]),
    "Pontiff": ("Pontiff Sulyvahn", []),
    "Stray Demon": ("Stray Demon", ["Titanite"]),
    "The Four Kings": ("The Four Kings", ["Four Kings Sword"]),
    "The Last Giant": ("The Last Giant", ["Giant Stone Axe"]),
    "Vordt": ("Vordt of the Boreal Valley", ["Vordt's Great Hammer", "Pontiff's Left Eye"]),
    "4 - Soul of Cinder (Final Boss)": ("Soul of Cinder", []),
}


def printed_tier(folder):
    for tier in os.listdir(BOSSES):
        if tier == folder or os.path.isdir(os.path.join(BOSSES, tier, folder)):
            return tier.split(" ")[0]
    sys.exit(f"no folder for {folder} under {BOSSES}")


def catalogue():
    names = set()
    for path in glob.glob(os.path.join(ROOT, "Resources", "Prefabs", "Equipment", "**", "*.tres"), recursive=True):
        m = re.search(r'^name = "(.+)"$', open(path, encoding="utf-8").read(), re.M)
        if m: names.add(m.group(1).replace('\\"', '"'))
    return names


def quoted(s):
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"'


def write(folder, shown, kind, final, treasure):
    lines = [
        '[gd_resource type="Resource" script_class="BossData" load_steps=2 format=3]',
        "",
        '[ext_resource type="Script" path="res://Scripts/Bosses/BossData.cs" id="1_boss"]',
        "",
        "[resource]",
        'script = ExtResource("1_boss")',
        f"bossName = {quoted(shown)}",
        f"kind = {kind}",
        f"final = {'true' if final else 'false'}",
        f"treasure = PackedStringArray({', '.join(quoted(t) for t in treasure)})",
        "",
    ]
    path = os.path.join(OUT, f"{shown.replace(',', '')}.tres")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines))


if __name__ == "__main__":
    known = catalogue()
    missing = [t for _, cards in TREASURE.values() for t in cards if t not in known]
    if missing: sys.exit(f"not in the catalogue: {missing}")
    os.makedirs(OUT, exist_ok=True)
    for folder, (shown, cards) in TREASURE.items():
        tier = printed_tier(folder)
        kind = SWAPS.get(folder, KIND[tier])
        write(folder, shown, kind, tier == "4", cards)
        print(f"{['Mini', 'Main', 'Mega'][kind]:4} {shown}{'  (final)' if tier == '4' else ''}{'  (swapped)' if folder in SWAPS else ''}")
