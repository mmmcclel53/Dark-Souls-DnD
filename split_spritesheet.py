"""
Split a 10-column x 7-row TTS deck sprite sheet into individual PNGs.
Usage: python split_spritesheet.py <path_to_spritesheet.png> [output_dir]
"""

import sys
import os
from PIL import Image

COLS = 10
ROWS = 7

CARD_NAMES = [
    # Row 1: Black Knight + moves
    "Black Knight", "Overhead Swing", "Heavy Slash", "Backswing", "Vicious Hack",
    "Defensive Strike", "Wide Swing", "Massive Swing", "Hacking Slash", "Charge",
    # Row 2: Black Knight equipment + Gravelord Nito + moves
    "Black Knight Halberd", "Black Knight Shield", "Blue Titanite", "Gravelord Nito", "Gravelord Greatsword",
    "Death Wave", "Miasma", "Sword Slam", "Sword Sweep", "Deathly Thrust",
    # Row 3: Nito moves + equipment + Giant Skeleton Archer
    "Death Grip", "Deathly Strike", "Toxicity", "Entropy", "Creeping Death",
    "Death's Embrace", "Lunging Cleave", "Gravelord Sword", "Gravelord Sword Dance", "Giant Skeleton Archer",
    # Row 4: More enemies
    "Giant Skeleton Soldier", "Necromancer", "Skeleton Archer", "Skeleton Beast", "Skeleton Soldier",
]

def slugify(name):
    return name.lower().replace(" ", "_").replace("(", "").replace(")", "").replace("-", "_").replace("'", "").strip("_")

def split(sheet_path, out_dir):
    img = Image.open(sheet_path)
    w, h = img.size
    cell_w = w / COLS
    cell_h = h / ROWS

    os.makedirs(out_dir, exist_ok=True)
    print(f"Sheet size: {w}x{h}  |  Cell size: {cell_w:.1f}x{cell_h:.1f}")

    idx = 0
    for row in range(ROWS):
        for col in range(COLS):
            if idx >= len(CARD_NAMES):
                break
            name = CARD_NAMES[idx]
            if not name:
                idx += 1
                continue

            left   = round(col * cell_w)
            top    = round(row * cell_h)
            right  = round((col + 1) * cell_w)
            bottom = round((row + 1) * cell_h)
            cell   = img.crop((left, top, right, bottom))

            filename = f"{idx+1:02d}_{slugify(name)}.png"
            cell.save(os.path.join(out_dir, filename))
            print(f"  [{idx+1:02d}] {filename}  ({left},{top} -> {right},{bottom})")
            idx += 1

    print(f"\nDone — {idx} cards saved to: {out_dir}")

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python split_spritesheet.py <sheet.png> [output_dir]")
        sys.exit(1)
    sheet = sys.argv[1]
    out   = sys.argv[2] if len(sys.argv) > 2 else os.path.join(os.path.dirname(sheet), "cards")
    split(sheet, out)
