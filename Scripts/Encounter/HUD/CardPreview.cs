using Godot;

// The full printed card, shown while a piece of equipment is hovered: a slot on the
// equipment cross, or the raised weapon's art above its attack rows. It sits just above
// whatever is hovered, kept on screen, and goes as soon as the cursor leaves it.
//
// It checks every frame that the cursor is still over what it was shown for, rather than
// trusting MouseExited: the action bar rebuilds its header freely, and a freed control
// never says the mouse has left.
public partial class CardPreview : CanvasLayer
{
	public float cardHeight = 330f;
	public float gap = 10f;
	public float margin = 8f;
	public float fadeSeconds = 0.1f;

	private TextureRect card;
	private Control anchor;

	public override void _Ready() {
		Layer = 14;         // over the HUD and the roll reveal (12), under the enemy card viewer (15)
		card = new TextureRect {
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false,
		};
		AddChild(card);
	}

	public void ShowCard(Texture2D texture, Control over) {
		if (texture == null || over == null || card == null) return;
		anchor = over;
		card.Texture = texture;

		Vector2 art = texture.GetSize();
		Vector2 size = new Vector2(cardHeight * art.X / Mathf.Max(art.Y, 1f), cardHeight);
		card.Size = size;
		card.Position = Place(over.GetGlobalRect(), size);

		if (!card.Visible) {
			card.Modulate = new Color(1, 1, 1, 0);
			card.Visible = true;
			CreateTween().TweenProperty(card, "modulate:a", 1f, fadeSeconds);
		}
	}

	public void HideCard(Control over) {
		if (anchor != over) return;
		anchor = null;
		if (card != null) card.Visible = false;
	}

	public override void _Process(double delta) {
		if (card == null || !card.Visible) return;
		bool stillOver = IsInstanceValid(anchor) && anchor.IsVisibleInTree()
			&& anchor.GetGlobalRect().HasPoint(anchor.GetGlobalMousePosition());
		if (!stillOver) HideCard(anchor);
	}

	// Above the hovered control, centred on it; below it when there is no room above.
	private Vector2 Place(Rect2 over, Vector2 size) {
		Vector2 screen = GetViewport().GetVisibleRect().Size;
		float x = over.GetCenter().X - size.X * 0.5f;
		float y = over.Position.Y - gap - size.Y;
		if (y < margin) y = over.End.Y + gap;
		x = Mathf.Clamp(x, margin, Mathf.Max(margin, screen.X - size.X - margin));
		y = Mathf.Clamp(y, margin, Mathf.Max(margin, screen.Y - size.Y - margin));
		return new Vector2(x, y);
	}
}
