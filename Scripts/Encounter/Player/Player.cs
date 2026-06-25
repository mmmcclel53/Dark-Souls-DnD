using Godot;

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

    [Export] public int stamina = 10;
    [Export] public int hits = 0;

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
    public Weapon GetBackupSlot() => GameManager.GetInstance(backupSlotId) as Weapon;
    public Weapon GetLeftHand() => GameManager.GetInstance(leftHandId) as Weapon;
    public Weapon GetRightHand() => GameManager.GetInstance(rightHandId) as Weapon;
    public Armour GetArmour() => GameManager.GetInstance(armourId) as Armour;

    public int GetMaxEndurance() => 10;
    public int GetRemainingHP() => GetMaxEndurance() - hits;
    public int GetCurrentStamina() => stamina;

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

    private (int min, int max) GetBestAttackRange(bool magic) {
        int bestMin = 0, bestMax = 0;
        Weapon[] weapons = { GetLeftHand(), GetRightHand(), GetBackupSlot() };
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
        var armour = GetArmour();
        var armourDice = magic ? armour?.magicDefense : armour?.physicalDefense;
        if (armourDice != null)
            foreach (Dice d in armourDice) { var (a, b) = FacesRange(d); min += a; max += b; }
        Weapon[] weapons = { GetLeftHand(), GetRightHand(), GetBackupSlot() };
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

    // public string currentNode;
    // public bool isAggro = false;
    // public EncounterManager.StatusEffect statusEffect = EncounterManager.StatusEffect.NONE;
}
