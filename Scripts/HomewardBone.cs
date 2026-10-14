using Godot;

// The party's Homeward Bones (Matt, Oct 2026): a consumable found as map loot. During a
// character's own activation it ends the encounter and returns the party to the last bonfire,
// as the card says ("discard this card to end an encounter and return to the Bonfire tile").
// Shown on the action bar beside the Party Estus, with how many the party holds.
//
// The count lives on the campaign save, like PartyEstus. Without a save (an encounter run on its
// own) there are none and every call no-ops.
public static class HomewardBone {

	private const string CARD_PATH = "res://Resources/Images/Sprites/Items/HomewardBone.png";
	// The bone on its card, as fractions of the card, for the token.
	private static readonly Rect2 BONE = new Rect2(0.27f, 0.36f, 0.48f, 0.38f);

	private static SaveGame Save => CampaignManager.CurrentSave;

	public static int Count => Save?.homewardBones ?? 0;

	// Only inside a campaign encounter, with somewhere to go back to.
	public static bool CanUse => Count > 0 && WorldMapManager.HasPendingEncounter
		&& !string.IsNullOrEmpty(WorldMapManager.LastBonfireId);

	public static void Add() {
		if (Save != null) Save.homewardBones++;
	}

	public static bool Spend() {
		if (Save == null || Save.homewardBones <= 0) return false;
		Save.homewardBones--;
		return true;
	}

	private static Texture2D card;
	private static AtlasTexture bone;

	// Straight from the file when the editor has not imported it yet, as TokenArt does.
	public static Texture2D Card {
		get {
			if (card != null) return card;
			if (ResourceLoader.Exists(CARD_PATH)) card = GD.Load<Texture2D>(CARD_PATH);
			if (card == null) {
				Image image = Image.LoadFromFile(ProjectSettings.GlobalizePath(CARD_PATH));
				if (image != null && !image.IsEmpty()) card = ImageTexture.CreateFromImage(image);
			}
			return card;
		}
	}

	public static Texture2D Token {
		get {
			if (bone != null || Card == null) return bone;
			Vector2 size = Card.GetSize();
			bone = new AtlasTexture {
				Atlas = Card,
				Region = new Rect2(BONE.Position * size, BONE.Size * size),
			};
			return bone;
		}
	}
}
