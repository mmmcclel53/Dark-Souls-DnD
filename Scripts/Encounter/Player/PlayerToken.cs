using Godot;
using Godot.Collections;
using System.Collections.Generic;

// A character on the board. Mirrors Enemy: the Player resource is the campaign-persistent
// half (stats, equipment, level) and this node owns everything that only exists inside an
// encounter — position, the endurance bar, conditions and the aggro token.
public partial class PlayerToken : TextureButton
{

	[Export] public Player player;

	[Export] public TextureRect avatar;
	public Endurance endurance => player.endurance;
	public bool hasAggro => EncounterManager.aggroHolder == this;

	private HashSet<EncounterManager.StatusEffect> conditions => player.conditions;

	// Activation state, all reset by BeginActivation.
	public bool hasWalked { get; private set; }
	public bool hasMoved { get; private set; }
	public bool hasAttacked { get; private set; }

	// p22: movement happens entirely before or entirely after the attacking, never split.
	// Latched on the first attack from whether the character had already moved.
	public bool movementLocked { get; private set; }

	private readonly HashSet<Weapon> usedWeapons = new HashSet<Weapon>();

	public override void _Ready() {
		Pressed += OnClick;
		// The art is clipped to a disc by the Token panel, so it goes on the TextureRect
		// inside it rather than on the button.
		if (avatar != null) avatar.Texture = player.character?.avatar ?? player.character?.image;

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
		CheckDeath();
		return true;
	}

	public void ApplyDamage(int damage) {
		if (damage <= 0) return;

		// Bleed adds 2 to the damage suffered, then comes off (p21).
		if (conditions.Remove(EncounterManager.StatusEffect.BLEED)) {
			damage += 2;
		}
		endurance.TakeDamage(damage);
		CheckDeath();
	}

	// p19: a win removes every black and red cube from the bar.
	public void RestoreEndurance() {
		endurance.Clear();
	}

	public void Heal(int health) {
		endurance.GainHealth(health);
	}

	// No chrome on the board any more; the portrait pane reads this to size and arrow the
	// active character.
	public bool isActivating { get; private set; }

	public void SetActivating(bool activating) {
		isActivating = activating;
	}

	public void RefreshAggro() {}

	public void ApplyCondition(EncounterManager.StatusEffect condition) {
		if (condition == EncounterManager.StatusEffect.NONE) return;
		if (player.GetImmunities().Contains(condition)) return;
		conditions.Add(condition);
	}

	public void RemoveCondition(EncounterManager.StatusEffect condition) {
		conditions.Remove(condition);
	}

	public bool HasCondition(EncounterManager.StatusEffect condition) => conditions.Contains(condition);

	// Rules p21: poison, frostbite and stagger come off at the end of the model's own
	// activation. Bleed stays until the model next suffers damage.
	// p21: every remaining condition comes off when the encounter ends, bleed included.
	public void ClearAllConditions() {
		conditions.Clear();
	}

	public void ClearVolatileConditions() {
		if (conditions.Contains(EncounterManager.StatusEffect.POISON)) ApplyDamage(1);
		conditions.Remove(EncounterManager.StatusEffect.POISON);
		conditions.Remove(EncounterManager.StatusEffect.FROST);
		conditions.Remove(EncounterManager.StatusEffect.STAGGER);
	}

	// Rules p20: all ten boxes covered kills the character, and p19 makes that an
	// immediate party defeat. The turn loop owns what happens next.
	private void CheckDeath() {
		if (!endurance.isDead || EncounterManager.partyDefeated) return;

		EncounterManager.partyDefeated = true;
		EncounterManager.deathGridIndex = EncounterManager.GridIndexOf((Node2D)GetParent());
	}

	public void OnClick() {
		EncounterManager.selectedPlayer = this;
		(GetParent()?.GetParent() as GameNode)?.Press();
	}

}
