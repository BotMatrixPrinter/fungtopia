using System;
using System.Numerics;

namespace Fungtopia.Forest;

/// <summary>
/// The forest's open places: round clearings and winding dirt paths between them. Giant
/// mushrooms (<see cref="Obstacles"/>) never stand on them.
/// </summary>
public static class ForestLayout
{
    /// <summary>Half the width of a dirt path, in meters.</summary>
    public const float PathHalfWidth = 1.8f;

    private static readonly Clearing[] s_clearings =
    {
        new(0f, 84f, 13f),     // the start clearing, near the south edge
        new(0f, -5f, 17f),     // the big middle clearing
        new(-56f, 22f, 12f),
        new(54f, -36f, 12f),
        new(-22f, -70f, 12f),
        new(60f, 56f, 11f),
    };

    // Each path is a polyline of (x, z) points; consecutive paths are separated by a NaN point.
    private static readonly Vector2[] s_paths =
    {
        // South to north through the middle.
        new(0f, 84f), new(5f, 70f), new(8f, 58f), new(3f, 46f), new(-5f, 35f), new(-4f, 22f),
        new(3f, 10f), new(0f, -5f), new(-8f, -22f), new(-15f, -36f), new(-13f, -50f),
        new(-22f, -70f), new(-30f, -84f), new(-26f, -99f),
        new(float.NaN, float.NaN),
        // West to east.
        new(-99f, 30f), new(-80f, 26f), new(-56f, 22f), new(-42f, 13f), new(-28f, 10f),
        new(-14f, 2f), new(0f, -5f), new(14f, -12f), new(26f, -25f), new(40f, -30f),
        new(54f, -36f), new(70f, -34f), new(84f, -42f), new(99f, -40f),
        new(float.NaN, float.NaN),
        // North-east clearing down to the middle path.
        new(60f, 56f), new(48f, 48f), new(36f, 46f), new(24f, 38f), new(10f, 37f), new(-5f, 35f),
    };

    /// <summary>The clearings; the first one holds <see cref="ForestBounds.PlayerStart"/>.</summary>
    public static ReadOnlySpan<Clearing> Clearings => s_clearings;

    /// <summary>The path polylines as (x, z) points; a NaN point separates two paths.</summary>
    public static ReadOnlySpan<Vector2> PathPoints => s_paths;

    /// <summary>Horizontal distance (meters) from (x, z) to the center line of the nearest path.</summary>
    public static float PathDistance(float x, float z)
    {
        var p = new Vector2(x, z);
        float best = float.MaxValue;
        for (int i = 0; i + 1 < s_paths.Length; i++)
        {
            Vector2 a = s_paths[i], b = s_paths[i + 1];
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

    /// <summary>Whether (x, z) lies on a dirt path.</summary>
    public static bool OnPath(float x, float z) => PathDistance(x, z) <= PathHalfWidth;

    /// <summary>How far (x, z) is inside the nearest clearing (meters, negative when outside all).</summary>
    public static float ClearingDepth(float x, float z)
    {
        float best = float.MinValue;
        foreach (var c in s_clearings)
        {
            float depth = c.Radius - MathF.Sqrt((x - c.X) * (x - c.X) + (z - c.Z) * (z - c.Z));
            if (depth > best)
                best = depth;
        }
        return best;
    }
}
