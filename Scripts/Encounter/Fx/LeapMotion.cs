using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

// A leap (p29) is the boss coming down on a node, not a blow from across the board. TakeOff
// crouches, springs up and sails over to hang above the landing node, growing as it rises,
// with its shadow spreading on the floor there (GameNode.MarkLeap). It hovers through the
// Block-or-Dodge choice and the roll, and Drop brings it down: the slam the attack stroke's
// SLAM form waits on. Like every token motion it is done on the token's button, offset from
// the wrapper that still stands on the node it left; MovePlayer then reparents it onto the
// node it landed on, from where it is already drawn. The wrapper is lifted over the other
// tokens only while it is in the air.
public static class LeapMotion
{
	public const float RISE = 1.35f;
	private const float PEAK = 1.5f;
	private const float CROUCH_SECONDS = 0.12f;
	private const float FLIGHT_SECONDS = 0.6f;
	private const float DROP_SECONDS = 0.14f;
	private const int AIRBORNE_Z = 4;

	private class Flight {
		public Node2D wrapper;
		public GameNode landing;
		public Tween hover;
		public int z;
	}

	private static readonly Dictionary<Control, Flight> airborne = new Dictionary<Control, Flight>();

	public static bool IsAirborne(Control button) => button != null && airborne.ContainsKey(button);

	public static async Task TakeOff(Node2D wrapper, GameNode landing) {
		if (wrapper == null || !GodotObject.IsInstanceValid(wrapper) || !wrapper.IsInsideTree() || landing == null) return;
		if (wrapper.GetChildOrNull<Control>(0) is not Control button || IsAirborne(button)) return;

		TokenMotion.Settle(button);
		button.PivotOffset = button.Size * 0.5f;
		Flight flight = new Flight { wrapper = wrapper, landing = landing, z = wrapper.ZIndex };
		airborne[button] = flight;
		wrapper.ZIndex = AIRBORNE_Z;
		landing.MarkLeap(true);

		Vector2 landingCentre = landing.GetGlobalTransform() * (landing.Size * 0.5f);
		Vector2 above = wrapper.GetGlobalTransform().AffineInverse() * landingCentre - button.Size * 0.5f;

		Tween tween = button.CreateTween();
		tween.TweenProperty(button, "scale", Vector2.One * 0.9f, CROUCH_SECONDS).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(button, "position", above, FLIGHT_SECONDS).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		tween.Parallel().TweenProperty(button, "scale", Vector2.One * PEAK, FLIGHT_SECONDS * 0.5f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		tween.Chain().TweenProperty(button, "scale", Vector2.One * RISE, FLIGHT_SECONDS * 0.35f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		await button.ToSignal(tween, Tween.SignalName.Finished);
		if (!GodotObject.IsInstanceValid(button) || !IsAirborne(button)) return;

		// Hangs there, breathing a little, until it drops.
		flight.hover = button.CreateTween().SetLoops();
		flight.hover.TweenProperty(button, "scale", Vector2.One * (RISE + 0.05f), 0.5f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		flight.hover.TweenProperty(button, "scale", Vector2.One * RISE, 0.5f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
	}

	// Down onto the landing node; returns at the moment it hits the floor. Nothing if it is not
	// in the air (a second character struck by the same leap, or a leap that was never taken).
	public static async Task Drop(Node2D wrapper) {
		if (wrapper == null || !GodotObject.IsInstanceValid(wrapper)) return;
		if (wrapper.GetChildOrNull<Control>(0) is not Control button || !airborne.TryGetValue(button, out Flight flight)) return;
		airborne.Remove(button);
		if (flight.hover != null && flight.hover.IsValid()) flight.hover.Kill();

		if (button.IsInsideTree()) {
			Tween tween = button.CreateTween();
			tween.TweenProperty(button, "scale", Vector2.One, DROP_SECONDS).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
			await button.ToSignal(tween, Tween.SignalName.Finished);
		}
		if (GodotObject.IsInstanceValid(flight.landing)) flight.landing.MarkLeap(false);
		if (!GodotObject.IsInstanceValid(wrapper)) return;
		wrapper.ZIndex = flight.z;
		Footfall.Land(wrapper);
		BoardFx.camera?.Shake(5f, 0.3f);
	}
}
