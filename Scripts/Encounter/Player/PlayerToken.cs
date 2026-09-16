using Godot;
using Godot.Collections;
using System.Collections.Generic;

// A character on the board. Mirrors Enemy: the Player resource is the campaign-persistent
// half (stats, equipment, level) and this node owns everything that only exists inside an
// encounter — position, the endurance bar, conditions and the aggro token.
public partial class PlayerToken : TextureButton
{

	[Export] public Player player;

	[Export] public Control activeRim;
	[Export] public Control aggroMarker;
	[Export] public Container enduranceBoxes;
	[Export] public Label enduranceLabel;
	[Export] public Control enduranceNode;
	[Export] public Control statusesNode;

	// Ordered to match EncounterManager.StatusEffect: BLEED, POISON, FROST, STAGGER.
	[Export] public Array<TextureRect> conditionIcons;

	[Export] public Color spentColour = new Color(0.10f, 0.09f, 0.07f);
	[Export] public Color damageColour = new Color(0.64f, 0.17f, 0.13f);
	[Export] public Color freeColour = new Color(0.33f, 0.30f, 0.24f);

	[Export] public float compactWidthPx = 64f;

	public Endurance endurance => player.endurance;
	public bool hasAggro => EncounterManager.aggroHolder == this;

	private readonly HashSet<EncounterManager.StatusEffect> conditions = new HashSet<EncounterManager.StatusEffect>();

	// Activation state, all reset by BeginActivation.
	public bool hasWalked { get; private set; }
	public bool hasMoved { get; private set; }
	public bool hasAttacked { get; private set; }

	// p22: movement happens entirely before or entirely after the attacking, never split.
	// Latched on the first attack from whether the character had already moved.
	public bool movementLocked { get; private set; }

	private readonly HashSet<Weapon> usedWeapons = new HashSet<Weapon>();

	private bool isCompact;
	private float lastRenderedWidth = -1f;

	public override void _Ready() {
		Pressed += OnClick;
		TextureNormal = player.character?.avatar ?? player.character?.image;

		RefreshEndurance();
		RefreshConditions();
		RefreshAggro();
		SetActivating(false);
	}

	// Rules p22: a character gains 2 Stamina and the Aggro token when they activate.
	public void BeginActivation() {
		endurance.GainStamina(2);
		EncounterManager.SetAggroHolder(this);

		hasWalked = false;
		hasMoved = false;
		hasAttacked = false;
		movementLocked = false;
		usedWeapons.Clear();

		SetActivating(true);
		RefreshEndurance();
	}

	// The first node is a free Walk, every later one is a Run at 1 stamina. Frostbite adds 1
	// to each, including the walk (p21/p22).
	public int NextStepCost() {
		int cost = hasWalked ? 1 : 0;
		if (HasCondition(EncounterManager.StatusEffect.FROST)) cost += 1;
		return cost;
	}

	public bool CanStep() => !movementLocked && CanSpend(NextStepCost());

	// Pays for one node of movement. The caller does the reparenting.
	public bool TakeStep() {
		if (!CanStep()) return false;
		if (!SpendStamina(NextStepCost())) return false;

		hasWalked = true;
		hasMoved = true;
		return true;
	}

	public bool HasAttackedWith(Weapon weapon) => weapon != null && usedWeapons.Contains(weapon);

	// p22: up to one attack with each weapon in a hand slot.
	public void RecordAttack(Weapon weapon) {
		if (!hasAttacked) {
			hasAttacked = true;
			movementLocked = hasMoved;
		}
		if (weapon != null) usedWeapons.Add(weapon);
	}

	public void EndActivation() {
		SetActivating(false);
		ClearVolatileConditions();
	}

	public bool CanSpend(int stamina) => endurance.CanSpend(stamina);

	public bool SpendStamina(int stamina) {
		if (!endurance.SpendStamina(stamina)) return false;
		RefreshEndurance();
		CheckDeath();
		return true;
	}

	public void ApplyDamage(int damage) {
		if (damage <= 0) return;

		// Bleed adds 2 to the damage suffered, then comes off (p21).
		if (conditions.Remove(EncounterManager.StatusEffect.BLEED)) {
			damage += 2;
			RefreshConditions();
		}
		endurance.TakeDamage(damage);
		RefreshEndurance();
		CheckDeath();
	}

	// p19: a win removes every black and red cube from the bar.
	public void RestoreEndurance() {
		endurance.Clear();
		RefreshEndurance();
	}

	public void Heal(int health) {
		endurance.GainHealth(health);
		RefreshEndurance();
	}

	public void SetActivating(bool activating) {
		if (activeRim != null) activeRim.Visible = activating;
	}

	public void RefreshAggro() {
		if (aggroMarker != null) aggroMarker.Visible = hasAggro;
	}

	public void ApplyCondition(EncounterManager.StatusEffect condition) {
		if (condition == EncounterManager.StatusEffect.NONE) return;
		if (player.GetImmunities().Contains(condition)) return;
		conditions.Add(condition);
		RefreshConditions();
	}

	public void RemoveCondition(EncounterManager.StatusEffect condition) {
		conditions.Remove(condition);
		RefreshConditions();
	}

	public bool HasCondition(EncounterManager.StatusEffect condition) => conditions.Contains(condition);

	// Rules p21: poison, frostbite and stagger come off at the end of the model's own
	// activation. Bleed stays until the model next suffers damage.
	// p21: every remaining condition comes off when the encounter ends, bleed included.
	public void ClearAllConditions() {
		conditions.Clear();
		RefreshConditions();
	}

	public void ClearVolatileConditions() {
		if (conditions.Contains(EncounterManager.StatusEffect.POISON)) ApplyDamage(1);
		conditions.Remove(EncounterManager.StatusEffect.POISON);
		conditions.Remove(EncounterManager.StatusEffect.FROST);
		conditions.Remove(EncounterManager.StatusEffect.STAGGER);
		RefreshConditions();
	}

	// Rules p20: all ten boxes covered kills the character, and p19 makes that an
	// immediate party defeat. The turn loop owns what happens next.
	private void CheckDeath() {
		if (!endurance.isDead || EncounterManager.partyDefeated) return;

		EncounterManager.partyDefeated = true;
		EncounterManager.deathGridIndex = EncounterManager.GridIndexOf((Node2D)GetParent());
	}

	private void RefreshEndurance() {
		if (enduranceLabel != null) {
			enduranceLabel.Text = endurance.free.ToString();
		}
		if (enduranceBoxes == null) return;

		int box = 0;
		foreach (Node child in enduranceBoxes.GetChildren()) {
			if (child is not ColorRect rect) continue;
			// Black fills from the left, red from the right, exactly like the cubes.
			bool spent = box < endurance.staminaSpent;
			bool wounded = box >= Endurance.BOXES - endurance.damageTaken;
			rect.Color = spent ? spentColour : (wounded ? damageColour : freeColour);
			box++;
		}
	}

	private void RefreshConditions() {
		if (conditionIcons == null) return;
		for (int i = 0; i < conditionIcons.Count; i++) {
			if (conditionIcons[i] != null) {
				conditionIcons[i].Visible = conditions.Contains((EncounterManager.StatusEffect)i);
			}
		}
	}

	public void OnClick() {
		EncounterManager.selectedPlayer = this;
	}

	public override void _Process(double delta) {
		float renderedWidth = Size.X * GetGlobalTransformWithCanvas().Scale.X;
		if (Mathf.Abs(renderedWidth - lastRenderedWidth) > 0.5f) {
			lastRenderedWidth = renderedWidth;
			isCompact = renderedWidth < compactWidthPx;
		}

		bool showDetail = EncounterManager.showEnemyInfo;
		if (enduranceNode != null) enduranceNode.Visible = showDetail;
		if (statusesNode != null) statusesNode.Visible = showDetail && !isCompact;
	}
}
