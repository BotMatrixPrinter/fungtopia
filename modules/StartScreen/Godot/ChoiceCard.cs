using Godot;

namespace Fungtopia.StartScreen.View;

/// <summary>
/// One choice: a rounded cream card with a pink mushroom-cap top, a turntable preview and a
/// name. The highlighted card grows a little, gets a gold border and a bobbing arrow above it.
/// </summary>
public partial class ChoiceCard : Control
{
    private readonly StyleBoxFlat _box = new() { BgColor = StartStyle.Cream, BorderColor = StartStyle.Ink, CornerDetail = 12, AntiAliasing = true };
    private float _glow;
    private float _time;

    public Texture2D? Preview { get; set; }

    public string Title { get; set; } = "";

    public Color CapColor { get; set; } = StartStyle.Pink;

    public bool Highlighted { get; set; }

    public ChoiceCard() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        _glow = Mathf.MoveToward(_glow, Highlighted ? 1f : 0f, dt * 6f);
        PivotOffset = Size * 0.5f;
        float s = 1f + _glow * (0.06f + Mathf.Sin(_time * 4f) * 0.012f);
        Scale = new Vector2(s, s);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var size = Size;
        if (size.X < 8f || size.Y < 8f)
            return;
        float capHeight = size.Y * 0.16f;
        var body = new Rect2(0f, capHeight * 0.6f, size.X, size.Y - capHeight * 0.6f);
        _box.SetCornerRadiusAll(36);
        _box.SetBorderWidthAll(Mathf.RoundToInt(5f + _glow * 4f));
        _box.BorderColor = StartStyle.Ink.Lerp(StartStyle.Gold, _glow);
        _box.BgColor = StartStyle.Cream.Lerp(new Color(1f, 0.98f, 0.9f), _glow);
        _box.ShadowColor = new Color(StartStyle.Ink, 0.3f);
        _box.ShadowSize = 10;
        _box.ShadowOffset = new Vector2(0, 8);
        DrawStyleBox(_box, body);
        StartStyle.DrawCap(this, new Vector2(size.X * 0.5f, capHeight * 1.05f), size.X * 0.82f, capHeight, CapColor, Title.Length);

        if (Preview != null)
        {
            float side = Mathf.Min(size.X - 30f, size.Y - capHeight - 90f);
            DrawTextureRect(Preview, new Rect2((size.X - side) * 0.5f, capHeight + 8f, side, side), false);
        }
        var band = new Rect2(24f, size.Y - 86f, size.X - 48f, 64f);
        _box.SetCornerRadiusAll(32);
        _box.SetBorderWidthAll(4);
        _box.BorderColor = StartStyle.Ink;
        _box.BgColor = CapColor;
        _box.ShadowSize = 0;
        DrawStyleBox(_box, band);
        StartStyle.DrawCentered(this, Title, band.GetCenter(), Mathf.RoundToInt(size.X * 0.068f), StartStyle.Cream, 10);

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
