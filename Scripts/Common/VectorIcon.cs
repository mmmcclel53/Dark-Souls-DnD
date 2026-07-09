using Godot;

// A tiny procedurally-drawn UI glyph (sword / shield) so we don't need bespoke icon
// assets. Gold-tinted to match the equipment screen's theme.
public partial class VectorIcon : Control
{
    public enum IconKind { Sword, Shield }

    public IconKind kind = IconKind.Sword;
    public Color color = new Color(0.80f, 0.66f, 0.37f);

    public override void _Ready() {
        Resized += QueueRedraw;
    }

    public override void _Draw() {
        if (Size.X <= 0 || Size.Y <= 0) return;
        if (kind == IconKind.Sword) DrawSword(Size);
        else DrawShield(Size);
    }

    private void DrawSword(Vector2 s) {
        float cx = s.X * 0.5f;
        float w = s.X * 0.13f;
        var blade = new Vector2[] {
            new Vector2(cx,          s.Y * 0.04f),   // tip
            new Vector2(cx - w,      s.Y * 0.20f),
            new Vector2(cx - w * 0.6f, s.Y * 0.60f),
            new Vector2(cx + w * 0.6f, s.Y * 0.60f),
            new Vector2(cx + w,      s.Y * 0.20f),
        };
        DrawColoredPolygon(blade, color);
        DrawLine(new Vector2(cx - s.X * 0.30f, s.Y * 0.62f),
                 new Vector2(cx + s.X * 0.30f, s.Y * 0.62f), color, 2f);   // crossguard
        DrawLine(new Vector2(cx, s.Y * 0.62f), new Vector2(cx, s.Y * 0.88f), color, 2f); // grip
        DrawCircle(new Vector2(cx, s.Y * 0.92f), s.X * 0.07f, color);       // pommel
    }

    private void DrawShield(Vector2 s) {
        var pts = new Vector2[] {
            new Vector2(s.X * 0.16f, s.Y * 0.12f),
            new Vector2(s.X * 0.84f, s.Y * 0.12f),
            new Vector2(s.X * 0.84f, s.Y * 0.54f),
            new Vector2(s.X * 0.50f, s.Y * 0.92f),
            new Vector2(s.X * 0.16f, s.Y * 0.54f),
        };
        DrawColoredPolygon(pts, color);
        DrawLine(new Vector2(s.X * 0.50f, s.Y * 0.18f),
                 new Vector2(s.X * 0.50f, s.Y * 0.84f), new Color(0, 0, 0, 0.30f), 1.5f); // center ridge
    }
}
