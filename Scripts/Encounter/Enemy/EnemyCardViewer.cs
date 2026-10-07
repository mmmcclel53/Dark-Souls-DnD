using Godot;

// The printed data card, large, beside the board, for as long as an enemy's face in the
// Enemy Activation bar, or its token on the board, is hovered. It takes no input and dims nothing, so the hover stays
// on the face and the ringed token on the board stays in view.
public partial class EnemyCardViewer : CanvasLayer
{

	[Export] public Control overlay;
	[Export] public TextureRect cardImage;
	[Export] public float fadeTime = 0.12f;

	// Card height as a fraction of the screen; the width follows the card's own aspect.
	[Export] public float heightFraction = 0.45f;

	// The one in the encounter, for the board tokens, which have no reference to the bar.
	public static EnemyCardViewer current;

	private Tween fade;

	public override void _ExitTree() {
		if (current == this) current = null;
	}

	public override void _Ready() {
		current = this;
		if (overlay == null) return;
		overlay.Visible = false;
		overlay.MouseFilter = Control.MouseFilterEnum.Ignore;
	}

	public void ShowCard(EnemyData data) {
		if (overlay == null || data?.cardTexture == null) return;

		if (cardImage != null) {
			cardImage.Texture = data.cardTexture;
			Vector2 cardSize = data.cardTexture.GetSize();
			float height = overlay.GetViewportRect().Size.Y * heightFraction;
			cardImage.CustomMinimumSize = new Vector2(height * cardSize.X / cardSize.Y, height);
		}

		overlay.Visible = true;
		FadeTo(1f);
	}

	public void Close() {
		if (overlay == null || !overlay.Visible) return;
		FadeTo(0f).Finished += () => overlay.Visible = false;
	}

	private Tween FadeTo(float alpha) {
		fade?.Kill();
		if (alpha > 0f) overlay.Modulate = new Color(1, 1, 1, 0);
		fade = CreateTween();
		fade.TweenProperty(overlay, "modulate:a", alpha, fadeTime);
		return fade;
	}
}
