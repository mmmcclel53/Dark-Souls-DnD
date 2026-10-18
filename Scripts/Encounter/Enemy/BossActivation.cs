using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

// A boss on the board is an Enemy like any other (its health, conditions, Block and Resist,
// its face in the activation bar all come through the EnemyData its BossData makes), but its
// activation is a behaviour card instead of the icons on its data card (p29): the top card of
// its deck is turned over, and its icons resolve left to right, the whole card as many times
// as its Repeat icon says.
//
// Moving towards a target turns the boss to face a neighbouring node nearer it, the players
// choosing which when there is more than one, and steps forward; moving away turns its back
// to a farther node and backs onto it; a Move without a target shifts that way without
// turning. Moving towards a character on its own node turns to face that character instead
// (p29). Attacks never turn it, so where the boss faces decides whom an Area attack reaches.
public partial class Enemy
{
	public BossData boss { get; private set; }
	public BossBehaviour behaviour { get; private set; }
	public bool isBoss => boss != null;
	public Vector2I facing => behaviour?.facing ?? Vector2I.Zero;

	// The beat a turn in place is held for, so the arcs are seen to swing.
	private const float TURN_SECONDS = 0.35f;

	// Set before the token enters the tree, as `data` is: _Ready builds the token from it.
	public void MakeBoss(BossData bossData, BossBehaviour state) {
		boss = bossData;
		behaviour = state;
		data = bossData.AsEnemy();
		tier = 1;
	}

	// The card being resolved carries the pillar icon (Pillars of Woe): its moves may step onto a
	// standing pillar and it smashes every pillar it reaches.
	private bool smashing;

	private async void ActivateBoss() {
		BossCard card = behaviour.Draw();
		smashing = card?.pillars == true;
		if (card != null) {
			if (EncounterManager.bossPanel != null) await EncounterManager.bossPanel.Reveal(this, card);
			for (int pass = 0; pass < Mathf.Max(1, card.repeat) && !isDead && !EncounterManager.partyDefeated; pass++) {
				foreach (BossAction action in card.actions) {
					if (action == null) continue;
					await Resolve(card, action);
					if (isDead || EncounterManager.partyDefeated) break;
				}
			}
		}
		smashing = false;
		EncounterManager.isEnemyMoving = false;
		EmitSignal(SignalName.ActivationFinished);
		if (isDead) QueueFree();
	}

	// ----- Pillars of Woe -----

	// Open to the boss: anything but a wall, and a standing pillar while its card smashes them.
	private bool Passable(PathNode node) {
		PathGrid grid = EncounterManager.pathGrid;
		return !grid.IsBlocked(node) || (smashing && grid.IsStandingPillar(node));
	}

	private async Task Smash(IEnumerable<GameNode> nodes) {
		if (!smashing) return;
		bool any = false;
		foreach (GameNode node in nodes) any |= EncounterManager.SmashPillar(node);
		if (any) await Beat(TURN_SECONDS);
	}

	// The nodes round one, itself included: a leap's landing shakes the pillars beside it.
	private static List<GameNode> Around(GameNode centre) {
		List<GameNode> around = new List<GameNode>();
		Vector2I at = BossArcs.GridOf(centre);
		for (int y = at.Y - 1; y <= at.Y + 1; y++) {
			for (int x = at.X - 1; x <= at.X + 1; x++) {
				if (x < 0 || y < 0 || x >= EncounterManager.gridSize || y >= EncounterManager.gridSize) continue;
				around.Add(EncounterManager.GameNodeAtIndex(y * EncounterManager.gridSize + x));
			}
		}
		return around;
	}

	private Task Resolve(BossCard card, BossAction action) => action.kind switch {
		BossAction.Kind.MOVE => BossMove(card, action),
		BossAction.Kind.ATTACK => BossAttack(card, action),
		BossAction.Kind.TURN_LEFT => TurnBy(-2),
		BossAction.Kind.TURN_RIGHT => TurnBy(2),
		BossAction.Kind.ROTATE => TurnBy(4),
		BossAction.Kind.LEAP => BossLeap(card, action),
		BossAction.Kind.HEAL => DrinkEstus(action.damage),
		BossAction.Kind.SUMMON => Summon(action.nodes),
		_ => Task.CompletedTask,
	};

	private async Task TurnBy(int eighths) {
		behaviour.facing = BossArcs.Turn(behaviour.facing, eighths);
		await Beat(TURN_SECONDS);
	}

	private async Task Beat(float seconds) {
		if (!IsInsideTree()) return;
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	}

	private Node2D BossTarget(BossAction action) => action.target switch {
		BossAction.Target.AGGRO => aggroPlayer,
		BossAction.Target.NEAREST => NearestOf(aggroPlayer, nonAggroPlayers),
		_ => null,
	};

	// The attack as an enemy attack, so the Block-or-Dodge prompt, the roll and the dodge all
	// take it as they take any other: the card gives the range and dodge difficulty (p27).
	private static EnemyMove AttackOf(BossCard card, BossAction action) => new EnemyMove {
		damage = action.damage,
		isMagic = action.magic,
		statusEffect = action.condition,
		attackRange = card.range,
		dodgeDifficulty = card.dodge,
		towardsAggro = action.target == BossAction.Target.AGGRO,
	};

	// ----- Leaping (p29) -----

	// Straight onto the target's node, however far, without turning. It is seen as a leap
	// (LeapMotion): the boss rises, sails over and hangs above the node, and its strike on each
	// character there is the drop itself. Every leap pushes: each
	// character on the landing node goes to any open neighbouring node, the players choosing,
	// in no particular arc. On the boss's own node it still pushes them off (Matt, Oct 2026). A
	// number on the leap's Push shield is an attack on each of them first (Vaulting Slam): only
	// those it hits are pushed, so a successful dodge stays put.
	private async Task BossLeap(BossCard card, BossAction action) {
		Node2D target = BossTarget(action);
		if (target == null || !GodotObject.IsInstanceValid(target) || target.GetParent() is not GameNode landing) return;
		Node2D self = (Node2D)GetParent();
		GameNode from = (GameNode)self.GetParent();
		await SpotlightBoss(new List<GameNode> { landing }, target, action);
		await LeapMotion.TakeOff(self, landing);
		if (action.damage <= 0) await LeapMotion.Drop(self);

		foreach (Node2D occupant in EncounterManager.GetPlayersInNode(landing, "Player")) {
			if (action.damage > 0 && !await Strike(occupant, AttackOf(card, action), slam: true)) continue;
			if (isDead || EncounterManager.partyDefeated) return;
			if (!GodotObject.IsInstanceValid(occupant)) continue;
			await Pushing.ShoveAnywhere(occupant, landing == from ? null : from);
			if (isDead || EncounterManager.partyDefeated) return;
		}
		// Every character there dodged, or none was there: it still comes down.
		await LeapMotion.Drop(self);
		if (landing == from) {
			await Smash(Around(landing));
			return;
		}

		EncounterManager.isEnemyMoving = true;
		lastStep = Pushing.Step(EncounterManager.pathGrid.NodeFromObj(self), EncounterManager.pathGrid.NodeFromObj(target));
		EncounterManager.MovePlayer(self, landing, from);
		await Beat(TokenMotion.StepSeconds(self));
		Footfall.Land(self);
		EncounterManager.ApplyNodeHazard(landing, self);
		await EncounterManager.ResolveOverflow(landing, self);
		EncounterManager.isEnemyMoving = false;
		await Smash(Around(landing));
	}

	// ----- Moving (p29) -----

	private async Task BossMove(BossCard card, BossAction action) {
		int nodes = action.nodes;
		// Frostbite: an enemy's Move icon values are 1 lower (p21). A Move 0 still turns.
		if (HasCondition(EncounterManager.StatusEffect.FROST)) nodes = Mathf.Max(0, nodes - 1);
		Node2D target = BossTarget(action);
		if (action.target != BossAction.Target.NONE && (target == null || !GodotObject.IsInstanceValid(target))) return;
		bool chasing = target != null && (action.arm == BossAction.Arm.FRONT || action.arm == BossAction.Arm.BACK);
		if (!chasing && nodes <= 0) return;

		// A Move with Push first clears the boss's own node, each character straight out
		// through its own arc (p21, p30), a Move 0 included (Matt).
		if (action.push) {
			Node2D self = (Node2D)GetParent();
			foreach (Node2D occupant in EncounterManager.GetPlayersInNode((Control)self.GetParent(), "Player")) {
				PlayerToken token = EncounterManager.GetPlayerToken(occupant);
				if (token == null) continue;
				await Pushing.Shove(occupant, token.bossSide == Vector2I.Zero ? behaviour.facing : token.bossSide, this);
				if (isDead) return;
			}
		}

		EncounterManager.isEnemyMoving = true;
		if (chasing) await Chase(card, action, target, nodes);
		else await Shift(card, action, nodes);
		EncounterManager.isEnemyMoving = false;
	}

	// Towards a character: each node, the boss turns to face a neighbouring node nearer it and
	// steps forward onto it; away, it turns its back to a farther one and backs onto it (p29).
	// Where more than one node would do, the players choose which (Matt, Oct 2026): the boss
	// first turns towards its target, then the nodes light and a click picks one. A Move 0 only
	// turns, the same way. Towards a character on its own node, it turns to face that character
	// instead of moving.
	private async Task Chase(BossCard card, BossAction action, Node2D target, int nodes) {
		bool towards = action.arm == BossAction.Arm.FRONT;
		PathGrid grid = EncounterManager.pathGrid;
		await SpotlightBoss(new List<GameNode>(), target, action);

		for (int n = 0; n < Mathf.Max(1, nodes); n++) {
			if (isDead || !GodotObject.IsInstanceValid(target) || !IsInsideTree()) break;
			PathNode here = grid.NodeFromObj((Node2D)GetParent());
			PathNode goal = grid.NodeFromObj(target);
			bool last = n == nodes - 1;

			if (goal == here) {
				Vector2I side = EncounterManager.GetPlayerToken(target)?.bossSide ?? Vector2I.Zero;
				if (side == Vector2I.Zero) side = behaviour.facing;
				await Face(side);
				if (towards || nodes == 0) break;
				// Away from a character on its own node: facing it, it backs off the other way.
				PathNode back = Neighbour(here, -side);
				if (back == null) break;
				await StepOnto(card, action, back, last);
				continue;
			}

			List<PathNode> options = StepOptions(here, goal, towards);
			if (options.Count == 0) break;
			PathNode chosen = options[0];
			if (options.Count > 1) {
				Vector2I toward = Toward(here, goal);
				await Face(towards ? toward : -toward);
				chosen = await ChooseStep(options, target);
				if (chosen == null) break;
			}
			Vector2I step = new Vector2I(chosen.gridX - here.gridX, chosen.gridY - here.gridY);
			await Face(towards ? step : -step);
			if (nodes == 0) break;
			await StepOnto(card, action, chosen, last);
		}
	}

	// A Move without a target shifts that way, relative to its facing, without turning (p29).
	private async Task Shift(BossCard card, BossAction action, int nodes) {
		PathGrid grid = EncounterManager.pathGrid;
		Vector2I way = action.arm switch {
			BossAction.Arm.BACK => -behaviour.facing,
			BossAction.Arm.LEFT => BossArcs.Turn(behaviour.facing, -2),
			BossAction.Arm.RIGHT => BossArcs.Turn(behaviour.facing, 2),
			_ => behaviour.facing,
		};
		List<PathNode> route = new List<PathNode>();
		PathNode here = grid.NodeFromObj((Node2D)GetParent());
		for (int n = 0; n < nodes; n++) {
			PathNode next = Neighbour(here, way);
			if (next == null) break;
			route.Add(next);
			here = next;
		}
		if (route.Count == 0) return;
		await SpotlightBoss(route.ConvertAll(EnemyMovement.GameNodeAt), null, action);
		for (int i = 0; i < route.Count && !isDead && IsInsideTree(); i++) {
			await StepOnto(card, action, route[i], i == route.Count - 1);
		}
	}

	// One node of a move: onto it, whatever it tramples there, its hazard, and on the last node
	// anything it overfills.
	private async Task StepOnto(BossCard card, BossAction action, PathNode next, bool last) {
		Node2D self = (Node2D)GetParent();
		PathNode from = EncounterManager.pathGrid.NodeFromObj(self);
		GameNode node = EnemyMovement.GameNodeAt(next);
		Vector2I step = Pushing.Step(from, next);
		lastStep = step;

		EncounterManager.MovePlayer(self, node, (Control)self.GetParent());
		await Beat(TokenMotion.StepSeconds(self));
		Footfall.Land(self);
		await Smash(new[] { node });
		if (isDead || EncounterManager.partyDefeated) return;
		if (action.push) await TrampleNode(card, action, node, step);
		if (isDead) return;
		EncounterManager.ApplyNodeHazard(node, self);
		if (last) await EncounterManager.ResolveOverflow(node, self);
	}

	private async Task Face(Vector2I direction) {
		if (direction == Vector2I.Zero || behaviour.facing == direction) return;
		behaviour.facing = direction;
		await Beat(TURN_SECONDS * 0.6f);
	}

	// The neighbouring nodes nearer the target by node distance (farther, going away). Nearer,
	// there are at most three: straight on and the two either side of it.
	private List<PathNode> StepOptions(PathNode here, PathNode goal, bool towards) {
		PathGrid grid = EncounterManager.pathGrid;
		int now = EnemyMovement.Distance(here, goal);
		List<PathNode> options = new List<PathNode>();
		foreach (PathNode neighbour in grid.GetNeighbours(here)) {
			if (!Passable(neighbour)) continue;
			int distance = EnemyMovement.Distance(neighbour, goal);
			if (towards ? distance < now : distance > now) options.Add(neighbour);
		}
		return options;
	}

	// The compass step pointing most nearly at the goal.
	private static Vector2I Toward(PathNode here, PathNode goal) {
		float angle = Mathf.Atan2(goal.gridY - here.gridY, goal.gridX - here.gridX);
		int index = Mathf.PosMod(Mathf.RoundToInt(angle / (Mathf.Pi * 0.25f)), 8);
		return BossArcs.COMPASS[index];
	}

	// The players pick the node: the choices light, the boss and its target are ringed, and a
	// click on one answers (PushPrompt's node choice, which the board already routes clicks to).
	private async Task<PathNode> ChooseStep(List<PathNode> options, Node2D target) {
		PathGrid grid = EncounterManager.pathGrid;
		PushPrompt prompt = EncounterManager.pushPrompt;
		if (prompt == null) return options[0];
		TokenHighlight ring = TokenHighlight.Attach(EncounterManager.GetPlayerToken(target), CHASED_COLOUR);
		GameNode chosen = await prompt.ChooseNode((Node2D)GetParent(), options.ConvertAll(grid.GameNodeAt));
		TokenHighlight.Detach(ring);
		if (chosen == null) return null;
		Vector2I at = BossArcs.GridOf(chosen);
		return grid.grid[at.X, at.Y];
	}

	private static readonly Color CHASED_COLOUR = new Color(0.85f, 0.18f, 0.12f);

	private PathNode Neighbour(PathNode from, Vector2I step) {
		PathGrid grid = EncounterManager.pathGrid;
		int x = from.gridX + step.X, y = from.gridY + step.Y;
		if (x < 0 || y < 0 || x >= EncounterManager.gridSize || y >= EncounterManager.gridSize) return null;
		PathNode node = grid.grid[x, y];
		return Passable(node) ? node : null;
	}

	// Moving with Push onto a node with characters: each is struck if the Move has damage, then
	// pushed on along the boss's step, staying in the arc it was in (p28, p30).
	private async Task TrampleNode(BossCard card, BossAction action, GameNode node, Vector2I step) {
		var hits = new List<Node2D>();
		foreach (Node2D occupant in EncounterManager.GetPlayersInNode(node, "Player")) {
			bool hit = true;
			if (action.damage > 0) hit = await Strike(occupant, AttackOf(card, action));
			if (isDead || EncounterManager.partyDefeated) return;
			if (hit) hits.Add(occupant);
		}
		foreach (Node2D occupant in hits) {
			if (GodotObject.IsInstanceValid(occupant)) await Pushing.Shove(occupant, step, this);
			if (isDead) return;
		}
	}

	private async Task SpotlightBoss(List<GameNode> route, Node2D target, BossAction action) {
		if (EncounterManager.spotlight == null) return;
		await EncounterManager.spotlight.PreviewMove(this, route, EncounterManager.GetPlayerToken(target), action.target == BossAction.Target.NEAREST);
	}

	// ----- Attacking (p29) -----

	private async Task BossAttack(BossCard card, BossAction action) {
		EnemyMove move = AttackOf(card, action);
		Node2D self = (Node2D)GetParent();
		List<Node2D> targets = new List<Node2D>();
		List<GameNode> lit = new List<GameNode>();
		Node2D aimedAt = null;

		if (action.area) {
			foreach (Node2D model in EncounterManager.players) {
				PlayerToken token = EncounterManager.GetPlayerToken(model);
				if (token == null || EnemyMovement.Distance(self, model) > card.range) continue;
				if ((BossArcs.OfCharacter(this, token) & action.AttackedArcs) != 0) targets.Add(model);
			}
			lit = AreaNodes(card.range, action.AttackedArcs);
		} else {
			aimedAt = BossTarget(action) ?? aggroPlayer;
			// Out of range misses entirely and has no effect (p25).
			if (aimedAt == null || !GodotObject.IsInstanceValid(aimedAt) || EnemyMovement.Distance(self, aimedAt) > card.range) return;
			if (aimedAt.GetParent() is not GameNode targetNode) return;
			targets = action.node ? EncounterManager.GetPlayersInNode(targetNode, "Player") : new List<Node2D> { aimedAt };
			lit.Add(targetNode);
		}

		if (EncounterManager.spotlight != null) {
			await EncounterManager.spotlight.PreviewArea(this, lit, targets.ConvertAll(EncounterManager.GetPlayerToken),
				action.target == BossAction.Target.NEAREST ? EncounterManager.GetPlayerToken(aimedAt) : null);
		}

		var hits = new List<Node2D>();
		foreach (Node2D occupant in targets) {
			if (!GodotObject.IsInstanceValid(occupant)) continue;
			bool hit = await Strike(occupant, move);
			if (isDead || EncounterManager.partyDefeated) return;
			if (hit) hits.Add(occupant);
		}
		if (action.condition == EncounterManager.StatusEffect.ABYSS) {
			foreach (GameNode node in lit) EncounterManager.LayAbyss(node);
		}
		if (action.push) {
			foreach (Node2D occupant in hits) {
				if (!GodotObject.IsInstanceValid(occupant)) continue;
				PlayerToken token = EncounterManager.GetPlayerToken(occupant);
				bool onNode = occupant.GetParent() == self.GetParent();
				Vector2I away = onNode && token != null && token.bossSide != Vector2I.Zero ? token.bossSide : Pushing.Away(self, occupant, behaviour.facing);
				await Pushing.Shove(occupant, away, this);
				if (isDead) return;
			}
		}
		// The pillars it reached come down once its blows have landed.
		await Smash(lit);
	}

	// Every node an Area attack reaches: in range and in one of its arcs, and the boss's own node.
	private List<GameNode> AreaNodes(int range, BossAction.Arcs arcs) {
		List<GameNode> nodes = new List<GameNode>();
		PathGrid grid = EncounterManager.pathGrid;
		PathNode here = grid.NodeFromObj((Node2D)GetParent());
		nodes.Add(grid.GameNodeAt(here));
		for (int y = 0; y < EncounterManager.gridSize; y++) {
			for (int x = 0; x < EncounterManager.gridSize; x++) {
				PathNode node = grid.grid[x, y];
				if (node == here || !Passable(node) || EnemyMovement.Distance(here, node) > range) continue;
				if ((BossArcs.Of(new Vector2I(x - here.gridX, y - here.gridY), behaviour.facing) & arcs) != 0) nodes.Add(grid.GameNodeAt(node));
			}
		}
		return nodes;
	}

	// The bottle icon: the boss drinks Estus and heals (Heavy Knight's 4, Matt). It never heals
	// past its starting health, and heating up is not undone.
	private async Task DrinkEstus(int amount) {
		int healed = Mathf.Min(amount, maxHealth - currentHealth);
		if (healed <= 0) return;
		currentHealth += healed;
		BoardFloat.Number(this, healed, ESTUS_COLOUR);
		ConditionBurst.Flare(this, ESTUS_COLOUR);
		await Beat(TURN_SECONDS);
	}

	private static readonly Color ESTUS_COLOUR = new Color(1f, 0.62f, 0.2f);

	// ----- Summoning -----

	private static readonly Color SUMMON_COLOUR = new Color(0.55f, 0.75f, 0.35f);

	// The summoned join the enemy phase at its end, so they act in the phase they arrive in,
	// after the boss, as their threat would put them.
	private async Task Summon(int count) {
		if (boss.summon == null || EncounterManager.summon == null || GetParent()?.GetParent() is not GameNode here) return;
		List<GameNode> spots = SpreadAround(here, behaviour.facing, count);
		if (spots.Count == 0) return;
		if (EncounterManager.spotlight != null) await EncounterManager.spotlight.PreviewArea(this, spots, new List<PlayerToken>(), null);
		foreach (GameNode spot in spots) {
			Enemy summoned = EncounterManager.summon(boss.summon, spot);
			if (summoned != null) ConditionBurst.Flare(summoned, SUMMON_COLOUR);
			await Beat(0.15f);
		}
	}

	// Where `count` models go round a node, spread as evenly as the board allows (Matt, Oct
	// 2026): each pick is the open neighbour farthest round the compass from those already
	// picked, the first the way `facing` points, and nearer neighbours (a lattice step, the
	// grid's straight steps) before the corners. The emptiest neighbours go first, so models
	// already there (earlier rats, characters) push the new ones elsewhere; a neighbour off the
	// board, a wall or one with no room is passed over.
	public static List<GameNode> SpreadAround(GameNode centre, Vector2I facing, int count) {
		List<GameNode> spots = new List<GameNode>();
		PathGrid grid = EncounterManager.pathGrid;
		if (centre == null || grid == null) return spots;
		Vector2I at = BossArcs.GridOf(centre);
		int start = Mathf.Max(0, System.Array.IndexOf(BossArcs.COMPASS, facing));
		var planned = new Dictionary<GameNode, int>();
		var taken = new List<int>();

		for (int n = 0; n < count; n++) {
			int best = -1;
			float bestScore = float.MinValue;
			for (int i = 0; i < 8; i++) {
				Vector2I step = BossArcs.COMPASS[i];
				int x = at.X + step.X, y = at.Y + step.Y;
				if (x < 0 || y < 0 || x >= EncounterManager.gridSize || y >= EncounterManager.gridSize) continue;
				if (grid.IsBlocked(grid.grid[x, y])) continue;
				GameNode node = grid.GameNodeAt(grid.grid[x, y]);
				planned.TryGetValue(node, out int already);
				int load = EncounterManager.GetAllPlayersInNode(node).Count + already;
				if (load >= EncounterManager.MAX_MODELS_PER_NODE) continue;

				int gap = 8;
				foreach (int t in taken) {
					int d = Mathf.Abs(i - t) % 8;
					gap = Mathf.Min(gap, Mathf.Min(d, 8 - d));
				}
				int fromStart = Mathf.Abs(i - start) % 8;
				float score = gap * 10f - load * 100f + (i % 2 == 0 ? 2f : 0f) - Mathf.Min(fromStart, 8 - fromStart) * 0.1f;
				if (score > bestScore) {
					bestScore = score;
					best = i;
				}
			}
			if (best < 0) break;
			Vector2I pick = at + BossArcs.COMPASS[best];
			GameNode chosen = grid.GameNodeAt(grid.grid[pick.X, pick.Y]);
			planned[chosen] = planned.TryGetValue(chosen, out int had) ? had + 1 : 1;
			taken.Add(best);
			spots.Add(chosen);
		}
		return spots;
	}

	// ----- Heat up (p28) -----

	// `damage` is what one hit took off, Bleed's +2 included.
	private void CheckHeatUp(int damage) {
		if (!isBoss || isDead || currentHealth <= 0) return;
		// Corrupted Dragonslayer: 4 or more from one attack, the first three times, puts a Heat Up
		// card on top of the deck (Matt). Three Heat Up cards, so the cards run out on the third.
		if (boss.ability == BossData.Ability.CORRUPTED_DRAGONSLAYER) {
			if (damage >= 4 && behaviour.HeatUpOnTop()) ConditionBurst.Flare(this, HEAT_UP_COLOUR);
			return;
		}
		if (!behaviour.ShouldHeatUp(currentHealth)) return;
		behaviour.HeatUp();
		ConditionBurst.Flare(this, HEAT_UP_COLOUR);
	}

	private static readonly Color HEAT_UP_COLOUR = new Color(1f, 0.45f, 0.12f);
}
