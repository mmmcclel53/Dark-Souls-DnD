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

Every boss gets its avatar (Tools/BossAvatars/generate.py) when it has one. The bosses in
boss_fights.py are fought for real: their data card, stats, special ability, tile and behaviour
cards are written too, and their card art copied under Resources/Prefabs/Bosses/<Name>/.

Writes Resources/Prefabs/Bosses/<Name>.tres.
"""
import glob
import os
import re
import shutil
import sys

from boss_fights import FIGHTS

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


ABILITY = {"NONE": 0, "HEAVY_BLOWS": 1, "FLYING_HIGH": 2, "ICE_IN_HIS_VEINS": 3, "TITANITE_CONSTRUCT": 4, "PARRY": 5, "BRUTAL_IMPACT": 6, "CORRUPTED_DRAGONSLAYER": 7, "PILLARS_OF_WOE": 8, "ROYAL_PROTECTION": 9}   # BossData.Ability
KINDS = {"MOVE": 0, "ATTACK": 1, "TURN_LEFT": 2, "TURN_RIGHT": 3, "ROTATE": 4, "LEAP": 5, "HEAL": 6, "SUMMON": 7}
TARGETS = {None: 0, "aggro": 1, "nearest": 2}
ARMS = {"front": 0, "back": 1, "left": 2, "right": 3}
ARC_BITS = {"F": 1, "R": 2, "B": 4, "L": 8}
CONDITIONS = {None: 4, "bleed": 0, "poison": 1, "frost": 2, "stagger": 3, "abyss": 5, "calamity": 6, "corrosion": 7, "curse": 8}
UNLIMITED_RANGE = 99


def arc_bits(letters):
    return sum(ARC_BITS[c] for c in letters)


def res_path(*parts):
    return "res://" + "/".join(["Resources", "Prefabs", "Bosses", *parts])


def boss_dir(folder):
    tier = printed_tier(folder)
    return os.path.join(BOSSES, folder) if tier == "4" else os.path.join(BOSSES, next(t for t in os.listdir(BOSSES) if t.split(" ")[0] == tier), folder)


def copy_art(source, folder_name, *target):
    # Keeps the source's own extension (the fan-made cards are jpg), so Godot imports it as what it is.
    target = (*target[:-1], os.path.splitext(target[-1])[0] + os.path.splitext(source)[1].lower())
    out = os.path.join(OUT, folder_name, *target)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    shutil.copyfile(source, out)
    return res_path(folder_name, *target)


def portrait(crop, card, framed=None):
    # The activation bar's 3:4 face. `framed` (centre x, top, bottom) is set by hand per boss to
    # show as much of the art as fits between the title and the Block/Resist shield; without
    # it, the face inside the avatar's circle.
    from PIL import Image
    w, h = Image.open(card).size
    if framed:
        cx, top, bottom = framed
        width = 0.75 * (bottom - top) * h / w
        return f"Rect2({cx - width / 2:.4f}, {top:.4f}, {width:.4f}, {bottom - top:.4f})"
    cx, cy, d = crop
    width = d * 0.8
    height = width * 4 / 3 * w / h
    return f"Rect2({cx - width / 2:.4f}, {cy - height / 2:.4f}, {width:.4f}, {height:.4f})"


def action_lines(sub_id, icon, script_id):
    lines = [f'[sub_resource type="Resource" id="{sub_id}"]', f'script = ExtResource("{script_id}")',
             f"kind = {KINDS[icon['kind']]}", f"target = {TARGETS[icon['target']]}", f"push = {'true' if icon['push'] else 'false'}"]
    if icon["kind"] == "SUMMON":
        lines += [f"nodes = {icon['nodes']}"]
    elif icon["kind"] == "MOVE":
        lines += [f"nodes = {icon['nodes']}", f"arm = {ARMS[icon['arm']]}", f"damage = {icon.get('damage', 0)}",
                  f"weak = {arc_bits(icon.get('weak', ''))}", f"condition = {CONDITIONS[icon.get('condition')]}"]
    elif icon["kind"] in ("LEAP", "HEAL"):
        lines += [f"damage = {icon.get('damage', 0)}", f"condition = {CONDITIONS[icon.get('condition')]}"]
    elif icon["kind"] == "ATTACK":
        lines += [f"damage = {icon['damage']}", f"magic = {'true' if icon['magic'] else 'false'}",
                  f"area = {'true' if icon['area'] else 'false'}", f"node = {'true' if icon['node'] else 'false'}",
                  f"arcs = {arc_bits(icon['arcs'])}", f"weak = {arc_bits(icon['weak'])}", f"condition = {CONDITIONS[icon['condition']]}"]
    return lines + [""]


def write(folder, shown, kind, final, treasure):
    name = shown.replace(",", "")
    ext = ['[ext_resource type="Script" path="res://Scripts/Bosses/BossData.cs" id="1_boss"]']
    subs = []
    props = []

    avatar = os.path.join(OUT, name, f"{name} - Avatar.png")
    if os.path.exists(avatar):
        ext.append(f'[ext_resource type="Texture2D" path="{res_path(name, name + " - Avatar.png")}" id="2_avatar"]')
        props.append('avatarTexture = ExtResource("2_avatar")')

    fight = FIGHTS.get(shown)
    if fight:
        source = boss_dir(folder)
        ext.append('[ext_resource type="Script" path="res://Scripts/Bosses/BossCard.cs" id="3_card"]')
        ext.append('[ext_resource type="Script" path="res://Scripts/Bosses/BossAction.cs" id="4_action"]')
        data_card = os.path.join(source, fight["card"])
        ext.append(f'[ext_resource type="Texture2D" path="{copy_art(data_card, name, name + ".png")}" id="5_data"]')
        card_ids = []
        for i, (file, card_name, rng, dodge, repeat, heat_up, icons) in enumerate(fight["cards"]):
            art = copy_art(os.path.join(source, file), name, "Cards", f"{card_name}.png")
            ext.append(f'[ext_resource type="Texture2D" path="{art}" id="c{i}_art"]')
            action_ids = []
            smashes = any(icon["kind"] == "PILLAR" for icon in icons)
            icons = [icon for icon in icons if icon["kind"] != "PILLAR"]
            for j, icon in enumerate(icons):
                action_ids.append(f"a{i}_{j}")
                subs += action_lines(action_ids[-1], icon, "4_action")
            card_ids.append(f"card{i}")
            subs += [f'[sub_resource type="Resource" id="card{i}"]', 'script = ExtResource("3_card")',
                     f"cardName = {quoted(card_name)}", f'texture = ExtResource("c{i}_art")',
                     f"range = {UNLIMITED_RANGE if rng is None else rng}", f"dodge = {dodge}", f"repeat = {repeat}",
                     f"heatUp = {'true' if heat_up else 'false'}", f"pillars = {'true' if smashes else 'false'}",
                     f"actions = Array[Object]([{', '.join(f'SubResource(\"{a}\")' for a in action_ids)}])", ""]
        props += ['cardTexture = ExtResource("5_data")', f"portraitRegion = {portrait(fight['crop'], data_card, fight.get('portrait'))}",
                  f"threatLevel = {fight['threat']}", f"health = {fight['health']}",
                  f"physicalDefense = {fight['block']}", f"magicalDefense = {fight['resist']}",
                  f"heatUp = {fight['heat_up']}", f"deckSize = {fight['deck']}", f"ability = {ABILITY[fight['ability']]}",
                  f"cards = Array[Object]([{', '.join(f'SubResource(\"{c}\")' for c in card_ids)}])",
                  f"tile = {quoted(fight['tile'])}"]
        if fight.get("summon"):
            ext.append(f'[ext_resource type="Resource" path="{fight["summon"]}" id="6_summon"]')
            props += ['summon = ExtResource("6_summon")', f"summonAtStart = {fight.get('summon_at_start', 0)}"]

    lines = [f'[gd_resource type="Resource" script_class="BossData" load_steps={len(ext) + len([l for l in subs if l.startswith("[sub")]) + 1} format=3]', ""]
    lines += ext + [""] + subs
    lines += [
        "[resource]",
        'script = ExtResource("1_boss")',
        f"bossName = {quoted(shown)}",
        f"kind = {kind}",
        f"final = {'true' if final else 'false'}",
        f"treasure = PackedStringArray({', '.join(quoted(t) for t in treasure)})",
    ] + props + [""]
    path = os.path.join(OUT, f"{name}.tres")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines))


if __name__ == "__main__":
    unknown = [f for f in FIGHTS if f not in {shown for shown, _ in TREASURE.values()}]
    if unknown: sys.exit(f"fought but not a boss here: {unknown}")
    known = catalogue()
    missing = [t for _, cards in TREASURE.values() for t in cards if t not in known]
    if missing: sys.exit(f"not in the catalogue: {missing}")
    os.makedirs(OUT, exist_ok=True)
    for folder, (shown, cards) in TREASURE.items():
        tier = printed_tier(folder)
        kind = SWAPS.get(folder, KIND[tier])
        write(folder, shown, kind, tier == "4", cards)
        print(f"{['Mini', 'Main', 'Mega'][kind]:4} {shown}{'  (final)' if tier == '4' else ''}{'  (swapped)' if folder in SWAPS else ''}")
