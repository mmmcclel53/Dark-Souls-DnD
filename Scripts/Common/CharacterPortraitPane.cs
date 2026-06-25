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

    private VBoxContainer container;
    private PackedScene portraitScene;
    private int selectedIndex = -1;

    public override void _Ready() {
        Layer = 5;
        Visible = false;

        portraitScene = ResourceLoader.Load<PackedScene>(PORTRAIT_SCENE);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.LeftWide);
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_top", 80);
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

    public new void Show() {
        Visible = true;
        Refresh();
    }

    public new void Hide() {
        Visible = false;
        selectedIndex = -1;
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
