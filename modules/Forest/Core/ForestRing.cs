using System;
using System.Collections.Generic;
using Fungtopia.Map;

namespace Fungtopia.Forest;

/// <summary>
/// Where the ring of dense giant mushrooms around the forest square may stand (the view draws
/// it): outside the square, out of the Map's gaps for the roads (<see cref="Areas.ForestGapSouth"/>,
/// <see cref="Areas.ForestGapNorth"/>) and the Deep Forest path (<see cref="Areas.ForestGapWest"/>),
/// off all walkable ground, and not where the Map draws its own cliff mushrooms beside the roads;
/// and where the view puts them (<see cref="Mushrooms"/>), the ones by the west gap darker.
/// </summary>
public static class ForestRing
{
    /// <summary>How deep (meters) the ring reaches beyond the square's edge.</summary>
    public const float Depth = 26f;

    /// <summary>Ring mushrooms keep at least this far (meters) from walkable ground.</summary>
    public const float WalkableClearance = 1.5f;

    /// <summary>Ring mushrooms within this distance (meters) of <see cref="Areas.ForestGapWest"/> have dark Deep Forest caps.</summary>
    public const float DarkReach = 16f;

    /// <summary>The grid cell (meters) the ring is laid out on, one mushroom at most per cell.</summary>
    public const float Cell = 5.5f;

    private static readonly RingMushroom[] s_mushrooms = Place();

    /// <summary>Every ring mushroom the view draws: each one <see cref="Keeps"/> allows, the same every run.</summary>
    public static ReadOnlySpan<RingMushroom> Mushrooms => s_mushrooms;

    private static RingMushroom[] Place()
    {
        var list = new List<RingMushroom>();
        // A row of stems along the narrow strip between the square and the Deep Forest (too narrow
        // for the grid to fill), starting on each side right at the west gap's edge (as close as
        // Keeps allows), so the ring opens there just as wide as the Map's Deep Forest path.
        int id = 100000;
        for (int side = -1; side <= 1; side += 2)
        {
            float edge = Areas.DeepForestGapHalfWidth + WalkableClearance + 0.05f;
            while (edge < Areas.ForestHalfSize + Depth)
            {
                float r = 1.2f + 0.8f * ForestRandom.Value(id, 36);
                float x = Areas.ForestGapWest.X - 4.5f - 0.6f * ForestRandom.Value(id, 37);
                float z = Areas.ForestGapWest.Y + side * (edge + r);
                if (Keeps(x, z, r) && Clear(list, list.Count, x, z, r))
                    list.Add(Mushroom(x, z, r, id));
                id++;
                edge += 2f * r + 0.6f;
            }
        }

        // Then the band, on a jittered grid, around the flanking stems.
        int flanks = list.Count;
        float outer = Areas.ForestHalfSize + Depth;
        id = 0;
        for (float gx = -outer; gx < outer; gx += Cell)
        for (float gz = -outer; gz < outer; gz += Cell, id++)
        {
            float x = gx + Cell * (0.15f + 0.7f * ForestRandom.Value(id, 30));
            float z = gz + Cell * (0.15f + 0.7f * ForestRandom.Value(id, 31));
            float r = 1.1f + 1.5f * ForestRandom.Value(id, 32);
            if (Keeps(x, z, r) && Clear(list, flanks, x, z, r))
                list.Add(Mushroom(x, z, r, id)); // else inside the square, in a gap, where the Map draws its own, or on a flanking stem
        }
        return list.ToArray();
    }

    private static RingMushroom Mushroom(float x, float z, float r, int id)
    {
        float toGap = MathF.Sqrt((x - Areas.ForestGapWest.X) * (x - Areas.ForestGapWest.X) + (z - Areas.ForestGapWest.Y) * (z - Areas.ForestGapWest.Y));
        return new RingMushroom(x, z, r, toGap <= DarkReach, id);
    }

    /// <summary>Whether a stem of <paramref name="r"/> at (x, z) keeps clear of the first <paramref name="count"/> placed stems.</summary>
    private static bool Clear(List<RingMushroom> placed, int count, float x, float z, float r)
    {
        for (int i = 0; i < count; i++)
        {
            var m = placed[i];
            if ((m.X - x) * (m.X - x) + (m.Z - z) * (m.Z - z) < (m.Radius + r + 0.5f) * (m.Radius + r + 0.5f))
                return false;
        }
        return true;
    }

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

/// <summary>One giant mushroom of the ring: its stem circle, whether its cap is a dark Deep Forest one, and a fixed id for its looks.</summary>
public readonly record struct RingMushroom(float X, float Z, float Radius, bool Dark, int Id);
