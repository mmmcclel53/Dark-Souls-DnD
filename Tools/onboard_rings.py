"""Writes a Ring .tres (and copies its card) for each ring Matt typed out, Oct 2026.

    python Tools/onboard_rings.py

Each ring goes to Resources/Prefabs/Equipment/Rings/<Name>/<Name>.tres beside <Name>.png,
copied from the board game folder. The rule each ring carries is Ring.Effect, an ordinal;
EFFECT below must match that enum's order. Requirements are (Str, Dex, Int, Faith).
Rarities are Claude's ranking, agreed with Matt (Oct 2026). Bellowing Dragoncrest and Great
Swamp are cut (they only ever helped one class's Heroic Action); their Effect values stay.
Re-running overwrites the .tres files; their Effect ordinals do not change.
"""
import os
import shutil

PROJ = "C:/Users/Matt McClelland/Documents/Game Dev/Godot/Projects/Dark Souls DnD"
SRC = "C:/Users/Matt McClelland/Documents/Game Dev/Dark Souls Board Game/Images/General Rewards/Armour/Rings"
OUT = PROJ + "/Resources/Prefabs/Equipment/Rings"

# Ring.Effect, in order.
EFFECT = ["NONE", "BELLOWING_DRAGONCREST", "BLUE_TEARSTONE", "CARTHUS_MILKRING", "CHLORANTHY",
          "COVETOUS_SILVER_SERPENT", "DARK_WOOD_GRAIN", "DIVINE_BLESSING", "DUSK_CROWN", "GREAT_SWAMP",
          "HORNET", "KNIGHT_SLAYER", "MAGIC_STONEPLATE", "OBSCURING", "RED_TEARSTONE", "RING_OF_FAVOUR",
          "SUN_PRINCESS", "TINY_BEING", "WOLF"]

RINGS = [
    ("Blue Tearstone Ring", (0, 25, 0, 25), "BLUE_TEARSTONE", "UNCOMMON"),
    ("Carthus Milkring", (24, 30, 0, 0), "CARTHUS_MILKRING", "EPIC"),
    ("Chloranthy Ring", (18, 18, 18, 18), "CHLORANTHY", "LEGENDARY"),
    ("Covetous Silver Serpent Ring", (0, 31, 27, 0), "COVETOUS_SILVER_SERPENT", "UNCOMMON"),
    ("Dark Wood Grain Ring", (0, 26, 0, 0), "DARK_WOOD_GRAIN", "COMMON"),
    ("Divine Blessing", (0, 0, 0, 30), "DIVINE_BLESSING", "LEGENDARY"),  # Faith 30, once per rest: Tools/CardEdits
    ("Dusk Crown Ring", (0, 0, 30, 0), "DUSK_CROWN", "RARE"),
    ("Hornet Ring", (0, 30, 0, 0), "HORNET", "COMMON"),
    ("Knight Slayer's Ring", (30, 0, 0, 0), "KNIGHT_SLAYER", "UNCOMMON"),
    ("Magic Stoneplate Ring", (0, 0, 0, 32), "MAGIC_STONEPLATE", "UNCOMMON"),
    ("Obscuring Ring", (0, 28, 0, 20), "OBSCURING", "RARE"),
    ("Red Tearstone Ring", (0, 0, 0, 20), "RED_TEARSTONE", "COMMON"),
    ("Ring of Favour", (0, 21, 0, 21), "RING_OF_FAVOUR", "UNCOMMON"),
    ("Sun Princess Ring", (30, 0, 0, 25), "SUN_PRINCESS", "EPIC"),
    ("Tiny Being's Ring", (0, 0, 0, 20), "TINY_BEING", "COMMON"),
    ("Wolf Ring", (25, 35, 25, 0), "WOLF", "LEGENDARY"),
]

RING_TYPE = 4      # Equipment.EquipmentType.Ring
RARITY = {"STARTER": 0, "COMMON": 1, "UNCOMMON": 2, "RARE": 3, "EPIC": 4, "LEGENDARY": 5}  # Equipment.Rarity

# Cards edited for this project (Tools/CardEdits/edit_cards.py), used in place of the board
# game folder's.
EDITS = PROJ + "/Tools/CardEdits"


def write(name, reqs, effect, rarity):
    folder = os.path.join(OUT, name)
    os.makedirs(folder, exist_ok=True)
    edited = os.path.join(EDITS, name + ".png")
    source = edited if os.path.exists(edited) else os.path.join(SRC, name + ".png")
    shutil.copyfile(source, os.path.join(folder, name + ".png"))
    s, d, i, f = reqs
    res = f"res://Resources/Prefabs/Equipment/Rings/{name}/{name}"
    text = "\n".join([
        '[gd_resource type="Resource" script_class="Ring" load_steps=3 format=3]',
        "",
        '[ext_resource type="Script" path="res://Scripts/Equipment/Ring.cs" id="1_ring"]',
        f'[ext_resource type="Texture2D" path="{res}.png" id="2_art"]',
        "",
        "[resource]",
        'script = ExtResource("1_ring")',
        f'name = "{name}"',
        'image = ExtResource("2_art")',
        f"type = {RING_TYPE}",
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
    for ring in RINGS:
        write(*ring)
    print(f"{len(RINGS)} rings written to {OUT}")
