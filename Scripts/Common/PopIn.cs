using Godot;

// A control arriving: starts large and settles to size with a little overshoot. For icons
// that appear in a row — a condition landing on a portrait, the Aggro badge landing on a
// token — where a plain visibility flip is too easy to miss.
public static class PopIn
{
	public static void Scale(Control control, float from = 1.6f, float seconds = 0.3f) {
		if (control == null || !GodotObject.IsInstanceValid(control)) return;

		Vector2 size = control.Size.X > 0f ? control.Size : control.CustomMinimumSize;
		control.PivotOffset = size * 0.5f;
		control.Scale = new Vector2(from, from);
		Tween tween = control.CreateTween();
		tween.TweenProperty(control, "scale", Vector2.One, seconds).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}
}
