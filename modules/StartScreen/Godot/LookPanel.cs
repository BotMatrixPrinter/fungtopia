using Fungtopia.Wardrobe;
using Godot;

namespace Fungtopia.StartScreen.View;

/// <summary>
/// The "Make your look" panel: a rounded cream card with five rows (skin tone, hair style, hair
/// color, eye color, outfit). Colors are round swatches, styles and outfits are little pills.
/// The highlighted row glows gold, the chosen value of each row gets a gold ring, and the one
/// under the mouse grows a little. Outfits that do not fit the character are left out.
/// </summary>
public partial class LookPanel : Control
{
    private const float Width = 760f;
    private const float Top = 30f;
    private const float RowHeight = 116f;
    private const float Inset = 36f;

    private static readonly string[] RowNames = { "Skin tone", "Hair style", "Hair color", "Eye color", "Outfit" };

    private readonly StyleBoxFlat _box = new() { CornerDetail = 12, AntiAliasing = true };
    private float _time;

    public StartScreenModule? Screen { get; set; }

    /// <summary>The value under the mouse (row, value), or row -1.</summary>
    public (int Row, int Value) Hovered { get; set; } = (-1, -1);

    public LookPanel() => MouseFilter = MouseFilterEnum.Ignore;

    /// <summary>The panel's height for its width, so the view can place it.</summary>
    public static float HeightFor(float width) => width / Width * (Top * 2f + RowHeight * StartScreenModule.LookRows);

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    /// <summary>The (row, value) at a point in global coordinates, or (-1, -1).</summary>
    public (int Row, int Value) At(Vector2 global)
    {
        if (!Visible || Screen == null)
            return (-1, -1);
        var local = (global - GlobalPosition) / Scale;
        for (int row = 0; row < StartScreenModule.LookRows; row++)
        {
            int count = StartScreenModule.LookCount((LookRow)row);
            for (int value = 0; value < count; value++)
                if (Shown(row, value) && ItemRect(row, value).Grow(4f * Unit).HasPoint(local))
                    return (row, value);
        }
        return (-1, -1);
    }

    private float Unit => Size.X / Width;

    private bool Shown(int row, int value) => row != (int)LookRow.Outfit || Screen!.OutfitFits(value);

    /// <summary>Where value <paramref name="value"/> of row <paramref name="row"/> is drawn (local coordinates).</summary>
    private Rect2 ItemRect(int row, int value)
    {
        float u = Unit;
        float y = (Top + row * RowHeight + 46f) * u;
        float left = Inset * 1.6f * u, width = Size.X - left * 2f;
        var kind = (LookRow)row;
        if (kind is LookRow.HairStyle or LookRow.Outfit)
        {
            // Pills, the shown ones side by side.
            int count = StartScreenModule.LookCount(kind), shown = 0, slot = 0;
            for (int i = 0; i < count; i++)
                if (Shown(row, i))
                {
                    if (i < value)
                        slot++;
                    shown++;
                }
            float gap = 10f * u, w = (width - gap * (shown - 1)) / shown;
            return new Rect2(left + slot * (w + gap), y, w, 56f * u);
        }
        int n = StartScreenModule.LookCount(kind);
        float side = 56f * u, step = Mathf.Min(side + 22f * u, (width - side) / Mathf.Max(1, n - 1));
        return new Rect2(left + value * step, y, side, side);
    }

    public override void _Draw()
    {
        if (Screen == null || Size.X < 8f)
            return;
        float u = Unit;
        _box.BgColor = new Color(StartStyle.Cream, 0.96f);
        _box.BorderColor = StartStyle.Ink;
        _box.SetBorderWidthAll(Mathf.RoundToInt(5f * u));
        _box.SetCornerRadiusAll(Mathf.RoundToInt(40f * u));
        _box.ShadowColor = new Color(StartStyle.Ink, 0.3f);
        _box.ShadowSize = 10;
        _box.ShadowOffset = new Vector2(0, 8);
        DrawStyleBox(_box, new Rect2(Vector2.Zero, Size));

        for (int row = 0; row < StartScreenModule.LookRows; row++)
        {
            var kind = (LookRow)row;
            bool current = Screen.Row == kind;
            var band = new Rect2(14f * u, (Top + row * RowHeight - 14f) * u, Size.X - 28f * u, (RowHeight - 4f) * u);
            if (current)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 4f);
                _box.BgColor = new Color(StartStyle.Gold, 0.22f + 0.08f * pulse);
                _box.BorderColor = StartStyle.Gold.Darkened(0.15f);
                _box.SetBorderWidthAll(Mathf.RoundToInt(4f * u));
                _box.SetCornerRadiusAll(Mathf.RoundToInt(28f * u));
                _box.ShadowSize = 0;
                DrawStyleBox(_box, band);
            }
            int labelSize = Mathf.RoundToInt(30f * u);
            float labelWidth = StartStyle.Font.GetStringSize(RowNames[row], HorizontalAlignment.Left, -1, labelSize).X;
            StartStyle.DrawCentered(this, RowNames[row], new Vector2(Inset * 1.6f * u + labelWidth * 0.5f, (Top + row * RowHeight + 18f) * u),
                labelSize, current ? StartStyle.Ink : StartStyle.Ink.Lightened(0.25f), 0);
            if (current)
            {
                // Little arrows at the ends of the highlighted row: left / right change it.
                float cy = (Top + row * RowHeight + 74f) * u;
                DrawArrow(new Vector2(30f * u, cy), -1f, u);
                DrawArrow(new Vector2(Size.X - 30f * u, cy), 1f, u);
            }

            int count = StartScreenModule.LookCount(kind);
            int chosen = Screen.LookValue(kind);
            for (int value = 0; value < count; value++)
            {
                if (!Shown(row, value))
                    continue;
                var rect = ItemRect(row, value);
                bool hovered = Hovered == (row, value);
                if (hovered)
                    rect = rect.Grow(4f * u);
                DrawItem(kind, value, rect, value == chosen, u);
            }
        }
    }

    private void DrawItem(LookRow kind, int value, Rect2 rect, bool chosen, float u)
    {
        var ring = chosen ? StartStyle.Gold.Darkened(0.1f) : StartStyle.Ink;
        float ringWidth = (chosen ? 7f : 4f) * u;
        if (kind is LookRow.HairStyle or LookRow.Outfit)
        {
            _box.BgColor = chosen ? (kind == LookRow.Outfit ? StartStyle.Pink : StartStyle.Lilac) : StartStyle.Spot;
            _box.BorderColor = ring;
            _box.SetBorderWidthAll(Mathf.RoundToInt(ringWidth));
            _box.SetCornerRadiusAll(Mathf.RoundToInt(rect.Size.Y * 0.5f));
            _box.ShadowSize = 0;
            DrawStyleBox(_box, rect);
            string name = kind == LookRow.HairStyle ? Looks.HairStyles[value].Name : Catalog.StarterOutfits[value].Name;
            // The biggest size up to 26 that fits the pill.
            int size = Mathf.RoundToInt(26f * u);
            float room = rect.Size.X - 22f * u;
            float width = StartStyle.Font.GetStringSize(name, HorizontalAlignment.Left, -1, size).X;
            if (width > room)
                size = Mathf.Max(8, Mathf.FloorToInt(size * room / width));
            StartStyle.DrawCentered(this, name, rect.GetCenter(), size, chosen ? StartStyle.Cream : StartStyle.Ink,
                chosen ? Mathf.RoundToInt(6f * u) : 0);
            return;
        }
        var rgb = kind switch
        {
            LookRow.Skin => Looks.SkinTones[value],
            LookRow.HairColor => Looks.HairColors[value],
            _ => Looks.EyeColors[value],
        };
        var center = rect.GetCenter();
        float r = rect.Size.X * 0.5f;
        DrawCircle(center, r, new Color(rgb.X, rgb.Y, rgb.Z));
        if (kind == LookRow.EyeColor)
        {
            // A little round eye: a white sparkle on the iris.
            DrawCircle(center + new Vector2(-r * 0.3f, -r * 0.3f), r * 0.24f, new Color(1f, 1f, 1f, 0.95f));
        }
        DrawArc(center, r, 0f, Mathf.Tau, 40, ring, ringWidth, true);
        if (chosen)
            DrawArc(center, r + 7f * u, 0f, Mathf.Tau, 40, new Color(StartStyle.Gold, 0.6f), 3f * u, true);
    }

    private void DrawArrow(Vector2 tip, float direction, float u)
    {
        float bob = Mathf.Sin(_time * 5f) * 3f * u;
        tip += new Vector2(direction * bob, 0f);
        var points = new[] { tip, tip + new Vector2(-direction * 18f * u, -16f * u), tip + new Vector2(-direction * 18f * u, 16f * u) };
        DrawColoredPolygon(points, StartStyle.Gold);
        DrawPolyline(new[] { points[0], points[1], points[2], points[0] }, StartStyle.Ink, 3f * u, true);
    }
}
