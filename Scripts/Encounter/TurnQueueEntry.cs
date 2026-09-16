using Godot;

// One face in the turn queue. Knows nothing about the encounter; TurnQueue drives it.
public partial class TurnQueueEntry : VBoxContainer
{

	[Export] public TextureRect avatar;
	[Export] public Control threatBadge;
	[Export] public Label threatLabel;
	[Export] public Label nameLabel;
	[Export] public Control activeRim;
	[Export] public Control spentSlash;

	// A threat below zero means the entry has no threat value to show, which is the
	// party's case — characters activate after every enemy, not in threat order.
	public void Fill(Texture2D face, string label, int threat) {
		if (avatar != null) avatar.Texture = face;
		if (nameLabel != null) nameLabel.Text = label;
		if (threatBadge != null) threatBadge.Visible = threat >= 0;
		if (threatLabel != null && threat >= 0) threatLabel.Text = threat.ToString();
	}

	public void SetState(bool activating, bool spent) {
		if (activeRim != null) activeRim.Visible = activating;
		if (spentSlash != null) spentSlash.Visible = spent;

		float alpha = spent ? 0.4f : (activating ? 1f : 0.7f);
		Modulate = new Color(1, 1, 1, alpha);
	}
}
