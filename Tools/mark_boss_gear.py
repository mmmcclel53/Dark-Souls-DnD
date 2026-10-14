#!/usr/bin/env python3
# Marks boss gear in the equipment .tres files: every row of the two item sheets whose
# "Belongs To" is Boss gets `bossTreasure = true` after the [resource] script line (properties
# above `script = ` are dropped on load). Boss gear never reaches the Merchant or map loot; it is
# reserved for boss payouts. Safe to re-run: files already marked are left alone.
# Where a boss item shares its name with an ordinary one, the boss copy is the "<Name> (Boss)"
# folder (see onboard_equipment.py) and only that is marked.
import csv, os, sys

DL   = "C:/Users/Matt McClelland/Downloads/"
PROJ = "C:/Users/Matt McClelland/Documents/Game Dev/Godot/Projects/Dark Souls DnD"
WEAPONS = PROJ + "/Resources/Prefabs/Equipment/Weapons"
ARMOR   = PROJ + "/Resources/Prefabs/Equipment/Armor"
LINE = "bossTreasure = true"

def boss_targets(csv_name, is_weapon_sheet):
    with open(DL + csv_name, newline="", encoding="utf-8") as f:
        rows = list(csv.DictReader(f))
    names = [r["Name"].strip() for r in rows]
    dup = {n for n in names if names.count(n) > 1}
    for row in rows:
        if (row.get("Belongs To") or "").strip() != "Boss": continue
        name = row["Name"].strip()
        typ = (row.get("Type") or "").strip()
        base = WEAPONS if (is_weapon_sheet or typ == "Shield") else ARMOR
        folder = name + " (Boss)" if name in dup else name
        yield name, base, folder

def mark(path):
    with open(path, encoding="utf-8", newline="") as f:
        text = f.read()
    nl = "\r\n" if "\r\n" in text else "\n"
    lines = text.split(nl)
    try:
        start = lines.index("[resource]")
    except ValueError:
        return "no [resource]"
    if any(l.strip() == LINE for l in lines[start:]): return "already"
    for i in range(start + 1, len(lines)):
        if lines[i].startswith("script = "):
            lines.insert(i + 1, LINE)
            with open(path, "w", encoding="utf-8", newline="") as f:
                f.write(nl.join(lines))
            return "marked"
    return "no script line"

def main():
    results = {"marked": 0, "already": 0}
    problems = []
    seen = set()
    targets = list(boss_targets("Item Effectiveness (NEW) - Weapons.csv", True)) + \
              list(boss_targets("Item Effectiveness (NEW) - Armor.csv", False))
    for name, base, folder in targets:
        path = os.path.join(base, folder, folder + ".tres")
        if not os.path.exists(path):
            problems.append(f"{name}: no file at {path}"); continue
        if path in seen: continue
        seen.add(path)
        r = mark(path)
        if r in results: results[r] += 1
        else: problems.append(f"{name}: {r}")
        both = os.path.exists(os.path.join(base, name, name + ".tres")) and os.path.exists(os.path.join(base, name + " (Boss)", name + " (Boss).tres"))
        if both: print(f"note: {name} has a plain and a (Boss) folder; only (Boss) marked")
    print(f"names in the sheets: {len(targets)}, files: {len(seen)}")
    print(f"newly marked: {results['marked']}, already marked: {results['already']}")
    for p in problems: print("  !", p)

if __name__ == "__main__":
    main()
