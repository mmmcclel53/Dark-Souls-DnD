using Godot;
using System.Threading.Tasks;

// The banner that opens each phase of a round, after Baldur's Gate 3's turn banners: a dark
// band across the screen with a glow pooled behind the words, which fades in, holds and
// fades away. The turn loop awaits it, so the phase only starts once the player has read it.
//
// The band is the same shader the bonfire's "Rested" banner uses, tinted per phase: red for
// the enemies, gold for the adventurers. It never takes input.
//
// The same band closes the encounter: "Victory" in gold, or the games' "You Died" in red,
// held longer and larger, the band growing slowly the whole time it is up. The words never
// scale — only the band's rect does — because a scaled Label re-samples its glyphs every
// frame and shimmers.
public partial class PhaseBanner : CanvasLayer
{
	private const string SHADER_PATH = "res://Resources/Shaders/Banner.gdshader";

	public float fadeInSeconds = 0.3f;
	public float holdSeconds = 0.9f;
	public float fadeOutSeconds = 0.5f;
	public float bandHeight = 120f;
	public int fontSize = 44;
	public Color textColour = new Color(0.84f, 0.8f, 0.72f);

	public float outcomeFadeInSeconds = 0.7f;
	public float outcomeHoldSeconds = 2.2f;
	public float outcomeFadeOutSeconds = 0.9f;
	public float outcomeBandHeight = 170f;
	public int outcomeFontSize = 66;
	public float outcomeCreep = 1.12f;

	private Control root;
	private ColorRect band;
	private Label title;
	private ShaderMaterial material;

	public override void _Ready() {
		Layer = 11;         // above the portrait pane and HUD, below the roll reveal (12)
		Build();
		root.Visible = false;
	}

	private void Build() {
		root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddChild(root);

		material = new ShaderMaterial { Shader = GD.Load<Shader>(SHADER_PATH) };
		material.SetShaderParameter("glow_strength", 0.4f);

		band = new ColorRect { Material = material, MouseFilter = Control.MouseFilterEnum.Ignore };
		band.AnchorLeft = 0f;
		band.AnchorRight = 1f;
		band.AnchorTop = band.AnchorBottom = 0.4f;
		band.OffsetTop = -bandHeight * 0.5f;
		band.OffsetBottom = bandHeight * 0.5f;
		root.AddChild(band);

		title = new Label {
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		title.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		title.AddThemeFontSizeOverride("font_size", 44);
		title.AddThemeColorOverride("font_color", new Color(0.84f, 0.8f, 0.72f));
		title.AddThemeConstantOverride("outline_size", 0);
		band.AddChild(title);
	}

	public Task Show(string text, Color glow) =>
		Play(text, glow, textColour, fontSize, bandHeight, fadeInSeconds, holdSeconds, fadeOutSeconds, 1f);

	public Task ShowOutcome(string text, Color glow, Color colour) =>
		Play(text, glow, colour, outcomeFontSize, outcomeBandHeight, outcomeFadeInSeconds, outcomeHoldSeconds, outcomeFadeOutSeconds, outcomeCreep);

	private async Task Play(string text, Color glow, Color colour, int size, float height, float fadeIn, float hold, float fadeOut, float creep) {
		if (root == null) return;

		title.Text = text;
		title.AddThemeFontSizeOverride("font_size", size);
		title.AddThemeColorOverride("font_color", colour);
		material.SetShaderParameter("glow_colour", glow);
		SetBandHeight(height);
		root.Modulate = new Color(1, 1, 1, 0);
		root.Visible = true;

		float total = fadeIn + hold + fadeOut;
		Tween tween = CreateTween().SetParallel(true);
		Tween fade = CreateTween();
		fade.TweenProperty(root, "modulate:a", 1f, fadeIn).SetEase(Tween.EaseType.Out);
		fade.TweenInterval(hold);
		fade.TweenProperty(root, "modulate:a", 0f, fadeOut).SetEase(Tween.EaseType.In);
		if (creep > 1f) tween.TweenMethod(Callable.From<float>(SetBandHeight), height, height * creep, total);
		await ToSignal(fade, Tween.SignalName.Finished);

		root.Visible = false;
	}

	private void SetBandHeight(float height) {
		band.OffsetTop = -height * 0.5f;
		band.OffsetBottom = height * 0.5f;
	}
}
