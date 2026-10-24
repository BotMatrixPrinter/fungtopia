using Godot;

namespace Fungtopia.StartScreen.View;

/// <summary>The start screen's soft colors and its bold friendly font (the HUD's look).</summary>
public static class StartStyle
{
    public static readonly Color Ink = new(0.27f, 0.17f, 0.23f);
    public static readonly Color Cream = new(1f, 0.95f, 0.86f);
    public static readonly Color Spot = new(1f, 0.97f, 0.93f);
    public static readonly Color Pink = new(0.95f, 0.49f, 0.55f);
    public static readonly Color Red = new(0.9f, 0.25f, 0.27f);
    public static readonly Color Gold = new(1f, 0.78f, 0.3f);
    public static readonly Color Blue = new(0.45f, 0.62f, 0.95f);
    public static readonly Color Green = new(0.5f, 0.78f, 0.42f);
    public static readonly Color Lilac = new(0.72f, 0.56f, 0.92f);

    /// <summary>The caps of the logo's letters, in turn.</summary>
    public static readonly Color[] CapColors = { Red, Pink, Gold, Blue, Green, Lilac };

    private static Font? _font;

    /// <summary>A rounded, heavy font when the system has one, else the boldest fallback.</summary>
    public static Font Font => _font ??= new SystemFont
    {
        FontNames = new[] { "Nunito", "Fredoka", "Baloo 2", "Varela Round", "Comic Neue", "Quicksand", "DejaVu Sans" },
        FontWeight = 800,
        Antialiasing = TextServer.FontAntialiasing.Gray,
    };

    public static LabelSettings Text(int size, Color color, int outline) => new()
    {
        Font = Font,
        FontSize = size,
        FontColor = color,
        OutlineSize = outline,
        OutlineColor = Ink,
        ShadowSize = outline > 0 ? 1 : 0,
        ShadowColor = new Color(Ink, 0.35f),
        ShadowOffset = new Vector2(0, 5),
    };

    /// <summary>Text centered on <paramref name="center"/>, with an ink outline.</summary>
    public static void DrawCentered(CanvasItem canvas, string text, Vector2 center, int size, Color color, int outline)
    {
        if (size <= 0 || text.Length == 0)
            return;
        var font = Font;
        var extent = font.GetStringSize(text, HorizontalAlignment.Left, -1, size);
        var origin = center + new Vector2(-extent.X * 0.5f, font.GetAscent(size) - extent.Y * 0.5f);
        if (outline > 0)
            canvas.DrawStringOutline(font, origin, text, HorizontalAlignment.Left, -1, size, outline, Ink);
        canvas.DrawString(font, origin, text, HorizontalAlignment.Left, -1, size, color);
    }

    /// <summary>
    /// A mushroom cap: a dome <paramref name="width"/> wide and <paramref name="height"/> tall
    /// standing on <paramref name="baseCenter"/>, with cream spots and an ink outline.
    /// </summary>
    public static void DrawCap(CanvasItem canvas, Vector2 baseCenter, float width, float height, Color color, int seed)
    {
        const int n = 24;
        var points = new Vector2[n + 2];
        for (int i = 0; i <= n; i++)
        {
            float a = Mathf.Pi + Mathf.Pi * i / n;
            // A slightly flared rim: the dome bulges past its base at the bottom.
            float flare = 1f + 0.08f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a)), 6f);
            points[i] = baseCenter + new Vector2(Mathf.Cos(a) * width * 0.5f * flare, Mathf.Sin(a) * height);
        }
        points[n + 1] = points[0];
        canvas.DrawColoredPolygon(points[..(n + 1)], color);
        for (int i = 0; i < 3; i++)
        {
            float u = ((seed * 7 + i * 5) % 9) / 9f;
            float x = (i - 1) * width * 0.3f + (u - 0.5f) * width * 0.08f;
            float y = -height * (i == 1 ? 0.62f : 0.38f);
            canvas.DrawCircle(baseCenter + new Vector2(x, y), height * (i == 1 ? 0.17f : 0.13f), Spot);
        }
        canvas.DrawPolyline(points, Ink, Mathf.Max(3f, height * 0.08f), true);
    }
}
