"""Cuts the weapon-option icons out of the rulebook as white-on-transparent PNGs, so the
game can tint them like the other combat icons (Resources/Images/Sprites/Combat/).

    python Tools/RuleIcons/extract.py <path to the rulebook pdf>

Shift, Node, Shaft and Repeat are vector art on p23; Push is the inline icon on p21. Each
region is rendered at 8x, its ink (darker than the parchment) becomes alpha, and Shift's and
Repeat's example numbers are blanked out: the game draws the real value over the icon.
"""
import sys
import fitz
import numpy as np
from PIL import Image
from scipy import ndimage

OUT = "Resources/Images/Sprites/Combat"
ZOOM = 8
SIZE = 128

# page, clip rect in points, the hole (centre x, centre y, radius, as fractions of the clip)
# where an example number sits and should be cleared, and whether to drop stray specks.
# Shift's arrowheads are separate islands of ink, so it keeps everything.
ICONS = {
    "Shift":  (23, (40, 178, 63, 201), (0.50, 0.50, 0.20), False),
    "Node":   (23, (40, 287, 63, 309), None, True),
    "Shaft":  (23, (39, 342, 64, 367), None, True),
    "Repeat": (23, (40, 393, 60, 414), (0.52, 0.52, 0.24), True),
    "Push":   (21, (100, 199, 117, 215), None, True),
}


def extract(doc, page, clip, hole, despeckle):
    pix = doc[page - 1].get_pixmap(matrix=fitz.Matrix(ZOOM, ZOOM), clip=fitz.Rect(*clip))
    rgb = np.frombuffer(pix.samples, dtype=np.uint8).reshape(pix.height, pix.width, pix.n)[:, :, :3].astype(float)
    lum = rgb.mean(axis=2)
    paper = np.percentile(lum, 90)            # the parchment: the brightest common tone
    alpha = np.clip((paper - lum) / max(paper - 20, 1), 0, 1)
    alpha[alpha < 0.12] = 0                   # paper grain

    # Fragments of neighbouring print caught by the clip: any island of ink far smaller than
    # the icon's largest piece goes.
    islands, count = ndimage.label(alpha > 0)
    if despeckle and count > 1:
        sizes = ndimage.sum(np.ones_like(alpha), islands, range(1, count + 1))
        for label, size in enumerate(sizes, start=1):
            if size < sizes.max() * 0.04:
                alpha[islands == label] = 0

    if hole:
        h, w = alpha.shape
        cx, cy, r = hole[0] * w, hole[1] * h, hole[2] * min(w, h)
        yy, xx = np.mgrid[0:h, 0:w]
        alpha[(xx - cx) ** 2 + (yy - cy) ** 2 < r * r] = 0

    out = np.zeros((alpha.shape[0], alpha.shape[1], 4), dtype=np.uint8)
    out[:, :, :3] = 255
    out[:, :, 3] = (alpha * 255).astype(np.uint8)
    img = Image.fromarray(out)
    img = img.crop(img.getbbox())             # trim to the ink
    side = max(img.size)
    square = Image.new("RGBA", (side, side), (255, 255, 255, 0))
    square.paste(img, ((side - img.width) // 2, (side - img.height) // 2))
    return square.resize((SIZE, SIZE), Image.LANCZOS)


def main():
    doc = fitz.open(sys.argv[1])
    for name, (page, clip, hole, despeckle) in ICONS.items():
        extract(doc, page, clip, hole, despeckle).save(f"{OUT}/{name}.png")
        print("wrote", name)


if __name__ == "__main__":
    main()
