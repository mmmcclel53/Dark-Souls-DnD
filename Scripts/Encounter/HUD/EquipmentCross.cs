using Godot;

// The Dark Souls equipment cross, floating left of the action bar: backup slot on top,
// armour directly under it edge to edge, the two hand slots either side of that seam with
// a small gap. Sized from one slot size so the whole cluster scales together.
//
// Clicking a hand raises that weapon in the bar; the armour lights up while an incoming
// attack is being answered. A two-handed weapon occupies one slot and greys the other.
public partial class EquipmentCross : Control
{

	[Signal] public delegate void HandPressedEventHandler(int hand);
	[Signal] public delegate void ArmourPressedEventHandler();
	[Signal] public delegate void BackupPressedEventHandler();

	public const int LEFT = 0;
	public const int RIGHT = 1;

	[Export] public EquipmentSlot backup;
	[Export] public EquipmentSlot left;
	[Export] public EquipmentSlot right;
	[Export] public EquipmentSlot armour;

	[Export] public Vector2 slotSize = new Vector2(84, 108);
	[Export] public float gap = 8f;

	private Player player;

	public override void _Ready() {
		if (left != null) left.Pressed += () => EmitSignal(SignalName.HandPressed, LEFT);
		if (right != null) right.Pressed += () => EmitSignal(SignalName.HandPressed, RIGHT);
		if (armour != null) armour.Pressed += () => EmitSignal(SignalName.ArmourPressed);
		if (backup != null) backup.Pressed += () => EmitSignal(SignalName.BackupPressed);
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
		backup?.Show(player.GetBackupSlot());
		armour?.Show(player.GetArmour());

		// A two-hander fills one hand and greys the other (p12: the other slot must be empty).
		left?.Show(leftHand);
		right?.Show(rightHand);
		if (leftHand != null && leftHand.numHands > 1 && rightHand == null) right?.SetSpent(true);
		if (rightHand != null && rightHand.numHands > 1 && leftHand == null) left?.SetSpent(true);
	}

	public void Clear() {
		player = null;
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

	public void SetDefending(bool defending) {
		armour?.SetRaised(defending);
	}
}
