using Godot;
using Godot.Collections;

// One enemy in the Enemy Activation bar. The board token is just an avatar now, so this is
// where an enemy's threat, tier, health and conditions are actually read. The face is a
// close crop of the card art, like the party pane's portraits; clicking it asks for the
// whole card.
//
// Polls its own enemy rather than being pushed at: health and conditions change from several
// places (attacks, pushes, poison ticking at end of activation) and a missed refresh would
// leave a dead enemy looking healthy.
public partial class TurnQueueEntry : VBoxContainer
{
	[Signal] public delegate void CardRequestedEventHandler();

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

	public override void _Ready() {
		if (face != null) face.GuiInput += OnFaceInput;
	}

	private void OnFaceInput(InputEvent @event) {
		if (@event is InputEventMouseButton click && click.Pressed
			&& click.ButtonIndex == MouseButton.Left) {
			face.AcceptEvent();
			EmitSignal(SignalName.CardRequested);
		}
	}

	public void Bind(Enemy boundEnemy) {
		enemy = boundEnemy;
		if (enemy == null) return;

		if (avatar != null) avatar.Texture = enemy.data.GetPortrait();
		if (nameLabel != null) nameLabel.Text = enemy.data.enemyName;
		if (threatLabel != null) threatLabel.Text = enemy.threatLevel.ToString();
		if (threatBadge != null) threatBadge.Visible = true;

		// Tier 2 and 3 get an ember rim; tier 1 is the plain frame.
		if (tierRim != null) tierRim.Visible = enemy.tier > 1;

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
				if (conditionIcons[i] != null) {
					conditionIcons[i].Visible = enemy.HasCondition((EncounterManager.StatusEffect)i);
				}
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
