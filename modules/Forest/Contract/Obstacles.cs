using System;

namespace Fungtopia.Forest;

/// <summary>
/// The giant mushroom trunks inside <see cref="ForestBounds"/>: a fixed array of circles that
/// walkers must not pass through. The same on every run; never on paths, clearings or the start.
/// (The wall of giant mushrooms around the square stands outside it; walkers stay inside
/// <see cref="ForestBounds"/> instead.)
/// </summary>
public static class Obstacles
{
    /// <summary>Distance (meters) between neighbouring candidate trunk spots.</summary>
    private const float Cell = 10f;

    private static readonly Obstacle[] s_all = Generate();

    /// <summary>Every trunk.</summary>
    public static ReadOnlySpan<Obstacle> All => s_all;

    /// <summary>Number of trunks.</summary>
    public static int Count => s_all.Length;

    /// <summary>Whether a walker of <paramref name="radius"/> standing at (x, z) overlaps a trunk.</summary>
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

    /// <summary>A fixed pseudo-random value in [0, 1) for integer cell (i, j) and channel k.</summary>
    internal static float Hash(int i, int j, int k)
    {
        uint h = unchecked((uint)i * 374761393u + (uint)j * 668265263u + (uint)k * 2246822519u + 0x9E3779B9u);
        h = (h ^ (h >> 15)) * 2246822519u;
        h = (h ^ (h >> 13)) * 3266489917u;
        h ^= h >> 16;
        return (h >> 8) * (1f / 16777216f);
    }

    private static Obstacle[] Generate()
    {
        int n = (int)(2 * ForestBounds.HalfSize / Cell);
        var trunks = new Obstacle[n * n];
        int count = 0;
        for (int i = 0; i < n; i++)
        for (int j = 0; j < n; j++)
        {
            float x = -ForestBounds.HalfSize + (i + 0.15f + 0.7f * Hash(i, j, 0)) * Cell;
            float z = -ForestBounds.HalfSize + (j + 0.15f + 0.7f * Hash(i, j, 1)) * Cell;
            float r = 0.9f + 1.5f * Hash(i, j, 2) * Hash(i, j, 3);
            if (Hash(i, j, 4) < 0.18f)
                continue; // a gap, so the forest is not a grid
            if (!ForestBounds.Contains(x, z, r + 2f))
                continue;
            if (ForestLayout.ClearingDepth(x, z) > -(r + 1.5f))
                continue;
            if (ForestLayout.PathDistance(x, z) < ForestLayout.PathHalfWidth + r + 2f)
                continue;
            bool crowded = false;
            for (int k = 0; k < count && !crowded; k++)
            {
                float dx = x - trunks[k].X, dz = z - trunks[k].Z, gap = r + trunks[k].Radius + 3f;
                crowded = dx * dx + dz * dz < gap * gap;
            }
            if (!crowded)
                trunks[count++] = new Obstacle(x, z, r);
        }
        Array.Resize(ref trunks, count);
        return trunks;
    }
}
