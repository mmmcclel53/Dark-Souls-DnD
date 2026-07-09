using Godot;
using System.Collections.Generic;

// Left column of the EquipmentModal. Shows the character portrait, "Level N Class",
// attributes laid out horizontally, combat totals, status immunities (as icons), and a
// snapshot of the special abilities granted by currently-equipped gear.
public partial class CharacterSummaryPanel : Control
{
    [Export] public Label playerNameLabel;
    [Export] public TextureRect avatarRect;
    [Export] public Label levelClassLabel;

    // Attribute value cells (the STR/DEX/INT/FAI headers are static, built by the modal).
    [Export] public Label strLabel;
    [Export] public Label dexLabel;
    [Export] public Label intLabel;
    [Export] public Label faiLabel;

    [Export] public Label physDamageLabel;
    [Export] public Label magicDamageLabel;
    [Export] public Label physDefenseLabel;
    [Export] public Label magicDefenseLabel;
    [Export] public Label dodgeLabel;

    [Export] public Container immunitiesRow;   // holds status icons, or a "None" label
    [Export] public Container featuresList;     // holds one row per equipment ability

    private Player player;

    private static readonly Dictionary<EncounterManager.StatusEffect, Texture2D> statusIcons = new();

    public void SetPlayer(Player p) {
        player = p;
        Refresh();
    }

    public void Refresh() {
        if (player == null) {
            if (playerNameLabel != null) playerNameLabel.Text = "— Select a character —";
            if (avatarRect != null) avatarRect.Texture = null;
            if (levelClassLabel != null) levelClassLabel.Text = "";
            ClearStatLabels();
            ClearContainer(immunitiesRow);
            ClearContainer(featuresList);
            return;
        }

        if (playerNameLabel != null) playerNameLabel.Text = player.name;
        if (avatarRect != null) avatarRect.Texture = player.character?.avatar ?? player.character?.image;
        if (levelClassLabel != null)
            levelClassLabel.Text = $"Level {player.GetLevel()} {player.character?.name ?? ""}".TrimEnd();

        if (strLabel != null) strLabel.Text = player.strength.ToString();
        if (dexLabel != null) dexLabel.Text = player.dexterity.ToString();
        if (intLabel != null) intLabel.Text = player.intelligence.ToString();
        if (faiLabel != null) faiLabel.Text = player.faith.ToString();

        var (pdMin, pdMax) = player.GetBestPhysicalDamage();
        var (mdMin, mdMax) = player.GetBestMagicDamage();
        var (pfMin, pfMax) = player.GetPhysicalDefense();
        var (mfMin, mfMax) = player.GetMagicDefense();
        int dodge = player.GetDodge();

        if (physDamageLabel != null)  physDamageLabel.Text  = Range(pdMin, pdMax);
        if (magicDamageLabel != null) magicDamageLabel.Text = Range(mdMin, mdMax);
        if (physDefenseLabel != null) physDefenseLabel.Text = Range(pfMin, pfMax);
        if (magicDefenseLabel != null) magicDefenseLabel.Text = Range(mfMin, mfMax);
        if (dodgeLabel != null)       dodgeLabel.Text        = $"Dodge {dodge}";

        RefreshImmunities();
        RefreshFeatures();
    }

    private void RefreshImmunities() {
        if (immunitiesRow == null) return;
        ClearContainer(immunitiesRow);

        var immunities = player.GetImmunities();
        if (immunities.Count == 0) {
            var none = new Label();
            none.Text = "None";
            none.AddThemeFontSizeOverride("font_size", 12);
            none.AddThemeColorOverride("font_color", new Color(0.58f, 0.56f, 0.50f));
            immunitiesRow.AddChild(none);
            return;
        }

        foreach (var status in immunities) {
            var icon = new TextureRect();
            icon.CustomMinimumSize = new Vector2(26, 26);
            icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            icon.Texture = StatusIcon(status);
            icon.TooltipText = $"Immune to {Titlecase(status)}";
            immunitiesRow.AddChild(icon);
        }
    }

    private void RefreshFeatures() {
        if (featuresList == null) return;
        ClearContainer(featuresList);

        var lines = new List<string>();
        var seen = new HashSet<string>();
        void Add(string text) { if (!string.IsNullOrEmpty(text) && seen.Add(text)) lines.Add(text); }

        foreach (var status in player.GetInflictedStatuses())
            Add($"Inflicts {Titlecase(status)}");
        foreach (var effect in player.GetEquippedPassives())
            Add(DescribeEffect(effect));

        if (lines.Count == 0) {
            var none = new Label();
            none.Text = "None";
            none.HorizontalAlignment = HorizontalAlignment.Center;
            none.AddThemeFontSizeOverride("font_size", 12);
            none.AddThemeColorOverride("font_color", new Color(0.58f, 0.56f, 0.50f));
            featuresList.AddChild(none);
            return;
        }

        foreach (var line in lines) {
            var row = new Label();
            row.Text = $"◆  {line}";
            row.AddThemeFontSizeOverride("font_size", 12);
            row.AddThemeColorOverride("font_color", new Color(0.80f, 0.78f, 0.72f));
            row.AutowrapMode = TextServer.AutowrapMode.Word;
            featuresList.AddChild(row);
        }
    }

    // Short, human-readable label for a passive equipment effect. Returns null for
    // downsides/inert effects we don't want cluttering the snapshot.
    private static string DescribeEffect(EquipmentEffect e) {
        if (e == null || e.type == EquipmentEffect.EffectType.NONE) return null;
        int m = e.magnitude;
        switch (e.type) {
            case EquipmentEffect.EffectType.BONUS_DAMAGE:            return $"Damage +{m}";
            case EquipmentEffect.EffectType.ATTACK_DICE:             return $"Attack +{m} {Titlecase(e.diceColour.ToString())} Dice";
            case EquipmentEffect.EffectType.DEFENSE_DICE:            return $"{DefenceKind(e.defenseKind)} Defence +{m} Dice";
            case EquipmentEffect.EffectType.GRANT_MAGIC:            return "Attacks Gain Magic";
            case EquipmentEffect.EffectType.APPLY_STATUS:          return $"Inflicts {Titlecase(e.inflictedStatus)}";
            case EquipmentEffect.EffectType.IGNORE_PHYSICAL_DEFENSE: return "Ignores Physical Defence";
            case EquipmentEffect.EffectType.PUSH:                   return $"Push {m}";
            case EquipmentEffect.EffectType.HEAL:                   return $"Heal +{m}";
            case EquipmentEffect.EffectType.GAIN_STAMINA:          return $"Stamina +{m}";
            case EquipmentEffect.EffectType.REMOVE_STATUS:         return $"Remove {m} Status";
            case EquipmentEffect.EffectType.BONUS_MOVEMENT:        return $"Move +{m} Node";
            case EquipmentEffect.EffectType.DODGE_DICE:            return $"Dodge +{m} Dice";
            case EquipmentEffect.EffectType.DODGE_RANGE:          return $"Dodge +{m} Node";
            case EquipmentEffect.EffectType.DODGE_STAMINA_MOD:     return m == 0 ? "Free Dodge" : $"Dodge Stamina {Signed(m)}";
            case EquipmentEffect.EffectType.RUN_COST_MOD:         return $"Run Cost {Signed(m)}";
            case EquipmentEffect.EffectType.CANNOT_DODGE:         return "Cannot Dodge";
            case EquipmentEffect.EffectType.CANNOT_MOVE:          return "Cannot Move";
            case EquipmentEffect.EffectType.ATTACK_STAMINA_COST_MOD: return $"Attack Cost {Signed(m)} Stamina";
            case EquipmentEffect.EffectType.BYPASS_TWO_HAND_CHECK: return "Usable With Two-Handed";
            case EquipmentEffect.EffectType.MAY_MOVE_AGGRO:       return "May Move Aggro";
            case EquipmentEffect.EffectType.MAY_TAKE_AGGRO:       return "May Take Aggro";
            case EquipmentEffect.EffectType.REDIRECT_DAMAGE:      return "May Redirect Damage";
            default: return null;   // LOSE_HEALTH / LOSE_STAMINA and unmapped: omit
        }
    }

    private static string DefenceKind(EquipmentEffect.DefenseKind k) {
        switch (k) {
            case EquipmentEffect.DefenseKind.MAGIC: return "Magic";
            case EquipmentEffect.DefenseKind.BOTH:  return "All";
            default: return "Physical";
        }
    }

    private static string Signed(int n) => n > 0 ? $"+{n}" : n.ToString();

    // Collapse a min~max range to a single number when they match (so "0~0" reads "0").
    private static string Range(int min, int max) => min == max ? min.ToString() : $"{min}~{max}";

    private static string Titlecase(EncounterManager.StatusEffect s) => Titlecase(s.ToString());

    private static string Titlecase(string raw) {
        if (string.IsNullOrEmpty(raw)) return raw;
        return char.ToUpper(raw[0]) + raw.Substring(1).ToLower();
    }

    private static Texture2D StatusIcon(EncounterManager.StatusEffect s) {
        if (statusIcons.TryGetValue(s, out var cached)) return cached;
        string file = s switch {
            EncounterManager.StatusEffect.BLEED   => "Bleed",
            EncounterManager.StatusEffect.POISON  => "Poison",
            EncounterManager.StatusEffect.FROST   => "Frost",
            EncounterManager.StatusEffect.STAGGER => "Stagger",
            _ => null,
        };
        Texture2D tex = file == null ? null
            : GD.Load<Texture2D>($"res://Resources/Images/Sprites/Statuses/{file}.png");
        statusIcons[s] = tex;
        return tex;
    }

    private static void ClearContainer(Container c) {
        if (c == null) return;
        foreach (Node child in c.GetChildren()) child.QueueFree();
    }

    private void ClearStatLabels() {
        foreach (var l in new[] { strLabel, dexLabel, intLabel, faiLabel,
                                  physDamageLabel, magicDamageLabel, physDefenseLabel, magicDefenseLabel, dodgeLabel })
            if (l != null) l.Text = "";
    }
}
