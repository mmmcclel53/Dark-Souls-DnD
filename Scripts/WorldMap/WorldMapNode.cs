using Godot;

// One hex tile on the world map. Draws a pseudo-3D extruded hex: the top face
// in the terrain color, plus stacked side walls whose height shows elevation.
// Supports two orientations: pointy-top normally, flat-top when the map view
// is rotated by an odd 30° step (a hex grid rotated 30° is a flat-top grid).
public partial class WorldMapNode : Control {
	public const float HEX_RADIUS = 46f;
	public const float WALL_STEP = 12f;
	public static readonly float HEX_WIDTH = Mathf.Sqrt(3f) * HEX_RADIUS;

	// Set by WorldMap before layout; all tiles share the map's orientation.
	public static bool FlatTop;

	public WorldNodeData data;

	public event System.Action<WorldMapNode> Clicked;

	private bool cleared;
	private bool reachable;
	private bool selected;
	private bool hovered;
	// The party's dropped souls lie here (SoulCache), drawn as the games' bloodstain. The only
	// hex that animates, so it is the only one that processes.
	private bool bloodstain;
	private float stainTime;

	public bool Cleared { get => cleared; set { cleared = value; QueueRedraw(); } }
	// Only a reachable hex does anything when clicked, so only a reachable hex shows the hand.
	public bool Reachable {
		get => reachable;
		set {
			reachable = value;
			MouseDefaultCursorShape = value ? CursorShape.PointingHand : CursorShape.Arrow;
			QueueRedraw();
		}
	}
	public bool Selected { get => selected; set { selected = value; QueueRedraw(); } }

	private static Texture2D[] levelIcons;
	private static Texture2D soulIcon;

	private const string SOUL_ICON_PATH = "res://Resources/Images/Sprites/Soul.png";
	private const int SOUL_FONT_SIZE = 12;
	// The soul counter's colours (SoulCounter), so the two read as the same thing.
	private static readonly Color SOUL_PLATE = new Color(0.06f, 0.05f, 0.05f, 0.92f);
	private static readonly Color SOUL_RIM = new Color(0.72f, 0.70f, 0.64f, 0.95f);
	private static readonly Color SOUL_NUMBER = new Color(0.87f, 0.85f, 0.80f);
	private static readonly Color SOUL_DIM = new Color(0.62f, 0.62f, 0.62f, 0.7f);

	// Center of the top hex face in local coordinates (control is sized to fit
	// either orientation).
	public Vector2 TopFaceCenter => new Vector2(HEX_RADIUS, HEX_RADIUS);

	private float WallHeight => data.elevation * WALL_STEP;

	public override void _Ready() {
		EnsureIconsLoaded();
		// Pass: this node handles left clicks itself; wheel zoom and drag-pan
		// events fall through to the board.
		MouseFilter = MouseFilterEnum.Pass;
		Size = new Vector2(HEX_RADIUS * 2f, HEX_RADIUS * 2f + WallHeight);
		MouseEntered += () => { hovered = true; QueueRedraw(); };
		MouseExited += () => { hovered = false; QueueRedraw(); };
		bloodstain = SoulCache.droppedAmount > 0 && SoulCache.droppedWorldNode == data.id;
		SetProcess(bloodstain);
		TooltipText = BuildTooltip();
	}

	public override void _Process(double delta) {
		stainTime += (float)delta;
		QueueRedraw();
	}

	public override void _GuiInput(InputEvent @event) {
		if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed) {
			AcceptEvent();
			Clicked?.Invoke(this);
		}
	}

	// Restrict mouse hit detection to the top hex face so clicks on a tall
	// tile's walls fall through to the tile visually behind them.
	public override bool _HasPoint(Vector2 point) {
		return Geometry2D.IsPointInPolygon(point, HexPoints(TopFaceCenter, HEX_RADIUS, FlatTop));
	}

	public override void _Draw() {
		Vector2 c = TopFaceCenter;
		Color face = TerrainColor(data.terrain);
		Vector2[] hex = HexPoints(c, HEX_RADIUS, FlatTop);

		DrawWalls(hex, c, face);

		Color drawFace = face;
		if (hovered && reachable) drawFace = face.Lerp(Colors.White, 0.18f);
		DrawColoredPolygon(hex, drawFace);

		if (data.terrain == WorldTerrain.VOLCANO)
			DrawOutline(hex, new Color(0.85f, 0.15f, 0.08f), 4f);
		else
			DrawOutline(hex, face.Darkened(0.45f), 1.5f);

		if (reachable)
			DrawOutline(HexPoints(c, HEX_RADIUS - 3f, FlatTop), new Color(1f, 1f, 1f, 0.85f), 2.5f);
		if (selected)
			DrawOutline(HexPoints(c, HEX_RADIUS - 7f, FlatTop), new Color(1f, 0.84f, 0.3f), 3f);

		if (bloodstain) DrawBloodstainPool(c);

		if (data.encounterType == WorldEncounterType.BONFIRE)
			DrawBonfire(c);
		else if (WorldMapManager.IsFight(data))
			DrawEncounter(c);

		if (bloodstain) DrawBloodstainMotes(c);

		if (WorldMapManager.IsFight(data)) {
			Vector2 corner = c + new Vector2(HEX_RADIUS * 0.52f, -HEX_RADIUS * 0.62f);
			if (cleared) DrawClearedBadge(corner);
			else DrawSoulGlyph(corner);
		}
	}

	// Hex vertices clockwise (y-down coordinates). Pointy-top starts at the top
	// vertex; flat-top starts at the top-right vertex.
	public static Vector2[] HexPoints(Vector2 center, float radius, bool flatTop) {
		const float W = 0.8660254f; // cos(30°)
		if (flatTop) {
			return new[] {
				center + new Vector2(0.5f * radius, -W * radius),
				center + new Vector2(radius, 0),
				center + new Vector2(0.5f * radius, W * radius),
				center + new Vector2(-0.5f * radius, W * radius),
				center + new Vector2(-radius, 0),
				center + new Vector2(-0.5f * radius, -W * radius),
			};
		}
		return new[] {
			center + new Vector2(0, -radius),
			center + new Vector2(W * radius, -0.5f * radius),
			center + new Vector2(W * radius, 0.5f * radius),
			center + new Vector2(0, radius),
			center + new Vector2(-W * radius, 0.5f * radius),
			center + new Vector2(-W * radius, -0.5f * radius),
		};
	}

	// The downward-facing edges get extruded wall quads, with a separator line
	// per elevation step so tall tiles read as stacked hexes. Pointy-top has
	// two such edges (SE, SW); flat-top has three (SE, bottom, SW).
	private void DrawWalls(Vector2[] hex, Vector2 center, Color face) {
		float h = WallHeight;
		if (h <= 0f) return;
		Vector2 drop = new Vector2(0, h);

		Color wallBase = data.terrain == WorldTerrain.VOLCANO ? new Color(0.25f, 0.2f, 0.2f) : face;
		int[] chain = FlatTop ? new[] { 1, 2, 3, 4 } : new[] { 2, 3, 4 };

		for (int i = 0; i < chain.Length - 1; i++) {
			Vector2 a = hex[chain[i]];
			Vector2 b = hex[chain[i + 1]];
			float midX = (a.X + b.X) / 2f - center.X;
			Color wall = wallBase.Darkened(midX > 1f ? 0.35f : midX < -1f ? 0.55f : 0.45f);
			DrawColoredPolygon(new[] { a, b, b + drop, a + drop }, wall);
		}

		var hull = new Vector2[chain.Length];
		for (int i = 0; i < chain.Length; i++) hull[i] = hex[chain[i]];
		Color seam = wallBase.Darkened(0.75f);
		for (int step = 1; step < data.elevation; step++) {
			Vector2 off = new Vector2(0, step * WALL_STEP);
			DrawPolyline(Offset(hull, off), seam, 1.2f);
		}
		DrawPolyline(Offset(hull, drop), seam, 1.5f);
	}

	private static Vector2[] Offset(Vector2[] points, Vector2 by) {
		var result = new Vector2[points.Length];
		for (int i = 0; i < points.Length; i++) result[i] = points[i] + by;
		return result;
	}

	private void DrawOutline(Vector2[] points, Color color, float width) {
		var closed = new Vector2[points.Length + 1];
		points.CopyTo(closed, 0);
		closed[points.Length] = points[0];
		DrawPolyline(closed, color, width, true);
	}

	private void DrawBonfire(Vector2 c) {
		DrawCircle(c, 32f, new Color(1f, 0.5f, 0.1f, 0.25f));
		var flame = new[] {
			c + new Vector2(0, -26), c + new Vector2(14, -6), c + new Vector2(10, 12),
			c + new Vector2(0, 18), c + new Vector2(-10, 12), c + new Vector2(-14, -6),
		};
		DrawColoredPolygon(flame, new Color(0.95f, 0.5f, 0.08f));
		var inner = new[] {
			c + new Vector2(0, -10), c + new Vector2(8, 4), c + new Vector2(0, 14), c + new Vector2(-8, 4),
		};
		DrawColoredPolygon(inner, new Color(1f, 0.85f, 0.3f));
	}

	// The encounter's toughest enemy in a ring coloured by its tier, with the level icon
	// sitting over the bottom of the ring. Without a plan, just the level icon.
	private void DrawEncounter(Vector2 c) {
		EncounterSpawn toughest = WorldMapManager.GetPlan(data.id)?.Toughest;
		Texture2D avatar = toughest?.Data?.avatarTexture;
		if (avatar == null) {
			DrawLevelIcon(c, new Vector2(72f, 72f));
			return;
		}

		// Beefier enemies loom larger here too, on the board's presence scale but compressed
		// to fit the hex: radius 20 for a normal face, up to 28 from ×2.5 presence.
		float presence = Enemy.PresenceFor(toughest.Health);
		float radius = Mathf.Lerp(20f, 28f, Mathf.Clamp((presence - 1f) / 1.5f, 0f, 1f));
		const float RIM = 3.5f;
		Vector2 centre = c + new Vector2(0, -10f);
		Color tint = cleared ? new Color(0.55f, 0.55f, 0.55f, 0.75f) : Colors.White;

		DrawCircle(centre, radius + RIM * 0.5f, new Color(0.06f, 0.05f, 0.05f, cleared ? 0.6f : 0.95f));
		DrawAvatarDisc(avatar, centre, radius, tint);
		Color rim = Enemy.TierRimColour(toughest.tier);
		if (cleared) rim = rim.Lerp(new Color(0.45f, 0.45f, 0.45f), 0.7f);
		DrawArc(centre, radius, 0, Mathf.Tau, 48, rim, RIM, true);
		if (data.encounterType == WorldEncounterType.BOSS) DrawBossRing(centre, radius + RIM + 2f);

		DrawLevelIcon(centre + new Vector2(0, radius), new Vector2(58f, 40f));
	}

	// A boss wears a second, outer ring: bone for a mini boss, gold for a main, crimson for a mega.
	private void DrawBossRing(Vector2 centre, float radius) {
		Color colour = data.boss switch {
			WorldBossKind.MINI => new Color(0.85f, 0.82f, 0.74f),
			WorldBossKind.MEGA => new Color(0.75f, 0.1f, 0.1f),
			_ => new Color(1f, 0.78f, 0.25f),
		};
		if (cleared) colour = colour.Lerp(new Color(0.45f, 0.45f, 0.45f), 0.7f);
		DrawArc(centre, radius, 0, Mathf.Tau, 48, colour, 2.5f, true);
		DrawArc(centre, radius + 4f, 0, Mathf.Tau, 48, colour with { A = colour.A * 0.6f }, 1.5f, true);
	}

	// The avatar art is a printed round token with its own gold rim; this keeps the face and
	// cuts it to a disc, so the tier rim replaces the printed one.
	private void DrawAvatarDisc(Texture2D texture, Vector2 centre, float radius, Color tint) {
		const int SEGMENTS = 40;
		const float UV_RADIUS = 0.44f;
		var points = new Vector2[SEGMENTS];
		var uvs = new Vector2[SEGMENTS];
		var colours = new Color[SEGMENTS];
		for (int i = 0; i < SEGMENTS; i++) {
			Vector2 dir = Vector2.FromAngle(Mathf.Tau * i / SEGMENTS);
			points[i] = centre + dir * radius;
			uvs[i] = new Vector2(0.5f, 0.5f) + dir * UV_RADIUS;
			colours[i] = tint;
		}
		DrawPolygon(points, colours, uvs, texture);
	}

	private void DrawLevelIcon(Vector2 c, Vector2 box) {
		Texture2D icon = levelIcons[Mathf.Clamp(data.level, 1, 4) - 1];
		if (icon != null) {
			// Aspect-fit into the icon box so non-square source art isn't skewed.
			float texW = icon.GetWidth(), texH = icon.GetHeight();
			float fit = Mathf.Min(box.X / texW, box.Y / texH);
			Vector2 size = new Vector2(texW * fit, texH * fit);
			Color tint = cleared ? new Color(0.6f, 0.6f, 0.6f, 0.7f) : Colors.White;
			DrawTextureRect(icon, new Rect2(c - size / 2f, size), false, tint);
			return;
		}
		// Placeholder if a level icon PNG is missing.
		float r = Mathf.Min(box.X, box.Y) * 0.36f;
		Color ring = cleared ? new Color(0.45f, 0.45f, 0.45f) : new Color(0.75f, 0.12f, 0.08f);
		DrawCircle(c, r, new Color(0.08f, 0.08f, 0.1f, 0.9f));
		DrawArc(c, r, 0, Mathf.Tau, 32, ring, 3f);
		var font = GetThemeDefaultFont();
		int fontSize = Mathf.RoundToInt(r);
		DrawString(font, c + new Vector2(-r, fontSize * 0.35f), data.level.ToString(),
			HorizontalAlignment.Center, r * 2f, fontSize, cleared ? new Color(0.7f, 0.7f, 0.7f) : Colors.White);
	}

	// What the fight still pays the party (SoulEconomy): lit with the number while its souls are
	// on the map, dimmed once they are held, dropped or spent, and lit again when a lost pile
	// gives them back. Nothing for a fight that pays nothing. It sits in the cleared badge's
	// corner, which it never shares: a win takes every soul the node has.
	private void DrawSoulGlyph(Vector2 corner) {
		if (soulIcon == null || WorldMapManager.SoulsPaidBy(data.id) <= 0) return;
		int souls = WorldMapManager.SoulsOnMap(data.id);
		const float ICON = 14f, HEIGHT = 18f, PAD = 4f, GAP = 2f;

		if (souls <= 0) {
			DrawCircle(corner, HEIGHT * 0.5f, SOUL_PLATE with { A = 0.7f });
			DrawArc(corner, HEIGHT * 0.5f, 0, Mathf.Tau, 24, SOUL_DIM, 1f, true);
			DrawTextureRect(soulIcon, new Rect2(corner - new Vector2(ICON, ICON) / 2f, new Vector2(ICON, ICON)), false, SOUL_DIM);
			return;
		}

		// Laid out leftwards from the badge's right edge, so it stays over the hex.
		Font font = GetThemeDefaultFont();
		string text = souls.ToString();
		float textWidth = font.GetStringSize(text, HorizontalAlignment.Left, -1, SOUL_FONT_SIZE).X;
		float width = PAD + ICON + GAP + textWidth + PAD;
		var plate = new Rect2(corner.X + HEIGHT * 0.5f - width, corner.Y - HEIGHT * 0.5f, width, HEIGHT);
		var box = new StyleBoxFlat { BgColor = SOUL_PLATE, BorderColor = SOUL_RIM };
		box.SetBorderWidthAll(1);
		box.SetCornerRadiusAll((int)(HEIGHT * 0.5f));
		DrawStyleBox(box, plate);

		var iconCentre = new Vector2(plate.Position.X + PAD + ICON * 0.5f, corner.Y);
		DrawCircle(iconCentre, ICON * 0.55f, new Color(1f, 0.85f, 0.5f, 0.22f));
		DrawTextureRect(soulIcon, new Rect2(iconCentre - new Vector2(ICON, ICON) / 2f, new Vector2(ICON, ICON)), false);
		float baseline = corner.Y + (font.GetAscent(SOUL_FONT_SIZE) - font.GetDescent(SOUL_FONT_SIZE)) * 0.5f;
		DrawString(font, new Vector2(iconCentre.X + ICON * 0.5f + GAP, baseline), text,
			HorizontalAlignment.Left, -1, SOUL_FONT_SIZE, SOUL_NUMBER);
	}

	private static readonly Color STAIN = new Color(0.3f, 1f, 0.5f);
	private const int STAIN_MOTES = 9;

	private float StainBreath => 0.75f + 0.25f * Mathf.Sin(stainTime * 2f);

	// A pool of green light on the ground under the encounter's face, breathing slowly: layered
	// ellipses, squashed to lie flat on the hex, brightest in the middle.
	private void DrawBloodstainPool(Vector2 c) {
		const int LAYERS = 7;
		DrawSetTransform(c + new Vector2(0, 4f), 0, new Vector2(1f, 0.55f));
		for (int i = 0; i < LAYERS; i++) {
			float radius = HEX_RADIUS * 0.95f * (1f - i / (float)LAYERS);
			DrawCircle(Vector2.Zero, radius, STAIN with { A = 0.22f * StainBreath });
		}
		DrawSetTransform(Vector2.Zero, 0, Vector2.One);
	}

	// Over the face: a green ring breathing round the encounter, and motes rising off the pool
	// and fading, each on its own loop, the way the games' bloodstains smoke.
	private void DrawBloodstainMotes(Vector2 c) {
		Vector2 face = c + new Vector2(0, -10f);
		DrawArc(face, 33f, 0, Mathf.Tau, 48, STAIN with { A = 0.25f * StainBreath }, 7f, true);
		DrawArc(face, 33f, 0, Mathf.Tau, 48, STAIN.Lerp(Colors.White, 0.3f) with { A = 0.85f * StainBreath }, 2f, true);

		for (int i = 0; i < STAIN_MOTES; i++) {
			float progress = Mathf.PosMod(stainTime * 0.35f + i / (float)STAIN_MOTES, 1f);
			float x = Mathf.Sin(i * 2.3f + stainTime * 0.9f) * HEX_RADIUS * 0.55f;
			Vector2 at = c + new Vector2(x, Mathf.Lerp(12f, -HEX_RADIUS, progress));
			float alpha = Mathf.Sin(progress * Mathf.Pi);
			float radius = 2f + (i % 3) * 0.8f;
			DrawCircle(at, radius * 2.6f, STAIN with { A = 0.3f * alpha });
			DrawCircle(at, radius, STAIN.Lerp(Colors.White, 0.5f) with { A = alpha });
		}
	}

	private void DrawClearedBadge(Vector2 c) {
		DrawCircle(c, 10f, new Color(0.15f, 0.45f, 0.18f));
		DrawArc(c, 10f, 0, Mathf.Tau, 24, new Color(0.9f, 1f, 0.9f, 0.8f), 1.4f);
		DrawPolyline(new[] { c + new Vector2(-5, 0), c + new Vector2(-1.5f, 4), c + new Vector2(5, -4) }, Colors.White, 2.2f);
	}

	private string BuildTooltip() {
		string line = $"{data.Title}\n{PrettyTerrain(data.terrain)}, elevation {data.elevation}";
		if (data.encounterType == WorldEncounterType.ENCOUNTER) line += $"\nEncounter — Level {data.level}";
		else if (data.encounterType == WorldEncounterType.BOSS) line += $"\n{PrettyBoss(data.boss)} — Level {data.level}";
		else if (data.encounterType == WorldEncounterType.BONFIRE) line += "\nBonfire";
		if (bloodstain) line += $"\nBloodstain — {SoulCache.droppedAmount} souls";
		return line;
	}

	public static string PrettyBoss(WorldBossKind kind) => kind switch {
		WorldBossKind.MINI => "Mini Boss",
		WorldBossKind.MEGA => "Mega Boss",
		_ => "Main Boss",
	};

	public static string PrettyTerrain(WorldTerrain t) {
		string s = t.ToString().ToLower();
		return char.ToUpper(s[0]) + s.Substring(1);
	}

	public static Color TerrainColor(WorldTerrain t) {
		switch (t) {
			case WorldTerrain.GRASS:    return new Color(0.33f, 0.54f, 0.25f);
			case WorldTerrain.MOUNTAIN: return new Color(0.55f, 0.56f, 0.58f);
			case WorldTerrain.SNOW:     return new Color(0.93f, 0.94f, 0.96f);
			case WorldTerrain.CITY:     return new Color(0.24f, 0.23f, 0.27f);
			case WorldTerrain.SAND:     return new Color(0.83f, 0.71f, 0.48f);
			case WorldTerrain.VOLCANO:  return new Color(0.10f, 0.09f, 0.09f);
			case WorldTerrain.LAVA:     return new Color(0.82f, 0.20f, 0.06f);
			case WorldTerrain.WATER:    return new Color(0.22f, 0.42f, 0.72f);
			default:                    return new Color(0.5f, 0.5f, 0.5f);
		}
	}

	private static void EnsureIconsLoaded() {
		if (levelIcons != null) return;
		soulIcon = ResourceLoader.Exists(SOUL_ICON_PATH) ? ResourceLoader.Load<Texture2D>(SOUL_ICON_PATH) : null;
		levelIcons = new Texture2D[4];
		for (int i = 1; i <= 4; i++) {
			string path = $"res://Resources/Images/Sprites/Encounters/Encounter Level {i}.png";
			if (ResourceLoader.Exists(path))
				levelIcons[i - 1] = ResourceLoader.Load<Texture2D>(path);
			else
				GD.PushWarning($"WorldMapNode: missing difficulty icon '{path}' — drawing a numbered placeholder");
		}
	}
}
