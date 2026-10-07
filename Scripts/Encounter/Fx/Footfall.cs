using Godot;
using System.Collections.Generic;

// A heavy enemy's step landing: a low ring of dust spreading from under it, a few motes
// kicked out and settling, and a small jolt of the board that grows with its size. Nothing
// for a normal token; it starts at ×1.5 presence (5 health).
public partial class Footfall : Node2D
{
	public const float MIN_PRESENCE = 1.5f;

	public float seconds = 0.6f;
	public Color dust = new Color(0.62f, 0.56f, 0.47f);

	private float r;
	private float weight;
	private float age;
	private readonly List<(Vector2 dir, float speed, float size)> motes = new List<(Vector2, float, float)>();

	public static void Land(Node2D wrapper) {
		if (wrapper == null || !IsInstanceValid(wrapper) || !wrapper.IsInsideTree()) return;
		float presence = EncounterManager.Presence(wrapper);
		if (presence < MIN_PRESENCE || wrapper.GetChildOrNull<Control>(0) is not Control token) return;

		float weight = TokenMotion.Weight(wrapper);
		BoardFx.camera?.Shake(1f + 2.5f * weight, 0.12f + 0.08f * weight);

		Footfall effect = BoardFx.Spawn(new Footfall { weight = weight });
		if (effect == null) return;
		effect.GlobalPosition = BoardFx.GlobalCentre(token);
		effect.r = BoardFx.LocalRadius(effect, token);
		int count = 5 + Mathf.RoundToInt(4f * weight);
		for (int i = 0; i < count; i++) {
			float angle = Mathf.Tau * (i + GD.Randf() * 0.6f) / count;
			effect.motes.Add((Vector2.FromAngle(angle), 0.35f + GD.Randf() * 0.35f, 0.035f + GD.Randf() * 0.03f));
		}
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
		float t = age / seconds;
		float fade = 1f - t;
		float ease = 1f - (1f - t) * (1f - t);

		// Low and wide: squashed vertically so it reads as lying on the floor.
		DrawSetTransform(Vector2.Zero, 0f, new Vector2(1f, 0.82f));
		float ring = r * (0.92f + 0.55f * ease);
		float alpha = (0.3f + 0.25f * weight) * fade * fade;
		DrawArc(Vector2.Zero, ring, 0f, Mathf.Tau, 48, new Color(dust, alpha), r * (0.16f - 0.08f * t), true);
		DrawArc(Vector2.Zero, ring * 1.08f, 0f, Mathf.Tau, 48, new Color(dust, alpha * 0.45f), r * 0.07f, true);

		foreach (var (dir, speed, size) in motes) {
			Vector2 at = dir * r * (0.95f + speed * ease);
			DrawCircle(at, r * size * (1f + 0.6f * t), new Color(dust, 0.5f * fade));
		}
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}
}
