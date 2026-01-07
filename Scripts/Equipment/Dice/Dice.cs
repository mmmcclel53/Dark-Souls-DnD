using Godot;
using System;

[GlobalClass]
public partial class Dice : Resource {

    [Export] public DiceUtility.DICE_TYPE diceType = DiceUtility.DICE_TYPE.BLACK;
    [Export] public int[] dice = new int[]{ 0, 1, 1, 1, 2, 2 };

    [Export] public int min = 0;
    [Export] public int max = 2;
    [Export] public double avg = 1.167f;

    public Dice() : this(DiceUtility.DICE_TYPE.BLACK, new int[]{ 0, 1, 1, 1, 2, 2 }) {}

    public Dice(DiceUtility.DICE_TYPE diceType, int[] dice)
    {
        this.diceType = diceType;
        this.dice = dice;
    }
}
