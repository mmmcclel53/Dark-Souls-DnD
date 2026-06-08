using Godot;

[GlobalClass]
public partial class Character : Resource
{
    [Export] public string name;
    [Export] public Texture2D image;
    [Export] public Texture2D avatar;
    [Export(PropertyHint.MultilineText)]
    public string description;

    [ExportGroup("Equipment")]
    [Export] public Weapon backupSlotDefault;
    [Export] public Weapon leftHandDefault;
    [Export] public Weapon rightHandDefault;
    [Export] public Armour armourDefault;
    
    [ExportGroup("Leveling")]
    [Export] public int threatLevel;
    [Export] public int[] strengthTiers = [10, 20, 30, 40];
    [Export] public int[] dexterityTiers = [10, 20, 30, 40];
    [Export] public int[] intelligenceTiers = [10, 20, 30, 40];
    [Export] public int[] faithTiers = [10, 20, 30, 40];

    public int GetDodge() {
        int dodge = armourDefault?.dodgeAbility ?? 0;
        Weapon[] weapons = [leftHandDefault, rightHandDefault, backupSlotDefault];
        foreach (Weapon w in weapons) dodge += w?.dodgeAbility ?? 0;
        return dodge;
    }

    public (int min, int max) GetBestPhysicalDamage() => GetBestAttackRange(false);
    public (int min, int max) GetBestMagicDamage() => GetBestAttackRange(true);
    public (int min, int max) GetPhysicalDefense() => GetDefenseRange(false);
    public (int min, int max) GetMagicDefense() => GetDefenseRange(true);

    private (int min, int max) GetBestAttackRange(bool magic) {
        int bestMin = 0, bestMax = 0;
        Weapon[] weapons = [leftHandDefault, rightHandDefault, backupSlotDefault];
        foreach (Weapon w in weapons) {
            if (w?.attacks == null) continue;
            foreach (PlayerMove move in w.attacks) {
                if (move?.damage == null || move.isMagic != magic) continue;
                int moveMin = move.modifier, moveMax = move.modifier;
                foreach (Dice d in move.damage) {
                    var (dMin, dMax) = FacesRange(d);
                    moveMin += dMin; moveMax += dMax;
                }
                moveMin = Mathf.Max(0, moveMin);
                if (moveMax > bestMax) { bestMax = moveMax; bestMin = moveMin; }
            }
        }
        return (bestMin, bestMax);
    }

    private (int min, int max) GetDefenseRange(bool magic) {
        int min = 0, max = 0;
        var armourDice = magic ? armourDefault?.magicDefense : armourDefault?.physicalDefense;
        if (armourDice != null)
            foreach (Dice d in armourDice) { var (a, b) = FacesRange(d); min += a; max += b; }
        Weapon[] weapons = [leftHandDefault, rightHandDefault, backupSlotDefault];
        foreach (Weapon w in weapons) {
            var wDice = magic ? w?.magicDefense : w?.physicalDefense;
            if (wDice == null) continue;
            foreach (Dice d in wDice) { var (a, b) = FacesRange(d); min += a; max += b; }
        }
        return (min, max);
    }

    private static (int min, int max) FacesRange(Dice d) {
        int min = int.MaxValue, max = int.MinValue;
        foreach (int face in d.dice) {
            if (face < min) min = face;
            if (face > max) max = face;
        }
        return (min == int.MaxValue ? 0 : min, max == int.MinValue ? 0 : max);
    }

    public Character() : this("Unknown", 1, [10, 20, 30, 40], [10, 20, 30, 40], [10, 20, 30, 40], [10, 20, 30, 40]) {}

    public Character(string name, int threatLevel, int[] strengthTiers, int[] dexterityTiers, int[] intelligenceTiers, int[] faithTiers)
    {
        this.name = name;
        this.threatLevel = threatLevel;
        this.strengthTiers = strengthTiers;
        this.dexterityTiers = dexterityTiers;
        this.intelligenceTiers = intelligenceTiers;
        this.faithTiers = faithTiers;
    }
}