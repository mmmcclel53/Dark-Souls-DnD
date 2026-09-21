using Godot;
using System.Threading.Tasks;

// The bonfire rest transition, in the spirit of the games: the edges of the world fall away,
// the fire flares in two beats like a heart, embers lift, the screen burns out to black, and
// "Rested" settles onto a dark band that slowly grows while it holds. Then the black lifts on
// a dying glow.
//
// Deliberately two halves rather than one call with a callback — the caller does its work
// between them, which is the whole point of covering the screen. Endurance bars refilling
// and encounters reappearing should happen unseen, not pop while the player is looking.
//
// Everything degrades to an instant rest if the parts are not wired, so the bonfire still
// works without it.
public partial class BonfireRestAnimation : Control {

	[Export] public ColorRect vignette;
	[Export] public TextureRect glow;
	[Export] public CpuParticles2D embers;
	[Export] public ColorRect fade;
	[Export] public Control banner;
	[Export] public Control band;
	[Export] public Label message;

	[Export] public string messageText = "Rested";

	[Export] public float flareSeconds = 1.0f;
	[Export] public float burnSeconds = 0.6f;
	[Export] public float bannerInSeconds = 0.5f;
	[Export] public float holdSeconds = 1.3f;
	[Export] public float bannerOutSeconds = 0.35f;
	[Export] public float returnSeconds = 0.8f;

	[Export] public float glowStartScale = 0.35f;
	[Export] public float glowPeakScale = 1.9f;
	[Export] public float vignetteOpen = 1.7f;
	[Export] public float vignetteClosed = 0.5f;
	// The band and its glow creep up in size the whole time the banner is on screen, as the
	// games' banners do. The words themselves stay still: a Label under a slowly changing
	// scale re-samples its glyphs every frame and shimmers.
	[Export] public float bannerStartScale = 0.94f;
	[Export] public float bannerEndScale = 1.03f;

	private const string VIGNETTE_RADIUS = "shader_parameter/radius";

	private Tween tween;
	private Tween flicker;

	private bool wired => vignette != null && glow != null && embers != null && fade != null
		&& banner != null && band != null && message != null;

	public override void _Ready() {
		Visible = false;
		Clear();
	}

	// Ends with the screen fully black, so the caller can change the world unseen.
	public async Task FadeOut() {
		if (!wired) return;

		Visible = true;
		Clear();
		// A Control pivots on its top-left corner; the flare and the band both have to grow
		// out of their own middle.
		glow.PivotOffset = glow.Size / 2f;
		band.PivotOffset = band.Size / 2f;
		band.Scale = Vector2.One * bannerStartScale;
		message.Text = messageText;

		PlaceEmbers();
		embers.Emitting = true;
		embers.Restart();

		float burnStart = flareSeconds;
		float bannerStart = flareSeconds + burnSeconds * 0.7f;

		tween = CreateTween().SetParallel(true);

		tween.TweenProperty(VignetteMaterial(), VIGNETTE_RADIUS, vignetteClosed, flareSeconds + burnSeconds)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

		// Two beats: a quick swell, a slight ebb, then the big bloom that carries into the burn.
		float beat = flareSeconds * 0.45f;
		float ebb = flareSeconds * 0.2f;
		float bloom = flareSeconds - beat - ebb + burnSeconds;
		Alpha(tween, glow, 0.7f, beat).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		Scale(tween, glow, glowPeakScale * 0.55f, beat).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		Scale(tween, glow, glowPeakScale * 0.45f, ebb).SetDelay(beat)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		Scale(tween, glow, glowPeakScale, bloom).SetDelay(beat + ebb)
			.SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
		Alpha(tween, glow, 1f, bloom).SetDelay(beat + ebb);

		Alpha(tween, fade, 1f, burnSeconds).SetDelay(burnStart)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);

		Alpha(tween, banner, 1f, bannerInSeconds).SetDelay(bannerStart)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		Scale(tween, band, bannerEndScale, bannerInSeconds + holdSeconds + bannerOutSeconds).SetDelay(bannerStart)
			.SetTrans(Tween.TransitionType.Linear);

		StartFlicker();

		await Wait(bannerStart + bannerInSeconds + holdSeconds);
	}

	public async Task FadeIn() {
		if (!wired) return;

		embers.Emitting = false;
		StopFlicker();

		// The black lifts on the last of the glow, so the return is warm rather than a cut.
		glow.Modulate = WithAlpha(glow.Modulate, 0.55f);
		glow.Scale = Vector2.One * glowPeakScale * 0.8f;

		Tween returning = CreateTween().SetParallel(true);
		Alpha(returning, banner, 0f, bannerOutSeconds);
		Alpha(returning, fade, 0f, returnSeconds).SetDelay(bannerOutSeconds * 0.6f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		Alpha(returning, glow, 0f, returnSeconds).SetDelay(bannerOutSeconds * 0.6f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
		returning.TweenProperty(VignetteMaterial(), VIGNETTE_RADIUS, vignetteOpen, returnSeconds)
			.SetDelay(bannerOutSeconds * 0.6f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);

		await ToSignal(returning, Tween.SignalName.Finished);
		Visible = false;
		Clear();
	}

	// The particles are a Node2D inside a Control, so nothing anchors them: they sit along the
	// bottom edge and drift up through the whole screen.
	private void PlaceEmbers() {
		Vector2 size = Size;
		embers.Position = new Vector2(size.X / 2f, size.Y + 12f);
		embers.EmissionRectExtents = new Vector2(size.X * 0.48f, 10f);
	}

	// A small, uneven pulse on the flare so it reads as fire rather than a lamp.
	private void StartFlicker() {
		StopFlicker();
		flicker = CreateTween().SetLoops();
		flicker.TweenProperty(glow, "modulate:a", -0.08f, 0.09f).AsRelative().SetTrans(Tween.TransitionType.Sine);
		flicker.TweenProperty(glow, "modulate:a", 0.08f, 0.13f).AsRelative().SetTrans(Tween.TransitionType.Sine);
		flicker.TweenProperty(glow, "modulate:a", -0.05f, 0.07f).AsRelative();
		flicker.TweenProperty(glow, "modulate:a", 0.05f, 0.11f).AsRelative();
	}

	private void StopFlicker() {
		if (flicker != null && flicker.IsValid()) flicker.Kill();
		flicker = null;
	}

	private void Clear() {
		if (!wired) return;
		if (tween != null && tween.IsValid()) tween.Kill();
		tween = null;
		StopFlicker();

		glow.Modulate = WithAlpha(glow.Modulate, 0f);
		fade.Modulate = WithAlpha(fade.Modulate, 0f);
		banner.Modulate = WithAlpha(banner.Modulate, 0f);
		glow.Scale = Vector2.One * glowStartScale;
		band.Scale = Vector2.One;
		embers.Emitting = false;
		VignetteMaterial()?.SetShaderParameter("radius", vignetteOpen);
	}

	private ShaderMaterial VignetteMaterial() => vignette.Material as ShaderMaterial;

	private async Task Wait(float seconds) =>
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

	private static Color WithAlpha(Color colour, float alpha) => new Color(colour.R, colour.G, colour.B, alpha);

	private static PropertyTweener Alpha(Tween tween, CanvasItem target, float alpha, float seconds) =>
		tween.TweenProperty(target, "modulate:a", alpha, seconds);

	private static PropertyTweener Scale(Tween tween, Control target, float scale, float seconds) =>
		tween.TweenProperty(target, "scale", Vector2.One * scale, seconds);
}
