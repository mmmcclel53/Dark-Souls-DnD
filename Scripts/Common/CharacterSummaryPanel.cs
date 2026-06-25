using Godot;

// Left column of the EquipmentModal. Shows name, avatar, class, description,
// stat tiers, and damage/defense/dodge summary from the player's currently
// equipped instances. Supports a preview override for hover comparisons.
public partial class CharacterSummaryPanel : Control
{
    [Export] public Label playerNameLabel;
    [Export] public TextureRect avatarRect;
    [Export] public Label classNameLabel;
    [Export] public Label descriptionLabel;

    [Export] public Label strLabel;
    [Export] public Label dexLabel;
    [Export] public Label intLabel;
    [Export] public Label faiLabel;

    [Export] public Label physDamageLabel;
    [Export] public Label magicDamageLabel;
    [Export] public Label physDefenseLabel;
    [Export] public Label magicDefenseLabel;
    [Export] public Label dodgeLabel;

    private Player player;

    public void SetPlayer(Player p) {
        player = p;
        Refresh();
    }

    public void Refresh() {
        if (player == null) {
            if (playerNameLabel != null) playerNameLabel.Text = "— Select a character —";
            if (avatarRect != null) avatarRect.Texture = null;
            if (classNameLabel != null) classNameLabel.Text = "";
            if (descriptionLabel != null) descriptionLabel.Text = "";
            ClearStatLabels();
            return;
        }
        if (playerNameLabel != null) playerNameLabel.Text = player.name;
        if (avatarRect != null) avatarRect.Texture = player.character?.avatar ?? player.character?.image;
        if (classNameLabel != null) classNameLabel.Text = player.character?.name ?? "";
        if (descriptionLabel != null) descriptionLabel.Text = player.character?.description ?? "";

        if (strLabel != null) strLabel.Text = $"STR {player.strength}";
        if (dexLabel != null) dexLabel.Text = $"DEX {player.dexterity}";
        if (intLabel != null) intLabel.Text = $"INT {player.intelligence}";
        if (faiLabel != null) faiLabel.Text = $"FAI {player.faith}";

        var (pdMin, pdMax) = player.GetBestPhysicalDamage();
        var (mdMin, mdMax) = player.GetBestMagicDamage();
        var (pfMin, pfMax) = player.GetPhysicalDefense();
        var (mfMin, mfMax) = player.GetMagicDefense();
        int dodge = player.GetDodge();

        if (physDamageLabel != null)  physDamageLabel.Text  = $"{pdMin}~{pdMax} Physical Damage";
        if (magicDamageLabel != null) magicDamageLabel.Text = $"{mdMin}~{mdMax} Magic Damage";
        if (physDefenseLabel != null) physDefenseLabel.Text = $"{pfMin}~{pfMax} Physical Defense";
        if (magicDefenseLabel != null) magicDefenseLabel.Text = $"{mfMin}~{mfMax} Magic Defense";
        if (dodgeLabel != null)       dodgeLabel.Text        = $"{dodge} Dodge";
    }

    private void ClearStatLabels() {
        if (strLabel != null) strLabel.Text = "";
        if (dexLabel != null) dexLabel.Text = "";
        if (intLabel != null) intLabel.Text = "";
        if (faiLabel != null) faiLabel.Text = "";
        if (physDamageLabel != null) physDamageLabel.Text = "";
        if (magicDamageLabel != null) magicDamageLabel.Text = "";
        if (physDefenseLabel != null) physDefenseLabel.Text = "";
        if (magicDefenseLabel != null) magicDefenseLabel.Text = "";
        if (dodgeLabel != null) dodgeLabel.Text = "";
    }
}
