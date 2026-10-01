using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

// Owns everything the player does during one character activation: stepping node to node,
// arming a weapon attack, and picking what it hits. ActionListener still owns the turn
// order and simply hands the activation over.
//
// Movement is deliberately one node per click (p22): the first step of an activation is the
// free Walk and each one after is a Run, so the cost only makes sense a step at a time.
public partial class CharacterTurn : Node
{

	[Signal] public delegate void ActivationEndedEventHandler();
	[Signal] public delegate void AttackResolvedEventHandler();
	[Signal] public delegate void DodgeStepEndedEventHandler();

	[Export] public CharacterActionBar actionBar;

	[Export] public Color moveColour = new Color(0.79f, 0.64f, 0.29f, 0.45f);
	[Export] public Color targetColour = new Color(0.64f, 0.17f, 0.13f, 0.55f);
	[Export] public Color targetRingColour = new Color(0.85f, 0.18f, 0.12f);

	public PlayerToken active { get; private set; }
	public bool isTargeting => armedMove != null;
	public PlayerMove armed => armedMove;

	private Weapon armedWeapon;
	private PlayerMove armedMove;
	private readonly List<GameNode> highlighted = new List<GameNode>();
	private readonly List<TokenHighlight> targetRings = new List<TokenHighlight>();
	private readonly List<Enemy> targetable = new List<Enemy>();

	// Backstab's choice of attack, while the Assassin is making it.
	private TaskCompletionSource<(Weapon, PlayerMove)> backstabChoice;

	// The character taking the free step a successful dodge grants (p22), while it lasts.
	private PlayerToken dodger;
	public bool isDodgeStepping => dodger != null;

	public override void _Ready() {
		EncounterManager.characterTurn = this;

		if (actionBar != null) {
			actionBar.AttackChosen += OnAttackChosen;
			actionBar.EndActivationPressed += () => EmitSignal(SignalName.ActivationEnded);
			actionBar.DodgeStepEnded += () => EmitSignal(SignalName.DodgeStepEnded);
			actionBar.HeroicChosen += UseHeroic;
			actionBar.BackstabPassed += () => backstabChoice?.TrySetResult((null, null));
			actionBar.ShowIdle();
		}
	}

	public void Begin(PlayerToken token) {
		active = token;
		Disarm();
		if (actionBar != null) actionBar.Refresh(token);
		ShowMoveOptions();
	}

	public void End() {
		Disarm();
		ClearHighlights();
		active = null;
		if (actionBar != null) actionBar.ShowIdle();
	}

	// ----- The dodge step (p22) -----

	// A character who dodged may move one node. Runs inside the enemy's activation, which
	// waits on it: the adjacent nodes light, the bar offers End Dodge, and either a node
	// click or that button finishes it. The stamina was paid with the dodge itself.
	public async Task OfferDodgeStep(PlayerToken token) {
		if (token == null || !GodotObject.IsInstanceValid(token)) return;

		dodger = token;
		ClearHighlights();
		foreach (GameNode node in AdjacentNodes(token)) {
			node.Highlight(moveColour, true);
			highlighted.Add(node);
		}
		actionBar?.ShowDodgeStep(token);

		await ToSignal(this, SignalName.DodgeStepEnded);

		ClearHighlights();
		dodger = null;
		actionBar?.ShowIdle();
	}

	public async void OnDodgeStepNodeClicked(GameNode node) {
		if (dodger == null || !highlighted.Contains(node)) return;

		Node2D self = (Node2D)dodger.GetParent();
		ClearHighlights();
		if (EncounterManager.MovePlayer(self, node, (Control)self.GetParent())) {
			EncounterManager.ApplyNodeHazard(node, self);
			await EncounterManager.ResolveOverflow(node, self);
		}
		EmitSignal(SignalName.DodgeStepEnded);
	}

	// ----- Movement -----

	private void ShowMoveOptions() {
		ClearHighlights();
		if (active == null || isTargeting || !active.CanStep()) return;

		foreach (GameNode node in AdjacentNodes(active)) {
			node.Highlight(moveColour, true);
			highlighted.Add(node);
		}
	}

	// Async because arriving on a full node opens the push prompt and waits on it.
	public async void OnNodeClicked(GameNode node) {
		if (active == null) return;

		if (isTargeting) {
			if (ArmedTerms.aoe) ResolveAgainstNode(node);
			else PickOnlyTargetOn(node);
			return;
		}
		if (!highlighted.Contains(node)) return;

		Node2D self = (Node2D)active.GetParent();
		if (!active.TakeStep()) return;
		if (!EncounterManager.MovePlayer(self, node, (Control)self.GetParent())) return;

		ClearHighlights();
		EncounterManager.ApplyNodeHazard(node, self);
		await EncounterManager.ResolveOverflow(node, self);

		if (active != null) Refresh();
	}

	private static List<GameNode> AdjacentNodes(PlayerToken token) {
		List<GameNode> nodes = new List<GameNode>();
		PathGrid grid = EncounterManager.pathGrid;
		PathNode here = grid.NodeFromObj((Node2D)token.GetParent());

		foreach (PathNode neighbour in grid.GetNeighbours(here)) {
			if (!neighbour.walkable) continue;
			nodes.Add(EnemyMovement.GameNodeAt(neighbour));
		}
		return nodes;
	}

	// ----- Attacking -----

	private void OnAttackChosen(Weapon weapon, PlayerMove move) {
		if (backstabChoice != null) {
			backstabChoice.TrySetResult((weapon, move));
			return;
		}
		// Clicking the armed option again cancels it.
		if (armedMove == move) {
			Disarm();
			Refresh();
			return;
		}

		armedWeapon = weapon;
		armedMove = move;
		// Rebuilt so the row shows armed, then the targets lit.
		Refresh();
	}

	private void Disarm() {
		armedWeapon = null;
		armedMove = null;
	}

	// Range, cost and AOE come from the terms, so an armed Heroic boost counts here too.
	private AttackTerms ArmedTerms => AttackTerms.For(active, armedWeapon, armedMove);

	private bool InRange(Enemy enemy) => ArmedTerms.Reaches(active, enemy);

	// Nodes light up for every armed attack; a single-target attack also rings each enemy
	// it can reach, since that is what gets clicked. The Node icon picks a node, not a model.
	private void ShowTargets() {
		ClearHighlights();

		bool aoe = ArmedTerms.aoe;
		foreach (Enemy enemy in EnemiesInRange()) {
			if (!aoe) targetRings.Add(TokenHighlight.Attach(enemy, targetRingColour));
			enemy.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
			targetable.Add(enemy);

			if (enemy.GetParent().GetParent() is not GameNode node || highlighted.Contains(node)) continue;
			node.Highlight(targetColour, true);
			highlighted.Add(node);
		}
	}

	private List<Enemy> EnemiesInRange(GameNode onNode = null) {
		List<Enemy> enemies = new List<Enemy>();
		foreach (Node2D enemyObj in EncounterManager.enemies) {
			Enemy enemy = EncounterManager.GetEnemy(enemyObj);
			if (enemy == null || !InRange(enemy)) continue;
			if (onNode != null && enemyObj.GetParent() != onNode) continue;
			enemies.Add(enemy);
		}
		return enemies;
	}

	// Clicking a lit node rather than a model is unambiguous when only one enemy there can
	// be hit; with more than one, the player has to pick the model.
	private void PickOnlyTargetOn(GameNode node) {
		if (!highlighted.Contains(node)) return;
		List<Enemy> candidates = EnemiesInRange(node);
		if (candidates.Count == 1) PickTarget(candidates[0]);
	}

	// Async because the roll is shown before it lands: the attack is disarmed and the
	// highlights cleared first, so nothing on the board answers a click during the reveal.
	public async void PickTarget(Enemy enemy) {
		if (!isTargeting || active == null || enemy == null || !InRange(enemy)) return;

		AttackTerms terms = ArmedTerms;
		if (terms.aoe) {
			if (enemy.GetParent()?.GetParent() is GameNode node) ResolveAgainstNode(node);
			return;
		}

		if (!PayFor(terms)) return;
		(Weapon weapon, PlayerMove move) = TakeArmed();
		await CombatPresenter.CharacterAttacks(active, move, weapon, enemy, terms.extraDice);
		FinishAttack(weapon, terms);
	}

	// The Node icon rolls once and compares that total against every enemy there (p23).
	private async void ResolveAgainstNode(GameNode node) {
		if (!highlighted.Contains(node)) return;

		List<Enemy> targets = EnemiesOn(node);
		if (targets.Count == 0) return;

		AttackTerms terms = ArmedTerms;
		if (!PayFor(terms)) return;
		(Weapon weapon, PlayerMove move) = TakeArmed();
		await CombatPresenter.CharacterAttacksNode(active, move, weapon, node, targets, terms.extraDice);
		FinishAttack(weapon, terms);
	}

	private static List<Enemy> EnemiesOn(GameNode node) {
		List<Enemy> targets = new List<Enemy>();
		foreach (Node2D occupant in EncounterManager.GetPlayersInNode(node, "Enemy")) {
			Enemy enemy = EncounterManager.GetEnemy(occupant);
			if (enemy != null) targets.Add(enemy);
		}
		return targets;
	}

	private bool PayFor(AttackTerms terms) =>
		terms.cost == 0 || (active.CanSpend(terms.cost) && active.SpendStamina(terms.cost));

	private (Weapon, PlayerMove) TakeArmed() {
		(Weapon weapon, PlayerMove move) = (armedWeapon, armedMove);
		Disarm();
		ClearHighlights();
		return (weapon, move);
	}

	private void FinishAttack(Weapon weapon, AttackTerms terms) {
		// The activation may have been torn down while the roll was on screen.
		if (active == null || !GodotObject.IsInstanceValid(active)) return;

		active.RecordAttack(weapon, !terms.reusesWeapon);
		if (terms.boosted) active.SpendHeroicBoost();
		EncounterManager.PruneDeadEnemies();
		Refresh();

		// Last, because a win ends the activation and tears this turn down.
		EmitSignal(SignalName.AttackResolved);
	}

	// ----- Heroic Actions -----

	// The Heroic token on the action bar, during the character's own activation. The effects
	// land at once; the four that boost an attack are armed for the next one it applies to.
	private async void UseHeroic() {
		if (active == null || !active.CanUseHeroicNow) return;
		PlayerToken user = active;
		Heroic.Kind kind = user.heroic;
		user.player.heroicUsed = true;
		Disarm();

		switch (kind) {
			case Heroic.Kind.PERSEVERANCE:
				foreach (PlayerToken member in PartyTokens()) member.RecoverStamina(2);
				break;
			case Heroic.Kind.KEEP_THE_FAITH:
				Node2D here = (Node2D)user.GetParent();
				foreach (PlayerToken member in PartyTokens()) {
					if (EnemyMovement.Distance(here, (Node2D)member.GetParent()) <= 1) member.Heal(2);
				}
				break;
			case Heroic.Kind.LUCKY_BREAK:
				user.RecoverStamina(2);
				user.Heal(2);
				user.player.luckUsed = false;
				break;
			case Heroic.Kind.COMBAT_VERSATILITY:
				await ChangeEquipment(user.player);
				break;
			default:
				if (Heroic.IsBoost(kind)) user.ArmHeroic(kind);
				break;
		}
		if (active == user) Refresh();
	}

	// Combat Versatility: the equipment screen, as at a bonfire, for this character only.
	private async Task ChangeEquipment(Player player) {
		EquipmentModal modal = GetNodeOrNull<EquipmentModal>("/root/EquipmentModal");
		if (modal == null) return;
		modal.Open(player, lockToPlayer: true);
		await ToSignal(modal, EquipmentModal.SignalName.Closed);
	}

	private static List<PlayerToken> PartyTokens() {
		List<PlayerToken> tokens = new List<PlayerToken>();
		foreach (Node2D model in EncounterManager.players) {
			PlayerToken token = EncounterManager.GetPlayerToken(model);
			if (token != null) tokens.Add(token);
		}
		return tokens;
	}

	// The Assassin's Backstab, after a successful dodge (and its free step): pick any attack
	// that reaches the enemy dodged and make it at no stamina, or pass. Runs inside that
	// enemy's activation, which waits on it; the enemy may die of it (Enemy.isDead).
	public async Task OfferBackstab(PlayerToken assassin, Enemy enemy) {
		if (actionBar == null || !GodotObject.IsInstanceValid(assassin) || !GodotObject.IsInstanceValid(enemy) || enemy.isDead) return;
		if (assassin.heroic != Heroic.Kind.BACKSTAB || assassin.player.heroicUsed) return;

		System.Func<Weapon, PlayerMove, bool> reaches = (weapon, move) =>
			GodotObject.IsInstanceValid(enemy) && AttackTerms.For(assassin, weapon, move, free: true).Reaches(assassin, enemy);
		if (!AnyAttack(assassin.player, reaches)) return;

		backstabChoice = new TaskCompletionSource<(Weapon, PlayerMove)>();
		actionBar.ShowBackstab(assassin, reaches);
		(Weapon chosenWeapon, PlayerMove chosenMove) = await backstabChoice.Task;
		backstabChoice = null;
		actionBar.ShowIdle();

		if (chosenMove == null || !GodotObject.IsInstanceValid(assassin) || !GodotObject.IsInstanceValid(enemy) || enemy.isDead) return;
		assassin.player.heroicUsed = true;

		AttackTerms terms = AttackTerms.For(assassin, chosenWeapon, chosenMove, free: true);
		if (terms.aoe && enemy.GetParent()?.GetParent() is GameNode node) {
			await CombatPresenter.CharacterAttacksNode(assassin, chosenMove, chosenWeapon, node, EnemiesOn(node), terms.extraDice);
		} else {
			await CombatPresenter.CharacterAttacks(assassin, chosenMove, chosenWeapon, enemy, terms.extraDice);
		}
	}

	private static bool AnyAttack(Player player, System.Func<Weapon, PlayerMove, bool> reaches) {
		foreach (Weapon weapon in new[] { player.GetLeftHand(), player.GetRightHand() }) {
			if (weapon?.attacks == null) continue;
			foreach (PlayerMove move in weapon.attacks) {
				if (move != null && reaches(weapon, move)) return true;
			}
		}
		return false;
	}

	// ----- Shared -----

	private void Refresh() {
		if (actionBar != null) actionBar.Refresh(active);
		if (isTargeting) ShowTargets(); else ShowMoveOptions();
	}

	private void ClearHighlights() {
		foreach (GameNode node in highlighted) {
			if (GodotObject.IsInstanceValid(node)) node.ClearHighlight();
		}
		highlighted.Clear();

		foreach (TokenHighlight ring in targetRings) TokenHighlight.Detach(ring);
		targetRings.Clear();
		foreach (Enemy enemy in targetable) {
			if (GodotObject.IsInstanceValid(enemy)) enemy.MouseDefaultCursorShape = Control.CursorShape.Arrow;
		}
		targetable.Clear();
	}
}
