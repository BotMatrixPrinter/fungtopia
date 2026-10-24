using System;
using System.Collections.Generic;
using Godot;

namespace Fungtopia.Travel.View;

/// <summary>
/// The full-screen travel map, drawn in code like Tony's drawing: three big ink-outlined circles on
/// parchment joined by dotted roads — Mushroom Town (houses with mushroom roofs under clouds),
/// Mushroom Forest (big spotted mushrooms with eyes) and Pet City (tall buildings) — each labeled,
/// a pin over "you are here" and a bouncing glow on the selected one. It draws on the virtual
/// <see cref="MapLayout"/> canvas, scaled to fit the screen. The shapes are built once.
/// </summary>
public partial class MapCanvas : Control
{
    private static readonly Color Ink = new(0.24f, 0.13f, 0.12f);
    private static readonly Color Dim = new(0.08f, 0.04f, 0.08f, 0.55f);
    private static readonly Color Parchment = new(0.97f, 0.9f, 0.74f);
    private static readonly Color ParchmentEdge = new(0.86f, 0.72f, 0.5f);
    private static readonly Color Stain = new(0.9f, 0.79f, 0.58f, 0.55f);
    private static readonly Color Road = new(0.58f, 0.4f, 0.24f);
    private static readonly Color Ribbon = new(1f, 0.97f, 0.9f);
    private static readonly Color Glow = new(1f, 0.82f, 0.3f);
    private static readonly Color Pin = new(0.98f, 0.45f, 0.62f);
    private static readonly Color White = new(1f, 1f, 1f);
    private static readonly Color Cream = new(1f, 0.95f, 0.84f);
    private static readonly Color CapRed = new(0.92f, 0.25f, 0.28f);
    private static readonly Color[] Fills =
    {
        new(0.74f, 0.9f, 0.99f),
        new(0.76f, 0.9f, 0.62f),
        new(0.87f, 0.83f, 0.98f),
    };

    private sealed class Shape
    {
        public Vector2[] Points = Array.Empty<Vector2>();
        public Vector2[] Outline = Array.Empty<Vector2>();
        public Color Fill;
        public float InkWidth;
    }

    private readonly List<Shape> _background = new();
    private readonly List<Shape>[] _places = { new(), new(), new() };
    private readonly List<Vector2> _roadDots = new();
    private Vector2[] _ring = Array.Empty<Vector2>();
    private Vector2[] _disc = Array.Empty<Vector2>();
    private Font _font = null!;

    /// <summary>The Core to draw (selection, "you are here"); the preview can draw without one.</summary>
    public TravelModule? Travel { get; set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _font = new FontVariation { BaseFont = ThemeDB.FallbackFont, VariationEmbolden = 0.9f };
        Build();
    }

    public override void _Process(double delta)
    {
        if (Visible)
            QueueRedraw();
    }

    public override void _Draw()
    {
        var screen = new System.Numerics.Vector2(Size.X, Size.Y);
        float scale = MapLayout.Scale(screen);
        var o = MapLayout.Origin(screen);
        var origin = new Vector2(o.X, o.Y);
        float time = (float)Time.GetTicksMsec() / 1000f;
        int selected = Travel?.Selected ?? -1;
        int here = Travel?.Here ?? 0;

        DrawRect(new Rect2(Vector2.Zero, Size), Dim);
        DrawSetTransform(origin, 0f, new Vector2(scale, scale));
        DrawShapes(_background);
        Text("Fungtopia", new Vector2(MapLayout.Width * 0.5f, 140f), 80, Ink, Ribbon, 14);
        foreach (var dot in _roadDots)
            DrawCircle(dot, 8f, Road, true, -1f, true);
        Text("Click a place, or choose with arrows / WASD / stick and press Enter / E / A.   Esc / B: close",
            new Vector2(MapLayout.Width * 0.5f, 1000f), 30, Ink, Parchment, 0);

        for (int i = 0; i < MapLayout.Centers.Length; i++)
        {
            var c = MapLayout.Centers[i];
            bool on = i == selected;
            float s = on ? 1.05f + 0.015f * MathF.Sin(time * 5f) : 1f;
            DrawSetTransform(origin + new Vector2(c.X, c.Y) * scale, 0f, new Vector2(scale * s, scale * s));
            float r = MapLayout.Radius;
            if (on)
            {
                var glow = new Color(Glow, 0.55f + 0.25f * MathF.Sin(time * 5f));
                DrawArc(Vector2.Zero, r + 20f, 0f, MathF.Tau, 96, glow, 22f, true);
            }
            DrawColoredPolygon(_disc, Fills[i]);
            DrawShapes(_places[i]);
            DrawPolyline(_ring, Ink, 10f, true);

            // The label on a ribbon under the circle.
            var label = new Rect2(-235f, r + 22f, 470f, 76f);
            DrawStyleBox(RibbonBox(on), label);
            Text(MapLayout.Name(i), new Vector2(0f, r + 76f), 44, Ink, Ribbon, 0);

            if (i == here)
                DrawPin(r, time);
        }
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private void DrawPin(float r, float time)
    {
        float bob = 6f * MathF.Sin(time * 3f);
        var tip = new Vector2(0f, -r + 6f + bob);
        var head = new Vector2(0f, -r - 46f + bob);
        DrawColoredPolygon(new[] { tip, head + new Vector2(-20f, 8f), head + new Vector2(20f, 8f) }, Ink);
        DrawColoredPolygon(new[] { tip + new Vector2(0f, -7f), head + new Vector2(-15f, 8f), head + new Vector2(15f, 8f) }, Pin);
        DrawCircle(head, 30f, Ink, true, -1f, true);
        DrawCircle(head, 25f, Pin, true, -1f, true);
        DrawCircle(head + new Vector2(-7f, -7f), 8f, White, true, -1f, true);
        Text("You are here!", new Vector2(0f, -r - 92f + bob), 36, Ink, Ribbon, 10);
    }

    private static StyleBoxFlat RibbonBox(bool selected) => selected ? s_ribbonSelected : s_ribbon;

    private static readonly StyleBoxFlat s_ribbon = MakeRibbon(Ribbon);
    private static readonly StyleBoxFlat s_ribbonSelected = MakeRibbon(new Color(1f, 0.9f, 0.55f));

    private static StyleBoxFlat MakeRibbon(Color fill)
    {
        var box = new StyleBoxFlat { BgColor = fill, BorderColor = Ink, AntiAliasing = true };
        box.SetBorderWidthAll(6);
        box.SetCornerRadiusAll(38);
        return box;
    }

    private void Text(string text, Vector2 center, int size, Color color, Color outline, int outlineSize)
    {
        const float width = 1600f;
        var at = new Vector2(center.X - width * 0.5f, center.Y);
        if (outlineSize > 0)
            DrawStringOutline(_font, at, text, HorizontalAlignment.Center, width, size, outlineSize, outline);
        DrawString(_font, at, text, HorizontalAlignment.Center, width, size, color);
    }

    private void DrawShapes(List<Shape> shapes)
    {
        foreach (var shape in shapes)
        {
            DrawColoredPolygon(shape.Points, shape.Fill);
            if (shape.InkWidth > 0f)
                DrawPolyline(shape.Outline, Ink, shape.InkWidth, true);
        }
    }

    // ---- Building the shapes (once) ----

    private void Build()
    {
        float r = MapLayout.Radius;
        _disc = Ellipse(Vector2.Zero, r, r, 96);
        _ring = Closed(_disc);

        // Parchment with a darker inner edge and a few stains.
        Add(_background, RoundedRect(new Rect2(60f, 40f, 1800f, 1000f), 46f), ParchmentEdge, 8f);
        Add(_background, RoundedRect(new Rect2(80f, 60f, 1760f, 960f), 34f), Parchment, 0f);
        Add(_background, Ellipse(new Vector2(250f, 220f), 120f, 70f, 32), Stain, 0f);
        Add(_background, Ellipse(new Vector2(1650f, 900f), 150f, 60f, 32), Stain, 0f);
        Add(_background, Ellipse(new Vector2(1180f, 200f), 70f, 40f, 32), Stain, 0f);
        Add(_background, Ellipse(new Vector2(700f, 930f), 90f, 35f, 32), Stain, 0f);
        // Little mushrooms on both sides of the title.
        Mushroom(_background, new Vector2(700f, 120f), 34f, CapRed, false);
        Mushroom(_background, new Vector2(1220f, 120f), 34f, new Color(0.7f, 0.45f, 0.88f), false);

        // Dotted roads Town - Forest - Pet City, outside the circles.
        var centers = MapLayout.Centers;
        for (int i = 0; i + 1 < centers.Length; i++)
        {
            var a = new Vector2(centers[i].X, centers[i].Y);
            var b = new Vector2(centers[i + 1].X, centers[i + 1].Y);
            var control = (a + b) * 0.5f + new Vector2(0f, 150f);
            for (int k = 0; k <= 60; k++)
            {
                float t = k / 60f;
                var p = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * control + t * t * b;
                if (p.DistanceTo(a) > r + 26f && p.DistanceTo(b) > r + 26f && k % 2 == 0)
                    _roadDots.Add(p);
            }
        }

        BuildTown(_places[0]);
        BuildForest(_places[1]);
        BuildPetCity(_places[2]);
    }

    private static void BuildTown(List<Shape> shapes)
    {
        float r = MapLayout.Radius;
        Add(shapes, Segment(r, 95f), new Color(0.55f, 0.82f, 0.42f), 0f);
        Cloud(shapes, new Vector2(-70f, -120f), 1f);
        Cloud(shapes, new Vector2(90f, -70f), 0.75f);
        House(shapes, new Vector2(-100f, 140f), 0.85f, CapRed);
        House(shapes, new Vector2(100f, 140f), 0.85f, new Color(0.98f, 0.6f, 0.25f));
        House(shapes, new Vector2(0f, 150f), 1.1f, new Color(0.98f, 0.55f, 0.72f));
    }

    private static void BuildForest(List<Shape> shapes)
    {
        float r = MapLayout.Radius;
        Add(shapes, Segment(r, 115f), new Color(0.42f, 0.68f, 0.32f), 0f);
        Mushroom(shapes, new Vector2(-122f, 140f), 52f, new Color(0.7f, 0.45f, 0.88f), true);
        Mushroom(shapes, new Vector2(125f, 135f), 50f, new Color(0.98f, 0.6f, 0.25f), true);
        Mushroom(shapes, new Vector2(0f, 150f), 92f, CapRed, true);
        for (int i = 0; i < 5; i++)
        {
            float x = -96f + i * 48f;
            Add(shapes, new[] { new Vector2(x - 9f, 175f), new Vector2(x - 4f, 150f), new Vector2(x, 168f),
                new Vector2(x + 5f, 148f), new Vector2(x + 10f, 175f) }, new Color(0.3f, 0.55f, 0.25f), 0f);
        }
    }

    private static void BuildPetCity(List<Shape> shapes)
    {
        float r = MapLayout.Radius;
        Add(shapes, Segment(r, 140f), new Color(0.98f, 0.76f, 0.82f), 0f);
        var colors = new[]
        {
            new Color(0.75f, 0.95f, 0.8f), new Color(0.7f, 0.85f, 0.98f), new Color(1f, 0.9f, 0.6f),
            new Color(0.98f, 0.75f, 0.8f), new Color(0.9f, 0.75f, 0.95f),
        };
        float[] xs = { -118f, -60f, 0f, 60f, 118f };
        float[] tops = { -100f, -135f, -175f, -140f, -95f };
        float[] widths = { 50f, 56f, 64f, 56f, 50f };
        int[] order = { 0, 4, 1, 3, 2 };
        foreach (int i in order)
        {
            var rect = new Rect2(xs[i] - widths[i] * 0.5f, tops[i], widths[i], 150f - tops[i]);
            Add(shapes, RoundedRect(rect, 8f), colors[i], 5f);
            for (float y = tops[i] + 18f; y < 120f; y += 30f)
            for (int col = -1; col <= 1; col += 2)
                Add(shapes, RoundedRect(new Rect2(xs[i] + col * widths[i] * 0.22f - 7f, y, 14f, 16f), 3f),
                    new Color(1f, 1f, 0.85f), 0f);
        }
        // A spotted mushroom dome on the tallest tower and a big paw print on the plaza.
        Add(shapes, Dome(new Vector2(0f, -175f), 38f, 28f), CapRed, 5f);
        Spots(shapes, new Vector2(0f, -175f), 38f, 28f, 3);
        var paw = new Color(0.86f, 0.42f, 0.58f);
        Add(shapes, Ellipse(new Vector2(0f, 180f), 20f, 14f, 20), paw, 0f);
        for (int t = 0; t < 4; t++)
            Add(shapes, Ellipse(new Vector2(-21f + t * 14f, 160f - (t == 1 || t == 2 ? 7f : 0f)), 6f, 7f, 12), paw, 0f);
    }

    private static void Cloud(List<Shape> shapes, Vector2 at, float s)
    {
        var puffs = new[] { new Vector3(-38f, 8f, 26f), new Vector3(0f, -6f, 36f), new Vector3(38f, 8f, 26f) };
        foreach (var p in puffs)
            Add(shapes, Ellipse(at + new Vector2(p.X, p.Y) * s, (p.Z + 5f) * s, (p.Z + 5f) * s, 28), Ink, 0f);
        Add(shapes, RoundedRect(new Rect2(at + new Vector2(-62f, 0f) * s, new Vector2(124f, 37f) * s), 18f * s), Ink, 0f);
        foreach (var p in puffs)
            Add(shapes, Ellipse(at + new Vector2(p.X, p.Y) * s, p.Z * s, p.Z * s, 28), White, 0f);
        Add(shapes, RoundedRect(new Rect2(at + new Vector2(-60f, 2f) * s, new Vector2(120f, 30f) * s), 15f * s), White, 0f);
    }

    /// <summary>A house standing on <paramref name="ground"/>: cream walls, a door, two eye windows, a spotted mushroom roof.</summary>
    private static void House(List<Shape> shapes, Vector2 ground, float s, Color roof)
    {
        float w = 74f * s, h = 62f * s;
        Add(shapes, RoundedRect(new Rect2(ground.X - w * 0.5f, ground.Y - h, w, h), 8f * s), Cream, 5f);
        Add(shapes, RoundedRect(new Rect2(ground.X - 11f * s, ground.Y - 30f * s, 22f * s, 30f * s), 10f * s),
            new Color(0.6f, 0.38f, 0.22f), 4f);
        Eye(shapes, ground + new Vector2(-22f * s, -40f * s), 8f * s);
        Eye(shapes, ground + new Vector2(22f * s, -40f * s), 8f * s);
        var roofBase = ground + new Vector2(0f, -h + 6f * s);
        Add(shapes, Dome(roofBase, 58f * s, 50f * s), roof, 5f);
        Spots(shapes, roofBase, 58f * s, 50f * s, 4);
    }

    /// <summary>A big mushroom standing on <paramref name="ground"/>: cream stem with eyes and a spotted cap.</summary>
    private static void Mushroom(List<Shape> shapes, Vector2 ground, float capRadius, Color cap, bool eyes)
    {
        float stemW = capRadius * 0.62f, stemH = capRadius * 1.15f;
        Add(shapes, RoundedRect(new Rect2(ground.X - stemW * 0.5f, ground.Y - stemH, stemW, stemH + 4f), stemW * 0.3f),
            Cream, 5f);
        if (eyes)
        {
            Eye(shapes, ground + new Vector2(-stemW * 0.2f, -stemH * 0.55f), capRadius * 0.11f);
            Eye(shapes, ground + new Vector2(stemW * 0.2f, -stemH * 0.55f), capRadius * 0.11f);
        }
        var capBase = ground + new Vector2(0f, -stemH + capRadius * 0.12f);
        Add(shapes, Dome(capBase, capRadius, capRadius * 0.82f), cap, 5f);
        Spots(shapes, capBase, capRadius, capRadius * 0.82f, 5);
    }

    private static void Eye(List<Shape> shapes, Vector2 at, float radius)
    {
        Add(shapes, Ellipse(at, radius, radius * 1.2f, 20), White, 3f);
        Add(shapes, Ellipse(at + new Vector2(0f, radius * 0.15f), radius * 0.68f, radius * 0.85f, 20), Ink, 0f);
        Add(shapes, Ellipse(at + new Vector2(-radius * 0.25f, -radius * 0.2f), radius * 0.28f, radius * 0.28f, 12), White, 0f);
    }

    private static void Spots(List<Shape> shapes, Vector2 capBase, float rx, float ry, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float t = (i + 0.5f) / count;
            float a = MathF.PI * (0.18f + 0.64f * t);
            float lift = i % 2 == 0 ? 0.55f : 0.78f;
            var p = capBase + new Vector2(-MathF.Cos(a) * rx * 0.72f, -MathF.Sin(a) * ry * lift);
            float s = rx * (i % 2 == 0 ? 0.13f : 0.1f);
            Add(shapes, Ellipse(p, s, s * 0.85f, 14), White, 0f);
        }
    }

    private static void Add(List<Shape> shapes, Vector2[] points, Color fill, float inkWidth) =>
        shapes.Add(new Shape { Points = points, Outline = Closed(points), Fill = fill, InkWidth = inkWidth });

    private static Vector2[] Closed(Vector2[] points)
    {
        var closed = new Vector2[points.Length + 1];
        points.CopyTo(closed, 0);
        closed[^1] = points[0];
        return closed;
    }

    private static Vector2[] Ellipse(Vector2 c, float rx, float ry, int n)
    {
        var points = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float a = MathF.Tau * i / n;
            points[i] = c + new Vector2(MathF.Cos(a) * rx, MathF.Sin(a) * ry);
        }
        return points;
    }

    /// <summary>The upper half of an ellipse standing on <paramref name="c"/> (y down), a mushroom cap or roof.</summary>
    private static Vector2[] Dome(Vector2 c, float rx, float ry)
    {
        const int n = 28;
        var points = new Vector2[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float a = MathF.PI * i / n;
            points[i] = c + new Vector2(MathF.Cos(a) * rx, -MathF.Sin(a) * ry);
        }
        return points;
    }

    /// <summary>The part of the circle of radius <paramref name="r"/> below <paramref name="y"/> (y down).</summary>
    private static Vector2[] Segment(float r, float y)
    {
        const int n = 40;
        float a0 = MathF.Asin(y / r);
        var points = new Vector2[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float a = a0 + (MathF.PI - 2f * a0) * i / n;
            points[i] = new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * r);
        }
        return points;
    }

    private static Vector2[] RoundedRect(Rect2 rect, float radius)
    {
        const int perCorner = 6;
        radius = MathF.Min(radius, MathF.Min(rect.Size.X, rect.Size.Y) * 0.5f);
        var corners = new[]
        {
            new Vector2(rect.End.X - radius, rect.Position.Y + radius),
            new Vector2(rect.End.X - radius, rect.End.Y - radius),
            new Vector2(rect.Position.X + radius, rect.End.Y - radius),
            new Vector2(rect.Position.X + radius, rect.Position.Y + radius),
        };
        var points = new Vector2[4 * (perCorner + 1)];
        for (int c = 0; c < 4; c++)
        for (int i = 0; i <= perCorner; i++)
        {
            float a = -MathF.PI * 0.5f + c * MathF.PI * 0.5f + MathF.PI * 0.5f * i / perCorner;
            points[c * (perCorner + 1) + i] = corners[c] + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius;
        }
        return points;
    }
}
