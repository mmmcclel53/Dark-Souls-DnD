using Godot;

// A character's face in a gold ring, BG3-style: the art clipped to a disc by CircleAvatar
// shader, damage rising from the bottom as a red fill, the rings drawn here.
//
// Character avatars are 3:4 with the face near the top, so the disc shows the top square
// of the art rather than its middle.
public partial class CircleAvatar : Control
{

	[Export] public TextureRect art;
	[Export] public Shader circleShader;

	[Export] public Color ringColour = new Color(0.788f, 0.635f, 0.294f);
	[Export] public Color haloColour = new Color(0.788f, 0.635f, 0.294f, 0.28f);
	[Export] public float ringWidth = 3f;

	private ShaderMaterial material;
	private Texture2D shownFor;
	private AtlasTexture crop;

	public override void _Ready() {
		if (art != null && circleShader != null) {
			material = new ShaderMaterial { Shader = circleShader };
			art.Material = material;
		}
		Resized += Fit;
		Fit();
	}

	private void Fit() {
		if (art != null) material?.SetShaderParameter("size", art.Size);
		QueueRedraw();
	}

	public void Show(Player player) {
		Texture2D avatar = player?.character?.avatar ?? player?.character?.image;
		if (art == null || avatar == null) {
			if (art != null) art.Texture = null;
			return;
		}

		if (avatar != shownFor) {
			shownFor = avatar;
			Vector2 size = avatar.GetSize();
			float side = Mathf.Min(size.X, size.Y);
			crop = new AtlasTexture {
				Atlas = avatar,
				Region = new Rect2((size.X - side) * 0.5f, 0f, side, side),
			};
		}
		art.Texture = crop;
		SetDamage((float)player.endurance.damageTaken / Endurance.BOXES);
	}

	public void SetDamage(float fraction) {
		material?.SetShaderParameter("damage", Mathf.Clamp(fraction, 0f, 1f));
	}

	public void Clear() {
		shownFor = null;
		if (art != null) art.Texture = null;
		SetDamage(0f);
	}

	public override void _Draw() {
		Vector2 centre = Size * 0.5f;
		float radius = Mathf.Min(Size.X, Size.Y) * 0.5f;
		DrawArc(centre, radius - ringWidth * 0.5f, 0f, Mathf.Tau, 96, ringColour, ringWidth, true);
		DrawArc(centre, radius - ringWidth - 4f, 0f, Mathf.Tau, 96, haloColour, 1.5f, true);
	}
}
