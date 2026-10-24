using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Map;

namespace Fungtopia.Town;

/// <summary>What a placement draws: one object of <c>Models/town.glb</c> (a House by its variant).</summary>
public enum TownPiece
{
    PlayerHouse,
    MillieHouse,
    Clinic,
    FurnitureShop,
    Fountain,
    House,
    StreetLamp,
    Bench,
    Fence,
    FlowerBed,
    /// <summary>Abby's clothing shop (added after the props, so the earlier pieces keep their numbers).</summary>
    ClothingShop,
}

/// <summary>
/// One thing drawn in Mushroom Town: at (<see cref="X"/>, <see cref="Z"/>) on the ground, turned
/// by <see cref="Yaw"/> (radians about +Y; at 0 its front faces -Z), sized
/// <see cref="Width"/> (local X) by <see cref="Depth"/> (local Z) by <see cref="Height"/>.
/// A building of the Map Contract keeps its index in <see cref="Buildings.All"/>.
/// </summary>
public readonly struct TownPlacement
{
    public readonly TownPiece Piece;
    public readonly int Variant;
    public readonly float X;
    public readonly float Z;
    public readonly float Yaw;
    public readonly float Width;
    public readonly float Depth;
    public readonly float Height;
    /// <summary>Index in <see cref="Buildings.All"/>, or -1 for a prop (lamp, bench, fence, flower bed).</summary>
    public readonly int BuildingIndex;

    public TownPlacement(TownPiece piece, int variant, float x, float z, float yaw, float width, float depth, float height, int buildingIndex)
    {
        Piece = piece;
        Variant = variant;
        X = x;
        Z = z;
        Yaw = yaw;
        Width = width;
        Depth = depth;
        Height = height;
        BuildingIndex = buildingIndex;
    }

    /// <summary>Radius of the circle on the ground a prop takes (half its longest side).</summary>
    public float Radius => MathF.Max(Width, Depth) * 0.5f;
}

/// <summary>
/// Everything Town draws, as a plain list: each Town building of <see cref="Buildings.All"/> once,
/// exactly where the Map puts it, then the props (street lamps, benches, fences, flower beds),
/// all on walkable ground, off the buildings, the town's dirt paths and the Map's named spots.
/// Fixed data, the same every run.
/// </summary>
public static class TownLayout
{
    /// <summary>Plain houses come in this many looks (cap colors, doors).</summary>
    public const int HouseVariants = 4;

    /// <summary>Meters a prop keeps between its edge and the middle of a dirt path.</summary>
    public const float PathClearance = 1.8f;

    /// <summary>Meters a prop keeps between its edge and a building's footprint or a named spot.</summary>
    public const float Clearance = 0.6f;

    public static readonly Vector3 LampSize = new(0.8f, 3.2f, 0.8f);
    public static readonly Vector3 BenchSize = new(1.8f, 0.9f, 0.7f);
    public static readonly Vector3 FenceSize = new(2f, 0.9f, 0.2f);
    public static readonly Vector3 FlowerBedSize = new(2f, 0.5f, 0.8f);

    /// <summary>The Map's named spots in or by town that props stay off.</summary>
    public static readonly Vector2[] Spots =
    {
        Flat(Places.HomeDoor), Flat(Places.ClinicDoor), Flat(Places.MillieSpot),
        Flat(Places.FurnitureShopSpot), Flat(Places.AbbySpot), Flat(Places.SignpostTown),
    };

    private static readonly float[] Sides = { -1f, 1f };

    // After the fields Build uses (static fields initialize in order).
    private static readonly TownPlacement[] s_all = Build();

    /// <summary>Every placement: the buildings first (in <see cref="Buildings.All"/> order), then the props.</summary>
    public static ReadOnlySpan<TownPlacement> All => s_all;

    /// <summary>Number of placements.</summary>
    public static int Count => s_all.Length;

    /// <summary>
    /// The piece drawing a Map building of <paramref name="kind"/>, or false for a kind Town does
    /// not draw (PetCity's, or one added to the Map later): Build skips it.
    /// </summary>
    public static bool TryPieceFor(BuildingKind kind, out TownPiece piece)
    {
        switch (kind)
        {
            case BuildingKind.PlayerHouse: piece = TownPiece.PlayerHouse; return true;
            case BuildingKind.MillieHouse: piece = TownPiece.MillieHouse; return true;
            case BuildingKind.Clinic: piece = TownPiece.Clinic; return true;
            case BuildingKind.FurnitureShop: piece = TownPiece.FurnitureShop; return true;
            case BuildingKind.Fountain: piece = TownPiece.Fountain; return true;
            case BuildingKind.House: piece = TownPiece.House; return true;
            case BuildingKind.ClothingShop: piece = TownPiece.ClothingShop; return true;
            default: piece = default; return false;
        }
    }

    /// <summary>
    /// Distance from (x, z) to the middle of the town's dirt paths, as the Map draws them: the
    /// street from the entrance to the square, and one from the square to 1 m in front of each
    /// building.
    /// </summary>
    public static float PathDistance(float x, float z)
    {
        var square = Buildings.TownSquare;
        float best = Segment(x, z, 0f, Areas.Town.MinZ, square.X, square.Y);
        foreach (var b in Buildings.All)
        {
            if (b.Area != Area.Town || b.Kind == BuildingKind.Fountain)
                continue;
            var front = b.Center + b.Front * (b.Depth * 0.5f + 1f);
            best = MathF.Min(best, Segment(x, z, square.X, square.Y, front.X, front.Y));
        }
        return best;
    }

    /// <summary>
    /// Whether a prop of <paramref name="radius"/> at (x, z) is on walkable town ground, clear of
    /// the Map's obstacles and buildings, the dirt paths and the named spots (doors, NPC spots,
    /// the signpost).
    /// </summary>
    public static bool PropFits(float x, float z, float radius)
    {
        if (Areas.At(x, z) != Area.Town || !Areas.Walkable(x, z, radius + Clearance) || Ground.Blocks(x, z))
            return false;
        foreach (var b in Buildings.All)
            if (b.Contains(x, z, radius + Clearance))
                return false;
        if (PathDistance(x, z) < radius + PathClearance)
            return false;
        foreach (var spot in Spots)
            if (Vector2.Distance(new Vector2(x, z), spot) < radius + 1.2f)
                return false;
        return true;
    }

    private static Vector2 Flat(Place place) => new(place.Position.X, place.Position.Z);

    private static float Segment(float x, float z, float ax, float az, float bx, float bz)
    {
        float abx = bx - ax, abz = bz - az;
        float t = Math.Clamp(((x - ax) * abx + (z - az) * abz) / (abx * abx + abz * abz), 0f, 1f);
        float dx = x - (ax + abx * t), dz = z - (az + abz * t);
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>Yaw whose front (-Z at 0) faces the direction (dx, dz).</summary>
    private static float Facing(float dx, float dz) => MathF.Atan2(-dx, -dz);

    private static TownPlacement[] Build()
    {
        var list = new List<TownPlacement>();
        var all = Buildings.All;
        int houses = 0;
        for (int i = 0; i < all.Length; i++)
        {
            var b = all[i];
            if (b.Area != Area.Town || !TryPieceFor(b.Kind, out var piece))
                continue;
            int variant = b.Kind == BuildingKind.House ? houses++ % HouseVariants : 0;
            list.Add(new TownPlacement(piece, variant, b.X, b.Z, b.Rotation, b.Width, b.Depth, b.Height, i));
        }
        int buildings = list.Count;

        bool Add(TownPiece piece, Vector3 size, float x, float z, float yaw)
        {
            float radius = MathF.Max(size.X, size.Z) * 0.5f;
            if (!PropFits(x, z, radius))
                return false;
            for (int k = buildings; k < list.Count; k++)
            {
                var other = list[k];
                if (Vector2.Distance(new Vector2(x, z), new Vector2(other.X, other.Z)) < radius + other.Radius - 0.01f)
                    return false;
            }
            list.Add(new TownPlacement(piece, 0, x, z, yaw, size.X, size.Z, size.Y, -1));
            return true;
        }

        var square = Buildings.TownSquare;

        // Street lamps in a ring just outside the square's paving, between the paths.
        for (int a = 0; a < 48; a++)
        {
            float angle = a * MathF.Tau / 48f;
            float x = square.X + MathF.Sin(angle) * 14f, z = square.Y + MathF.Cos(angle) * 14f;
            if (NearestOfKind(list, buildings, TownPiece.StreetLamp, x, z) >= 9f)
                Add(TownPiece.StreetLamp, LampSize, x, z, 0f);
        }

        // Benches on the paving, facing the fountain.
        for (int a = 0; a < 36; a++)
        {
            float angle = a * MathF.Tau / 36f + 0.09f;
            float dx = MathF.Sin(angle), dz = MathF.Cos(angle);
            float x = square.X + dx * 9.5f, z = square.Y + dz * 9.5f;
            if (NearestOfKind(list, buildings, TownPiece.Bench, x, z) >= 10f)
                Add(TownPiece.Bench, BenchSize, x, z, Facing(-dx, -dz));
        }

        // At each building: a lamp at both front corners, flower beds along its front either
        // side of the door.
        for (int i = 0; i < buildings; i++)
        {
            var p = list[i];
            if (p.Piece == TownPiece.Fountain)
                continue;
            var b = all[p.BuildingIndex];
            float front = -b.Depth * 0.5f;
            foreach (float side in Sides)
            {
                var lamp = b.ToWorld(side * (b.Width * 0.5f + 1.4f), front - 1.4f);
                Add(TownPiece.StreetLamp, LampSize, lamp.X, lamp.Y, 0f);
                var bed = b.ToWorld(side * b.Width * 0.3f, front - Clearance - FlowerBedSize.X * 0.5f - 0.05f);
                if (!Add(TownPiece.FlowerBed, FlowerBedSize, bed.X, bed.Y, b.Rotation))
                {
                    bed = b.ToWorld(side * (b.Width * 0.5f - 1f), front - Clearance - FlowerBedSize.X * 0.5f - 0.05f);
                    Add(TownPiece.FlowerBed, FlowerBedSize, bed.X, bed.Y, b.Rotation);
                }
            }
        }

        // Picket fences round the back and sides of each plain house's yard.
        for (int i = 0; i < buildings; i++)
        {
            var p = list[i];
            if (p.Piece != TownPiece.House)
                continue;
            var b = all[p.BuildingIndex];
            float hx = b.Width * 0.5f + 3f, front = -b.Depth * 0.5f - 1f, back = b.Depth * 0.5f + 3f;
            FenceLine(b, -hx, front, -hx, back);
            FenceLine(b, -hx, back, hx, back);
            FenceLine(b, hx, back, hx, front);
        }

        // A fence behind your house and Millie's (one shared back garden), and either side of the
        // street where it comes into town.
        FenceLine(default, -35.5f, 199f, -35.5f, 249f);
        FenceLine(default, 7f, 161f, 35f, 161f);
        FenceLine(default, -7f, 161f, -35f, 161f);

        // Flower beds round the square, between its lamps.
        for (int a = 0; a < 48; a++)
        {
            float angle = a * MathF.Tau / 48f + 0.065f;
            float dx = MathF.Sin(angle), dz = MathF.Cos(angle);
            float x = square.X + dx * 16.5f, z = square.Y + dz * 16.5f;
            if (NearestOfKind(list, buildings, TownPiece.FlowerBed, x, z) >= 8f)
                Add(TownPiece.FlowerBed, FlowerBedSize, x, z, Facing(-dx, -dz));
        }

        return list.ToArray();

        // Fence segments end to end from local (x0, z0) to (x1, z1) of building b (world when b is default).
        void FenceLine(Building b, float x0, float z0, float x1, float z1)
        {
            float length = MathF.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0));
            int count = (int)(length / FenceSize.X);
            for (int k = 0; k < count; k++)
            {
                float t = (k + 0.5f) / count;
                float lx = x0 + (x1 - x0) * t, lz = z0 + (z1 - z0) * t;
                Vector2 w = b.Width > 0f ? b.ToWorld(lx, lz) : new Vector2(lx, lz);
                Vector2 d = b.Width > 0f ? b.ToWorld(x1, z1) - b.ToWorld(x0, z0) : new Vector2(x1 - x0, z1 - z0);
                Add(TownPiece.Fence, FenceSize, w.X, w.Y, MathF.Atan2(-d.Y, d.X));
            }
        }
    }

    private static float NearestOfKind(List<TownPlacement> list, int start, TownPiece piece, float x, float z)
    {
        float best = float.MaxValue;
        for (int k = start; k < list.Count; k++)
            if (list[k].Piece == piece)
                best = MathF.Min(best, Vector2.Distance(new Vector2(x, z), new Vector2(list[k].X, list[k].Z)));
        return best;
    }
}
