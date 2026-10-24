using Godot;

namespace Fungtopia.HUD.View;

/// <summary>The clock's badge: a smiling sun on a sky-blue disk by day, a sleepy crescent moon on navy by night.</summary>
public partial class SkyIcon : Control
{
    private static readonly Color Sky = new(0.62f, 0.85f, 0.97f);
    private static readonly Color Night = new(0.2f, 0.22f, 0.45f);

    private bool _night;
    private bool _drawn;

    public SkyIcon() => MouseFilter = MouseFilterEnum.Ignore;

    public void Show(bool night)
    {
        if (_drawn && night == _night)
            return;
        _night = night;
        _drawn = true;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var c = Size * 0.5f;
        float r = Mathf.Min(Size.X, Size.Y) * 0.5f - 2f;
        DrawCircle(c, r, _night ? Night : Sky);
        if (_night)
        {
            // A crescent: a cream disk with a navy one bitten out of its upper right.
            DrawCircle(c, r * 0.62f, HudStyle.Cream);
            DrawCircle(c + new Vector2(r * 0.34f, -r * 0.24f), r * 0.52f, Night);
            DrawCircle(c + new Vector2(r * 0.45f, r * 0.5f), r * 0.07f, HudStyle.Cream);
            DrawCircle(c + new Vector2(r * 0.62f, -r * 0.5f), r * 0.05f, HudStyle.Cream);
            // Sleepy closed eyes.
            for (int side = -1; side <= 1; side += 2)
            {
                var eye = c + new Vector2(-r * 0.26f + side * r * 0.13f, r * 0.12f);
                DrawArc(eye, r * 0.08f, 0f, Mathf.Pi, 8, HudStyle.Ink, 2.5f, true);
            }
        }
        else
        {
            for (int i = 0; i < 8; i++)
            {
                float a = Mathf.Tau * i / 8f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                DrawLine(c + dir * r * 0.5f, c + dir * r * 0.82f, HudStyle.GoldShade, r * 0.16f, true);
            }
            DrawCircle(c, r * 0.5f, HudStyle.Gold);
            DrawArc(c, r * 0.5f, 0f, Mathf.Tau, 28, HudStyle.GoldShade, 2f, true);
            HudStyle.DrawEyes(this, c + new Vector2(0, -r * 0.06f), r * 0.32f, r * 0.1f);
        }
        DrawArc(c, r, 0f, Mathf.Tau, 40, HudStyle.Ink, 3.5f, true);
    }
}
