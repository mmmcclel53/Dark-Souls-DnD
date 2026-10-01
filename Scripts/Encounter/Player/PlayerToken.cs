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

	public Heroic.Kind heroic => player?.character?.heroicAction ?? Heroic.Kind.NONE;
	// A boost armed by this character's Heroic Action, waiting for the attack it applies to.
	public Heroic.Kind pendingHeroic { get; private set; }
	// Berserk Charge's free node, which neither costs nor counts as the Walk.
	public int freeSteps { get; private set; }

	private static readonly Color HEAL = new Color(0.55f, 0.85f, 0.5f);

	// The two tokens a character can wear on the board: Aggro at the top-left corner and
	// Nearest at the top-right. Sized in the token's art space, so they scale with it.
	public const string AGGRO_TEXTURE = "res://Resources/Images/Sprites/Statuses/Aggro Token.png";
	private const float BADGE_SIZE = 150f;
	private const float BADGE_OVERHANG = 30f;
	private TextureRect aggroBadge;
	private Control nearestBadge;

	// The token art is a scanned disc with black corners; this cuts it back to the disc.
	// Shared by every place the token is shown.
	private static ShaderMaterial aggroMask;
	public static ShaderMaterial AggroMask {
		get {
			if (aggroMask != null) return aggroMask;
			aggroMask = new ShaderMaterial { Shader = GD.Load<Shader>("res://Resources/Shaders/CircleMask.gdshader") };
			aggroMask.SetShaderParameter("centre", new Vector2(0.506f, 0.5f));
			aggroMask.SetShaderParameter("radius", 0.47f);
			aggroMask.SetShaderParameter("feather", 0.012f);
			return aggroMask;
		}
	}

	public override void _Ready() {
		Pressed += OnClick;
		// The art is clipped to a disc by the Token panel, so it goes on the TextureRect
		// inside it rather than on the button.
		if (avatar != null) avatar.Texture = player.character?.avatar ?? player.character?.image;

		BuildBadges();
		RefreshAggro();
		SetActivating(false);
	}

	private void BuildBadges() {
		aggroBadge = new TextureRect {
			Texture = GD.Load<Texture2D>(AGGRO_TEXTURE),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Position = new Vector2(-BADGE_OVERHANG, -BADGE_OVERHANG),
			Size = new Vector2(BADGE_SIZE, BADGE_SIZE),
			MouseFilter = MouseFilterEnum.Ignore,
			Visible = false,
			Material = AggroMask,
		};
		aggroBadge.AddToGroup(TokenHighlight.ABOVE_RINGS);
		AddChild(aggroBadge);

		nearestBadge = new Control {
			Position = new Vector2(EncounterManager.TOKEN_ART_SIZE - BADGE_SIZE + BADGE_OVERHANG, -BADGE_OVERHANG),
			Size = new Vector2(BADGE_SIZE, BADGE_SIZE),
			MouseFilter = MouseFilterEnum.Ignore,
			Visible = false,
		};
		nearestBadge.AddToGroup(TokenHighlight.ABOVE_RINGS);
		nearestBadge.Draw += DrawNearestBadge;
		AddChild(nearestBadge);
	}

	// A reticle on a dark disc: the ring of the "nearest" behaviour icon, in silver so it
	// never reads as the red Aggro token on the other corner.
	private void DrawNearestBadge() {
		Vector2 centre = nearestBadge.Size * 0.5f;
		float r = nearestBadge.Size.X * 0.5f;
		Color silver = new Color(0.9f, 0.88f, 0.8f);
		nearestBadge.DrawCircle(centre, r, new Color(0.07f, 0.06f, 0.05f, 0.95f));
		nearestBadge.DrawArc(centre, r * 0.92f, 0f, Mathf.Tau, 48, new Color(0.55f, 0.47f, 0.31f), r * 0.1f, true);
		nearestBadge.DrawArc(centre, r * 0.5f, 0f, Mathf.Tau, 48, silver, r * 0.12f, true);
		nearestBadge.DrawCircle(centre, r * 0.12f, silver);
		foreach (Vector2 dir in new[] { Vector2.Up, Vector2.Down, Vector2.Left, Vector2.Right }) {
			nearestBadge.DrawLine(centre + dir * r * 0.58f, centre + dir * r * 0.8f, silver, r * 0.1f, true);
		}
	}

	public void SetNearest(bool nearest) {
		if (nearestBadge != null) nearestBadge.Visible = nearest;
	}

	// Rules p22: a character gains 2 Stamina and the Aggro token when they activate.
	public void BeginActivation() {
		int recovered = Mathf.Min(2, endurance.staminaSpent);
		endurance.GainStamina(2);
		BoardFloat.Number(this, recovered, BoardFloat.STAMINA);
		EncounterManager.SetAggroHolder(this);

		hasWalked = false;
		hasMoved = false;
		hasAttacked = false;
		movementLocked = false;
		usedWeapons.Clear();
		pendingHeroic = Heroic.Kind.NONE;
		freeSteps = 0;

		SetActivating(true);
	}

	// The first node is a free Walk, every later one is a Run at 1 stamina. Frostbite adds 1
	// to each, including the walk (p21/p22).
	public int NextStepCost() {
		if (freeSteps > 0) return 0;
		int cost = hasWalked ? 1 : 0;
		if (HasCondition(EncounterManager.StatusEffect.FROST)) cost += 1;
		return cost;
	}

	// The Heroic free node is taken even after attacking; ordinary movement is not (p22).
	public bool CanStep() => (freeSteps > 0 || !movementLocked) && CanSpend(NextStepCost());

	// Pays for one node of movement. The caller does the reparenting.
	public bool TakeStep() {
		if (!CanStep()) return false;
		if (freeSteps > 0) {
			freeSteps--;
			hasMoved = true;
			return true;
		}
		if (!SpendStamina(NextStepCost())) return false;

		hasWalked = true;
		hasMoved = true;
		return true;
	}

	public bool HasAttackedWith(Weapon weapon) => weapon != null && usedWeapons.Contains(weapon);

	// p22: up to one attack with each weapon in a hand slot. Rapid Strike's additional
	// attack does not use the weapon's one.
	public void RecordAttack(Weapon weapon, bool usesWeapon = true) {
		if (!hasAttacked) {
			hasAttacked = true;
			movementLocked = hasMoved;
		}
		if (usesWeapon && weapon != null) usedWeapons.Add(weapon);
	}

	public bool CanUseHeroicNow => !player.heroicUsed && Heroic.FromBar(heroic);

	public void ArmHeroic(Heroic.Kind kind) {
		pendingHeroic = kind;
		if (kind == Heroic.Kind.BERSERK_CHARGE) freeSteps = 1;
	}

	public void SpendHeroicBoost() {
		pendingHeroic = Heroic.Kind.NONE;
	}

	public void EndActivation() {
		pendingHeroic = Heroic.Kind.NONE;
		freeSteps = 0;
		SetActivating(false);
		ClearVolatileConditions();
	}

	// The Estus Flask clears the whole endurance bar, stamina and damage alike, as a rest
	// does. Drinking it on a clean bar would waste it, so it is only offered with something
	// to restore.
	public bool CanDrinkEstus => !player.estusUsed && (endurance.staminaSpent > 0 || endurance.damageTaken > 0);

	public void DrinkEstus() {
		if (!CanDrinkEstus) return;
		int restored = endurance.staminaSpent + endurance.damageTaken;
		player.estusUsed = true;
		endurance.Clear();
		BoardFloat.Number(this, restored, BoardFloat.STAMINA);
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
		int applied = endurance.TakeDamage(damage);
		BoardFloat.Number(this, -applied, BoardFloat.DAMAGE);
		if (applied >= 3) BoardFx.punch?.Hit(applied);
		CheckDeath();
	}

	// p19: a win removes every black and red cube from the bar.
	public void RestoreEndurance() {
		endurance.Clear();
	}

	public void Heal(int health) {
		int healed = Mathf.Min(health, endurance.damageTaken);
		endurance.GainHealth(health);
		if (healed > 0) BoardFloat.Number(this, healed, HEAL);
	}

	public void RecoverStamina(int stamina) {
		int recovered = Mathf.Min(stamina, endurance.staminaSpent);
		endurance.GainStamina(stamina);
		if (recovered > 0) BoardFloat.Number(this, recovered, BoardFloat.STAMINA);
	}

	// No chrome on the board any more; the portrait pane reads this to size and arrow the
	// active character.
	public bool isActivating { get; private set; }

	public void SetActivating(bool activating) {
		isActivating = activating;
	}

	public void RefreshAggro() {
		if (aggroBadge != null) aggroBadge.Visible = hasAggro;
	}

	public void PopAggro() {
		if (aggroBadge != null) PopIn.Scale(aggroBadge, 1.5f);
	}

	// Where the badge sits on screen, for the handoff to fly between.
	public Vector2 AggroBadgeCentreGlobal() =>
		GetGlobalTransform() * (new Vector2(-BADGE_OVERHANG, -BADGE_OVERHANG) + new Vector2(BADGE_SIZE, BADGE_SIZE) * 0.5f);

	public float AggroBadgeSizeGlobal() => GetGlobalTransform().BasisXform(new Vector2(BADGE_SIZE, 0f)).Length();

	public void ApplyCondition(EncounterManager.StatusEffect condition) {
		if (condition == EncounterManager.StatusEffect.NONE) return;
		if (player.GetImmunities().Contains(condition)) return;
		if (conditions.Add(condition)) ConditionBurst.Play(this, condition);
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
