using System;
using Godot;

namespace Fungtopia.HUD.View;

/// <summary>A row of chunky hearts with big round eyes; empty ones sleep.</summary>
public partial class HeartRow : Control
{
    public const float HeartSize = 44f;
    public const float Gap = 10f;
    private const int Points = 40;

    private static readonly Vector2[] Whole = HeartPoints(0, Points);
    // From the bottom tip up the left side to the dip at the top.
    private static readonly Vector2[] LeftHalf = HeartPoints(Points / 2, Points / 2 + 1);
    private static readonly Vector2[] Outline = Closed(Whole);

    private readonly float[] _fills = new float[16];
    private int _count;
    private readonly Vector2[] _fill = new Vector2[Points];
    private readonly Vector2[] _half = new Vector2[Points / 2 + 1];
    private readonly Vector2[] _outline = new Vector2[Points + 1];

    public HeartRow() => MouseFilter = MouseFilterEnum.Ignore;

    /// <summary>Sets the hearts; redraws only when something changed.</summary>
    public void Show(HudModule hud)
    {
        int count = Math.Min(hud.HeartCount, _fills.Length);
        bool changed = count != _count;
        for (int i = 0; i < count; i++)
        {
            float fill = hud.HeartFill(i);
            changed |= fill != _fills[i];
            _fills[i] = fill;
        }
        _count = count;
        if (changed)
        {
            CustomMinimumSize = new Vector2(Math.Max(0, count * (HeartSize + Gap) - Gap), HeartSize);
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        for (int i = 0; i < _count; i++)
        {
            var center = new Vector2(i * (HeartSize + Gap) + HeartSize * 0.5f, HeartSize * 0.52f);
            float fill = _fills[i];
            Place(Whole, center, _fill);
            DrawColoredPolygon(_fill, fill >= 1f ? HudStyle.Heart : HudStyle.HeartEmpty);
            if (fill > 0f && fill < 1f)
            {
                Place(LeftHalf, center, _half);
                DrawColoredPolygon(_half, HudStyle.Heart);
            }
            if (fill > 0f)
                DrawCircle(center + new Vector2(-HeartSize * 0.22f, -HeartSize * 0.16f), HeartSize * 0.08f, new Color(1, 1, 1, 0.8f));
            Place(Outline, center, _outline);
            DrawPolyline(_outline, HudStyle.Ink, 3.5f, true);

            var eyes = center + new Vector2(0, HeartSize * 0.02f);
            if (fill > 0f)
                HudStyle.DrawEyes(this, eyes, HeartSize * 0.34f, HeartSize * 0.085f);
            else
                for (int side = -1; side <= 1; side += 2)
                    DrawArc(eyes + new Vector2(side * HeartSize * 0.17f, -2f), HeartSize * 0.07f, 0.2f, MathF.PI - 0.2f, 8, HudStyle.Ink, 2.5f, true);
        }
    }

    private static void Place(Vector2[] shape, Vector2 center, Vector2[] into)
    {
        for (int i = 0; i < shape.Length; i++)
            into[i] = center + shape[i] * HeartSize;
    }

    /// <summary>The classic heart curve, scaled to a unit box centered on the origin (Y down).</summary>
    private static Vector2[] HeartPoints(int from, int count)
    {
        var points = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            float t = MathF.Tau * (from + i) / Points;
            float x = 16f * MathF.Pow(MathF.Sin(t), 3);
            float y = 13f * MathF.Cos(t) - 5f * MathF.Cos(2 * t) - 2f * MathF.Cos(3 * t) - MathF.Cos(4 * t);
            points[i] = new Vector2(x / 34f, -(y + 2.5f) / 32f);
        }
        return points;
    }

    private static Vector2[] Closed(Vector2[] points)
    {
        var closed = new Vector2[points.Length + 1];
        points.CopyTo(closed, 0);
        closed[^1] = points[0];
        return closed;
    }
}
