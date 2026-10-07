using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

// The stroke an attack makes on the board, in two awaitable halves. Windup brings the
// attack to the brink and holds it there, quivering, with the attacker leaning in; Land
// lets it go and fires the impact — a burst and a shake for a hit, a ring for one that was
// blocked, a sidestep for a dodge.
//
// It takes one of three forms, chosen from the attack's data by FormFor:
//   BLADE  a sword swung on an arc through the target, trailing a crescent of light; the
//          hold is the blade stopped just short of the target.
//   ARROW  nocked at the archer and drawn back, with a faint aim line to the target; the
//          hold is the aim, and Land looses it down the line.
//   ORB    a spell gathering in the caster's hand; the hold is the charge, and Land sends
//          it streaking to the target.
//
// The hold is the point. An enemy's attack waits over the character through the Block-or-
// Dodge choice and the roll; a character's waits while their own roll is shown. Nothing
// lands before the outcome is known, and the impact fires in the same frame the outcome
// is applied.
//
// Lives under the board (a child of the Encounter control) so it zooms and pans with it,
// and draws additively so it glows over whatever it crosses. Frees itself when the stroke
// has faded. Token offsets go on the token's button, never its wrapper: FixPositioning
// owns the wrapper's position and would fight a tween there.
public partial class AttackSlash : Node2D
{
	public enum Style { CHARACTER, ENEMY }
	public enum Form { BLADE, ARROW, ORB }
	public enum Result { HIT, BLOCKED, DODGED }

	public readonly struct Blow {
		public readonly Control target;
		public readonly Result result;
		public readonly int damage;

		public Blow(Control target, Result result, int damage) {
			this.target = target;
			this.result = result;
			this.damage = damage;
		}
	}

	[Signal] public delegate void DoneEventHandler();

	public float windupSeconds = 0.3f;
	public float bladeContactSeconds = 0.06f;
	public float throughSeconds = 0.1f;
	public float fadeSeconds = 0.22f;
	public float impactSeconds = 0.4f;
	public float recoverSeconds = 0.3f;
	public float flightSpeed = 28f;   // token radii per second, for arrows and orbs
	public float spanDegrees = 130f;
	public float lunge = 0.18f;       // how far the attacker leans in, as a fraction of its token
	public float sidestep = 0.5f;     // how far a dodging defender hops, as a fraction of its token
	public float holdGap = 1.4f;      // token radii short of the target's centre the blade waits at
	public float focusZoom = 1.22f;   // the camera's slow creep in on an enemy's held attack
	public float focusSeconds = 2.5f;
	public float releaseSeconds = 0.45f;

	private static readonly Color BLOCK = new Color(0.4f, 0.62f, 0.96f);
	private static readonly Color RESIST = new Color(0.62f, 0.5f, 0.93f);
	private static readonly Color HIT_TINT = new Color(1f, 0.35f, 0.3f);

	private Control attacker;
	private Form form;
	private Style style;
	private Color edge;
	private Color core;
	private Vector2 focusGlobal;

	// Geometry in board units. origin and contact are the two centres; the blade pivots at
	// pivot and its tip sweeps an arc of radius through contact, angles relative to the line.
	private Vector2 origin;
	private Vector2 contact;
	private Vector2 dir;
	private float distance;
	private float tokenRadius;
	private Vector2 pivot;
	private float radius;
	private float baseAngle;
	private float span;
	private float tip;
	private float holdTip;

	// Animation state. progress is the windup (the draw, the gather); flight is the
	// projectile's way to the target once released.
	private float alpha;
	private float progress;
	private float flight;
	private bool released;
	private float age;

	// Directions in the tokens' own space, for the lean and the hop.
	private Vector2 lungeDir;
	private Vector2 sideDir;
	private Tween attackerTween;
	private bool settled;

	private bool holding;
	private float holdTime;
	private bool finishing;
	private bool done;

	private class Impact {
		public Vector2 at;
		public float r;
		public Result result;
		public int damage;
		public float seed;
		public float elapsed;
	}
	private readonly List<Impact> impacts = new List<Impact>();

	// A spell is an orb whatever its range; a physical attack from two nodes away or more
	// is an arrow; anything in reach is a blade.
	public static Form FormFor(bool magic, int range) =>
		magic ? Form.ORB : range >= 2 ? Form.ARROW : Form.BLADE;

	// Null when there is no board to draw on, so callers null-check rather than await nothing.
	public static AttackSlash Begin(Control attacker, Control target, Style style, Form form) {
		Node board = EncounterManager.pathGrid?.GetParent();
		if (board == null || !IsInstanceValid(attacker) || !IsInstanceValid(target)) return null;

		AttackSlash slash = new AttackSlash {
			attacker = attacker,
			form = form,
			style = style,
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
		};
		(slash.edge, slash.core) = Colours(style, form == Form.ORB);
		board.AddChild(slash);
		slash.Aim(attacker, target);
		return slash;
	}

	private static (Color edge, Color core) Colours(Style style, bool magic) {
		if (magic) return (new Color(0.62f, 0.5f, 0.93f), new Color(0.9f, 0.85f, 1f));
		return style == Style.ENEMY
			? (new Color(1f, 0.42f, 0.25f), new Color(1f, 0.85f, 0.7f))
			: (new Color(1f, 0.86f, 0.55f), new Color(1f, 0.98f, 0.9f));
	}

	private void Aim(Control attacker, Control target) {
		Vector2 fromGlobal = GlobalCentre(attacker);
		Vector2 toGlobal = GlobalCentre(target);
		focusGlobal = (fromGlobal + toGlobal) * 0.5f;
		Vector2 dirGlobal = fromGlobal.DistanceTo(toGlobal) > 0.01f ? (toGlobal - fromGlobal).Normalized() : Vector2.Right;
		lungeDir = LocalDir(attacker, dirGlobal);
		sideDir = LocalDir(target, dirGlobal.Orthogonal() * (GD.Randf() < 0.5f ? 1f : -1f));

		Transform2D toLocal = GetGlobalTransform().AffineInverse();
		origin = toLocal * fromGlobal;
		contact = toLocal * toGlobal;
		tokenRadius = LocalRadius(target, toLocal);

		Vector2 delta = contact - origin;
		distance = delta.Length();
		dir = distance > 0.01f ? delta / distance : Vector2.Right;
		baseAngle = dir.Angle();
		span = Mathf.DegToRad(spanDegrees);

		// Adjacent tokens pivot the swing on the attacker. Closer than that the arc would be
		// too tight to read, further and it would be enormous, so both get a fixed reach.
		radius = Mathf.Clamp(distance, tokenRadius * 3f, tokenRadius * 4f);
		pivot = contact - dir * radius;
		holdTip = -2f * Mathf.Asin(Mathf.Min(1f, holdGap * tokenRadius / (2f * radius)));
		tip = -span * 0.5f;
		alpha = 0f;
	}

	// ----- The two halves -----

	// Raises the attack and carries it to the hold, where it stays until Land.
	public async Task Windup() {
		if (!IsInsideTree()) return;

		// The camera creeps in on an enemy's held attack; a character's hold is too short.
		if (style == Style.ENEMY) BoardFx.camera?.Focus(focusGlobal, focusZoom, focusSeconds);
		Nudge(form == Form.BLADE ? lunge : lunge * 0.5f, windupSeconds, Tween.EaseType.Out);

		Tween tween = CreateTween().SetParallel(true);
		if (form == Form.BLADE) {
			tween.TweenMethod(Callable.From<float>(SetTip), -span * 0.5f, holdTip, windupSeconds)
				.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		} else {
			tween.TweenMethod(Callable.From<float>(SetProgress), 0f, 1f, windupSeconds)
				.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		}
		tween.TweenMethod(Callable.From<float>(SetAlpha), 0f, 1f, windupSeconds * 0.5f);
		await ToSignal(tween, Tween.SignalName.Finished);

		holding = true;
		holdTime = 0f;
	}

	// Lets the attack go. Returns at the moment of contact, with the impacts fired, so the
	// caller can apply the outcome then; the follow-through and fade run on by themselves,
	// and Settle waits for them.
	public async Task Land(IEnumerable<Blow> blows) {
		if (!IsInsideTree()) return;
		holding = false;
		if (style == Style.ENEMY) BoardFx.camera?.Release(releaseSeconds);

		float toContact = form == Form.BLADE ? bladeContactSeconds : FlightSeconds();
		List<Blow> landed = new List<Blow>(blows);
		// A dodge starts as the attack comes in, timed to be clear of it at contact.
		foreach (Blow blow in landed) {
			if (blow.result == Result.DODGED && IsInstanceValid(blow.target)) Sidestep(blow.target, toContact);
		}

		Tween tween = CreateTween();
		if (form == Form.BLADE) {
			Nudge(lunge * 1.6f, toContact, Tween.EaseType.In);
			tween.TweenMethod(Callable.From<float>(SetTip), tip, 0f, toContact)
				.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		} else {
			// The shot is loosed: the attacker settles back while it flies.
			released = true;
			Nudge(0f, recoverSeconds, Tween.EaseType.Out);
			tween.TweenMethod(Callable.From<float>(SetFlight), 0f, 1f, toContact)
				.SetTrans(form == Form.ORB ? Tween.TransitionType.Quad : Tween.TransitionType.Linear)
				.SetEase(Tween.EaseType.In);
		}
		await ToSignal(tween, Tween.SignalName.Finished);
		if (!IsInsideTree()) return;

		foreach (Blow blow in landed) Strike(blow);
		if (form == Form.BLADE) Nudge(0f, recoverSeconds, Tween.EaseType.Out);
		FollowThrough();
	}

	public async Task Settle() {
		if (done || !IsInsideTree()) return;
		await ToSignal(this, SignalName.Done);
	}

	private float FlightSeconds() =>
		Mathf.Clamp(distance / (tokenRadius * flightSpeed), 0.1f, 0.3f);

	private async void FollowThrough() {
		Tween tween = CreateTween();
		if (form == Form.BLADE) {
			tween.TweenMethod(Callable.From<float>(SetTip), 0f, span * 0.5f, throughSeconds)
				.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
			tween.TweenMethod(Callable.From<float>(SetAlpha), 1f, 0f, fadeSeconds).SetEase(Tween.EaseType.In);
		} else {
			// The arrow stays in the target and the orb dies away where it burst.
			tween.TweenMethod(Callable.From<float>(SetAlpha), 1f, 0f, fadeSeconds + 0.1f).SetEase(Tween.EaseType.In);
		}
		await ToSignal(tween, Tween.SignalName.Finished);
		finishing = true;
	}

	private void SetTip(float value) {
		tip = value;
	}

	private void SetAlpha(float value) {
		alpha = value;
	}

	private void SetProgress(float value) {
		progress = value;
	}

	private void SetFlight(float value) {
		flight = value;
	}

	// ----- What the tokens do -----

	private void Nudge(float fraction, float seconds, Tween.EaseType ease) {
		if (!IsInstanceValid(attacker)) return;
		// A token still settling from a step gives its offset up to the attack.
		if (!settled) {
			TokenMotion.Settle(attacker);
			settled = true;
		}
		if (attackerTween != null && attackerTween.IsValid()) attackerTween.Kill();

		attackerTween = attacker.CreateTween();
		attackerTween.TweenProperty(attacker, "position", lungeDir * attacker.Size.X * fraction, seconds)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(ease);
	}

	private void Strike(Blow blow) {
		Control target = blow.target;
		if (!IsInstanceValid(target)) return;

		Transform2D toLocal = GetGlobalTransform().AffineInverse();
		impacts.Add(new Impact {
			at = toLocal * GlobalCentre(target),
			r = LocalRadius(target, toLocal),
			result = blow.result,
			damage = blow.damage,
			seed = GD.Randf() * Mathf.Tau,
		});

		switch (blow.result) {
			case Result.HIT:
				// A big attacker's blow rocks its target harder, as the square root of its size.
				float heft = attacker is Enemy enemy ? Mathf.Sqrt(enemy.Presence) : 1f;
				Shake(target, 0.1f * heft * Mathf.Clamp(0.6f + 0.2f * blow.damage, 0.6f, 1.6f));
				Tint(target, HIT_TINT, 0.45f);
				break;
			case Result.BLOCKED:
				Shake(target, 0.03f);
				break;
		}
	}

	private static void Shake(Control token, float fraction) {
		float amplitude = token.Size.X * fraction;
		Tween tween = token.CreateTween();
		const int STEPS = 6;
		for (int i = 0; i < STEPS; i++) {
			float decay = 1f - (float)i / STEPS;
			Vector2 offset = new Vector2(GD.Randf() - 0.5f, GD.Randf() - 0.5f) * 2f * amplitude * decay;
			tween.TweenProperty(token, "position", offset, 0.035);
		}
		tween.TweenProperty(token, "position", Vector2.Zero, 0.05);
	}

	private static void Tint(Control token, Color tint, float seconds) {
		Color before = token.Modulate;
		token.Modulate = tint;
		Tween tween = token.CreateTween();
		tween.TweenProperty(token, "modulate", before, seconds).SetEase(Tween.EaseType.Out);
	}

	private void Sidestep(Control token, float toContact) {
		Tween tween = token.CreateTween();
		tween.TweenProperty(token, "position", sideDir * token.Size.X * sidestep, Mathf.Max(0.06f, toContact))
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		tween.TweenInterval(0.12);
		tween.TweenProperty(token, "position", Vector2.Zero, 0.22)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
	}

	// ----- Geometry -----

	private static Vector2 GlobalCentre(Control token) => token.GetGlobalTransform() * (token.Size * 0.5f);

	// A node can be the target too (the Node icon); a token standing on it is half its width.
	private static float LocalRadius(Control token, Transform2D toLocal) {
		float half = token.Size.X * (token is GameNode ? 0.25f : 0.5f);
		Vector2 edgeGlobal = token.GetGlobalTransform().BasisXform(new Vector2(half, 0f));
		return toLocal.BasisXform(edgeGlobal).Length();
	}

	private static Vector2 LocalDir(Control token, Vector2 globalDir) =>
		token.GetGlobalTransform().AffineInverse().BasisXform(globalDir).Normalized();

	// ----- Lifetime -----

	public override void _Process(double delta) {
		float dt = (float)delta;
		age += dt;
		if (holding) holdTime += dt;

		for (int i = impacts.Count - 1; i >= 0; i--) {
			impacts[i].elapsed += dt;
			if (impacts[i].elapsed >= impactSeconds) impacts.RemoveAt(i);
		}

		if (finishing && impacts.Count == 0) {
			Finish();
			return;
		}
		QueueRedraw();
	}

	private void Finish() {
		if (done) return;
		done = true;
		EmitSignal(SignalName.Done);
		QueueFree();
	}

	// ----- Drawing -----

	public override void _Draw() {
		if (alpha > 0f) {
			switch (form) {
				case Form.BLADE: DrawBladeStroke(); break;
				case Form.ARROW: DrawArrow(); break;
				case Form.ORB: DrawOrb(); break;
			}
		}
		foreach (Impact impact in impacts) DrawImpact(impact);
	}

	// The sword at the tip of its arc, with the crescent it has cut behind it. Held, it
	// trembles and its light breathes.
	private void DrawBladeStroke() {
		float quiver = holding
			? (Mathf.Sin(holdTime * 55f) * 0.35f + Mathf.Sin(holdTime * 23f) * 0.25f) * Mathf.DegToRad(1f)
			: 0f;
		float pulse = holding ? 0.85f + 0.15f * Mathf.Sin(holdTime * 9f) : 1f;

		float tipAngle = baseAngle + tip + quiver;
		float tailAngle = Mathf.Max(baseAngle - span * 0.5f, tipAngle - span * 0.55f);
		if (tipAngle - tailAngle >= 0.02f) {
			float width = tokenRadius * 0.7f;
			DrawCrescent(tailAngle, tipAngle, width, new Color(edge, 0.55f * alpha * pulse));
			DrawCrescent(tailAngle, tipAngle, width * 0.38f, new Color(core, 0.9f * alpha * pulse));
		}
		DrawSword(tipAngle, alpha * pulse);
	}

	// Built as a run of quads rather than one polygon: each is convex, so nothing has to be
	// triangulated, and the per-vertex colours give a smooth fade along the length.
	private void DrawCrescent(float from, float to, float width, Color colour) {
		const int SEGMENTS = 28;
		Vector2 previousInner = Vector2.Zero;
		Vector2 previousOuter = Vector2.Zero;
		float previousAlpha = 0f;

		for (int i = 0; i <= SEGMENTS; i++) {
			float t = (float)i / SEGMENTS;
			float angle = Mathf.Lerp(from, to, t);
			float w = Mathf.Max(width * 0.04f, width * Mathf.Pow(Mathf.Sin(t * Mathf.Pi), 0.7f));
			Vector2 ray = Vector2.FromAngle(angle);
			Vector2 inner = pivot + ray * (radius - w * 0.5f);
			Vector2 outer = pivot + ray * (radius + w * 0.5f);
			float a = colour.A * Mathf.Pow(t, 1.3f);

			if (i > 0) {
				DrawPolygon(
					new[] { previousInner, previousOuter, outer, inner },
					new[] { new Color(colour, previousAlpha), new Color(colour, previousAlpha), new Color(colour, a), new Color(colour, a) });
			}
			previousInner = inner;
			previousOuter = outer;
			previousAlpha = a;
		}
	}

	// A sword lying along the ray from the pivot — the attacker's hand — to the tip of the
	// arc: a tapered blade with a brighter fuller, a guard, a grip and a pommel.
	private void DrawSword(float angle, float brightness) {
		Vector2 ray = Vector2.FromAngle(angle);
		Vector2 side = ray.Orthogonal();
		float r = tokenRadius;
		float bladeBase = radius - r * 1.7f;
		float point = radius + r * 0.15f;
		Color steel = new Color(edge, 0.8f * brightness);
		Color light = new Color(core, 0.9f * brightness);

		Vector2 At(float along) => pivot + ray * along;

		DrawPolygon(
			new[] { At(bladeBase) + side * r * 0.14f, At(point), At(bladeBase) - side * r * 0.14f },
			new[] { steel, steel, steel });
		DrawPolygon(
			new[] { At(bladeBase) + side * r * 0.05f, At(point - r * 0.12f), At(bladeBase) - side * r * 0.05f },
			new[] { light, light, light });
		DrawLine(At(bladeBase) + side * r * 0.42f, At(bladeBase) - side * r * 0.42f, steel, Mathf.Max(1f, r * 0.1f), true);
		DrawLine(At(bladeBase), At(bladeBase - r * 0.45f), steel, Mathf.Max(1f, r * 0.12f), true);
		DrawCircle(At(bladeBase - r * 0.5f), r * 0.09f, light);
	}

	// The arrow: nocked in the archer's hand and drawn back during the hold, with a faint
	// aim line to the target, then loosed down that line and left in the target.
	private void DrawArrow() {
		float r = tokenRadius;
		Vector2 side = dir.Orthogonal();
		float pulse = holding ? 0.8f + 0.2f * Mathf.Sin(holdTime * 8f) : 1f;
		float jitter = holding ? Mathf.Sin(holdTime * 47f) * r * 0.02f : 0f;

		float aim = alpha * pulse * (released ? 1f - flight : 1f) * 0.35f;
		if (aim > 0.01f && distance > r * 2.2f) {
			DrawDashedLine(origin + dir * r * 1.1f, contact - dir * r * 0.9f, new Color(edge, aim),
				Mathf.Max(1f, r * 0.04f), r * 0.25f, true, true);
		}

		float headAlong = released
			? Mathf.Lerp(r * 0.85f, distance - r * 0.35f, flight)
			: Mathf.Lerp(r * 1.2f, r * 0.85f, progress);
		Vector2 head = origin + dir * headAlong + side * jitter;
		Vector2 tail = head - dir * r * 1.4f;
		Color shaft = new Color(edge, 0.9f * alpha);
		Color headColour = new Color(core, 0.95f * alpha);

		if (released && flight < 1f) {
			DrawLine(tail - dir * r * 1.2f, tail, new Color(edge, 0.35f * alpha), Mathf.Max(1f, r * 0.06f), true);
		}
		DrawLine(tail, head - dir * r * 0.2f, shaft, Mathf.Max(1f, r * 0.07f), true);
		DrawPolygon(
			new[] { head, head - dir * r * 0.32f + side * r * 0.12f, head - dir * r * 0.32f - side * r * 0.12f },
			new[] { headColour, headColour, headColour });
		foreach (float s in new[] { 1f, -1f }) {
			DrawLine(tail + dir * r * 0.05f, tail - dir * r * 0.2f + side * s * r * 0.14f, shaft, Mathf.Max(1f, r * 0.06f), true);
			DrawLine(tail + dir * r * 0.3f, tail + dir * r * 0.05f + side * s * r * 0.14f, shaft, Mathf.Max(1f, r * 0.06f), true);
		}
	}

	// The orb: gathers in the caster's hand with sparks spiralling in, pulses through the
	// hold, then streaks to the target with a tail of light.
	private void DrawOrb() {
		float r = tokenRadius;
		float pulse = holding ? 1f + 0.1f * Mathf.Sin(holdTime * 7f) : 1f;
		float along = released ? Mathf.Lerp(r * 0.9f, distance, flight) : r * 0.9f;
		Vector2 centre = origin + dir * along;
		float size = r * 0.42f * progress * pulse;
		if (size <= 0f) return;

		if (released && flight < 1f) {
			const int TAIL = 4;
			for (int i = 0; i < TAIL; i++) {
				float t = (float)i / TAIL;
				Vector2 a = centre - dir * r * 1.6f * Mathf.Min(1f, flight * 3f) * (1f - t);
				Vector2 b = centre - dir * r * 1.6f * Mathf.Min(1f, flight * 3f) * (1f - (float)(i + 1) / TAIL);
				DrawLine(a, b, new Color(edge, 0.5f * alpha * t), Mathf.Max(1f, size * 1.4f * t), true);
			}
		}

		DrawCircle(centre, size * 2.2f, new Color(edge, 0.18f * alpha));
		DrawCircle(centre, size * 1.3f, new Color(edge, 0.45f * alpha));
		DrawCircle(centre, size, new Color(core, 0.95f * alpha));

		if (!released) {
			const int SPARKS = 4;
			float orbit = size * (2.8f - 1.2f * progress);
			for (int k = 0; k < SPARKS; k++) {
				Vector2 spark = centre + Vector2.FromAngle(age * 4f + k * Mathf.Tau / SPARKS) * orbit;
				DrawCircle(spark, r * 0.05f, new Color(core, 0.8f * alpha));
			}
		}
	}

	private void DrawImpact(Impact impact) {
		float u = Mathf.Clamp(impact.elapsed / impactSeconds, 0f, 1f);
		float fade = 1f - u;

		switch (impact.result) {
			case Result.HIT: {
				float scale = 1f + 0.12f * Mathf.Min(impact.damage, 6);
				DrawCircle(impact.at, impact.r * (1.1f + 0.5f * u) * scale, new Color(1f, 1f, 1f, fade * fade * 0.85f));
				const int STREAKS = 7;
				for (int k = 0; k < STREAKS; k++) {
					Vector2 ray = Vector2.FromAngle(impact.seed + k * Mathf.Tau / STREAKS);
					Vector2 a = impact.at + ray * impact.r * (0.5f + 1.4f * u) * scale;
					Vector2 b = impact.at + ray * impact.r * (0.9f + 2.2f * u) * scale;
					DrawLine(a, b, new Color(edge, fade), Mathf.Max(1f, impact.r * 0.09f * fade), true);
				}
				break;
			}
			case Result.BLOCKED: {
				Color colour = form == Form.ORB ? RESIST : BLOCK;
				DrawCircle(impact.at, impact.r * 1.1f, new Color(colour, fade * fade * 0.35f));
				DrawArc(impact.at, impact.r * (1.05f + 0.8f * u), 0f, Mathf.Tau, 48,
					new Color(colour, fade * 0.9f), Mathf.Max(1f, impact.r * 0.14f * fade), true);
				break;
			}
			case Result.DODGED:
				DrawArc(impact.at, impact.r * (1f + 0.3f * u), 0f, Mathf.Tau, 48,
					new Color(core, fade * 0.35f), Mathf.Max(1f, impact.r * 0.06f), true);
				break;
		}
	}
}
