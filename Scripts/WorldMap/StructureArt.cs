using Godot;

// A structure standing on a world map hex. Stored as an ordinal in the campaign JSON, so only ever append.
public enum WorldStructure { NONE, CASTLE, CATHEDRAL, LIBRARY, AQUEDUCT, TOWER, CRYPT, RUINS, SLUM }

// Code-drawn silhouettes of the structures, in the hex art's style: a front elevation standing on the
// hex face, two tones of stone (lit from the upper left, shadow on the right), an inked edge, warm
// windows. Everything is sized from the hex radius so it follows any zoom or window size. Nothing here
// knows about nodes or sight; `WorldMapNode` decides when to call it. Spires may rise past the hex's
// top edge on purpose, the way a tower stands over the ground behind it.
public static class StructureArt {

	private static readonly Color STONE = new Color(0.40f, 0.38f, 0.41f);
	private static readonly Color WINDOW = new Color(1f, 0.74f, 0.32f, 0.95f);
	private static readonly Color SHADOW = new Color(0f, 0f, 0f, 0.18f);
	private static readonly Color OPENING = new Color(0.03f, 0.02f, 0.04f, 0.78f);
	private const float MIN_STONE_LUMA = 0.42f;
	private static readonly Color DARK_OPENING = new Color(0.05f, 0.04f, 0.06f);
	private static readonly Color SMOKE = new Color(0.75f, 0.75f, 0.78f, 0.35f);

	// Set while drawing a landmark seen through the fog: one flat colour, no windows, shadow or smoke.
	private static Color? flat;

	// The kinds tall enough to be seen from afar (the doc's "landmarks that pierce the fog").
	public static bool IsLandmark(WorldStructure kind) =>
		kind == WorldStructure.CASTLE || kind == WorldStructure.CATHEDRAL || kind == WorldStructure.TOWER || kind == WorldStructure.LIBRARY;

	public static string Name(WorldStructure kind) => kind switch {
		WorldStructure.CASTLE => "Castle",
		WorldStructure.CATHEDRAL => "Cathedral",
		WorldStructure.LIBRARY => "Library",
		WorldStructure.AQUEDUCT => "Aqueduct",
		WorldStructure.TOWER => "Tower",
		WorldStructure.CRYPT => "Crypt",
		WorldStructure.RUINS => "Ruins",
		WorldStructure.SLUM => "Hovels",
		_ => "",
	};

	private struct Palette {
		public Color lit, dark, ink, roof;
		public Palette(Color stone) {
			if (flat is Color f) {
				lit = dark = roof = f;
				ink = f.Darkened(0.3f);
				return;
			}
			lit = stone.Lightened(0.18f);
			dark = stone.Darkened(0.38f);
			ink = stone.Darkened(0.72f);
			roof = stone.Darkened(0.55f).Lerp(new Color(0.35f, 0.18f, 0.16f), 0.35f);
		}
	}

	// `c` is the hex's top-face centre, `r` its radius. `seed` varies the ruins and shacks per hex.
	// `stone` defaults to a neutral grey; pass the terrain colour darkened to tie it to its ground.
	// `silhouette` draws the whole thing in that one colour, for a landmark on a hex not yet seen.
	public static void Draw(CanvasItem canvas, Vector2 c, float r, WorldStructure kind, int seed = 0, Color? stone = null, Color? silhouette = null) {
		if (kind == WorldStructure.NONE) return;
		flat = silhouette;
		Palette p = new Palette(Floored(stone ?? STONE));
		float ground = c.Y + r * 0.38f;
		if (flat == null) DrawGroundShadow(canvas, new Vector2(c.X, ground), r * 0.72f, r * 0.17f);
		switch (kind) {
			case WorldStructure.CASTLE: Castle(canvas, c.X, ground, r, p); break;
			case WorldStructure.CATHEDRAL: Cathedral(canvas, c.X, ground, r, p); break;
			case WorldStructure.LIBRARY: Library(canvas, c.X, ground, r, p); break;
			case WorldStructure.AQUEDUCT: Aqueduct(canvas, c.X, ground, r, p); break;
			case WorldStructure.TOWER: Tower(canvas, c.X, ground, r, p); break;
			case WorldStructure.CRYPT: Crypt(canvas, c.X, ground, r, p, seed); break;
			case WorldStructure.RUINS: Ruins(canvas, c.X, ground, r, p, seed); break;
			case WorldStructure.SLUM: Slum(canvas, c.X, ground, r, p, seed); break;
		}
		flat = null;
	}

	// ---- the buildings -------------------------------------------------------------------------

	private static void Castle(CanvasItem cv, float cx, float g, float r, Palette p) {
		float halfW = r * 0.66f;
		float wallH = r * 0.34f;
		float towerW = r * 0.17f;
		float towerH = r * 0.62f;

		Block(cv, cx - halfW + towerW, g - wallH, cx + halfW - towerW, g, p);
		Battlements(cv, cx - halfW + towerW, cx + halfW - towerW, g - wallH, r * 0.07f, r * 0.07f, p.lit);
		Arch(cv, cx, g, r * 0.08f, r * 0.17f, Opening(), pointed: false);

		foreach (float tx in new[] { cx - halfW + towerW, cx + halfW - towerW }) {
			Block(cv, tx - towerW, g - towerH, tx + towerW, g, p);
			Cone(cv, tx, g - towerH, towerW * 1.15f, r * 0.26f, p.roof, p.ink);
			Window(cv, new Vector2(tx, g - towerH * 0.55f), r * 0.045f, r * 0.08f);
		}

		float keepW = r * 0.2f;
		float keepH = r * 0.86f;
		Block(cv, cx - keepW, g - keepH, cx + keepW, g - wallH * 0.5f, p);
		Battlements(cv, cx - keepW, cx + keepW, g - keepH, r * 0.06f, r * 0.07f, p.lit);
		Window(cv, new Vector2(cx - keepW * 0.4f, g - keepH * 0.7f), r * 0.045f, r * 0.08f);
		Window(cv, new Vector2(cx + keepW * 0.4f, g - keepH * 0.7f), r * 0.045f, r * 0.08f);
		Flag(cv, new Vector2(cx, g - keepH), r * 0.2f, r * 0.08f, new Color(0.6f, 0.1f, 0.1f), p.ink);
	}

	private static void Cathedral(CanvasItem cv, float cx, float g, float r, Palette p) {
		float naveHalf = r * 0.5f;
		float naveH = r * 0.42f;
		float ridge = g - naveH - r * 0.3f;

		Block(cv, cx - naveHalf, g - naveH, cx + naveHalf, g, p);
		for (int i = 0; i < 4; i++) {
			float bx = cx - naveHalf * 0.8f + i * naveHalf * 0.53f;
			Poly(cv, p.dark, new Vector2(bx - r * 0.04f, g), new Vector2(bx + r * 0.04f, g), new Vector2(bx, g - naveH * 0.9f));
		}
		Poly(cv, p.roof, new Vector2(cx - naveHalf - r * 0.04f, g - naveH), new Vector2(cx + naveHalf + r * 0.04f, g - naveH), new Vector2(cx, ridge));
		Outline(cv, p.ink, new Vector2(cx - naveHalf - r * 0.04f, g - naveH), new Vector2(cx, ridge), new Vector2(cx + naveHalf + r * 0.04f, g - naveH));
		Arch(cv, cx, g, r * 0.08f, r * 0.2f, Opening(), pointed: true);
		Rose(cv, new Vector2(cx, g - naveH - r * 0.1f), r * 0.075f, p.ink);
		Window(cv, new Vector2(cx + naveHalf * 0.3f, g - naveH * 0.55f), r * 0.03f, r * 0.1f, pointed: true);
		Window(cv, new Vector2(cx + naveHalf * 0.65f, g - naveH * 0.55f), r * 0.03f, r * 0.1f, pointed: true);

		float spireX = cx - naveHalf * 0.72f;
		float spireW = r * 0.13f;
		float spireBase = g - naveH - r * 0.55f;
		Block(cv, spireX - spireW, spireBase, spireX + spireW, g - naveH * 0.3f, p);
		Window(cv, new Vector2(spireX, spireBase + r * 0.18f), r * 0.04f, r * 0.1f, pointed: true);
		Cone(cv, spireX, spireBase, spireW * 1.1f, r * 0.55f, p.roof, p.ink);
		cv.DrawLine(new Vector2(spireX, spireBase - r * 0.55f), new Vector2(spireX, spireBase - r * 0.68f), p.ink, 1.5f);
		cv.DrawLine(new Vector2(spireX - r * 0.04f, spireBase - r * 0.62f), new Vector2(spireX + r * 0.04f, spireBase - r * 0.62f), p.ink, 1.5f);
	}

	private static void Library(CanvasItem cv, float cx, float g, float r, Palette p) {
		float halfW = r * 0.62f;
		float bodyH = r * 0.4f;
		float stepH = r * 0.05f;

		Block(cv, cx - halfW - r * 0.06f, g - stepH, cx + halfW + r * 0.06f, g, p);
		Block(cv, cx - halfW, g - bodyH, cx + halfW, g - stepH, p);
		int columns = 7;
		for (int i = 0; i < columns; i++) {
			float x = Mathf.Lerp(cx - halfW * 0.84f, cx + halfW * 0.84f, i / (float)(columns - 1));
			cv.DrawLine(new Vector2(x, g - stepH), new Vector2(x, g - bodyH + r * 0.04f), p.ink, 2.2f);
			cv.DrawLine(new Vector2(x - 1f, g - stepH), new Vector2(x - 1f, g - bodyH + r * 0.04f), p.lit, 1f);
		}
		float pedTop = g - bodyH - r * 0.18f;
		Poly(cv, p.dark, new Vector2(cx - halfW - r * 0.04f, g - bodyH), new Vector2(cx + halfW + r * 0.04f, g - bodyH), new Vector2(cx, pedTop));
		Outline(cv, p.ink, new Vector2(cx - halfW - r * 0.04f, g - bodyH), new Vector2(cx, pedTop), new Vector2(cx + halfW + r * 0.04f, g - bodyH));

		float domeR = r * 0.19f;
		Vector2 domeC = new Vector2(cx, g - bodyH - r * 0.06f);
		Block(cv, cx - domeR, domeC.Y - r * 0.08f, cx + domeR, domeC.Y, p);
		cv.DrawCircle(domeC + new Vector2(0, -r * 0.08f), domeR, p.roof);
		cv.DrawArc(domeC + new Vector2(0, -r * 0.08f), domeR, Mathf.Pi, Mathf.Tau, 18, p.ink, 1.5f, true);
		cv.DrawArc(domeC + new Vector2(-domeR * 0.25f, -r * 0.1f), domeR * 0.7f, Mathf.Pi * 1.1f, Mathf.Pi * 1.55f, 10, p.lit, 1.5f, true);
		Poly(cv, p.dark, new Vector2(cx - domeR, domeC.Y), new Vector2(cx + domeR, domeC.Y), new Vector2(cx + domeR, domeC.Y + r * 0.02f), new Vector2(cx - domeR, domeC.Y + r * 0.02f));
		cv.DrawLine(domeC + new Vector2(0, -r * 0.08f - domeR), domeC + new Vector2(0, -r * 0.08f - domeR - r * 0.07f), p.ink, 1.5f);
	}

	private static void Aqueduct(CanvasItem cv, float cx, float g, float r, Palette p) {
		float halfW = r * 0.82f;
		float pierH = r * 0.48f;
		float channelH = r * 0.1f;
		int arches = 4;
		float span = halfW * 2f / arches;

		Block(cv, cx - halfW, g - pierH - channelH, cx + halfW, g, p);
		for (int i = 0; i < arches; i++) {
			float ax = cx - halfW + span * (i + 0.5f);
			Arch(cv, ax, g, span * 0.4f, pierH * 0.82f, flat ?? OPENING, pointed: false);
		}
		Poly(cv, p.dark, new Vector2(cx - halfW, g - pierH - channelH), new Vector2(cx + halfW, g - pierH - channelH), new Vector2(cx + halfW, g - pierH), new Vector2(cx - halfW, g - pierH));
		cv.DrawLine(new Vector2(cx - halfW, g - pierH - channelH), new Vector2(cx + halfW, g - pierH - channelH), p.lit, 1.5f);
		cv.DrawLine(new Vector2(cx - halfW, g - pierH), new Vector2(cx + halfW, g - pierH), p.ink, 1.2f);
	}

	private static void Tower(CanvasItem cv, float cx, float g, float r, Palette p) {
		float w = r * 0.2f;
		float h = r * 1.15f;
		float top = g - h;

		Poly(cv, p.dark, new Vector2(cx - w * 1.4f, g), new Vector2(cx + w * 1.4f, g), new Vector2(cx + w, g - r * 0.12f), new Vector2(cx - w, g - r * 0.12f));
		Block(cv, cx - w, top, cx + w, g - r * 0.1f, p);
		Poly(cv, p.lit, new Vector2(cx - w * 1.25f, top + r * 0.02f), new Vector2(cx + w * 1.25f, top + r * 0.02f), new Vector2(cx + w * 1.25f, top + r * 0.1f), new Vector2(cx - w * 1.25f, top + r * 0.1f));
		Battlements(cv, cx - w * 1.25f, cx + w * 1.25f, top + r * 0.02f, r * 0.06f, r * 0.07f, p.lit);
		Window(cv, new Vector2(cx, top + r * 0.3f), r * 0.045f, r * 0.1f, pointed: true);
		Window(cv, new Vector2(cx, top + r * 0.62f), r * 0.045f, r * 0.1f, pointed: true);
		Arch(cv, cx, g - r * 0.1f, r * 0.06f, r * 0.14f, Opening(), pointed: true);
		Flag(cv, new Vector2(cx, top + r * 0.02f), r * 0.16f, r * 0.07f, new Color(0.15f, 0.2f, 0.45f), p.ink);
	}

	private static void Crypt(CanvasItem cv, float cx, float g, float r, Palette p, int seed) {
		float halfW = r * 0.3f;
		float h = r * 0.3f;
		Block(cv, cx - halfW, g - h, cx + halfW, g, p);
		Poly(cv, p.roof, new Vector2(cx - halfW - r * 0.04f, g - h), new Vector2(cx + halfW + r * 0.04f, g - h), new Vector2(cx, g - h - r * 0.16f));
		Outline(cv, p.ink, new Vector2(cx - halfW - r * 0.04f, g - h), new Vector2(cx, g - h - r * 0.16f), new Vector2(cx + halfW + r * 0.04f, g - h));
		Arch(cv, cx, g + r * 0.02f, r * 0.11f, h * 0.9f, Opening(), pointed: true);
		for (int i = 0; i < 3; i++) {
			float y = g - h * 0.6f + i * h * 0.22f;
			cv.DrawLine(new Vector2(cx - r * 0.09f, y), new Vector2(cx + r * 0.09f, y), new Color(0f, 0f, 0f, 0.5f), 1.2f);
		}
		cv.DrawLine(new Vector2(cx, g - h - r * 0.16f), new Vector2(cx, g - h - r * 0.3f), p.ink, 2f);
		cv.DrawLine(new Vector2(cx - r * 0.045f, g - h - r * 0.25f), new Vector2(cx + r * 0.045f, g - h - r * 0.25f), p.ink, 2f);

		var rng = new RandomNumberGenerator { Seed = (ulong)(seed * 7919 + 17) };
		for (int i = 0; i < 5; i++) {
			float side = i % 2 == 0 ? -1f : 1f;
			float x = cx + side * (halfW + r * 0.1f + rng.RandfRange(0f, r * 0.3f));
			float y = g + rng.RandfRange(-r * 0.08f, r * 0.1f);
			float sw = r * 0.045f;
			float sh = r * rng.RandfRange(0.1f, 0.17f);
			float lean = rng.RandfRange(-0.25f, 0.25f) * sw;
			Poly(cv, p.dark, new Vector2(x - sw, y), new Vector2(x + sw, y), new Vector2(x + sw + lean, y - sh), new Vector2(x - sw + lean, y - sh));
			cv.DrawArc(new Vector2(x + lean, y - sh), sw, Mathf.Pi, Mathf.Tau, 8, p.dark, sw * 2f, false);
			cv.DrawLine(new Vector2(x - sw, y), new Vector2(x - sw + lean, y - sh), p.lit, 1f);
		}
	}

	private static void Ruins(CanvasItem cv, float cx, float g, float r, Palette p, int seed) {
		var rng = new RandomNumberGenerator { Seed = (ulong)(seed * 104729 + 3) };
		int stubs = 5;
		for (int i = 0; i < stubs; i++) {
			float x = Mathf.Lerp(cx - r * 0.6f, cx + r * 0.6f, i / (float)(stubs - 1)) + rng.RandfRange(-r * 0.05f, r * 0.05f);
			float w = r * rng.RandfRange(0.05f, 0.1f);
			float h = r * rng.RandfRange(0.1f, 0.38f);
			float broken = rng.RandfRange(-w * 0.8f, w * 0.8f);
			Poly(cv, p.lit, new Vector2(x - w, g), new Vector2(x, g), new Vector2(x, g - h + broken), new Vector2(x - w, g - h));
			Poly(cv, p.dark, new Vector2(x, g), new Vector2(x + w, g), new Vector2(x + w, g - h * 0.85f), new Vector2(x, g - h + broken));
			Outline(cv, p.ink, new Vector2(x - w, g), new Vector2(x - w, g - h), new Vector2(x, g - h + broken), new Vector2(x + w, g - h * 0.85f), new Vector2(x + w, g));
		}
		float archX = cx + r * 0.14f;
		float archH = r * 0.26f;
		float archW = r * 0.2f;
		float pierW = r * 0.07f;
		Block(cv, archX - archW - pierW, g - archH, archX - archW, g, p);
		Block(cv, archX + archW, g - archH * 0.6f, archX + archW + pierW, g, p);
		var arc = new Vector2[9];
		for (int i = 0; i < arc.Length; i++) {
			float a = Mathf.Pi + Mathf.Pi * 0.62f * i / (arc.Length - 1);
			arc[i] = new Vector2(archX + Mathf.Cos(a) * (archW + pierW * 0.5f), g - archH + Mathf.Sin(a) * (archW + pierW * 0.5f));
		}
		cv.DrawPolyline(arc, p.dark, pierW, true);
		cv.DrawPolyline(arc, p.ink, 1.2f, true);
		for (int i = 0; i < arc.Length - 1; i += 2) cv.DrawLine(arc[i], arc[i] + (arc[i] - new Vector2(archX, g - archH)).Normalized() * pierW * 0.5f, p.ink, 1f);
		for (int i = 0; i < 7; i++) {
			Vector2 at = new Vector2(cx + rng.RandfRange(-r * 0.6f, r * 0.6f), g + rng.RandfRange(-r * 0.02f, r * 0.12f));
			float s = r * rng.RandfRange(0.02f, 0.045f);
			Poly(cv, i % 2 == 0 ? p.dark : p.lit, at + new Vector2(-s, s * 0.5f), at + new Vector2(s, s * 0.5f), at + new Vector2(s * 0.6f, -s * 0.5f), at + new Vector2(-s * 0.7f, -s * 0.4f));
		}
	}

	private static void Slum(CanvasItem cv, float cx, float g, float r, Palette p, int seed) {
		var rng = new RandomNumberGenerator { Seed = (ulong)(seed * 31337 + 5) };
		Color plank = p.dark.Lerp(new Color(0.3f, 0.22f, 0.15f), 0.5f);
		Color plankLit = plank.Lightened(0.2f);
		int shacks = 4;
		for (int i = 0; i < shacks; i++) {
			float x = Mathf.Lerp(cx - r * 0.55f, cx + r * 0.55f, i / (float)(shacks - 1)) + rng.RandfRange(-r * 0.04f, r * 0.04f);
			float y = g + (i % 2 == 0 ? -r * 0.06f : r * 0.06f);
			float w = r * rng.RandfRange(0.15f, 0.2f);
			float h = r * rng.RandfRange(0.17f, 0.26f);
			float roofRise = r * rng.RandfRange(0.05f, 0.1f);
			bool leanRight = rng.Randf() > 0.5f;
			float tilt = rng.RandfRange(-0.08f, 0.08f) * w;
			Poly(cv, plankLit, new Vector2(x - w, y), new Vector2(x, y), new Vector2(x + tilt, y - h), new Vector2(x - w + tilt, y - h));
			Poly(cv, plank, new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w + tilt, y - h), new Vector2(x + tilt, y - h));
			Vector2 ra = new Vector2(x - w - r * 0.03f + tilt, y - h);
			Vector2 rb = new Vector2(x + w + r * 0.03f + tilt, y - h);
			Vector2 peak = leanRight ? rb + new Vector2(0, -roofRise) : ra + new Vector2(0, -roofRise);
			Poly(cv, p.roof, ra, rb, peak);
			cv.DrawLine(ra, peak, p.ink, 1.3f);
			cv.DrawLine(peak, rb, p.ink, 1.3f);
			for (int s = 1; s < 4; s++) {
				float py = y - h * s / 4f;
				cv.DrawLine(new Vector2(x - w + tilt * s / 4f, py), new Vector2(x + w + tilt * s / 4f, py), p.ink.With(a: 0.5f), 1f);
			}
			Window(cv, new Vector2(x + (leanRight ? -w * 0.45f : w * 0.45f) + tilt * 0.5f, y - h * 0.5f), r * 0.03f, r * 0.04f);
			if (i == 1 || i == 2) {
				Vector2 chimney = peak + new Vector2(leanRight ? -r * 0.05f : r * 0.05f, 0);
				Poly(cv, p.dark, chimney + new Vector2(-r * 0.02f, 0), chimney + new Vector2(r * 0.02f, 0), chimney + new Vector2(r * 0.02f, -r * 0.08f), chimney + new Vector2(-r * 0.02f, -r * 0.08f));
				for (int k = 0; k < 3; k++) {
					if (flat == null) cv.DrawCircle(chimney + new Vector2(k * r * 0.03f + r * 0.01f, -r * 0.1f - k * r * 0.06f), r * (0.025f + k * 0.012f), SMOKE);
				}
			}
		}
	}

	// A stone taken from a dark terrain (volcano, city) would vanish into it: lift it to a floor.
	private static Color Floored(Color stone) {
		float luma = stone.Luminance;
		return luma >= MIN_STONE_LUMA ? stone : stone.Lerp(new Color(0.62f, 0.6f, 0.62f), Mathf.Clamp((MIN_STONE_LUMA - luma) / MIN_STONE_LUMA * 1.6f, 0f, 1f));
	}

	// ---- the pieces ----------------------------------------------------------------------------

	private static Color Opening() => flat ?? DARK_OPENING;

	private static void DrawGroundShadow(CanvasItem cv, Vector2 at, float rx, float ry) {
		var pts = new Vector2[20];
		for (int i = 0; i < pts.Length; i++) {
			float a = Mathf.Tau * i / pts.Length;
			pts[i] = at + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
		}
		cv.DrawColoredPolygon(pts, SHADOW);
	}

	// A wall: lit on the left two fifths, shadowed on the right, with a soft edge between and an inked outline.
	private static void Block(CanvasItem cv, float x0, float y0, float x1, float y1, Palette p) {
		float split = Mathf.Lerp(x0, x1, 0.4f);
		Poly(cv, p.lit, new Vector2(x0, y0), new Vector2(split, y0), new Vector2(split, y1), new Vector2(x0, y1));
		Poly(cv, p.dark, new Vector2(split, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(split, y1));
		cv.DrawLine(new Vector2(split, y0), new Vector2(split, y1), p.lit.Lerp(p.dark, 0.5f), 1.5f);
		Outline(cv, p.ink, new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1), new Vector2(x0, y0));
	}

	private static void Battlements(CanvasItem cv, float x0, float x1, float yTop, float merlonW, float h, Color color) {
		int count = Mathf.Max(2, (int)((x1 - x0) / (merlonW * 2f)));
		float pitch = (x1 - x0) / count;
		for (int i = 0; i < count; i++) {
			float mx = x0 + pitch * i + (pitch - merlonW) * 0.5f;
			Poly(cv, color, new Vector2(mx, yTop), new Vector2(mx + merlonW, yTop), new Vector2(mx + merlonW, yTop - h), new Vector2(mx, yTop - h));
		}
	}

	private static void Cone(CanvasItem cv, float cx, float yBase, float halfW, float h, Color fill, Color ink) {
		Poly(cv, fill, new Vector2(cx - halfW, yBase), new Vector2(cx + halfW, yBase), new Vector2(cx, yBase - h));
		Poly(cv, fill.Lightened(0.15f), new Vector2(cx - halfW, yBase), new Vector2(cx - halfW * 0.3f, yBase), new Vector2(cx, yBase - h));
		Outline(cv, ink, new Vector2(cx - halfW, yBase), new Vector2(cx, yBase - h), new Vector2(cx + halfW, yBase));
	}

	// An opening: a rectangle under a round or pointed head, drawn as one polygon.
	private static void Arch(CanvasItem cv, float cx, float yBase, float halfW, float h, Color fill, bool pointed) {
		int segs = 8;
		var pts = new Vector2[segs + 3];
		pts[0] = new Vector2(cx - halfW, yBase);
		float springY = yBase - h + halfW;
		for (int i = 0; i <= segs; i++) {
			float a = Mathf.Pi + Mathf.Pi * i / segs;
			float rad = pointed ? halfW * 1.25f : halfW;
			Vector2 head = new Vector2(cx + Mathf.Cos(a) * rad, springY + Mathf.Sin(a) * rad);
			if (pointed) head.X = Mathf.Clamp(head.X, cx - halfW, cx + halfW);
			pts[i + 1] = head;
		}
		pts[segs + 2] = new Vector2(cx + halfW, yBase);
		cv.DrawColoredPolygon(pts, fill);
	}

	private static void Window(CanvasItem cv, Vector2 at, float halfW, float h, bool pointed = false) {
		if (flat != null) return;
		Arch(cv, at.X, at.Y + h * 0.5f, halfW, h, WINDOW, pointed);
		cv.DrawCircle(at, h * 0.9f, new Color(WINDOW, 0.12f));
	}

	private static void Rose(CanvasItem cv, Vector2 at, float radius, Color ink) {
		if (flat != null) return;
		cv.DrawCircle(at, radius, WINDOW);
		cv.DrawArc(at, radius, 0, Mathf.Tau, 16, ink, 1.3f, true);
		for (int i = 0; i < 4; i++) {
			float a = Mathf.Pi * i / 4f;
			cv.DrawLine(at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, at - new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, ink, 1f);
		}
	}

	private static void Flag(CanvasItem cv, Vector2 foot, float poleH, float flagW, Color cloth, Color ink) {
		Vector2 top = foot + new Vector2(0, -poleH);
		cv.DrawLine(foot, top, ink, 1.5f);
		Poly(cv, flat ?? cloth, top, top + new Vector2(flagW, flagW * 0.35f), top + new Vector2(0, flagW * 0.7f));
	}

	private static void Poly(CanvasItem cv, Color color, params Vector2[] pts) {
		cv.DrawColoredPolygon(pts, color);
	}

	private static void Outline(CanvasItem cv, Color ink, params Vector2[] pts) {
		cv.DrawPolyline(pts, ink, 1.3f, true);
	}
}

internal static class ColorWith {
	public static Color With(this Color c, float a) => new Color(c.R, c.G, c.B, a);
}
