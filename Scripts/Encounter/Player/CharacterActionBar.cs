using Godot;
using System.Collections.Generic;

// The active character's options for this activation: one row per hand-slot weapon, one
// button per attack option on it, plus End Activation.
//
// Rebuilt on every change rather than diffed — a character has at most two weapons with a
// handful of options each, and a stale button here would let a player take an illegal turn.
public partial class CharacterActionBar : Control
{

	[Signal] public delegate void AttackChosenEventHandler(Weapon weapon, PlayerMove move);
	[Signal] public delegate void EndActivationPressedEventHandler();

	[Export] public Label nameLabel;
	[Export] public Label staminaLabel;
	[Export] public Label statusLabel;
	[Export] public Container weaponRows;
	[Export] public Button endActivationButton;

	public override void _Ready() {
		if (endActivationButton != null) {
			endActivationButton.Pressed += () => EmitSignal(SignalName.EndActivationPressed);
		}
	}

	// The bar keeps its place in the layout between activations rather than disappearing,
	// so the board does not resize — and every token on it jump — twice a round.
	public void ShowIdle(string status) {
		if (nameLabel != null) nameLabel.Text = "";
		if (staminaLabel != null) staminaLabel.Text = "";
		if (statusLabel != null) statusLabel.Text = status;
		if (endActivationButton != null) endActivationButton.Disabled = true;
		if (weaponRows != null) {
			foreach (Node child in weaponRows.GetChildren()) child.QueueFree();
		}
	}

	public void Refresh(PlayerToken token) {
		if (token == null) return;

		if (nameLabel != null) nameLabel.Text = token.player.name;
		if (staminaLabel != null) staminaLabel.Text = $"{token.endurance.free} left";
		if (statusLabel != null) statusLabel.Text = MovementStatus(token);
		if (endActivationButton != null) endActivationButton.Disabled = false;

		BuildWeaponRows(token);
	}

	private static string MovementStatus(PlayerToken token) {
		if (token.movementLocked) return "Movement spent — it already happened before the attack";
		if (!token.CanStep()) return "No endurance left to move";
		return token.hasWalked ? "Run: 1 per node" : "Walk: first node free";
	}

	private void BuildWeaponRows(PlayerToken token) {
		if (weaponRows == null) return;
		foreach (Node child in weaponRows.GetChildren()) child.QueueFree();

		foreach (Weapon weapon in HandWeapons(token.player)) {
			HBoxContainer row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 6);
			weaponRows.AddChild(row);

			Label label = new Label();
			label.Text = weapon.name;
			label.CustomMinimumSize = new Vector2(150, 0);
			label.AddThemeFontSizeOverride("font_size", 12);
			row.AddChild(label);

			bool used = token.HasAttackedWith(weapon);
			if (weapon.attacks == null || weapon.attacks.Count == 0) {
				row.AddChild(Note("no attack options"));
				continue;
			}

			foreach (PlayerMove move in weapon.attacks) {
				if (move == null) continue;
				row.AddChild(AttackButton(token, weapon, move, used));
			}

			if (used) row.AddChild(Note("already attacked"));
		}
	}

	private Button AttackButton(PlayerToken token, Weapon weapon, PlayerMove move, bool weaponUsed) {
		int cost = CombatResolver.AttackStaminaCost(token, move);

		Button button = new Button();
		button.Text = $"{DiceSummary(move)}  ·  {cost} sta";
		button.AddThemeFontSizeOverride("font_size", 12);
		button.Disabled = weaponUsed || !token.CanSpend(cost);
		button.TooltipText = Describe(weapon, move, cost);

		// Capture the loop values before the lambda, or every button fires the last option.
		Weapon capturedWeapon = weapon;
		PlayerMove capturedMove = move;
		button.Pressed += () => EmitSignal(SignalName.AttackChosen, capturedWeapon, capturedMove);
		return button;
	}

	private static string DiceSummary(PlayerMove move) {
		if (move.damage == null || move.damage.Count == 0) {
			return move.modifier > 0 ? $"{move.modifier} dmg" : "no dice";
		}

		Dictionary<DiceUtility.DICE_TYPE, int> counts = new Dictionary<DiceUtility.DICE_TYPE, int>();
		foreach (Dice die in move.damage) {
			if (die == null) continue;
			counts.TryGetValue(die.diceType, out int n);
			counts[die.diceType] = n + 1;
		}

		List<string> parts = new List<string>();
		foreach (var pair in counts) parts.Add($"{pair.Value}{Initial(pair.Key)}");
		string dice = string.Join(" ", parts);
		return move.modifier != 0 ? $"{dice} {move.modifier:+#;-#;+0}" : dice;
	}

	private static string Initial(DiceUtility.DICE_TYPE type) => type switch {
		DiceUtility.DICE_TYPE.BLACK => "B",
		DiceUtility.DICE_TYPE.BLUE => "U",
		DiceUtility.DICE_TYPE.ORANGE => "O",
		_ => "?",
	};

	private static string Describe(Weapon weapon, PlayerMove move, int cost) {
		int range = move.attackRange > 0 ? move.attackRange : weapon.attackRange;
		List<string> notes = new List<string> { $"Range {range}", $"{cost} stamina" };

		if (move.isMagic) notes.Add("magical");
		if (move.isAOE) notes.Add("whole node");
		if (move.isNotZeroRange) notes.Add("cannot hit range 0");
		if (move.isIgnoreDefense) notes.Add("ignores Block");
		if (move.repeat > 1) notes.Add($"repeats x{move.repeat} (not implemented)");
		if (move.bonusMovement > 0) notes.Add($"shift {move.bonusMovement} (not implemented)");

		return string.Join("  ·  ", notes);
	}

	private static List<Weapon> HandWeapons(Player player) {
		List<Weapon> weapons = new List<Weapon>();
		foreach (Weapon weapon in new[] { player.GetLeftHand(), player.GetRightHand() }) {
			if (weapon != null && !weapons.Contains(weapon)) weapons.Add(weapon);
		}
		return weapons;
	}

	private static Label Note(string text) {
		Label label = new Label();
		label.Text = text;
		label.AddThemeFontSizeOverride("font_size", 11);
		label.Modulate = new Color(1, 1, 1, 0.5f);
		return label;
	}
}
