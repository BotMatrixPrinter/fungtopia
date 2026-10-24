using System;
using System.Collections.Generic;

namespace Fungtopia.Map;

/// <summary>
/// The world's ground: its height everywhere and the obstacles on it (giant mushroom trunks,
/// buildings, the fountain). Pure, deterministic and allocation-free.
/// </summary>
public static class Ground
{
    /// <summary>How high the cliffs around the walkable ground rise above it (meters).</summary>
    public const float CliffHeight = 6.5f;

    /// <summary>Beyond the walkable edge, the hedge band (meters) before the cliffs start rising.</summary>
    public const float HedgeBand = 1.5f;

    /// <summary>Over how many meters (beyond the hedge band) the cliffs rise to full height.</summary>
    public const float CliffRise = 12f;

    /// <summary>The footprint circles of one building are at most about this big a cell (meters).</summary>
    private const float CoverCell = 4f;

    // Obstacle bins: a grid of BinSize cells over the forest, roads and both towns.
    private const float BinSize = 16f, BinMinX = -112f, BinMinZ = -304f;
    private const int BinsX = 14, BinsZ = 38;

    private static readonly Circle[] s_obstacles;
    private static readonly int s_forestTrunks;
    private static readonly int[] s_binStart = new int[BinsX * BinsZ + 1];
    private static readonly int[] s_binItems;

    static Ground()
    {
        s_obstacles = BuildObstacles(out s_forestTrunks);
        s_binItems = BuildBins();
    }

    /// <summary>
    /// Every obstacle circle walkers must not enter: first the forest's giant mushroom trunks
    /// (exactly modules/Forest's), then the circles covering each building of <see cref="Buildings.All"/>, in order.
    /// </summary>
    public static ReadOnlySpan<Circle> Obstacles => s_obstacles;

    /// <summary>How many of the first <see cref="Obstacles"/> are the forest's trunks.</summary>
    public static int ForestTrunkCount => s_forestTrunks;

    /// <summary>
    /// Ground height (meters, Y up) at (x, z). In the forest square it is exactly the forest's
    /// rolling ground; beyond it the rolling flattens out (roads, towns), and past the walkable
    /// edge it rises into cliffs. The home interior's floor is flat at <see cref="Areas.HomeFloorY"/>.
    /// Defined everywhere, continuous except at the far-away home room.
    /// </summary>
    public static float HeightAt(float x, float z)
    {
        float rolling = ForestFloor.Rolling(x, z);
        float outside = MathF.Max(MathF.Abs(x), MathF.Abs(z)) - Areas.ForestHalfSize;
        if (outside <= 0f)
            return rolling;
        if (Areas.Home.Depth(x, z) > -30f)
            return Areas.HomeFloorY;
        float h = rolling * (1f - 0.8f * SmoothStep(4f, 34f, outside));
        float beyond = -Areas.Depth(x, z);
        if (beyond > HedgeBand)
            h += CliffHeight * SmoothStep(HedgeBand, HedgeBand + CliffRise, beyond);
        return h;
    }

    /// <summary>Whether a walker of <paramref name="radius"/> standing at (x, z) overlaps an obstacle.</summary>
    public static bool Blocks(float x, float z, float radius = 0f)
    {
        int i0 = BinX(x - radius), i1 = BinX(x + radius), j0 = BinZ(z - radius), j1 = BinZ(z + radius);
        if (i1 < 0 || j1 < 0 || i0 >= BinsX || j0 >= BinsZ)
            return false;
        i0 = Math.Max(i0, 0); j0 = Math.Max(j0, 0);
        i1 = Math.Min(i1, BinsX - 1); j1 = Math.Min(j1, BinsZ - 1);
        for (int i = i0; i <= i1; i++)
        for (int j = j0; j <= j1; j++)
        {
            int bin = i * BinsZ + j;
            for (int k = s_binStart[bin]; k < s_binStart[bin + 1]; k++)
            {
                var o = s_obstacles[s_binItems[k]];
                float dx = x - o.X, dz = z - o.Z, r = o.Radius + radius;
                if (dx * dx + dz * dz < r * r)
                    return true;
            }
        }
        return false;
    }

    private static float SmoothStep(float edge0, float edge1, float v)
    {
        float t = Math.Clamp((v - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static int BinX(float x) => (int)MathF.Floor((x - BinMinX) / BinSize);

    private static int BinZ(float z) => (int)MathF.Floor((z - BinMinZ) / BinSize);

    private static Circle[] BuildObstacles(out int forestTrunks)
    {
        var all = new List<Circle>(ForestFloor.Trunks());
        forestTrunks = all.Count;
        foreach (var b in Buildings.All)
        {
            if (b.Kind == BuildingKind.Fountain)
            {
                all.Add(new Circle(b.X, b.Z, MathF.Max(b.Width, b.Depth) * 0.5f + 0.05f));
                continue;
            }
            // A grid of circles, each through the corners of its cell, covers the rectangle.
            int nx = (int)MathF.Ceiling(b.Width / CoverCell), nz = (int)MathF.Ceiling(b.Depth / CoverCell);
            float cw = b.Width / nx, cd = b.Depth / nz, r = 0.5f * MathF.Sqrt(cw * cw + cd * cd) + 0.05f;
            for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                var c = b.ToWorld(-b.Width * 0.5f + (i + 0.5f) * cw, -b.Depth * 0.5f + (j + 0.5f) * cd);
                all.Add(new Circle(c.X, c.Y, r));
            }
        }
        return all.ToArray();
    }

    private static int[] BuildBins()
    {
        var lists = new List<int>[BinsX * BinsZ];
        for (int b = 0; b < lists.Length; b++)
            lists[b] = new List<int>();
        for (int k = 0; k < s_obstacles.Length; k++)
        {
            var o = s_obstacles[k];
            for (int i = Math.Max(BinX(o.X - o.Radius), 0); i <= Math.Min(BinX(o.X + o.Radius), BinsX - 1); i++)
            for (int j = Math.Max(BinZ(o.Z - o.Radius), 0); j <= Math.Min(BinZ(o.Z + o.Radius), BinsZ - 1); j++)
                lists[i * BinsZ + j].Add(k);
        }
        var items = new List<int>();
        for (int b = 0; b < lists.Length; b++)
        {
            s_binStart[b] = items.Count;
            items.AddRange(lists[b]);
        }
        s_binStart[lists.Length] = items.Count;
        return items.ToArray();
    }
}
