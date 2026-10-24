using Godot;

namespace Fungtopia.Dialogue.View;

/// <summary>The speech bubble's soft colors and its bold friendly font.</summary>
public static class DialogueStyle
{
    public static readonly Color Ink = new(0.27f, 0.17f, 0.23f);
    public static readonly Color Paper = new(1f, 0.97f, 0.91f);
    public static readonly Color Cap = new(0.95f, 0.49f, 0.55f);
    public static readonly Color Spot = new(1f, 0.97f, 0.93f);
    public static readonly Color Text = new(0.33f, 0.22f, 0.29f);

    private static Font? _font;

    /// <summary>A rounded, heavy font when the system has one, else the boldest fallback.</summary>
    public static Font Font => _font ??= new SystemFont
    {
        FontNames = new[] { "Nunito", "Fredoka", "Baloo 2", "Varela Round", "Comic Neue", "Quicksand", "DejaVu Sans" },
        FontWeight = 800,
        Antialiasing = TextServer.FontAntialiasing.Gray,
    };

    public static LabelSettings Label(int size, Color color, int outline) => new()
    {
        Font = Font,
        FontSize = size,
        FontColor = color,
        OutlineSize = outline,
        OutlineColor = Ink,
        LineSpacing = 6,
    };
}
