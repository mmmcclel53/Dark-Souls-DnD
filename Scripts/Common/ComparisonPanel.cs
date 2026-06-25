using Godot;

// A small overlay panel that slides in from the right edge of the equipment column,
// showing "Currently equipped" vs "Proposed" with stat deltas. Confirm equips,
// Cancel hides the panel.
public partial class ComparisonPanel : PanelContainer
{
    [Signal] public delegate void ConfirmedEventHandler(int slotKind, string incomingInstanceId);
    [Signal] public delegate void CancelledEventHandler();

    [Export] public Label headerLabel;

    [Export] public TextureRect currentIcon;
    [Export] public Label currentNameLabel;
    [Export] public Label currentStatsLabel;

    [Export] public TextureRect incomingIcon;
    [Export] public Label incomingNameLabel;
    [Export] public Label incomingStatsLabel;

    [Export] public Label deltaLabel;

    [Export] public Button confirmButton;
    [Export] public Button cancelButton;

    private EquipmentModal.SlotKind slotKind;
    private string incomingId;

    public override void _Ready() {
        Visible = false;
        if (confirmButton != null)
            confirmButton.Pressed += () => EmitSignal(SignalName.Confirmed, (int)slotKind, incomingId);
        if (cancelButton != null)
            cancelButton.Pressed += () => EmitSignal(SignalName.Cancelled);
    }

    public void HidePanel() {
        Visible = false;
    }

    public void ShowComparison(Player p, EquipmentModal.SlotKind slot, Equipment current, Equipment incoming) {
        if (p == null || incoming == null) return;
        slotKind = slot;
        incomingId = incoming.id;
        Visible = true;

        if (headerLabel != null) headerLabel.Text = $"Equip to {SlotLabel(slot)}";

        if (currentIcon != null) currentIcon.Texture = current?.image;
        if (currentNameLabel != null) currentNameLabel.Text = current?.name ?? "— empty —";
        if (currentStatsLabel != null) currentStatsLabel.Text = StatBlock(current);

        if (incomingIcon != null) incomingIcon.Texture = incoming.image;
        if (incomingNameLabel != null) incomingNameLabel.Text = incoming.name;
        if (incomingStatsLabel != null) incomingStatsLabel.Text = StatBlock(incoming);

        if (deltaLabel != null) deltaLabel.Text = DeltaBlock(p, slot, current, incoming);
    }

    private static string SlotLabel(EquipmentModal.SlotKind s) {
        switch (s) {
            case EquipmentModal.SlotKind.Backup:    return "Backup";
            case EquipmentModal.SlotKind.LeftHand:  return "Left Hand";
            case EquipmentModal.SlotKind.RightHand: return "Right Hand";
            case EquipmentModal.SlotKind.Armour:    return "Armour";
            case EquipmentModal.SlotKind.BackupUpgrade1:    return "Backup Upgrade 1";
            case EquipmentModal.SlotKind.BackupUpgrade2:    return "Backup Upgrade 2";
            case EquipmentModal.SlotKind.LeftHandUpgrade1:  return "Left Hand Upgrade 1";
            case EquipmentModal.SlotKind.LeftHandUpgrade2:  return "Left Hand Upgrade 2";
            case EquipmentModal.SlotKind.RightHandUpgrade1: return "Right Hand Upgrade 1";
            case EquipmentModal.SlotKind.RightHandUpgrade2: return "Right Hand Upgrade 2";
            case EquipmentModal.SlotKind.ArmourUpgrade1:    return "Armour Upgrade 1";
            case EquipmentModal.SlotKind.ArmourUpgrade2:    return "Armour Upgrade 2";
            default: return "Slot";
        }
    }

    private static string StatBlock(Equipment e) {
        if (e == null) return "—";
        string s = $"{e.type} · {e.rarity}\n";
        if (e is Weapon w) {
            s += $"Atk options: {(w.attacks?.Count ?? 0)}\n";
            s += $"Dodge +{w.dodgeAbility}\n";
            s += $"Upgrade slots: {w.upgradeSlots}\n";
        }
        if (e is Armour a) {
            s += $"Dodge +{a.dodgeAbility}\n";
            s += $"Upgrade slots: {a.upgradeSlots}\n";
        }
        s += ReqLine(e);
        return s;
    }

    private static string ReqLine(Equipment e) {
        string s = "Reqs: ";
        bool any = false;
        if (e.strengthReq > 0)     { s += $"STR {e.strengthReq} ";     any = true; }
        if (e.dexterityReq > 0)    { s += $"DEX {e.dexterityReq} ";    any = true; }
        if (e.intelligenceReq > 0) { s += $"INT {e.intelligenceReq} "; any = true; }
        if (e.faithReq > 0)        { s += $"FAI {e.faithReq} ";        any = true; }
        return any ? s : "";
    }

    // Recomputes the player's summary stats with a hypothetical equip swap,
    // then prints the deltas vs. the player's current stats.
    private static string DeltaBlock(Player p, EquipmentModal.SlotKind slot, Equipment current, Equipment incoming) {
        var (pdMinB, pdMaxB) = p.GetBestPhysicalDamage();
        var (mdMinB, mdMaxB) = p.GetBestMagicDamage();
        var (pfMinB, pfMaxB) = p.GetPhysicalDefense();
        var (mfMinB, mfMaxB) = p.GetMagicDefense();
        int dodgeB = p.GetDodge();

        // Temporarily swap, recompute, then restore.
        string slotIdField = SlotIdSnapshot(p, slot, out int upgradeIdx, out string[] upgradeArrSnapshot);
        Equipment.EquipmentType prevTypeMarker = current?.type ?? Equipment.EquipmentType.Item;
        SwapForPreview(p, slot, incoming);

        var (pdMinA, pdMaxA) = p.GetBestPhysicalDamage();
        var (mdMinA, mdMaxA) = p.GetBestMagicDamage();
        var (pfMinA, pfMaxA) = p.GetPhysicalDefense();
        var (mfMinA, mfMaxA) = p.GetMagicDefense();
        int dodgeA = p.GetDodge();

        // Restore.
        RestoreFromSnapshot(p, slot, slotIdField, upgradeIdx, upgradeArrSnapshot);

        return
            $"Phys Dmg: {Fmt(pdMaxA - pdMaxB)}\n" +
            $"Mag Dmg:  {Fmt(mdMaxA - mdMaxB)}\n" +
            $"Phys Def: {Fmt(pfMaxA - pfMaxB)}\n" +
            $"Mag Def:  {Fmt(mfMaxA - mfMaxB)}\n" +
            $"Dodge:    {Fmt(dodgeA - dodgeB)}";
    }

    private static string Fmt(int delta) => delta >= 0 ? $"+{delta}" : delta.ToString();

    // Snapshot helpers — these preserve enough to undo a preview swap.

    private static string SlotIdSnapshot(Player p, EquipmentModal.SlotKind slot, out int upgradeIdx, out string[] upgradeArr) {
        upgradeIdx = -1;
        upgradeArr = null;
        switch (slot) {
            case EquipmentModal.SlotKind.Backup:    return p.backupSlotId;
            case EquipmentModal.SlotKind.LeftHand:  return p.leftHandId;
            case EquipmentModal.SlotKind.RightHand: return p.rightHandId;
            case EquipmentModal.SlotKind.Armour:    return p.armourId;
            case EquipmentModal.SlotKind.BackupUpgrade1:    upgradeIdx = 0; upgradeArr = (string[])p.backupUpgradeIds.Clone(); return null;
            case EquipmentModal.SlotKind.BackupUpgrade2:    upgradeIdx = 1; upgradeArr = (string[])p.backupUpgradeIds.Clone(); return null;
            case EquipmentModal.SlotKind.LeftHandUpgrade1:  upgradeIdx = 0; upgradeArr = (string[])p.leftHandUpgradeIds.Clone(); return null;
            case EquipmentModal.SlotKind.LeftHandUpgrade2:  upgradeIdx = 1; upgradeArr = (string[])p.leftHandUpgradeIds.Clone(); return null;
            case EquipmentModal.SlotKind.RightHandUpgrade1: upgradeIdx = 0; upgradeArr = (string[])p.rightHandUpgradeIds.Clone(); return null;
            case EquipmentModal.SlotKind.RightHandUpgrade2: upgradeIdx = 1; upgradeArr = (string[])p.rightHandUpgradeIds.Clone(); return null;
            case EquipmentModal.SlotKind.ArmourUpgrade1:    upgradeIdx = 0; upgradeArr = (string[])p.armourUpgradeIds.Clone(); return null;
            case EquipmentModal.SlotKind.ArmourUpgrade2:    upgradeIdx = 1; upgradeArr = (string[])p.armourUpgradeIds.Clone(); return null;
            default: return null;
        }
    }

    private static void RestoreFromSnapshot(Player p, EquipmentModal.SlotKind slot, string prevId, int upgradeIdx, string[] upgradeArr) {
        switch (slot) {
            case EquipmentModal.SlotKind.Backup:    p.backupSlotId = prevId; return;
            case EquipmentModal.SlotKind.LeftHand:  p.leftHandId   = prevId; return;
            case EquipmentModal.SlotKind.RightHand: p.rightHandId  = prevId; return;
            case EquipmentModal.SlotKind.Armour:    p.armourId     = prevId; return;
            case EquipmentModal.SlotKind.BackupUpgrade1:
            case EquipmentModal.SlotKind.BackupUpgrade2:
                p.backupUpgradeIds = upgradeArr; return;
            case EquipmentModal.SlotKind.LeftHandUpgrade1:
            case EquipmentModal.SlotKind.LeftHandUpgrade2:
                p.leftHandUpgradeIds = upgradeArr; return;
            case EquipmentModal.SlotKind.RightHandUpgrade1:
            case EquipmentModal.SlotKind.RightHandUpgrade2:
                p.rightHandUpgradeIds = upgradeArr; return;
            case EquipmentModal.SlotKind.ArmourUpgrade1:
            case EquipmentModal.SlotKind.ArmourUpgrade2:
                p.armourUpgradeIds = upgradeArr; return;
        }
    }

    private static void SwapForPreview(Player p, EquipmentModal.SlotKind slot, Equipment incoming) {
        EquipmentModal.SetEquipped(p, slot, incoming);
    }
}
