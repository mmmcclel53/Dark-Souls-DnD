using Godot;
using System.Collections.Generic;

// The icon and the colour each condition is shown in on the board.
public static class ConditionArt
{
	private static readonly Dictionary<EncounterManager.StatusEffect, Texture2D> icons = new Dictionary<EncounterManager.StatusEffect, Texture2D>();

	public static Texture2D Icon(EncounterManager.StatusEffect condition) {
		if (icons.TryGetValue(condition, out Texture2D cached)) return cached;

		string file = condition switch {
			EncounterManager.StatusEffect.BLEED => "Bleed",
			EncounterManager.StatusEffect.POISON => "Poison",
			EncounterManager.StatusEffect.FROST => "Frost",
			EncounterManager.StatusEffect.STAGGER => "Stagger",
			_ => null,
		};
		Texture2D texture = file == null ? null : GD.Load<Texture2D>($"res://Resources/Images/Sprites/Statuses/{file}.png");
		icons[condition] = texture;
		return texture;
	}

	public static Color Colour(EncounterManager.StatusEffect condition) => condition switch {
		EncounterManager.StatusEffect.BLEED => new Color(0.85f, 0.18f, 0.12f),
		EncounterManager.StatusEffect.POISON => new Color(0.35f, 0.8f, 0.3f),
		EncounterManager.StatusEffect.FROST => new Color(0.45f, 0.75f, 1f),
		EncounterManager.StatusEffect.STAGGER => new Color(0.95f, 0.8f, 0.3f),
		_ => Colors.White,
	};
}
