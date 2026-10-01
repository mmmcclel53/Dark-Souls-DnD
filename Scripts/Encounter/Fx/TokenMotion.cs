using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

// How pieces travel. A token's wrapper is placed by FixPositioning the instant it moves,
// and that stays true: the travel is done on the token's button, which is set back to
// where the wrapper was and eased to rest — lifted a little on the way, like a piece picked
// up and put down. Everyone re-packed by an arrival or a departure slides the same way;
// a pushed model lands with a stumble. The intermediate nodes of an enemy's walk are
// crossed without reparenting, so those steps move the wrapper itself.
public static class TokenMotion
{
	public const float STEP_SECONDS = 0.32f;
	private const float LIFT = 1.14f;

	private static readonly Dictionary<Control, Tween> travelling = new Dictionary<Control, Tween>();
	private static readonly Dictionary<Control, Tween> lifting = new Dictionary<Control, Tween>();

	// Where a token is drawn right now, mid-travel or not.
	public static Vector2 VisualPosition(Node2D wrapper) {
		Control button = wrapper.GetChildOrNull<Control>(0);
		return button != null ? button.GlobalPosition : wrapper.GlobalPosition;
	}

	// After the wrapper has been reparented or re-packed: travels from where it was drawn.
	public static void Glide(Node2D wrapper, Vector2 fromGlobal, bool lift, bool stumble = false) {
		if (wrapper == null || !GodotObject.IsInstanceValid(wrapper) || !wrapper.IsInsideTree()) return;
		Control button = wrapper.GetChildOrNull<Control>(0);
		if (button == null) return;

		Settle(button);
		Vector2 delta = wrapper.GetGlobalTransform().AffineInverse().BasisXform(fromGlobal - wrapper.GlobalPosition);
		if (delta.Length() < 0.01f) return;

		button.Position = delta;
		Tween tween = button.CreateTween();
		tween.TweenProperty(button, "position", Vector2.Zero, STEP_SECONDS)
			.SetTrans(stumble ? Tween.TransitionType.Back : Tween.TransitionType.Cubic)
			.SetEase(stumble ? Tween.EaseType.Out : Tween.EaseType.InOut);
		travelling[button] = tween;
		tween.Finished += () => travelling.Remove(button);

		if (lift) Lift(button, STEP_SECONDS);
	}

	// A step across a node the token does not stop on: the wrapper itself travels, so
	// nothing on the way is re-packed. Both ends are recomputed from their nodes each
	// frame, so a zoom mid-step does not throw it off.
	public static async Task Travel(Node2D wrapper, GameNode to, Vector2 localTarget) {
		if (wrapper == null || !GodotObject.IsInstanceValid(wrapper) || to == null) return;
		if (wrapper.GetParent() is not Control from) return;
		Vector2 fromLocal = wrapper.Position;

		Tween tween = wrapper.CreateTween();
		tween.TweenMethod(Callable.From<float>(t => {
			if (!GodotObject.IsInstanceValid(wrapper) || !GodotObject.IsInstanceValid(from) || !GodotObject.IsInstanceValid(to)) return;
			Vector2 a = from.GetGlobalTransform() * fromLocal;
			Vector2 b = to.GetGlobalTransform() * localTarget;
			wrapper.GlobalPosition = a.Lerp(b, t);
		}), 0f, 1f, STEP_SECONDS).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);

		Control button = wrapper.GetChildOrNull<Control>(0);
		if (button != null) Lift(button, STEP_SECONDS);
		await ToSignal(tween);
	}

	// Ends any travel on a token's button at once, for something that needs to own its offset.
	public static void Settle(Control button) {
		if (button == null || !GodotObject.IsInstanceValid(button)) return;
		Stop(travelling, button);
		Stop(lifting, button);
		button.Position = Vector2.Zero;
		button.Scale = Vector2.One;
	}

	private static void Stop(Dictionary<Control, Tween> running, Control button) {
		if (!running.TryGetValue(button, out Tween tween)) return;
		if (tween.IsValid()) tween.Kill();
		running.Remove(button);
	}

	// Up on the way out, down on the way in, about the token's centre.
	private static void Lift(Control button, float seconds) {
		Stop(lifting, button);
		button.PivotOffset = button.Size * 0.5f;
		button.Scale = Vector2.One;
		Tween tween = button.CreateTween();
		tween.TweenProperty(button, "scale", new Vector2(LIFT, LIFT), seconds * 0.5f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(button, "scale", Vector2.One, seconds * 0.5f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
		lifting[button] = tween;
		tween.Finished += () => lifting.Remove(button);
	}

	private static async Task ToSignal(Tween tween) {
		await tween.ToSignal(tween, Tween.SignalName.Finished);
	}
}
