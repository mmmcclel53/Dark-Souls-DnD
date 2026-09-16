using Godot;

// The dodge die: three blank faces and three dodge icons, so each die is a coin flip.
//
// Faces are 0/1 rather than a boolean icon flag so that a roll sums straight to the number
// of icons rolled. That is what a dodge difficulty of 2 or more is actually compared
// against (p25), and it keeps dodge dice rollable by the same DiceUtility.Roll as the rest.
[GlobalClass]
public partial class DodgeDice : Dice {

	private static DodgeDice standard;

	// The one dodge die the game has; equipment supplies a count, not a colour.
	public static DodgeDice Standard => standard ??= new DodgeDice();

	public DodgeDice() : base(DiceUtility.DICE_TYPE.DODGE, new int[]{ 0, 0, 0, 1, 1, 1 }) {
		min = 0;
		max = 1;
		avg = 0.5;
	}
}
