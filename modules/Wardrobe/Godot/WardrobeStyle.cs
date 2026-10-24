using Godot;

namespace Fungtopia.Wardrobe.View;

/// <summary>The screens' soft colors, rounded boxes and bold friendly font (like the HUD's).</summary>
public static class WardrobeStyle
{
    public static readonly Color Ink = new(0.27f, 0.17f, 0.23f);
    public static readonly Color Cream = new(1f, 0.96f, 0.89f);
    public static readonly Color Card = new(1f, 0.99f, 0.96f);
    public static readonly Color Cap = new(0.95f, 0.49f, 0.55f);
    public static readonly Color CapShade = new(0.86f, 0.38f, 0.47f);
    public static readonly Color Mint = new(0.62f, 0.88f, 0.76f);
    public static readonly Color Gold = new(1f, 0.82f, 0.33f);
    public static readonly Color Soft = new(0.55f, 0.45f, 0.5f);
    public static readonly Color Warn = new(0.88f, 0.25f, 0.3f);

    private static Font? _font;

    public static Font Font => _font ??= new SystemFont
    {
        FontNames = new[] { "Nunito", "Fredoka", "Baloo 2", "Varela Round", "Comic Neue", "Quicksand", "DejaVu Sans" },
        FontWeight = 800,
        Antialiasing = TextServer.FontAntialiasing.Gray,
    };

    public static Color ToColor(System.Numerics.Vector3 c) => new(c.X, c.Y, c.Z);

    public static StyleBoxFlat Box(Color fill, int radius, int border = 4, Color? borderColor = null, int margin = 12) => new()
    {
        BgColor = fill,
        BorderColor = borderColor ?? Ink,
        BorderWidthLeft = border, BorderWidthRight = border, BorderWidthTop = border, BorderWidthBottom = border,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        ContentMarginLeft = margin, ContentMarginRight = margin, ContentMarginTop = margin, ContentMarginBottom = margin,
        AntiAliasing = true,
        ShadowColor = new Color(Ink, 0.25f),
        ShadowSize = border > 0 ? 6 : 0,
        ShadowOffset = new Vector2(0, 5),
    };

    public static LabelSettings Text(int size, Color color, int outline = 0) => new()
    {
        Font = Font,
        FontSize = size,
        FontColor = color,
        OutlineSize = outline,
        OutlineColor = Ink,
    };

    public static Label Label(string text, int size, Color color, int outline = 0) => new()
    {
        Text = text,
        LabelSettings = Text(size, color, outline),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    /// <summary>Gives a button the screens' look: cream, a pink border when focused or hovered, mint when picked.</summary>
    public static void Style(Button button, Color fill, int radius = 22, int fontSize = 26)
    {
        button.AddThemeStyleboxOverride("normal", Box(fill, radius, 4));
        button.AddThemeStyleboxOverride("hover", Box(fill.Lightened(0.08f), radius, 5, Cap));
        button.AddThemeStyleboxOverride("pressed", Box(fill.Darkened(0.08f), radius, 5, CapShade));
        button.AddThemeStyleboxOverride("focus", Box(new Color(0, 0, 0, 0), radius, 7, Cap));
        button.AddThemeStyleboxOverride("disabled", Box(fill.Darkened(0.15f), radius, 4, Soft));
        button.AddThemeFontOverride("font", Font);
        button.AddThemeFontSizeOverride("font_size", fontSize);
        foreach (var name in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
            button.AddThemeColorOverride(name, Ink);
        button.AddThemeColorOverride("font_disabled_color", Soft);
    }

    /// <summary>"M¢ 1,250".</summary>
    public static string Coins(int coins) => "M¢ " + coins.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
}
