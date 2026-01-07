using Godot;
using Godot.Collections;

[GlobalClass]
public partial class PlayerMove : Resource {
    
    // Attack
    [Export] public int staminaCost;
    [Export] public Array<Dice> damage;
    [Export] public int modifier = 0;

    [Export] public int attackRange;
    [Export] public bool isNotZeroRange = false;
    [Export] public bool isAOE;
    [Export] public bool isMagic;
    [Export] public bool isLeap;
    [Export] public bool isPush;
    [Export] public EncounterManager.StatusEffect statusEffect = EncounterManager.StatusEffect.NONE;

    // Move
    [Export] public int bonusMovement;
    [Export] public int repeat;

    public PlayerMove() : this(0, new Array<Dice>{}, 0, 0, false, false, false, false, EncounterManager.StatusEffect.NONE, 0,0) {}

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
