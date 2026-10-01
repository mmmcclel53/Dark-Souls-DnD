using Godot;

// The Aggro token changing hands: it lifts off the last holder, hops across the board and
// lands on the new one with a pop. Neither token wears the badge while it is in the air.
public partial class AggroHandoff : Node2D
{
	public float seconds = 0.45f;

	private static Texture2D art;

	private PlayerToken target;
	private Vector2 from;
	private Vector2 to;
	private float side;
	private float age;

	// False when there is nothing to fly between, so the caller shows the badge outright.
	public static bool Fly(PlayerToken previous, PlayerToken next) {
		if (previous == null || next == null || !IsInstanceValid(previous) || !IsInstanceValid(next)) return false;
		if (!previous.IsInsideTree() || !next.IsInsideTree()) return false;

		art ??= GD.Load<Texture2D>(PlayerToken.AGGRO_TEXTURE);
		AggroHandoff flight = BoardFx.Spawn(new AggroHandoff { target = next, Material = PlayerToken.AggroMask });
		if (flight == null) return false;

		Transform2D toLocal = flight.GetGlobalTransform().AffineInverse();
		flight.from = toLocal * previous.AggroBadgeCentreGlobal();
		flight.to = toLocal * next.AggroBadgeCentreGlobal();
		flight.side = toLocal.BasisXform(new Vector2(previous.AggroBadgeSizeGlobal(), 0f)).Length();
		return true;
	}

	public override void _Process(double delta) {
		age += (float)delta;
		if (age >= seconds) {
			if (IsInstanceValid(target)) {
				target.RefreshAggro();
				target.PopAggro();
			}
			QueueFree();
			return;
		}
		QueueRedraw();
	}

	public override void _Draw() {
		if (art == null) return;
		float t = Mathf.Clamp(age / seconds, 0f, 1f);
		float eased = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
		float hop = Mathf.Sin(t * Mathf.Pi);

		Vector2 at = from.Lerp(to, eased) + Vector2.Up * from.DistanceTo(to) * 0.22f * hop;
		float size = side * (1f + 0.35f * hop);
		DrawTextureRect(art, new Rect2(at - new Vector2(size, size) * 0.5f, new Vector2(size, size)), false);
	}
}
