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

    // The start screen's "Make your look" colors.
    public static readonly Color Paper = new(1f, 0.95f, 0.86f);
    public static readonly Color Spot = new(1f, 0.97f, 0.93f);
    public static readonly Color Pink = new(0.95f, 0.49f, 0.55f);
    public static readonly Color Lilac = new(0.72f, 0.56f, 0.92f);
    public static readonly Color Green = new(0.5f, 0.78f, 0.42f);
    public static readonly Color Ring = new(1f, 0.78f, 0.3f);

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

    /// <summary>The look panel's box: cream, a thick ink border, big round corners and a soft shadow.</summary>
    public static StyleBoxFlat PanelBox()
    {
        var box = Box(new Color(Paper, 0.96f), 40, 5, null, 30);
        box.ShadowColor = new Color(Ink, 0.3f);
        box.ShadowSize = 10;
        box.ShadowOffset = new Vector2(0, 8);
        return box;
    }

    /// <summary>A big heading in cream with an ink outline and a soft drop shadow, like "Make your look".</summary>
    public static Label Heading(string text, int size)
    {
        var label = Label(text, size, Paper, Mathf.Max(6, size / 5));
        label.LabelSettings.ShadowSize = 1;
        label.LabelSettings.ShadowColor = new Color(Ink, 0.35f);
        label.LabelSettings.ShadowOffset = new Vector2(0, 5);
        return label;
    }

    /// <summary>A pill button (a tab): round ends, lilac with cream outlined text when chosen.</summary>
    public static void Pill(Button button, bool chosen, int fontSize = 24)
    {
        var fill = chosen ? Lilac : Spot;
        int radius = (int)(button.CustomMinimumSize.Y * 0.5f);
        button.AddThemeStyleboxOverride("normal", Box(fill, radius, chosen ? 6 : 4, chosen ? Ring.Darkened(0.1f) : Ink, 6));
        button.AddThemeStyleboxOverride("hover", Box(fill.Lightened(0.1f), radius, 6, Ring.Darkened(0.1f), 6));
        button.AddThemeStyleboxOverride("pressed", Box(fill.Darkened(0.08f), radius, 6, Ring.Darkened(0.2f), 6));
        button.AddThemeStyleboxOverride("focus", Box(new Color(0, 0, 0, 0), radius, 7, Pink, 6));
        button.AddThemeFontOverride("font", Font);
        button.AddThemeFontSizeOverride("font_size", fontSize);
        var text = chosen ? Paper : Ink;
        foreach (var name in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
            button.AddThemeColorOverride(name, text);
        button.AddThemeColorOverride("font_outline_color", Ink);
        button.AddThemeConstantOverride("outline_size", chosen ? 6 : 0);
    }

    /// <summary>Text centered on <paramref name="center"/>, with an ink outline when <paramref name="outline"/> &gt; 0.</summary>
    public static void DrawCentered(CanvasItem canvas, string text, Vector2 center, int size, Color color, int outline = 0)
    {
        if (size <= 0 || text.Length == 0)
            return;
        var extent = Font.GetStringSize(text, HorizontalAlignment.Left, -1, size);
        var origin = center + new Vector2(-extent.X * 0.5f, Font.GetAscent(size) - extent.Y * 0.5f);
        if (outline > 0)
            canvas.DrawStringOutline(Font, origin, text, HorizontalAlignment.Left, -1, size, outline, Ink);
        canvas.DrawString(Font, origin, text, HorizontalAlignment.Left, -1, size, color);
    }

    /// <summary>A spotted mushroom cap <paramref name="width"/> wide and <paramref name="height"/> tall standing on <paramref name="baseCenter"/>.</summary>
    public static void DrawCap(CanvasItem canvas, Vector2 baseCenter, float width, float height, Color color)
    {
        const int n = 24;
        var points = new Vector2[n + 2];
        for (int i = 0; i <= n; i++)
        {
            float a = Mathf.Pi + Mathf.Pi * i / n;
            float flare = 1f + 0.08f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a)), 6f);
            points[i] = baseCenter + new Vector2(Mathf.Cos(a) * width * 0.5f * flare, Mathf.Sin(a) * height);
        }
        points[n + 1] = points[0];
        canvas.DrawColoredPolygon(points[..(n + 1)], color);
        for (int i = 0; i < 3; i++)
        {
            float x = (i - 1) * width * 0.3f;
            float y = -height * (i == 1 ? 0.62f : 0.38f);
            canvas.DrawCircle(baseCenter + new Vector2(x, y), height * (i == 1 ? 0.17f : 0.13f), Spot);
        }
        canvas.DrawPolyline(points, Ink, Mathf.Max(3f, height * 0.08f), true);
    }

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
