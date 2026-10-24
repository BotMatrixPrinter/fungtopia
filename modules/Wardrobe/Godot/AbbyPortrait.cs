using Godot;

namespace Fungtopia.Wardrobe.View;

/// <summary>
/// Abby, the clothing shopkeeper, drawn small in the shop's header: big dark eyes with shines, a
/// happy smile, rosy cheeks, hair in a tall top bun and a triangle dress.
/// </summary>
public partial class AbbyPortrait : Control
{
    private static readonly Color Skin = new(0.98f, 0.76f, 0.62f);
    private static readonly Color Hair = new(0.45f, 0.24f, 0.16f);
    private static readonly Color Dress = new(0.68f, 0.5f, 0.92f);
    private static readonly Color Blush = new(1f, 0.55f, 0.6f);
    private static readonly Color Eye = new(0.07f, 0.04f, 0.06f);

    public AbbyPortrait()
    {
        CustomMinimumSize = new Vector2(120, 150);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        var ink = WardrobeStyle.Ink;
        float cx = Size.X * 0.5f;
        // The triangle dress with white spots.
        var dress = new[] { new Vector2(cx, 92), new Vector2(cx - 44, 148), new Vector2(cx + 44, 148) };
        DrawColoredPolygon(new[] { dress[0] + new Vector2(0, -4), dress[1] + new Vector2(-4, 3), dress[2] + new Vector2(4, 3) }, ink);
        DrawColoredPolygon(dress, Dress);
        foreach (var spot in new[] { new Vector2(-14, 132), new Vector2(16, 124), new Vector2(0, 112), new Vector2(26, 140) })
            DrawCircle(new Vector2(cx, 0) + spot, 4.5f, Colors.White);
        // The tall top bun.
        DrawCircle(new Vector2(cx, 18), 17, ink);
        DrawCircle(new Vector2(cx, 18), 14, Hair);
        DrawCircle(new Vector2(cx, 34), 13, ink);
        DrawCircle(new Vector2(cx, 34), 10.5f, Hair);
        // The head and hair.
        var head = new Vector2(cx, 68);
        DrawCircle(head, 37, ink);
        DrawCircle(head, 34, Skin);
        DrawArc(head + new Vector2(0, 2), 31, Mathf.Pi * 1.05f, Mathf.Pi * 1.95f, 24, Hair, 14f, true);
        // Big dark eyes with shines, rosy cheeks and a happy smile.
        for (int side = -1; side <= 1; side += 2)
        {
            var eye = head + new Vector2(side * 13, 4);
            DrawSetTransform(eye, 0, new Vector2(1f, 1.35f));
            DrawCircle(Vector2.Zero, 7.5f, Eye);
            DrawSetTransform(Vector2.Zero);
            DrawCircle(eye + new Vector2(-2.5f, -4.5f), 3f, Colors.White);
            DrawCircle(eye + new Vector2(2.5f, 3f), 1.4f, Colors.White);
            DrawCircle(head + new Vector2(side * 23, 17), 5.5f, new Color(Blush, 0.8f));
        }
        DrawArc(head + new Vector2(0, 14), 8, 0.2f, Mathf.Pi - 0.2f, 12, ink, 3f, true);
    }
}
