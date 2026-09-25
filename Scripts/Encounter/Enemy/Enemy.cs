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

	public List<PathNode> path;
	public int currentHealth { get; private set; }
	public int currentPathIndex = 0;

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

	// Behaviour icons resolve left to right (p24). Attacks and leaps finish immediately, so
	// this keeps pulling until it finds a walk to hand to _Process, or runs out and reports
	// the activation over. Anything that cannot resolve is skipped rather than left to stall.
	private async void ProcessNextMove() {
		while (moveQueue.Count > 0) {
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

			path = EnemyMovement.Plan(move, this, (Node2D)GetParent(), TargetFor(move));
			if (path != null && path.Count > 0) return;
		}

		path = null;
		activeMove = null;
		EmitSignal(SignalName.ActivationFinished);
	}

	// The skull icon means the aggro holder; the ring means whichever character is nearest.
	// Ties go to the aggro holder, and failing that to the higher Taunt (p24).
	private Node2D TargetFor(EnemyMove move) {
		if (move.towardsAggro) return aggroPlayer;

		Node2D nearest = null;
		int nearestDistance = int.MaxValue;
		int nearestTaunt = int.MinValue;

		foreach (Node2D candidate in nonAggroPlayers) {
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

		Node2D self = (Node2D)GetParent();
		if (!EncounterManager.MovePlayer(self, destination, (Control)self.GetParent())) return;
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

		// The Node icon hits every character sharing the target's node (p25).
		if (move.isAOE) {
			foreach (Node2D occupant in EncounterManager.GetPlayersInNode(targetNode, "Player")) {
				await Strike(occupant, move);
			}
		} else {
			await Strike(target, move);
		}
	}

	// Every character targeted gets the Block-or-Dodge choice before the dice are rolled.
	private async Task Strike(Node2D playerObj, EnemyMove move) {
		PlayerToken token = EncounterManager.GetPlayerToken(playerObj);
		if (token == null) return;

		int dodgeCost = CombatResolver.DodgeStaminaCost(token);

		bool dodge = false;
		if (EncounterManager.dodgePrompt != null && token.CanSpend(dodgeCost)) {
			dodge = await EncounterManager.dodgePrompt.Ask(this, move, token);
		}

		if (dodge && token.SpendStamina(dodgeCost)) {
			CombatResolver.AttackOutcome outcome = await CombatPresenter.EnemyAttacksDodging(this, move, token);
			// A successful dodge lets the character move one node (p22); this waits on it.
			if (!outcome.hit && EncounterManager.characterTurn != null) {
				await EncounterManager.characterTurn.OfferDodgeStep(token);
			}
		} else {
			await CombatPresenter.EnemyAttacks(this, move, token);
		}
	}

	// Movement attacks hit every character on each node the enemy moves into, and the push
	// shoves them out of the way afterwards (p21, p25).
	private async Task EnterNode(GameNode node, EnemyMove move) {
		if (move == null || !move.isPush) return;

		foreach (Node2D occupant in EncounterManager.GetPlayersInNode(node, "Player")) {
			if (move.damage > 0) await Strike(occupant, move);
			Push(occupant, node);
		}
	}

	private void Push(Node2D playerObj, GameNode from) {
		GameNode destination = EnemyMovement.PushDestination(EncounterManager.pathGrid.NodeFromObj(playerObj));
		if (destination == null || destination == from) return;
		EncounterManager.MovePlayer(playerObj, destination, from);
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
		if (currentHealth <= 0) {
			QueueFree();
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

	public override void _Ready() {
		Pressed += OnClick;

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

	public override void _Process(double delta) {
		if (path != null && path.Count > 0) {
			Node2D self = (Node2D)GetParent();
			Control parent = (Control)self.GetParent();
			float nodeSize = parent.Size.X;
			Vector2 centeredPos = new Vector2(nodeSize/4,nodeSize/4);
			if (!EncounterManager.isEnemyMoving) {
				EncounterManager.isEnemyMoving = true;
				self.Position = centeredPos;
			}

			PathNode pathNode = path[currentPathIndex];
			int child = (pathNode.gridY*EncounterManager.gridSize) + pathNode.gridX;
			GameNode node = EncounterManager.pathGrid.GetChild<GameNode>(child);
			// centeredPos is in the node's own space; the board can be zoomed, so it has to
			// go through the node's transform rather than be added to a global position.
			Vector2 targetPos = node.GetGlobalTransform() * centeredPos;

			if (self.GlobalPosition.DistanceTo(targetPos) > 1f) {
				Vector2 moveDir = (targetPos - self.GlobalPosition).Normalized();
				Vector2 time = new Vector2((float)(delta*250), (float)(delta*250));
				self.GlobalPosition += moveDir * time;
			} else {
				bool last = currentPathIndex >= path.Count-1;
				if (last) EncounterManager.MovePlayer(self, node, (Control)self.GetParent());
				ResolveArrival(node, last);
			}
		}
	}

	// A push or movement attack can open the Block-or-Dodge prompt, so arriving on a node
	// has to be able to wait. path is cleared first so _Process does not re-enter mid-await.
	private async void ResolveArrival(GameNode node, bool last) {
		List<PathNode> remaining = path;
		path = null;

		await EnterNode(node, activeMove);

		Node2D self = (Node2D)GetParent();
		EncounterManager.ApplyNodeHazard(node, self);
		if (last) await EncounterManager.ResolveOverflow(node, self);

		if (last) {
			EncounterManager.isEnemyMoving = false;
			currentPathIndex = 0;
			ProcessNextMove();
			return;
		}

		await ToSignal(GetTree().CreateTimer(0.5f), "timeout");
		currentPathIndex++;
		path = remaining;
	}
}
