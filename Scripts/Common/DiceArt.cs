using Godot;
using System.Collections.Generic;

// The printed faces of the four dice: one sheet per die under Resources/Images/Sprites/Dice,
// 3x2 cells of 100px cut by Tools/DiceFaces/generate.py from photos of the real dice.
// Each cell's pip count is listed here in reading order; a rolled value picks one of the
// cells showing that many, so a 2 on the orange die is either of its two-sword faces. The
// six cells are the die's six sides, so a cube wearing all of them is the real die.
public static class DiceArt
{
	public const int CELL = 100;
	public const int CELLS = 6;

	private static readonly Dictionary<DiceUtility.DICE_TYPE, Texture2D> sheets = new Dictionary<DiceUtility.DICE_TYPE, Texture2D>();

	private static readonly Dictionary<DiceUtility.DICE_TYPE, int[]> pips = new Dictionary<DiceUtility.DICE_TYPE, int[]> {
		{ DiceUtility.DICE_TYPE.BLACK, new[] { 1, 1, 1, 2, 2, 0 } },
		{ DiceUtility.DICE_TYPE.BLUE, new[] { 1, 1, 2, 2, 2, 3 } },
		{ DiceUtility.DICE_TYPE.ORANGE, new[] { 1, 2, 2, 3, 3, 4 } },
		{ DiceUtility.DICE_TYPE.DODGE, new[] { 1, 1, 1, 0, 0, 0 } },
	};

	public static Texture2D Sheet(DiceUtility.DICE_TYPE type) {
		if (sheets.TryGetValue(type, out Texture2D cached)) return cached;
		string file = type switch {
			DiceUtility.DICE_TYPE.BLUE => "Blue",
			DiceUtility.DICE_TYPE.ORANGE => "Orange",
			DiceUtility.DICE_TYPE.DODGE => "Green",
			_ => "Black",
		};
		string path = $"res://Resources/Images/Sprites/Dice/{file}.png";
		Texture2D sheet = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
		sheets[type] = sheet;
		return sheet;
	}

	// A cell showing `value` pips, chosen at random among those that do; -1 when none does.
	public static int CellFor(DiceUtility.DICE_TYPE type, int value) {
		if (!pips.TryGetValue(type, out int[] counts)) return -1;
		List<int> matching = new List<int>();
		for (int i = 0; i < counts.Length; i++) {
			if (counts[i] == value) matching.Add(i);
		}
		return matching.Count == 0 ? -1 : matching[GD.RandRange(0, matching.Count - 1)];
	}

	public static int RandomCell() => GD.RandRange(0, CELLS - 1);

	public static Rect2 CellRegion(int cell) =>
		new Rect2((cell % 3) * CELL, (cell / 3) * CELL, CELL, CELL);

	// The cell's corners as texture coordinates on its sheet, top-left first, clockwise.
	public static Vector2[] CellUvs(int cell, Vector2 sheetSize) {
		Rect2 region = CellRegion(cell);
		Vector2[] corners = { Vector2.Zero, Vector2.Right, Vector2.One, Vector2.Down };
		Vector2[] uvs = new Vector2[4];
		for (int i = 0; i < 4; i++) uvs[i] = (region.Position + corners[i] * region.Size) / sheetSize;
		return uvs;
	}
}
