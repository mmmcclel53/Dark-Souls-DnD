using Godot;

// Decides which character wears the Nearest marker. Hovering an enemy (on the board or in
// the activation bar) shows the character that enemy counts as nearest; the spotlight pins
// the marker on the target of an enemy's "nearest" behaviour while it previews. A hover wins
// over a pin, and letting go of the hover falls back to it.
public static class NearestMarker
{
	private static Enemy hovered;
	private static PlayerToken pinned;
	private static PlayerToken shown;

	public static void Hover(Enemy enemy) {
		hovered = enemy;
		Refresh();
	}

	public static void Unhover(Enemy enemy) {
		if (hovered == enemy) hovered = null;
		Refresh();
	}

	public static void Pin(PlayerToken token) {
		pinned = token;
		Refresh();
	}

	public static void Reset() {
		hovered = null;
		pinned = null;
		shown = null;
	}

	private static void Refresh() {
		PlayerToken next = GodotObject.IsInstanceValid(hovered) ? hovered.NearestCharacter() : null;
		if (next == null && GodotObject.IsInstanceValid(pinned)) next = pinned;
		if (next == shown) return;

		if (GodotObject.IsInstanceValid(shown)) shown.SetNearest(false);
		if (next != null) next.SetNearest(true);
		shown = next;
	}
}
