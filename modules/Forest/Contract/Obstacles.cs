using System;

namespace Fungtopia.Forest;

/// <summary>
/// The giant mushroom trunks inside <see cref="ForestBounds"/>: a fixed array of circles that
/// walkers must not pass through. The same on every run; never on paths, clearings or the start.
/// (The wall of giant mushrooms around the square stands outside it; walkers stay inside
/// <see cref="ForestBounds"/> instead.) Forwards to the Map module, which owns the world's
/// obstacles: these are the first <see cref="Fungtopia.Map.Ground.ForestTrunkCount"/> circles of
/// <see cref="Fungtopia.Map.Ground.Obstacles"/>, in the same order.
/// </summary>
public static class Obstacles
{
    private static readonly Obstacle[] s_all = Forward();

    /// <summary>Every trunk.</summary>
    public static ReadOnlySpan<Obstacle> All => s_all;

    /// <summary>Number of trunks.</summary>
    public static int Count => s_all.Length;

    /// <summary>
    /// Whether a walker of <paramref name="radius"/> standing at (x, z) overlaps a trunk (the
    /// forest's only; <see cref="Fungtopia.Map.Ground.Blocks"/> also checks buildings).
    /// </summary>
    public static bool Blocks(float x, float z, float radius = 0f)
    {
        foreach (var o in s_all)
        {
            float dx = x - o.X, dz = z - o.Z, r = o.Radius + radius;
            if (dx * dx + dz * dz < r * r)
                return true;
        }
        return false;
    }

    private static Obstacle[] Forward()
    {
        var trunks = Fungtopia.Map.Ground.Obstacles.Slice(0, Fungtopia.Map.Ground.ForestTrunkCount);
        var all = new Obstacle[trunks.Length];
        for (int i = 0; i < trunks.Length; i++)
            all[i] = new Obstacle(trunks[i].X, trunks[i].Z, trunks[i].Radius);
        return all;
    }
}
