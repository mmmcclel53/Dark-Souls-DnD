using Godot;

// A roaming threat on the map: its toughest enemy's face cut to a disc inside an iron ring,
// positioned over its hex like the party marker. Hunting, the ring pulses ember red. Out of
// sight it is a faint grey ghost, where the party last saw it.
public partial class WorldMapRoamerMarker : Control {
	private const float RADIUS = 17f;
	private const float RIM = 4f;
	private static readonly Color IRON = new Color(0.23f, 0.24f, 0.28f);
	private static readonly Color IRON_EDGE = new Color(0.55f, 0.57f, 0.63f);
	private static readonly Color EMBER = new Color(0.95f, 0.28f, 0.08f);
	private static readonly Vector2 CENTRE = new Vector2(0f, -6f);

	private Texture2D avatar;
	private bool chasing;
	private bool ghost;
	private float phase;

	public bool Chasing {
		get => chasing;
		set {
			if (chasing == value) return;
			chasing = value;
			UpdateProcessing();
			QueueRedraw();
		}
	}

	public bool Ghost {
		get => ghost;
		set {
			if (ghost == value) return;
			ghost = value;
			UpdateProcessing();
			QueueRedraw();
		}
	}

	public void SetAvatar(Texture2D texture) {
		avatar = texture;
		QueueRedraw();
	}

	public override void _Ready() {
		MouseFilter = MouseFilterEnum.Ignore;
		Size = Vector2.Zero;
		ZIndex = 99;
		Visible = false;
		UpdateProcessing();
	}

	private void UpdateProcessing() => SetProcess(chasing && !ghost);

	public override void _Process(double delta) {
		phase += (float)delta;
		QueueRedraw();
	}

	public override void _Draw() {
		float fade = ghost ? 0.3f : 1f;
		DrawSetTransform(CENTRE + new Vector2(0, RADIUS + 3f), 0, new Vector2(1f, 0.4f));
		DrawCircle(Vector2.Zero, RADIUS, new Color(0f, 0f, 0f, 0.35f * fade));
		DrawSetTransform(Vector2.Zero, 0, Vector2.One);

		DrawCircle(CENTRE, RADIUS + RIM * 0.5f, new Color(0.06f, 0.05f, 0.05f, 0.95f * fade));
		if (avatar != null) {
			Color tint = ghost ? new Color(0.55f, 0.55f, 0.58f, 0.35f) : Colors.White;
			WorldMapNode.AvatarDisc(this, avatar, CENTRE, RADIUS, tint);
		}

		float pulse = chasing && !ghost ? 0.5f + 0.5f * Mathf.Sin(phase * 6f) : 0f;
		Color ring = ghost ? IRON.Lerp(new Color(0.5f, 0.5f, 0.5f), 0.5f) with { A = 0.4f } : IRON.Lerp(EMBER, chasing ? 0.35f + 0.65f * pulse : 0f);
		if (chasing && !ghost) DrawArc(CENTRE, RADIUS + RIM + 1f, 0, Mathf.Tau, 40, EMBER with { A = 0.2f + 0.35f * pulse }, 6f, true);
		DrawArc(CENTRE, RADIUS, 0, Mathf.Tau, 40, ring, RIM, true);
		DrawArc(CENTRE, RADIUS + RIM * 0.5f, 0, Mathf.Tau, 40, IRON_EDGE with { A = IRON_EDGE.A * 0.7f * fade }, 1.2f, true);
	}
}
