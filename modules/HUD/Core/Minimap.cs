using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Forest;
using Fungtopia.Map;

namespace Fungtopia.HUD;

/// <summary>What one cell (1 m square) of a <see cref="MinimapPicture"/> shows.</summary>
public enum MinimapCell : byte
{
    /// <summary>Not walkable: cliffs, hedges, the forest's mushroom wall (the dark background).</summary>
    Outside,
    /// <summary>Walkable grass: the forest, the town, the edges of the roads.</summary>
    Ground,
    /// <summary>Pet City's walkable paving.</summary>
    Paving,
    /// <summary>The Deep Forest's darker moss.</summary>
    Moss,
    /// <summary>A forest clearing: open walkable ground, a little lighter.</summary>
    Clearing,
    /// <summary>A road, a forest or town path, Pet City's avenue or the overgrown Deep Forest path (dirt).</summary>
    Path,
}

/// <summary>What a little marker on the minimap is.</summary>
public enum MinimapMarkKind : byte
{
    /// <summary>A signpost (<see cref="Places.Signpost"/>, the forest's north one too).</summary>
    Signpost,
    /// <summary>An NPC spot or a door (<see cref="Places"/>: Millie, Lucy, Abby, the clinic's door...).</summary>
    Spot,
}

/// <summary>A marker at a world point (x, z).</summary>
public readonly struct MinimapMark
{
    public readonly MinimapMarkKind Kind;
    public readonly float X;
    public readonly float Z;

    public MinimapMark(MinimapMarkKind kind, float x, float z)
    {
        Kind = kind;
        X = x;
        Z = z;
    }
}

/// <summary>A building's footprint on the minimap: its kind and the four world corners (x, z) of <see cref="Building"/>.</summary>
public readonly struct MinimapBuilding
{
    public readonly BuildingKind Kind;
    public readonly Vector2 Center;
    public readonly Vector2 A, B, C, D;

    public MinimapBuilding(in Building b)
    {
        Kind = b.Kind;
        Center = b.Center;
        float w = b.Width * 0.5f, d = b.Depth * 0.5f;
        A = b.ToWorld(-w, -d);
        B = b.ToWorld(w, -d);
        C = b.ToWorld(w, d);
        D = b.ToWorld(-w, d);
    }

    /// <summary>Corner <paramref name="index"/> (0..3, around the footprint).</summary>
    public Vector2 Corner(int index) => index switch { 0 => A, 1 => B, 2 => C, _ => D };

    /// <summary>The fountain is round (width = depth = its diameter).</summary>
    public bool Round => Kind == BuildingKind.Fountain;
}

/// <summary>
/// A zone's minimap as plain data, built once (<see cref="For"/> caches it): a grid of 1 m cells
/// sampled from <see cref="Areas.Walkable"/> and the path distances, north (-Z) up, the cell at
/// (0, 0) at <see cref="Bounds"/>' north-west corner (MinX, MinZ); the buildings' footprints, the
/// signposts and the NPC spots in it. The view turns the cells into a texture and draws the rest.
/// The Road's picture covers the strip from Pet City to Mushroom Town (x -80..80, z -290..290); the
/// minimap shows a <see cref="HudModule.RoadWindowMeters"/> window of it around the player.
/// </summary>
public sealed class MinimapPicture
{
    /// <summary>Meters per cell.</summary>
    public const float CellSize = 1f;

    /// <summary>What the Road's picture covers: both roads, the zone edges they join and the towns.</summary>
    public static readonly AreaBounds RoadBounds = new(-80f, -290f, 80f, 290f);

    private static readonly MinimapPicture?[] s_cache = new MinimapPicture?[6];

    public Area Area { get; }
    public AreaBounds Bounds { get; }
    public int Width { get; }
    public int Height { get; }

    /// <summary>Row-major cells, <see cref="Width"/> per row, row 0 the northmost.</summary>
    public MinimapCell[] Cells { get; }

    public MinimapBuilding[] Buildings { get; }
    public MinimapMark[] Marks { get; }

    private MinimapPicture(Area area, AreaBounds bounds)
    {
        Area = area;
        Bounds = bounds;
        Width = (int)MathF.Ceiling((bounds.MaxX - bounds.MinX) / CellSize);
        Height = (int)MathF.Ceiling((bounds.MaxZ - bounds.MinZ) / CellSize);
        Cells = new MinimapCell[Width * Height];
        for (int row = 0; row < Height; row++)
        {
            float z = bounds.MinZ + (row + 0.5f) * CellSize;
            for (int col = 0; col < Width; col++)
                Cells[row * Width + col] = Sample(bounds.MinX + (col + 0.5f) * CellSize, z);
        }

        bool road = area == Area.Road;
        var buildings = new List<MinimapBuilding>();
        foreach (var b in Fungtopia.Map.Buildings.All)
            if (road ? bounds.Contains(b.X, b.Z) : b.Area == area)
                buildings.Add(new MinimapBuilding(b));
        Buildings = buildings.ToArray();

        var marks = new List<MinimapMark>();
        void Add(MinimapMarkKind kind, in Place place)
        {
            float x = place.Position.X, z = place.Position.Z;
            if (road ? bounds.Contains(x, z) : Areas.At(x, z) == area)
                marks.Add(new MinimapMark(kind, x, z));
        }
        Add(MinimapMarkKind.Signpost, Places.SignpostTown);
        Add(MinimapMarkKind.Signpost, Places.SignpostForestSouth);
        Add(MinimapMarkKind.Signpost, Places.SignpostForestNorth);
        Add(MinimapMarkKind.Signpost, Places.SignpostPetCity);
        Add(MinimapMarkKind.Signpost, Places.SignpostDeepForest);
        Add(MinimapMarkKind.Spot, Places.HomeDoor);
        Add(MinimapMarkKind.Spot, Places.ClinicDoor);
        Add(MinimapMarkKind.Spot, Places.MillieSpot);
        Add(MinimapMarkKind.Spot, Places.FurnitureShopSpot);
        Add(MinimapMarkKind.Spot, Places.LucySpot);
        Add(MinimapMarkKind.Spot, Places.AbbySpot);
        Add(MinimapMarkKind.Spot, Places.FoodSpot);
        Marks = marks.ToArray();
    }

    /// <summary>
    /// The picture of <paramref name="area"/> (Town, Forest, PetCity, DeepForest, or Road for the
    /// strip the roads run through), built on the first call and the same object after; null for Home.
    /// </summary>
    public static MinimapPicture? For(Area area)
    {
        int i = (int)area;
        if (area == Area.Home || i < 0 || i >= s_cache.Length)
            return null;
        return s_cache[i] ??= new MinimapPicture(area, area == Area.Road ? RoadBounds : Areas.Bounds(area));
    }

    /// <summary>What the map shows at world (x, z), as the Map says.</summary>
    public static MinimapCell Sample(float x, float z)
    {
        if (!Areas.Walkable(x, z))
            return MinimapCell.Outside;
        if (Areas.RoadDistance(x, z) <= Areas.RoadHalfWidth || Areas.DeepForestPath.Contains(x, z))
            return MinimapCell.Path;
        if (Areas.Town.Contains(x, z))
            return Areas.TownPathDistance(x, z) <= Areas.TownPathHalfWidth ? MinimapCell.Path : MinimapCell.Ground;
        if (Areas.PetCity.Contains(x, z))
            return Props.CityAvenueDistance(x, z) <= Props.CityAvenueHalfWidth ? MinimapCell.Path : MinimapCell.Paving;
        if (Areas.DeepForest.Contains(x, z))
            return MinimapCell.Moss;
        if (Areas.Forest.Contains(x, z))
        {
            if (ForestLayout.OnPath(x, z))
                return MinimapCell.Path;
            if (ForestLayout.ClearingDepth(x, z) >= 0f)
                return MinimapCell.Clearing;
        }
        return MinimapCell.Ground;
    }

    /// <summary>The cell at world (x, z); <see cref="MinimapCell.Outside"/> beyond the picture.</summary>
    public MinimapCell CellAt(float x, float z)
    {
        int col = (int)MathF.Floor((x - Bounds.MinX) / CellSize), row = (int)MathF.Floor((z - Bounds.MinZ) / CellSize);
        if (col < 0 || row < 0 || col >= Width || row >= Height)
            return MinimapCell.Outside;
        return Cells[row * Width + col];
    }

    /// <summary>
    /// World (x, z) on the picture, 0..1 each way: (0, 0) its top-left (north-west) corner,
    /// (1, 1) its bottom-right (south-east); north (-Z) up, east (+X) right.
    /// </summary>
    public Vector2 ToPicture(float x, float z) => MinimapMath.ToWindow(Bounds, x, z);
}

/// <summary>Pure helpers mapping world points and facings onto the minimap.</summary>
public static class MinimapMath
{
    /// <summary>World (x, z) within <paramref name="window"/>: (0, 0) its north-west corner, (1, 1) its south-east.</summary>
    public static Vector2 ToWindow(in AreaBounds window, float x, float z) =>
        new((x - window.MinX) / (window.MaxX - window.MinX), (z - window.MinZ) / (window.MaxZ - window.MinZ));

    /// <summary>
    /// The arrow's angle on the minimap for a facing (<see cref="Fungtopia.Player.PlayerState.Facing"/>,
    /// yaw 0 north, positive turning left): radians clockwise from straight up (a Godot control's
    /// rotation), in -π..π. North points up (0), west left (-π/2), east right (π/2).
    /// </summary>
    public static float ArrowAngle(float facing) => Wrap(-facing);

    /// <summary>An angle wrapped into -π..π.</summary>
    public static float Wrap(float angle)
    {
        if (float.IsNaN(angle) || float.IsInfinity(angle))
            return 0f;
        angle %= 2f * MathF.PI;
        if (angle > MathF.PI)
            angle -= 2f * MathF.PI;
        else if (angle < -MathF.PI)
            angle += 2f * MathF.PI;
        return angle;
    }
}
