using System;
using Godot;

namespace Fungtopia.HUD.View;

/// <summary>
/// A rounded mushroom-cap panel: a soft pink dome with cream spots and an ink outline, and a cream
/// band along its bottom where the contents sit.
/// </summary>
public partial class MushroomPanel : Control
{
    private const int ArcPoints = 28;
    private const float Corner = 22f;
    private const float Outline = 4f;

    private static readonly Vector3[] Spots =
    {
        new(0.15f, 0.62f, 0.13f), new(0.33f, 0.3f, 0.18f), new(0.55f, 0.22f, 0.12f), new(0.72f, 0.36f, 0.16f), new(0.88f, 0.66f, 0.11f),
    };

    private Vector2[] _shape = Array.Empty<Vector2>();
    private Vector2[] _outline = Array.Empty<Vector2>();
    private Vector2 _shapeSize;
    private readonly StyleBoxFlat _band = new()
    {
        BgColor = HudStyle.Cream,
        BorderColor = HudStyle.Ink,
        CornerDetail = 10,
        AntiAliasing = true,
    };

    /// <summary>Height of the cream band at the bottom, in pixels.</summary>
    public float BandHeight { get; set; } = 56f;

    public MushroomPanel() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw()
    {
        if (_shapeSize != Size)
            Build();
        DrawColoredPolygon(_shape, HudStyle.Cap);
        float w = Size.X, capHeight = Size.Y - BandHeight;
        foreach (var spot in Spots)
        {
            // x across the panel, y down the cap, radius: fractions of the cap's height.
            DrawCircle(new Vector2(spot.X * w, spot.Y * capHeight), spot.Z * capHeight, HudStyle.Spot);
        }
        DrawPolyline(_outline, HudStyle.Ink, Outline, true);

        var band = new Rect2(10f, Size.Y - BandHeight - 4f, w - 20f, BandHeight - 4f);
        _band.SetCornerRadiusAll((int)(BandHeight * 0.5f));
        _band.SetBorderWidthAll(3);
        DrawStyleBox(_band, band);
    }

    /// <summary>The dome: a half ellipse on top, straight sides, rounded bottom corners.</summary>
    private void Build()
    {
        _shapeSize = Size;
        float w = Size.X, h = Size.Y, dome = MathF.Min((h - BandHeight) * 0.9f, w * 0.5f);
        var points = new System.Collections.Generic.List<Vector2>();
        for (int i = 0; i <= ArcPoints; i++)
        {
            float a = MathF.PI + MathF.PI * i / ArcPoints;
            points.Add(new Vector2(w * 0.5f + MathF.Cos(a) * w * 0.5f, dome + MathF.Sin(a) * dome));
        }
        for (int i = 0; i <= 6; i++)
        {
            float a = MathF.PI * 0.5f * i / 6;
            points.Add(new Vector2(w - Corner + MathF.Cos(a) * Corner, h - Corner + MathF.Sin(a) * Corner));
        }
        for (int i = 0; i <= 6; i++)
        {
            float a = MathF.PI * 0.5f + MathF.PI * 0.5f * i / 6;
            points.Add(new Vector2(Corner + MathF.Cos(a) * Corner, h - Corner + MathF.Sin(a) * Corner));
        }
        _shape = points.ToArray();
        points.Add(points[0]);
        _outline = points.ToArray();
    }
}
