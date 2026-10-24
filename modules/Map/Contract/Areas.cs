using System;
using System.Numerics;

namespace Fungtopia.Map;

/// <summary>The places of the world. <see cref="Areas.At"/> tells which one a point belongs to.</summary>
public enum Area
{
    Town,
    Road,
    Forest,
    PetCity,
    Home,
    DeepForest,
}

/// <summary>An axis-aligned rectangle on the ground (x/z, meters).</summary>
public readonly struct AreaBounds
{
    public readonly float MinX;
    public readonly float MinZ;
    public readonly float MaxX;
    public readonly float MaxZ;

    public AreaBounds(float minX, float minZ, float maxX, float maxZ)
    {
        MinX = minX;
        MinZ = minZ;
        MaxX = maxX;
        MaxZ = maxZ;
    }

    /// <summary>Center as (x, z).</summary>
    public Vector2 Center => new((MinX + MaxX) * 0.5f, (MinZ + MaxZ) * 0.5f);

    /// <summary>Signed distance (meters) from (x, z) to the edge: positive inside, negative outside.</summary>
    public float Depth(float x, float z)
    {
        float dx = MathF.Max(MinX - x, x - MaxX), dz = MathF.Max(MinZ - z, z - MaxZ);
        if (dx <= 0f && dz <= 0f)
            return -MathF.Max(dx, dz);
        float ox = MathF.Max(dx, 0f), oz = MathF.Max(dz, 0f);
        return -MathF.Sqrt(ox * ox + oz * oz);
    }

    /// <summary>Whether (x, z) is inside, keeping <paramref name="margin"/> meters from the edge.</summary>
    public bool Contains(float x, float z, float margin = 0f) =>
        x >= MinX + margin && x <= MaxX - margin && z >= MinZ + margin && z <= MaxZ - margin;
}

/// <summary>
/// The world's layout: Mushroom Forest in the middle (the 200 m square on the origin), Mushroom
/// Town to the south (+Z), Pet City to the north (-Z), joined by two dirt roads through gaps in
/// the forest's mushroom wall, the darker Deep Forest to the west (-X) through a narrow overgrown
/// path in the middle of the forest's west edge, and the player's home interior, a room far away
/// under the world.
/// Only these are walkable; cliffs, hedges and giant mushrooms close off everything else.
/// </summary>
public static class Areas
{
    /// <summary>Half the side of the forest square (meters).</summary>
    public const float ForestHalfSize = ForestFloor.HalfSize;

    /// <summary>Half the width of a road (meters).</summary>
    public const float RoadHalfWidth = 3f;

    /// <summary>Half the width of each gap in the forest's ring of dense mushrooms (meters).</summary>
    public const float ForestGapHalfWidth = 5f;

    /// <summary>The middle of the forest's south road gap (x, z): toward Mushroom Town.</summary>
    public static readonly Vector2 ForestGapSouth = new(0f, ForestHalfSize);

    /// <summary>The middle of the forest's north road gap (x, z): toward Pet City.</summary>
    public static readonly Vector2 ForestGapNorth = new(0f, -ForestHalfSize);

    /// <summary>The middle of the forest's west gap (x, z): the overgrown path to the Deep Forest.</summary>
    public static readonly Vector2 ForestGapWest = new(-ForestHalfSize, 0f);

    /// <summary>Half the width of the west gap and of the overgrown path through it (meters).</summary>
    public const float DeepForestGapHalfWidth = 4f;

    /// <summary>Height (Y, meters) of the home interior's floor.</summary>
    public const float HomeFloorY = -50f;

    public static readonly AreaBounds Forest = new(-ForestHalfSize, -ForestHalfSize, ForestHalfSize, ForestHalfSize);
    public static readonly AreaBounds Town = new(-80f, 150f, 80f, 290f);
    public static readonly AreaBounds PetCity = new(-80f, -290f, 80f, -150f);

    /// <summary>The Deep Forest, west of Mushroom Forest beyond a 10 m strip of cliff.</summary>
    public static readonly AreaBounds DeepForest = new(-280f, -90f, -110f, 90f);

    /// <summary>
    /// The overgrown path through the west gap: from 4 m inside the forest to 2 m inside the Deep
    /// Forest, <see cref="DeepForestGapHalfWidth"/> to each side of z = 0. Part of the Deep Forest.
    /// </summary>
    public static readonly AreaBounds DeepForestPath = new(-112f, -DeepForestGapHalfWidth, -96f, DeepForestGapHalfWidth);

    /// <summary>The home interior room: 12 m (x) by 10 m (z) around (600, 600); its door is in the south (+Z) wall.</summary>
    public static readonly AreaBounds Home = new(594f, 595f, 606f, 605f);

    // Road center lines as (x, z) points; a NaN point separates the two roads. Each starts a few
    // meters inside the forest and ends a few meters inside its town.
    private static readonly Vector2[] s_roads =
    {
        // South: forest gap to Mushroom Town's north entrance.
        new(0f, 94f), new(0f, 104f), new(2.5f, 112f), new(4f, 120f), new(4f, 128f), new(2f, 136f),
        new(-1f, 143f), new(0f, 150f), new(0f, 156f),
        new(float.NaN, float.NaN),
        // North: forest gap to Pet City's south entrance.
        new(0f, -94f), new(0f, -104f), new(-2.5f, -112f), new(-4f, -120f), new(-4f, -128f), new(-2f, -136f),
        new(1f, -143f), new(0f, -150f), new(0f, -156f),
    };

    /// <summary>The road center lines as (x, z) points; a NaN point separates the two roads.</summary>
    public static ReadOnlySpan<Vector2> RoadPoints => s_roads;

    /// <summary>The bounds of an area; <see cref="Area.Road"/> has none (throws).</summary>
    public static AreaBounds Bounds(Area area) => area switch
    {
        Area.Forest => Forest,
        Area.Town => Town,
        Area.PetCity => PetCity,
        Area.Home => Home,
        Area.DeepForest => DeepForest,
        _ => throw new ArgumentOutOfRangeException(nameof(area), area, "roads have no rectangle; use RoadPoints"),
    };

    /// <summary>Horizontal distance (meters) from (x, z) to the nearest road's center line.</summary>
    public static float RoadDistance(float x, float z) => ForestFloor.PolylineDistance(s_roads, x, z);

    /// <summary>
    /// Signed distance (meters) from (x, z) to the edge of the walkable ground: positive inside
    /// (how far a walker may lean), negative outside (how far beyond the edge). Allocation-free.
    /// </summary>
    public static float Depth(float x, float z)
    {
        float d = Forest.Depth(x, z);
        if (d >= RoadHalfWidth)
            return d; // deeper in the forest than any road reaches
        d = MathF.Max(d, Town.Depth(x, z));
        d = MathF.Max(d, PetCity.Depth(x, z));
        d = MathF.Max(d, Home.Depth(x, z));
        d = MathF.Max(d, DeepForest.Depth(x, z));
        d = MathF.Max(d, DeepForestPath.Depth(x, z));
        return MathF.Max(d, RoadHalfWidth - RoadDistance(x, z));
    }

    /// <summary>Whether a walker standing at (x, z) keeps <paramref name="margin"/> meters inside the walkable ground. Obstacles are separate: see <see cref="Ground.Blocks"/>.</summary>
    public static bool Walkable(float x, float z, float margin = 0f) => Depth(x, z) >= margin;

    /// <summary>
    /// The area (x, z) belongs to: Home, Forest, Town, PetCity or DeepForest inside their rectangles
    /// (the overgrown path west of the forest square is DeepForest), Road on a road between them,
    /// and for a point that is not walkable the nearest of these.
    /// </summary>
    public static Area At(float x, float z)
    {
        if (Home.Depth(x, z) > -40f)
            return Area.Home;
        float forest = Forest.Depth(x, z);
        if (forest >= 0f)
            return Area.Forest;
        float deep = MathF.Max(DeepForest.Depth(x, z), DeepForestPath.Depth(x, z));
        if (deep >= 0f)
            return Area.DeepForest;
        float town = Town.Depth(x, z);
        if (town >= 0f)
            return Area.Town;
        float city = PetCity.Depth(x, z);
        if (city >= 0f)
            return Area.PetCity;
        float road = RoadHalfWidth - RoadDistance(x, z);
        if (road >= 0f)
            return Area.Road;
        Area best = Area.Forest;
        float depth = forest;
        if (town > depth) { best = Area.Town; depth = town; }
        if (city > depth) { best = Area.PetCity; depth = city; }
        if (deep > depth) { best = Area.DeepForest; depth = deep; }
        if (road > depth) best = Area.Road;
        return best;
    }
}
