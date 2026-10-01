using Godot;

// A condition taking hold: two rings in its colour spread from the token, one chasing the
// other, and the condition's icon lifts away from it a beat later.
public partial class ConditionBurst : Node2D
{
	public float seconds = 0.5f;

	private Vector2 at;
	private float r;
	private Color colour;
	private float age;

	public static void Play(Control token, EncounterManager.StatusEffect condition) {
		if (condition == EncounterManager.StatusEffect.NONE) return;
		if (token == null || !IsInstanceValid(token) || !token.IsInsideTree() || token.IsQueuedForDeletion()) return;

		ConditionBurst burst = BoardFx.Spawn(new ConditionBurst { colour = ConditionArt.Colour(condition), Material = BoardFx.Additive() });
		if (burst != null) {
			burst.at = burst.GetGlobalTransform().AffineInverse() * BoardFx.GlobalCentre(token);
			burst.r = BoardFx.LocalRadius(burst, token);
		}
		BoardFloat.Icon(token, ConditionArt.Icon(condition), Colors.White, 0.15f);
	}

	public override void _Process(double delta) {
		age += (float)delta;
		if (age >= seconds) {
			QueueFree();
			return;
		}
		QueueRedraw();
	}

	public override void _Draw() {
		float u = Mathf.Clamp(age / seconds, 0f, 1f);
		float fade = 1f - u;
		DrawCircle(at, r * 1.05f, new Color(colour, fade * fade * 0.3f));
		DrawArc(at, r * (1f + 1.0f * u), 0f, Mathf.Tau, 48, new Color(colour, fade * 0.9f), Mathf.Max(1f, r * 0.12f * fade), true);

		float chase = Mathf.Max(0f, u - 0.2f) / 0.8f;
		if (u > 0.2f) {
			DrawArc(at, r * (0.9f + 0.8f * chase), 0f, Mathf.Tau, 48, new Color(colour, (1f - chase) * 0.6f), Mathf.Max(1f, r * 0.08f * (1f - chase)), true);
		}
	}
}
