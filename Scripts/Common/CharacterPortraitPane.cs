using Godot;

// Autoloaded floating UI: a CanvasLayer with a vertical stack of CharacterPortrait
// nodes on the left side of the screen. Survives scene changes. Scenes that want
// it visible call Show(); scenes that don't call Hide() in their _Ready.
//
// Clicking a portrait fires PortraitClicked(playerIndex).
public partial class CharacterPortraitPane : CanvasLayer
{
    [Signal] public delegate void PortraitClickedEventHandler(int playerIndex);

    private const string PORTRAIT_SCENE = "res://Resources/Prefabs/Player/CharacterPortrait.tscn";

    private const int DEFAULT_TOP_MARGIN = 80;

    private MarginContainer margin;
    private VBoxContainer container;
    private PackedScene portraitScene;
    private int selectedIndex = -1;

    public override void _Ready() {
        Layer = 5;
        Visible = false;

        portraitScene = ResourceLoader.Load<PackedScene>(PORTRAIT_SCENE);

        margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.LeftWide);
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_top", DEFAULT_TOP_MARGIN);
        margin.AddThemeConstantOverride("margin_bottom", 80);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(margin);

        container = new VBoxContainer();
        container.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        container.SizeFlagsVertical = Control.SizeFlags.Fill;
        container.AddThemeConstantOverride("separation", 8);
        container.MouseFilter = Control.MouseFilterEnum.Pass;
        margin.AddChild(container);
    }

    // Back to the default clearance: a scene that needs more says so after showing the
    // pane, and the one after it must not inherit that.
    public new void Show() {
        Visible = true;
        SetTopMargin(DEFAULT_TOP_MARGIN);
        Refresh();
    }

    public new void Hide() {
        Visible = false;
        selectedIndex = -1;
        SetTopMargin(DEFAULT_TOP_MARGIN);
    }

    // The pane floats over whatever scene is up, so the scene has to say how much HUD it
    // has to clear — the encounter stacks a title bar and a turn queue above the board.
    public void SetTopMargin(int pixels) {
        margin?.AddThemeConstantOverride("margin_top", pixels);
    }

    // Endurance lives on Player, so anything that moves a bar has to say so or the pane
    // keeps showing what the character had when the scene loaded.
    public void RefreshFor(Player player) {
        if (container == null || player == null) return;
        foreach (Node child in container.GetChildren()) {
            if (child is CharacterPortrait portrait && portrait.GetPlayer() == player) {
                portrait.Refresh();
                return;
            }
        }
    }

    public void Refresh() {
        if (container == null) return;
        foreach (Node child in container.GetChildren()) child.QueueFree();

        var players = CampaignManager.Players;
        if (players == null) return;

        for (int i = 0; i < players.Length; i++) {
            int idx = i;
            var portrait = portraitScene.Instantiate<CharacterPortrait>();
            container.AddChild(portrait);
            portrait.SetPlayer(players[i]);
            portrait.PortraitClicked += () => OnPortraitClicked(idx);
            portrait.SetSelected(idx == selectedIndex);
        }
    }

    public void SetSelectedIndex(int index) {
        selectedIndex = index;
        for (int i = 0; i < container.GetChildCount(); i++) {
            if (container.GetChild(i) is CharacterPortrait p)
                p.SetSelected(i == selectedIndex);
        }
    }

    public void RefreshPortrait(int index) {
        if (container == null) return;
        if (index < 0 || index >= container.GetChildCount()) return;
        if (container.GetChild(index) is CharacterPortrait p) p.Refresh();
    }

    private void OnPortraitClicked(int index) {
        EmitSignal(SignalName.PortraitClicked, index);
    }
}
