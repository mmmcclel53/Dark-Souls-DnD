using Godot;
using System.Text;

// A panel that overlays the loadout column showing "Current" vs "Proposed" as two
// rarity-framed cards, plus a colour-coded table of stat changes (green better, red
// worse). Confirm equips; Cancel hides the panel.
public partial class ComparisonPanel : PanelContainer
{
    [Signal] public delegate void ConfirmedEventHandler(int slotKind, string incomingInstanceId);
    [Signal] public delegate void CancelledEventHandler();

    [Export] public Label headerLabel;

    [Export] public PanelContainer currentIconFrame;
    [Export] public TextureRect currentIcon;
    [Export] public Label currentNameLabel;
    [Export] public Label currentStatsLabel;

    [Export] public PanelContainer incomingIconFrame;
    [Export] public TextureRect incomingIcon;
    [Export] public Label incomingNameLabel;
    [Export] public Label incomingStatsLabel;

    [Export] public Label physDmgDelta;
    [Export] public Label magDmgDelta;
    [Export] public Label physDefDelta;
    [Export] public Label magDefDelta;
    [Export] public Label dodgeDelta;

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

        SetCard(currentIconFrame, currentIcon, currentNameLabel, currentStatsLabel, current);
        SetCard(incomingIconFrame, incomingIcon, incomingNameLabel, incomingStatsLabel, incoming);

        var (pd, md, pf, mf, dodge) = ComputeDeltas(p, slot, incoming);
        EquipmentModal.SetDeltaLabel(physDmgDelta, pd);
        EquipmentModal.SetDeltaLabel(magDmgDelta, md);
        EquipmentModal.SetDeltaLabel(physDefDelta, pf);
        EquipmentModal.SetDeltaLabel(magDefDelta, mf);
        EquipmentModal.SetDeltaLabel(dodgeDelta, dodge);
    }

    private static void SetCard(PanelContainer frame, TextureRect icon, Label name, Label stats, Equipment e) {
        if (icon != null) icon.Texture = e?.image;
        if (frame != null) {
            var st = new StyleBoxFlat();
            st.BgColor = new Color(0.03f, 0.03f, 0.03f, 0.85f);
            st.BorderColor = e != null ? EquipmentModal.RarityColor(e.rarity) : new Color(0.4f, 0.4f, 0.4f);
            st.SetBorderWidthAll(2);
            st.SetCornerRadiusAll(3);
            frame.AddThemeStyleboxOverride("panel", st);
        }
        if (name != null) {
            name.Text = e?.name ?? "— Empty —";
            name.AddThemeColorOverride("font_color",
                e != null ? EquipmentModal.RarityColor(e.rarity) : new Color(0.6f, 0.6f, 0.6f));
        }
        if (stats != null) stats.Text = StatBlock(e);
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
        if (e == null) return "— empty —";
        var sb = new StringBuilder();
        sb.AppendLine($"{e.type} · {e.rarity}");
        if (e is Weapon w) {
            sb.AppendLine($"Attacks: {(w.attacks?.Count ?? 0)}");
            if (w.dodgeAbility != 0) sb.AppendLine($"Dodge {Signed(w.dodgeAbility)}");
            if (w.upgradeSlots != 0) sb.AppendLine($"Upgrade slots: {w.upgradeSlots}");
        }
        if (e is Armour a) {
            if (a.dodgeAbility != 0) sb.AppendLine($"Dodge {Signed(a.dodgeAbility)}");
            if (a.upgradeSlots != 0) sb.AppendLine($"Upgrade slots: {a.upgradeSlots}");
        }
        string reqs = ReqLine(e);
        if (!string.IsNullOrEmpty(reqs)) sb.Append(reqs);
        return sb.ToString().TrimEnd();
    }

    private static string Signed(int n) => n > 0 ? $"+{n}" : n.ToString();

    private static string ReqLine(Equipment e) {
        var sb = new StringBuilder("Reqs: ");
        bool any = false;
        if (e.strengthReq > 0)     { sb.Append($"STR {e.strengthReq} ");     any = true; }
        if (e.dexterityReq > 0)    { sb.Append($"DEX {e.dexterityReq} ");    any = true; }
        if (e.intelligenceReq > 0) { sb.Append($"INT {e.intelligenceReq} "); any = true; }
        if (e.faithReq > 0)        { sb.Append($"FAI {e.faithReq} ");        any = true; }
        return any ? sb.ToString().TrimEnd() : "";
    }

    // Recomputes the player's best stats with a hypothetical equip swap and returns the
    // deltas vs. the player's current stats. A template (empty id) is temporarily minted
    // so its stats resolve, then removed — keeping the preview accurate without polluting
    // the owned pool.
    private static (int pd, int md, int pf, int mf, int dodge) ComputeDeltas(
            Player p, EquipmentModal.SlotKind slot, Equipment incoming) {
        var (_, pdB) = p.GetBestPhysicalDamage();
        var (_, mdB) = p.GetBestMagicDamage();
        var (_, pfB) = p.GetPhysicalDefense();
        var (_, mfB) = p.GetMagicDefense();
        int dodgeB = p.GetDodge();

        Equipment previewItem = incoming;
        string tempId = null;
        if (string.IsNullOrEmpty(incoming.id)) {
            previewItem = GameManager.MintInstance(incoming.name);
            tempId = previewItem?.id;
            if (previewItem == null) previewItem = incoming;
        }

        string prevId = SlotIdSnapshot(p, slot, out int upgradeIdx, out string[] upgradeArr);
        SwapForPreview(p, slot, previewItem);

        var (_, pdA) = p.GetBestPhysicalDamage();
        var (_, mdA) = p.GetBestMagicDamage();
        var (_, pfA) = p.GetPhysicalDefense();
        var (_, mfA) = p.GetMagicDefense();
        int dodgeA = p.GetDodge();

        RestoreFromSnapshot(p, slot, prevId, upgradeIdx, upgradeArr);
        if (tempId != null) GameManager.RemoveInstance(tempId);

        return (pdA - pdB, mdA - mdB, pfA - pfB, mfA - mfB, dodgeA - dodgeB);
    }

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
