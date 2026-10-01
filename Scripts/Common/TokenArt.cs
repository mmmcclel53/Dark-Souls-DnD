using Godot;
using System.Collections.Generic;

// The character board's three tokens, ready and spent: Estus Flask, Heroic Action, Luck.
// A spent token is flipped over, the way the physical token is, until a bonfire rest.
public static class TokenArt
{
	public enum Kind { ESTUS, HEROIC, LUCK }

	private const string DIR = "res://Resources/Images/Sprites/Character/";
	private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

	public static Texture2D Ready(Kind kind) => Load(Name(kind));
	public static Texture2D Used(Kind kind) => Load(Name(kind) + "Used");
	public static Texture2D For(Kind kind, bool used) => used ? Used(kind) : Ready(kind);

	public static string Title(Kind kind) => kind switch {
		Kind.ESTUS => "Estus Flask",
		Kind.HEROIC => "Heroic Action",
		_ => "Luck",
	};

	private static string Name(Kind kind) => kind switch {
		Kind.ESTUS => "Estus",
		Kind.HEROIC => "Heroic",
		_ => "Luck",
	};

	// Straight from the file when the editor has not imported it yet, so a fresh checkout
	// run without opening the editor still shows the art.
	private static Texture2D Load(string name) {
		if (cache.TryGetValue(name, out Texture2D cached)) return cached;
		string path = DIR + name + ".png";
		Texture2D texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
		if (texture == null) {
			Image image = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
			if (image != null && !image.IsEmpty()) texture = ImageTexture.CreateFromImage(image);
		}
		cache[name] = texture;
		return texture;
	}
}
