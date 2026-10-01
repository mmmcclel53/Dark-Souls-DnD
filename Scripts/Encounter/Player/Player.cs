using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class Player : Resource
{
    [Export] public string name = "Mattmac53";
    [Export] public Character character;

    [Export] public int strength;
    [Export] public int dexterity;
    [Export] public int intelligence;
    [Export] public int faith;

    [Export] public string backupSlotId = "";
    [Export] public string leftHandId = "";
    [Export] public string rightHandId = "";
    [Export] public string armourId = "";

    [Export] public string[] backupUpgradeIds = new string[0];
    [Export] public string[] leftHandUpgradeIds = new string[0];
    [Export] public string[] rightHandUpgradeIds = new string[0];
    [Export] public string[] armourUpgradeIds = new string[0];

    // The character board's tokens (p19): each is spent once and flipped, and only a
    // bonfire rest turns them back. Exported so a spent token stays spent across encounters
    // and saves; unlike the endurance bar, nothing but a rest clears them.
    [Export] public bool estusUsed;
    [Export] public bool heroicUsed;
    [Export] public bool luckUsed;

    // Encounter-scoped and deliberately NOT exported: the bar clears on victory (p19),
    // so it must never be written into the saved character. PlayerToken drives it.
    public Endurance endurance { get; private set; } = new Endurance();

    // Conditions live here rather than on the board token for the same reason the endurance
    // bar does: the portrait pane has a Player, not a PlayerToken, and it is where the
    // character's conditions are now shown.
    public readonly HashSet<EncounterManager.StatusEffect> conditions = new HashSet<EncounterManager.StatusEffect>();

    public Player() { }

    public Player(string name, Character character) {
        this.name = name;
        this.character = character;

        this.strength = character.strengthTiers[0];
        this.dexterity = character.dexterityTiers[0];
        this.intelligence = character.intelligenceTiers[0];
        this.faith = character.faithTiers[0];
    }

    // Equipment accessors via the GameManager owned pool.
    public void RefreshTokens() {
        estusUsed = false;
        heroicUsed = false;
        luckUsed = false;
    }

    public Weapon GetBackupSlot() => GameManager.GetInstance(backupSlotId) as Weapon;
    public Weapon GetLeftHand() => GameManager.GetInstance(leftHandId) as Weapon;
    public Weapon GetRightHand() => GameManager.GetInstance(rightHandId) as Weapon;
    public Armour GetArmour() => GameManager.GetInstance(armourId) as Armour;

    public int GetMaxEndurance() => Endurance.BOXES;
    public int GetRemainingHP() => endurance.healthRemaining;

    // Stamina and Health share the bar, so what is spendable is simply what is uncovered.
    public int GetCurrentStamina() => endurance.free;

    // Character level = 1 + total stat investments: how many tiers each attribute has
    // climbed above its starting tier, summed across all four. A freshly created
    // character is Level 1.
    public int GetLevel() {
        if (character == null) return 1;
        return 1
             + TierIndex(strength, character.strengthTiers)
             + TierIndex(dexterity, character.dexterityTiers)
             + TierIndex(intelligence, character.intelligenceTiers)
             + TierIndex(faith, character.faithTiers);
    }

    private static int TierIndex(int value, int[] tiers) {
        if (tiers == null) return 0;
        int idx = 0;
        for (int i = 0; i < tiers.Length; i++) if (value >= tiers[i]) idx = i;
        return idx;
    }

    // Status effects the wearer is immune to, gathered from every equipped piece.
    public List<EncounterManager.StatusEffect> GetImmunities() {
        var set = new HashSet<EncounterManager.StatusEffect>();
        void Collect(Godot.Collections.Array<EncounterManager.StatusEffect> arr) {
            if (arr == null) return;
            foreach (var s in arr)
                if (s != EncounterManager.StatusEffect.NONE) set.Add(s);
        }
        Collect(GetArmour()?.immunities);
        foreach (Weapon w in new[] { GetLeftHand(), GetRightHand(), GetBackupSlot() })
            Collect(w?.immunities);
        return new List<EncounterManager.StatusEffect>(set);
    }

    // Passive EquipmentEffects contributed by all equipped gear (armour + weapons/shields).
    public List<EquipmentEffect> GetEquippedPassives() {
        var list = new List<EquipmentEffect>();
        void Collect(Godot.Collections.Array<EquipmentEffect> arr) {
            if (arr == null) return;
            foreach (var e in arr)
                if (e != null && e.type != EquipmentEffect.EffectType.NONE) list.Add(e);
        }
        Collect(GetArmour()?.passives);
        foreach (Weapon w in new[] { GetLeftHand(), GetRightHand(), GetBackupSlot() })
            Collect(w?.passives);
        return list;
    }

    // Statuses this character's weapon attacks inflict on hit.
    public List<EncounterManager.StatusEffect> GetInflictedStatuses() {
        var set = new HashSet<EncounterManager.StatusEffect>();
        foreach (Weapon w in new[] { GetLeftHand(), GetRightHand(), GetBackupSlot() }) {
            if (w?.attacks == null) continue;
            foreach (PlayerMove m in w.attacks)
                if (m != null && m.statusEffect != EncounterManager.StatusEffect.NONE)
                    set.Add(m.statusEffect);
        }
        return new List<EncounterManager.StatusEffect>(set);
    }

    // ----- Stat summary, mirrors Character.* but reads currently-equipped instances -----

    public int GetDodge() {
        int dodge = GetArmour()?.dodgeAbility ?? 0;
        Weapon[] weapons = { GetLeftHand(), GetRightHand(), GetBackupSlot() };
        foreach (Weapon w in weapons) dodge += w?.dodgeAbility ?? 0;
        return dodge;
    }

    public (int min, int max) GetBestPhysicalDamage() => GetBestAttackRange(false);
    public (int min, int max) GetBestMagicDamage() => GetBestAttackRange(true);
    public (int min, int max) GetPhysicalDefense() => GetDefenseRange(false);
    public (int min, int max) GetMagicDefense() => GetDefenseRange(true);

    // The attack with the highest ceiling among everything held, and the dice it rolls: the
    // summary shows both the pool and its range, so they have to come from one place.
    public (List<Dice> dice, int modifier) GetBestAttackPool(bool magic) {
        List<Dice> best = new List<Dice>();
        int bestModifier = 0, bestMax = 0;
        Weapon[] weapons = { GetLeftHand(), GetRightHand(), GetBackupSlot() };
        foreach (Weapon w in weapons) {
            if (w?.attacks == null) continue;
            foreach (PlayerMove move in w.attacks) {
                if (move?.damage == null || move.isMagic != magic) continue;
                List<Dice> dice = new List<Dice>();
                foreach (Dice d in move.damage) if (d != null) dice.Add(d);
                int max = RangeOf(dice, move.modifier).max;
                if (max <= bestMax) continue;
                bestMax = max;
                best = dice;
                bestModifier = move.modifier;
            }
        }
        return (best, bestModifier);
    }

    // Block or Resist gathered from every equipped piece, as CombatResolver rolls it.
    public (List<Dice> dice, int modifier) GetDefensePool(bool magic) {
        List<Dice> pool = new List<Dice>();
        void Add(Godot.Collections.Array<Dice> dice) {
            if (dice == null) return;
            foreach (Dice d in dice) if (d != null) pool.Add(d);
        }
        Armour armour = GetArmour();
        Add(magic ? armour?.magicDefense : armour?.physicalDefense);
        foreach (Weapon w in new[] { GetLeftHand(), GetRightHand(), GetBackupSlot() }) {
            Add(magic ? w?.magicDefense : w?.physicalDefense);
        }
        int modifier = magic ? armour?.magicDefenseModifier ?? 0 : armour?.physicalDefenseModifier ?? 0;
        return (pool, modifier);
    }

    private (int min, int max) GetBestAttackRange(bool magic) {
        var (dice, modifier) = GetBestAttackPool(magic);
        return dice.Count == 0 && modifier == 0 ? (0, 0) : RangeOf(dice, modifier);
    }

    private (int min, int max) GetDefenseRange(bool magic) {
        var (dice, modifier) = GetDefensePool(magic);
        return RangeOf(dice, modifier);
    }

    private static (int min, int max) RangeOf(List<Dice> dice, int modifier) {
        int min = modifier, max = modifier;
        foreach (Dice d in dice) { var (a, b) = FacesRange(d); min += a; max += b; }
        return (Mathf.Max(0, min), max);
    }

    private static (int min, int max) FacesRange(Dice d) {
        int min = int.MaxValue, max = int.MinValue;
        foreach (int face in d.dice) {
            if (face < min) min = face;
            if (face > max) max = face;
        }
        return (min == int.MaxValue ? 0 : min, max == int.MinValue ? 0 : max);
    }

    // public string currentNode;
    // public bool isAggro = false;
    // public EncounterManager.StatusEffect statusEffect = EncounterManager.StatusEffect.NONE;
}
