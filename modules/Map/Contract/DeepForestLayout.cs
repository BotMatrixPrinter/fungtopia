using System;
using System.Numerics;

namespace Fungtopia.Map;

/// <summary>
/// The Deep Forest, west of Mushroom Forest: its bounds, its entrance, the spots where its hard
/// monsters spawn, and its giant mushrooms (denser and bigger than the forest's; they are in
/// <see cref="Ground.Obstacles"/> after the buildings). Fixed data, deterministic.
/// </summary>
public static class DeepForestLayout
{
    /// <summary>The Deep Forest's rectangle (<see cref="Areas.DeepForest"/>).</summary>
    public static readonly AreaBounds Bounds = Areas.DeepForest;

    /// <summary>Where the overgrown path from the forest's west gap comes out into the Deep Forest (x, z).</summary>
    public static readonly Vector2 Entrance = new(Areas.DeepForest.MaxX, 0f);

    /// <summary>No spawn spot is closer than this to <see cref="Entrance"/> (meters).</summary>
    public const float SpawnMinEntranceDistance = 40f;

    /// <summary>Around each spawn spot and the entrance, this much ground (meters) is kept clear of giant mushrooms.</summary>
    public const float ClearingRadius = 6f;

    // Spread over the whole Deep Forest, all well away from the entrance.
    private static readonly Vector2[] s_spawns =
    {
        new(-150f, -65f), new(-150f, 62f), new(-185f, -20f), new(-180f, 35f), new(-200f, -55f),
        new(-215f, 70f), new(-235f, 5f), new(-245f, -72f), new(-262f, -30f), new(-260f, 45f),
    };

    /// <summary>The monster spawn spots (x, z): walkable, clear of obstacles, none near the entrance.</summary>
    public static ReadOnlySpan<Vector2> SpawnPoints => s_spawns;

    // The entrance clearing: from the path's end to a few meters in, around the signpost.
    private static float EntranceDistance(float x, float z) =>
        Vector2.Distance(new Vector2(x, z), new Vector2(Math.Clamp(x, Entrance.X - 10f, Entrance.X), 0f));

    /// <summary>The Deep Forest's giant mushroom trunks, packed tighter (2.5 m gaps) and bigger than the forest's.</summary>
    internal static Circle[] Trunks()
    {
        const float cell = 6.5f, gapBetween = 2.5f;
        var b = Bounds;
        int nx = (int)((b.MaxX - b.MinX) / cell), nz = (int)((b.MaxZ - b.MinZ) / cell);
        var trunks = new Circle[nx * nz];
        int count = 0;
        for (int i = 0; i < nx; i++)
        for (int j = 0; j < nz; j++)
        {
            float x = b.MinX + (i + 0.15f + 0.7f * ForestFloor.Hash(i, j, 10)) * cell;
            float z = b.MinZ + (j + 0.15f + 0.7f * ForestFloor.Hash(i, j, 11)) * cell;
            float r = 1.3f + 2.2f * ForestFloor.Hash(i, j, 12) * ForestFloor.Hash(i, j, 13);
            if (ForestFloor.Hash(i, j, 14) < 0.1f)
                continue;
            // Two meters from the edge, so no pocket is ever closed off.
            if (!b.Contains(x, z, r + 2f))
                continue;
            if (EntranceDistance(x, z) < ClearingRadius + 4f + r)
                continue;
            bool clear = true;
            foreach (var s in s_spawns)
                clear &= Vector2.Distance(s, new Vector2(x, z)) >= ClearingRadius + r;
            for (int k = 0; k < count && clear; k++)
            {
                float dx = x - trunks[k].X, dz = z - trunks[k].Z, gap = r + trunks[k].Radius + gapBetween;
                clear = dx * dx + dz * dz >= gap * gap;
            }
            if (clear)
                trunks[count++] = new Circle(x, z, r);
        }
        Array.Resize(ref trunks, count);
        return trunks;
    }

    /// <summary>
    /// The Deep Forest's rough ground on top of the rolling: lumpy soil, ridges of roots and small
    /// dips (meters, about -0.5..0.6). Gentle enough to walk everywhere.
    /// </summary>
    internal static float Roughness(float x, float z)
    {
        float lumps = 0.3f * MathF.Sin(0.19f * x + 0.7f) * MathF.Sin(0.16f * z - 0.4f);
        float roots = 0.14f * MathF.Abs(MathF.Sin(0.5f * x + 0.8f * MathF.Sin(0.21f * z)));
        float dip = MathF.Max(0f, MathF.Sin(0.09f * x + 1.3f) * MathF.Sin(0.1f * z + 2.1f));
        return lumps + roots - 0.35f * dip * dip;
    }
}
