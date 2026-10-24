using Godot;

namespace Fungtopia.Wardrobe.View;

/// <summary>
/// A big mushroom button like the start screen's "Next": a spotted cap over a cream band with the
/// text. Focused or hovered it pulses with a gold rim; disabled it is greyed. A real
/// <see cref="Button"/>, so focus moves and the view's hit-testing work on it.
/// </summary>
public partial class CapButton : Button
{
    private readonly StyleBoxFlat _band = new() { BgColor = WardrobeStyle.Paper, CornerDetail = 12, AntiAliasing = true };
    private float _time;

    public CapButton(string text, Color cap, Vector2 size, int fontSize)
    {
        CapText = text;
        CapColor = cap;
        FontSize = fontSize;
        CustomMinimumSize = size;
        FocusMode = FocusModeEnum.All;
        var none = new StyleBoxEmpty();
        foreach (var name in new[] { "normal", "hover", "pressed", "focus", "disabled", "hover_pressed" })
            AddThemeStyleboxOverride(name, none);
    }

    public string CapText { get; set; }

    public Color CapColor { get; set; }

    public int FontSize { get; set; }

    private bool Highlighted => !Disabled && (HasFocus() || IsHovered());

    public override void _Process(double delta)
    {
        _time += (float)delta;
        PivotOffset = Size * 0.5f;
        float s = Highlighted ? 1.04f + Mathf.Sin(_time * 5f) * 0.035f : 1f;
        Scale = new Vector2(s, s);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var size = Size;
        if (size.X < 8f || size.Y < 8f)
            return;
        float capHeight = size.Y * 0.42f;
        var band = new Rect2(size.X * 0.08f, capHeight - 6f, size.X * 0.84f, size.Y - capHeight);
        _band.SetCornerRadiusAll((int)(band.Size.Y * 0.5f));
        _band.SetBorderWidthAll(5);
        _band.BgColor = Disabled ? WardrobeStyle.Paper.Darkened(0.12f) : WardrobeStyle.Paper;
        _band.BorderColor = Highlighted ? WardrobeStyle.Ring.Darkened(0.2f) : WardrobeStyle.Ink;
        _band.ShadowColor = new Color(WardrobeStyle.Ink, 0.3f);
        _band.ShadowSize = 8;
        _band.ShadowOffset = new Vector2(0, 6);
        DrawStyleBox(_band, band);
        var cap = Disabled ? CapColor.Lerp(new Color(0.75f, 0.72f, 0.72f), 0.7f) : Highlighted ? CapColor.Lightened(0.12f) : CapColor;
        WardrobeStyle.DrawCap(this, new Vector2(size.X * 0.5f, capHeight + 4f), size.X, capHeight, cap);
        WardrobeStyle.DrawCentered(this, CapText, band.GetCenter() + new Vector2(0, 2f), FontSize,
            Disabled ? WardrobeStyle.Soft : WardrobeStyle.Ink);
    }
}
