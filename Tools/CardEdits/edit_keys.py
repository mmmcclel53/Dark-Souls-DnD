"""Edits the boss key cards for the mega boss gates (Matt, Oct 2026):

  Big Pilgrim's Key, Blighttown Key, Soldier Key
      their text -> "You may unlock a certain door"
  Big Pilgrim's Key
      Dexterity and Intelligence 17 -> 0

    python Tools/CardEdits/edit_keys.py

The method is edit_cards.py's: nothing is drawn with a computer font. Every letter is a
printed one. Blighttown Key's own print gives "You may" exactly as set (no key card has a
capital U, so "Used to..." could not be printed) and the letters of "unlock a certain door",
taken from "encounter cards" first because "face-down" splits its w. Blighttown has no i, so
the i is Big Pilgrim's Key's, from "this". Each card's old text comes off with the paper's
grain put back, faded out over a few pixels so no seam shows, and the new line is laid in
that card's own ink, scaled by the width of the card's own printed "You may" and centred
where its text was. Big Pilgrim's Key's two 17s give way to copies of its own Strength 0.

Writes Tools/CardEdits/<Name>.png and copies it to Resources/Images/Sprites/Items/Keys/,
where the key's loot reveal reads it. The originals in the board game folder are untouched.
"""
import os
import shutil
import numpy as np
from PIL import Image
from scipy import ndimage

from edit_cards import split, GRAIN

BOSSES = "C:/Users/Matt McClelland/Documents/Game Dev/Dark Souls Board Game/Images/Bosses/"
OUT = os.path.dirname(os.path.abspath(__file__))
SPRITES = os.path.join(OUT, "..", "..", "Resources", "Images", "Sprites", "Items", "Keys")
SMALL = "acemnorsuvwxz"          # x-height letters, for a line's baseline

# box: the text block; bands: one per printed line, so no letter joins one above or below;
# flag: the language flag's top-left, kept out; you_x: where the printed "You may" begins
# (the parchment's edge leaves specks before it).
CARDS = {
    "Big Pilgrim's Key": dict(path=BOSSES + "1 - Mini/Asylum Demon/Big Pilgrim's Key.png", box=(58, 830, 679, 954),
                              bands=[(840, 870), (871, 907), (908, 941)], flag=(642, 906), you_x=101),
    "Blighttown Key": dict(path=BOSSES + "3 - Mega/Gaping Dragon/Blighttown Key.png", box=(60, 853, 612, 942),
                           bands=[(864, 901), (904, 932)], flag=None, you_x=153),
    "Soldier Key": dict(path=BOSSES + "3 - Mega/The Last Giant/Soldier Key.png", box=(44, 812, 669, 930),
                        bands=[(822, 854), (855, 887), (892, 924)], flag=(612, 896), you_x=65),
}
BLIGHTTOWN_TEXT = ["You may look at face-down", "encounter cards"]
NEW_WORDS = "unlock a certain door"
I_FROM_PILGRIM = (364, 843, 370, 866)        # the i of "this", with its dot; baseline 866
PILGRIM_STATS = (125, 185, 171, 440)         # the requirement digits, inside the panel's frame lines


def load(path):
    return np.asarray(Image.open(path).convert("RGBA")).astype(float)


def soft_ink(alpha, labels, ids, box):
    """A glyph's ink with its soft edge (pixels up to 2 px out of its own labels)."""
    dist = ndimage.distance_transform_edt(~np.isin(labels, list(ids)))
    mask = dist <= 2
    ys, xs = np.nonzero(mask)
    x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
    return np.where(mask, alpha, 0)[y0:y1 + 1, x0:x1 + 1], x0 + box[0], y0 + box[1]


def segment(card, spec):
    """Glyphs line by line inside each band; pieces stacked over each other (an i's dot) join."""
    box = spec["box"]
    _, _, alpha = split(card, box)
    ink = alpha > 0.6
    labels = np.zeros(ink.shape, dtype=int)
    lines, next_id = [], 1
    for top, bottom in spec["bands"]:
        band = np.zeros_like(ink)
        band[top - box[1]:bottom - box[1] + 1] = ink[top - box[1]:bottom - box[1] + 1]
        lab, _ = ndimage.label(band)
        comps = []
        for i, (sy, sx) in enumerate(ndimage.find_objects(lab), 1):
            x0, y0, x1, y1 = sx.start + box[0], sy.start + box[1], sx.stop - 1 + box[0], sy.stop - 1 + box[1]
            w, h = x1 - x0 + 1, y1 - y0 + 1
            if w * h < 6 or w > 34 or x0 <= box[0] + 3 or x1 >= box[2] - 4: continue
            labels[lab == i] = next_id
            comps.append({"ids": {next_id}, "x0": x0, "y0": y0, "x1": x1, "y1": y1})
            next_id += 1
        merged = True
        while merged:
            merged = False
            for a in comps:
                for b in comps:
                    if a is b: continue
                    overlap = min(a["x1"], b["x1"]) - max(a["x0"], b["x0"]) + 1
                    if overlap >= 0.6 * min(a["x1"] - a["x0"] + 1, b["x1"] - b["x0"] + 1):
                        a["ids"] |= b["ids"]
                        a["x0"], a["x1"] = min(a["x0"], b["x0"]), max(a["x1"], b["x1"])
                        a["y0"], a["y1"] = min(a["y0"], b["y0"]), max(a["y1"], b["y1"])
                        comps.remove(b)
                        merged = True
                        break
                if merged: break
        lines.append(sorted(comps, key=lambda c: c["x0"]))
    dist, (iy, ix) = ndimage.distance_transform_edt(labels == 0, return_indices=True)
    owner = np.where(dist <= 2, labels[iy, ix], 0)
    return alpha, owner, lines


def words_of(line):
    gaps = [b["x0"] - a["x1"] - 1 for a, b in zip(line, line[1:])]
    cut = max(5, np.median(gaps) + 3.5)
    words, current = [], [line[0]]
    for gap, c in zip(gaps, line[1:]):
        if gap > cut:
            words.append(current)
            current = [c]
        else:
            current.append(c)
    words.append(current)
    return words


def bands_of(card, spec):
    """Per line: baseline and the x extent of its print."""
    box = spec["box"]
    _, _, alpha = split(card, box)
    out = []
    for top, bottom in spec["bands"]:
        ink = alpha[top - box[1]:bottom - box[1] + 1, 4:-4] > 0.6
        if spec["flag"]:
            ink[max(0, spec["flag"][1] - top):, spec["flag"][0] - box[0] - 4:] = False
        profile = ink.sum(1)
        core = np.nonzero(profile > 0.55 * profile.max())[0]
        cols = np.nonzero(ink.sum(0))[0]
        out.append(dict(base=top + core[-1], x0=cols[0] + box[0] + 4, x1=cols[-1] + box[0] + 4))
    return out


def you_may_width(card, spec):
    """Width of the card's own printed "You may": the first two runs of inked columns on its
    first line from you_x, split at word gaps. The cards are set at slightly different sizes,
    and this is the same two words on every one, so it is the scale between them."""
    box = spec["box"]
    top, bottom = spec["bands"][0]
    _, _, alpha = split(card, box)
    cols = np.nonzero((alpha[top - box[1]:bottom - box[1] + 1] > 0.6).sum(0))[0] + box[0]
    cols = cols[cols >= spec["you_x"]]
    runs, start = [], cols[0]
    for a, b in zip(cols, cols[1:]):
        if b - a > 6:
            runs.append((start, a))
            start = b
    runs.append((start, cols[-1]))
    return runs[1][1] - runs[0][0] + 1


def scale(glyph, f):
    if abs(f - 1) < 0.01: return glyph
    h, w = glyph["ink"].shape
    im = Image.fromarray((glyph["ink"] * 255).astype(np.uint8))
    ink = np.asarray(im.resize((max(1, round(w * f)), max(1, round(h * f))), Image.LANCZOS)).astype(float) / 255
    return dict(ink=ink, edge=round(glyph["edge"] * f), top=round(glyph["top"] * f), width=round(glyph["width"] * f))


def blighttown_library():
    """Blighttown Key's letters, its "You may" as set, its spacing, and the width it scales by."""
    spec = CARDS["Blighttown Key"]
    card = load(spec["path"])
    box = spec["box"]
    alpha, owner, lines = segment(card, dict(spec, flag=None))

    def ink(c):
        mask = np.isin(owner, list(c["ids"]))
        ys, xs = np.nonzero(mask)
        x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
        return np.where(mask, alpha, 0)[y0:y1 + 1, x0:x1 + 1], x0 + box[0], y0 + box[1]

    lib, gaps_in, gaps_between, you_may = {}, [], [], None
    order = list(zip(lines, BLIGHTTOWN_TEXT))
    for line, text in order[1:] + order[:1]:
        words = words_of(line)
        expect = text.split(" ")
        assert [len(w) for w in words] == [len(t) for t in expect], (text, [len(w) for w in words])
        base = int(np.median([c["y1"] for w, t in zip(words, expect) for c, ch in zip(w, t) if ch in SMALL]))
        for w, t in zip(words, expect):
            gaps_in += [b["x0"] - a["x1"] - 1 for a, b in zip(w, w[1:])]
            for c, ch in zip(w, t):
                if ch not in lib:
                    g, ix, iy = ink(c)
                    lib[ch] = dict(ink=g, edge=c["x0"] - ix, top=iy - base, width=c["x1"] - c["x0"] + 1)
        gaps_between += [b[0]["x0"] - a[-1]["x1"] - 1 for a, b in zip(words, words[1:])]
        if text == BLIGHTTOWN_TEXT[0]:
            pieces = []
            for w in words[:2]:
                for c in w:
                    g, ix, iy = ink(c)
                    pieces.append((g, ix - words[0][0]["x0"], iy - base))
            you_may = (pieces, words[1][-1]["x1"] - words[0][0]["x0"] + 1)
    reference = you_may_width(card, spec)

    pilgrim = CARDS["Big Pilgrim's Key"]
    pcard = load(pilgrim["path"])
    pbox = (I_FROM_PILGRIM[0] - 6, I_FROM_PILGRIM[1] - 6, I_FROM_PILGRIM[2] + 7, I_FROM_PILGRIM[3] + 5)
    _, _, palpha = split(pcard, pbox)
    plabels, _ = ndimage.label(palpha > 0.6)
    ids = {l for l in np.unique(plabels[6:-5, 6:-7]) if l}
    g, ix, iy = soft_ink(palpha, plabels, ids, pbox)
    lib["i"] = scale(dict(ink=g, edge=I_FROM_PILGRIM[0] - ix, top=iy - I_FROM_PILGRIM[3],
                          width=I_FROM_PILGRIM[2] - I_FROM_PILGRIM[0] + 1), reference / you_may_width(pcard, pilgrim))
    return dict(lib=lib, you_may=you_may, letter=int(round(np.median(gaps_in))),
                word=int(round(np.median(gaps_between))), reference=reference)


def fill_paper(region, alpha, old, rng, sigma=5):
    """Paper over the old print: the local average of the clean paper around it (the closing
    estimate is the brightest nearby paper, which shows as pale patches over a whole paragraph),
    plus that paper's own grain."""
    clean = (alpha < GRAIN) & ~ndimage.binary_dilation(alpha > 0.4, iterations=2)
    weight = ndimage.gaussian_filter(clean.astype(float), sigma)
    mean = np.stack([ndimage.gaussian_filter(region[..., c] * clean, sigma) for c in range(3)], -1) \
        / np.maximum(weight, 1e-3)[..., None]
    grain = (region - mean)[clean]
    out = region.copy()
    out[old] = mean[old] + grain[rng.integers(0, len(grain), old.sum())]
    return out


def feathered(region, alpha, old, rng, reach=4):
    """fill_paper, faded out over a few pixels past the lifted area so no seam shows."""
    filled = fill_paper(region, alpha, ndimage.binary_dilation(old, iterations=reach), rng)
    w = np.clip(1 - ndimage.distance_transform_edt(~old) / reach, 0, 1)[..., None]
    return region * (1 - w) + filled * w


def lay(out, ink, x, y, colour):
    a = np.clip((ink - GRAIN) / (1 - GRAIN), 0, 1)[..., None]
    h, w = ink.shape
    out[y:y + h, x:x + w] = out[y:y + h, x:x + w] * (1 - a) + colour * a


def rewrite(name, library, rng):
    spec = CARDS[name]
    card = load(spec["path"])
    box = spec["box"]
    region, _, alpha = split(card, box)
    lines = bands_of(card, spec)
    f = you_may_width(card, spec) / library["reference"]
    ink_colour = np.median(region[alpha > 0.9], axis=0)

    old = np.zeros(alpha.shape, bool)
    y0, y1 = spec["bands"][0][0] - 2, spec["bands"][-1][1] + 4
    x0, x1 = min(l["x0"] for l in lines) - 3, max(l["x1"] for l in lines) + 4
    old[y0 - box[1]:y1 - box[1], x0 - box[0]:x1 - box[0]] = True
    old &= ndimage.binary_dilation(alpha > 0.12, iterations=3)   # the soft edge too, or it rings
    if spec["flag"]:
        old[spec["flag"][1] - box[1]:, spec["flag"][0] - box[0]:] = False
    out = feathered(region, alpha, old, rng)

    pieces = []
    you_may, printed_width = library["you_may"]
    for ink, x, top in you_may:
        g = scale(dict(ink=ink, edge=0, top=top, width=1), f)
        pieces.append((g["ink"], round(x * f), g["top"]))
    word, letter = round(library["word"] * f), round(library["letter"] * f)
    x = round(printed_width * f) + word
    for w in NEW_WORDS.split(" "):
        for i, ch in enumerate(w):
            g = scale(library["lib"][ch], f)
            pieces.append((g["ink"], x - g["edge"], g["top"]))
            x += g["width"] + (letter if i < len(w) - 1 else 0)
        x += word
    width = x - word

    left = round(np.mean([(l["x0"] + l["x1"]) / 2 for l in lines]) - width / 2)
    bases = [l["base"] for l in lines]
    baseline = bases[len(bases) // 2] if len(bases) % 2 else round(np.mean(bases))
    for ink, gx, top in pieces:
        lay(out, ink, left + gx - box[0], baseline + top - box[1], ink_colour)
    card[box[1]:box[3], box[0]:box[2], :3] = np.clip(out, 0, 255)
    return card


def zero_requirements(card, rng):
    """Big Pilgrim's Key: Dexterity and Intelligence 17 -> 0, with the card's own Strength 0."""
    col = PILGRIM_STATS
    region, _, alpha = split(card, col)
    labels, _ = ndimage.label(alpha > 0.45)
    found = []
    for lab, (sy, sx) in enumerate(ndimage.find_objects(labels), 1):
        if (sx.stop - sx.start) < 6 or (sx.stop - sx.start) * (sy.stop - sy.start) < 30: continue
        found.append((sx.start + col[0], sy.start + col[1], sx.stop - 1 + col[0], sy.stop - 1 + col[1], lab))
    rows = []
    for g in sorted(found, key=lambda g: g[3]):
        if rows and abs(rows[-1][0][3] - g[3]) < 10: rows[-1].append(g)
        else: rows.append([g])
    rows = [sorted(r) for r in rows]
    assert len(rows) == 4 and len(rows[0]) == 1 and len(rows[1]) == 2 and len(rows[2]) == 2, rows
    zero = rows[0][0]
    ink_colour = np.median(region[alpha > 0.9], axis=0)
    zink, zx, zy = soft_ink(alpha, labels, {zero[4]}, col)
    old = ndimage.binary_dilation(np.isin(labels, [g[4] for row in rows[1:3] for g in row]), iterations=2)
    out = fill_paper(region, alpha, old, rng, sigma=3)
    for row in rows[1:3]:
        lay(out, zink, zx - col[0], zy + (row[0][3] - zero[3]) - col[1], ink_colour)
    card[col[1]:col[3], col[0]:col[2], :3] = np.clip(out, 0, 255)


if __name__ == "__main__":
    rng = np.random.default_rng(7)
    library = blighttown_library()
    os.makedirs(SPRITES, exist_ok=True)
    for name in CARDS:
        card = rewrite(name, library, rng)
        if name == "Big Pilgrim's Key": zero_requirements(card, rng)
        path = os.path.join(OUT, f"{name}.png")
        Image.fromarray(np.clip(card, 0, 255).astype(np.uint8)).save(path)
        shutil.copyfile(path, os.path.join(SPRITES, f"{name}.png"))
        print("wrote", name)
