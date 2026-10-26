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
    /// <summary>A small clump of flowers (free ground decoration, walked over; not solid).</summary>
    Flowers,
    /// <summary>Abby's clothing shop (added after the props, so the earlier pieces keep their numbers).</summary>
    ClothingShop,
    /// <summary>A round planter full of flowers (the Map's <see cref="PropKind.Planter"/>).</summary>
    Planter,
    /// <summary>A wooden crate (the Map's <see cref="PropKind.Crate"/>).</summary>
    Crate,
    /// <summary>The "Mushroom Munchies" food stall on the square (the Map's <see cref="BuildingKind.FoodStall"/>).</summary>
    FoodStall,
    /// <summary>A small garden mushroom or fungus (free ground decoration, walked over); its <see cref="TownPlacement.Variant"/> is its <see cref="MushroomKind"/>.</summary>
    Mushroom,
}

/// <summary>
/// One thing drawn in Mushroom Town: at (<see cref="X"/>, <see cref="Z"/>) on the ground, turned
/// by <see cref="Yaw"/> (radians about +Y; at 0 its front faces -Z), sized
/// <see cref="Width"/> (local X) by <see cref="Depth"/> (local Z) by <see cref="Height"/>.
/// A building of the Map Contract keeps its index in <see cref="Buildings.All"/>, a solid prop its
/// index in <see cref="Props.All"/>; free decoration has neither. A garden mushroom leans by
/// <see cref="Tilt"/> (radians about its local X, after the yaw) and belongs to group
/// <see cref="Group"/> (a single, pair or cluster growing together; -1 for anything else).
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
    /// <summary>Index in <see cref="Buildings.All"/>, or -1.</summary>
    public readonly int BuildingIndex;
    /// <summary>Index in <see cref="Props.All"/>, or -1.</summary>
    public readonly int PropIndex;
    public readonly float Tilt;
    public readonly int Group;

    public TownPlacement(TownPiece piece, int variant, float x, float z, float yaw, float width, float depth, float height, int buildingIndex, int propIndex = -1, float tilt = 0f, int group = -1)
    {
        Tilt = tilt;
        Group = group;
        Piece = piece;
        Variant = variant;
        X = x;
        Z = z;
        Yaw = yaw;
        Width = width;
        Depth = depth;
        Height = height;
        BuildingIndex = buildingIndex;
        PropIndex = propIndex;
    }

    /// <summary>Radius of the circle on the ground it takes (half its longest side).</summary>
    public float Radius => MathF.Max(Width, Depth) * 0.5f;

    /// <summary>Whether it is free decoration: neither a Map building nor a Map prop.</summary>
    public bool IsDecoration => BuildingIndex < 0 && PropIndex < 0;
}

/// <summary>
/// Everything Town draws, as a plain list: each Town building of <see cref="Buildings.All"/> once,
/// exactly where the Map puts it, then each Town prop of <see cref="Props.All"/> (street lamps,
/// benches, fence segments, planters, crates) once, exactly where and as large as the Map says,
/// then small flower clumps (free decoration under 0.4 m, walked over) by the doors and round the
/// square. Fixed data, the same every run.
/// </summary>
public static class TownLayout
{
    /// <summary>Plain houses come in this many looks (cap colors, doors).</summary>
    public const int HouseVariants = 4;

    /// <summary>Flower clumps come in this many palettes (Flowers0..3); neighbors differ.</summary>
    public const int FlowerVariants = 4;

    /// <summary>Free decoration stays at most this tall and this wide (meters); anything bigger is a Map prop.</summary>
    public const float DecorationMaxHeight = 0.4f;
    public const float DecorationMaxWidth = 0.6f;

    /// <summary>Depth (local Z) a bench is drawn at; its length is the Map's 2 x radius.</summary>
    public const float BenchDepth = 0.62f;

    /// <summary>Depth (local Z) of a fence segment's pickets; its length is the Map's 2 x radius.</summary>
    public const float FenceDepth = 0.2f;

    /// <summary>Size (width, height, depth) of a flower clump.</summary>
    public static readonly Vector3 FlowersSize = new(0.5f, 0.35f, 0.5f);

    /// <summary>Meters a flower clump keeps from the middle of a dirt path (beyond its own radius).</summary>
    public const float PathClearance = 1.8f;

    /// <summary>The Map's named spots in or by town that flower clumps stay off.</summary>
    public static readonly Vector2[] Spots =
    {
        Flat(Places.HomeDoor), Flat(Places.ClinicDoor), Flat(Places.MillieSpot),
        Flat(Places.FurnitureShopSpot), Flat(Places.AbbySpot), Flat(Places.FoodSpot),
        Flat(Places.SignpostTown),
    };

    private static readonly float[] Sides = { -1f, 1f };

    // After the fields Build uses (static fields initialize in order).
    private static readonly TownPlacement[] s_all = Build();

    /// <summary>Every placement: the buildings first (in <see cref="Buildings.All"/> order), then the Map's props (in <see cref="Props.All"/> order), then the flower clumps.</summary>
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
            case BuildingKind.FoodStall: piece = TownPiece.FoodStall; return true;
            default: piece = default; return false;
        }
    }

    /// <summary>
    /// The piece drawing a Map prop of <paramref name="kind"/>, or false for a kind Town has no
    /// model for (forest mushrooms, the statue, one the Map adds later): Build skips it.
    /// </summary>
    public static bool TryPieceFor(PropKind kind, out TownPiece piece)
    {
        switch (kind)
        {
            case PropKind.Lamp: piece = TownPiece.StreetLamp; return true;
            case PropKind.Bench: piece = TownPiece.Bench; return true;
            case PropKind.FenceSegment: piece = TownPiece.Fence; return true;
            case PropKind.Planter: piece = TownPiece.Planter; return true;
            case PropKind.Crate: piece = TownPiece.Crate; return true;
            default: piece = default; return false;
        }
    }

    /// <summary>
    /// The placement drawing Map prop <paramref name="prop"/> (index <paramref name="index"/> in
    /// <see cref="Props.All"/>): at its spot and rotation, its footprint inside its obstacle circles
    /// and as long and tall as the Map says (a fence or bench 2 x radius along local X, a lamp or
    /// planter 2 x radius across, a square crate with its corners on its circle).
    /// </summary>
    public static TownPlacement ForProp(in Prop prop, TownPiece piece, int index)
    {
        float across = prop.Radius * 2f;
        (float width, float depth) = piece switch
        {
            TownPiece.Bench => (across, BenchDepth),
            TownPiece.Fence => (across, FenceDepth),
            TownPiece.Crate => (prop.Radius * MathF.Sqrt(2f), prop.Radius * MathF.Sqrt(2f)),
            _ => (across, across),
        };
        return new TownPlacement(piece, 0, prop.X, prop.Z, prop.Rotation, width, depth, prop.Height, -1, index);
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
    /// Whether a flower clump or mushroom of <paramref name="radius"/> at (x, z) is on walkable town ground,
    /// off every obstacle (buildings and props), the dirt paths and the named spots.
    /// </summary>
    public static bool FlowersFit(float x, float z, float radius)
    {
        if (Areas.At(x, z) != Area.Town || !Areas.Walkable(x, z, radius + 0.5f) || Ground.Blocks(x, z, radius + 0.2f))
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

    /// <summary>Whether a placement of <paramref name="piece"/> and <paramref name="variant"/> after index <paramref name="from"/> stands within <paramref name="distance"/> of (x, z).</summary>
    private static bool NearSame(List<TownPlacement> list, int from, TownPiece piece, int variant, float x, float z, float distance)
    {
        for (int k = from; k < list.Count; k++)
            if (list[k].Piece == piece && list[k].Variant == variant
                && Vector2.Distance(new Vector2(x, z), new Vector2(list[k].X, list[k].Z)) < distance)
                return true;
        return false;
    }

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

        // Every solid prop: exactly the Map's.
        var props = Props.All;
        for (int i = 0; i < props.Length; i++)
            if (props[i].Area == Area.Town && TryPieceFor(props[i].Kind, out var piece))
                list.Add(ForProp(props[i], piece, i));
        int decoration = list.Count;

        bool Free(float x, float z, float radius, int from)
        {
            if (!FlowersFit(x, z, radius))
                return false;
            for (int k = decoration; k < from; k++)
                if (Vector2.Distance(new Vector2(x, z), new Vector2(list[k].X, list[k].Z)) < radius + list[k].Radius)
                    return false;
            return true;
        }

        bool Add(float x, float z, float yaw)
        {
            float radius = MathF.Max(FlowersSize.X, FlowersSize.Z) * 0.5f;
            if (!Free(x, z, radius, list.Count))
                return false;
            // Its palette from the spot's seed, unlike the nearest clumps'.
            int variant = (int)(TownMushrooms.Seed(x, z) % FlowerVariants);
            for (int tries = 0; tries < FlowerVariants && NearSame(list, decoration, TownPiece.Flowers, variant, x, z, 1.6f); tries++)
                variant = (variant + 1) % FlowerVariants;
            list.Add(new TownPlacement(TownPiece.Flowers, variant, x, z, yaw, FlowersSize.X, FlowersSize.Z, FlowersSize.Y, -1));
            return true;
        }

        int groups = 0;
        // A group of garden mushrooms rooted at (x, z): its kind from the spot's seed, unlike any
        // group within NeighborDistance; all its members must fit, or none is placed.
        void AddMushrooms(float x, float z)
        {
            uint seed = TownMushrooms.Seed(x, z);
            int kind = (int)(seed % (uint)TownMushrooms.KindCount);
            int tries = 0;
            while (NearSame(list, decoration, TownPiece.Mushroom, kind, x, z, TownMushrooms.NeighborDistance + 2f * TownMushrooms.GroupRadius))
            {
                if (++tries == TownMushrooms.KindCount)
                    return;
                kind = (kind + TownMushrooms.KindStep) % TownMushrooms.KindCount;
            }
            var k = (MushroomKind)kind;
            int count = TownMushrooms.GroupSize(k, seed);
            int start = list.Count;
            for (int m = 0; m < count; m++)
            {
                var (mx, mz, yaw, tilt, width, height) = TownMushrooms.Member(k, seed, m, count, x, z);
                if (!Free(mx, mz, width * 0.5f, start))
                {
                    list.RemoveRange(start, list.Count - start);
                    return;
                }
                list.Add(new TownPlacement(TownPiece.Mushroom, kind, mx, mz, yaw, width, width, height, -1, -1, tilt, groups));
            }
            groups++;
        }

        // Flower clumps along each building's front, either side of the door.
        for (int i = 0; i < buildings; i++)
        {
            var p = list[i];
            if (p.Piece == TownPiece.Fountain)
                continue;
            var b = all[p.BuildingIndex];
            foreach (float side in Sides)
                for (int k = 0; k < 3; k++)
                {
                    var w = b.ToWorld(side * (b.Width * 0.18f + k * 0.75f), -b.Depth * 0.5f - 0.8f - (k % 2) * 0.35f);
                    Add(w.X, w.Y, k * 1.3f + side);
                }
        }

        // Flower clumps round the square.
        var square = Buildings.TownSquare;
        for (int a = 0; a < 40; a++)
        {
            float angle = a * MathF.Tau / 40f + 0.04f;
            float r = a % 2 == 0 ? 16.5f : 19.5f;
            Add(square.X + MathF.Sin(angle) * r, square.Y + MathF.Cos(angle) * r, angle * 3f);
        }

        // Garden mushrooms: round each building's walls (outside its footprint), along the fences,
        // by the lamps and planters, and in the gaps round the square.
        for (int i = 0; i < buildings; i++)
        {
            var p = list[i];
            float gap = p.Piece == TownPiece.Fountain ? 0.9f : 0.75f;
            int around = 6 + (int)((p.Width + p.Depth) * 0.6f);
            for (int a = 0; a < around; a++)
            {
                float angle = (a + 0.37f) * MathF.Tau / around;
                float c = MathF.Cos(angle), sn = MathF.Sin(angle), m = MathF.Max(MathF.Abs(c), MathF.Abs(sn));
                var b = all[p.BuildingIndex];
                var w = b.ToWorld(c / m * (p.Width * 0.5f + gap), sn / m * (p.Depth * 0.5f + gap));
                AddMushrooms(w.X, w.Y);
            }
        }
        for (int i = buildings; i < decoration; i++)
        {
            var p = list[i];
            var prop = props[p.PropIndex];
            if (p.Piece == TownPiece.Fence)
            {
                // One group each side, at different ends.
                foreach (float side in Sides)
                    AddMushrooms(p.X + prop.Along.X * side * p.Width * 0.3f + prop.Front.X * side * 0.75f,
                                 p.Z + prop.Along.Y * side * p.Width * 0.3f + prop.Front.Y * side * 0.75f);
            }
            else if (p.Piece is TownPiece.StreetLamp or TownPiece.Planter)
            {
                for (int a = 0; a < 2; a++)
                {
                    float angle = a * MathF.PI + p.Yaw + 0.5f;
                    AddMushrooms(p.X + MathF.Sin(angle) * (p.Radius + 0.7f), p.Z + MathF.Cos(angle) * (p.Radius + 0.7f));
                }
            }
        }
        for (int a = 0; a < 30; a++)
        {
            float angle = (a + 0.5f) * MathF.Tau / 30f + 0.04f;
            AddMushrooms(square.X + MathF.Sin(angle) * 18f, square.Y + MathF.Cos(angle) * 18f);
        }

        return list.ToArray();
    }
}
