using Godot;

namespace Fungtopia.Save.View;

/// <summary>The "Saved!" bubble: pops in at the top of the screen and fades after a moment.</summary>
public partial class SaveToast : Control
{
    private readonly StyleBoxFlat _box = new() { CornerDetail = 12, AntiAliasing = true, BgColor = PauseMenu.Green, BorderColor = PauseMenu.Ink };
    private float _left;
    private string _text = "";
    private Color _color = PauseMenu.Green;

    public const float Seconds = 1.6f;

    public SaveToast()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    public void Show(string text, Color color)
    {
        _text = text;
        _color = color;
        _left = Seconds;
    }

    public override void _Process(double delta)
    {
        if (_left <= 0f)
            return;
        _left = Mathf.Max(0f, _left - (float)delta);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_left <= 0f || _text.Length == 0)
            return;
        float t = Seconds - _left;
        float pop = Mathf.Clamp(t / 0.15f, 0f, 1f);
        float alpha = Mathf.Clamp(_left / 0.4f, 0f, 1f);
        float scale = Mathf.Min(Size.X / 1920f, Size.Y / 1080f) * (0.7f + 0.3f * pop + 0.08f * Mathf.Sin(pop * Mathf.Pi));
        var font = PauseMenu.Font;
        const int fontSize = 52;
        var extent = font.GetStringSize(_text, HorizontalAlignment.Left, -1, fontSize);
        var size = new Vector2(extent.X + 120f, 110f);
        DrawSetTransform(new Vector2(Size.X * 0.5f, 62f * scale), 0f, Vector2.One * scale);
        Modulate = new Color(1f, 1f, 1f, alpha);
        _box.BgColor = _color;
        _box.SetBorderWidthAll(6);
        _box.SetCornerRadiusAll(55);
        _box.ShadowColor = new Color(PauseMenu.Ink, 0.35f);
        _box.ShadowSize = 8;
        _box.ShadowOffset = new Vector2(0, 6);
        DrawStyleBox(_box, new Rect2(-size * 0.5f, size));
        DrawCircle(new Vector2(-size.X * 0.5f + 40f, -18f), 11f, new Color(PauseMenu.Spot, 0.85f));
        DrawCircle(new Vector2(size.X * 0.5f - 44f, 20f), 8f, new Color(PauseMenu.Spot, 0.85f));
        var origin = new Vector2(-extent.X * 0.5f, font.GetAscent(fontSize) - extent.Y * 0.5f);
        DrawStringOutline(font, origin, _text, HorizontalAlignment.Left, -1, fontSize, 12, PauseMenu.Ink);
        DrawString(font, origin, _text, HorizontalAlignment.Left, -1, fontSize, PauseMenu.Spot);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
