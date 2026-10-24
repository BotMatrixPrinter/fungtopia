using Godot;

namespace Fungtopia.HUD.View;

/// <summary>A gold Mushroom Coin with a little spotted mushroom and big round eyes.</summary>
public partial class CoinIcon : Control
{
    public CoinIcon() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw()
    {
        var c = Size * 0.5f;
        float r = Mathf.Min(Size.X, Size.Y) * 0.5f - 2f;
        DrawCircle(c, r, HudStyle.GoldShade);
        DrawCircle(c + new Vector2(-1.5f, -2f), r * 0.86f, HudStyle.Gold);
        DrawArc(c, r, 0f, Mathf.Tau, 40, HudStyle.Ink, 3.5f, true);
        // The mushroom: a little cap over a stem with the eyes.
        var capCenter = c + new Vector2(0, -r * 0.08f);
        DrawCircle(capCenter, r * 0.5f, HudStyle.Cap);
        DrawRect(new Rect2(capCenter.X - r * 0.55f, capCenter.Y, r * 1.1f, r * 0.5f), HudStyle.Gold);
        DrawCircle(capCenter + new Vector2(-r * 0.2f, -r * 0.24f), r * 0.1f, HudStyle.Spot);
        DrawCircle(capCenter + new Vector2(r * 0.18f, -r * 0.3f), r * 0.07f, HudStyle.Spot);
        DrawRect(new Rect2(c.X - r * 0.26f, capCenter.Y, r * 0.52f, r * 0.5f), HudStyle.Cream);
        HudStyle.DrawEyes(this, new Vector2(c.X, capCenter.Y + r * 0.22f), r * 0.24f, r * 0.08f);
    }
}
