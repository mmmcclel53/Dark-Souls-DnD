using Godot;
using System.Collections.Generic;

// The Dark Souls equipment cross, floating left of the action bar: backup slot on top,
// armour directly under it edge to edge, the two hand slots either side of that seam with
// a small gap. Sized from one slot size so the whole cluster scales together.
//
// Clicking a hand raises that weapon in the bar; while an incoming attack is being answered,
// every piece that rolls defence lights up instead. A two-handed weapon occupies one slot and greys the other.
//
// The backup slot holds every weapon not in a hand (p12), so with more than one it is a
// carousel: arrows either side of it step through them and dots along its foot say how many
// there are. Stepping is only a view; it changes nothing the character carries.
public partial class EquipmentCross : Control
{

	[Signal] public delegate void HandPressedEventHandler(int hand);
	[Signal] public delegate void ArmourPressedEventHandler();
	[Signal] public delegate void BackupPressedEventHandler();
	[Signal] public delegate void BackupCycledEventHandler();

	public const int LEFT = 0;
	public const int RIGHT = 1;

	[Export] public EquipmentSlot backup;
	[Export] public EquipmentSlot left;
	[Export] public EquipmentSlot right;
	[Export] public EquipmentSlot armour;

	[Export] public Vector2 slotSize = new Vector2(84, 108);
	[Export] public float gap = 8f;
	[Export] public Color pagerColour = new Color(0.86f, 0.8f, 0.66f);

	private Player player;
	private Button backupPrev;
	private Button backupNext;
	private Label backupDots;

	public override void _Ready() {
		if (left != null) left.Pressed += () => EmitSignal(SignalName.HandPressed, LEFT);
		if (right != null) right.Pressed += () => EmitSignal(SignalName.HandPressed, RIGHT);
		if (armour != null) armour.Pressed += () => EmitSignal(SignalName.ArmourPressed);
		if (backup != null) backup.Pressed += () => EmitSignal(SignalName.BackupPressed);
		BuildPager();
		Layout();
	}

	public Vector2 clusterSize => new Vector2(3f * slotSize.X + 2f * gap, 2f * slotSize.Y);

	public void Layout() {
		float column = slotSize.X + gap;
		float centreX = column + slotSize.X * 0.5f;

		Place(backup, new Vector2(centreX - slotSize.X * 0.5f, 0f));
		Place(armour, new Vector2(centreX - slotSize.X * 0.5f, slotSize.Y));
		Place(left, new Vector2(0f, slotSize.Y * 0.5f));
		Place(right, new Vector2(2f * column, slotSize.Y * 0.5f));

		CustomMinimumSize = clusterSize;
		Size = clusterSize;
		PlacePager();
	}

	private void BuildPager() {
		backupPrev = PagerButton("‹", -1);
		backupNext = PagerButton("›", 1);
		backupDots = new Label {
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			Visible = false,
		};
		backupDots.AddThemeFontSizeOverride("font_size", 10);
		backupDots.AddThemeColorOverride("font_color", pagerColour);
		backupDots.AddThemeColorOverride("font_outline_color", Colors.Black);
		backupDots.AddThemeConstantOverride("outline_size", 4);
		AddChild(backupDots);
	}

	private Button PagerButton(string glyph, int direction) {
		Button button = new Button { Text = glyph, Flat = true, FocusMode = FocusModeEnum.None, Visible = false };
		button.AddThemeFontSizeOverride("font_size", 30);
		foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color" }) {
			button.AddThemeColorOverride(state, state == "font_color" ? pagerColour : Colors.White);
		}
		button.AddThemeColorOverride("font_outline_color", Colors.Black);
		button.AddThemeConstantOverride("outline_size", 6);
		button.Pressed += () => Cycle(direction);
		AddChild(button);
		return button;
	}

	// Either side of the backup slot, in the corners the hands leave free above them.
	private void PlacePager() {
		if (backup == null || backupPrev == null) return;
		Vector2 arrow = new Vector2(gap + 14f, slotSize.Y * 0.45f);
		float y = (slotSize.Y * 0.5f - arrow.Y) * 0.5f;
		backupPrev.Position = new Vector2(backup.Position.X - arrow.X, y);
		backupPrev.Size = arrow;
		backupNext.Position = new Vector2(backup.Position.X + slotSize.X, y);
		backupNext.Size = arrow;
		backupDots.Position = new Vector2(backup.Position.X, slotSize.Y - 16f);
		backupDots.Size = new Vector2(slotSize.X, 14f);
	}

	private void Cycle(int direction) {
		if (player == null) return;
		player.CycleBackup(direction, includeEmpty: false);
		backup?.Show(player.GetBackupSlot());
		RefreshPager();
		EmitSignal(SignalName.BackupCycled);
	}

	private void RefreshPager() {
		int count = player?.BackupIds().Length ?? 0;
		bool paged = count > 1;
		if (backupPrev != null) backupPrev.Visible = paged;
		if (backupNext != null) backupNext.Visible = paged;
		if (backupDots == null) return;
		backupDots.Visible = paged;
		string dots = "";
		for (int i = 0; i < count; i++) dots += i == player.shownBackup ? "●" : "○";
		backupDots.Text = dots;
	}

	private void Place(EquipmentSlot slot, Vector2 position) {
		if (slot == null) return;
		slot.CustomMinimumSize = slotSize;
		slot.Size = slotSize;
		slot.Position = position;
	}

	public void Show(Player shown) {
		player = shown;
		if (player == null) {
			Clear();
			return;
		}

		Weapon leftHand = player.GetLeftHand();
		Weapon rightHand = player.GetRightHand();
		player.ClampShownBackup(includeEmpty: false);
		backup?.Show(player.GetBackupSlot());
		RefreshPager();
		armour?.Show(player.GetArmour());

		// A two-hander fills one hand and greys the other (p12: the other slot must be empty).
		left?.Show(leftHand);
		right?.Show(rightHand);
		if (leftHand != null && leftHand.numHands > 1 && rightHand == null) right?.SetSpent(true);
		if (rightHand != null && rightHand.numHands > 1 && leftHand == null) left?.SetSpent(true);
	}

	public void Clear() {
		player = null;
		RefreshPager();
		backup?.Clear();
		left?.Clear();
		right?.Clear();
		armour?.Clear();
	}

	public Weapon WeaponIn(int hand) => hand == LEFT ? player?.GetLeftHand() : player?.GetRightHand();

	public void SetRaised(Weapon weapon) {
		left?.SetRaised(weapon != null && left.item == weapon);
		right?.SetRaised(weapon != null && right.item == weapon);
	}

	public void SetSpent(Weapon weapon, bool spent) {
		if (left != null && left.item == weapon) left.SetSpent(spent);
		if (right != null && right.item == weapon) right.SetSpent(spent);
	}

	// Lights exactly the pieces given, wherever they sit; an empty list lights nothing.
	public void SetDefending(ICollection<Equipment> gear) {
		foreach (EquipmentSlot slot in new[] { backup, left, right, armour }) {
			slot?.SetRaised(slot.item != null && gear != null && gear.Contains(slot.item));
		}
	}
}
