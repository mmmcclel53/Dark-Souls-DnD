"""Edits printed cards where this project changed what they say (Matt, Oct 2026):

  Titanite Scale  "+1 damage" -> "+2 damage", and Strength / Dexterity 0 -> 20
  Raw Gem         "cost +2 stamina" -> "cost +1 stamina", and Strength 0 -> 15
  Divine Blessing "permanently discard this card to" -> "once per rest, use this card to",
                  and Faith 22 -> 30

    python Tools/CardEdits/edit_cards.py

Nothing is drawn with a computer font. Each new digit is a printed one lifted from another
card that sets it in the same face, at the same size, on a scan of the same resolution:
the "2" of Raw Gem's "+2 damage", the "1" of Titanite Shard's "+1 damage", the "20" of Red
Tearstone Ring's Faith, the "15" of Great Swamp Ring's Dexterity, the "30" of Carthus
Milkring's Dexterity. A line of print is split into ink and paper (the paper is the ink
closed away and softened), the old digits are lifted off with the paper's grain put back,
and the new ones are laid on the old baseline in the target card's own ink. Each new
digit is placed with the spacing its source card gives it.

Writes Tools/CardEdits/<Name>.png, which Tools/onboard_gems.py uses in place of the
original card. The originals in the board game folder are untouched.
"""
import os
import numpy as np
from PIL import Image
from scipy import ndimage

BASE = "C:/Users/Matt McClelland/Documents/Game Dev/Dark Souls Board Game/Images/General Rewards/"
GEMS = BASE + "Weapons/Gems/"
RINGS = BASE + "Armour/Rings/"
OUT = os.path.dirname(os.path.abspath(__file__))

INK_FLOOR = 45      # the darkest the printed ink gets
# The paper estimate is the brightest paper nearby, so plain paper grain reads as up to
# this much "ink". Below it is paper; a lifted glyph is remapped so its grain stays behind.
GRAIN = 0.25
PAD = 2


def load(path):
    # A card set aside from the game still lends its printed digits.
    if not os.path.exists(path):
        path = os.path.join(os.path.dirname(path), "not implemented", os.path.basename(path))
    return np.asarray(Image.open(path).convert("RGBA")).astype(float)


def split(rgb, box):
    """Paper and ink alpha for a box of print (x0, y0, x1, y1, exclusive ends)."""
    x0, y0, x1, y1 = box
    region = rgb[y0:y1, x0:x1, :3].copy()
    paper = np.stack([ndimage.gaussian_filter(ndimage.grey_closing(region[..., c], size=(9, 9)), 1.5)
                      for c in range(3)], -1)
    lum, paper_lum = region.mean(-1), paper.mean(-1)
    alpha = np.clip((paper_lum - lum) / np.maximum(paper_lum - INK_FLOOR, 1), 0, 1)
    return region, paper, alpha


def lift(card, box, glyphs, rng):
    """Takes the glyphs (inclusive boxes) off, putting paper and its grain back."""
    region, paper, alpha = split(card, box)
    clean = (alpha < GRAIN) & ~ndimage.binary_dilation(alpha > 0.4, iterations=2)
    grain = (region - paper)[clean]
    out = region.copy()
    ink = ndimage.binary_dilation(alpha > GRAIN, iterations=1)
    for gx0, gy0, gx1, gy1 in glyphs:
        mask = np.zeros_like(alpha, dtype=bool)
        mask[gy0 - box[1] - 1:gy1 - box[1] + 2, gx0 - box[0] - 1:gx1 - box[0] + 2] = True
        mask &= ink
        out[mask] = paper[mask] + grain[rng.integers(0, len(grain), mask.sum())]
    card[box[1]:box[3], box[0]:box[2], :3] = np.clip(out, 0, 255)
    return np.median(region[alpha > 0.9], axis=0)


def lay(card, ink, source, source_box, glyph, x0, baseline):
    """Lays a glyph (inclusive box) from the source card at x0, sitting on the baseline."""
    _, _, alpha = split(source, source_box)
    gx0, gy0, gx1, gy1 = glyph
    sx, sy = source_box[0], source_box[1]
    src = alpha[gy0 - sy - PAD:gy1 - sy + PAD + 1, gx0 - sx - PAD:gx1 - sx + PAD + 1]
    a = np.clip((src - GRAIN) / (1 - GRAIN), 0, 1)[..., None]
    h, w = src.shape
    top = baseline - (gy1 - gy0) - PAD
    left = x0 - PAD
    window = card[top:top + h, left:left + w, :3]
    card[top:top + h, left:left + w, :3] = window * (1 - a) + ink * a


def save(card, name):
    path = os.path.join(OUT, name + ".png")
    Image.fromarray(np.clip(card, 0, 255).astype(np.uint8)).save(path)
    print("wrote", path)


def titanite_scale(rng):
    card = load(GEMS + "Titanite Scale.png")
    raw = load(GEMS + "Raw Gem.png")
    red = load(RINGS + "Red Tearstone Ring.png")

    # "+1 damage" -> "+2": Raw Gem's "2" where the "1" began, which leaves the 7 px before
    # "damage" that Raw Gem has.
    line = (60, 632, 445, 672)
    ink = lift(card, line, [(340, 644, 347, 660)], rng)
    lay(card, ink, raw, (60, 645, 445, 685), (98, 659, 108, 676), 340, 660)

    # Strength and Dexterity 0 -> 20. Red Tearstone's "20" starts 7 px left of where its
    # lone "0"s do, and the Scale's lone "0"s stand where Red Tearstone's do.
    stats = (84, 150, 119, 235)
    ink = lift(card, stats, [(91, 158, 107, 180), (91, 205, 107, 228)], rng)
    twenty = ((78, 295, 116, 332), (83, 302, 113, 325))
    lay(card, ink, red, *twenty, 84, 180)
    lay(card, ink, red, *twenty, 84, 228)
    save(card, "Titanite Scale")


def raw_gem(rng):
    card = load(GEMS + "Raw Gem.png")
    shard = load(GEMS + "Titanite Shard.png")

    # "cost +2 stamina" -> "+1": Titanite Shard's "1", 4 px after the "+" as on the Shard,
    # which also leaves the Shard's gap before the next word.
    line = (60, 652, 445, 685)
    ink = lift(card, line, [(301, 660, 311, 676)], rng)
    lay(card, ink, shard, (60, 632, 445, 672), (335, 643, 341, 659), 303, 676)

    # Strength 0 -> 15. Great Swamp's "15" starts where its lone "0"s do, as Raw Gem's does.
    swamp = load(RINGS + "Great Swamp Ring.png")
    stats = (84, 150, 114, 235)
    ink = lift(card, stats, [(88, 158, 103, 181)], rng)
    lay(card, ink, swamp, (78, 195, 116, 232), (89, 202, 109, 224), 88, 181)
    save(card, "Raw Gem")


def divine_blessing(rng):
    """ "permanently discard this card to" -> "once per rest, use this card to".

    Every letter comes off this card: line 2's own, plus line 1's "u" (of "you") and comma
    (after "activation"), one line pitch up. Each is lifted by its own ink, not a box, so
    no sliver of a neighbour comes with it. "this card to" stays as printed and the line is
    re-centred where the card centres it, with the card's letter and word spacing.
    """
    card = load(RINGS + "Divine Blessing.png")
    box = (60, 622, 440, 697)
    region, paper, alpha = split(card, box)
    labels, _ = ndimage.label(alpha > 0.45)
    # Every inked pixel belongs to the nearest letter, up to 2 px out (the soft edges).
    distance, (iy, ix) = ndimage.distance_transform_edt(labels == 0, return_indices=True)
    owner = np.where(distance <= 2, labels[iy, ix], 0)
    found = {}
    for label, (sy, sx) in enumerate(ndimage.find_objects(labels), start=1):
        if sy is not None:
            found[(sx.start + box[0], sy.start + box[1], sx.stop - 1 + box[0], sy.stop - 1 + box[1])] = label

    def glyph(*bboxes, shift=0):
        """A letter's ink with its soft edge, the x span of its solid ink (which is what the
        card's spacing is measured from), how far the soft edge reaches left of that, and
        where its top lands on line 2."""
        ids = [found[b] for b in bboxes]
        mask = np.isin(owner, ids)
        ys, xs = np.nonzero(mask)
        x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
        ink = np.where(mask, alpha, 0)[y0:y1 + 1, x0:x1 + 1]
        core_x0 = min(b[0] for b in bboxes)
        core_x1 = max(b[2] for b in bboxes)
        return ink, core_x0, core_x1, core_x0 - (x0 + box[0]), y0 + box[1] + shift

    o = glyph((392, 650, 403, 662))
    n = glyph((144, 651, 154, 662))
    c = glyph((242, 651, 250, 662))
    e = glyph((157, 651, 166, 662))
    pe = glyph((80, 650, 102, 669))
    r = glyph((264, 651, 270, 662))
    s = glyph((234, 651, 240, 662))
    t = glyph((182, 649, 187, 662))
    u = glyph((344, 625, 355, 639), shift=24)
    comma = glyph((310, 636, 314, 643), shift=24)
    this_card_to = [(292, 649, 297, 662), (299, 643, 303, 662), (304, 651, 310, 662), (313, 645, 315, 647),
                    (313, 651, 316, 662), (319, 651, 326, 662), (335, 651, 343, 662), (345, 651, 354, 662),
                    (356, 651, 363, 662), (364, 643, 375, 662), (385, 649, 390, 662), (392, 650, 403, 662)]
    line_two = [(80, 650, 102, 669), (104, 650, 130, 662), (133, 651, 142, 662), (144, 651, 154, 662),
                (157, 651, 166, 662), (169, 651, 178, 662), (182, 649, 187, 662), (190, 644, 193, 662),
                (196, 651, 206, 663), (196, 665, 199, 668), (213, 643, 224, 662), (227, 645, 229, 648),
                (227, 651, 230, 662), (234, 651, 240, 662), (242, 651, 250, 662), (253, 651, 262, 662),
                (264, 651, 270, 662), (271, 643, 282, 662)] + this_card_to
    kept = glyph(*this_card_to)

    # The card's spacing: 2 px between letters (1 after "pe" and "r", which sit close),
    # 8 between words, 9 before a "t", whose crossbar leaves a gap of its own.
    LETTER, WORD = 2, 8
    words = [[(o, LETTER), (n, LETTER), (c, LETTER), (e, 0)],
             [(pe, 1), (r, 0)],
             [(r, 1), (e, LETTER), (s, LETTER), (t, 1), (comma, 0)],
             [(u, LETTER), (s, LETTER), (e, 0)]]
    pieces, x = [], 0
    for word in words:
        for (ink, x0, x1, edge, top), gap in word:
            pieces.append((ink, x - edge, top))
            x += (x1 - x0 + 1) + gap
        x += WORD
    x += 1
    pieces.append((kept[0], x - kept[3], kept[4]))
    width = x + (kept[2] - kept[1] + 1)
    left = round((80 + 403 + 1) / 2 - width / 2)    # where the card centred the line

    # Lift the old line 2 off, putting the paper and its grain back.
    old = np.isin(owner, [found[b] for b in line_two])
    clean = (alpha < GRAIN) & ~ndimage.binary_dilation(alpha > 0.4, iterations=2)
    grain = (region - paper)[clean]
    ink = np.median(region[alpha > 0.9], axis=0)
    out = region.copy()
    out[old] = paper[old] + grain[rng.integers(0, len(grain), old.sum())]

    for glyph_ink, gx, top in pieces:
        a = np.clip((glyph_ink - GRAIN) / (1 - GRAIN), 0, 1)[..., None]
        h, w = glyph_ink.shape
        y, xx = top - box[1], left + gx - box[0]
        out[y:y + h, xx:xx + w] = out[y:y + h, xx:xx + w] * (1 - a) + ink * a
    card[box[1]:box[3], box[0]:box[2], :3] = np.clip(out, 0, 255)

    # Faith 22 -> 30. Carthus Milkring's "30" starts 8 px left of its lone "0"s, right
    # against the icon frame; Divine Blessing's column sits 7 px further left, so it starts
    # at the frame here too, with a hairline gap.
    milkring = load(RINGS + "Carthus Milkring.png")
    ink = lift(card, (79, 290, 122, 335), [(81, 301, 108, 324)], rng)
    lay(card, ink, milkring, (80, 195, 122, 232), (85, 203, 115, 226), 79, 324)
    save(card, "Divine Blessing")


if __name__ == "__main__":
    rng = np.random.default_rng(7)
    titanite_scale(rng)
    raw_gem(rng)
    divine_blessing(rng)
