using Godot;
using System.Collections.Generic;

// One character on the board in an EncounterSnapshot. Only what outlives an activation:
// everything BeginActivation resets is rebuilt by beginning the activation again.
[GlobalClass]
public partial class CharacterState : Resource
{
	// Index into the campaign party.
	[Export] public int partyIndex = -1;
	[Export] public int gridIndex = -1;
	[Export] public int[] conditions = new int[0];
	[Export] public bool faraamUsed;
	[Export] public Vector2I lastStep;
	// Magic Barrier and the like, which last into the enemy phase: (die type, defence kind) pairs.
	[Export] public int[] defenceBuffs = new int[0];

	public static CharacterState Of(PlayerToken token, int partyIndex) {
		List<int> buffs = new List<int>();
		foreach ((Dice die, EquipmentEffect.DefenseKind kind) in token.player.defenceBuffs) {
			buffs.Add((int)(die?.diceType ?? DiceUtility.DICE_TYPE.BLACK));
			buffs.Add((int)kind);
		}
		return new CharacterState {
			partyIndex = partyIndex,
			gridIndex = EncounterManager.GridIndexOf((Node2D)token.GetParent()),
			conditions = EncounterSnapshot.ConditionsOf(token.player.conditions),
			faraamUsed = token.faraamUsed,
			lastStep = token.lastStep,
			defenceBuffs = buffs.ToArray(),
		};
	}

	// The token is already on its node; this puts back what it carried.
	public void ApplyTo(PlayerToken token) {
		Player player = token.player;
		player.conditions.Clear();
		foreach (int condition in conditions) player.conditions.Add((EncounterManager.StatusEffect)condition);
		token.faraamUsed = faraamUsed;
		token.lastStep = lastStep;

		player.defenceBuffs.Clear();
		for (int i = 0; i + 1 < defenceBuffs.Length; i += 2) {
			Dice die = (DiceUtility.DICE_TYPE)defenceBuffs[i] switch {
				DiceUtility.DICE_TYPE.BLUE => Heroic.BlueDie,
				DiceUtility.DICE_TYPE.ORANGE => Heroic.OrangeDie,
				_ => Heroic.BlackDie,
			};
			player.defenceBuffs.Add((die, (EquipmentEffect.DefenseKind)defenceBuffs[i + 1]));
		}
	}
}
