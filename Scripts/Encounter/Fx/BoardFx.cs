using Godot;

// What the board-space effects share: the board they are parented under (so they zoom,
// pan and clip with it), the camera to jolt and the punch layer to flash. The camera and
// the punch register on ready and are cleared by EncounterManager.Reset, like the prompts.
public static class BoardFx
{
	public static BoardCamera camera;
	public static ScreenPunch punch;

	// The Encounter control. Effects go after the grid, so they draw over the tokens.
	public static Node Board => EncounterManager.pathGrid?.GetParent();

	public static void Reset() {
		camera = null;
		punch = null;
	}

	// Adds an effect under the board and returns it, or frees it and returns null when
	// there is no board to draw on, so callers null-check rather than leak an orphan.
	public static T Spawn<T>(T effect) where T : Node {
		Node board = Board;
		if (board == null || !GodotObject.IsInstanceValid(board)) {
			effect.Free();
			return null;
		}
		board.AddChild(effect);
		return effect;
	}

	public static Vector2 GlobalCentre(Control token) => token.GetGlobalTransform() * (token.Size * 0.5f);

	// A node can stand in for a token (the Node icon); a token standing on it is half its width.
	public static float GlobalRadius(Control token) {
		float half = token.Size.X * (token is GameNode ? 0.25f : 0.5f);
		return token.GetGlobalTransform().BasisXform(new Vector2(half, 0f)).Length();
	}

	public static float LocalRadius(Node2D effect, Control token) =>
		effect.GetGlobalTransform().AffineInverse().BasisXform(new Vector2(GlobalRadius(token), 0f)).Length();

	public static CanvasItemMaterial Additive() =>
		new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
}
