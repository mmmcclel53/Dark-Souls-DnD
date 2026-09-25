using Godot;
using System.Collections.Generic;

// A die type and how many of them: a rounded square in the die's colour with the count in
// the middle. Stands in for "1B 1U" wherever a dice pool is shown.
public partial class DiceChip : Control
{

	public DiceUtility.DICE_TYPE diceType = DiceUtility.DICE_TYPE.BLACK;
	public int count = 1;
	public float side = 20f;
	public int fontSize = 12;

	public static DiceChip Create(DiceUtility.DICE_TYPE type, int count, float side = 20f) {
		return new DiceChip {
			diceType = type,
			count = count,
			side = side,
			fontSize = Mathf.RoundToInt(side * 0.6f),
			CustomMinimumSize = new Vector2(side, side),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore,
		};
	}

	// One chip per die type in the pool, in BLACK, BLUE, ORANGE order.
	public static List<DiceChip> ForPool(IEnumerable<Dice> pool, float side = 20f) {
		SortedDictionary<DiceUtility.DICE_TYPE, int> counts = new SortedDictionary<DiceUtility.DICE_TYPE, int>();
		foreach (Dice die in pool) {
			if (die == null) continue;
			counts.TryGetValue(die.diceType, out int n);
			counts[die.diceType] = n + 1;
		}

		List<DiceChip> chips = new List<DiceChip>();
		foreach (var pair in counts) chips.Add(Create(pair.Key, pair.Value, side));
		return chips;
	}

	public override void _Draw() {
		(Color fill, Color edge, Color text) = Colours(diceType);

		StyleBoxFlat box = new StyleBoxFlat {
			BgColor = fill,
			BorderColor = edge,
			AntiAliasing = true,
		};
		box.SetBorderWidthAll(1);
		box.SetCornerRadiusAll(Mathf.RoundToInt(Size.Y * 0.22f));
		DrawStyleBox(box, new Rect2(Vector2.Zero, Size));

		Font font = GetThemeDefaultFont();
		string label = count.ToString();
		float ascent = font.GetAscent(fontSize);
		float descent = font.GetDescent(fontSize);
		float baseline = (Size.Y + ascent - descent) * 0.5f;
		DrawString(font, new Vector2(0, baseline), label, HorizontalAlignment.Center, Size.X, fontSize, text);
	}

	private static (Color fill, Color edge, Color text) Colours(DiceUtility.DICE_TYPE type) => type switch {
		DiceUtility.DICE_TYPE.BLUE => (new Color(0.20f, 0.40f, 0.78f), new Color(0.55f, 0.70f, 0.95f), Colors.White),
		DiceUtility.DICE_TYPE.ORANGE => (new Color(0.90f, 0.52f, 0.13f), new Color(1f, 0.78f, 0.45f), new Color(0.12f, 0.08f, 0.04f)),
		DiceUtility.DICE_TYPE.DODGE => (new Color(0.30f, 0.62f, 0.30f), new Color(0.60f, 0.85f, 0.55f), Colors.White),
		_ => (new Color(0.12f, 0.11f, 0.10f), new Color(0.55f, 0.50f, 0.42f), Colors.White),
	};
}
