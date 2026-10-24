using System;
using System.Collections.Generic;

namespace Fungtopia.Forest;

/// <summary>
/// The obstacles inside <see cref="ForestBounds"/>: a fixed array of circles that walkers must not
/// pass through. The same on every run. (The wall of giant mushrooms around the square stands
/// outside it; walkers stay inside <see cref="ForestBounds"/> instead.) Forwards to the Map module,
/// which owns the world's obstacles: these are every circle of
/// <see cref="Fungtopia.Map.Ground.Obstacles"/> whose center is inside
/// <see cref="Fungtopia.Map.Areas.Forest"/>, in the same order, whatever it is (today the giant
/// mushroom trunks, first among them; any solid prop the Map adds there too).
/// </summary>
public static class Obstacles
{
    private static readonly Obstacle[] s_all = Forward();

    /// <summary>Every obstacle in the forest.</summary>
    public static ReadOnlySpan<Obstacle> All => s_all;

    /// <summary>Number of obstacles in the forest.</summary>
    public static int Count => s_all.Length;

    /// <summary>
    /// Whether a walker of <paramref name="radius"/> standing at (x, z) overlaps an obstacle
    /// in the forest (the forest's only; <see cref="Fungtopia.Map.Ground.Blocks"/> checks them all).
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
        var all = new List<Obstacle>();
        foreach (var o in Fungtopia.Map.Ground.Obstacles)
            if (Fungtopia.Map.Areas.Forest.Contains(o.X, o.Z))
                all.Add(new Obstacle(o.X, o.Z, o.Radius));
        return all.ToArray();
    }
}
