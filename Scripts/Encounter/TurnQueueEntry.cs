using Godot;
using Godot.Collections;

// One enemy in the Enemy Activation bar. The board token is just an avatar now, so this is
// where an enemy's threat, tier, health and conditions are actually read. The face is a
// close crop of the card art, like the party pane's portraits; hovering it shows the whole
// card and rings the enemy's token on the board, so the face can be matched to the piece.
//
// Polls its own enemy rather than being pushed at: health and conditions change from several
// places (attacks, pushes, poison ticking at end of activation) and a missed refresh would
// leave a dead enemy looking healthy.
public partial class TurnQueueEntry : VBoxContainer
{
	[Signal] public delegate void HoverStartedEventHandler();
	[Signal] public delegate void HoverEndedEventHandler();

	// A steady pale-gold rim on the hovered enemy's board token: still, so it is never
	// mistaken for the pulsing red of a target or an attacker.
	private static readonly Color HOVER_RIM = new Color(0.98f, 0.86f, 0.55f);

	[Export] public Control face;
	[Export] public TextureRect avatar;
	[Export] public Control damageOverlay;
	[Export] public Control threatBadge;
	[Export] public Label threatLabel;
	[Export] public Label nameLabel;
	[Export] public Control activeRim;
	[Export] public Control attackRim;
	[Export] public Control tierRim;
	[Export] public Control spentVeil;
	[Export] public TickBar health;
	[Export] public Label healthLabel;
	[Export] public Container conditions;

	// Ordered to match EncounterManager.StatusEffect: BLEED, POISON, FROST, STAGGER.
	[Export] public Array<TextureRect> conditionIcons;

	private Enemy enemy;
	public Enemy boundEnemy => enemy;
	private int shownHealth = -1;
	private int shownConditions = -1;
	private TokenHighlight hoverRing;

	public override void _Ready() {
		if (face != null) {
			face.MouseEntered += OnHoverStarted;
			face.MouseExited += OnHoverEnded;
		}
	}

	// The bar is rebuilt freely, and a freed face never reports the mouse leaving.
	public override void _ExitTree() {
		if (hoverRing != null) OnHoverEnded();
	}

	private void OnHoverStarted() {
		NearestMarker.Hover(enemy);
		TokenHighlight.Detach(hoverRing);
		hoverRing = TokenHighlight.Attach(enemy, HOVER_RIM);
		if (hoverRing != null) hoverRing.pulseSpeed = 0f;
		EmitSignal(SignalName.HoverStarted);
	}

	private void OnHoverEnded() {
		NearestMarker.Unhover(enemy);
		TokenHighlight.Detach(hoverRing);
		hoverRing = null;
		EmitSignal(SignalName.HoverEnded);
	}

	public void Bind(Enemy boundEnemy) {
		enemy = boundEnemy;
		if (enemy == null) return;

		if (avatar != null) avatar.Texture = enemy.data.GetPortrait();
		if (nameLabel != null) nameLabel.Text = enemy.data.enemyName;
		if (threatLabel != null) threatLabel.Text = enemy.threatLevel.ToString();
		if (threatBadge != null) threatBadge.Visible = true;

		// Tier 2 and 3 get a coloured rim, matching the world map; tier 1 is the plain frame.
		if (tierRim != null) {
			tierRim.Visible = enemy.tier > 1;
			if (tierRim.GetThemeStylebox("panel") is StyleBoxFlat rim) {
				rim = (StyleBoxFlat)rim.Duplicate();
				rim.BorderColor = Enemy.TierRimColour(enemy.tier);
				tierRim.AddThemeStyleboxOverride("panel", rim);
			}
		}

		shownHealth = -1;
		shownConditions = -1;
		Refresh();
	}

	public void SetState(bool activating, bool spent) {
		if (activeRim != null) activeRim.Visible = activating;
		if (spentVeil != null) spentVeil.Visible = spent;
		// Dim by darkening, never by alpha: the bar is transparent and the board shows through.
		float shade = spent ? 0.5f : (activating ? 1f : 0.85f);
		Modulate = new Color(shade, shade, shade, 1f);
	}

	// Red while this enemy's attack is waiting on a Block or Dodge.
	public void SetAttacking(bool attacking) {
		if (attackRim != null) attackRim.Visible = attacking;
	}

	public override void _Process(double delta) {
		if (enemy == null || !GodotObject.IsInstanceValid(enemy)) return;
		if (enemy.currentHealth == shownHealth && ConditionMask() == shownConditions) return;
		Refresh();
	}

	private void Refresh() {
		if (enemy == null || !GodotObject.IsInstanceValid(enemy)) return;

		bool first = shownConditions < 0;
		shownHealth = enemy.currentHealth;
		shownConditions = ConditionMask();

		health?.ShowHealth(enemy.currentHealth, enemy.maxHealth);
		if (healthLabel != null) healthLabel.Text = $"{enemy.currentHealth}/{enemy.maxHealth}";

		// Damage climbs the portrait from the bottom, so a nearly dead model is nearly solid.
		if (damageOverlay != null && enemy.maxHealth > 0) {
			float lost = 1f - ((float)enemy.currentHealth / enemy.maxHealth);
			damageOverlay.Visible = lost > 0f;
			damageOverlay.AnchorTop = 1f - lost;
			damageOverlay.OffsetTop = 0;
			damageOverlay.OffsetBottom = 0;
		}

		if (conditionIcons != null) {
			for (int i = 0; i < conditionIcons.Count; i++) {
				TextureRect icon = conditionIcons[i];
				if (icon == null) continue;
				bool shown = enemy.HasCondition((EncounterManager.StatusEffect)i);
				if (shown && !icon.Visible && !first) PopIn.Scale(icon);
				icon.Visible = shown;
			}
		}
		if (conditions != null) conditions.Visible = shownConditions != 0;
	}

	private int ConditionMask() {
		if (enemy == null || !GodotObject.IsInstanceValid(enemy)) return 0;

		int mask = 0;
		for (int i = 0; i < 4; i++) {
			if (enemy.HasCondition((EncounterManager.StatusEffect)i)) mask |= 1 << i;
		}
		return mask;
	}
}
