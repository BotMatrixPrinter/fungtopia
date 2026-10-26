using System;
using Fungtopia.Map;
using Godot;

namespace Fungtopia.HUD.View;

/// <summary>Clips the minimap to a rounded square: it draws the shape, its children show only inside it.</summary>
public partial class MinimapMask : Control
{
    public const int Corner = 26;

    private readonly StyleBoxFlat _shape = new() { BgColor = Colors.White, CornerDetail = 12, AntiAliasing = true };

    public MinimapMask()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ClipChildren = ClipChildrenMode.Only;
        _shape.SetCornerRadiusAll(Corner);
    }

    public override void _Draw() => DrawStyleBox(_shape, new Rect2(Vector2.Zero, Size));
}

/// <summary>
/// One zone's picture at a fixed scale: the cells as a soft texture (dark moss off the walkable
/// ground, green grass, lighter clearings, lilac paving, deep moss, golden dirt paths), the
/// buildings as little filled footprints with tiny icons (a heart on your house, a flower on
/// Millie's, a red cross on the clinic, a paw on the Pet Store, a coin on the shops), the signposts
/// as little posts and the NPC spots as dots. Drawn once per zone; the minimap only moves it.
/// </summary>
public partial class MinimapLayer : Control
{
    private static readonly Color Outside = new(0.26f, 0.42f, 0.34f);
    private static readonly Color Hedge = new(0.33f, 0.52f, 0.38f);
    private static readonly Color SpookyOutside = new(0.16f, 0.14f, 0.22f);
    private static readonly Color SpookyHedge = new(0.22f, 0.2f, 0.29f);
    private static readonly Color Grass = new(0.56f, 0.8f, 0.45f);
    private static readonly Color Clearing = new(0.7f, 0.88f, 0.52f);
    private static readonly Color Paving = new(0.87f, 0.81f, 0.94f);
    private static readonly Color Moss = new(0.4f, 0.53f, 0.45f);
    private static readonly Color Dirt = new(1f, 0.88f, 0.65f);

    private static readonly Color HouseColor = new(0.86f, 0.62f, 0.52f);
    private static readonly Color MillieColor = new(0.74f, 0.6f, 0.95f);
    private static readonly Color ClinicColor = new(1f, 0.99f, 0.97f);
    private static readonly Color ShopColor = new(1f, 0.78f, 0.36f);
    private static readonly Color PetStoreColor = new(0.45f, 0.84f, 0.8f);
    private static readonly Color BlockColor = new(0.62f, 0.7f, 0.9f);
    private static readonly Color Water = new(0.5f, 0.8f, 1f);
    private static readonly Color Cross = new(0.92f, 0.25f, 0.3f);
    private static readonly Color Wood = new(0.6f, 0.4f, 0.25f);

    private MinimapPicture? _picture;
    private float _scale = 1f;
    private ImageTexture? _texture;
    private Vector2[][] _footprints = Array.Empty<Vector2[]>();

    public MinimapLayer()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.Linear;
    }

    /// <summary>Builds the texture and footprints of <paramref name="picture"/> at <paramref name="scale"/> pixels per meter.</summary>
    public void Show(MinimapPicture picture, float scale)
    {
        _picture = picture;
        _scale = scale;
        Size = new Vector2(picture.Bounds.MaxX - picture.Bounds.MinX, picture.Bounds.MaxZ - picture.Bounds.MinZ) * scale;
        _texture = ImageTexture.CreateFromImage(Paint(picture));
        _footprints = new Vector2[picture.Buildings.Length][];
        for (int i = 0; i < _footprints.Length; i++)
        {
            var b = picture.Buildings[i];
            var points = new Vector2[5];
            for (int k = 0; k < 4; k++)
                points[k] = ToLayer(b.Corner(k).X, b.Corner(k).Y);
            points[4] = points[0];
            _footprints[i] = points;
        }
        QueueRedraw();
    }

    private Vector2 ToLayer(float x, float z) =>
        new((x - _picture!.Bounds.MinX) * _scale, (z - _picture.Bounds.MinZ) * _scale);

    /// <summary>The cells as pixels; off-ground cells next to the ground are a lighter hedge rim.</summary>
    private static Image Paint(MinimapPicture picture)
    {
        int w = picture.Width, h = picture.Height;
        var cells = picture.Cells;
        bool spooky = picture.Area == Area.DeepForest;
        var bytes = new byte[w * h * 4];
        for (int row = 0; row < h; row++)
            for (int col = 0; col < w; col++)
            {
                var cell = cells[row * w + col];
                Color c = cell switch
                {
                    MinimapCell.Ground => Grass,
                    MinimapCell.Clearing => Clearing,
                    MinimapCell.Paving => Paving,
                    MinimapCell.Moss => Moss,
                    MinimapCell.Path => Dirt,
                    _ => NearGround(cells, w, h, col, row)
                        ? (spooky ? SpookyHedge : Hedge)
                        : (spooky ? SpookyOutside : Outside),
                };
                int i = (row * w + col) * 4;
                bytes[i] = (byte)(c.R * 255f);
                bytes[i + 1] = (byte)(c.G * 255f);
                bytes[i + 2] = (byte)(c.B * 255f);
                bytes[i + 3] = 255;
            }
        return Image.CreateFromData(w, h, false, Image.Format.Rgba8, bytes);
    }

    private static bool NearGround(MinimapCell[] cells, int w, int h, int col, int row)
    {
        for (int dr = -2; dr <= 2; dr++)
            for (int dc = -2; dc <= 2; dc++)
            {
                int r = row + dr, c = col + dc;
                if (r >= 0 && c >= 0 && r < h && c < w && cells[r * w + c] != MinimapCell.Outside)
                    return true;
            }
        return false;
    }

    public override void _Draw()
    {
        if (_picture == null || _texture == null)
            return;
        DrawTextureRect(_texture, new Rect2(Vector2.Zero, Size), false);

        for (int i = 0; i < _footprints.Length; i++)
            DrawBuilding(_picture.Buildings[i], _footprints[i]);

        foreach (var mark in _picture.Marks)
        {
            var p = ToLayer(mark.X, mark.Z);
            if (mark.Kind == MinimapMarkKind.Signpost)
            {
                // A little post with a golden sign board.
                DrawLine(p + new Vector2(0f, 3f), p + new Vector2(0f, -7f), HudStyle.Ink, 3f, true);
                DrawRect(new Rect2(p + new Vector2(-5.5f, -11f), new Vector2(11f, 6f)), HudStyle.Ink);
                DrawRect(new Rect2(p + new Vector2(-4f, -9.5f), new Vector2(8f, 3f)), HudStyle.Gold);
            }
            else
            {
                DrawCircle(p, 3.6f, HudStyle.Ink);
                DrawCircle(p, 2.3f, HudStyle.Cream);
            }
        }
    }

    private void DrawBuilding(in MinimapBuilding b, Vector2[] footprint)
    {
        var center = ToLayer(b.Center.X, b.Center.Y);
        if (b.Round)
        {
            float r = (b.A - b.C).Length() * 0.5f / MathF.Sqrt(2f) * _scale;
            DrawCircle(center, r + 1.2f, HudStyle.Ink);
            DrawCircle(center, r, Water);
            return;
        }
        var fill = b.Kind switch
        {
            BuildingKind.PlayerHouse => HudStyle.Heart,
            BuildingKind.MillieHouse => MillieColor,
            BuildingKind.Clinic => ClinicColor,
            BuildingKind.FurnitureShop or BuildingKind.ClothingShop or BuildingKind.FoodStall => ShopColor,
            BuildingKind.PetStore => PetStoreColor,
            BuildingKind.CityBlock => BlockColor,
            _ => HouseColor,
        };
        DrawColoredPolygon(footprint, fill);
        DrawPolyline(footprint, HudStyle.Ink, 1.6f, true);
        switch (b.Kind)
        {
            case BuildingKind.PlayerHouse:
                // A tiny cream heart: your house.
                DrawCircle(center + new Vector2(-2f, -1.2f), 2.3f, HudStyle.Cream);
                DrawCircle(center + new Vector2(2f, -1.2f), 2.3f, HudStyle.Cream);
                DrawColoredPolygon(new[] { center + new Vector2(-4.2f, -0.4f), center + new Vector2(4.2f, -0.4f), center + new Vector2(0f, 4.2f) }, HudStyle.Cream);
                break;
            case BuildingKind.MillieHouse:
                // A little flower.
                for (int k = 0; k < 5; k++)
                {
                    float a = k * MathF.Tau / 5f;
                    DrawCircle(center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 2.4f, 1.6f, HudStyle.Cream);
                }
                DrawCircle(center, 1.4f, HudStyle.Gold);
                break;
            case BuildingKind.Clinic:
                DrawRect(new Rect2(center - new Vector2(1.2f, 4f), new Vector2(2.4f, 8f)), Cross);
                DrawRect(new Rect2(center - new Vector2(4f, 1.2f), new Vector2(8f, 2.4f)), Cross);
                break;
            case BuildingKind.PetStore:
                // A paw: a pad and four toes.
                DrawCircle(center + new Vector2(0f, 1.6f), 2.6f, HudStyle.Ink);
                for (int k = 0; k < 4; k++)
                    DrawCircle(center + new Vector2(-3.6f + k * 2.4f, k is 0 or 3 ? -1.4f : -3f), 1.1f, HudStyle.Ink);
                break;
            case BuildingKind.FurnitureShop or BuildingKind.ClothingShop:
                DrawCircle(center, 2.2f, HudStyle.GoldShade);
                break;
        }
    }
}

/// <summary>The player's arrow: a chubby pink arrowhead with an ink outline and a cream dot, tip up at rotation 0.</summary>
public partial class MinimapArrow : Control
{
    private static readonly Vector2[] Shape =
    {
        new(0f, -11f), new(8.5f, 7.5f), new(0f, 3.5f), new(-8.5f, 7.5f),
    };

    private static readonly Vector2[] Outline =
    {
        new(0f, -11f), new(8.5f, 7.5f), new(0f, 3.5f), new(-8.5f, 7.5f), new(0f, -11f),
    };

    public MinimapArrow() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw()
    {
        DrawCircle(new Vector2(0f, 0f), 12.5f, new Color(HudStyle.Cream, 0.55f));
        DrawColoredPolygon(Shape, HudStyle.Heart);
        DrawPolyline(Outline, HudStyle.Ink, 2.5f, true);
        DrawCircle(new Vector2(0f, -1.5f), 1.8f, HudStyle.Cream);
    }
}
