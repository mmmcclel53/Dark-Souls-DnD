using Godot;
using System;
using System.Collections.Generic;

public static partial class DiceUtility {

    public enum DICE_TYPE { BLACK, BLUE, ORANGE };

    public static int RollDice(int[] dice) {
        Random random = new Random();
        int i = random.Next(dice.Length);
        return dice[i];
    }
}
