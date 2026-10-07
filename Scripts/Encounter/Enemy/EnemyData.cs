using Godot;
using Godot.Collections;

[GlobalClass]
public partial class EnemyData : Resource {

	// Cards that show the infinity symbol in their range circle. The grid is only
	// ever 7x7, so any distance check against this always passes.
	public const int UNLIMITED_RANGE = 99;

	// What some gear asks about (Dark Armour, Hollow Soldier Shield, Sunset Shield, the Alonne
	// armours). Going by name, agreed with Matt (Oct 2026). Stored as an ordinal: only append.
	public enum Kind { NONE, HOLLOW, ALONNE }

	// The set an enemy belongs to, which decides the world terrain it is met on
	// (EncounterGenerator.FAMILY_BY_TERRAIN). Encounters never mix families (Matt, Oct 2026).
	// Stored as an ordinal: only append.
	public enum Family { NONE, DARKROOT, IRON_KEEP, HOLLOWS, TOMB_OF_GIANTS, PAINTED_WORLD, ANOR_LONDO }

	[Export] public string enemyName = "";
	[Export] public Family family = Family.NONE;
	[Export] public Kind kind = Kind.NONE;
	[Export] public Texture2D cardTexture;
	[Export] public Texture2D avatarTexture;

	// Where the enemy's art sits on the card, as fractions of the card so the two card
	// sizes in the set share one default. The activation bar shows this crop rather than
	// the round board icon; nudge it per enemy when the art is off-centre.
	[Export] public Rect2 portraitRegion = new Rect2(0.27f, 0.14f, 0.46f, 0.325f);

	[Export] public int threatLevel = 1;
	[Export] public int health = 1;

	[Export] public int physicalDefense = 0;
	[Export] public int magicalDefense = 0;

	[Export(PropertyHint.ResourceType, "EnemyMove")] public Array<EnemyMove> moves = new Array<EnemyMove>();

	private AtlasTexture portrait;

	public Texture2D GetPortrait() {
		if (cardTexture == null) return avatarTexture;
		if (portrait != null) return portrait;

		Vector2 cardSize = cardTexture.GetSize();
		portrait = new AtlasTexture {
			Atlas = cardTexture,
			Region = new Rect2(portraitRegion.Position * cardSize, portraitRegion.Size * cardSize),
		};
		return portrait;
	}
}
