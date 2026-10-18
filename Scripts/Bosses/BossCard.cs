using Godot;
using Godot.Collections;

// One boss behaviour card (p27): its own range and dodge difficulty, how many times the whole
// card repeats, whether it is a Heat Up card, and its icons left to right.
// No constructor: see the Resource note in CLAUDE.md.
[GlobalClass]
public partial class BossCard : Resource {
	[Export] public string cardName = "";
	[Export] public Texture2D texture;
	// EnemyData.UNLIMITED_RANGE for the ∞ range symbol.
	[Export] public int range;
	[Export] public int dodge = 1;
	// The Repeat icon: the whole card is resolved this many times (p29).
	[Export] public int repeat = 1;
	[Export] public bool heatUp;
	// The pillar icon (Pillars of Woe): this card smashes the pillars it reaches.
	[Export] public bool pillars;
	[Export(PropertyHint.ResourceType, "BossAction")] public Array<BossAction> actions = new Array<BossAction>();

	// The last attack icon on the card, whose arcs are the weak ones while it is on top of the
	// discard pile (p28). A Move with damage on its Push shield is a movement attack and counts
	// (Skewering Charge, Matt). Null on a card with no attack.
	public BossAction LastAttack {
		get {
			BossAction last = null;
			foreach (BossAction action in actions) {
				if (action?.kind == BossAction.Kind.ATTACK || (action?.kind == BossAction.Kind.MOVE && action.damage > 0)) last = action;
			}
			return last;
		}
	}
}
