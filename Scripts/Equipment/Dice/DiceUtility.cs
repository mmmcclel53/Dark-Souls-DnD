using Godot;
using System;
using System.Collections.Generic;

public static partial class DiceUtility {

    // DODGE is appended so the existing BLACK/BLUE/ORANGE ordinals in .tres files hold.
    public enum DICE_TYPE { BLACK, BLUE, ORANGE, DODGE };

    // Random.Shared is seeded per-thread and costs no allocation, which matters when a
    // single attack rolls a handful of dice back to back.
    public static int RollDice(int[] dice) {
        if (dice == null || dice.Length == 0) return 0;
        return dice[Random.Shared.Next(dice.Length)];
    }

    public static int Roll(Dice die) => die == null ? 0 : RollDice(die.dice);
}
