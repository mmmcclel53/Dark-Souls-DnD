"""Makes a board token (avatar) for every boss, in the printed enemy tokens' rim.

    python Tools/BossAvatars/generate.py [--only "Winged Knight;Sir Alonne"] [--preview]

No boss has a printed token. The rim is the printed enemy tokens' own: the per-pixel median
of the cleanest of them (their icons sit in different places, so the median keeps only the
rim and its gold ring). Inside the ring goes a cut of the boss's data card art, framed per
boss in CROPS as (card, centre x, centre y, diameter): the circle that shows inside the ring,
in fractions of the card's width, except the centre y, which is a fraction of its height.
The art sits between the title and the stat icons, so a diameter much over 0.45 shows them.
Its levels are stretched over what shows, since some scans are washed out, and it darkens
towards the ring.

Matt chose the card art in colour (Oct 2026). Turning it to ink on parchment like the
printed tokens was tried and rejected: the art is too dark and busy to give a silhouette.

Writes Resources/Prefabs/Bosses/<Boss>/<Model> - Avatar.png, where <Boss> is the BossData's
name (Ornstein and Smough share one, so do Pontiff Sulyvahn and his Shadow), or with
--preview only a contact sheet beside this script.
"""
import argparse
import glob
import os

import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
GAME = "C:/Users/Matt McClelland/Documents/Game Dev/Dark Souls Board Game/Images/"
BOSSES = GAME + "Bosses/"
OUT = os.path.join(ROOT, "Resources", "Prefabs", "Bosses")
SIZE = 512
RIM_TOKENS = 7          # how many of the most typical printed tokens the rim is the median of
ART_RADIUS = 0.665      # the gold ring's inner edge, as a fraction of the token's radius

# model -> (card, centre x, centre y, diameter)
CROPS = {
    "Asylum Demon": ("1 - Mini/Asylum Demon/Asylum Demon.png", 0.48, 0.26, 0.40),
    "Black Knight": ("1 - Mini/Black Knight/01_black_knight.png", 0.56, 0.26, 0.32),
    "Boreal Outrider Knight": ("1 - Mini/Boreal Outrider Knight/Boreal Outrider Knight.png", 0.58, 0.29, 0.38),
    "Gargoyle": ("1 - Mini/Gargoyle/Gargoyle.png", 0.45, 0.28, 0.44),
    "Heavy Knight": ("1 - Mini/Heavy Knight/01_heavy_knight.png", 0.50, 0.25, 0.38),
    "Old Dragonslayer": ("1 - Mini/Old Dragonslayer/Old Dragonslayer.png", 0.53, 0.285, 0.36),
    "Royal Rat Authority": ("1 - Mini/Royal Rat Authority/Royal Rat Authority.jpg", 0.76, 0.31, 0.34),
    "Titanite Demon": ("1 - Mini/Titanite Demon/Titanite Demon.png", 0.50, 0.285, 0.44),
    "Winged Knight": ("1 - Mini/Winged Knight/Winged Knight.png", 0.585, 0.27, 0.36),
    "Artorias": ("2 - Major/Artorias/Artorias.png", 0.52, 0.275, 0.42),
    "Dancer of the Boreal Valley": ("2 - Major/Dancer of the Boreal Valley/Dancer of the Boreal VAlley.png", 0.55, 0.31, 0.42),
    "Gravelord Nito": ("2 - Major/Gravelord Nito/14_gravelord_nito.png", 0.48, 0.26, 0.36),
    "Great Grey Wolf Sif": ("2 - Major/Grey Wolf Sif/Great Grey Wolf Sif.png", 0.47, 0.30, 0.38),
    "Crossbreed Priscilla": ("2 - Major/Priscilla/14_crossbreed_priscilla.png", 0.50, 0.26, 0.30),
    "Sanctuary Guardian": ("2 - Major/Sanctuary Guardian/Sanctuary Guardian.jpg", 0.38, 0.31, 0.36),
    "Sir Alonne": ("2 - Major/Sir Alonne/Sir Alonne.png", 0.50, 0.245, 0.36),
    "Smelter Demon": ("2 - Major/Smelter Demon/Smelter Demon.png", 0.50, 0.285, 0.44),
    "Dragon Slayer Ornstein": ("2 - Major/Smough & Ornstein/Dragon Slayer Ornstein.png", 0.52, 0.29, 0.38),
    "Executioner Smough": ("2 - Major/Smough & Ornstein/Executioner Smough.png", 0.50, 0.29, 0.38),
    "The Pursuer": ("2 - Major/The Pursuer/The Pursuer.png", 0.45, 0.28, 0.44),
    "Black Dragon Kalameet": ("3 - Mega/Black Dragon Kalameet/Black Draon Kalameet.png", 0.50, 0.31, 0.42),
    "Gaping Dragon": ("3 - Mega/Gaping Dragon/Gaping Dragon.png", 0.50, 0.28, 0.44),
    "Guardian Dragon": ("3 - Mega/Guardian Dragon/Guardian Dragon.png", 0.50, 0.31, 0.42),
    "Champion Gundyr": ("3 - Mega/Gundyr/Champion Gundyr.jpg", 0.52, 0.27, 0.32),
    "Manus, Father of the Abyss": ("3 - Mega/Manus/Manus, Father of the Abyss.png", 0.55, 0.30, 0.38),
    "Old Iron King": ("3 - Mega/Old Iron King/Old Iron King.png", 0.50, 0.28, 0.42),
    "Pontiff Sulyvahn": ("3 - Mega/Pontiff/Pontiff Sulyvahn.jpg", 0.50, 0.29, 0.32),
    "Pontiff's Shadow": ("3 - Mega/Pontiff/Pontiff's Shadow.jpg", 0.50, 0.29, 0.32),
    "Stray Demon": ("3 - Mega/Stray Demon/Stray Demon.png", 0.50, 0.27, 0.42),
    "The Four Kings": ("3 - Mega/The Four Kings/The Four Kings.png", 0.50, 0.28, 0.44),
    "The Last Giant": ("3 - Mega/The Last Giant/The Last Giant.png", 0.48, 0.27, 0.42),
    "Vordt of the Boreal Valley": ("3 - Mega/Vordt/Vordt of the Boreal Valley.png", 0.55, 0.31, 0.42),
    "Soul of Cinder": ("4 - Soul of Cinder (Final Boss)/Soul of Cinder.jpg", 0.50, 0.25, 0.34),
}

# models whose avatar goes with another boss's (the BossData's name)
BOSS_OF = {
    "Dragon Slayer Ornstein": "Smough & Ornstein",
    "Executioner Smough": "Smough & Ornstein",
    "Pontiff's Shadow": "Pontiff Sulyvahn",
}


def radius_grid(n):
    yy, xx = np.mgrid[:n, :n]
    return np.hypot(xx - (n - 1) / 2, yy - (n - 1) / 2) / (n / 2)


def rim():
    """The printed rim at SIZE, from the median of the printed enemy tokens most like it."""
    n = 284
    paths = [p for p in sorted(glob.glob(GAME + "Enemies/*/*Avatar*.png")) if Image.open(p).size[0] >= 180]
    stack = np.stack([np.array(Image.open(p).convert("RGBA").resize((n, n), Image.LANCZOS)).astype(float) for p in paths])
    r = radius_grid(n)
    band = (r > 0.70) & (r < 0.97)
    median = np.median(stack, 0)
    distance = [np.abs(s[..., :3] - median[..., :3]).mean(-1)[band].mean() for s in stack]
    best = stack[np.argsort(distance)[:RIM_TOKENS]]
    out = Image.fromarray(np.median(best, 0).clip(0, 255).astype(np.uint8))
    return out.resize((SIZE, SIZE), Image.LANCZOS)


def cut(name):
    card, cx, cy, diameter = CROPS[name]
    image = Image.open(BOSSES + card).convert("RGB")
    w, h = image.size
    side = diameter / ART_RADIUS * w
    box = (cx * w - side / 2, cy * h - side / 2, cx * w + side / 2, cy * h + side / 2)
    return image.crop(tuple(int(round(v)) for v in box)).resize((SIZE, SIZE), Image.LANCZOS)


def art_style(art):
    a = np.array(art).astype(float)
    r = radius_grid(SIZE)
    lo, hi = np.percentile(a.mean(-1)[r < ART_RADIUS], (1, 99.5))
    a = (a - lo) * 255 / max(hi - lo, 1)
    shade = 1 - 0.45 * np.clip((r - 0.45) / (ART_RADIUS - 0.45), 0, 1) ** 2
    return a * shade[..., None]


def token(name, ring):
    inner = art_style(cut(name))
    r = radius_grid(SIZE)
    inside = np.clip((ART_RADIUS - r) * SIZE / 2 / 1.5, 0, 1)[..., None]
    rim = np.array(ring).astype(float)
    rgb = inner * inside + rim[..., :3] * (1 - inside)
    alpha = rim[..., 3:4]
    return Image.fromarray(np.dstack([rgb, alpha]).clip(0, 255).astype(np.uint8))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--only", default="")
    parser.add_argument("--preview", action="store_true")
    args = parser.parse_args()
    names = [n.strip() for n in args.only.split(";") if n.strip()] or list(CROPS)
    ring = rim()
    tokens = [(n, token(n, ring)) for n in names]
    if args.preview:
        cell = 260
        cols = min(len(tokens), 6)
        sheet = Image.new("RGBA", (cols * cell, ((len(tokens) + cols - 1) // cols) * (cell + 18)), (40, 40, 40, 255))
        draw = ImageDraw.Draw(sheet)
        for i, (n, t) in enumerate(tokens):
            x, y = (i % cols) * cell, (i // cols) * (cell + 18)
            sheet.alpha_composite(t.resize((cell - 10, cell - 10), Image.LANCZOS), (x + 5, y + 5))
            draw.text((x + 6, y + cell), n, fill=(255, 220, 120, 255))
        path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "preview.png")
        sheet.save(path)
        print("wrote", path)
        return
    for n, t in tokens:
        folder = os.path.join(OUT, BOSS_OF.get(n, n).replace(",", ""))
        os.makedirs(folder, exist_ok=True)
        t.save(os.path.join(folder, f"{n.replace(',', '')} - Avatar.png"))
        print("wrote", os.path.relpath(folder, OUT), "/", n)


if __name__ == "__main__":
    main()
