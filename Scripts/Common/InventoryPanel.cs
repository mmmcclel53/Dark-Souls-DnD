using Godot;
using System.Collections.Generic;
using System.Linq;

// Right column. Lists every owned instance not currently equipped by any player.
// Supports search (name/type/rarity), filter (Type, Rarity), sort, and rarity-coloured borders.
// Greys out items whose stat reqs the selected character doesn't meet.
public partial class InventoryPanel : Control
{
    [Signal] public delegate void EquipmentClickedEventHandler(string instanceId);

    [Export] public LineEdit searchField;
    [Export] public OptionButton typeFilter;
    [Export] public OptionButton rarityFilter;
    [Export] public OptionButton sortSelect;
    [Export] public GridContainer grid;

    private Player viewer;
    private EquipmentModal.SlotKind slotFilter = EquipmentModal.SlotKind.None;

    private const int TILE_W = 52;
    private const int TILE_H = 78;

    private static readonly Color RARITY_STARTER   = new Color(0.55f, 0.55f, 0.55f);
    private static readonly Color RARITY_COMMON    = new Color(0.85f, 0.85f, 0.85f);
    private static readonly Color RARITY_RARE      = new Color(0.35f, 0.65f, 1.0f);
    private static readonly Color RARITY_LEGENDARY = new Color(0.9f, 0.65f, 0.15f);
    private static readonly Color RARITY_EPIC      = new Color(0.75f, 0.35f, 0.95f);

    public override void _Ready() {
        if (typeFilter != null) {
            typeFilter.Clear();
            typeFilter.AddItem("All Types", -1);
            foreach (Equipment.EquipmentType t in System.Enum.GetValues(typeof(Equipment.EquipmentType)))
                typeFilter.AddItem(t.ToString(), (int)t);
            typeFilter.ItemSelected += _ => Refresh();
        }
        if (rarityFilter != null) {
            rarityFilter.Clear();
            rarityFilter.AddItem("All Rarities", -1);
            foreach (Equipment.Rarity r in System.Enum.GetValues(typeof(Equipment.Rarity)))
                rarityFilter.AddItem(r.ToString(), (int)r);
            rarityFilter.ItemSelected += _ => Refresh();
        }
        if (sortSelect != null) {
            sortSelect.Clear();
            sortSelect.AddItem("Newest", 0);
            sortSelect.AddItem("Name A-Z", 1);
            sortSelect.AddItem("Rarity", 2);
            sortSelect.AddItem("Phys. Damage", 3);
            sortSelect.AddItem("Phys. Defense", 4);
            sortSelect.AddItem("Mag. Damage", 5);
            sortSelect.AddItem("Mag. Defense", 6);
            sortSelect.ItemSelected += _ => Refresh();
        }
        if (searchField != null) {
            searchField.TextChanged += _ => Refresh();
        }
    }

    public void SetSlotFilter(EquipmentModal.SlotKind slot, Player viewer) {
        this.slotFilter = slot;
        this.viewer = viewer;
        Refresh();
    }

    public void Refresh() {
        if (grid == null) return;
        foreach (Node child in grid.GetChildren()) child.QueueFree();

        // Build the set of equipped instance ids across the entire party.
        var equippedIds = new HashSet<string>();
        if (CampaignManager.Players != null) {
            foreach (var p in CampaignManager.Players) CollectEquippedIds(p, equippedIds);
        }

        var items = GameManager.GetAllInstances()
            .Where(e => e != null && !equippedIds.Contains(e.id))
            .ToList();

        // Search
        string query = searchField?.Text?.Trim().ToLowerInvariant() ?? "";
        if (!string.IsNullOrEmpty(query)) {
            items = items.Where(e =>
                (e.name?.ToLowerInvariant().Contains(query) ?? false) ||
                e.type.ToString().ToLowerInvariant().Contains(query) ||
                e.rarity.ToString().ToLowerInvariant().Contains(query)
            ).ToList();
        }

        // Type filter
        int typeId = typeFilter != null && typeFilter.Selected >= 0 ? (int)typeFilter.GetItemId(typeFilter.Selected) : -1;
        if (typeId >= 0) items = items.Where(e => (int)e.type == typeId).ToList();

        // Rarity filter
        int rarityId = rarityFilter != null && rarityFilter.Selected >= 0 ? (int)rarityFilter.GetItemId(rarityFilter.Selected) : -1;
        if (rarityId >= 0) items = items.Where(e => (int)e.rarity == rarityId).ToList();

        // Slot compatibility (auto, when a slot is selected)
        if (slotFilter != EquipmentModal.SlotKind.None)
            items = items.Where(e => EquipmentModal.SlotAccepts(slotFilter, e, viewer)).ToList();

        // Sort
        int sortId = sortSelect != null && sortSelect.Selected >= 0 ? (int)sortSelect.GetItemId(sortSelect.Selected) : 0;
        items = ApplySort(items, sortId);

        // Render
        foreach (var item in items) {
            var tile = BuildTile(item);
            grid.AddChild(tile);
        }
    }

    private List<Equipment> ApplySort(List<Equipment> items, int sortId) {
        switch (sortId) {
            case 1: return items.OrderBy(e => e.name).ToList();
            case 2: return items.OrderByDescending(e => (int)e.rarity).ToList();
            case 3: return items.OrderByDescending(e => MaxDamage(e, false)).ToList();
            case 4: return items.OrderByDescending(e => MaxDefense(e, false)).ToList();
            case 5: return items.OrderByDescending(e => MaxDamage(e, true)).ToList();
            case 6: return items.OrderByDescending(e => MaxDefense(e, true)).ToList();
            default: return items.OrderByDescending(e => GameManager.OwnedOrderIndex(e.id)).ToList();
        }
    }

    private Control BuildTile(Equipment item) {
        var panel = new PanelContainer();
        panel.CustomMinimumSize = new Vector2(TILE_W, TILE_H);

        var border = new StyleBoxFlat();
        border.BgColor = new Color(0.07f, 0.07f, 0.07f, 0.85f);
        border.BorderColor = RarityColor(item.rarity);
        border.SetBorderWidthAll(2);
        border.SetCornerRadiusAll(4);
        panel.AddThemeStyleboxOverride("panel", border);

        var btn = new TextureButton();
        btn.IgnoreTextureSize = true;
        btn.StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered;
        btn.TextureNormal = item.image;
        btn.CustomMinimumSize = new Vector2(TILE_W - 4, TILE_H - 4);

        bool meetsReqs = MeetsRequirements(item, viewer);
        if (!meetsReqs) {
            btn.Modulate = new Color(0.45f, 0.45f, 0.45f, 0.8f);
            btn.Disabled = true;
        }

        btn.Pressed += () => EmitSignal(SignalName.EquipmentClicked, item.id);

        // Tooltip with the basics.
        btn.TooltipText = $"{item.name}\n{item.type} · {item.rarity}";

        panel.AddChild(btn);
        return panel;
    }

    private static bool MeetsRequirements(Equipment e, Player p) {
        if (p == null) return true;
        return p.strength >= e.strengthReq
            && p.dexterity >= e.dexterityReq
            && p.intelligence >= e.intelligenceReq
            && p.faith >= e.faithReq;
    }

    private static int MaxDamage(Equipment e, bool magic) {
        if (e is not Weapon w || w.attacks == null) return 0;
        int best = 0;
        foreach (PlayerMove m in w.attacks) {
            if (m?.damage == null || m.isMagic != magic) continue;
            int max = m.modifier;
            foreach (Dice d in m.damage) max += MaxFace(d);
            if (max > best) best = max;
        }
        return best;
    }

    private static int MaxDefense(Equipment e, bool magic) {
        int max = 0;
        Godot.Collections.Array<Dice> dice = null;
        if (e is Weapon w) dice = magic ? w.magicDefense : w.physicalDefense;
        if (e is Armour a) dice = magic ? a.magicDefense : a.physicalDefense;
        if (dice != null) foreach (Dice d in dice) max += MaxFace(d);
        return max;
    }

    private static int MaxFace(Dice d) {
        if (d?.dice == null || d.dice.Length == 0) return 0;
        int max = int.MinValue;
        foreach (int f in d.dice) if (f > max) max = f;
        return max == int.MinValue ? 0 : max;
    }

    private static void CollectEquippedIds(Player p, HashSet<string> outIds) {
        if (p == null) return;
        AddNonEmpty(outIds, p.backupSlotId);
        AddNonEmpty(outIds, p.leftHandId);
        AddNonEmpty(outIds, p.rightHandId);
        AddNonEmpty(outIds, p.armourId);
        foreach (var arr in new[] { p.backupUpgradeIds, p.leftHandUpgradeIds, p.rightHandUpgradeIds, p.armourUpgradeIds }) {
            if (arr == null) continue;
            foreach (var id in arr) AddNonEmpty(outIds, id);
        }
    }

    private static void AddNonEmpty(HashSet<string> s, string id) {
        if (!string.IsNullOrEmpty(id)) s.Add(id);
    }

    private static Color RarityColor(Equipment.Rarity r) {
        switch (r) {
            case Equipment.Rarity.STARTER:   return RARITY_STARTER;
            case Equipment.Rarity.COMMON:    return RARITY_COMMON;
            case Equipment.Rarity.RARE:      return RARITY_RARE;
            case Equipment.Rarity.LEGENDARY: return RARITY_LEGENDARY;
            case Equipment.Rarity.EPIC:      return RARITY_EPIC;
            default: return RARITY_COMMON;
        }
    }
}
