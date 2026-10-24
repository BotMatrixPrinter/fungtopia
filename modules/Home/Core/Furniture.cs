using System;

namespace Fungtopia.Home;

/// <summary>A kind of furniture. <see cref="BasicBed"/> comes with the house; the others are sold at the furniture shop.</summary>
public enum FurnitureKind
{
    BasicBed,
    MushroomBed,
    Rug,
    Lamp,
    Table,
    Chair,
    PottedMushroom,
    Painting,
    PetBed,
    Bookshelf,
    ToadstoolStool,
}

/// <summary>The furniture catalog: names, prices in Mushroom Coins (M¢) and footprints on the floor grid.</summary>
public static class Furniture
{
    /// <summary>How many kinds there are.</summary>
    public const int KindCount = 11;

    private static readonly string[] Names =
    {
        "Basic Bed", "Mushroom Bed", "Rug", "Lamp", "Table", "Chair", "Potted Mushroom", "Painting", "Pet Bed",
        "Bookshelf", "Toadstool Stool",
    };

    private static readonly int[] Prices = { 0, 300, 120, 150, 200, 100, 80, 250, 180, 220, 90 };

    // Footprints in grid cells (0.5 m) before rotating: width along X, depth along Z.
    private static readonly int[] Widths = { 2, 3, 4, 1, 2, 1, 1, 1, 2, 3, 1 };
    private static readonly int[] Depths = { 4, 4, 3, 1, 2, 1, 1, 1, 2, 1, 1 };

    /// <summary>What the shop sells, in the order its catalog panel lists them.</summary>
    public static ReadOnlySpan<FurnitureKind> ForSale => s_forSale;

    private static readonly FurnitureKind[] s_forSale =
    {
        FurnitureKind.MushroomBed, FurnitureKind.Rug, FurnitureKind.Lamp, FurnitureKind.Table, FurnitureKind.Chair,
        FurnitureKind.PottedMushroom, FurnitureKind.Painting, FurnitureKind.PetBed, FurnitureKind.Bookshelf,
        FurnitureKind.ToadstoolStool,
    };

    public static string Name(FurnitureKind kind) => Names[(int)kind];

    /// <summary>Price in M¢ (0 for the basic bed, which is not sold).</summary>
    public static int Price(FurnitureKind kind) => Prices[(int)kind];

    /// <summary>Width along X in grid cells at rotation 0.</summary>
    public static int Width(FurnitureKind kind) => Widths[(int)kind];

    /// <summary>Depth along Z in grid cells at rotation 0.</summary>
    public static int Depth(FurnitureKind kind) => Depths[(int)kind];

    /// <summary>Width along X in grid cells at a rotation (quarter turns): odd turns swap width and depth.</summary>
    public static int Width(FurnitureKind kind, int rotation) => (rotation & 1) == 0 ? Width(kind) : Depth(kind);

    /// <summary>Depth along Z in grid cells at a rotation (quarter turns).</summary>
    public static int Depth(FurnitureKind kind, int rotation) => (rotation & 1) == 0 ? Depth(kind) : Width(kind);
}
