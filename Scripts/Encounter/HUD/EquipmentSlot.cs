using Godot;
using System.Collections.Generic;

// One slot of the equipment cross: a translucent dark square with a light hairline, the
// item's card art inside, in the style of the Dark Souls HUD. The art is a crop of the
// printed card framed on the item itself (SubjectRegion), so no card ever needs slicing
// into its own resource; set wholeCard to show the entire card.
//
// A Button so the bar can raise a weapon by clicking it. The empty backup slot draws the
// swap arrows itself.
public partial class EquipmentSlot : Button
{

	[Export] public TextureRect art;
	[Export] public Control spentVeil;
	[Export] public Control glowRim;

	// Card fractions, used when autoFrame is off or a card defeats the detection. Weapons
	// with attack rows put the item high on the card; armour sits lower and fills more of it.
	public static readonly Rect2 DEFAULT_WEAPON_REGION = new Rect2(0.22f, 0.13f, 0.56f, 0.46f);
	public static readonly Rect2 DEFAULT_ARMOUR_REGION = new Rect2(0.20f, 0.20f, 0.60f, 0.60f);

	[Export] public Rect2 weaponRegion = DEFAULT_WEAPON_REGION;
	[Export] public Rect2 armourRegion = DEFAULT_ARMOUR_REGION;
	[Export] public bool wholeCard = false;
	[Export] public bool autoFrame = true;

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
		(autoFrame ? SubjectRegion(equipment?.image) : null) ?? (equipment is Armour ? armourRegion : weaponRegion);

	public static Rect2 DefaultRegionFor(Equipment equipment) =>
		SubjectRegion(equipment?.image) ?? (equipment is Armour ? DEFAULT_ARMOUR_REGION : DEFAULT_WEAPON_REGION);

	// ----- Framing the item -----
	//
	// Cards come in two layouts: a weapon with attack rows sits high, above a parchment
	// table, while shields, spells and armour without one sit lower and larger. One fixed crop
	// cut the bottom off the second kind, so each card is measured once instead: find where
	// the parchment starts, find the item against the dark card between the title and that,
	// and frame it at the slot's aspect. Everything is in card fractions, and the numbers were
	// tuned by eye against every card in the set.

	public const float SLOT_ASPECT = 84f / 108f;
	private const float TITLE_BOTTOM = 0.17f;   // two-line titles reach this far
	private const float ICON_ROW_TOP = 0.84f;
	private const float COLUMN_LEFT = 0.2f;   // the stat column and the icon column sit outside
	private const float COLUMN_RIGHT = 0.8f;
	private const float PADDING = 0.03f;
	private const float TABLE_SEARCH_TOP = 0.40f;
	private const float TABLE_RUN = 0.08f;      // a table is far taller than this; nothing else lit is
	private const float TABLE_MARGIN = 0.02f;

	private static readonly Dictionary<Texture2D, Rect2?> subjectCache = new Dictionary<Texture2D, Rect2?>();

	public static Rect2? SubjectRegion(Texture2D card) {
		if (card == null) return null;
		if (subjectCache.TryGetValue(card, out Rect2? cached)) return cached;

		Rect2? region = null;
		Image image = card.GetImage();
		if (image != null && !image.IsEmpty()) {
			if (image.IsCompressed()) image.Decompress();
			image.Convert(Image.Format.Rgb8);
			region = MeasureSubject(image.GetData(), image.GetWidth(), image.GetHeight());
		}
		subjectCache[card] = region;
		return region;
	}

	private static Rect2? MeasureSubject(byte[] rgb, int width, int height) {
		float Luma(int x, int y) {
			int i = (y * width + x) * 3;
			return (rgb[i] + rgb[i + 1] + rgb[i + 2]) / 3f;
		}
		int Chroma(int x, int y) {
			int i = (y * width + x) * 3;
			return Mathf.Max(rgb[i], Mathf.Max(rgb[i + 1], rgb[i + 2])) - Mathf.Min(rgb[i], Mathf.Min(rgb[i + 1], rgb[i + 2]));
		}

		int ya = (int)(height * TITLE_BOTTOM), yb = (int)(height * ICON_ROW_TOP);
		int xa = (int)(width * COLUMN_LEFT), xb = (int)(width * COLUMN_RIGHT);
		float card = Median(Luma, xa, xb, ya, yb);

		// The parchment: rows lit three quarters of the way across, for a long stretch. Scans
		// run from dim to bright and some cards have a lighter patterned back, so "lit" is
		// measured against the card itself. The long run is what rejects an item's edge that
		// happens to line up with the bottoms of the stat and icon columns.
		int px0 = (int)(width * 0.12f), px1 = (int)(width * 0.88f);
		bool[] lit = new bool[height];
		for (int y = (int)(height * TABLE_SEARCH_TOP); y < yb; y++) {
			int count = 0;
			for (int x = px0; x < px1; x++) if (Luma(x, y) > card + 40f) count++;
			lit[y] = count > 0.75f * (px1 - px0);
		}
		float bottom = ICON_ROW_TOP;
		int run = Mathf.Max(3, (int)(height * TABLE_RUN));
		for (int y = (int)(height * TABLE_SEARCH_TOP); y < yb - run; y++) {
			if (!lit[y]) continue;
			int count = 0;
			for (int i = y; i < y + run; i++) if (lit[i]) count++;
			if (count >= 0.85f * run) {
				bottom = (float)y / height - TABLE_MARGIN;
				break;
			}
		}

		yb = (int)(height * bottom) - 2;
		if (yb - ya < 10 || xb - xa < 10) return null;

		// The item against the card's own background above the table.
		float threshold = Median(Luma, xa, xb, ya, yb) + 45f;

		int[] rowHits = new int[yb - ya];
		int[] columnHits = new int[xb - xa];
		for (int y = ya; y < yb; y++) {
			for (int x = xa; x < xb; x++) {
				if (Luma(x, y) <= threshold && Chroma(x, y) <= 70) continue;
				rowHits[y - ya]++;
				columnHits[x - xa]++;
			}
		}
		int top = -1, last = -1, left = -1, right = -1;
		for (int i = 0; i < rowHits.Length; i++) {
			if (rowHits[i] <= 0.05f * columnHits.Length) continue;
			if (top < 0) top = i;
			last = i;
		}
		for (int i = 0; i < columnHits.Length; i++) {
			if (columnHits[i] <= 0.05f * rowHits.Length) continue;
			if (left < 0) left = i;
			right = i;
		}
		if (last - top < 5 || right - left < 5) return null;

		Rect2 subject = new Rect2(
			(float)(xa + left) / width - PADDING, (float)(ya + top) / height - PADDING,
			(float)(right - left) / width + 2f * PADDING, (float)(last - top) / height + 2f * PADDING);
		return Frame(subject, bottom, (float)width / height);
	}

	private static float Median(System.Func<int, int, float> luma, int x0, int x1, int y0, int y1) {
		List<float> samples = new List<float>();
		for (int y = y0; y < y1; y += 3)
			for (int x = x0; x < x1; x += 3) samples.Add(luma(x, y));
		if (samples.Count == 0) return 0f;
		samples.Sort();
		return samples[samples.Count / 2];
	}

	// Grows the item's box to the slot's aspect around its centre, then shrinks it until it
	// clears the side columns, the title and the parchment.
	private static Rect2 Frame(Rect2 subject, float bottom, float cardAspect) {
		Vector2 centre = subject.GetCenter();
		float w = subject.Size.X, h = subject.Size.Y;
		if (w * cardAspect / h < SLOT_ASPECT) w = h * SLOT_ASPECT / cardAspect;
		else h = w * cardAspect / SLOT_ASPECT;

		float fit = Mathf.Min(1f, Mathf.Min((COLUMN_RIGHT - COLUMN_LEFT) / w, (bottom - TITLE_BOTTOM) / h));
		w *= fit;
		h *= fit;
		float x = Within(centre.X - w * 0.5f, COLUMN_LEFT, COLUMN_RIGHT - w);
		float y = Within(centre.Y - h * 0.5f, TITLE_BOTTOM, bottom - h);
		return new Rect2(x, y, w, h);
	}

	// Not Mathf.Clamp: a frame that exactly fills its band leaves max a rounding error below
	// min, and Clamp throws on that.
	private static float Within(float value, float min, float max) => Mathf.Max(min, Mathf.Min(value, max));

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
