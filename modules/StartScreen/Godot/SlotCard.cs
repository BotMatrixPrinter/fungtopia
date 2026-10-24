using Godot;

namespace Fungtopia.StartScreen.View;

/// <summary>
/// One save slot (or one of a slot's two saves): a rounded cream card with a mushroom-cap top,
/// a colored title band and a few lines of info (character, day, coins, area, saved time). The
/// highlighted card grows a little, gets a gold border and a bobbing arrow; a card that cannot be
/// chosen is greyed.
/// </summary>
public partial class SlotCard : Control
{
    private readonly StyleBoxFlat _box = new() { BgColor = StartStyle.Cream, BorderColor = StartStyle.Ink, CornerDetail = 12, AntiAliasing = true };
    private float _glow;
    private float _time;

    public string Title { get; set; } = "";

    /// <summary>The info lines, top to bottom; the first is drawn bigger.</summary>
    public string[] Lines { get; set; } = System.Array.Empty<string>();

    public Color CapColor { get; set; } = StartStyle.Blue;

    public bool Highlighted { get; set; }

    public bool Enabled { get; set; } = true;

    public SlotCard() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        _glow = Mathf.MoveToward(_glow, Highlighted ? 1f : 0f, dt * 6f);
        PivotOffset = Size * 0.5f;
        float s = 1f + _glow * (0.05f + Mathf.Sin(_time * 4f) * 0.01f);
        Scale = new Vector2(s, s);
        Modulate = Enabled ? Colors.White : new Color(0.8f, 0.78f, 0.8f, 0.75f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var size = Size;
        if (size.X < 8f || size.Y < 8f)
            return;
        float capHeight = size.Y * 0.14f;
        var body = new Rect2(0f, capHeight * 0.6f, size.X, size.Y - capHeight * 0.6f);
        _box.SetCornerRadiusAll(32);
        _box.SetBorderWidthAll(Mathf.RoundToInt(5f + _glow * 4f));
        _box.BorderColor = StartStyle.Ink.Lerp(StartStyle.Gold, _glow);
        _box.BgColor = StartStyle.Cream.Lerp(new Color(1f, 0.98f, 0.9f), _glow);
        _box.ShadowColor = new Color(StartStyle.Ink, 0.3f);
        _box.ShadowSize = 10;
        _box.ShadowOffset = new Vector2(0, 8);
        DrawStyleBox(_box, body);
        var cap = Enabled ? CapColor : CapColor.Lerp(new Color(0.7f, 0.68f, 0.7f), 0.7f);
        StartStyle.DrawCap(this, new Vector2(size.X * 0.5f, capHeight * 1.05f), size.X * 0.8f, capHeight, cap, Title.Length);

        var band = new Rect2(24f, capHeight + 18f, size.X - 48f, size.Y * 0.13f);
        _box.SetCornerRadiusAll(Mathf.RoundToInt(band.Size.Y * 0.5f));
        _box.SetBorderWidthAll(4);
        _box.BorderColor = StartStyle.Ink;
        _box.BgColor = cap;
        _box.ShadowSize = 0;
        DrawStyleBox(_box, band);
        StartStyle.DrawCentered(this, Title, band.GetCenter(), Mathf.RoundToInt(band.Size.Y * 0.55f), StartStyle.Cream, 8);

        float top = band.End.Y + size.Y * 0.06f;
        float step = (size.Y - 30f - top) / Mathf.Max(Lines.Length, 1);
        for (int i = 0; i < Lines.Length; i++)
        {
            int font = Mathf.RoundToInt(size.X * (i == 0 ? 0.085f : 0.065f));
            StartStyle.DrawCentered(this, Lines[i], new Vector2(size.X * 0.5f, top + step * (i + 0.5f)), font, StartStyle.Ink, 0);
        }

        if (_glow > 0.01f)
        {
            // A bobbing gold arrow above the highlighted card.
            float y = -38f + Mathf.Sin(_time * 5f) * 8f;
            var tip = new Vector2(size.X * 0.5f, y + 26f);
            var arrow = new[] { tip, tip + new Vector2(-24f, -30f), tip + new Vector2(24f, -30f) };
            var color = new Color(StartStyle.Gold, _glow);
            DrawColoredPolygon(arrow, color);
            DrawPolyline(new[] { arrow[0], arrow[1], arrow[2], arrow[0] }, new Color(StartStyle.Ink, _glow), 4f, true);
        }
    }
}
