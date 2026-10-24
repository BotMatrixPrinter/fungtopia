using System;
using System.Numerics;

namespace Fungtopia.Map;

/// <summary>A named spot: a point on the ground and the way it faces.</summary>
public readonly struct Place
{
    /// <summary>On the ground: <c>Position.Y == Ground.HeightAt(Position.X, Position.Z)</c>.</summary>
    public readonly Vector3 Position;

    /// <summary>Facing as a yaw (radians about +Y, Godot's rotation.y); 0 faces -Z (north).</summary>
    public readonly float Yaw;

    public Place(Vector3 position, float yaw)
    {
        Position = position;
        Yaw = yaw;
    }

    /// <summary>A place on the ground at (x, z) facing <paramref name="yaw"/>.</summary>
    public Place(float x, float z, float yaw) : this(new Vector3(x, Ground.HeightAt(x, z), z), yaw)
    {
    }

    /// <summary>The unit horizontal direction it faces.</summary>
    public Vector3 Facing => new(-MathF.Sin(Yaw), 0f, -MathF.Cos(Yaw));
}

/// <summary>
/// The world's named spots. Each is walkable (<see cref="Areas.Walkable"/> with 0.5 m to spare)
/// and clear of obstacles for a walker of 0.5 m radius.
/// </summary>
public static class Places
{
    /// <summary>How far in front of a building's front its door or counter spot is (meters from the footprint).</summary>
    public const float FrontGap = 1.6f;

    private const float North = 0f, South = MathF.PI, West = MathF.PI / 2f;

    /// <summary>Outside your house's door, facing out (where you appear when you leave the house).</summary>
    public static readonly Place HomeDoor = InFront(BuildingKind.PlayerHouse);

    /// <summary>Inside the home interior near its door, facing into the room (where you appear when you enter).</summary>
    public static readonly Place HomeInterior = new(600f, 601f, North);

    /// <summary>Just inside the home interior's door, facing it: walking here leaves the house.</summary>
    public static readonly Place HomeExit = new(600f, 604f, South);

    /// <summary>Outside Dr. Jeremy's clinic's door, facing out (where you wake up after fainting).</summary>
    public static readonly Place ClinicDoor = InFront(BuildingKind.Clinic);

    /// <summary>Where Millie stands, in front of her house next door to yours, facing the street.</summary>
    public static readonly Place MillieSpot = InFront(BuildingKind.MillieHouse);

    /// <summary>Where Lucy stands at her pet store's counter (its open front), facing the plaza.</summary>
    public static readonly Place LucySpot = InFront(BuildingKind.PetStore);

    /// <summary>In front of the furniture shop stall, facing out from it.</summary>
    public static readonly Place FurnitureShopSpot = InFront(BuildingKind.FurnitureShop);

    /// <summary>Where Abby stands at her clothing shop's door (its counter), facing the town square.</summary>
    public static readonly Place AbbySpot = InFront(BuildingKind.ClothingShop);

    /// <summary>In front of the food stall's counter ("Mushroom Munchies"), facing out from it towards the fountain.</summary>
    public static readonly Place FoodSpot = InFront(BuildingKind.FoodStall);

    /// <summary>The signpost at Mushroom Town's north entrance, beside the road, facing into town.</summary>
    public static readonly Place SignpostTown = new(4.5f, 156f, South);

    /// <summary>The signpost inside the forest's south gap, beside the road, facing into the forest.</summary>
    public static readonly Place SignpostForestSouth = new(4.5f, 95f, North);

    /// <summary>The signpost inside the forest's north gap, beside the road, facing into the forest.</summary>
    public static readonly Place SignpostForestNorth = new(-4.5f, -95f, South);

    /// <summary>The signpost at Pet City's south entrance, beside the road, facing into the city.</summary>
    public static readonly Place SignpostPetCity = new(-4.5f, -156f, North);

    /// <summary>The Deep Forest's entrance signpost, just inside the west gap beside the overgrown path, facing into the Deep Forest.</summary>
    public static readonly Place SignpostDeepForest = new(-112f, 5.5f, West);

    /// <summary>
    /// The signpost of an area (fast travel arrives there): Town, PetCity, DeepForest, and for
    /// Forest its south entrance (<see cref="SignpostForestSouth"/>). Road and Home have none (throws).
    /// </summary>
    public static Place Signpost(Area area) => area switch
    {
        Area.Town => SignpostTown,
        Area.Forest => SignpostForestSouth,
        Area.PetCity => SignpostPetCity,
        Area.DeepForest => SignpostDeepForest,
        _ => throw new ArgumentOutOfRangeException(nameof(area), area, "only Town, Forest, PetCity and DeepForest have signposts"),
    };

    private static Place InFront(BuildingKind kind)
    {
        var b = Buildings.First(kind);
        var p = b.Center + b.Front * (b.Depth * 0.5f + FrontGap);
        return new Place(p.X, p.Y, b.Rotation);
    }
}
