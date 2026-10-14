"""Writes a Gem .tres (and copies its card) for each gem Matt typed out, Oct 2026.

    python Tools/onboard_gems.py [name ...]

Each gem goes to Resources/Prefabs/Equipment/Gems/<Name>/<Name>.tres beside <Name>.png,
copied from the board game folder. The rule each gem carries is Gem.Effect, an ordinal;
EFFECT below must match that enum's order. Requirements are (Str, Dex, Int, Faith).
"""
import os
import shutil
import sys

PROJ = "C:/Users/Matt McClelland/Documents/Game Dev/Godot/Projects/Dark Souls DnD"
SRC = "C:/Users/Matt McClelland/Documents/Game Dev/Dark Souls Board Game/Images/General Rewards/Weapons/Gems"
OUT = PROJ + "/Resources/Prefabs/Equipment/Gems"

# Gem.Effect, in order.
EFFECT = ["NONE", "BLACK_DIE", "BLEED", "BLACK_DIE_MAGIC", "DAMAGE_MAGIC", "REACH_MAGIC", "HOLLOW",
          "MAGIC", "POISON", "RAW", "SIMPLE", "DAMAGE", "DAMAGE_TWO"]

# Cards edited for this project, used in place of the board game folder's.
EDITS = PROJ + "/Tools/CardEdits"

# Boss treasure: these come from a boss's folder and are written with bossTreasure = true, so
# they never reach the Merchant or map loot. The card art is the one listed here.
BOSSES = "C:/Users/Matt McClelland/Documents/Game Dev/Dark Souls Board Game/Images/Bosses"
BOSS_CARD = {
    # The Black Knight's card; it used to be onboarded from General Rewards as ordinary treasure.
    "Blue Titanite": BOSSES + "/1 - Mini/Black Knight/13_blue_titanite.png",
    # The Titanite Demon and the Stray Demon each pay a "Titanite" (+1 damage, no requirements).
    # The catalogue is keyed by name, so both pay this one gem; the Stray Demon's card is
    # Bosses/3 - Mega/Stray Demon/Titanite.png.
    "Titanite": BOSSES + "/1 - Mini/Titanite Demon/Titanite.png",
}

# Name, requirements (Str, Dex, Int, Faith), effect, rarity (Claude's ranking, agreed with Matt).
GEMS = [
    ("Blessed Gem", (0, 0, 0, 25), "BLACK_DIE", "UNCOMMON"),
    ("Blood Gem", (15, 0, 0, 0), "BLEED", "UNCOMMON"),
    ("Blue Titanite", (0, 0, 20, 0), "BLACK_DIE_MAGIC", "EPIC"),
    ("Titanite", (0, 0, 0, 0), "DAMAGE", "COMMON"),
    ("Carthus Flame Arc", (0, 0, 30, 0), "DAMAGE_MAGIC", "RARE"),
    ("Crystal Gem", (0, 0, 25, 0), "BLACK_DIE", "UNCOMMON"),
    ("Crystal Magic Weapon", (0, 0, 30, 0), "DAMAGE_MAGIC", "RARE"),
    ("Faron Flashsword", (0, 0, 25, 15), "REACH_MAGIC", "EPIC"),
    ("Heavy Gem", (25, 0, 0, 0), "BLACK_DIE", "UNCOMMON"),
    ("Hollow Gem", (0, 0, 15, 15), "HOLLOW", "COMMON"),
    ("Lightning Gem", (0, 0, 15, 0), "MAGIC", "COMMON"),
    ("Poison Gem", (0, 15, 0, 0), "POISON", "COMMON"),
    ("Raw Gem", (15, 0, 0, 0), "RAW", "RARE"),             # +1 stamina, Str 15: Tools/CardEdits/edit_cards.py
    ("Sharp Gem", (0, 25, 0, 0), "BLACK_DIE", "UNCOMMON"),
    ("Simple Gem", (0, 0, 0, 15), "SIMPLE", "RARE"),
    ("Titanite Scale", (20, 20, 0, 0), "DAMAGE_TWO", "EPIC"),  # +2, Str/Dex 20: Tools/CardEdits/edit_cards.py
    ("Titanite Shard", (0, 0, 0, 0), "DAMAGE", "COMMON"),
]

GEM_TYPE = 5       # Equipment.EquipmentType.Gem
RARITY = {"STARTER": 0, "COMMON": 1, "UNCOMMON": 2, "RARE": 3, "EPIC": 4, "LEGENDARY": 5}  # Equipment.Rarity


def write(name, reqs, effect, rarity):
    folder = os.path.join(OUT, name)
    os.makedirs(folder, exist_ok=True)
    edited = os.path.join(EDITS, name + ".png")
    boss = name in BOSS_CARD
    source = edited if os.path.exists(edited) else BOSS_CARD.get(name, os.path.join(SRC, name + ".png"))
    shutil.copyfile(source, os.path.join(folder, name + ".png"))
    s, d, i, f = reqs
    res = f"res://Resources/Prefabs/Equipment/Gems/{name}/{name}"
    text = "\n".join([
        '[gd_resource type="Resource" script_class="Gem" load_steps=3 format=3]',
        "",
        '[ext_resource type="Script" path="res://Scripts/Equipment/Gem.cs" id="1_gem"]',
        f'[ext_resource type="Texture2D" path="{res}.png" id="2_art"]',
        "",
        "[resource]",
        'script = ExtResource("1_gem")',
    ] + (["bossTreasure = true"] if boss else []) + [
        f'name = "{name}"',
        'image = ExtResource("2_art")',
        f"type = {GEM_TYPE}",
        f"rarity = {RARITY[rarity]}",
        "isUpgrade = true",
        f"effect = {EFFECT.index(effect)}",
        f"strengthReq = {s}",
        f"dexterityReq = {d}",
        f"intelligenceReq = {i}",
        f"faithReq = {f}",
        "",
    ])
    with open(os.path.join(folder, name + ".tres"), "w", encoding="utf-8") as out:
        out.write(text)


if __name__ == "__main__":
    # Names on the command line write only those gems; none writes them all.
    only = set(sys.argv[1:])
    done = [g for g in GEMS if not only or g[0] in only]
    for gem in done:
        write(*gem)
    print(f"{len(done)} gems written to {OUT}")
