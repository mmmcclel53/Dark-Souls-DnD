using Godot;
using Godot.Collections;

[GlobalClass]
public partial class PlayerMove : Resource {

    // Constrains how a multi-hit attack (repeat > 1) may retarget between hits.
    //   FREE      : each hit may pick a new target ("x2 Turns" with no condition)
    //   ONE_ENEMY : every hit must strike the same single enemy ("x5 Turns / One Enemy")
    //   ONE_NODE  : every hit strikes one enemy within a single chosen node
    //               ("x5 Turns / Must Target 1 Node")
    public enum RepeatTarget { FREE, ONE_ENEMY, ONE_NODE }

    // Attack
    [Export] public int staminaCost;
    [Export] public Array<Dice> damage;
    [Export] public int modifier = 0;

    [Export] public int attackRange;
    [Export] public bool isNotZeroRange = false;   // "No Same Node" — ranged, cannot hit an enemy sharing your node
    [Export] public bool isAOE;
    [Export] public bool isMagic;
    [Export] public bool isLeap;
    [Export] public bool isPush;
    [Export] public bool isIgnoreDefense = false;  // "Ignore Physical Defense" — target skips its Block roll
    [Export] public EncounterManager.StatusEffect statusEffect = EncounterManager.StatusEffect.NONE;

    // Move
    // Shift (p23): free nodes the attack may move. Icons before the dice move before the roll,
    // after the dice after it; shiftAfter is how many of bonusMovement are the latter.
    [Export] public int bonusMovement;
    [Export] public int shiftAfter;
    [Export] public int repeat = 1;   // total times this attack is made (1 = once); from "xN Turns"
    [Export] public RepeatTarget repeatConstraint = RepeatTarget.FREE;

    public int ShiftBefore => Mathf.Max(0, bonusMovement - shiftAfter);
    public int ShiftAfter => Mathf.Min(shiftAfter, bonusMovement);
    public int Uses => Mathf.Max(1, repeat);

    public EquipmentEffect Effect(EquipmentEffect.EffectType type) {
        if (bonusEffects == null) return null;
        foreach (EquipmentEffect e in bonusEffects) if (e != null && e.type == type) return e;
        return null;
    }

    public bool HasEffect(EquipmentEffect.EffectType type) => Effect(type) != null;

    // An option that heals, refreshes, shields or empowers instead of attacking: no dice and
    // nothing to inflict, only effects on characters (Heal, Replenishment, Magic Barrier,
    // Great Magic Weapon). It picks characters, not an enemy.
    public bool IsSupport => (damage == null || damage.Count == 0) && modifier == 0
        && statusEffect == EncounterManager.StatusEffect.NONE && !isPush && bonusEffects != null && bonusEffects.Count > 0
        && !HasEffect(EquipmentEffect.EffectType.DIRECT_DAMAGE) && bonusMovement == 0;

    // Rapport: no roll, the chosen enemy simply suffers the damage.
    public bool IsDirectDamage => HasEffect(EquipmentEffect.EffectType.DIRECT_DAMAGE);

    // Nodes a Push moves the model: "Push x2" is a Push effect of 2.
    public int PushNodes => !isPush ? 0 : Mathf.Max(1, Effect(EquipmentEffect.EffectType.PUSH)?.magnitude ?? 1);

    // An option that is only a Shift (Carthus Curved Sword's, Lucerne's): nothing to roll or
    // inflict, so it moves and never asks for a target.
    public bool IsMovementOnly => bonusMovement > 0 && (damage == null || damage.Count == 0) && modifier == 0
        && statusEffect == EncounterManager.StatusEffect.NONE && !isPush;

    // Conditional / triggered riders (Damage +1 If Embered, heals, buffs, etc.).
    // Always-on attack keywords stay as the flags above; only genuinely conditional
    // or targeted effects live here. Empty for the majority of weapons.
    [Export] public Array<EquipmentEffect> bonusEffects = new();

    // Same trap as EnemyMove: this must not set anything, or Godot's strip-on-save plus
    // reconstruct-on-load loses it. Chaining here set repeat to 0 against a field
    // initializer of 1, which would have made every saved attack run zero times.
    public PlayerMove() {}

    public PlayerMove(int staminaCost, Array<Dice> damage, int modifier, int attackRange, bool isAOE, bool isMagic,bool isLeap, bool isPush, EncounterManager.StatusEffect statusEffect, int bonusMovement, int repeat)
    {   
        this.staminaCost = staminaCost;
        this.damage = damage;
        this.modifier = modifier;

        this.attackRange = attackRange;
        this.isAOE = isAOE;
        this.isMagic = isMagic;
        this.isLeap = isLeap;
        this.isPush = isPush;
        this.statusEffect = statusEffect;
        
        this.bonusMovement = bonusMovement;
        this.repeat = repeat;
    }
}
