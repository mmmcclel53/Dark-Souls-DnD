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
	// The last node step this character took: their push on an enemy sharing their node
	// carries on in this direction (Pushing).
	public Vector2I lastStep { get; set; }
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

	// Tiny Being's Ring: the start-of-activation gain waiting on the wearer's choice of all
	// stamina or one of it as health. 0 when nothing is waiting.
	public int pendingStartGain { get; private set; }
	// Attack rolls made in this activation, for the Ring of Favour.
	public int attackRolls;

	// Rules p22: a character gains 2 Stamina and the Aggro token when they activate.
	public void BeginActivation() {
		int gain = StartGain();
		if (player.HasRing(Ring.Effect.TINY_BEING) && endurance.damageTaken > 0) {
			pendingStartGain = gain;
		} else {
			pendingStartGain = 0;
			int recovered = Mathf.Min(gain, endurance.staminaSpent);
			endurance.GainStamina(gain);
			BoardFloat.Number(this, recovered, BoardFloat.STAMINA);
		}
		EncounterManager.SetAggroHolder(this);
		attackRolls = 0;

		hasWalked = false;
		walkLeft = WalkNodes();
		hasMoved = false;
		magicThisActivation = false;
		bonusThisActivation = 0;
		hasAttacked = false;
		movementLocked = false;
		usedWeapons.Clear();
		pendingHeroic = Heroic.Kind.NONE;
		freeSteps = 0;

		SetActivating(true);
	}

	// Mask of the Child: 3 instead of 2. Chloranthy Ring: 4 instead of 2, for the wearer and
	// anyone on the wearer's node. Both say "instead of 2", so the larger stands.
	private int StartGain() {
		int gain = player.HasPassive(EquipmentEffect.EffectType.GAIN_STAMINA, EquipmentEffect.Condition.ON_START_ACTIVATION) ? 3 : 2;
		if (player.HasRing(Ring.Effect.CHLORANTHY)) return 4;
		if (GetParent()?.GetParent() is Control node) {
			foreach (Node2D model in EncounterManager.GetPlayersInNode(node, "Player")) {
				if (EncounterManager.GetPlayerToken(model)?.player?.HasRing(Ring.Effect.CHLORANTHY) ?? false) return 4;
			}
		}
		return gain;
	}

	// Health that one of this character's equipment cards gives (a spell, a ring, armour).
	// Cleric Armour: "they gain +1 health".
	public void GearHeal(PlayerToken target, int amount) {
		if (target == null || amount <= 0) return;
		if (player.HasPassive(EquipmentEffect.EffectType.HEAL, EquipmentEffect.Condition.IF_GAIN_HEALTH_ACTIVATED)) amount += 1;
		target.Heal(amount);
	}

	// Gear that answers this character using their Heroic Action (Gold-Hemmed Black Robes,
	// Embraced Armour of Favor; Crimson Robes is asked for in CharacterTurn, as it is a choice).
	public void OnHeroicUsed() {
		if (player.HasPassive(EquipmentEffect.EffectType.GAIN_STAMINA, EquipmentEffect.Condition.IF_HEROIC_ABILITY_ACTIVATED)) RecoverStamina(1);
		if (player.HasPassive(EquipmentEffect.EffectType.HEAL, EquipmentEffect.Condition.IF_HEROIC_ABILITY_ACTIVATED)) GearHeal(this, 1);
	}

	// Losing health (Bewitched Alonne Sword): not damage, so no Bleed, but it can overflow the
	// bar and kill (Matt).
	public void LoseHealth(int health) {
		if (health <= 0) return;
		int applied = endurance.TakeDamage(health);
		BoardFloat.Number(this, -applied, BoardFloat.DAMAGE);
		CheckDeath();
	}

	// Tiny Being's Ring: the waiting gain, as all stamina or one of it as health.
	public void TakeStartGain(bool asHealth) {
		int gain = pendingStartGain;
		pendingStartGain = 0;
		if (gain <= 0) return;
		if (asHealth) {
			RecoverStamina(gain - 1);
			GearHeal(this, 1);
		} else {
			RecoverStamina(gain);
		}
	}

	// Nodes of the free Walk left this activation. Dancer Armour walks 2; Havel's Armour
	// cannot walk at all, so every step is a Run.
	private int walkLeft;

	// Great Magic Weapon: this activation's attacks are magic, and maybe +1 damage.
	public bool magicThisActivation;
	public int bonusThisActivation;

	private int WalkNodes() {
		if (player.HasPassive(EquipmentEffect.EffectType.CANNOT_MOVE)) return 0;
		return 1 + (player.Passive(EquipmentEffect.EffectType.BONUS_MOVEMENT)?.magnitude ?? 0);
	}

	// The Walk is free, every other node is a Run at 1 stamina (Catarina Armour: 2). Frostbite
	// adds 1 to each, including the walk (p21/p22).
	public int NextStepCost() {
		if (freeSteps > 0) return 0;
		int cost = walkLeft > 0 ? 0 : 1 + (player.Passive(EquipmentEffect.EffectType.RUN_COST_MOD)?.magnitude ?? 0);
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

		if (walkLeft > 0) walkLeft--;
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

	// The rings that pay out at the end of an activation do so before Poison ticks.
	public void EndActivation() {
		pendingHeroic = Heroic.Kind.NONE;
		freeSteps = 0;
		pendingStartGain = 0;
		SetActivating(false);
		magicThisActivation = false;
		bonusThisActivation = 0;
		if (player.HasRing(Ring.Effect.SUN_PRINCESS)) GearHeal(this, 1);
		// Lothric Knight Armour: 1 health for each enemy on this node.
		if (player.HasPassive(EquipmentEffect.EffectType.HEAL, EquipmentEffect.Condition.ON_END_ACTIVATION) && GetParent()?.GetParent() is Control here) {
			int enemies = 0;
			foreach (Node2D model in EncounterManager.GetPlayersInNode(here, "Enemy")) if (EncounterManager.GetEnemy(model) != null) enemies++;
			GearHeal(this, enemies);
		}
		if (player.HasRing(Ring.Effect.RING_OF_FAVOUR) && attackRolls >= 2) RecoverStamina(1);
		ClearVolatileConditions();
	}

	// Divine Blessing: once per rest, in the wearer's own activation, every red cube and
	// every condition comes off. Like Estus, only offered with something to take off.
	public bool CanUseDivineBlessing => player.HasRing(Ring.Effect.DIVINE_BLESSING) && !player.divineBlessingUsed
		&& (endurance.damageTaken > 0 || conditions.Count > 0);

	public void UseDivineBlessing() {
		if (!CanUseDivineBlessing) return;
		player.divineBlessingUsed = true;
		Heal(endurance.damageTaken);
		conditions.Clear();
	}

	// Damage the character does to themselves (Dusk Crown Ring). Only an enemy attack or a
	// condition kills (Matt's ruling), so this stops at a full bar.
	public void SufferOwnDamage(int damage) {
		if (damage <= 0) return;
		if (conditions.Remove(EncounterManager.StatusEffect.BLEED)) damage += 2;
		int applied = endurance.TakeDamage(Mathf.Min(damage, endurance.free));
		BoardFloat.Number(this, -applied, BoardFloat.DAMAGE);
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
		// Spending can only fill the bar, never overflow it, so it can never kill.
		return endurance.SpendStamina(stamina);
	}

	private const float HEAVY_BLOW_PRESENCE = 2f;

	// `presence` is the attacker's size, when an enemy's blow is what lands (ScreenPunch).
	public void ApplyDamage(int damage, float presence = 1f) {
		if (damage <= 0) return;

		// Bleed adds 2 to the damage suffered, then comes off (p21).
		if (conditions.Remove(EncounterManager.StatusEffect.BLEED)) {
			damage += 2;
		}
		int applied = endurance.TakeDamage(damage);
		BoardFloat.Number(this, -applied, BoardFloat.DAMAGE);
		// A big enemy's blow is felt even when it is light.
		if (applied >= 3 || (applied > 0 && presence >= HEAVY_BLOW_PRESENCE)) BoardFx.punch?.Hit(applied, presence);
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

	// Damage that overflows the bar kills the character (Matt's ruling, in place of p20's
	// "all ten boxes covered"), and p19 makes that an immediate party defeat. The turn
	// loop owns what happens next.
	private void CheckDeath() {
		if (!endurance.isDead || EncounterManager.partyDefeated) return;

		EncounterManager.partyDefeated = true;
		EncounterManager.deathGridIndex = EncounterManager.GridIndexOf((Node2D)GetParent());
	}

	// The board token of a party member, when they are on the board.
	public static PlayerToken Of(Player player) {
		foreach (Node2D model in EncounterManager.players) {
			PlayerToken token = EncounterManager.GetPlayerToken(model);
			if (token != null && token.player == player) return token;
		}
		return null;
	}

	// Faraam Armour: "once per encounter". The token is made fresh each encounter.
	public bool faraamUsed;

	// Crimson Robes (after the Heroic Action), when there is no asking: the condition that
	// hurts most goes.
	public void RemoveWorstCondition() {
		foreach (EncounterManager.StatusEffect c in new[] { EncounterManager.StatusEffect.POISON, EncounterManager.StatusEffect.BLEED,
				EncounterManager.StatusEffect.STAGGER, EncounterManager.StatusEffect.FROST }) {
			if (conditions.Remove(c)) return;
		}
	}

	public System.Collections.Generic.List<EncounterManager.StatusEffect> Conditions() =>
		new System.Collections.Generic.List<EncounterManager.StatusEffect>(conditions);

	public void OnClick() {
		// A heal or a rider is choosing characters: the click is a pick, not a move.
		if (EncounterManager.characterTurn != null && EncounterManager.characterTurn.isPicking) {
			EncounterManager.characterTurn.OnCharacterPicked(this);
			return;
		}
		EncounterManager.selectedPlayer = this;
		(GetParent()?.GetParent() as GameNode)?.Press();
	}

}
