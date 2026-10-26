using System;
using Godot;

namespace Fungtopia.HUD.View;

/// <summary>
/// The minimap at the top right under the Mushroom Coins panel: a cream rounded square with an ink
/// outline and a little pink spotted "N" cap on top, the current zone's <see cref="MinimapPicture"/>
/// inside it (north up, its longer side <see cref="MapSize"/> pixels) and the player's arrow on top.
/// The picture's texture, buildings and markers are drawn once per zone (<see cref="MinimapLayer"/>);
/// each frame only moves the layer (on the Road, where it scrolls under a fixed window) and the arrow.
/// </summary>
public partial class MinimapView : Control
{
    /// <summary>The map's longer side, in pixels.</summary>
    public const float MapSize = 204f;

    /// <summary>The cream rim around the map, in pixels (so the whole minimap is about 220 across).</summary>
    public const float Rim = 8f;

    private readonly StyleBoxFlat _frame = new()
    {
        BgColor = HudStyle.Cream,
        BorderColor = HudStyle.Ink,
        CornerDetail = 12,
        AntiAliasing = true,
        ShadowColor = new Color(HudStyle.Ink, 0.25f),
        ShadowSize = 6,
        ShadowOffset = new Vector2(0f, 4f),
    };

    private MinimapMask _mask = null!;
    private MinimapLayer _layer = null!;
    private MinimapArrow _arrow = null!;
    private MinimapPicture? _shown;
    private Vector2 _mapSize;
    private float _scale;

    public MinimapView() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Ready()
    {
        _frame.SetBorderWidthAll(4);
        _frame.SetCornerRadiusAll(MinimapMask.Corner + (int)Rim);
        _mask = new MinimapMask { Position = new Vector2(Rim, Rim) };
        AddChild(_mask);
        _layer = new MinimapLayer();
        _mask.AddChild(_layer);
        _arrow = new MinimapArrow();
        _mask.AddChild(_arrow);
    }

    /// <summary>Shows the minimap of <paramref name="hud"/> at <paramref name="fraction"/> between ticks.</summary>
    public void Show(HudModule hud, float fraction)
    {
        var picture = hud.Minimap;
        Visible = hud.MinimapVisible && picture != null;
        if (!Visible || picture == null)
            return;
        var window = hud.MinimapWindow(fraction);
        float ww = window.MaxX - window.MinX, wh = window.MaxZ - window.MinZ;
        if (!ReferenceEquals(picture, _shown))
        {
            _shown = picture;
            _scale = MapSize / MathF.Max(ww, wh);
            _mapSize = new Vector2(ww, wh) * _scale;
            Size = _mapSize + new Vector2(Rim, Rim) * 2f;
            _mask.Size = _mapSize;
            _mask.QueueRedraw();
            _layer.Show(picture, _scale);
            QueueRedraw();
        }
        _layer.Position = new Vector2((picture.Bounds.MinX - window.MinX) * _scale, (picture.Bounds.MinZ - window.MinZ) * _scale);
        var at = hud.MinimapPlayer(fraction);
        _arrow.Position = new Vector2(at.X * _mapSize.X, at.Y * _mapSize.Y);
        _arrow.Rotation = hud.MinimapArrow(fraction);
        Modulate = new Color(1f, 1f, 1f, hud.MinimapAlpha(fraction));
    }

    public override void _Draw()
    {
        DrawStyleBox(_frame, new Rect2(Vector2.Zero, Size));
        // A little pink mushroom cap with cream spots and an "N" sits on the top edge: north.
        var cap = new Vector2(Size.X * 0.5f, -10f);
        DrawCircle(cap, 16f, HudStyle.Ink);
        DrawCircle(cap, 13f, HudStyle.Cap);
        DrawCircle(cap + new Vector2(-8f, -5.5f), 2.8f, HudStyle.Spot);
        DrawCircle(cap + new Vector2(8.5f, -4.5f), 2.3f, HudStyle.Spot);
        DrawCircle(cap + new Vector2(8f, 6.5f), 1.9f, HudStyle.Spot);
        var font = HudStyle.Font;
        const int size = 19;
        float width = font.GetStringSize("N", HorizontalAlignment.Left, -1f, size).X;
        float baseline = cap.Y + (font.GetAscent(size) - font.GetDescent(size)) * 0.5f;
        DrawString(font, new Vector2(cap.X - width * 0.5f, baseline), "N", HorizontalAlignment.Left, -1f, size, HudStyle.Ink);
    }
}
