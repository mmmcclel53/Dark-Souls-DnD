"""Contact sheets of every enemy's and boss's board token beside its activation-bar portrait
(the portraitRegion crop of its card), for checking the framing by eye.

    python Tools/avatar_sheets.py

Writes Docs/enemy_avatars.png and Docs/boss_avatars.png.
"""
import glob
import os
import re

from PIL import Image, ImageDraw, ImageFont

CELL_W, CELL_H, COLS = 250, 175, 6


def res(path):
    return path.replace("res://", "")


def region(text):
    m = re.search(r"portraitRegion = Rect2\(([^)]+)\)", text)
    return tuple(map(float, m.group(1).split(","))) if m else None


def entries():
    for path in sorted(glob.glob("Resources/Prefabs/Enemies/*/*.tres")):
        s = open(path, encoding="utf-8").read()
        yield ("enemy", re.search(r'enemyName = "([^"]+)"', s).group(1),
               res(re.search(r'path="([^"]+)" id="2_card"', s).group(1)),
               res(re.search(r'path="([^"]+)" id="3_avatar"', s).group(1)), region(s))
    for path in sorted(glob.glob("Resources/Prefabs/Bosses/*.tres")):
        s = open(path, encoding="utf-8").read()
        avatar = re.search(r'path="([^"]+)" id="2_avatar"', s)
        card = re.search(r'path="([^"]+)" id="5_data"', s)
        yield ("boss", re.search(r'bossName = "([^"]+)"', s).group(1),
               card and res(card.group(1)), avatar and res(avatar.group(1)), region(s))


def main():
    font = ImageFont.truetype("arial.ttf", 15)
    items = list(entries())
    for kind in ("enemy", "boss"):
        group = [i for i in items if i[0] == kind]
        sheet = Image.new("RGB", (COLS * CELL_W, ((len(group) + COLS - 1) // COLS) * CELL_H), (24, 22, 20))
        draw = ImageDraw.Draw(sheet)
        for k, (_, name, card, avatar, reg) in enumerate(group):
            x, y = (k % COLS) * CELL_W, (k // COLS) * CELL_H
            if avatar and os.path.exists(avatar):
                a = Image.open(avatar).convert("RGBA").resize((120, 120))
                sheet.paste(a, (x + 5, y + 5), a)
            if card and reg and os.path.exists(card):
                c = Image.open(card).convert("RGB")
                w, h = c.size
                box = (int(reg[0] * w), int(reg[1] * h), int((reg[0] + reg[2]) * w), int((reg[1] + reg[3]) * h))
                sheet.paste(c.crop(box).resize((90, 120)), (x + 135, y + 5))
            draw.text((x + 5, y + 135), name, fill=(235, 220, 180), font=font)
        sheet.save(f"Docs/{kind}_avatars.png")


if __name__ == "__main__":
    main()
