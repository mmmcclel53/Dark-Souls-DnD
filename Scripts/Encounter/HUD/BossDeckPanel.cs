using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

// A boss's behaviour cards beside the board, at its right edge (p27-29): the card it played
// last face up, where the weak arcs come from (p28), with the deck face down under it and the
// cards played so far this time through the deck in order, so the pattern can be learned.
// Each activation turns the next card over here before the boss acts on it; a click or Enter
// hurries it on. Hovering any card shows it full size in the card viewer.
//
// Shown only while a boss is on the board. It reads the boss's deck every frame rather than
// being told, as the activation bar's faces do.
public partial class BossDeckPanel : CanvasLayer
{
	public float revealSeconds = 1.1f;
	public float flipSeconds = 0.22f;

	private const float CARD_WIDTH = 150f;
	private const float THUMB_WIDTH = 30f;
	private const float MARGIN = 14f;

	private static readonly Color BRONZE = new Color(0.62f, 0.5f, 0.3f);
	private static readonly Color EMBER = new Color(1f, 0.45f, 0.12f);
	private static readonly Color PANEL = new Color(0.05f, 0.04f, 0.04f, 0.72f);

	private Enemy boss;
	private VBoxContainer column;
	private TextureRect face;
	private CardBack back;
	private Label deckCount;
	private HBoxContainer played;
	private string shown = "";
	private bool skip;
	private bool revealing;

	public override void _Ready() {
		Layer = 6;
		EncounterManager.bossPanel = this;
		Build();
		column.Visible = false;
	}

	public override void _ExitTree() {
		if (EncounterManager.bossPanel == this) EncounterManager.bossPanel = null;
	}

	public void Track(Enemy tracked) {
		boss = tracked;
		shown = "";
	}

	private void Build() {
		Control root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		AddChild(root);
		root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

		column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
		column.AddThemeConstantOverride("separation", 8);
		root.AddChild(column);
		column.AnchorLeft = column.AnchorRight = 1f;
		column.AnchorTop = column.AnchorBottom = 0.5f;
		column.OffsetLeft = -CARD_WIDTH - MARGIN;
		column.OffsetRight = -MARGIN;
		column.GrowVertical = Control.GrowDirection.Both;

		Vector2 cardSize = new Vector2(CARD_WIDTH, CARD_WIDTH * 4f / 3f);
		Control holder = new Control { CustomMinimumSize = cardSize, MouseFilter = Control.MouseFilterEnum.Ignore };
		column.AddChild(holder);
		back = new CardBack { CustomMinimumSize = cardSize, Size = cardSize, MouseFilter = Control.MouseFilterEnum.Ignore };
		holder.AddChild(back);
		face = new TextureRect {
			Size = cardSize,
			PivotOffset = cardSize * 0.5f,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		holder.AddChild(face);
		face.MouseEntered += () => ShowLarge(face.Texture);
		face.MouseExited += () => EnemyCardViewer.current?.Close();

		HBoxContainer deckRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		deckRow.AddThemeConstantOverride("separation", 6);
		column.AddChild(deckRow);

		Control deck = new Control { CustomMinimumSize = new Vector2(THUMB_WIDTH, THUMB_WIDTH * 4f / 3f), MouseFilter = Control.MouseFilterEnum.Ignore };
		deckRow.AddChild(deck);
		deck.AddChild(new CardBack { small = true, Size = deck.CustomMinimumSize, MouseFilter = Control.MouseFilterEnum.Ignore });
		deckCount = new Label {
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Size = deck.CustomMinimumSize,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		deckCount.AddThemeFontSizeOverride("font_size", 16);
		deckCount.AddThemeColorOverride("font_color", new Color(0.93f, 0.85f, 0.62f));
		deckCount.AddThemeColorOverride("font_outline_color", Colors.Black);
		deckCount.AddThemeConstantOverride("outline_size", 5);
		if (ResourceLoader.Exists("res://OptimusPrinceps.ttf")) deckCount.AddThemeFontOverride("font", GD.Load<Font>("res://OptimusPrinceps.ttf"));
		deck.AddChild(deckCount);

		played = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		played.AddThemeConstantOverride("separation", 3);
		deckRow.AddChild(played);
	}

	public override void _Process(double delta) {
		bool live = boss != null && GodotObject.IsInstanceValid(boss) && !boss.isDead && boss.behaviour != null;
		column.Visible = live;
		if (!live) return;
		back.boss = boss;
		back.heated = boss.behaviour.heated;

		BossBehaviour behaviour = boss.behaviour;
		string now = $"{behaviour.CardsLeft}|{string.Join(",", behaviour.DiscardIndices)}|{behaviour.LastIndex}|{behaviour.heated}";
		if (now == shown) return;
		shown = now;
		deckCount.Text = behaviour.CardsLeft.ToString();
		if (!revealing) {
			ShowFace(behaviour.lastCard);
			face.Scale = Vector2.One;
		}
		foreach (Node child in played.GetChildren()) child.QueueFree();
		foreach (BossCard card in behaviour.discard) played.AddChild(Thumb(card));
	}

	private void ShowFace(BossCard card) {
		face.Texture = card?.texture;
		face.Visible = card?.texture != null;
	}

	private TextureRect Thumb(BossCard card) {
		TextureRect thumb = new TextureRect {
			Texture = card?.texture,
			CustomMinimumSize = new Vector2(THUMB_WIDTH, THUMB_WIDTH * 4f / 3f),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		thumb.MouseEntered += () => ShowLarge(card?.texture);
		thumb.MouseExited += () => EnemyCardViewer.current?.Close();
		return thumb;
	}

	private static void ShowLarge(Texture2D texture) {
		if (texture != null) EnemyCardViewer.current?.ShowTexture(texture);
	}

	// The card turned over: the face flips edge-on and back with the new card, glows a moment,
	// and holds so it can be read before the boss acts on it.
	public async Task Reveal(Enemy revealing, BossCard card) {
		Track(revealing);
		if (card?.texture == null || !IsInsideTree()) return;
		this.revealing = true;
		column.Visible = true;

		Tween tween = CreateTween();
		tween.TweenProperty(face, "scale:x", 0f, flipSeconds * 0.5f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
		tween.TweenCallback(Callable.From(() => ShowFace(card)));
		face.Scale = new Vector2(face.Visible ? 1f : 0f, 1f);
		tween.TweenProperty(face, "scale:x", 1f, flipSeconds * 0.5f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(face, "modulate", new Color(1.35f, 1.2f, 0.95f), 0.12f);
		tween.TweenProperty(face, "modulate", Colors.White, 0.35f);
		await ToSignal(tween, Tween.SignalName.Finished);

		skip = false;
		float elapsed = 0f;
		while (elapsed < revealSeconds && !skip && IsInsideTree()) {
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			elapsed += (float)GetProcessDeltaTime();
		}
		this.revealing = false;
	}

	public override void _Input(InputEvent @event) {
		if (!revealing) return;
		bool click = @event is InputEventMouseButton button && button.Pressed && button.ButtonIndex == MouseButton.Left;
		if (!click && !@event.IsActionPressed("ui_accept")) return;
		skip = true;
		GetViewport().SetInputAsHandled();
	}

	// The back of a behaviour card: dark, rimmed in bronze, the boss's face in the middle. Embers
	// round its edge once the boss has heated up.
	private partial class CardBack : Control
	{
		public Enemy boss;
		public bool heated;
		public bool small;
		private float time;

		public override void _Process(double delta) {
			time += (float)delta;
			QueueRedraw();
		}

		public override void _Draw() {
			Rect2 rect = new Rect2(Vector2.Zero, Size);
			float corner = Mathf.Min(Size.X, Size.Y) * 0.08f;
			DrawStyleBox(new StyleBoxFlat {
				BgColor = PANEL, BorderColor = BRONZE,
				BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
				CornerRadiusTopLeft = (int)corner, CornerRadiusTopRight = (int)corner,
				CornerRadiusBottomLeft = (int)corner, CornerRadiusBottomRight = (int)corner,
			}, rect);
			if (!small && boss?.data?.avatarTexture != null) {
				float side = Size.X * 0.62f;
				DrawTextureRect(boss.data.avatarTexture, new Rect2(Size * 0.5f - new Vector2(side, side) * 0.5f, new Vector2(side, side)), false, new Color(1, 1, 1, 0.35f));
			}
			if (heated) {
				float glow = 0.55f + 0.45f * Mathf.Sin(time * Mathf.Tau * 0.9f);
				DrawRect(rect.Grow(-1f), new Color(EMBER, 0.8f * glow), false, small ? 1.5f : 2.5f);
			}
		}
	}
}
