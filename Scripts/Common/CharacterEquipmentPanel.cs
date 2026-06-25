using Godot;

// Middle column of the EquipmentModal. Mirrors CharacterSheet's slot layout
// (Backup, Left Hand, Right Hand, Armour, each with 2 upgrade buttons)
// but without the StaminaHealth strip. Emits SlotSelected when any
// slot or upgrade button is clicked. Reads current equipment from Player.
public partial class CharacterEquipmentPanel : Control
{
    [Signal] public delegate void SlotSelectedEventHandler(int slotKind);

    [Export] public TextureRect background;

    [Export] public TextureButton backupSlotButton;
    [Export] public TextureButton backupUpgrade1Button;
    [Export] public TextureButton backupUpgrade2Button;

    [Export] public TextureButton leftHandButton;
    [Export] public TextureButton leftHandUpgrade1Button;
    [Export] public TextureButton leftHandUpgrade2Button;

    [Export] public TextureButton rightHandButton;
    [Export] public TextureButton rightHandUpgrade1Button;
    [Export] public TextureButton rightHandUpgrade2Button;

    [Export] public TextureButton armourButton;
    [Export] public TextureButton armourUpgrade1Button;
    [Export] public TextureButton armourUpgrade2Button;

    [Export] public Panel selectionHighlight;

    private Player player;
    private EquipmentModal.SlotKind selectedSlot = EquipmentModal.SlotKind.None;

    public override void _Ready() {
        Wire(backupSlotButton,     EquipmentModal.SlotKind.Backup);
        Wire(backupUpgrade1Button, EquipmentModal.SlotKind.BackupUpgrade1);
        Wire(backupUpgrade2Button, EquipmentModal.SlotKind.BackupUpgrade2);
        Wire(leftHandButton,       EquipmentModal.SlotKind.LeftHand);
        Wire(leftHandUpgrade1Button, EquipmentModal.SlotKind.LeftHandUpgrade1);
        Wire(leftHandUpgrade2Button, EquipmentModal.SlotKind.LeftHandUpgrade2);
        Wire(rightHandButton,      EquipmentModal.SlotKind.RightHand);
        Wire(rightHandUpgrade1Button, EquipmentModal.SlotKind.RightHandUpgrade1);
        Wire(rightHandUpgrade2Button, EquipmentModal.SlotKind.RightHandUpgrade2);
        Wire(armourButton,         EquipmentModal.SlotKind.Armour);
        Wire(armourUpgrade1Button, EquipmentModal.SlotKind.ArmourUpgrade1);
        Wire(armourUpgrade2Button, EquipmentModal.SlotKind.ArmourUpgrade2);
        if (selectionHighlight != null) selectionHighlight.Visible = false;
    }

    private void Wire(TextureButton btn, EquipmentModal.SlotKind kind) {
        if (btn == null) return;
        btn.Pressed += () => EmitSignal(SignalName.SlotSelected, (int)kind);
    }

    public void SetPlayer(Player p) {
        player = p;
        Refresh();
    }

    public void SetSelectedSlot(EquipmentModal.SlotKind slot) {
        selectedSlot = slot;
        // TODO: visualize per-button selection (simple modulate for now).
        ApplySelectionModulate();
    }

    public void HighlightCompatibleSlots(Equipment item) {
        // TODO: pulse compatible slot buttons. For now we just leave selection alone;
        // the inventory click in the modal also drives ComparisonPanel which is the
        // primary feedback channel.
    }

    public void Refresh() {
        if (background != null)
            background.Texture = player?.character?.image;

        if (player == null) {
            ClearAllSlotIcons();
            HideAllUpgradeButtons();
            return;
        }

        var backup = player.GetBackupSlot();
        var left   = player.GetLeftHand();
        var right  = player.GetRightHand();
        var armour = player.GetArmour();

        SetIcon(backupSlotButton, backup?.image);
        SetIcon(leftHandButton,   left?.image);
        SetIcon(rightHandButton,  right?.image);
        SetIcon(armourButton,     armour?.image);

        ApplyUpgradeVisibility(backup, backupUpgrade1Button, backupUpgrade2Button, player.backupUpgradeIds);
        ApplyUpgradeVisibility(left,   leftHandUpgrade1Button, leftHandUpgrade2Button, player.leftHandUpgradeIds);
        ApplyUpgradeVisibility(right,  rightHandUpgrade1Button, rightHandUpgrade2Button, player.rightHandUpgradeIds);
        ApplyUpgradeVisibility(armour, armourUpgrade1Button, armourUpgrade2Button, player.armourUpgradeIds);

        ApplySelectionModulate();
    }

    private void ApplyUpgradeVisibility(Equipment baseItem, TextureButton up1, TextureButton up2, string[] upgradeIds) {
        int slots = UpgradeSlotsOf(baseItem);
        if (up1 != null) {
            up1.Visible = slots >= 1;
            up1.TextureNormal = up1.Visible ? GameManager.GetInstance(GetUpgradeId(upgradeIds, 0))?.image : null;
        }
        if (up2 != null) {
            up2.Visible = slots >= 2;
            up2.TextureNormal = up2.Visible ? GameManager.GetInstance(GetUpgradeId(upgradeIds, 1))?.image : null;
        }
    }

    private static int UpgradeSlotsOf(Equipment baseItem) {
        if (baseItem is Weapon w) return w.upgradeSlots;
        if (baseItem is Armour a) return a.upgradeSlots;
        return 0;
    }

    private static string GetUpgradeId(string[] arr, int idx) {
        if (arr == null || idx < 0 || idx >= arr.Length) return "";
        return arr[idx];
    }

    private void ClearAllSlotIcons() {
        SetIcon(backupSlotButton, null);
        SetIcon(leftHandButton, null);
        SetIcon(rightHandButton, null);
        SetIcon(armourButton, null);
    }

    private void HideAllUpgradeButtons() {
        foreach (var b in new[] { backupUpgrade1Button, backupUpgrade2Button,
                                   leftHandUpgrade1Button, leftHandUpgrade2Button,
                                   rightHandUpgrade1Button, rightHandUpgrade2Button,
                                   armourUpgrade1Button, armourUpgrade2Button })
            if (b != null) b.Visible = false;
    }

    private static void SetIcon(TextureButton btn, Texture2D tex) {
        if (btn == null) return;
        btn.TextureNormal = tex;
    }

    private void ApplySelectionModulate() {
        // Simple visual: dim non-selected slot icons when a slot is selected.
        var allSlots = new (TextureButton btn, EquipmentModal.SlotKind kind)[] {
            (backupSlotButton, EquipmentModal.SlotKind.Backup),
            (backupUpgrade1Button, EquipmentModal.SlotKind.BackupUpgrade1),
            (backupUpgrade2Button, EquipmentModal.SlotKind.BackupUpgrade2),
            (leftHandButton, EquipmentModal.SlotKind.LeftHand),
            (leftHandUpgrade1Button, EquipmentModal.SlotKind.LeftHandUpgrade1),
            (leftHandUpgrade2Button, EquipmentModal.SlotKind.LeftHandUpgrade2),
            (rightHandButton, EquipmentModal.SlotKind.RightHand),
            (rightHandUpgrade1Button, EquipmentModal.SlotKind.RightHandUpgrade1),
            (rightHandUpgrade2Button, EquipmentModal.SlotKind.RightHandUpgrade2),
            (armourButton, EquipmentModal.SlotKind.Armour),
            (armourUpgrade1Button, EquipmentModal.SlotKind.ArmourUpgrade1),
            (armourUpgrade2Button, EquipmentModal.SlotKind.ArmourUpgrade2),
        };
        bool anySelected = selectedSlot != EquipmentModal.SlotKind.None;
        foreach (var (btn, kind) in allSlots) {
            if (btn == null) continue;
            btn.Modulate = (anySelected && kind != selectedSlot) ? new Color(1, 1, 1, 0.55f) : Colors.White;
        }
    }
}
