using System;
using Fungtopia.Map;

namespace Fungtopia.Forest;

/// <summary>
/// Where the ring of dense giant mushrooms around the forest square may stand (the view draws
/// it): outside the square, out of the Map's gaps for the roads (<see cref="Areas.ForestGapSouth"/>,
/// <see cref="Areas.ForestGapNorth"/>) and the Deep Forest path (<see cref="Areas.ForestGapWest"/>),
/// off all walkable ground, and not where the Map draws its own cliff mushrooms beside the roads.
/// </summary>
public static class ForestRing
{
    /// <summary>How deep (meters) the ring reaches beyond the square's edge.</summary>
    public const float Depth = 26f;

    /// <summary>Ring mushrooms keep at least this far (meters) from walkable ground.</summary>
    public const float WalkableClearance = 1.5f;

    /// <summary>Whether a ring mushroom with a stem of <paramref name="radius"/> may stand at (x, z).</summary>
    public static bool Keeps(float x, float z, float radius)
    {
        float outside = MathF.Max(MathF.Abs(x), MathF.Abs(z)) - Areas.ForestHalfSize;
        if (outside < radius + 0.5f || outside > Depth)
            return false;
        if (InGap(x, z, radius))
            return false;
        float beyond = -OtherWalkableDepth(x, z);
        if (beyond < radius + WalkableClearance)
            return false; // a road, the Deep Forest path or a town
        // Beside the roads the Map draws its own cliff mushrooms (3.5 m + radius past the edge).
        return !(Areas.RoadDistance(x, z) <= 14f && -Areas.Depth(x, z) >= 3.5f + radius);
    }

    /// <summary>Like <see cref="Areas.Depth"/>, but for the walkable ground beyond the forest square only (roads, towns, the Deep Forest).</summary>
    private static float OtherWalkableDepth(float x, float z)
    {
        float d = Areas.RoadHalfWidth - Areas.RoadDistance(x, z);
        d = MathF.Max(d, Areas.Town.Depth(x, z));
        d = MathF.Max(d, Areas.PetCity.Depth(x, z));
        d = MathF.Max(d, Areas.DeepForest.Depth(x, z));
        return MathF.Max(d, Areas.DeepForestPath.Depth(x, z));
    }

    /// <summary>Whether a circle of <paramref name="radius"/> at (x, z) reaches into one of the Map's three gaps in the ring.</summary>
    public static bool InGap(float x, float z, float radius)
    {
        float half = Areas.ForestHalfSize;
        // North and south: |x - gap| under the gap's half width, from the edge out through the ring.
        if (MathF.Abs(z) >= half && MathF.Abs(z) >= MathF.Abs(x))
        {
            var gap = z > 0f ? Areas.ForestGapSouth : Areas.ForestGapNorth;
            return MathF.Abs(x - gap.X) < Areas.ForestGapHalfWidth + radius;
        }
        if (x <= -half && MathF.Abs(x) >= MathF.Abs(z))
            return MathF.Abs(z - Areas.ForestGapWest.Y) < Areas.DeepForestGapHalfWidth + radius;
        return false;
    }
}
