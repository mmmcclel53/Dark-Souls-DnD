using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Enemy : TextureButton
{

	[Signal] public delegate void ActivationFinishedEventHandler();

	[Export] public EnemyData data;
	[Export] public int tier = 1;

	public int threatLevel => data.threatLevel;
	public Array<EnemyMove> moves => data.moves;

	// Tier scaling is applied per instance so the shared EnemyData resource is never mutated.
	public int maxHealth { get; private set; }
	public int physicalDefense { get; private set; }
	public int magicalDefense { get; private set; }

	public int currentHealth { get; private set; }
	// Killed. Usually freed the same frame; killed during its own activation (a Backstab),
	// it is hidden and kept until its behaviour loop, still running on it, has ended.
	public bool isDead { get; private set; }
	public float stepPauseSeconds = 0.1f;

	private readonly HashSet<EncounterManager.StatusEffect> conditions = new HashSet<EncounterManager.StatusEffect>();

	private Queue<EnemyMove> moveQueue = new Queue<EnemyMove>();
	private EnemyMove activeMove;
	private Node2D aggroPlayer;
	private List<Node2D> nonAggroPlayers;

	public void DoMoves(Node2D aggroPlayer, List<Node2D> nonAggroPlayers) {
		this.aggroPlayer = aggroPlayer;
		this.nonAggroPlayers = nonAggroPlayers;
		moveQueue.Clear();
		foreach (EnemyMove move in moves) {
			moveQueue.Enqueue(move);
		}
		ProcessNextMove();
	}

	// Behaviour icons resolve left to right (p24). Attacks and leaps finish here; a walk
	// carries on by itself and comes back to this when it is done. Anything that cannot
	// resolve is skipped rather than left to stall, so the activation always ends.
	private async void ProcessNextMove() {
		while (moveQueue.Count > 0 && !isDead) {
			EnemyMove move = moveQueue.Dequeue();
			activeMove = move;

			if (move.isLeap) {
				await Leap(move);
				continue;
			}

			if (move.direction == 0) {
				await Attack(move);
				continue;
			}

			Node2D target = TargetFor(move);
			List<PathNode> planned = EnemyMovement.Plan(move, this, (Node2D)GetParent(), target);
			if (planned == null || planned.Count == 0) continue;

			// Shown before the first step, so the player sees where it is going first.
			await SpotlightMove(planned.ConvertAll(EnemyMovement.GameNodeAt), target, move);
			Walk(planned);
			return;
		}

		activeMove = null;
		EmitSignal(SignalName.ActivationFinished);
		if (isDead) QueueFree();
	}

	// Node by node. The nodes on the way are crossed without stopping — the wrapper itself
	// travels, so nothing there is re-packed — and the last is arrived on through
	// MovePlayer like any other move. Every node entered still resolves its push attack
	// and hazard, and the last its overflow; a push attack can open the Block-or-Dodge
	// prompt, so each step is waited on.
	private async void Walk(List<PathNode> planned) {
		EncounterManager.isEnemyMoving = true;
		Node2D self = (Node2D)GetParent();

		for (int i = 0; i < planned.Count; i++) {
			if (!GodotObject.IsInstanceValid(self) || !IsInsideTree()) return;
			if (isDead) break;
			GameNode node = EnemyMovement.GameNodeAt(planned[i]);
			bool last = i == planned.Count - 1;

			if (last) {
				EncounterManager.MovePlayer(self, node, (Control)self.GetParent());
				await ToSignal(GetTree().CreateTimer(TokenMotion.STEP_SECONDS), SceneTreeTimer.SignalName.Timeout);
			} else {
				float nodeSize = node.Size.X;
				await TokenMotion.Travel(self, node, new Vector2(nodeSize / 4f, nodeSize / 4f));
			}

			await EnterNode(node, activeMove);
			if (isDead) break;
			EncounterManager.ApplyNodeHazard(node, self);
			if (last) await EncounterManager.ResolveOverflow(node, self);
			else await ToSignal(GetTree().CreateTimer(stepPauseSeconds), SceneTreeTimer.SignalName.Timeout);
		}

		EncounterManager.isEnemyMoving = false;
		ProcessNextMove();
	}

	// The skull icon means the aggro holder; the ring means whichever character is nearest.
	// Ties go to the aggro holder, and failing that to the higher Taunt (p24).
	private Node2D TargetFor(EnemyMove move) =>
		move.towardsAggro ? aggroPlayer : NearestOf(aggroPlayer, nonAggroPlayers);

	// Who this enemy counts as nearest right now, with the same tie-breaks its "nearest"
	// behaviours use. Read by the Nearest marker whenever the enemy is hovered.
	public PlayerToken NearestCharacter() {
		PlayerToken holder = EncounterManager.aggroHolder;
		Node2D aggro = GodotObject.IsInstanceValid(holder) ? holder.GetParent() as Node2D : null;
		if (aggro == null && EncounterManager.players.Count > 0) aggro = EncounterManager.players[0];
		return EncounterManager.GetPlayerToken(NearestOf(aggro, EncounterManager.players));
	}

	private Node2D NearestOf(Node2D aggroPlayer, IEnumerable<Node2D> candidates) {
		if (candidates == null) return aggroPlayer;
		Node2D nearest = null;
		int nearestDistance = int.MaxValue;
		int nearestTaunt = int.MinValue;

		foreach (Node2D candidate in candidates) {
			if (!GodotObject.IsInstanceValid(candidate)) continue;
			int distance = EnemyMovement.Distance((Node2D)GetParent(), candidate);

			if (distance < nearestDistance) {
				nearestDistance = distance;
				nearest = candidate;
				nearestTaunt = TauntOf(candidate);
				continue;
			}
			if (distance > nearestDistance) continue;

			if (candidate == aggroPlayer) {
				nearest = candidate;
				nearestTaunt = TauntOf(candidate);
			} else if (nearest != aggroPlayer && TauntOf(candidate) > nearestTaunt) {
				nearest = candidate;
				nearestTaunt = TauntOf(candidate);
			}
		}
		return nearest ?? aggroPlayer;
	}

	private static int TauntOf(Node2D playerObj) =>
		EncounterManager.GetPlayerToken(playerObj)?.player?.character?.taunt ?? 0;

	// A leap goes straight to the target's node however far away it is (p29), but still
	// cannot land on a node that is already full.
	private async Task Leap(EnemyMove move) {
		Node2D target = TargetFor(move);
		if (target == null || target.GetParent() is not GameNode destination) return;

		await SpotlightMove(new List<GameNode> { destination }, target, move);

		Node2D self = (Node2D)GetParent();
		if (!EncounterManager.MovePlayer(self, destination, (Control)self.GetParent())) return;
		await ToSignal(GetTree().CreateTimer(TokenMotion.STEP_SECONDS), SceneTreeTimer.SignalName.Timeout);
		EncounterManager.ApplyNodeHazard(destination, self);
		await EncounterManager.ResolveOverflow(destination, self);
		await EnterNode(destination, move);
	}

	// Out of range misses entirely and has no effect (p25).
	private async Task Attack(EnemyMove move) {
		Node2D target = TargetFor(move);
		if (target == null || !GodotObject.IsInstanceValid(target)) return;
		if (EnemyMovement.Distance((Node2D)GetParent(), target) > move.attackRange) return;

		if (target.GetParent() is not GameNode targetNode) return;

		if (EncounterManager.spotlight != null) {
			await EncounterManager.spotlight.PreviewAttack(this, targetNode, EncounterManager.GetPlayerToken(target), !move.towardsAggro);
		}

		// The Node icon hits every character sharing the target's node (p25).
		if (move.isAOE) {
			foreach (Node2D occupant in EncounterManager.GetPlayersInNode(targetNode, "Player")) {
				await Strike(occupant, move);
				if (isDead) return;
			}
		} else {
			await Strike(target, move);
		}
	}

	private async Task SpotlightMove(List<GameNode> route, Node2D target, EnemyMove move) {
		if (EncounterManager.spotlight == null) return;
		await EncounterManager.spotlight.PreviewMove(this, route, EncounterManager.GetPlayerToken(target), !move.towardsAggro);
	}

	// Every character targeted gets the Block-or-Dodge choice before the dice are rolled.
	// The blade goes up first and stops short of the character; it stays there through the
	// choice and the roll, and only comes down once the presenter knows the outcome.
	private async Task Strike(Node2D playerObj, EnemyMove move) {
		PlayerToken token = EncounterManager.GetPlayerToken(playerObj);
		if (token == null) return;

		AttackSlash swing = AttackSlash.Begin(this, token, AttackSlash.Style.ENEMY, AttackSlash.FormFor(move.isMagic, move.attackRange));
		if (swing != null) await swing.Windup();

		int dodgeCost = CombatResolver.DodgeStaminaCost(token);

		bool dodge = false;
		if (EncounterManager.dodgePrompt != null && token.CanSpend(dodgeCost)) {
			dodge = await EncounterManager.dodgePrompt.Ask(this, move, token);
		}

		if (dodge && token.SpendStamina(dodgeCost)) {
			CombatResolver.AttackOutcome outcome = await CombatPresenter.EnemyAttacksDodging(this, move, token, swing);
			// A successful dodge lets the character move one node (p22); this waits on it.
			if (!outcome.hit && EncounterManager.characterTurn != null) {
				await EncounterManager.characterTurn.OfferDodgeStep(token);
				// The Assassin's Backstab: attack the enemy just dodged.
				await EncounterManager.characterTurn.OfferBackstab(token, this);
			}
		} else {
			await CombatPresenter.EnemyAttacks(this, move, token, swing);
		}
	}

	// Movement attacks hit every character on each node the enemy moves into, and the push
	// shoves them out of the way afterwards (p21, p25).
	private async Task EnterNode(GameNode node, EnemyMove move) {
		if (move == null || !move.isPush) return;

		foreach (Node2D occupant in EncounterManager.GetPlayersInNode(node, "Player")) {
			if (move.damage > 0) await Strike(occupant, move);
			if (isDead) return;
			Push(occupant, node);
		}
	}

	private void Push(Node2D playerObj, GameNode from) {
		GameNode destination = EnemyMovement.PushDestination(EncounterManager.pathGrid.NodeFromObj(playerObj));
		if (destination == null || destination == from) return;
		EncounterManager.MovePlayer(playerObj, destination, from, true);
	}

	public void EndActivation() {
		SetActivating(false);
		ClearVolatileConditions();
	}

	public void ApplyDamage(int damage) {
		if (damage <= 0) return;

		// Bleed adds 2 to the damage suffered, then comes off (p21).
		if (conditions.Remove(EncounterManager.StatusEffect.BLEED)) {
			damage += 2;
			}

		currentHealth = Mathf.Max(0, currentHealth - damage);
		BoardFloat.Number(this, -damage, BoardFloat.DAMAGE);
		if (currentHealth <= 0 && !isDead) {
			isDead = true;
			// The ghost does the dying; this token is gone the moment the turn loop looks.
			EnemyDeath.Play(this);
			if (isActivating) Visible = false;
			else QueueFree();
		}
	}

	// The board token carries no chrome, so activation is just a flag the Enemy Activation
	// bar reads to mark which face is acting.
	public bool isActivating { get; private set; }

	public void SetActivating(bool activating) {
		isActivating = activating;
	}

	public void ApplyCondition(EncounterManager.StatusEffect condition) {
		if (condition == EncounterManager.StatusEffect.NONE) return;
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

	public override void _Ready() {
		Pressed += OnClick;
		MouseEntered += () => NearestMarker.Hover(this);
		MouseExited += () => NearestMarker.Unhover(this);

		TextureNormal = data.avatarTexture;

		maxHealth = data.health;
		physicalDefense = data.physicalDefense;
		magicalDefense = data.magicalDefense;

		if (tier > 1) {
			maxHealth = (int)Mathf.Ceil(maxHealth * 1.5);
			physicalDefense += tier;
			magicalDefense += tier;
		}
		currentHealth = maxHealth;

		SetActivating(false);

	}

	// While an attack is armed a click picks this enemy; otherwise it is a click on its node.
	public void OnClick() {
		if (EncounterManager.characterTurn != null && EncounterManager.characterTurn.isTargeting) {
			EncounterManager.characterTurn.PickTarget(this);
			return;
		}
		(GetParent()?.GetParent() as GameNode)?.Press();
	}

}
