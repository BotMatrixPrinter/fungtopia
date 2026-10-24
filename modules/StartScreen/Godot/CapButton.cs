using Godot;

namespace Fungtopia.StartScreen.View;

/// <summary>
/// A big round button shaped like a mushroom: a spotted cap with the text on a cream stem band
/// below it. When highlighted it pulses and its cap turns brighter.
/// </summary>
public partial class CapButton : Control
{
    private readonly StyleBoxFlat _box = new() { BgColor = StartStyle.Cream, BorderColor = StartStyle.Ink, CornerDetail = 12, AntiAliasing = true };
    private float _time;

    public string Text { get; set; } = "";

    public int FontSize { get; set; } = 64;

    public Color CapColor { get; set; } = StartStyle.Red;

    public bool Highlighted { get; set; }

    public CapButton() => MouseFilter = MouseFilterEnum.Ignore;

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
        _box.SetCornerRadiusAll((int)(band.Size.Y * 0.5f));
        _box.SetBorderWidthAll(5);
        _box.BorderColor = Highlighted ? StartStyle.Gold.Darkened(0.2f) : StartStyle.Ink;
        _box.ShadowColor = new Color(StartStyle.Ink, 0.3f);
        _box.ShadowSize = 8;
        _box.ShadowOffset = new Vector2(0, 6);
        DrawStyleBox(_box, band);
        var cap = Highlighted ? CapColor.Lightened(0.12f) : CapColor;
        StartStyle.DrawCap(this, new Vector2(size.X * 0.5f, capHeight + 4f), size.X, capHeight, cap, 3);
        StartStyle.DrawCentered(this, Text, band.GetCenter() + new Vector2(0, 2f), FontSize, StartStyle.Ink, 0);
    }
}
