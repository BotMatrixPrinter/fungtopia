using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Map;

namespace Fungtopia.Forest;

/// <summary>
/// Where the view's props (tiny mushrooms, ferns, flowers) may stand, and the dressing of
/// the west gap to the Deep Forest: the overgrown little trail from the gap to the forest's paths
/// and a few small dark mushrooms beside it. Props never stand on paths, trunks, the trail or
/// the Map's Deep Forest path (<see cref="Areas.DeepForestPath"/>), so the gap stays open.
/// </summary>
public static class ForestProps
{
    /// <summary>Half the width (meters) of the overgrown trail.</summary>
    public const float TrailHalfWidth = 1.1f;

    /// <summary>Props keep this far (meters) from the Map's Deep Forest path.</summary>
    public const float GapClearance = 0.5f;

    // From the west gap, winding between the trunks to the forest's west path at (-80, 26).
    private static readonly Vector2[] s_trail =
    {
        new(-100f, 0f), new(-94f, 0.8f), new(-89f, 4.5f), new(-84.5f, 11f), new(-81.5f, 19f), new(-80f, 26f),
    };

    /// <summary>The overgrown trail's center line, (x, z) points from the west gap inward.</summary>
    public static ReadOnlySpan<Vector2> TrailPoints => s_trail;

    /// <summary>Horizontal distance from (x, z) to the trail's center line.</summary>
    public static float TrailDistance(float x, float z)
    {
        var p = new Vector2(x, z);
        float best = float.MaxValue;
        for (int i = 0; i + 1 < s_trail.Length; i++)
        {
            Vector2 a = s_trail[i], ab = s_trail[i + 1] - a;
            float t = Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0f, 1f);
            best = MathF.Min(best, Vector2.DistanceSquared(p, a + ab * t));
        }
        return MathF.Sqrt(best);
    }

    /// <summary>Whether a circle of <paramref name="radius"/> at (x, z) comes within <see cref="GapClearance"/> of the Map's Deep Forest path through the west gap.</summary>
    public static bool InWestGap(float x, float z, float radius) =>
        Areas.DeepForestPath.Depth(x, z) > -(radius + GapClearance);

    /// <summary>Whether a prop of <paramref name="radius"/> at (x, z) may stand: inside the square, off trunks, paths, the trail and the west gap.</summary>
    public static bool Open(float x, float z, float radius) =>
        ForestBounds.Contains(x, z, radius) && !Obstacles.Blocks(x, z, radius + 0.3f)
        && ForestLayout.PathDistance(x, z) > ForestLayout.PathHalfWidth + radius
        && TrailDistance(x, z) > TrailHalfWidth + radius
        && !InWestGap(x, z, radius);

    private static readonly SmallMushroom[] s_dark = PlaceDark();

    /// <summary>A few tiny dark Deep Forest mushrooms (under <see cref="ForestScenery.DecorationMaxHeight"/> drawn) beside the trail near the west gap, each <see cref="Open"/>.</summary>
    public static ReadOnlySpan<SmallMushroom> DarkMushrooms => s_dark;

    private static SmallMushroom[] PlaceDark()
    {
        var dark = new List<SmallMushroom>();
        for (int k = 0; k < 40 && dark.Count < 7; k++)
        {
            // Along both sides of the gap's mouth, a little way in.
            float side = k % 2 == 0 ? 1f : -1f;
            float x = -Areas.ForestHalfSize + 1f + 8f * ForestRandom.Value(k, 90);
            float z = side * (Areas.DeepForestGapHalfWidth + 1.2f + 5f * ForestRandom.Value(k, 91));
            float size = 0.12f + 0.1f * ForestRandom.Value(k, 92);
            if (Open(x, z, size))
                dark.Add(new SmallMushroom(x, z, size));
        }
        return dark.ToArray();
    }
}

/// <summary>A small mushroom at (x, z) whose cap is <see cref="Size"/> meters across in radius.</summary>
public readonly record struct SmallMushroom(float X, float Z, float Size);
