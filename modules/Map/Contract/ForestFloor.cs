using System;
using System.Numerics;

namespace Fungtopia.Map;

/// <summary>
/// The Mushroom Forest exactly as modules/Forest/Contract defines it (copied, so the Map does not
/// depend on the Forest module): its rolling-ground formula, paths, clearings and giant mushroom
/// trunks. Heights, paths and trunks inside the forest square are unchanged.
/// </summary>
internal static class ForestFloor
{
    public const float HalfSize = 100f;
    public const float PathHalfWidth = 1.8f;

    /// <summary>The forest's rolling ground (Forest's Terrain.HeightAt).</summary>
    public static float Rolling(float x, float z)
    {
        return 1.1f * MathF.Sin(0.031f * x + 0.4f) * MathF.Cos(0.027f * z - 1.1f)
             + 0.5f * MathF.Sin(0.052f * x - 0.061f * z + 2.3f)
             + 0.3f * MathF.Sin(0.13f * x + 1.7f) * MathF.Sin(0.11f * z + 0.3f);
    }

    // (x, z, radius) of each clearing; the first holds the forest's start.
    private static readonly Vector3[] s_clearings =
    {
        new(0f, 84f, 13f), new(0f, -5f, 17f), new(-56f, 22f, 12f),
        new(54f, -36f, 12f), new(-22f, -70f, 12f), new(60f, 56f, 11f),
    };

    private static readonly Vector2[] s_paths =
    {
        new(0f, 84f), new(5f, 70f), new(8f, 58f), new(3f, 46f), new(-5f, 35f), new(-4f, 22f),
        new(3f, 10f), new(0f, -5f), new(-8f, -22f), new(-15f, -36f), new(-13f, -50f),
        new(-22f, -70f), new(-30f, -84f), new(-26f, -99f),
        new(float.NaN, float.NaN),
        new(-99f, 30f), new(-80f, 26f), new(-56f, 22f), new(-42f, 13f), new(-28f, 10f),
        new(-14f, 2f), new(0f, -5f), new(14f, -12f), new(26f, -25f), new(40f, -30f),
        new(54f, -36f), new(70f, -34f), new(84f, -42f), new(99f, -40f),
        new(float.NaN, float.NaN),
        new(60f, 56f), new(48f, 48f), new(36f, 46f), new(24f, 38f), new(10f, 37f), new(-5f, 35f),
    };

    public static ReadOnlySpan<Vector2> PathPoints => s_paths;

    /// <summary>Horizontal distance from (x, z) to the nearest center line of a polyline set (NaN separates lines).</summary>
    public static float PolylineDistance(ReadOnlySpan<Vector2> points, float x, float z)
    {
        var p = new Vector2(x, z);
        float best = float.MaxValue;
        for (int i = 0; i + 1 < points.Length; i++)
        {
            Vector2 a = points[i], b = points[i + 1];
            if (float.IsNaN(a.X) || float.IsNaN(b.X))
                continue;
            Vector2 ab = b - a;
            float t = Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0f, 1f);
            float d = Vector2.DistanceSquared(p, a + ab * t);
            if (d < best)
                best = d;
        }
        return MathF.Sqrt(best);
    }

    public static float ClearingDepth(float x, float z)
    {
        float best = float.MinValue;
        foreach (var c in s_clearings)
        {
            float depth = c.Z - MathF.Sqrt((x - c.X) * (x - c.X) + (z - c.Y) * (z - c.Y));
            if (depth > best)
                best = depth;
        }
        return best;
    }

    private static float Hash(int i, int j, int k)
    {
        uint h = unchecked((uint)i * 374761393u + (uint)j * 668265263u + (uint)k * 2246822519u + 0x9E3779B9u);
        h = (h ^ (h >> 15)) * 2246822519u;
        h = (h ^ (h >> 13)) * 3266489917u;
        h ^= h >> 16;
        return (h >> 8) * (1f / 16777216f);
    }

    private static bool InSquare(float x, float z, float margin)
    {
        float limit = HalfSize - margin;
        return x >= -limit && x <= limit && z >= -limit && z <= limit;
    }

    /// <summary>The forest's giant mushroom trunks (Forest's Obstacles.All), in the same order.</summary>
    public static Circle[] Trunks()
    {
        const float cell = 10f;
        int n = (int)(2 * HalfSize / cell);
        var trunks = new Circle[n * n];
        int count = 0;
        for (int i = 0; i < n; i++)
        for (int j = 0; j < n; j++)
        {
            float x = -HalfSize + (i + 0.15f + 0.7f * Hash(i, j, 0)) * cell;
            float z = -HalfSize + (j + 0.15f + 0.7f * Hash(i, j, 1)) * cell;
            float r = 0.9f + 1.5f * Hash(i, j, 2) * Hash(i, j, 3);
            if (Hash(i, j, 4) < 0.18f)
                continue;
            if (!InSquare(x, z, r + 2f))
                continue;
            if (ClearingDepth(x, z) > -(r + 1.5f))
                continue;
            if (PolylineDistance(s_paths, x, z) < PathHalfWidth + r + 2f)
                continue;
            bool crowded = false;
            for (int k = 0; k < count && !crowded; k++)
            {
                float dx = x - trunks[k].X, dz = z - trunks[k].Z, gap = r + trunks[k].Radius + 3f;
                crowded = dx * dx + dz * dz < gap * gap;
            }
            if (!crowded)
                trunks[count++] = new Circle(x, z, r);
        }
        Array.Resize(ref trunks, count);
        return trunks;
    }
}
