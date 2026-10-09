using Godot;

[GlobalClass]
public partial class Character : Resource
{
    [Export] public string name;
    [Export] public Texture2D image;
    [Export] public Texture2D avatar;
    [Export(PropertyHint.MultilineText)]
    public string description;

    // From the character board; see Heroic.
    [Export] public Heroic.Kind heroicAction;

    [ExportGroup("Equipment")]
    [Export] public Weapon backupSlotDefault;
    // More weapons starting in the backup slot (p12 lets it hold several). No class starts
    // with any; the test bench's Tester does, to show the backup carousel.
    [Export] public Godot.Collections.Array<Weapon> extraBackupDefaults = new();
    [Export] public Weapon leftHandDefault;
    [Export] public Weapon rightHandDefault;
    [Export] public Armour armourDefault;
    
    [ExportGroup("Leveling")]
    // Taunt (p40): how strongly enemies favour this character when breaking a
    // nearest-target tie. Characters have no threat level — only enemies do.
    [Export] public int taunt;
    [Export] public int[] strengthTiers = [10, 20, 30, 40];
    [Export] public int[] dexterityTiers = [10, 20, 30, 40];
    [Export] public int[] intelligenceTiers = [10, 20, 30, 40];
    [Export] public int[] faithTiers = [10, 20, 30, 40];
    // Where the board's 4x4 grid of level-up squares sits, as fractions of `image`: the centre
    // of the top-left square (Strength, Base) and the span to the bottom-right one's (Faith,
    // Tier 3), and a square's width. The Level Up screen puts the cubes there. Measured from
    // the scans by Tools/LevelSquares/measure.py.
    [Export] public Rect2 tierSquares;
    [Export] public float tierSquareSize;

    // The stats the starting gear asks anything of: where a new character's Base cubes go
    // (Player). Two for every class so far; the Deprived's gear asks for none.
    public System.Collections.Generic.List<Player.Stat> StartingGearStats() {
        var gear = new System.Collections.Generic.List<Equipment> { leftHandDefault, rightHandDefault, armourDefault, backupSlotDefault };
        foreach (Weapon extra in extraBackupDefaults) gear.Add(extra);
        var stats = new System.Collections.Generic.List<Player.Stat>();
        foreach (Player.Stat stat in Player.STATS) {
            foreach (Equipment item in gear) {
                if (item == null) continue;
                int req = stat switch {
                    Player.Stat.STRENGTH => item.strengthReq,
                    Player.Stat.DEXTERITY => item.dexterityReq,
                    Player.Stat.INTELLIGENCE => item.intelligenceReq,
                    _ => item.faithReq,
                };
                if (req > 0) { stats.Add(stat); break; }
            }
        }
        return stats;
    }

    public int GetDodge() {
        int dodge = armourDefault?.dodgeAbility ?? 0;
        Weapon[] weapons = [leftHandDefault, rightHandDefault];
        foreach (Weapon w in weapons) dodge += w?.dodgeAbility ?? 0;
        return dodge;
    }

    public (int min, int max) GetBestPhysicalDamage() => GetBestAttackRange(false);
    public (int min, int max) GetBestMagicDamage() => GetBestAttackRange(true);
    public (int min, int max) GetPhysicalDefense() => GetDefenseRange(false);
    public (int min, int max) GetMagicDefense() => GetDefenseRange(true);

    private (int min, int max) GetBestAttackRange(bool magic) {
        int bestMin = 0, bestMax = 0;
        Weapon[] weapons = [leftHandDefault, rightHandDefault];
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
        Weapon[] weapons = [leftHandDefault, rightHandDefault];
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

    public Character(string name, int taunt, int[] strengthTiers, int[] dexterityTiers, int[] intelligenceTiers, int[] faithTiers)
    {
        this.name = name;
        this.taunt = taunt;
        this.strengthTiers = strengthTiers;
        this.dexterityTiers = dexterityTiers;
        this.intelligenceTiers = intelligenceTiers;
        this.faithTiers = faithTiers;
    }
}