#!/usr/bin/env python3
# Onboards weapon/armor CSV rows into Godot .tres resources (one folder + image + .tres each).
import csv, os, re, sys, shutil, random, string

DL   = "C:/Users/Matt McClelland/Downloads/"
PROJ = "C:/Users/Matt McClelland/Documents/Game Dev/Godot/Projects/Dark Souls DnD"
BULK_W = "C:/Users/Matt McClelland/Documents/Game Dev/Dark Souls Board Game/Bulk Weapons"
BULK_A = "C:/Users/Matt McClelland/Documents/Game Dev/Dark Souls Board Game/Bulk Armor"
OUT_W  = PROJ + "/Resources/Prefabs/Equipment/Weapons"
OUT_A  = PROJ + "/Resources/Prefabs/Equipment/Armor"

SCRIPT_WEAPON = "res://Scripts/Equipment/Weapon.cs"
SCRIPT_ARMOUR = "res://Scripts/Equipment/Armour.cs"
SCRIPT_MOVE   = "res://Scripts/Encounter/Player/PlayerMove.cs"
SCRIPT_EFFECT = "res://Scripts/Equipment/EquipmentEffect.cs"
DICE = {0:"res://Resources/Prefabs/Dice/Black Dice.tres",
        1:"res://Resources/Prefabs/Dice/Blue Dice.tres",
        2:"res://Resources/Prefabs/Dice/Orange Dice.tres"}

# enum ints
ET = dict(NONE=0,BONUS_DAMAGE=1,IGNORE_PHYSICAL_DEFENSE=2,GRANT_MAGIC=3,ATTACK_DICE=4,APPLY_STATUS=5,
    PUSH=6,HEAL=7,LOSE_HEALTH=8,GAIN_STAMINA=9,LOSE_STAMINA=10,REMOVE_STATUS=11,DEFENSE_DICE=12,
    REDIRECT_DAMAGE=13,BONUS_MOVEMENT=14,DODGE_DICE=15,DODGE_RANGE=16,DODGE_STAMINA_MOD=17,
    RUN_COST_MOD=18,CANNOT_DODGE=19,CANNOT_MOVE=20,ATTACK_STAMINA_COST_MOD=21,UPGRADE_REQ_MOD=22,
    BYPASS_TWO_HAND_CHECK=23,MAY_MOVE_AGGRO=24,MAY_TAKE_AGGRO=25)
COND = dict(NONE=0,IF_EMBERED=1,IF_MULTIPLE_ENEMIES_ON_NODE=2,IF_BLOCKING=3,IF_ATTACKER_HOLLOW=4,
    IF_ATTACKER_ALONNE=5,IF_TRAP_ACTIVATED=6,IF_HEROIC_ABILITY_ACTIVATED=7,IF_GAIN_HEALTH_ACTIVATED=8,
    IF_AOE_ATTACK=9,IF_MAGIC_ATTACK=10,IF_ARMOUR_UPGRADE_EQUIPPED=11,IF_ATTACKING_WEAK_ARC=12,
    IF_ALLY_DAMAGED_SAME_NODE=13,ON_ATTACK=14,ON_START_ACTIVATION=15,ON_END_ACTIVATION=16,ON_ATTACKED=17)
SCOPE = dict(TARGET=0,SELF=1,ONE_ENEMY=2,ONE_NODE=3,ALL_ENEMIES_IN_RANGE=4,ONE_CHARACTER=5,
    TWO_CHARACTERS=6,ALL_CHARACTERS=7,ONE_CHARACTER_IN_RANGE=8)
DUR = dict(INSTANT=0,UNTIL_END_OF_ACTIVATION=1,UNTIL_NEXT_CHARACTER_ACTIVATION=2,
    UNTIL_END_OF_ENEMY_ACTIVATION=3,PERMANENT=4)
DK = dict(PHYSICAL=0,MAGIC=1,BOTH=2)
STATUS = dict(BLEED=0,POISON=1,FROST=2,STAGGER=3,PUSH=4,NONE=5)
COLOUR = dict(Black=0,Blue=1,Orange=2)
REPEAT_T = dict(FREE=0,ONE_ENEMY=1,ONE_NODE=2)
STATUS_TOKENS = {"Bleed":"BLEED","Poison":"POISON","Frost":"FROST","Stagger":"STAGGER"}

warnings = []
def warn(item, msg): warnings.append(f"{item}: {msg}")

def eff(t, **kw):
    d = dict(type=ET[t], magnitude=0, inflictedStatus=5, diceColour=0, defenseKind=0,
             condition=0, scope=0, scopeRange=0, duration=0, _dice=(t in ("ATTACK_DICE","DEFENSE_DICE")))
    for k,v in kw.items(): d[k]=v
    return d

# ---- condition column interpretation ----
COND_MAP = {
    "if embered":("cond","IF_EMBERED"), "more than 1 enemy on node":("cond","IF_MULTIPLE_ENEMIES_ON_NODE"),
    "if blocking":("cond","IF_BLOCKING"), "if hollow type":("cond","IF_ATTACKER_HOLLOW"),
    "if hollow type attacked":("cond","IF_ATTACKER_HOLLOW"), "if alonne type attacked":("cond","IF_ATTACKER_ALONNE"),
    "if trap activated":("cond","IF_TRAP_ACTIVATED"), "if heroic ability activated":("cond","IF_HEROIC_ABILITY_ACTIVATED"),
    "if gain health activated":("cond","IF_GAIN_HEALTH_ACTIVATED"), "if aoe attack":("cond","IF_AOE_ATTACK"),
    "if magic attack":("cond","IF_MAGIC_ATTACK"), "if armor upgrade equipped":("cond","IF_ARMOUR_UPGRADE_EQUIPPED"),
    "if attacking weak arc":("cond","IF_ATTACKING_WEAK_ARC"), "if character damaged in same node":("cond","IF_ALLY_DAMAGED_SAME_NODE"),
    "after attack":("cond","ON_ATTACK"), "if attack":("cond","ON_ATTACK"),
    "if start your activation":("cond","ON_START_ACTIVATION"), "if start activation":("cond","ON_START_ACTIVATION"),
    "if end your activation":("cond","ON_END_ACTIVATION"), "if end character activation":("cond","ON_END_ACTIVATION"),
    "one character":("scope","ONE_CHARACTER"), "two characters":("scope","TWO_CHARACTERS"),
    "all characters":("scope","ALL_CHARACTERS"), "one enemy":("repeat","ONE_ENEMY"),
    "must target 1 node":("repeat","ONE_NODE"), "all enemies in range":("aoe",None), "all enemies":("aoe",None),
    "until end of activation":("dur","UNTIL_END_OF_ACTIVATION"),
    "until next character activation":("dur","UNTIL_NEXT_CHARACTER_ACTIVATION"),
    "until end of enemy activation":("dur","UNTIL_END_OF_ENEMY_ACTIVATION"),
}

def parse_effect_text(item, text, cond, is_passive=False):
    """Returns (flags, effects, immunities). flags = dict of PlayerMove-ish fields."""
    flags = {}
    effects = []
    immun = []
    text = (text or "").strip()
    cond = (cond or "").strip()
    tokens = [t.strip() for t in text.split(",") if t.strip()]

    for tok in tokens:
        low = tok.lower()
        m = None
        if low == "magic": flags["isMagic"]=True
        elif low == "aoe": flags["isAOE"]=True
        elif low == "no same node": flags["isNotZeroRange"]=True
        elif low == "ignore physical defense": flags["isIgnoreDefense"]=True
        elif low == "push": flags["isPush"]=True
        elif low == "push x2": flags["isPush"]=True; effects.append(eff("PUSH",magnitude=2))
        elif tok in STATUS_TOKENS: flags["statusEffect"]=STATUS[STATUS_TOKENS[tok]]
        elif (m:=re.match(r"Move \+(\d+)$",tok)): flags["bonusMovement"]=int(m.group(1))
        elif (m:=re.match(r"Range\s*\+(\d+)$",tok)): flags["attackRange"]=int(m.group(1))
        elif (m:=re.match(r"x(\d+) Turns$",tok)): flags["repeat"]=int(m.group(1))
        elif (m:=re.match(r"Damage \+(\d+)$",tok)): effects.append(eff("BONUS_DAMAGE",magnitude=int(m.group(1))))
        elif (m:=re.match(r"Health \+(\d+) for each enemy on node$",tok)):
            effects.append(eff("HEAL",magnitude=int(m.group(1)))); warn(item,f"'{tok}' per-enemy scaling not modeled (stored flat)")
        elif (m:=re.match(r"Health \+(\d+)$",tok)): effects.append(eff("HEAL",magnitude=int(m.group(1))))
        elif (m:=re.match(r"Health \-(\d+)$",tok)): effects.append(eff("LOSE_HEALTH",magnitude=int(m.group(1))))
        elif (m:=re.match(r"Stamina \+(\d+)$",tok)): effects.append(eff("GAIN_STAMINA",magnitude=int(m.group(1))))
        elif (m:=re.match(r"Stamina \-(\d+)$",tok)): effects.append(eff("LOSE_STAMINA",magnitude=int(m.group(1))))
        elif low == "all attacks gain magic": effects.append(eff("GRANT_MAGIC"))
        elif (m:=re.match(r"All Attacks Gain Magic & Damage \+(\d+)$",tok)):
            effects.append(eff("GRANT_MAGIC")); effects.append(eff("BONUS_DAMAGE",magnitude=int(m.group(1))))
        elif (m:=re.match(r"Remove (\d+) Status condition$",tok)): effects.append(eff("REMOVE_STATUS",magnitude=int(m.group(1))))
        elif low == "may move aggro": effects.append(eff("MAY_MOVE_AGGRO"))
        elif low == "may take aggro": effects.append(eff("MAY_TAKE_AGGRO"))
        elif low == "suffer bleed": effects.append(eff("APPLY_STATUS",inflictedStatus=STATUS["BLEED"],scope=SCOPE["SELF"]))
        elif low == "you may suffer that damage": effects.append(eff("REDIRECT_DAMAGE",scope=SCOPE["SELF"]))
        elif low == "cannot dodge": effects.append(eff("CANNOT_DODGE",duration=DUR["PERMANENT"]))
        elif low == "cannot walk or dodge": effects.append(eff("CANNOT_MOVE",duration=DUR["PERMANENT"]))
        elif low == "bypass two hand check": effects.append(eff("BYPASS_TWO_HAND_CHECK",duration=DUR["PERMANENT"]))
        elif low == "stamina free dodge": effects.append(eff("DODGE_STAMINA_MOD",magnitude=0,duration=DUR["PERMANENT"]))
        elif (m:=re.match(r"Dodge \+(\d+) Dice$",tok)): effects.append(eff("DODGE_DICE",magnitude=int(m.group(1)),duration=DUR["PERMANENT"]))
        elif (m:=re.match(r"Dodge \+(\d+) Node$",tok)): effects.append(eff("DODGE_RANGE",magnitude=int(m.group(1)),duration=DUR["PERMANENT"]))
        elif (m:=re.match(r"Dodge \+(\d+) Stamina$",tok)): effects.append(eff("DODGE_STAMINA_MOD",magnitude=int(m.group(1)),duration=DUR["PERMANENT"]))
        elif (m:=re.match(r"Dodge \+(\d+)$",tok)): effects.append(eff("DODGE_DICE",magnitude=int(m.group(1)),duration=DUR["PERMANENT"]))
        elif (m:=re.match(r"Walk \+(\d+) Node$",tok)): effects.append(eff("BONUS_MOVEMENT",magnitude=int(m.group(1)),duration=DUR["PERMANENT"]))
        elif (m:=re.match(r"Run \+(\d+) Stamina$",tok)): effects.append(eff("RUN_COST_MOD",magnitude=int(m.group(1)),duration=DUR["PERMANENT"]))
        elif (m:=re.match(r"Stamina Attack Cost \-(\d+)$",tok)): effects.append(eff("ATTACK_STAMINA_COST_MOD",magnitude=int(m.group(1)),duration=DUR["PERMANENT"]))
        elif (m:=re.match(r"Armor Upgrade reqs \-(\d+)$",tok)): effects.append(eff("UPGRADE_REQ_MOD",magnitude=int(m.group(1)),duration=DUR["PERMANENT"]))
        elif low == "push hollow": effects.append(eff("PUSH",condition=COND["IF_ATTACKER_HOLLOW"]))
        elif (m:=re.match(r"(Phys(?:ical)?(?: & Magic)?|Magic) Defense \+(\d+) (Black|Blue|Orange)( Dice)?$",tok)):
            kind = "BOTH" if "&" in m.group(1) else ("MAGIC" if m.group(1)=="Magic" else "PHYSICAL")
            effects.append(eff("DEFENSE_DICE",magnitude=int(m.group(2)),diceColour=COLOUR[m.group(3)],defenseKind=DK[kind]))
        elif (m:=re.match(r"All Characters (?:Gain|gain) (Phys(?:ical)?(?: & Magic)?|Magic) Defense \+(\d+) (Black|Blue|Orange)( Dice)?$",tok)):
            kind = "BOTH" if "&" in m.group(1) else ("MAGIC" if m.group(1)=="Magic" else "PHYSICAL")
            effects.append(eff("DEFENSE_DICE",magnitude=int(m.group(2)),diceColour=COLOUR[m.group(3)],defenseKind=DK[kind],scope=SCOPE["ALL_CHARACTERS"]))
        elif (m:=re.match(r"Attack \+(\d+) (Black|Blue|Orange)",tok)):
            effects.append(eff("ATTACK_DICE",magnitude=int(m.group(1)),diceColour=COLOUR[m.group(2)]))
            if "instead of" in low: warn(item,f"'{tok}': 'instead of Black' nuance dropped")
        elif low == "all enemies": flags["isAOE"]=True
        elif low.startswith("immune to"):
            rest = tok[len("Immune to"):].strip()
            for part in re.split(r"&|,", rest):
                p=part.strip().upper()
                if p in STATUS: immun.append(STATUS[p])
                else: warn(item,f"immunity '{part.strip()}' not a status - skipped")
        elif low == "look at 1 trap": warn(item,f"'{tok}' has no effect type - skipped (inert)")
        else:
            warn(item, f"UNMAPPED effect token: '{tok}' (cond='{cond}')")

    # apply condition column to produced effects / move
    apply_condition(item, cond, flags, effects)
    return flags, effects, immun

def apply_condition(item, cond, flags, effects):
    if not cond: return
    low = cond.lower()
    # rider effect hidden in condition col
    m = re.match(r"Stamina \+(\d+) to One Character within (\d+) Range$", cond)
    if m:
        effects.append(eff("GAIN_STAMINA",magnitude=int(m.group(1)),scope=SCOPE["ONE_CHARACTER_IN_RANGE"],scopeRange=int(m.group(2))))
        return
    if low == "in range +1":
        flags["isAOE"]=True
        for e in effects: e["scopeRange"]=1
        return
    if low in COND_MAP:
        kind,val = COND_MAP[low]
        if kind=="cond":
            for e in effects: e["condition"]=COND[val]
            if not effects: flags["_pending_cond"]=COND[val]
        elif kind=="scope":
            for e in effects:
                if e["type"] in (ET["HEAL"],ET["GAIN_STAMINA"],ET["LOSE_STAMINA"],ET["LOSE_HEALTH"],ET["REMOVE_STATUS"]):
                    e["scope"]=SCOPE[val]
        elif kind=="dur":
            for e in effects: e["duration"]=DUR[val]
        elif kind=="repeat":
            flags["repeatConstraint"]=REPEAT_T[val]
        elif kind=="aoe":
            flags["isAOE"]=True
    else:
        warn(item, f"UNMAPPED condition: '{cond}'")

def dice_list(row, prefix):
    out=[]
    for c,key in ((0,"Black"),(1,"Blue"),(2,"Orange")):
        n=intval(row.get(f"{prefix} {key}"))
        out += [c]*n
    return out

def intval(s):
    s=(s or "").strip()
    if s in ("",): return 0
    try: return int(float(s))
    except: return 0

# ---------- .tres writer ----------
def rid(prefix, counter): return f"{prefix}{counter}"

def gen_uid(used):
    while True:
        u="uid://"+"".join(random.choice(string.ascii_lowercase+string.digits) for _ in range(12))
        if u not in used: used.add(u); return u

USED_UID=set()

def render(item):
    """item: dict describing a Weapon or Armour resource. Returns .tres text."""
    ext=[]      # (type, path, id, is_script)
    extid={}
    def add_ext(kind, path):
        if path in extid: return extid[path]
        i=f"{len(ext)+1}_x"
        extid[path]=i; ext.append((kind,path,i)); return i

    is_weapon = item["cls"]=="Weapon"
    scr = add_ext("Script", SCRIPT_WEAPON if is_weapon else SCRIPT_ARMOUR)
    img = add_ext("Texture2D", item["imgpath"])

    subs=[]  # (id, lines)
    move_script=None; eff_script=None

    def add_effect_sub(e):
        nonlocal eff_script
        if eff_script is None: eff_script=add_ext("Script", SCRIPT_EFFECT)
        sid=f"EE{len(subs)}"
        L=[f'[sub_resource type="Resource" id="{sid}"]', f'script = ExtResource("{eff_script}")',
           f'type = {e["type"]}']
        if e["magnitude"]: L.append(f'magnitude = {e["magnitude"]}')
        if e["inflictedStatus"]!=5: L.append(f'inflictedStatus = {e["inflictedStatus"]}')
        if e["_dice"]: L.append(f'diceColour = {e["diceColour"]}')
        if e["type"]==ET["DEFENSE_DICE"]: L.append(f'defenseKind = {e["defenseKind"]}')
        if e["condition"]: L.append(f'condition = {e["condition"]}')
        if e["scope"]: L.append(f'scope = {e["scope"]}')
        if e["scopeRange"]: L.append(f'scopeRange = {e["scopeRange"]}')
        if e["duration"]: L.append(f'duration = {e["duration"]}')
        subs.append((sid,L)); return sid

    def dice_array(colours):
        refs=[f'ExtResource("{add_ext("Resource",DICE[c])}")' for c in colours]
        return "Array[Object]([" + ", ".join(refs) + "])"

    # attacks
    attack_ids=[]
    for mv in item.get("attacks",[]):
        if move_script is None: move_script=add_ext("Script", SCRIPT_MOVE)
        sid=f"PM{len(subs)}"
        L=[f'[sub_resource type="Resource" id="{sid}"]', f'script = ExtResource("{move_script}")']
        if mv["staminaCost"]: L.append(f'staminaCost = {mv["staminaCost"]}')
        if mv["damage"]: L.append(f'damage = {dice_array(mv["damage"])}')
        if mv.get("modifier"): L.append(f'modifier = {mv["modifier"]}')
        L.append(f'attackRange = {mv["attackRange"]}')
        for f in ("isNotZeroRange","isAOE","isMagic","isPush","isIgnoreDefense"):
            if mv.get(f): L.append(f'{f} = true')
        if mv.get("statusEffect",5)!=5: L.append(f'statusEffect = {mv["statusEffect"]}')
        if mv.get("bonusMovement"): L.append(f'bonusMovement = {mv["bonusMovement"]}')
        L.append(f'repeat = {mv.get("repeat",1)}')
        if mv.get("repeatConstraint"): L.append(f'repeatConstraint = {mv["repeatConstraint"]}')
        be=[add_effect_sub(e) for e in mv.get("bonusEffects",[])]
        if be: L.append('bonusEffects = Array[Object]([' + ", ".join(f'SubResource("{s}")' for s in be) + '])')
        subs.append((sid,L)); attack_ids.append(sid)

    passive_ids=[add_effect_sub(e) for e in item.get("passives",[])]

    # body FIRST — defense dice_array() may register new ext_resources (colours not used by
    # any attack). Header must be rendered only after every add_ext() call has happened.
    body=['[resource]', f'script = ExtResource("{scr}")', f'name = "{item["name"]}"',
          f'image = ExtResource("{img}")', f'type = {item["type"]}', f'rarity = 1',
          f'isUpgrade = {"true" if item["isUpgrade"] else "false"}']
    if is_weapon:
        body.append(f'numHands = {item.get("numHands",1)}')
        body.append(f'attackRange = {item.get("attackRange",0)}')
        body.append('attacks = ' + ("Array[Object]([" + ", ".join(f'SubResource("{s}")' for s in attack_ids) + "])" if attack_ids else "null"))
    body.append('physicalDefense = ' + (dice_array(item["physicalDefense"]) if item.get("physicalDefense") else "null"))
    if not is_weapon: body.append(f'physicalDefenseModifier = {item.get("physModifier",0)}')
    body.append('magicDefense = ' + (dice_array(item["magicDefense"]) if item.get("magicDefense") else "null"))
    if not is_weapon: body.append(f'magicDefenseModifier = {item.get("magModifier",0)}')
    body.append(f'dodgeAbility = {item.get("dodgeAbility",0)}')
    body.append(f'upgradeSlots = {item.get("upgradeSlots",0)}')
    if passive_ids: body.append('passives = Array[Object]([' + ", ".join(f'SubResource("{s}")' for s in passive_ids) + '])')
    if item.get("immunities"): body.append('immunities = Array[int]([' + ", ".join(str(x) for x in item["immunities"]) + '])')
    body += [f'strengthReq = {item["str"]}', f'dexterityReq = {item["dex"]}',
             f'intelligenceReq = {item["int"]}', f'faithReq = {item["fth"]}']

    # header (ext list now complete)
    load_steps = len(ext)+len(subs)+1
    cls = "Weapon" if is_weapon else "Armour"
    head=[f'[gd_resource type="Resource" script_class="{cls}" load_steps={load_steps} format=3 uid="{gen_uid(USED_UID)}"]','']
    for kind,path,i in ext:
        head.append(f'[ext_resource type="{kind}" path="{path}" id="{i}"]')
    head.append('')
    for sid,L in subs:
        head += L + ['']
    return "\n".join(head+body)+"\n"

# ---------- row -> item ----------
def build_attacks(row, name):
    attacks=[]
    for slot,pfx in (("1st","1st Attack"),("2nd","2nd Attack"),("3rd","3rd Attack")):
        cost=intval(row.get(f"{pfx} Cost")); dice=dice_list(row,pfx)
        bonus=row.get(f"{pfx} Bonus Effect") or ""; cond=row.get(f"{pfx} Condition") or ""
        mod=intval(row.get(f"{pfx} Modifier"))
        if cost==0 and not dice and not bonus.strip() and not cond.strip():
            continue  # empty attack
        flags,effects,immun = parse_effect_text(name, bonus, cond)
        if immun: warn(name, f"per-attack immunity ignored: {immun}")
        mv=dict(staminaCost=cost, damage=dice, modifier=mod, attackRange=flags.get("attackRange", intval(row.get("Range"))),
                repeat=flags.get("repeat",1), bonusEffects=effects,
                isNotZeroRange=flags.get("isNotZeroRange",False), isAOE=flags.get("isAOE",False),
                isMagic=flags.get("isMagic",False), isPush=flags.get("isPush",False),
                isIgnoreDefense=flags.get("isIgnoreDefense",False),
                statusEffect=flags.get("statusEffect",5), bonusMovement=flags.get("bonusMovement",0),
                repeatConstraint=flags.get("repeatConstraint",0))
        attacks.append(mv)
    return attacks

def process(csv_path, native_bulk, other_bulk, source):
    items=[]
    seen=set()
    with open(csv_path, newline='', encoding='utf-8') as f:
        rows=list(csv.DictReader(f))
    names=[r["Name"].strip() for r in rows]
    dup={n for n in names if names.count(n)>1}
    for row in rows:
        name=row["Name"].strip()
        typ=(row.get("Type") or "").strip()
        belongs=(row.get("Belongs To") or "").strip()
        # class + type int + output folder
        if source=="weapon":
            cls="Weapon"; tint = 3 if typ=="Spell" else 1; outbase=OUT_W; foldertag="Weapons"
            fth=intval(row.get("Fth"))
        else:
            if typ=="Shield": cls="Weapon"; tint=2; outbase=OUT_W; foldertag="Weapons"
            else: cls="Armour"; tint=0; outbase=OUT_A; foldertag="Armor"
            fth=intval(row.get("Faith"))
        # folder / image name (boss dup disambiguation)
        folder=name
        if name in dup and belongs=="Boss": folder=name+" (Boss)"
        # locate image
        cand=[folder+".png", name+".png"]
        src=None
        for base in (native_bulk, other_bulk):
            for c in cand:
                p=os.path.join(base,c)
                if os.path.exists(p): src=p; break
            if src: break
        if not src: warn(name,"IMAGE NOT FOUND"); continue

        item=dict(cls=cls, type=tint, name=name, folder=folder, isUpgrade=(" - Upgrade" in name),
                  outdir=os.path.join(outbase,folder), foldertag=foldertag,
                  imgpath=f"res://Resources/Prefabs/Equipment/{foldertag}/{folder}/{folder}.png",
                  imgsrc=src,
                  str=intval(row.get("Str")), dex=intval(row.get("Dex")), int=intval(row.get("Int")), fth=fth,
                  dodgeAbility=intval(row.get("Dodge")), upgradeSlots=intval(row.get("Slots")))
        # defense
        if source=="weapon":
            item["numHands"]=intval(row.get("# of Hands")) or 1
            item["attackRange"]=intval(row.get("Range"))
            item["physicalDefense"]=dice_list(row,"Phys.")
            item["magicDefense"]=dice_list(row,"Mag.")
            item["attacks"]=build_attacks(row,name)
            pflags,peff,pimm = parse_effect_text(name, row.get("Passive Ability"), row.get("Condition"), is_passive=True)
            item["passives"]=peff; item["immunities"]=pimm
            for e in peff: e.setdefault("duration",0)
        else:
            item["physicalDefense"]=dice_list(row,"Phy.")
            item["magicDefense"]=dice_list(row,"Mag.")
            item["physModifier"]=intval(row.get("Phy. Modifier"))
            item["magModifier"]=intval(row.get("Mag. Modifier"))
            if cls=="Weapon":  # shield: no attacks, but keep weapon fields
                item["numHands"]=1; item["attackRange"]=0; item["attacks"]=[]
            pflags,peff,pimm = parse_effect_text(name, row.get("Passive Ability"), row.get("Condition"), is_passive=True)
            item["passives"]=peff; item["immunities"]=pimm
        items.append(item)
    return items

def main():
    dry = "--dry" in sys.argv
    dry_names = set()
    if dry:
        idx=sys.argv.index("--dry")
        dry_names=set(a for a in sys.argv[idx+1:])
    items = process(DL+"Item Effectiveness (NEW) - Weapons.csv", BULK_W, BULK_A, "weapon") \
          + process(DL+"Item Effectiveness (NEW) - Armor.csv", BULK_A, BULK_W, "armor")
    print(f"Parsed {len(items)} items.")
    made=0
    for it in items:
        text=render(it)
        if dry:
            if it["name"] in dry_names:
                print("\n"+"="*70+f"\n### {it['folder']}  ({it['cls']} type={it['type']})  img={os.path.basename(it['imgsrc'])}\n"+"="*70)
                print(text)
            continue
        os.makedirs(it["outdir"], exist_ok=True)
        shutil.copy2(it["imgsrc"], os.path.join(it["outdir"], it["folder"]+".png"))
        with open(os.path.join(it["outdir"], it["folder"]+".tres"),"w",encoding="utf-8") as f:
            f.write(text)
        made+=1
    if not dry: print(f"Wrote {made} folders.")
    print(f"\n--- {len(warnings)} warnings ---")
    for w in warnings: print("  !", w)

if __name__=="__main__": main()
