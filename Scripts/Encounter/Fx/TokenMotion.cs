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
	// A heavy piece is not picked up so much as dragged: its own steps take longer and
	// barely lift. Weight is presence over normal, 0 for a normal token and 1 from ×2.5.
	private const float HEAVY_LIFT = 1.04f;
	private const float SLOW_PER_PRESENCE = 0.35f;

	public static float Weight(Node2D wrapper) => Mathf.Clamp((EncounterManager.Presence(wrapper) - 1f) / 1.5f, 0f, 1f);

	public static float StepSeconds(Node2D wrapper) =>
		STEP_SECONDS * (1f + SLOW_PER_PRESENCE * (EncounterManager.Presence(wrapper) - 1f));

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

		// The mover takes its own pace; anyone re-packed around it slides at the normal one.
		float seconds = lift ? StepSeconds(wrapper) : STEP_SECONDS;
		button.Position = delta;
		Tween tween = button.CreateTween();
		tween.TweenProperty(button, "position", Vector2.Zero, seconds)
			.SetTrans(stumble ? Tween.TransitionType.Back : Tween.TransitionType.Cubic)
			.SetEase(stumble ? Tween.EaseType.Out : Tween.EaseType.InOut);
		travelling[button] = tween;
		tween.Finished += () => travelling.Remove(button);

		if (lift) Lift(button, seconds, LiftFor(wrapper));
	}

	// A step across a node the token does not stop on: the wrapper itself travels, so
	// nothing on the way is re-packed. Both ends are recomputed from their nodes each
	// frame, so a zoom mid-step does not throw it off. It travels at the size it has alone,
	// centred, so a big enemy squeezed onto a crowded node grows back as it walks off.
	public static async Task Travel(Node2D wrapper, GameNode to) {
		if (wrapper == null || !GodotObject.IsInstanceValid(wrapper) || to == null) return;
		if (wrapper.GetParent() is not Control from) return;
		Vector2 fromLocal = wrapper.Position;
		float nodeSize = to.Size.X;
		float scale = nodeSize / (EncounterManager.TOKEN_ART_SIZE * 2f) * EncounterManager.Presence(wrapper);
		Vector2 localTarget = Vector2.One * (nodeSize - EncounterManager.TOKEN_ART_SIZE * scale) * 0.5f;
		float seconds = StepSeconds(wrapper);
		if (!Mathf.IsEqualApprox(wrapper.Scale.X, scale)) {
			wrapper.CreateTween().TweenProperty(wrapper, "scale", new Vector2(scale, scale), seconds);
		}

		Tween tween = wrapper.CreateTween();
		tween.TweenMethod(Callable.From<float>(t => {
			if (!GodotObject.IsInstanceValid(wrapper) || !GodotObject.IsInstanceValid(from) || !GodotObject.IsInstanceValid(to)) return;
			Vector2 a = from.GetGlobalTransform() * fromLocal;
			Vector2 b = to.GetGlobalTransform() * localTarget;
			wrapper.GlobalPosition = a.Lerp(b, t);
		}), 0f, 1f, seconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);

		Control button = wrapper.GetChildOrNull<Control>(0);
		if (button != null) Lift(button, seconds, LiftFor(wrapper));
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

	private static float LiftFor(Node2D wrapper) => Mathf.Lerp(LIFT, HEAVY_LIFT, Weight(wrapper));

	// Up on the way out, down on the way in, about the token's centre.
	private static void Lift(Control button, float seconds, float height) {
		Stop(lifting, button);
		button.PivotOffset = button.Size * 0.5f;
		button.Scale = Vector2.One;
		Tween tween = button.CreateTween();
		tween.TweenProperty(button, "scale", new Vector2(height, height), seconds * 0.5f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(button, "scale", Vector2.One, seconds * 0.5f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
		lifting[button] = tween;
		tween.Finished += () => lifting.Remove(button);
	}

	private static async Task ToSignal(Tween tween) {
		await tween.ToSignal(tween, Tween.SignalName.Finished);
	}
}
