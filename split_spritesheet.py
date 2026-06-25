"""
Split a 10-column x 7-row equipment card sprite sheet into 70 individual PNGs.
Usage: python split_spritesheet.py <path_to_spritesheet.png> [output_dir]
"""

import sys
import os
from PIL import Image

COLS = 10
ROWS = 7

# Card names in row-major order (top-left to bottom-right, matching the sheet)
CARD_NAMES = [
    "Black Hand Armour", "Blood Gem", "Bloodshield", "Brigand Axe", "Claymore",
    "Court Sorcerer Robes", "Crystal Gem", "Demon Titanite", "Drang Armour", "East-West Shield",
    "Effigy Shield", "Ember (1)", "Ember (2)", "Ember (3)", "Exile Armour",
    "Exile Greatsword", "Greataxe", "Great Magic Weapon", "Heal", "Heal Aid",
    "Heavy Gem", "Kukri", "Lightning Gem", "Lothric Knight Greatsword", "Painting Guardian Armour",
    "Paladin Armour", "Pike", "Rapier", "Red Tearstone Ring", "Reinforced Club",
    "Shortsword", "Silver Eagle Kite Shield", "Silver Knight Straight Sword", "Sorcerer's Staff", "Soul Arrow",
    "Soul Spear", "Soulstream", "Sunlight Shield", "Throwing Knives", "Titanite Shard (1)",
    "Titanite Shard (2)", "Titanite Shard (3)", "Vilka's Rapier", "Worker Armour", "Xanthous Robes",
    "Avelyn", "Drake Sword", "Fume Ultra Greatsword", "Gotthard Twinswords", "Moonlight Greatsword",
    # Rows 6-7 appear to be empty/partial in the sheet — placeholders below
    "", "", "", "", "", "", "", "", "", "",
    "", "", "", "", "", "", "", "", "", "",
]

def slugify(name):
    return name.lower().replace(" ", "_").replace("(", "").replace(")", "").replace("-", "_").replace("'", "").strip("_")

def split(sheet_path, out_dir):
    img = Image.open(sheet_path)
    w, h = img.size
    cell_w = w // COLS
    cell_h = h // ROWS

    os.makedirs(out_dir, exist_ok=True)
    print(f"Sheet size: {w}x{h}  |  Cell size: {cell_w}x{cell_h}")

    idx = 0
    for row in range(ROWS):
        for col in range(COLS):
            left   = col * cell_w
            top    = row * cell_h
            right  = left + cell_w
            bottom = top  + cell_h
            cell   = img.crop((left, top, right, bottom))

            name = CARD_NAMES[idx] if idx < len(CARD_NAMES) and CARD_NAMES[idx] else f"card_{row}_{col}"
            filename = f"{idx+1:02d}_{slugify(name)}.png"
            cell.save(os.path.join(out_dir, filename))
            print(f"  [{idx+1:02d}] {filename}")
            idx += 1

    print(f"\nDone — {idx} cards saved to: {out_dir}")

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python split_spritesheet.py <sheet.png> [output_dir]")
        sys.exit(1)
    sheet = sys.argv[1]
    out   = sys.argv[2] if len(sys.argv) > 2 else os.path.join(os.path.dirname(sheet), "cards")
    split(sheet, out)
