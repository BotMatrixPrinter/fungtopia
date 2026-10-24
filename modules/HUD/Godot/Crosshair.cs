using System;
using Godot;

namespace Fungtopia.HUD.View;

/// <summary>The center crosshair: a soft dot, and around it the bow draw ring filling clockwise.</summary>
public partial class Crosshair : Control
{
    public const float Radius = 30f;
    private const float Width = 7f;

    private float _draw = -1f;
    private bool _visible;

    public Crosshair() => MouseFilter = MouseFilterEnum.Ignore;

    public void Show(float draw, bool visible)
    {
        if (draw == _draw && visible == _visible)
            return;
        _draw = draw;
        _visible = visible;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, 6f, HudStyle.Ink);
        DrawCircle(Vector2.Zero, 3.5f, HudStyle.Cream);
        if (!_visible)
            return;
        DrawArc(Vector2.Zero, Radius, 0f, MathF.Tau, 48, new Color(HudStyle.Ink, 0.7f), Width + 5f, true);
        DrawArc(Vector2.Zero, Radius, 0f, MathF.Tau, 48, new Color(HudStyle.Cream, 0.55f), Width, true);
        if (_draw <= 0f)
            return;
        bool full = _draw >= 1f;
        var color = full ? HudStyle.Gold : HudStyle.Heart.Lerp(HudStyle.Gold, _draw * _draw);
        float start = -MathF.PI * 0.5f;
        int points = Math.Max(3, (int)(48 * _draw));
        DrawArc(Vector2.Zero, Radius, start, start + MathF.Tau * _draw, points, color, Width, true);
        if (full)
            DrawArc(Vector2.Zero, Radius + 8f, 0f, MathF.Tau, 48, new Color(HudStyle.Gold, 0.5f), 3f, true);
    }
}
