using Godot;
using Godot.Collections;

// One boss of the board game: its tier, the treasure cards it pays out, and (once it has been
// onboarded for real fights) its data card and behaviour cards. Written by
// Tools/onboard_bosses.py from the board game's Images/Bosses folder, which also holds this
// project's tier swaps. A boss without behaviour cards is fought as a stand-in encounter
// (EncounterGenerator) and pays this boss's treasure (BossRoster.Payout).
// No constructor: see the Resource note in CLAUDE.md.
[GlobalClass]
public partial class BossData : Resource {
	// What the boss data card's special ability does, looked up where it applies. Stored as an
	// ordinal: only append.
	public enum Ability { NONE, HEAVY_BLOWS, FLYING_HIGH, ICE_IN_HIS_VEINS, TITANITE_CONSTRUCT, PARRY, BRUTAL_IMPACT, CORRUPTED_DRAGONSLAYER, PILLARS_OF_WOE, ROYAL_PROTECTION }

	[Export] public string bossName = "";
	[Export] public WorldBossKind kind = WorldBossKind.MINI;
	// The campaign's last boss (Soul of Cinder), kept for the last section's main boss.
	[Export] public bool final;
	// Catalogue names (GameManager.GetTemplate) of the cards it pays, one of the party's choice.
	[Export] public string[] treasure = new string[0];

	// ----- The fight (p26) -----

	[Export] public Texture2D cardTexture;
	[Export] public Texture2D avatarTexture;
	// Where the art sits on the data card, for the activation bar's face (EnemyData).
	[Export] public Rect2 portraitRegion = new Rect2(0.27f, 0.14f, 0.46f, 0.325f);
	[Export] public int threatLevel = 10;
	[Export] public int health = 1;
	[Export] public int physicalDefense;
	[Export] public int magicalDefense;
	// Heat up once health falls to this or below (p28).
	[Export] public int heatUp;
	// How many of the starting cards make the deck (p28).
	[Export] public int deckSize = 4;
	[Export] public Ability ability = Ability.NONE;
	[Export(PropertyHint.ResourceType, "BossCard")] public Array<BossCard> cards = new Array<BossCard>();
	// The printed boss tile it is fought on (BoardLayout).
	[Export] public string tile = "";
	// The enemy its summon icon calls (the Royal Rat Authority's Undead Rats), and how many it
	// brings with it as the fight starts, spread round it (Enemy.SpreadAround).
	[Export] public EnemyData summon;
	[Export] public int summonAtStart;

	// A boss with behaviour cards is fought as itself; the rest are still stand-ins.
	public bool Playable => cards.Count > 0 && BoardLayout.Load(tile) != null;

	// The board token is an Enemy, which reads everything it shows and rolls against from an
	// EnemyData; this is that card for the boss, made once and never saved.
	private EnemyData asEnemy;

	public EnemyData AsEnemy() {
		asEnemy ??= new EnemyData {
			enemyName = bossName,
			cardTexture = cardTexture,
			avatarTexture = avatarTexture,
			portraitRegion = portraitRegion,
			threatLevel = threatLevel,
			health = health,
			physicalDefense = physicalDefense,
			magicalDefense = magicalDefense,
		};
		return asEnemy;
	}
}
