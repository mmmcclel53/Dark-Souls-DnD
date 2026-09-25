using Godot;

// One slot of the equipment cross: a translucent dark square with a light hairline, the
// item's card art inside, in the style of the Dark Souls HUD. The art is a fixed crop of
// the printed card (every weapon card shares one layout, every armour card another), so no
// card ever needs slicing into its own resource; set wholeCard to show the entire card.
//
// A Button so the bar can raise a weapon by clicking it. The empty backup slot draws the
// swap arrows itself.
public partial class EquipmentSlot : Button
{

	[Export] public TextureRect art;
	[Export] public Control spentVeil;
	[Export] public Control glowRim;

	// Card fractions. Weapons, shields and spells put the item on a pedestal in the upper
	// middle of the card; armour sits lower and fills more of it.
	public static readonly Rect2 DEFAULT_WEAPON_REGION = new Rect2(0.22f, 0.13f, 0.56f, 0.46f);
	public static readonly Rect2 DEFAULT_ARMOUR_REGION = new Rect2(0.20f, 0.20f, 0.60f, 0.60f);

	[Export] public Rect2 weaponRegion = DEFAULT_WEAPON_REGION;
	[Export] public Rect2 armourRegion = DEFAULT_ARMOUR_REGION;
	[Export] public bool wholeCard = false;

	// Only the backup slot means anything when empty: it is where a swap comes from.
	[Export] public bool showSwapArrows = false;
	[Export] public Color arrowColour = new Color(0.62f, 0.58f, 0.50f, 0.6f);

	public Equipment item { get; private set; }

	public void Show(Equipment equipment) {
		item = equipment;
		if (art != null) art.Texture = equipment == null ? null : CropOf(equipment, wholeCard ? null : RegionFor(equipment));
		SetSpent(false);
		SetRaised(false);
		QueueRedraw();
	}

	public void Clear() => Show(null);

	public void SetRaised(bool raised) {
		if (glowRim != null) glowRim.Visible = raised;
	}

	public void SetSpent(bool spent) {
		if (spentVeil != null) spentVeil.Visible = spent;
	}

	public Rect2 RegionFor(Equipment equipment) =>
		equipment is Armour ? armourRegion : weaponRegion;

	public static Rect2 DefaultRegionFor(Equipment equipment) =>
		equipment is Armour ? DEFAULT_ARMOUR_REGION : DEFAULT_WEAPON_REGION;

	// The crop as a texture, for anywhere that shows a piece of gear small. A null region
	// gives the whole card.
	public static Texture2D CropOf(Equipment equipment, Rect2? region) {
		Texture2D card = equipment?.image;
		if (card == null || region == null) return card;

		Vector2 size = card.GetSize();
		return new AtlasTexture {
			Atlas = card,
			Region = new Rect2(region.Value.Position * size, region.Value.Size * size),
		};
	}

	public override void _Draw() {
		if (item != null || !showSwapArrows) return;

		// Two small triangles: the backup slot swaps with a hand at the start of an activation.
		Vector2 c = Size * 0.5f;
		float w = Mathf.Min(Size.X, Size.Y) * 0.16f;
		float h = w * 0.9f;
		float gap = 4f;
		DrawPolyline(new[] { new Vector2(c.X - w, c.Y - gap), new Vector2(c.X, c.Y - gap - h), new Vector2(c.X + w, c.Y - gap), new Vector2(c.X - w, c.Y - gap) }, arrowColour, 1.5f, true);
		DrawPolyline(new[] { new Vector2(c.X - w, c.Y + gap), new Vector2(c.X, c.Y + gap + h), new Vector2(c.X + w, c.Y + gap), new Vector2(c.X - w, c.Y + gap) }, arrowColour, 1.5f, true);
	}
}
