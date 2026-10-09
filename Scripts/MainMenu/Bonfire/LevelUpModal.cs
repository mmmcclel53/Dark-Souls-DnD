using Godot;
using System.Collections.Generic;
using System.Linq;

// The Firekeeper (p15): the Bonfire's Level Up screen. The character's board fills the left
// (LevelUpBoard); levels are queued on it square by square and nothing is spent until Confirm,
// which pays from the party's souls (SoulCache.Spend, oldest first) and places the cubes.
//
// The rules (Matt, Oct 2026; SoulEconomy): a level costs the level being reached, whichever
// stat it raises; the level is capped by how many sections' bosses are beaten
// (WorldMapManager.LevelCap); a character's free starting cubes (the Deprived's) go first, each
// on an empty row's Base square, for nothing.
//
// Respec (Matt) lifts every cube off the board and hands the same number back to place anew,
// so the level stays and no souls move: nothing is refunded to the pool, where it could be
// lost or sent back to its encounters. It confirms only once every cube is back on the board,
// and only while all the character carries can still be wielded.
public partial class LevelUpModal : CanvasLayer {
	private static readonly Color GOLD = new Color(0.80f, 0.66f, 0.37f);
	private static readonly Color TEXT = new Color(0.84f, 0.82f, 0.75f);
	private static readonly Color TEXT_DIM = new Color(0.62f, 0.60f, 0.54f);
	private static readonly Color PANEL_LINE = new Color(0.32f, 0.28f, 0.19f);
	private static readonly string[] STAT_NAMES = { "Strength", "Dexterity", "Intelligence", "Faith" };
	private const string SOUL_ICON = "res://Resources/Images/Sprites/Soul.png";
	private const int THUMB_W = 46, THUMB_H = 64;

	[Signal] public delegate void ClosedEventHandler();

	private Player player;
	// Stats queued to raise, in order. Replayed against the rules on every change (Replay), so
	// taking one back drops whatever no longer holds after it.
	private List<Player.Stat> queue = new List<Player.Stat>();
	private bool respec;

	private LevelUpBoard board;
	private Label nameLabel, classLabel, levelLabel, soulsLabel, costLabel, capLabel, freeLabel, moreLabel;
	private Label[] statLabels = new Label[4];
	private GridContainer unlocks, losses;
	private Control unlocksBox, lossesBox;
	private Button respecButton, confirmButton;

	public override void _Ready() {
		Layer = 10;
		Visible = false;
		BuildUi();
		board.SquareClicked += OnSquareClicked;
	}

	public bool IsOpen() => Visible;

	public void Open(Player p) {
		if (p == null) return;
		player = p;
		queue.Clear();
		SetRespec(false);
		Visible = true;
		Refresh();
	}

	public void Close() {
		Visible = false;
		queue.Clear();
		SetRespec(false);
		EmitSignal(SignalName.Closed);
	}

	private void Cycle(int step) {
		Player[] party = CampaignManager.Players;
		if (party.Length == 0) return;
		int index = System.Array.IndexOf(party, player);
		Open(party[((index + step) % party.Length + party.Length) % party.Length]);
	}

	// ---- The queue ----

	private struct Plan {
		public int[] tier;          // per stat, with the valid part of the queue placed
		public int free;            // free cubes it places (in a respec, every cube it places)
		public int bought;          // levels it buys
		public int cost;
		public int level;           // the character's level after it
		public bool[] canRaise;     // per stat: whether one more can be queued
	}

	// Walks the queue in order and keeps what the rules allow: free cubes first, each on an
	// empty row's Base; then levels, up to Tier 3, under the cap, while the souls last.
	private Plan Replay(bool trim) {
		int[] start = respec ? new[] { -1, -1, -1, -1 } : Player.STATS.Select(player.TierOf).ToArray();
		var plan = new Plan { tier = start, canRaise = new bool[4] };
		int cap = WorldMapManager.LevelCap(), souls = SoulCache.current, level = player.GetLevel();
		var kept = new List<Player.Stat>();
		foreach (Player.Stat stat in queue) {
			if (!Allowed(stat, plan, cap, souls, level)) continue;
			Place(stat, ref plan, level);
			kept.Add(stat);
		}
		if (trim) queue = kept;
		plan.level = level + plan.bought;
		foreach (Player.Stat stat in Player.STATS) plan.canRaise[(int)stat] = Allowed(stat, plan, cap, souls, level);
		return plan;
	}

	private bool Allowed(Player.Stat stat, Plan plan, int cap, int souls, int level) {
		int tier = plan.tier[(int)stat];
		if (respec) return tier < Player.TOP_TIER && plan.free < RespecCubes;
		if (plan.free < player.freeCubes) return tier < 0;
		int next = level + plan.bought + 1;
		return tier < Player.TOP_TIER && next <= cap && plan.cost + SoulEconomy.LevelCost(next) <= souls;
	}

	private void Place(Player.Stat stat, ref Plan plan, int level) {
		plan.tier[(int)stat]++;
		if (respec || plan.free < player.freeCubes) {
			plan.free++;
			return;
		}
		plan.bought++;
		plan.cost += SoulEconomy.LevelCost(level + plan.bought);
	}

	// Every cube the character has, starting ones included: what a respec places again.
	private int RespecCubes => player.CubesPlaced() + player.freeCubes;

	// A lit square queues one more; a queued cube comes off with every queued one above it.
	private void OnSquareClicked(int stat, int tier) {
		var s = (Player.Stat)stat;
		Plan plan = Replay(false);
		if (tier > plan.tier[stat]) queue.Add(s);
		else for (int i = tier; i <= plan.tier[stat]; i++) queue.RemoveAt(queue.LastIndexOf(s));
		Refresh();
	}

	private void SetRespec(bool on) {
		respec = on;
		queue.Clear();
		respecButton.SetPressedNoSignal(on);
		foreach (string state in new[] { "font_color", "font_pressed_color", "font_hover_pressed_color", "font_hover_color" })
			respecButton.AddThemeColorOverride(state, on ? GOLD : TEXT);
	}

	private void Confirm() {
		Plan plan = Replay(true);
		if (queue.Count == 0) return;
		if (respec) {
			if (plan.free < RespecCubes || Unwieldable(plan).Any()) return;
			player.Respec(plan.tier);
			SetRespec(false);
			CampaignManager.Autosave();
			Refresh();
			return;
		}
		if (plan.cost > 0 && !SoulCache.Spend(plan.cost)) return;
		foreach (Player.Stat stat in queue) player.Raise(stat);
		player.freeCubes -= plan.free;
		queue.Clear();
		CampaignManager.Autosave();
		Refresh();
	}

	// ---- Readout ----

	private void Refresh() {
		if (player == null) return;
		Plan plan = Replay(true);
		int level = player.GetLevel(), cap = WorldMapManager.LevelCap(), souls = SoulCache.current;

		board.player = player;
		for (int i = 0; i < 4; i++) {
			board.placed[i] = respec ? -1 : player.TierOf(Player.STATS[i]);
			board.queued[i] = plan.tier[i];
			board.canRaise[i] = plan.canRaise[i];
		}

		nameLabel.Text = player.name;
		classLabel.Text = player.character?.name ?? "";
		levelLabel.Text = plan.level == level ? level.ToString() : $"{level}  →  {plan.level}";
		capLabel.Text = cap < SoulEconomy.MAX_LEVEL ? $"max {cap}" : "";
		soulsLabel.Text = plan.cost == 0 ? souls.ToString() : $"{souls}  →  {souls - plan.cost}";

		int next = plan.level + 1;
		bool full = respec || plan.level >= SoulEconomy.MAX_LEVEL || next > cap;
		costLabel.Text = full ? "—" : SoulEconomy.LevelCost(next).ToString();
		costLabel.AddThemeColorOverride("font_color", !full && plan.cost + SoulEconomy.LevelCost(next) > souls ? EquipmentModal.WORSE : TEXT);

		int freeLeft = (respec ? RespecCubes : player.freeCubes) - plan.free;
		freeLabel.Visible = freeLeft > 0;
		freeLabel.Text = respec
			? $"{freeLeft} {(freeLeft == 1 ? "cube" : "cubes")} to place"
			: $"{freeLeft} free {(freeLeft == 1 ? "cube" : "cubes")}";

		int[] before = Player.STATS.Select(player.GetStat).ToArray();
		int[] after = Player.STATS.Select(s => ValueAt(s, plan.tier[(int)s])).ToArray();
		for (int i = 0; i < 4; i++) {
			bool changed = after[i] != before[i];
			statLabels[i].Text = changed ? $"{before[i]}  →  {after[i]}" : before[i].ToString();
			Color colour = after[i] > before[i] ? EquipmentModal.BETTER : after[i] < before[i] ? EquipmentModal.WORSE : TEXT;
			statLabels[i].AddThemeColorOverride("font_color", colour);
		}

		ShowUnlocks(before, after);
		var lost = Unwieldable(plan).ToList();
		ShowItems(losses, lost);
		lossesBox.Visible = lost.Count > 0;
		respecButton.Disabled = RespecCubes == 0;
		confirmButton.Disabled = queue.Count == 0 || respec && (freeLeft > 0 || lost.Count > 0);
	}

	// In a respec, what the character carries that the new spread could not wield (p12). Levels
	// only ever raise stats, so outside a respec nothing is lost.
	private IEnumerable<Equipment> Unwieldable(Plan plan) {
		if (!respec) return Enumerable.Empty<Equipment>();
		int[] after = Player.STATS.Select(s => ValueAt(s, plan.tier[(int)s])).ToArray();
		return player.CarriedItems().Where(e => !player.MeetsRequirements(e, after)).GroupBy(e => e.name).Select(g => g.First());
	}

	private void ShowItems(GridContainer grid, IEnumerable<Equipment> items) {
		foreach (Node child in grid.GetChildren()) child.QueueFree();
		foreach (Equipment item in items) grid.AddChild(Thumbnail(item));
	}

	private int ValueAt(Player.Stat stat, int tier) => tier < 0 ? 0 : player.Tiers(stat)[tier];

	// Owned equipment the queued levels make wieldable, and a count of the rest of the game's.
	private void ShowUnlocks(int[] before, int[] after) {
		var owned = GameManager.GetAllInstances()
			.Where(e => e != null && !player.MeetsRequirements(e, before) && player.MeetsRequirements(e, after))
			.GroupBy(e => e.name).Select(g => g.First()).ToList();
		var ownedNames = owned.Select(e => e.name).ToHashSet();
		int more = GameManager.GetAllTemplates()
			.Count(e => e != null && !ownedNames.Contains(e.name) && !player.MeetsRequirements(e, before) && player.MeetsRequirements(e, after));

		ShowItems(unlocks, owned);
		moreLabel.Text = more > 0 ? $"{(owned.Count > 0 ? "and " : "")}{more} more in the game" : "";
		unlocksBox.Visible = owned.Count > 0 || more > 0;
	}

	private static Control Thumbnail(Equipment item) {
		var frame = new PanelContainer { TooltipText = item.name, MouseFilter = Control.MouseFilterEnum.Pass };
		var border = new StyleBoxFlat { BgColor = new Color(0.07f, 0.07f, 0.07f, 0.85f), BorderColor = EquipmentModal.RarityColor(item.rarity) };
		border.SetBorderWidthAll(2);
		border.SetCornerRadiusAll(3);
		frame.AddThemeStyleboxOverride("panel", border);
		frame.AddChild(new TextureRect {
			Texture = item.image,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(THUMB_W, THUMB_H),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		});
		return frame;
	}

	// ---- Layout ----

	private void BuildUi() {
		var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.65f), MouseFilter = Control.MouseFilterEnum.Stop };
		backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddChild(backdrop);

		var frame = new PanelContainer();
		frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.KeepSize, 28);
		var frameStyle = new StyleBoxFlat { BgColor = new Color(0.055f, 0.052f, 0.05f, 0.99f), BorderColor = PANEL_LINE };
		frameStyle.SetBorderWidthAll(2);
		frameStyle.SetCornerRadiusAll(4);
		frameStyle.ContentMarginLeft = frameStyle.ContentMarginRight = 20;
		frameStyle.ContentMarginTop = frameStyle.ContentMarginBottom = 16;
		frame.AddThemeStyleboxOverride("panel", frameStyle);
		AddChild(frame);

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 20);
		frame.AddChild(columns);

		board = new LevelUpBoard { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill, SizeFlagsStretchRatio = 2.4f };
		columns.AddChild(board);

		var side = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(280, 0) };
		side.AddThemeConstantOverride("separation", 10);
		columns.AddChild(side);

		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 8);
		side.AddChild(header);
		header.AddChild(ArrowButton("‹", () => Cycle(-1)));
		var names = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		names.AddThemeConstantOverride("separation", 0);
		header.AddChild(names);
		nameLabel = MakeLabel(22, GOLD);
		nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
		names.AddChild(nameLabel);
		classLabel = MakeLabel(14, TEXT_DIM);
		classLabel.HorizontalAlignment = HorizontalAlignment.Center;
		names.AddChild(classLabel);
		header.AddChild(ArrowButton("›", () => Cycle(1)));

		side.AddChild(Rule());

		var readout = new GridContainer { Columns = 3 };
		readout.AddThemeConstantOverride("h_separation", 14);
		readout.AddThemeConstantOverride("v_separation", 6);
		side.AddChild(readout);
		levelLabel = AddRow(readout, MakeLabel(16, TEXT_DIM, "Level"));
		capLabel = MakeLabel(13, TEXT_DIM);
		readout.AddChild(capLabel);
		var soulIcon = new TextureRect {
			Texture = GD.Load<Texture2D>(SOUL_ICON), CustomMinimumSize = new Vector2(18, 18),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
		};
		soulsLabel = AddRow(readout, soulIcon);
		readout.AddChild(new Control());
		costLabel = AddRow(readout, MakeLabel(16, TEXT_DIM, "Next level"));
		readout.AddChild(new Control());

		freeLabel = MakeLabel(14, GOLD);
		freeLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		side.AddChild(freeLabel);

		side.AddChild(Rule());

		var stats = new GridContainer { Columns = 2 };
		stats.AddThemeConstantOverride("h_separation", 14);
		stats.AddThemeConstantOverride("v_separation", 4);
		side.AddChild(stats);
		for (int i = 0; i < 4; i++) statLabels[i] = AddRow(stats, MakeLabel(15, TEXT_DIM, STAT_NAMES[i]));

		unlocksBox = new VBoxContainer();
		unlocksBox.AddThemeConstantOverride("separation", 6);
		side.AddChild(unlocksBox);
		unlocksBox.AddChild(Rule());
		unlocksBox.AddChild(MakeLabel(16, GOLD, "Unlocks"));
		unlocks = new GridContainer { Columns = 5 };
		unlocks.AddThemeConstantOverride("h_separation", 6);
		unlocks.AddThemeConstantOverride("v_separation", 6);
		unlocksBox.AddChild(unlocks);
		moreLabel = MakeLabel(13, TEXT_DIM);
		unlocksBox.AddChild(moreLabel);

		lossesBox = new VBoxContainer();
		lossesBox.AddThemeConstantOverride("separation", 6);
		side.AddChild(lossesBox);
		lossesBox.AddChild(Rule());
		lossesBox.AddChild(MakeLabel(16, EquipmentModal.WORSE, "Unwieldable"));
		losses = new GridContainer { Columns = 5 };
		losses.AddThemeConstantOverride("h_separation", 6);
		losses.AddThemeConstantOverride("v_separation", 6);
		lossesBox.AddChild(losses);

		side.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

		var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
		buttons.AddThemeConstantOverride("separation", 8);
		side.AddChild(buttons);
		respecButton = MakeButton("Respec", () => { });
		respecButton.ToggleMode = true;
		respecButton.Toggled += on => { SetRespec(on); Refresh(); };
		buttons.AddChild(respecButton);
		buttons.AddChild(MakeButton("Close", Close));
		confirmButton = MakeButton("Confirm", Confirm);
		confirmButton.AddThemeColorOverride("font_color", GOLD);
		buttons.AddChild(confirmButton);
	}

	private static Label AddRow(GridContainer grid, Control name) {
		grid.AddChild(name);
		var value = MakeLabel(16, TEXT);
		grid.AddChild(value);
		return value;
	}

	private static Label MakeLabel(int size, Color colour, string text = "") {
		var label = new Label { Text = text };
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", colour);
		return label;
	}

	private static Button MakeButton(string text, System.Action pressed) {
		var button = new Button { Text = text, CustomMinimumSize = new Vector2(88, 30) };
		button.Pressed += () => pressed();
		return button;
	}

	private static Button ArrowButton(string text, System.Action pressed) {
		var button = MakeButton(text, pressed);
		button.CustomMinimumSize = new Vector2(30, 30);
		return button;
	}

	private static Control Rule() {
		var rule = new ColorRect { Color = PANEL_LINE, CustomMinimumSize = new Vector2(0, 1), MouseFilter = Control.MouseFilterEnum.Ignore };
		return rule;
	}
}
