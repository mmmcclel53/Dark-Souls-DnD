"""Writes a BoardLayout for each printed boss tile that a boss is fought on, and copies its art.

    python Tools/BossTiles/layouts.py

The board is the 7x7 grid of GameNodes turned 45° (Board Geometry in CLAUDE.md): grid (x, y)
sits u = x + y - 6 lattice steps right of the centre and v = y - x steps down. A boss tile
draws its circles at its own spacing, so each one's circles are listed here by (u, v) with the
centre measured off the art as a fraction of the tile (the bright rings were found by colour
and checked by eye against a 10% grid of the scan). The core-set tiles have 13 circles, the
inner diamond |u|, |v| <= 2.

Per tile: its circles, the boss's spawn circle (the red skull), the entrances (the row by the
door the party comes in at) and the way the boss faces at the start, towards that door, as a
grid step (down the board is (-1, 1)).

Writes Resources/Prefabs/BossTiles/<tile>.tres and Resources/Images/Backgrounds/Boss/<tile>.jpg.
"""
import os
import shutil

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
TILES = "C:/Users/Matt McClelland/Documents/Game Dev/Dark Souls Board Game/Images/Tiles/Boss/"
OUT = os.path.join(ROOT, "Resources", "Prefabs", "BossTiles")
ART = os.path.join(ROOT, "Resources", "Images", "Backgrounds", "Boss")
GRID = 7

LAYOUTS = {
    # The mini boss's hall: the door at the bottom, the skull at the top.
    "castle": {
        "node_size": 0.17,
        "circles": {
            (-2, -2): (0.163, 0.116), (0, -2): (0.500, 0.116), (2, -2): (0.837, 0.115),
            (-1, -1): (0.331, 0.296), (1, -1): (0.667, 0.295),
            (-2, 0): (0.163, 0.475), (0, 0): (0.499, 0.473), (2, 0): (0.837, 0.473),
            (-1, 1): (0.330, 0.652), (1, 1): (0.667, 0.652),
            (-2, 2): (0.162, 0.832), (0, 2): (0.498, 0.833), (2, 2): (0.838, 0.833),
        },
        "spawn": (0, -2),
        "entrances": [(-2, 2), (0, 2), (2, 2)],
        "facing": (-1, 1),
    },
    # The core set's main boss tile: a round courtyard of 21 circles, |u|, |v| <= 3 without
    # the corners, the edge columns squeezed in. It has no door (in the book it joins the
    # mini boss tile), so the party comes in along the bottom.
    "generic": {
        "node_size": 0.16,
        "circles": {
            (-1, -3): (0.336, 0.036), (1, -3): (0.670, 0.035),
            (-2, -2): (0.169, 0.197), (0, -2): (0.504, 0.196), (2, -2): (0.836, 0.193),
            (-3, -1): (0.042, 0.353), (-1, -1): (0.336, 0.355), (1, -1): (0.668, 0.352), (3, -1): (0.958, 0.360),
            (-2, 0): (0.169, 0.512), (0, 0): (0.503, 0.511), (2, 0): (0.835, 0.509),
            (-3, 1): (0.042, 0.669), (-1, 1): (0.337, 0.668), (1, 1): (0.668, 0.666), (3, 1): (0.958, 0.676),
            (-2, 2): (0.170, 0.829), (0, 2): (0.501, 0.826), (2, 2): (0.835, 0.823),
            (-1, 3): (0.341, 0.963), (1, 3): (0.675, 0.961),
        },
        "spawn": (0, 0),
        "entrances": [(-1, 3), (1, 3)],
        "facing": (-1, 1),
    },
    # A grass bank with a stream through it (the Royal Rat Authority's): 17 circles, |u| <= 2
    # and |v| <= 3, at the generic tile's spacing; the boss starts on the skull in the middle
    # and the party comes in at the bottom.
    "river boss": {
        "node_size": 0.16,
        "circles": {
            (-1, -3): (0.335, 0.040), (1, -3): (0.669, 0.040),
            (-2, -2): (0.168, 0.192), (0, -2): (0.503, 0.192), (2, -2): (0.835, 0.192),
            (-1, -1): (0.335, 0.351), (1, -1): (0.667, 0.351),
            (-2, 0): (0.168, 0.508), (0, 0): (0.500, 0.500), (2, 0): (0.833, 0.508),
            (-1, 1): (0.336, 0.669), (1, 1): (0.668, 0.667),
            (-2, 2): (0.168, 0.827), (0, 2): (0.500, 0.825), (2, 2): (0.833, 0.823),
            (-1, 3): (0.340, 0.959), (1, 3): (0.675, 0.959),
        },
        "spawn": (0, 0),
        "entrances": [(-1, 3), (1, 3)],
        "facing": (-1, 1),
    },
    # The Asylum Demon's and Stray Demon's hall: 25 circles on the full diamond, |u| + |v| even
    # and both <= 3, the demon on the dais at the top (its skull has no ring, so its centre is
    # the skull's). The four sockets on the outer walkways are pillars (Pillars of Woe): no one
    # can stand on one until the demon smashes it.
    "Asylum Demon & Stray Demon": {
        "node_size": 0.13,
        "circles": {
            (-3, -3): (0.091, 0.094), (-1, -3): (0.362, 0.094), (1, -3): (0.632, 0.091), (3, -3): (0.902, 0.091),
            (-2, -2): (0.227, 0.228), (0, -2): (0.489, 0.215), (2, -2): (0.768, 0.224),
            (-3, -1): (0.092, 0.365), (-1, -1): (0.363, 0.364), (1, -1): (0.634, 0.362), (3, -1): (0.903, 0.360),
            (-2, 0): (0.228, 0.500), (0, 0): (0.498, 0.498), (2, 0): (0.769, 0.496),
            (-3, 1): (0.095, 0.635), (-1, 1): (0.364, 0.634), (1, 1): (0.634, 0.632), (3, 1): (0.904, 0.629),
            (-2, 2): (0.229, 0.771), (0, 2): (0.500, 0.768), (2, 2): (0.771, 0.766),
            (-3, 3): (0.095, 0.906), (-1, 3): (0.365, 0.905), (1, 3): (0.636, 0.902), (3, 3): (0.906, 0.899),
        },
        "pillars": [(-2, -2), (2, -2), (-2, 2), (2, 2)],
        "spawn": (0, -2),
        "entrances": [(-1, 3), (1, 3)],
        "facing": (-1, 1),
    },
}


def index(u, v):
    x, y = (u - v + 6) // 2, (u + v + 6) // 2
    assert (u + v) % 2 == 0 and 0 <= x < GRID and 0 <= y < GRID, (u, v)
    return y * GRID + x


def write(tile, layout):
    from PIL import Image, ImageOps
    source = Image.open(os.path.join(TILES, f"{tile}.jpg"))
    # Some scans (generic, river boss) are stored upside down with an EXIF tag saying so: image
    # viewers turn them, Godot does not. The circles were measured on the stored pixels, so they
    # turn with the art; the spawn, entrances and facing are already where they belong on the board.
    turned = source.getexif().get(274) == 3
    circles = {((-u, -v) if turned else (u, v)): ((1 - x, 1 - y) if turned else (x, y))
               for (u, v), (x, y) in layout["circles"].items()}
    cells = sorted((index(u, v), (round(x, 3), round(y, 3))) for (u, v), (x, y) in circles.items())
    lines = [
        '[gd_resource type="Resource" script_class="BoardLayout" load_steps=2 format=3]',
        "",
        '[ext_resource type="Script" path="res://Scripts/Bosses/BoardLayout.cs" id="1_layout"]',
        "",
        "[resource]",
        'script = ExtResource("1_layout")',
        f'tile = "{tile}"',
        f"nodeSize = {layout['node_size']}",
        f"cells = PackedInt32Array({', '.join(str(i) for i, _ in cells)})",
        f"centres = PackedVector2Array({', '.join(f'{x}, {y}' for _, (x, y) in cells)})",
        f"entrances = PackedInt32Array({', '.join(str(index(*e)) for e in layout['entrances'])})",
        f"pillars = PackedInt32Array({', '.join(str(index(*p)) for p in layout.get('pillars', []))})",
        f"bossSpawn = {index(*layout['spawn'])}",
        f"bossFacing = Vector2i({layout['facing'][0]}, {layout['facing'][1]})",
        "",
    ]
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, f"{tile}.tres"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines))
    os.makedirs(ART, exist_ok=True)
    # Turned the way it is meant to be seen, and resized: the newer scans are 5000px square,
    # far more than a board is ever drawn at.
    if turned or max(source.size) > 2048:
        art = ImageOps.exif_transpose(source).convert("RGB")
        if max(art.size) > 2048:
            art = art.resize((2048, round(2048 * art.size[1] / art.size[0])), Image.LANCZOS)
        art.save(os.path.join(ART, f"{tile}.jpg"), quality=92)
    else:
        shutil.copyfile(os.path.join(TILES, f"{tile}.jpg"), os.path.join(ART, f"{tile}.jpg"))
    print(f"{tile}: {len(cells)} nodes, spawn {index(*layout['spawn'])}, entrances {[index(*e) for e in layout['entrances']]}")


if __name__ == "__main__":
    for name, data in LAYOUTS.items():
        write(name, data)
