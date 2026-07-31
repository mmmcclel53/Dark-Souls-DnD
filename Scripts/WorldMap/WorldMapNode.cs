using Godot;

// One hex tile on the world map. Draws a pseudo-3D extruded hex: the top face
// in the terrain color, plus stacked side walls whose height shows elevation.
public partial class WorldMapNode : Control {
	public const float HEX_RADIUS = 46f;
	public const float WALL_STEP = 12f;
	public static readonly float HEX_WIDTH = Mathf.Sqrt(3f) * HEX_RADIUS;

	public WorldNodeData data;

	public event System.Action<WorldMapNode> Clicked;

	private bool cleared;
	private bool reachable;
	private bool selected;
	private bool hovered;

	public bool Cleared { get => cleared; set { cleared = value; QueueRedraw(); } }
	public bool Reachable { get => reachable; set { reachable = value; QueueRedraw(); } }
	public bool Selected { get => selected; set { selected = value; QueueRedraw(); } }

	private static Texture2D[] levelIcons;

	// Center of the top hex face in local coordinates.
	public Vector2 TopFaceCenter => new Vector2(HEX_WIDTH / 2f, HEX_RADIUS);

	private float WallHeight => data.elevation * WALL_STEP;

	public override void _Ready() {
		EnsureIconsLoaded();
		MouseFilter = MouseFilterEnum.Stop;
		Size = new Vector2(HEX_WIDTH, HEX_RADIUS * 2f + WallHeight);
		MouseEntered += () => { hovered = true; QueueRedraw(); };
		MouseExited += () => { hovered = false; QueueRedraw(); };
		TooltipText = BuildTooltip();
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
		return Geometry2D.IsPointInPolygon(point, HexPoints(TopFaceCenter, HEX_RADIUS));
	}

	public override void _Draw() {
		Vector2 c = TopFaceCenter;
		Color face = TerrainColor(data.terrain);
		Vector2[] hex = HexPoints(c, HEX_RADIUS);

		DrawWalls(hex, face);

		Color drawFace = face;
		if (hovered && reachable) drawFace = face.Lerp(Colors.White, 0.18f);
		DrawColoredPolygon(hex, drawFace);

		if (data.terrain == WorldTerrain.VOLCANO)
			DrawOutline(hex, new Color(0.85f, 0.15f, 0.08f), 4f);
		else
			DrawOutline(hex, face.Darkened(0.45f), 1.5f);

		if (reachable)
			DrawOutline(HexPoints(c, HEX_RADIUS - 3f), new Color(1f, 1f, 1f, 0.85f), 2.5f);
		if (selected)
			DrawOutline(HexPoints(c, HEX_RADIUS - 7f), new Color(1f, 0.84f, 0.3f), 3f);

		if (data.encounterType == WorldEncounterType.BONFIRE)
			DrawBonfire(c);
		else if (data.encounterType == WorldEncounterType.ENCOUNTER)
			DrawLevelIcon(c);

		if (data.encounterType == WorldEncounterType.ENCOUNTER && cleared)
			DrawClearedBadge(c + new Vector2(HEX_RADIUS * 0.42f, -HEX_RADIUS * 0.55f));
	}

	// Pointy-top hex vertices, clockwise from the top vertex (y-down coordinates).
	private static Vector2[] HexPoints(Vector2 center, float radius) {
		const float W = 0.8660254f; // cos(30°)
		return new[] {
			center + new Vector2(0, -radius),
			center + new Vector2(W * radius, -0.5f * radius),
			center + new Vector2(W * radius, 0.5f * radius),
			center + new Vector2(0, radius),
			center + new Vector2(-W * radius, 0.5f * radius),
			center + new Vector2(-W * radius, -radius * 0.5f),
		};
	}

	// The two downward-facing edges (SE and SW) get extruded wall quads, with a
	// separator line per elevation step so tall tiles read as stacked hexes.
	private void DrawWalls(Vector2[] hex, Color face) {
		float h = WallHeight;
		if (h <= 0f) return;
		Vector2 drop = new Vector2(0, h);

		Color wallBase = data.terrain == WorldTerrain.VOLCANO ? new Color(0.25f, 0.2f, 0.2f) : face;
		Color seWall = wallBase.Darkened(0.35f);
		Color swWall = wallBase.Darkened(0.55f);

		// hex[2]=right-bottom, hex[3]=bottom, hex[4]=left-bottom
		DrawColoredPolygon(new[] { hex[2], hex[3], hex[3] + drop, hex[2] + drop }, seWall);
		DrawColoredPolygon(new[] { hex[3], hex[4], hex[4] + drop, hex[3] + drop }, swWall);

		Color seam = wallBase.Darkened(0.75f);
		for (int step = 1; step < data.elevation; step++) {
			Vector2 off = new Vector2(0, step * WALL_STEP);
			DrawPolyline(new[] { hex[2] + off, hex[3] + off, hex[4] + off }, seam, 1.2f);
		}
		DrawPolyline(new[] { hex[2] + drop, hex[3] + drop, hex[4] + drop }, seam, 1.5f);
	}

	private void DrawOutline(Vector2[] points, Color color, float width) {
		var closed = new Vector2[points.Length + 1];
		points.CopyTo(closed, 0);
		closed[points.Length] = points[0];
		DrawPolyline(closed, color, width, true);
	}

	private void DrawBonfire(Vector2 c) {
		DrawCircle(c, 16f, new Color(1f, 0.5f, 0.1f, 0.25f));
		var flame = new[] {
			c + new Vector2(0, -13), c + new Vector2(7, -3), c + new Vector2(5, 6),
			c + new Vector2(0, 9), c + new Vector2(-5, 6), c + new Vector2(-7, -3),
		};
		DrawColoredPolygon(flame, new Color(0.95f, 0.5f, 0.08f));
		var inner = new[] {
			c + new Vector2(0, -5), c + new Vector2(4, 2), c + new Vector2(0, 7), c + new Vector2(-4, 2),
		};
		DrawColoredPolygon(inner, new Color(1f, 0.85f, 0.3f));
	}

	private void DrawLevelIcon(Vector2 c) {
		Texture2D icon = levelIcons[Mathf.Clamp(data.level, 1, 4) - 1];
		if (icon != null) {
			const float ICON_SIZE = 36f;
			Color tint = cleared ? new Color(0.6f, 0.6f, 0.6f, 0.7f) : Colors.White;
			DrawTextureRect(icon, new Rect2(c - new Vector2(ICON_SIZE / 2f, ICON_SIZE / 2f), new Vector2(ICON_SIZE, ICON_SIZE)), false, tint);
			return;
		}
		// Placeholder until the level icon PNGs exist.
		Color ring = cleared ? new Color(0.45f, 0.45f, 0.45f) : new Color(0.75f, 0.12f, 0.08f);
		DrawCircle(c, 13f, new Color(0.08f, 0.08f, 0.1f, 0.9f));
		DrawArc(c, 13f, 0, Mathf.Tau, 24, ring, 2f);
		var font = GetThemeDefaultFont();
		DrawString(font, c + new Vector2(-12f, 6f), data.level.ToString(),
			HorizontalAlignment.Center, 24f, 16, cleared ? new Color(0.7f, 0.7f, 0.7f) : Colors.White);
	}

	private void DrawClearedBadge(Vector2 c) {
		DrawCircle(c, 8f, new Color(0.15f, 0.45f, 0.18f));
		DrawArc(c, 8f, 0, Mathf.Tau, 20, new Color(0.9f, 1f, 0.9f, 0.8f), 1.2f);
		DrawPolyline(new[] { c + new Vector2(-4, 0), c + new Vector2(-1, 3), c + new Vector2(4, -3) }, Colors.White, 1.8f);
	}

	private string BuildTooltip() {
		string line = $"{data.Title}\n{PrettyTerrain(data.terrain)}, elevation {data.elevation}";
		if (data.encounterType == WorldEncounterType.ENCOUNTER) line += $"\nEncounter — Level {data.level}";
		else if (data.encounterType == WorldEncounterType.BONFIRE) line += "\nBonfire";
		return line;
	}

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
		levelIcons = new Texture2D[4];
		for (int i = 1; i <= 4; i++) {
			string path = $"res://Resources/Images/Sprites/Encounters/level_{i}.png";
			if (ResourceLoader.Exists(path))
				levelIcons[i - 1] = ResourceLoader.Load<Texture2D>(path);
			else
				GD.PushWarning($"WorldMapNode: missing difficulty icon '{path}' — drawing a numbered placeholder");
		}
	}
}
