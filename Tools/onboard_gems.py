"""Writes a Gem .tres (and copies its card) for each gem Matt typed out, Oct 2026.

    python Tools/onboard_gems.py

Each gem goes to Resources/Prefabs/Equipment/Gems/<Name>/<Name>.tres beside <Name>.png,
copied from the board game folder. The rule each gem carries is Gem.Effect, an ordinal;
EFFECT below must match that enum's order. Requirements are (Str, Dex, Int, Faith).
"""
import os
import shutil

PROJ = "C:/Users/Matt McClelland/Documents/Game Dev/Godot/Projects/Dark Souls DnD"
SRC = "C:/Users/Matt McClelland/Documents/Game Dev/Dark Souls Board Game/Images/General Rewards/Weapons/Gems"
OUT = PROJ + "/Resources/Prefabs/Equipment/Gems"

# Gem.Effect, in order.
EFFECT = ["NONE", "BLACK_DIE", "BLEED", "BLACK_DIE_MAGIC", "DAMAGE_MAGIC", "REACH_MAGIC", "HOLLOW",
          "MAGIC", "POISON", "RAW", "SIMPLE", "DAMAGE", "DAMAGE_TWO"]

# Cards edited for this project, used in place of the board game folder's.
EDITS = PROJ + "/Tools/CardEdits"

# Name, requirements (Str, Dex, Int, Faith), effect, rarity (Claude's ranking, agreed with Matt).
GEMS = [
    ("Blessed Gem", (0, 0, 0, 25), "BLACK_DIE", "UNCOMMON"),
    ("Blood Gem", (15, 0, 0, 0), "BLEED", "UNCOMMON"),
    ("Blue Titanite", (0, 0, 20, 0), "BLACK_DIE_MAGIC", "EPIC"),
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
    source = edited if os.path.exists(edited) else os.path.join(SRC, name + ".png")
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
    for gem in GEMS:
        write(*gem)
    print(f"{len(GEMS)} gems written to {OUT}")
