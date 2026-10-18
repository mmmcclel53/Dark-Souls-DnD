using Godot;

// One icon on a boss behaviour card, resolved left to right (p29).
//
// A Move icon is the four-armed cross: the arm carrying the number is the way the boss goes,
// relative to the way it faces. With a target icon beside it (skull for the Aggro holder, ring
// for the nearest character) the front arm moves towards the target and the back arm away,
// turning the boss as it goes (p29); without one the boss shifts that way without turning.
// The shield with an arrow beside it is Push.
//
// An Attack hits its target (and, with the Node icon, everyone on the target's node), or with
// the Area icon everyone in the attacked arcs within the card's range (p29). The arc diagram
// under it gives the attacked arcs (green) and the weak arcs (red): a character attacking the
// boss from a weak arc of the last attack on the top card of the discard pile gains a black
// die (p28).
//
// Stored as ordinals in the .tres files: only append to these enums.
// No constructor: see the Resource note in CLAUDE.md.
[GlobalClass]
public partial class BossAction : Resource {
	// HEAL: the boss's own Estus (the bottle icon), healing `damage` health.
	// SUMMON: `nodes` of BossData.summon, spread on the nodes round the boss.
	public enum Kind { MOVE, ATTACK, TURN_LEFT, TURN_RIGHT, ROTATE, LEAP, HEAL, SUMMON }
	public enum Target { NONE, AGGRO, NEAREST }
	// The arm of the Move cross the number sits on.
	public enum Arm { FRONT, BACK, LEFT, RIGHT }

	[System.Flags]
	public enum Arcs { NONE = 0, FRONT = 1, RIGHT = 2, BACK = 4, LEFT = 8, ALL = 15 }

	[Export] public Kind kind = Kind.MOVE;
	[Export] public Target target = Target.NONE;
	[Export] public bool push;

	// MOVE
	[Export] public int nodes;
	[Export] public Arm arm = Arm.FRONT;

	// ATTACK
	[Export] public int damage;
	[Export] public bool magic;
	[Export] public bool area;
	[Export] public bool node;
	[Export(PropertyHint.Flags, "Front,Right,Back,Left")] public int arcs;
	[Export(PropertyHint.Flags, "Front,Right,Back,Left")] public int weak;
	[Export] public EncounterManager.StatusEffect condition = EncounterManager.StatusEffect.NONE;

	public Arcs AttackedArcs => (Arcs)arcs;
	public Arcs WeakArcs => (Arcs)weak;
}
