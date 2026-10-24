using System;
using System.Numerics;
using Fungtopia.Map;

namespace Fungtopia.Town;

/// <summary>
/// Where a placement stands on the uneven ground (<see cref="Ground.HeightAt"/>): <see cref="Low"/>
/// and <see cref="High"/> are the lowest and highest ground under its footprint, <see cref="Base"/>
/// where its base goes (the lowest ground, sunk a little), <see cref="Top"/> its top (its height
/// above the highest ground, so it looks as tall as it is from the high side).
/// </summary>
public readonly struct GroundFit
{
    public readonly float Low;
    public readonly float High;
    public readonly float Base;
    public readonly float Top;

    public GroundFit(float low, float high, float sink, float height)
    {
        Low = low;
        High = high;
        Base = low - sink;
        Top = high + height;
    }

    /// <summary>How much taller than its own height it is drawn: its stem reaches down to <see cref="Base"/>.</summary>
    public float Extra => High - Base;

    /// <summary>The height it is drawn at, from <see cref="Base"/> to <see cref="Top"/>.</summary>
    public float DrawnHeight => Top - Base;
}

/// <summary>
/// Plants Town's objects so their whole base touches or goes into the ground: samples the ground
/// at the footprint's center and <see cref="FootprintPoints"/> - 1 points on and inside its edge,
/// sets the base at the lowest of them minus a small <see cref="Sink"/>, and stretches the stem
/// down by the difference to the highest. Plain math, the same every run.
/// </summary>
public static class TownGround
{
    /// <summary>Meters the base goes below the lowest ground under the footprint.</summary>
    public const float Sink = 0.15f;

    /// <summary>A flower clump sinks only this much (it has no stem to hide).</summary>
    public const float FlowersSink = 0.05f;

    /// <summary>Points on the footprint's edge (an ellipse, or a rectangle for boxy pieces).</summary>
    public const int EdgePoints = 16;

    /// <summary>Points on a ring halfway in.</summary>
    public const int InnerPoints = 8;

    /// <summary>All the points sampled: the center, the edge, the inner ring.</summary>
    public const int FootprintPoints = 1 + EdgePoints + InnerPoints;

    /// <summary>Whether a piece stands on a rectangular footprint (otherwise a round or oval stem or basin).</summary>
    public static bool IsBoxy(TownPiece piece) =>
        piece is TownPiece.FurnitureShop or TownPiece.FoodStall or TownPiece.Bench or TownPiece.Fence or TownPiece.Crate;

    /// <summary>Whether a piece gets a flared skirt where it meets the ground (big stems, foundations, basins).</summary>
    public static bool HasRootFlare(TownPiece piece) =>
        piece is TownPiece.PlayerHouse or TownPiece.MillieHouse or TownPiece.Clinic or TownPiece.House
            or TownPiece.ClothingShop or TownPiece.Fountain or TownPiece.StreetLamp or TownPiece.Planter;

    /// <summary>The sink of a piece below its lowest ground.</summary>
    public static float SinkOf(TownPiece piece) => piece == TownPiece.Flowers ? FlowersSink : Sink;

    /// <summary>
    /// Footprint point <paramref name="k"/> (0 .. <see cref="FootprintPoints"/> - 1) of a piece of
    /// <paramref name="width"/> by <paramref name="depth"/> at (x, z) turned by <paramref name="yaw"/>,
    /// as world (x, z).
    /// </summary>
    public static Vector2 FootprintPoint(TownPiece piece, float x, float z, float yaw, float width, float depth, int k)
    {
        if ((uint)k >= FootprintPoints)
            throw new ArgumentOutOfRangeException(nameof(k));
        float lx = 0f, lz = 0f;
        if (k > 0)
        {
            bool edge = k <= EdgePoints;
            int i = edge ? k - 1 : k - 1 - EdgePoints;
            int n = edge ? EdgePoints : InnerPoints;
            float scale = edge ? 0.5f : 0.25f;
            float angle = i * MathF.Tau / n;
            float c = MathF.Cos(angle), s = MathF.Sin(angle);
            if (IsBoxy(piece))
            {
                // Onto the rectangle's edge (its corners at the diagonals).
                float m = MathF.Max(MathF.Abs(c), MathF.Abs(s));
                c /= m;
                s /= m;
            }
            lx = c * width * scale;
            lz = s * depth * scale;
        }
        // Local X/Z turned by yaw about +Y (Godot: yaw 0 faces -Z).
        float cy = MathF.Cos(yaw), sy = MathF.Sin(yaw);
        return new Vector2(x + lx * cy + lz * sy, z - lx * sy + lz * cy);
    }

    /// <summary>Footprint point <paramref name="k"/> of placement <paramref name="p"/>.</summary>
    public static Vector2 FootprintPoint(in TownPlacement p, int k) =>
        FootprintPoint(p.Piece, p.X, p.Z, p.Yaw, p.Width, p.Depth, k);

    /// <summary>How a piece of this size at (x, z) stands on the ground.</summary>
    public static GroundFit Fit(TownPiece piece, float x, float z, float yaw, float width, float depth, float height)
    {
        float low = float.MaxValue, high = float.MinValue;
        for (int k = 0; k < FootprintPoints; k++)
        {
            var w = FootprintPoint(piece, x, z, yaw, width, depth, k);
            float h = Ground.HeightAt(w.X, w.Y);
            low = MathF.Min(low, h);
            high = MathF.Max(high, h);
        }
        return new GroundFit(low, high, SinkOf(piece), height);
    }

    /// <summary>How placement <paramref name="p"/> stands on the ground.</summary>
    public static GroundFit Fit(in TownPlacement p) => Fit(p.Piece, p.X, p.Z, p.Yaw, p.Width, p.Depth, p.Height);
}
