using Godot;

// The printed data card, large, over everything. Opened from the Enemy Activation bar;
// any click or Escape puts it away. The inspect dialog still owns the written-out version.
public partial class EnemyCardViewer : CanvasLayer
{

	[Export] public Control overlay;
	[Export] public TextureRect cardImage;
	[Export] public float fadeTime = 0.12f;

	// Card height as a fraction of the screen; the width follows the card's own aspect.
	[Export] public float heightFraction = 0.45f;

	private Tween fade;

	public override void _Ready() {
		if (overlay == null) return;
		overlay.Visible = false;
		overlay.GuiInput += OnOverlayInput;
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

	private void OnOverlayInput(InputEvent @event) {
		if (@event is InputEventMouseButton click && click.Pressed) {
			overlay.AcceptEvent();
			Close();
		}
	}

	public override void _UnhandledInput(InputEvent @event) {
		if (overlay != null && overlay.Visible && @event.IsActionPressed("ui_cancel")) {
			GetViewport().SetInputAsHandled();
			Close();
		}
	}
}
