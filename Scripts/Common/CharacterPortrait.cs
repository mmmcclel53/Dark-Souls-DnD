using Godot;
using Godot.Collections;

// One character in the floating party pane. The board token is just an avatar now, so this
// is where a character's endurance, conditions and turn are actually read.
//
// Name sits above the art and the bars below it so nothing covers the portrait, and the
// character whose turn it is grows, gets a white frame and a bobbing arrow, the way a party
// bar marks the active member.
public partial class CharacterPortrait : Control
{
    [Signal] public delegate void PortraitClickedEventHandler();

    [Export] public TextureButton avatarButton;
    [Export] public Control artFrame;
    [Export] public Control damageOverlay;
    [Export] public Label nameLabel;
    [Export] public TickBar endurance;
    [Export] public Panel selectionBorder;
    [Export] public Panel activeBorder;
    // Every condition, top down to the right of the art, at most STATUSES_TALL to a column
    // before the next one starts; the Aggro token under the portrait (Matt, Oct 2026). The arrow
    // stands beyond the columns.
    [Export] public Container statuses;
    [Export] public Control activeArrow;
    [Export] public float statusSize = 18f;
    private const int STATUSES_TALL = 4;
    private StatusIcon aggro;

    // Every character avatar is 3:4, so the frame is too — a square frame cropped the art.
    // A BG3-style strip: the bar carries the active character's full readout, so these
    // only need to say who is in the party, how hurt they are, and whose turn it is.
    [Export] public Vector2 restingSize = new Vector2(60, 80);
    [Export] public Vector2 activeSize = new Vector2(75, 100);
    [Export] public float arrowBob = 3f;
    [Export] public float arrowBobRate = 1.5f;

    private Player player;
    private bool selected;
    private bool wasActive;
    private float arrowTime;
    private float arrowLeft, arrowRight;
    private int shownSpent = -1;
    private int shownDamage = -1;
    private int shownConditions = -1;

    public override void _Ready() {
        if (avatarButton != null)
            avatarButton.Pressed += () => EmitSignal(SignalName.PortraitClicked);
        if (selectionBorder != null) selectionBorder.Visible = false;
        if (activeBorder != null) activeBorder.Visible = false;
        CenterContainer aggroRow = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        aggro = StatusIcon.For(null, statusSize);
        aggro.Visible = false;
        aggroRow.AddChild(aggro);
        AddChild(aggroRow);
        if (activeArrow != null) {
            activeArrow.Visible = false;
            activeArrow.Draw += DrawArrow;
            arrowLeft = activeArrow.OffsetLeft;
            arrowRight = activeArrow.OffsetRight;
        }
    }

    // A white wedge pointing back at the portrait, outlined so it reads over any background.
    private void DrawArrow() {
        Vector2 size = activeArrow.Size;
        Vector2[] points = { new Vector2(0, size.Y * 0.5f), new Vector2(size.X, 0), new Vector2(size.X, size.Y) };
        activeArrow.DrawColoredPolygon(points, Colors.White);
        activeArrow.DrawPolyline(new[] { points[0], points[1], points[2], points[0] }, new Color(0, 0, 0, 0.8f), 1.5f, true);
    }

    public void SetPlayer(Player p) {
        player = p;
        shownSpent = shownDamage = shownConditions = -1;
        Refresh();
    }

    public Player GetPlayer() => player;

    public void SetSelected(bool isSelected) {
        selected = isSelected;
        if (selectionBorder != null) selectionBorder.Visible = isSelected;
    }

    // The pane has a Player, not a PlayerToken, so activation is asked of the encounter.
    private bool IsActivating() {
        PlayerToken active = EncounterManager.characterTurn?.active;
        return active != null && active.player == player;
    }

    public override void _Process(double delta) {
        if (player == null) return;

        bool active = IsActivating();
        if (active != wasActive) {
            wasActive = active;
            ApplyActiveState(active);
        }
        if (active) BobArrow(delta);

        Endurance bar = player.endurance;
        if (bar.staminaSpent == shownSpent && bar.damageTaken == shownDamage
            && ConditionMask() == shownConditions) return;
        Refresh();
    }

    private void ApplyActiveState(bool active) {
        if (artFrame != null) artFrame.CustomMinimumSize = active ? activeSize : restingSize;
        if (activeBorder != null) activeBorder.Visible = active;
        if (activeArrow != null) activeArrow.Visible = active;
    }

    private void BobArrow(double delta) {
        if (activeArrow == null) return;
        arrowTime += (float)delta;
        float nudge = Mathf.Sin(arrowTime * Mathf.Tau * arrowBobRate) * arrowBob;
        activeArrow.OffsetLeft = arrowLeft + arrowShift + nudge;
        activeArrow.OffsetRight = arrowRight + arrowShift + nudge;
    }

    public void Refresh() {
        if (player == null) return;

        Endurance bar = player.endurance;
        bool first = shownConditions < 0;
        shownSpent = bar.staminaSpent;
        shownDamage = bar.damageTaken;
        shownConditions = ConditionMask();

        if (avatarButton != null)
            avatarButton.TextureNormal = player.character?.avatar ?? player.character?.image;
        if (nameLabel != null) nameLabel.Text = player.name;

        endurance?.ShowEndurance(bar);

        // Damage climbs the portrait from the bottom, so how hurt someone is reads without
        // counting ticks.
        if (damageOverlay != null) {
            float lost = (float)bar.damageTaken / Endurance.BOXES;
            damageOverlay.Visible = lost > 0f;
            damageOverlay.AnchorTop = 1f - lost;
            damageOverlay.OffsetTop = 0;
            damageOverlay.OffsetBottom = 0;
        }

        ShowStatuses(first);
        ApplyActiveState(IsActivating());
    }

    private void ShowStatuses(bool first) {
        if (statuses == null) return;
        var had = new System.Collections.Generic.HashSet<EncounterManager.StatusEffect?>();
        foreach (Node child in statuses.GetChildren()) {
            if (child is StatusIcon old) had.Add(old.condition);
            child.QueueFree();
        }
        bool holding = HasAggro();
        if (aggro != null) {
            if (holding && !aggro.Visible && !first) PopIn.Scale(aggro);
            aggro.Visible = holding;
        }
        var now = StatusIcon.Of(player, false);
        foreach (EncounterManager.StatusEffect? status in now) {
            StatusIcon icon = StatusIcon.For(status, statusSize);
            statuses.AddChild(icon);
            // A status landing pops in; the first fill does not.
            if (!first && !had.Contains(status)) PopIn.Scale(icon);
        }
        int columns = (now.Count + STATUSES_TALL - 1) / STATUSES_TALL;
        float wide = columns * (statusSize + 2f);
        statuses.OffsetRight = statuses.OffsetLeft + Mathf.Max(wide, 1f);
        statuses.AnchorBottom = 0f;
        statuses.OffsetBottom = statuses.OffsetTop + STATUSES_TALL * (statusSize + 2f) - 2f;
        arrowShift = wide;
        if (activeArrow != null) {
            activeArrow.OffsetLeft = arrowLeft + arrowShift;
            activeArrow.OffsetRight = arrowRight + arrowShift;
        }
    }

    private float arrowShift;

    // The holder is a board token, which is gone outside an encounter.
    private bool HasAggro() {
        PlayerToken holder = EncounterManager.aggroHolder;
        return GodotObject.IsInstanceValid(holder) && holder.player == player;
    }

    private int ConditionMask() => StatusIcon.Mask(player, HasAggro());
}
