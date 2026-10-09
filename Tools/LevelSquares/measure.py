"""Finds the 4x4 grid of level-up squares on each class's character board.

The boards are scans of the printed character boards, at two sizes, so the tier table does
not sit at one fixed fraction of the art. This finds the sixteen dark squares in the table
(the board's bottom-right, above the red endurance bar) and prints, per class, the centre of
the top-left and bottom-right squares and a square's width, all as fractions of the board.
The Level Up screen (LevelUpPanel) draws the white Level Up cubes there.

With --write it also sets `tierSquares` and `tierSquareSize` in every Character resource under
Resources/Prefabs/Player, measured from the board its `image` names (the Tester uses the
Assassin's).

Usage: python measure.py [--write]
"""
import glob
import os
import re
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = os.path.join(os.path.dirname(__file__), '..', '..')
PLAYER_DIR = os.path.join(ROOT, 'Resources', 'Prefabs', 'Player')

# Where to look: the table sits in the board's bottom-right, above the endurance bar.
REGION = (0.60, 0.60, 1.00, 0.90)
# Brightness cutoffs to try, in order. A grimy scan joins a square to its surroundings at
# one cutoff and not another (Herald's at 95), so the first giving all sixteen wins.
DARK = (95, 80, 70, 110)


def measure(path):
    image = Image.open(path).convert('L')
    w, h = image.size
    x0, y0, x1, y1 = (int(REGION[0] * w), int(REGION[1] * h), int(REGION[2] * w), int(REGION[3] * h))
    grey = np.asarray(image.crop((x0, y0, x1, y1)), dtype=np.int32)
    for dark in DARK:
        squares = find_squares(grey < dark, w, x0, y0)
        if len(squares) == 16:
            break
    else:
        raise ValueError(f'{path}: no cutoff found all 16 squares')
    xs = sorted(s[0] for s in squares)
    ys = sorted(s[1] for s in squares)
    left, right = np.mean(xs[:4]), np.mean(xs[-4:])
    top, bottom = np.mean(ys[:4]), np.mean(ys[-4:])
    size = np.mean([s[2] for s in squares])
    return left / w, top / h, (right - left) / w, (bottom - top) / h, size / w


def find_squares(mask, w, x0, y0):
    labels, count = ndimage.label(mask)
    squares = []
    for index, box in enumerate(ndimage.find_objects(labels), start=1):
        bh, bw = box[0].stop - box[0].start, box[1].stop - box[1].start
        if not (0.025 * w < bw < 0.06 * w and 0.75 < bw / bh < 1.33):
            continue
        fill = (labels[box] == index).mean()
        if fill < 0.8:
            continue
        cy = (box[0].start + box[0].stop) / 2 + y0
        cx = (box[1].start + box[1].stop) / 2 + x0
        squares.append((cx, cy, bw))
    return squares


def write(tres, values):
    left, top, width, height, size = values
    text = open(tres, encoding='utf-8').read()
    text = re.sub(r'\ntierSquares = .*', '', text)
    text = re.sub(r'\ntierSquareSize = .*', '', text)
    lines = f'\ntierSquares = Rect2({left:.5f}, {top:.5f}, {width:.5f}, {height:.5f})\ntierSquareSize = {size:.5f}'
    # After the script line: Godot drops a property set before the script exists.
    text = re.sub(r'(\nscript = [^\n]*)', lambda m: m.group(1) + lines, text, count=1)
    open(tres, 'w', encoding='utf-8', newline='').write(text)


def board_of(tres):
    text = open(tres, encoding='utf-8').read()
    if 'script_class="Character"' not in text:
        return None
    image = re.search(r'\nimage = ExtResource\("([^"]+)"\)', text)
    if not image:
        return None
    path = re.search(r'path="res://([^"]+)" id="' + re.escape(image.group(1)) + '"', text)
    return os.path.join(ROOT, path.group(1)) if path else None


def main():
    for board in sorted(glob.glob(os.path.join(PLAYER_DIR, '*', '*.jpg'))):
        name = os.path.basename(os.path.dirname(board))
        values = measure(board)
        print(f'{name:11} centres from ({values[0]:.4f}, {values[1]:.4f}) across ({values[2]:.4f}, {values[3]:.4f}), square {values[4]:.4f}')
    if '--write' not in sys.argv:
        return
    for tres in sorted(glob.glob(os.path.join(PLAYER_DIR, '*', '*.tres'))):
        board = board_of(tres)
        if board:
            write(tres, measure(board))
            print(f'wrote {os.path.relpath(tres, ROOT)} from {os.path.basename(board)}')


if __name__ == '__main__':
    main()
