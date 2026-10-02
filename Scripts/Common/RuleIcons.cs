using Godot;
using System.Collections.Generic;

// The weapon-option icons from the rulebook (p21, p23): Push, Node, Shaft, Repeat and Shift,
// white on transparent so the scene tints them. Cut from the PDF by Tools/RuleIcons/extract.py.
// Shift and Repeat have their example numbers blanked; the caller draws the real value.
public static class RuleIcons
{
	public enum Kind { PUSH, NODE, SHAFT, REPEAT, SHIFT }

	private const string DIR = "res://Resources/Images/Sprites/Combat/";
	private static readonly Dictionary<Kind, Texture2D> cache = new Dictionary<Kind, Texture2D>();

	public static string Title(Kind kind) => kind switch {
		Kind.PUSH => "Push",
		Kind.NODE => "Node: hits every enemy on the node",
		Kind.SHAFT => "Shaft: cannot hit range 0",
		Kind.REPEAT => "Repeat",
		_ => "Shift: free movement",
	};

	// Straight from the file when the editor has not imported it yet.
	public static Texture2D Get(Kind kind) {
		if (cache.TryGetValue(kind, out Texture2D cached)) return cached;
		string path = DIR + kind switch {
			Kind.PUSH => "Push",
			Kind.NODE => "Node",
			Kind.SHAFT => "Shaft",
			Kind.REPEAT => "Repeat",
			_ => "Shift",
		} + ".png";
		Texture2D texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
		if (texture == null) {
			Image image = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
			if (image != null && !image.IsEmpty()) texture = ImageTexture.CreateFromImage(image);
		}
		cache[kind] = texture;
		return texture;
	}
}
