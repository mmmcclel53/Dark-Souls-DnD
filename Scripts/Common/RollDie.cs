using Godot;

// One die in the roll reveal: a big square in the die's colour that shows whichever face
// it is told to, tumbling through random faces before it lands on the real one. Dodge dice
// show the dodge icon or nothing, matching the physical die's icon-or-blank faces.
public partial class RollDie : Control
{

	public DiceUtility.DICE_TYPE type = DiceUtility.DICE_TYPE.BLACK;
	public int value;
	public int[] faces = { 0 };
	public Texture2D dodgeIcon;

	public bool settled { get; private set; }

	private int shown;

	public static RollDie Create(RollReveal.Face face, Texture2D dodgeIcon, float side) {
		RollDie die = new RollDie {
			type = face.type,
			value = face.value,
			faces = face.faces != null && face.faces.Length > 0 ? face.faces : new[] { face.value },
			dodgeIcon = dodgeIcon,
			CustomMinimumSize = new Vector2(side, side),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		die.shown = die.faces[0];
		return die;
	}

	public void ShowFace(int face) {
		shown = face;
		QueueRedraw();
	}

	public int RandomFace() => faces[System.Random.Shared.Next(faces.Length)];

	public void Settle() {
		settled = true;
		ShowFace(value);

		PivotOffset = Size * 0.5f;
		Scale = new Vector2(1.3f, 1.3f);
		Tween tween = CreateTween();
		tween.TweenProperty(this, "scale", Vector2.One, 0.22).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}

	public override void _Draw() {
		(Color fill, Color edge, Color text) = Colours(type);

		StyleBoxFlat box = new StyleBoxFlat { BgColor = fill, BorderColor = settled ? new Color(0.95f, 0.8f, 0.45f) : edge, AntiAliasing = true };
		box.SetBorderWidthAll(settled ? 2 : 1);
		box.SetCornerRadiusAll(Mathf.RoundToInt(Size.Y * 0.18f));
		DrawStyleBox(box, new Rect2(Vector2.Zero, Size));

		if (type == DiceUtility.DICE_TYPE.DODGE) {
			if (shown >= 1 && dodgeIcon != null) {
				float inset = Size.X * 0.18f;
				DrawTextureRect(dodgeIcon, new Rect2(inset, inset, Size.X - inset * 2f, Size.Y - inset * 2f), false, text);
			}
			return;
		}

		Font font = GetThemeDefaultFont();
		int fontSize = Mathf.RoundToInt(Size.Y * 0.55f);
		float baseline = (Size.Y + font.GetAscent(fontSize) - font.GetDescent(fontSize)) * 0.5f;
		DrawString(font, new Vector2(0, baseline), shown.ToString(), HorizontalAlignment.Center, Size.X, fontSize, text);
	}

	private static (Color fill, Color edge, Color text) Colours(DiceUtility.DICE_TYPE type) => type switch {
		DiceUtility.DICE_TYPE.BLUE => (new Color(0.20f, 0.40f, 0.78f), new Color(0.55f, 0.70f, 0.95f), Colors.White),
		DiceUtility.DICE_TYPE.ORANGE => (new Color(0.90f, 0.52f, 0.13f), new Color(1f, 0.78f, 0.45f), new Color(0.12f, 0.08f, 0.04f)),
		DiceUtility.DICE_TYPE.DODGE => (new Color(0.30f, 0.62f, 0.30f), new Color(0.60f, 0.85f, 0.55f), Colors.White),
		_ => (new Color(0.12f, 0.11f, 0.10f), new Color(0.55f, 0.50f, 0.42f), Colors.White),
	};
}
