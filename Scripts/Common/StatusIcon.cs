using Godot;
using System.Collections.Generic;

// One status a character carries (a condition, or the Aggro token), as an icon whose tooltip
// reads "[icon] Name" over a line saying what it does. Shown beside a portrait in the party
// pane and round the action bar's avatar.
public partial class StatusIcon : TextureRect
{
	// Null for the Aggro token.
	public EncounterManager.StatusEffect? condition;

	private static readonly Color GOLD = new Color(0.93f, 0.85f, 0.62f);
	private static readonly Color TEXT = new Color(0.85f, 0.82f, 0.74f);
	private static Font titleFont;

	public static StatusIcon For(EncounterManager.StatusEffect? condition, float size) {
		// The expand mode before any size, or the texture's own size wins.
		StatusIcon icon = new StatusIcon {
			ExpandMode = ExpandModeEnum.IgnoreSize,
			StretchMode = StretchModeEnum.KeepAspectCentered,
			condition = condition,
			Texture = condition is EncounterManager.StatusEffect c ? ConditionArt.Icon(c) : GD.Load<Texture2D>(PlayerToken.AGGRO_TEXTURE),
			CustomMinimumSize = new Vector2(size, size),
			Size = new Vector2(size, size),
			MouseFilter = MouseFilterEnum.Pass,
			// Any text makes Godot ask for the tooltip; _MakeCustomTooltip draws the real one.
			TooltipText = ConditionArt.Name(condition),
		};
		if (condition == null) icon.Material = PlayerToken.AggroMask;
		return icon;
	}

	// What a character carries now, Aggro first, then the conditions in their enum order.
	public static List<EncounterManager.StatusEffect?> Of(Player player, bool aggro) {
		List<EncounterManager.StatusEffect?> statuses = new List<EncounterManager.StatusEffect?>();
		if (player == null) return statuses;
		if (aggro) statuses.Add(null);
		foreach (EncounterManager.StatusEffect c in System.Enum.GetValues<EncounterManager.StatusEffect>()) {
			if (c != EncounterManager.StatusEffect.NONE && player.conditions.Contains(c)) statuses.Add(c);
		}
		return statuses;
	}

	// A key for polling: changes whenever the set of statuses does.
	public static int Mask(Player player, bool aggro) {
		int mask = aggro ? 1 << 30 : 0;
		if (player == null) return mask;
		foreach (EncounterManager.StatusEffect c in player.conditions) mask |= 1 << (int)c;
		return mask;
	}

	public override GodotObject _MakeCustomTooltip(string forText) {
		titleFont ??= ResourceLoader.Exists("res://OptimusPrinceps.ttf") ? GD.Load<Font>("res://OptimusPrinceps.ttf") : null;

		PanelContainer panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
			BgColor = new Color(0.06f, 0.05f, 0.04f, 0.94f),
			BorderColor = new Color(0.62f, 0.5f, 0.3f),
			BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
			ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 7, ContentMarginBottom = 8,
		});
		VBoxContainer column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 4);
		panel.AddChild(column);

		HBoxContainer title = new HBoxContainer();
		title.AddThemeConstantOverride("separation", 6);
		TextureRect icon = new TextureRect {
			Texture = Texture, Material = Material,
			CustomMinimumSize = new Vector2(22, 22),
			ExpandMode = ExpandModeEnum.IgnoreSize,
			StretchMode = StretchModeEnum.KeepAspectCentered,
		};
		title.AddChild(icon);
		Label name = new Label { Text = ConditionArt.Name(condition), VerticalAlignment = VerticalAlignment.Center };
		name.AddThemeColorOverride("font_color", GOLD);
		name.AddThemeFontSizeOverride("font_size", 17);
		if (titleFont != null) name.AddThemeFontOverride("font", titleFont);
		title.AddChild(name);
		column.AddChild(title);

		Label description = new Label {
			Text = ConditionArt.Description(condition),
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(240, 0),
		};
		description.AddThemeColorOverride("font_color", TEXT);
		description.AddThemeFontSizeOverride("font_size", 13);
		column.AddChild(description);
		return panel;
	}
}
