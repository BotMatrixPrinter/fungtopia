using Godot;

namespace Fungtopia.HUD.View;

/// <summary>The quest tracker's marker: a little spotted mushroom cap, turning into a green check when the quest is done.</summary>
public partial class QuestIcon : Control
{
    private static readonly Color Leaf = new(0.45f, 0.78f, 0.42f);

    private bool _complete;
    private bool _drawn;

    public QuestIcon() => MouseFilter = MouseFilterEnum.Ignore;

    public void Show(bool complete)
    {
        if (_drawn && complete == _complete)
            return;
        _complete = complete;
        _drawn = true;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var c = Size * 0.5f;
        float r = Mathf.Min(Size.X, Size.Y) * 0.5f - 2f;
        if (_complete)
        {
            DrawCircle(c, r, Leaf);
            DrawArc(c, r, 0f, Mathf.Tau, 32, HudStyle.Ink, 3f, true);
            DrawPolyline(new[] { c + new Vector2(-r * 0.45f, 0f), c + new Vector2(-r * 0.1f, r * 0.38f), c + new Vector2(r * 0.5f, -r * 0.38f) },
                Colors.White, r * 0.24f, true);
            return;
        }
        DrawCircle(c, r, HudStyle.Cream);
        DrawArc(c, r, 0f, Mathf.Tau, 32, HudStyle.Ink, 3f, true);
        // A tiny cap with two spots.
        var cap = c + new Vector2(0, r * 0.18f);
        var dome = new Vector2[13];
        for (int i = 0; i <= 12; i++)
        {
            float a = Mathf.Pi + Mathf.Pi * i / 12f;
            dome[i] = cap + new Vector2(Mathf.Cos(a) * r * 0.72f, Mathf.Sin(a) * r * 0.62f);
        }
        DrawColoredPolygon(dome, HudStyle.Cap);
        DrawCircle(cap + new Vector2(-r * 0.22f, -r * 0.42f), r * 0.1f, HudStyle.Spot);
        DrawCircle(cap + new Vector2(r * 0.2f, -r * 0.5f), r * 0.08f, HudStyle.Spot);
    }
}
