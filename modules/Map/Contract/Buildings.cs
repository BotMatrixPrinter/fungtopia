using System;
using System.Numerics;

namespace Fungtopia.Map;

/// <summary>What a building is, so the Town and PetCity modules know what to draw.</summary>
public enum BuildingKind
{
    /// <summary>Your house: a big mushroom house; its door is <see cref="Places.HomeDoor"/>.</summary>
    PlayerHouse,
    /// <summary>Your best friend Millie's mushroom house, next door to yours.</summary>
    MillieHouse,
    /// <summary>Dr. Jeremy's clinic: a white mushroom house with a red cross.</summary>
    Clinic,
    /// <summary>The furniture shop's market stall.</summary>
    FurnitureShop,
    /// <summary>Another cute mushroom house in town.</summary>
    House,
    /// <summary>The fountain in the town square (round: width = depth = its diameter).</summary>
    Fountain,
    /// <summary>Lucy's Pet Store in Pet City; its counter is at its open front (<see cref="Places.LucySpot"/>).</summary>
    PetStore,
    /// <summary>A tall, colorful cartoon city block with windows, in Pet City.</summary>
    CityBlock,
    /// <summary>Abby's clothing shop: a boutique mushroom house on the town square's south side; she stands at its door (<see cref="Places.AbbySpot"/>).</summary>
    ClothingShop,
    /// <summary>"Mushroom Munchies": a small open food stall on the town square, its counter facing the fountain (<see cref="Places.FoodSpot"/>).</summary>
    FoodStall,
}

/// <summary>
/// A building's footprint: a rectangle of <see cref="Width"/> (along its local X) by
/// <see cref="Depth"/> (along its local Z) centered on (<see cref="X"/>, <see cref="Z"/>), turned
/// by <see cref="Rotation"/> (yaw in radians about +Y, Godot's rotation.y). At rotation 0 its front
/// (door, counter) faces -Z (north). <see cref="Ground.Obstacles"/> covers it with circles.
/// </summary>
public readonly struct Building
{
    public readonly BuildingKind Kind;
    public readonly Area Area;
    public readonly float X;
    public readonly float Z;
    public readonly float Rotation;
    public readonly float Width;
    public readonly float Depth;
    /// <summary>Suggested height (meters) to draw it; not used for walking.</summary>
    public readonly float Height;

    public Building(BuildingKind kind, Area area, float x, float z, float rotation, float width, float depth, float height)
    {
        Kind = kind;
        Area = area;
        X = x;
        Z = z;
        Rotation = rotation;
        Width = width;
        Depth = depth;
        Height = height;
    }

    /// <summary>Center as (x, z).</summary>
    public Vector2 Center => new(X, Z);

    /// <summary>The unit direction (x, z) its front faces.</summary>
    public Vector2 Front => new(-MathF.Sin(Rotation), -MathF.Cos(Rotation));

    /// <summary>The world (x, z) of a point given in the building's own frame (local x right, local z back).</summary>
    public Vector2 ToWorld(float localX, float localZ)
    {
        float c = MathF.Cos(Rotation), s = MathF.Sin(Rotation);
        return new Vector2(X + localX * c + localZ * s, Z - localX * s + localZ * c);
    }

    /// <summary>Whether world (x, z) is on the footprint, grown by <paramref name="margin"/> meters.</summary>
    public bool Contains(float x, float z, float margin = 0f)
    {
        float c = MathF.Cos(Rotation), s = MathF.Sin(Rotation), dx = x - X, dz = z - Z;
        float lx = dx * c - dz * s, lz = dx * s + dz * c;
        return MathF.Abs(lx) <= Width * 0.5f + margin && MathF.Abs(lz) <= Depth * 0.5f + margin;
    }
}

/// <summary>Every building (and the fountain) of Mushroom Town and Pet City. Fixed data.</summary>
public static class Buildings
{
    private const float FaceNorth = 0f;
    private const float FaceSouth = MathF.PI;
    private const float FaceEast = -MathF.PI / 2f;
    private const float FaceWest = MathF.PI / 2f;

    /// <summary>The town square's center (x, z); the fountain stands on it.</summary>
    public static readonly Vector2 TownSquare = new(0f, 215f);

    /// <summary>Pet City's plaza center (x, z).</summary>
    public static readonly Vector2 CityPlaza = new(0f, -215f);

    // Declared before s_all, which uses it (static fields initialize in order).
    /// <summary>The food stall's yaw: its front faces the fountain from (-5.2, 205.3).</summary>
    private static readonly float FoodStallYaw = MathF.Atan2(-5.2f, -9.7f);

    private static readonly Building[] s_all =
    {
        // Mushroom Town: around the square, the road comes in from the north (z = 150).
        new(BuildingKind.PlayerHouse, Area.Town, -24f, 212f, FaceEast, 12f, 12f, 10f),
        new(BuildingKind.MillieHouse, Area.Town, -23f, 236f, FaceEast, 10f, 10f, 8f),
        new(BuildingKind.Clinic, Area.Town, 24f, 208f, FaceWest, 12f, 10f, 8.5f),
        new(BuildingKind.FurnitureShop, Area.Town, 21f, 236f, FaceWest, 7f, 4f, 3.5f),
        new(BuildingKind.Fountain, Area.Town, 0f, 215f, 0f, 7f, 7f, 2.2f),
        new(BuildingKind.House, Area.Town, -50f, 176f, FaceSouth, 9f, 9f, 7f),
        new(BuildingKind.House, Area.Town, 50f, 176f, FaceSouth, 9f, 9f, 6.5f),
        new(BuildingKind.House, Area.Town, -48f, 266f, FaceNorth, 9f, 9f, 7.5f),
        new(BuildingKind.House, Area.Town, 48f, 266f, FaceNorth, 9f, 9f, 6.5f),

        // Pet City: tall blocks along both sides of the plaza, the pet store at its north end;
        // the road comes in from the south (z = -150).
        new(BuildingKind.PetStore, Area.PetCity, 0f, -262f, FaceSouth, 18f, 12f, 11f),
        new(BuildingKind.CityBlock, Area.PetCity, -46f, -172f, FaceEast, 12f, 12f, 22f),
        new(BuildingKind.CityBlock, Area.PetCity, -46f, -200f, FaceEast, 12f, 12f, 30f),
        new(BuildingKind.CityBlock, Area.PetCity, -46f, -228f, FaceEast, 12f, 12f, 18f),
        new(BuildingKind.CityBlock, Area.PetCity, -46f, -256f, FaceEast, 12f, 12f, 26f),
        new(BuildingKind.CityBlock, Area.PetCity, 46f, -172f, FaceWest, 12f, 12f, 26f),
        new(BuildingKind.CityBlock, Area.PetCity, 46f, -200f, FaceWest, 12f, 12f, 19f),
        new(BuildingKind.CityBlock, Area.PetCity, 46f, -228f, FaceWest, 12f, 12f, 32f),
        new(BuildingKind.CityBlock, Area.PetCity, 46f, -256f, FaceWest, 12f, 12f, 23f),
        new(BuildingKind.CityBlock, Area.PetCity, -24f, -276f, FaceSouth, 14f, 10f, 20f),
        new(BuildingKind.CityBlock, Area.PetCity, 24f, -276f, FaceSouth, 14f, 10f, 24f),

        // Mushroom Town again: Abby's clothing shop, south across the square from the road, its door facing the fountain.
        new(BuildingKind.ClothingShop, Area.Town, 0f, 246f, FaceNorth, 10f, 9f, 8f),

        // Mushroom Town again: the food stall on the square's north-west side, between the street and
        // the path to the north-west house, its counter facing the fountain.
        new(BuildingKind.FoodStall, Area.Town, -5.2f, 205.3f, FoodStallYaw, 4.2f, 2.4f, 3f),
    };

    /// <summary>Every building, in a fixed order.</summary>
    public static ReadOnlySpan<Building> All => s_all;

    /// <summary>Number of buildings.</summary>
    public static int Count => s_all.Length;

    /// <summary>The first building of <paramref name="kind"/> (each named kind exists once).</summary>
    public static Building First(BuildingKind kind)
    {
        foreach (var b in s_all)
            if (b.Kind == kind)
                return b;
        throw new ArgumentOutOfRangeException(nameof(kind), kind, "no such building");
    }
}
