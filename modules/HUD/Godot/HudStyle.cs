using Godot;

namespace Fungtopia.HUD.View;

/// <summary>The HUD's soft colors and its bold friendly font.</summary>
public static class HudStyle
{
    public static readonly Color Ink = new(0.27f, 0.17f, 0.23f);
    public static readonly Color Cap = new(0.95f, 0.49f, 0.55f);
    public static readonly Color CapShade = new(0.86f, 0.38f, 0.47f);
    public static readonly Color Spot = new(1f, 0.97f, 0.93f);
    public static readonly Color Cream = new(1f, 0.95f, 0.86f);
    public static readonly Color Heart = new(1f, 0.42f, 0.55f);
    public static readonly Color HeartEmpty = new(0.85f, 0.78f, 0.82f);
    public static readonly Color Gold = new(1f, 0.82f, 0.33f);
    public static readonly Color GoldShade = new(0.93f, 0.64f, 0.2f);
    public static readonly Color Dusk = new(0.23f, 0.16f, 0.3f);
    // The spookier Deep Forest banner: a dusky violet cap, faint greenish spots, misty lilac band and text.
    public static readonly Color SpookyCap = new(0.36f, 0.26f, 0.45f);
    public static readonly Color SpookySpot = new(0.72f, 0.85f, 0.7f, 0.55f);
    public static readonly Color SpookyBand = new(0.3f, 0.25f, 0.38f);
    public static readonly Color Mist = new(0.88f, 0.86f, 0.97f);

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
        ShadowSize = 0,
        ShadowColor = new Color(Ink, 0.35f),
        ShadowOffset = new Vector2(0, 4),
    };

    /// <summary>Two big round eyes with white shines, centered at <paramref name="center"/>.</summary>
    public static void DrawEyes(CanvasItem canvas, Vector2 center, float spacing, float radius)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            var eye = center + new Vector2(side * spacing * 0.5f, 0);
            canvas.DrawCircle(eye, radius, Ink);
            canvas.DrawCircle(eye + new Vector2(-radius * 0.3f, -radius * 0.35f), radius * 0.38f, Colors.White);
        }
    }
}
