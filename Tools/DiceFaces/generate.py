"""Cuts the printed die-face sheets into the face sheets the roll reveal maps onto its cube.

Input: a 302x302 photo of each die's faces, laid out as a 3x2 grid of ~100px cells with
the top third empty (the dodge die has one row of three icons; its blank faces come from
the empty top third). Output: one PNG per die, 3x2 cells of 100px, faces in reading order,
cropped as they are — the photos read better untouched than any sharpening did.

Usage: python generate.py <orange.jpg> <black.jpg> <blue.jpg> <green.jpg>
Writes to Resources/Images/Sprites/Dice/<Name>.png and a preview contact sheet beside
this script. The source photos are kept in source/ next to this script.
"""
import os
import sys
from PIL import Image

CELL = 100
OUT_DIR = os.path.join(os.path.dirname(__file__), '..', '..', 'Resources', 'Images', 'Sprites', 'Dice')

# Where each face sits on the sheet, as (column, row) with row 0 the empty top third.
LAYOUTS = {
    'Orange': [(0, 1), (1, 1), (2, 1), (0, 2), (1, 2), (2, 2)],
    'Black':  [(0, 1), (1, 1), (2, 1), (0, 2), (1, 2), (2, 2)],
    'Blue':   [(0, 1), (1, 1), (2, 1), (0, 2), (1, 2), (2, 2)],
    'Green':  [(0, 1), (1, 1), (2, 1), (0, 0), (1, 0), (2, 0)],
}


def cell(sheet, col, row):
    w, h = sheet.size
    cw, ch = w / 3.0, h / 3.0
    box = (int(round(col * cw)), int(round(row * ch)), int(round((col + 1) * cw)), int(round((row + 1) * ch)))
    return sheet.crop(box).resize((CELL, CELL), Image.LANCZOS)


def build(name, path):
    sheet = Image.open(path).convert('RGB')
    out = Image.new('RGB', (CELL * 3, CELL * 2))
    for i, (col, row) in enumerate(LAYOUTS[name]):
        out.paste(cell(sheet, col, row), ((i % 3) * CELL, (i // 3) * CELL))
    return out


def main(paths):
    os.makedirs(OUT_DIR, exist_ok=True)
    names = ['Orange', 'Black', 'Blue', 'Green']
    sheets = []
    for name, path in zip(names, paths):
        image = build(name, path)
        image.save(os.path.join(OUT_DIR, f'{name}.png'))
        sheets.append(image)
    preview = Image.new('RGB', (CELL * 3, CELL * 2 * len(sheets)))
    for i, image in enumerate(sheets):
        preview.paste(image, (0, i * CELL * 2))
    preview.save(os.path.join(os.path.dirname(__file__), 'preview.png'))
    print('wrote', OUT_DIR)


if __name__ == '__main__':
    main(sys.argv[1:5])
