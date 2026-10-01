using Godot;

// A heavy hit on a character, felt through the screen: the board jolts and a red vignette
// flashes in from the edges and fades, the games' "you were hit" idiom. The vignette is the
// bonfire's shader tinted red and held wide, so only the edges ever colour. Built by the
// encounter scene after Reset; it never takes input.
public partial class ScreenPunch : CanvasLayer
{
	private const string SHADER_PATH = "res://Resources/Shaders/Vignette.gdshader";

	public float flashSeconds = 0.55f;
	public float shakeSeconds = 0.35f;
	public Color tint = new Color(0.6f, 0.05f, 0.02f, 0.9f);

	private ColorRect vignette;
	private Tween flash;

	public override void _Ready() {
		Layer = 11;     // with the phase banner: over the HUD, under the roll reveal

		ShaderMaterial material = new ShaderMaterial { Shader = GD.Load<Shader>(SHADER_PATH) };
		material.SetShaderParameter("radius", 1.55f);
		material.SetShaderParameter("softness", 0.95f);
		material.SetShaderParameter("colour", tint);

		vignette = new ColorRect { Material = material, MouseFilter = Control.MouseFilterEnum.Ignore };
		vignette.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		vignette.Modulate = new Color(1f, 1f, 1f, 0f);
		AddChild(vignette);
	}

	// Damage of 3 is a punch; 5 and up is the full weight of it.
	public void Hit(int damage) {
		float strength = Mathf.Clamp((damage - 2f) / 3f, 0.35f, 1f);
		FlashEdges(0.45f + 0.55f * strength);
		BoardFx.camera?.Shake(4f + 6f * strength, shakeSeconds);
	}

	// The lighter version, for the moment the dice show what is coming: edges only, no jolt.
	public void Flash(int damage) {
		FlashEdges(0.25f + 0.08f * Mathf.Min(damage, 5));
	}

	private void FlashEdges(float alpha) {
		if (flash != null && flash.IsValid()) flash.Kill();
		vignette.Modulate = new Color(1f, 1f, 1f, alpha);
		flash = CreateTween();
		flash.TweenProperty(vignette, "modulate:a", 0f, flashSeconds).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
	}
}
