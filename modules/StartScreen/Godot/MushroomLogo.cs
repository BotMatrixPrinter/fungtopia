using Godot;

namespace Fungtopia.StartScreen.View;

/// <summary>
/// The "Fungtopia" logo: big cream letters with ink outlines, each wearing a spotted mushroom
/// cap of its own color, bobbing gently one after another. Centered on its position; scale it
/// with <see cref="Control.Scale"/>.
/// </summary>
public partial class MushroomLogo : Control
{
    private const string Word = "Fungtopia";
    private const int FontSize = 168;
    private const float Spacing = 14f;

    private readonly float[] _widths = new float[Word.Length];
    private float _total;
    private float _time;

    public MushroomLogo() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Ready()
    {
        for (int i = 0; i < Word.Length; i++)
        {
            _widths[i] = StartStyle.Font.GetStringSize(Word[i].ToString(), HorizontalAlignment.Left, -1, FontSize).X;
            _total += _widths[i] + (i > 0 ? Spacing : 0f);
        }
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var font = StartStyle.Font;
        float ascent = font.GetAscent(FontSize);
        float x = -_total * 0.5f;
        for (int i = 0; i < Word.Length; i++)
        {
            string letter = Word[i].ToString();
            float bob = Mathf.Sin(_time * 2.4f - i * 0.55f) * 7f;
            float w = _widths[i];
            // Baseline at y = 60: the letters sit a little below the logo's center, caps above.
            var baseline = new Vector2(x, 60f + bob);
            // The cap sits on the letter's top (lowercase letters get a lower cap).
            bool tall = char.IsUpper(Word[i]) || Word[i] is 't' or 'f' or 'l';
            float top = baseline.Y - (tall ? ascent * 0.74f : ascent * 0.54f);
            DrawStringOutline(font, baseline + new Vector2(0, 8f), letter, HorizontalAlignment.Left, -1, FontSize, 22, new Color(StartStyle.Ink, 0.35f));
            DrawStringOutline(font, baseline, letter, HorizontalAlignment.Left, -1, FontSize, 22, StartStyle.Ink);
            DrawString(font, baseline, letter, HorizontalAlignment.Left, -1, FontSize, StartStyle.Cream);
            var color = StartStyle.CapColors[i % StartStyle.CapColors.Length];
            float wobble = Mathf.Sin(_time * 2.4f - i * 0.55f + 0.8f) * 0.05f;
            DrawSetTransform(new Vector2(x + w * 0.5f, top + 6f), wobble, Vector2.One);
            StartStyle.DrawCap(this, Vector2.Zero, w * 0.98f + 10f, w * 0.34f + 12f, color, i);
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
            x += w + Spacing;
        }
    }
}
