using Godot;

[GlobalClass]
public partial class EnemyMove : Resource {
    [Export] public bool isLeap;
    [Export] public bool isPush;
    [Export] public bool towardsAggro;
    [Export] public int damage;

    // Move: nodes travelled. Positive moves towards the target, negative moves away
    // (the card shows the count at the top of the movement icon for towards, bottom for away).
    // Zero on a pure attack, and on a leap, which always reaches its target.
    [Export] public int direction;
    
    // Attack
    [Export] public int attackRange;
    [Export] public int dodgeDifficulty = 1;
    [Export] public bool isAOE;
    [Export] public bool isMagic;
    [Export] public EncounterManager.StatusEffect statusEffect = EncounterManager.StatusEffect.NONE;

    public EnemyMove() : this(false, false, true, 1, 1, 0, 1, false, false, EncounterManager.StatusEffect.NONE) {}

    public EnemyMove(bool isLeap, bool isPush, bool towardsAggro, int damage, int direction, int attackRange, int dodgeDifficulty, bool isAOE, bool isMagic, EncounterManager.StatusEffect statusEffect)
    {
        this.isLeap = isLeap;
        this.isPush = isPush;
        this.towardsAggro = towardsAggro;
        this.damage = damage;
        
        this.direction = direction;
        
        this.attackRange = attackRange;
        this.dodgeDifficulty = dodgeDifficulty;
        this.isAOE = isAOE;
        this.isMagic = isMagic;
        this.statusEffect = statusEffect;
    }
}
