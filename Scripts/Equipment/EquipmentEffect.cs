using Godot;

// Shared, data-driven effect used by both weapon attacks (PlayerMove.bonusEffects)
// and armour/shield passives (Armour.passives).
//
// The item cards express these as free text across two columns ("Bonus Effect" +
// "Condition"). That "Condition" text is overloaded — it is sometimes a trigger, a
// target scope, or a duration. We split it into three explicit fields so each card's
// intent is unambiguous:
//   - condition : the gate/timing that must be met for the effect to fire
//   - scope     : who/what the effect applies to
//   - duration  : how long the effect lasts once applied
//
// Effects whose backing systems don't exist yet (aggro, traps, embered state, hollow
// typing, heroic abilities) are defined here as inert placeholders so item data can be
// authored in full; they simply no-op until those systems land.
[GlobalClass]
public partial class EquipmentEffect : Resource {

    // What the effect does. Grouped by domain; not every entry is used by both
    // weapons and armour (e.g. dodge modifiers are armour-only, GRANT_MAGIC is
    // weapon-only), but the vocabulary is shared so both can draw from one list.
    public enum EffectType {
        NONE,

        // Combat modifiers to an attack
        BONUS_DAMAGE,               // "Damage +N"
        IGNORE_PHYSICAL_DEFENSE,    // target skips its Block roll (also a PlayerMove flag)
        GRANT_MAGIC,                // "All Attacks Gain Magic"
        ATTACK_DICE,                // "Attack +N <colour>" (see diceColour)
        APPLY_STATUS,               // apply a status (see inflictedStatus) — e.g. Vordt "Suffer Bleed"
        PUSH,                       // push target N nodes — conditional pushes ("Push Hollow")

        // Resource changes (self / allies)
        HEAL,                       // "Health +N"
        LOSE_HEALTH,                // "Health -N" / "You may suffer that damage"
        GAIN_STAMINA,               // "Stamina +N"
        LOSE_STAMINA,               // "Stamina -N"
        REMOVE_STATUS,              // "Remove N Status condition" (player's choice; uses magnitude)

        // Defensive / passive
        DEFENSE_DICE,               // "Phys/Magic Defense +N <colour> Dice"
        REDIRECT_DAMAGE,            // "You may suffer that damage" (ally in same node)

        // Movement / dodge (mostly armour)
        BONUS_MOVEMENT,             // "Move/Walk +N Node"
        DODGE_DICE,                 // "Dodge +N Dice"
        DODGE_RANGE,                // "Dodge +N Node"
        DODGE_STAMINA_MOD,          // "Dodge +/-N Stamina" (magnitude 0 = free dodge)
        RUN_COST_MOD,               // "Run +/-N Stamina"
        CANNOT_DODGE,               // "Cannot Dodge"
        CANNOT_MOVE,                // "Cannot walk or dodge"

        // Cost / requirement modifiers
        ATTACK_STAMINA_COST_MOD,    // "Stamina Attack Cost -N"
        UPGRADE_REQ_MOD,            // "Armor Upgrade reqs -N"
        BYPASS_TWO_HAND_CHECK,      // shield usable regardless of two-hand rule

        // Aggro / board control
        MAY_MOVE_AGGRO,             // "May move Aggro"
        MAY_TAKE_AGGRO,             // "May take Aggro"
    }

    // The gate/timing that must be satisfied for the effect to fire. NONE = always.
    // TODO(matt): entries below reference systems not yet built (embered state, hollow/
    // alonne enemy typing, traps, heroic abilities, aggro). They are inert placeholders
    // for now — revisit when those systems are implemented.
    public enum Condition {
        NONE,
        IF_EMBERED,                     // "If Embered"
        IF_MULTIPLE_ENEMIES_ON_NODE,    // "More than 1 enemy on node"
        IF_BLOCKING,                    // "If Blocking"
        IF_ATTACKER_HOLLOW,             // "If Hollow Type (Attacked)"
        IF_ATTACKER_ALONNE,             // "If Alonne Type Attacked"
        IF_TRAP_ACTIVATED,              // "If Trap Activated"
        IF_HEROIC_ABILITY_ACTIVATED,    // "If Heroic Ability Activated"
        IF_GAIN_HEALTH_ACTIVATED,       // "If Gain Health Activated"
        IF_AOE_ATTACK,                  // "If AOE Attack"
        IF_MAGIC_ATTACK,                // "If Magic Attack"
        IF_ARMOUR_UPGRADE_EQUIPPED,     // "If Armor Upgrade Equipped"
        IF_ATTACKING_WEAK_ARC,          // "If attacking weak arc"
        IF_ALLY_DAMAGED_SAME_NODE,      // "If Character Damaged in same node"
        ON_ATTACK,                      // "After Attack" / "If Attack"
        ON_START_ACTIVATION,            // "If Start Your Activation"
        ON_END_ACTIVATION,              // "If End Your/Character Activation"
        ON_ATTACKED,                    // when the wearer is attacked (generic)
    }

    // Who/what the effect applies to.
    public enum TargetScope {
        TARGET,                 // the attack's normal target(s)
        SELF,                   // the acting character
        ONE_ENEMY,              // a single chosen enemy (repeats lock to it)
        ONE_NODE,               // a single chosen node; repeats hit one enemy within it
        ALL_ENEMIES_IN_RANGE,   // "All Enemies In Range"
        ONE_CHARACTER,          // "One Character"
        TWO_CHARACTERS,         // "Two Characters"
        ALL_CHARACTERS,         // "All Characters"
        ONE_CHARACTER_IN_RANGE, // "One Character within N Range" (N is scopeRange)
    }

    // Which defense track a DEFENSE_DICE effect boosts.
    public enum DefenseKind { PHYSICAL, MAGIC, BOTH }

    // How long the effect persists once it fires.
    public enum Duration {
        INSTANT,                            // resolves immediately (damage, heal, status)
        UNTIL_END_OF_ACTIVATION,            // "Until End of Activation"
        UNTIL_NEXT_CHARACTER_ACTIVATION,    // "Until Next Character Activation"
        UNTIL_END_OF_ENEMY_ACTIVATION,      // "Until end of Enemy Activation"
        PERMANENT,                          // always-on passive (immunities, cost mods)
    }

    [Export] public EffectType type = EffectType.NONE;
    [Export] public int magnitude = 0;

    // Which status this effect applies. Only meaningful when type is APPLY_STATUS.
    // (Status immunities are not modeled here — see the item's immunities array.)
    [Export] public EncounterManager.StatusEffect inflictedStatus = EncounterManager.StatusEffect.NONE;

    // Dice colour for ATTACK_DICE / DEFENSE_DICE effects.
    [Export] public DiceUtility.DICE_TYPE diceColour = DiceUtility.DICE_TYPE.BLACK;

    // Which defense track this boosts. Only meaningful when type is DEFENSE_DICE.
    [Export] public DefenseKind defenseKind = DefenseKind.PHYSICAL;

    [Export] public Condition condition = Condition.NONE;
    [Export] public TargetScope scope = TargetScope.TARGET;
    // Range (in nodes) the scope reaches, for scopes like ONE_CHARACTER_IN_RANGE.
    [Export] public int scopeRange = 0;
    [Export] public Duration duration = Duration.INSTANT;
}
