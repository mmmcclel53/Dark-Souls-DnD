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
			EncounterManager.StatusEffect.ABYSS => "Abyss",
			EncounterManager.StatusEffect.CALAMITY => "Calamity",
			EncounterManager.StatusEffect.CORROSION => "Corrosion",
			EncounterManager.StatusEffect.CURSE => "Curse",
			_ => null,
		};
		Texture2D texture = file == null ? null : GD.Load<Texture2D>($"res://Resources/Images/Sprites/Statuses/{file}.png");
		icons[condition] = texture;
		return texture;
	}

	// For a status's tooltip (StatusIcon). Null is the Aggro token.
	public static string Name(EncounterManager.StatusEffect? condition) => condition switch {
		null => "Aggro",
		EncounterManager.StatusEffect.BLEED => "Bleed",
		EncounterManager.StatusEffect.POISON => "Poison",
		EncounterManager.StatusEffect.FROST => "Frostbite",
		EncounterManager.StatusEffect.STAGGER => "Stagger",
		EncounterManager.StatusEffect.ABYSS => "Abyss",
		EncounterManager.StatusEffect.CALAMITY => "Calamity",
		EncounterManager.StatusEffect.CORROSION => "Corrosion",
		EncounterManager.StatusEffect.CURSE => "Curse",
		_ => "",
	};

	public static string Description(EncounterManager.StatusEffect? condition) => condition switch {
		null => "Enemies that hunt the Aggro token come for this character first.",
		EncounterManager.StatusEffect.BLEED => "The next damage suffered is 2 more, then Bleed comes off.",
		EncounterManager.StatusEffect.POISON => "Suffer 1 damage at the end of this activation, then Poison comes off.",
		EncounterManager.StatusEffect.FROST => "Walking, running and dodging cost 1 more stamina. Comes off at the end of this activation.",
		EncounterManager.StatusEffect.STAGGER => "Weapon attacks cost 1 more stamina. Comes off at the end of this activation.",
		EncounterManager.StatusEffect.ABYSS => "Suffer 2 damage at the end of this activation, then Abyss comes off.",
		EncounterManager.StatusEffect.CALAMITY => "-1 on Block and Resist rolls, and dodging needs one more success. Comes off when an attack deals damage.",
		EncounterManager.StatusEffect.CORROSION => "-1 on Block rolls. Comes off when an attack deals damage.",
		EncounterManager.StatusEffect.CURSE => "Suffer 1 damage at the end of every activation, and nothing heals. Only resting at a bonfire lifts it.",
		_ => "",
	};

	public static Color Colour(EncounterManager.StatusEffect condition) => condition switch {
		EncounterManager.StatusEffect.BLEED => new Color(0.85f, 0.18f, 0.12f),
		EncounterManager.StatusEffect.POISON => new Color(0.35f, 0.8f, 0.3f),
		EncounterManager.StatusEffect.FROST => new Color(0.45f, 0.75f, 1f),
		EncounterManager.StatusEffect.STAGGER => new Color(0.95f, 0.8f, 0.3f),
		EncounterManager.StatusEffect.ABYSS => new Color(0.3f, 0.45f, 1f),
		EncounterManager.StatusEffect.CALAMITY => new Color(1f, 0.7f, 0.2f),
		EncounterManager.StatusEffect.CORROSION => new Color(0.6f, 0.9f, 0.2f),
		EncounterManager.StatusEffect.CURSE => new Color(0.75f, 0.68f, 0.55f),
		_ => Colors.White,
	};
}
