using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fungtopia.Map;

/// <summary>What a solid prop is, so the module drawing its area knows what to draw. New kinds are added at the end.</summary>
public enum PropKind
{
    /// <summary>A cluster of medium mushrooms (0.8-2.5 m tall) in the Mushroom Forest or the Deep Forest.</summary>
    MediumMushroom,
    /// <summary>A street lamp (a tall glowing mushroom lamp) by a road, a street or a square.</summary>
    Lamp,
    /// <summary>A bench, its seat along local X (<see cref="Prop.Radius"/> is half its length), facing local -Z.</summary>
    Bench,
    /// <summary>A straight piece of fence along local X (<see cref="Prop.Radius"/> is half its length); a row of small circles.</summary>
    FenceSegment,
    /// <summary>A big flower box or planter.</summary>
    Planter,
    /// <summary>A wooden crate.</summary>
    Crate,
    /// <summary>A statue on a pedestal.</summary>
    Statue,
}

/// <summary>
/// A solid prop standing on the ground: drawn exactly at (<see cref="X"/>, <see cref="Z"/>),
/// turned by <see cref="Rotation"/> (yaw, radians about +Y; at 0 its front faces -Z), about
/// 2 x <see cref="Radius"/> across and <see cref="Height"/> tall. <see cref="Ground.Obstacles"/>
/// holds its circles (<see cref="Circle(int)"/>): one of <see cref="Radius"/> for most kinds, a
/// row of small ones along local X for a <see cref="PropKind.FenceSegment"/>.
/// </summary>
public readonly struct Prop
{
    public readonly PropKind Kind;
    public readonly Area Area;
    public readonly float X;
    public readonly float Z;
    public readonly float Rotation;
    public readonly float Radius;
    public readonly float Height;

    public Prop(PropKind kind, Area area, float x, float z, float rotation, float radius, float height)
    {
        Kind = kind;
        Area = area;
        X = x;
        Z = z;
        Rotation = rotation;
        Radius = radius;
        Height = height;
    }

    /// <summary>Center as (x, z).</summary>
    public Vector2 Center => new(X, Z);

    /// <summary>The unit direction (x, z) its front faces.</summary>
    public Vector2 Front => new(-MathF.Sin(Rotation), -MathF.Cos(Rotation));

    /// <summary>The unit direction (x, z) of its local X: the length of a bench or fence segment.</summary>
    public Vector2 Along => new(MathF.Cos(Rotation), -MathF.Sin(Rotation));

    /// <summary>How many obstacle circles it has.</summary>
    public int CircleCount => Kind == PropKind.FenceSegment
        ? (int)MathF.Ceiling(2f * (Radius - Props.FencePostRadius) / Props.FenceCircleSpacing) + 1
        : 1;

    /// <summary>Its <paramref name="index"/>th obstacle circle (0 .. <see cref="CircleCount"/> - 1).</summary>
    public Circle Circle(int index)
    {
        if (Kind != PropKind.FenceSegment)
            return new Circle(X, Z, Radius);
        int n = CircleCount;
        float half = Radius - Props.FencePostRadius, t = -half + 2f * half * index / (n - 1);
        var p = Center + Along * t;
        return new Circle(p.X, p.Y, Props.FencePostRadius);
    }
}

/// <summary>
/// Every solid prop of the world: medium mushrooms in the Mushroom Forest and (denser, bigger) in
/// the Deep Forest, lamps, benches, garden fences, planters and crates in Mushroom Town, lamps,
/// benches, planters and a statue in Pet City, and lamps along the roads. Anything taller than
/// about 0.4 m (or wider than about 0.6 m) stands here and is solid; only small ground decoration
/// is scattered freely. Each keeps <see cref="Clearance"/> meters from paths, roads, doors, named
/// places, spawn spots and other obstacles. Fixed data, deterministic.
/// </summary>
public static class Props
{
    /// <summary>Free ground (meters) every prop keeps from paths, roads, doors, places, spawn spots, buildings and other obstacles.</summary>
    public const float Clearance = 1.5f;

    /// <summary>Radius (meters) of each circle of a fence segment.</summary>
    public const float FencePostRadius = 0.2f;

    /// <summary>At most this far (meters) between the centers of a fence segment's circles, so nothing slips through.</summary>
    public const float FenceCircleSpacing = 0.3f;

    /// <summary>Half the width of Pet City's avenue (meters): from its south entrance north past the plaza to Lucy's counter, along x = 0.</summary>
    public const float CityAvenueHalfWidth = 3f;

    /// <summary>A forest trunk with no other giant mushroom within this many meters of its edge stands alone in a glade no prop enters.</summary>
    public const float GladeRadius = 12f;

    private static readonly Prop[] s_all = Build();

    /// <summary>Every prop, in a fixed order: Mushroom Forest, Deep Forest, Mushroom Town, Pet City, roads.</summary>
    public static ReadOnlySpan<Prop> All => s_all;

    /// <summary>Number of props.</summary>
    public static int Count => s_all.Length;

    /// <summary>Distance (meters) from (x, z) to the center line of Pet City's avenue.</summary>
    public static float CityAvenueDistance(float x, float z) =>
        ForestFloor.SegmentDistance(x, z, 0f, Areas.PetCity.MaxZ, 0f, Places.LucySpot.Position.Z);

    // ---- Building the layout ----

    private sealed class Layout
    {
        public readonly List<Prop> Props = new();
        public readonly List<Circle> Solid = new();
        public readonly List<Vector2> Spots = new();
    }

    private static Prop[] Build()
    {
        var layout = new Layout();
        layout.Solid.AddRange(ForestFloor.Trunks());
        layout.Solid.AddRange(DeepForestLayout.Trunks());
        Ground.AddBuildingCircles(layout.Solid);
        foreach (var p in new[]
                 {
                     Places.HomeDoor, Places.ClinicDoor, Places.MillieSpot, Places.FurnitureShopSpot, Places.AbbySpot,
                     Places.LucySpot, Places.SignpostTown, Places.SignpostForestSouth, Places.SignpostForestNorth,
                     Places.SignpostPetCity, Places.SignpostDeepForest,
                 })
            layout.Spots.Add(new Vector2(p.Position.X, p.Position.Z));

        Forest(layout);
        DeepForest(layout);
        Town(layout);
        PetCity(layout);
        Roads(layout);
        return layout.Props.ToArray();
    }

    private static void Forest(Layout layout)
    {
        // The lone giant mushrooms' glades.
        var trunks = ForestFloor.Trunks();
        var glades = new List<Circle>();
        foreach (var t in trunks)
        {
            bool alone = true;
            foreach (var o in trunks)
                if (!o.Equals(t) && Vector2.Distance(o.Center, t.Center) < t.Radius + o.Radius + GladeRadius)
                    alone = false;
            if (alone)
                glades.Add(new Circle(t.X, t.Z, t.Radius + GladeRadius));
        }
        const float cell = 9.5f;
        int n = (int)(2f * Areas.ForestHalfSize / cell);
        for (int i = 0; i < n; i++)
        for (int j = 0; j < n; j++)
        {
            if (ForestFloor.Hash(i, j, 50) < 0.12f)
                continue;
            float x = -Areas.ForestHalfSize + (i + 0.1f + 0.8f * ForestFloor.Hash(i, j, 51)) * cell;
            float z = -Areas.ForestHalfSize + (j + 0.1f + 0.8f * ForestFloor.Hash(i, j, 52)) * cell;
            float height = 0.8f + 1.7f * ForestFloor.Hash(i, j, 53);
            float radius = 0.3f + 0.25f * height;
            if (glades.Exists(g => Vector2.Distance(g.Center, new Vector2(x, z)) < g.Radius + radius))
                continue;
            TryAdd(layout, new Prop(PropKind.MediumMushroom, Area.Forest, x, z, ForestFloor.Hash(i, j, 54) * MathF.Tau, radius, height));
        }
    }

    private static void DeepForest(Layout layout)
    {
        // Denser and a bit bigger than the forest's; squeezed between the giant trunks.
        const float cell = 6f;
        var b = Areas.DeepForest;
        int nx = (int)((b.MaxX - b.MinX) / cell), nz = (int)((b.MaxZ - b.MinZ) / cell);
        for (int i = 0; i < nx; i++)
        for (int j = 0; j < nz; j++)
        {
            float x = b.MinX + (i + 0.1f + 0.8f * ForestFloor.Hash(i, j, 30)) * cell;
            float z = b.MinZ + (j + 0.1f + 0.8f * ForestFloor.Hash(i, j, 31)) * cell;
            float height = 1.1f + 1.4f * ForestFloor.Hash(i, j, 32);
            float radius = 0.3f + 0.25f * height;
            TryAdd(layout, new Prop(PropKind.MediumMushroom, Area.DeepForest, x, z, ForestFloor.Hash(i, j, 33) * MathF.Tau, radius, height));
        }
    }

    private static void Town(Layout layout)
    {
        var square = Buildings.TownSquare;
        // A fenced garden behind each house, with a planter in it.
        foreach (var b in Buildings.All)
            if (b.Area == Area.Town && b.Kind is BuildingKind.PlayerHouse or BuildingKind.MillieHouse or BuildingKind.House)
                Garden(layout, b);
        // Street lamps on both sides of the street in from the entrance, and in a ring around the square.
        float offset = Areas.TownPathHalfWidth + Clearance + 0.35f;
        for (float z = Areas.Town.MinZ + 8f; z < square.Y - 16f; z += 12f)
        {
            TryAdd(layout, Lamp(Area.Town, offset, z, 1.9f));
            TryAdd(layout, Lamp(Area.Town, -offset, z + 6f, 1.8f));
        }
        Ring(layout, square, 15f, 9f, (x, z) => Lamp(Area.Town, x, z, 2f));
        // Benches around the square between the paths, facing the fountain, and planters between them.
        Ring(layout, square, 18f, 7f, (x, z) => new Prop(PropKind.Bench, Area.Town, x, z, Facing(square.X - x, square.Y - z), 0.8f, 0.9f));
        Ring(layout, square, 21f, 8f, (x, z) => new Prop(PropKind.Planter, Area.Town, x, z, Facing(square.X - x, square.Y - z), 0.6f, 0.7f));
        // Crates beside the furniture stall.
        var stall = Buildings.First(BuildingKind.FurnitureShop);
        foreach (float side in new[] { -1f, 1f })
        {
            var p = stall.ToWorld(side * (stall.Width * 0.5f + Clearance + 1.6f), 0.5f);
            TryAdd(layout, new Prop(PropKind.Crate, Area.Town, p.X, p.Y, stall.Rotation, 0.5f, 0.8f));
        }
    }

    private static void Garden(Layout layout, Building house)
    {
        // A square plot behind the house (local +Z), fenced on all four sides with a gate in the back.
        const float size = 7f, segment = size * 0.5f;
        float back = house.Depth * 0.5f + Clearance + 1.2f; // clear of the house's circles too
        int props = layout.Props.Count, solid = layout.Solid.Count;
        bool whole = true;
        void Fence(float lx, float lz, bool alongX)
        {
            var p = house.ToWorld(lx, back + lz);
            whole &= TryAdd(layout, new Prop(PropKind.FenceSegment, Area.Town, p.X, p.Y, alongX ? house.Rotation : house.Rotation + MathF.PI / 2f, segment * 0.5f, 0.8f), fence: true);
        }
        for (int k = 0; k < 2; k++)
        {
            float t = -size * 0.5f + (k + 0.5f) * segment;
            Fence(t, 0f, true);                     // the side facing the house
            if (k == 0)
                Fence(t, size, true);               // the back, its other half the gate
            Fence(-size * 0.5f, size * 0.5f + t, false);
            Fence(size * 0.5f, size * 0.5f + t, false);
        }
        var middle = house.ToWorld(0f, back + size * 0.5f);
        whole &= TryAdd(layout, new Prop(PropKind.Planter, Area.Town, middle.X, middle.Y, house.Rotation, 0.6f, 0.6f));
        if (!whole)
        {
            // No half gardens: where one does not fit, there is none.
            layout.Props.RemoveRange(props, layout.Props.Count - props);
            layout.Solid.RemoveRange(solid, layout.Solid.Count - solid);
        }
    }

    private static void PetCity(Layout layout)
    {
        var plaza = Buildings.CityPlaza;
        // Lamps and planters along both sides of the avenue, a ring of lamps around the plaza.
        float side = CityAvenueHalfWidth + Clearance + 1f;
        for (float z = Areas.PetCity.MaxZ - 8f; z > Places.LucySpot.Position.Z; z -= 12f)
        {
            if (MathF.Abs(z - plaza.Y) < 18f)
                continue;
            TryAdd(layout, Lamp(Area.PetCity, side, z, 2.6f));
            TryAdd(layout, Lamp(Area.PetCity, -side, z, 2.6f));
            TryAdd(layout, new Prop(PropKind.Planter, Area.PetCity, side, z - 6f, 0f, 0.6f, 0.7f));
            TryAdd(layout, new Prop(PropKind.Planter, Area.PetCity, -side, z - 6f, 0f, 0.6f, 0.7f));
        }
        for (int a = 0; a < 8; a++)
        {
            float angle = (a + 0.5f) * MathF.Tau / 8f;
            TryAdd(layout, Lamp(Area.PetCity, plaza.X + MathF.Sin(angle) * 16.5f, plaza.Y + MathF.Cos(angle) * 16.5f, 2.6f));
        }
        // Benches facing the avenue, and around the plaza facing its middle.
        foreach (float z in new[] { -168f, -186f, -244f })
        foreach (float sx in new[] { -1f, 1f })
            TryAdd(layout, new Prop(PropKind.Bench, Area.PetCity, sx * (side + 3f), z, Facing(-sx, 0f), 0.8f, 0.9f));
        for (int a = 0; a < 4; a++)
        {
            float angle = (a + 0.5f) * MathF.Tau / 4f;
            float x = plaza.X + MathF.Sin(angle) * 12.5f, z = plaza.Y + MathF.Cos(angle) * 12.5f;
            TryAdd(layout, new Prop(PropKind.Bench, Area.PetCity, x, z, Facing(plaza.X - x, plaza.Y - z), 0.8f, 0.9f));
        }
        // A statue on the plaza's west side, looking across it.
        TryAdd(layout, new Prop(PropKind.Statue, Area.PetCity, plaza.X - 22f, plaza.Y, Facing(1f, 0f), 1.1f, 3.4f));
    }

    private static void Roads(Layout layout)
    {
        // Lamps along the roads between the areas, alternating sides, beside the road's edge.
        var roads = Areas.RoadPoints;
        float travelled = 0f, next = 4f, offset = Areas.RoadHalfWidth + Clearance + 0.35f;
        int lamp = 0;
        for (int i = 0; i + 1 < roads.Length; i++)
        {
            Vector2 a = roads[i], b = roads[i + 1];
            if (float.IsNaN(a.X) || float.IsNaN(b.X))
            {
                travelled = 0f;
                next = 4f;
                continue;
            }
            float length = Vector2.Distance(a, b);
            var dir = (b - a) / length;
            while (next <= travelled + length)
            {
                var p = a + dir * (next - travelled);
                float sign = lamp % 2 == 0 ? 1f : -1f;
                var at = p + new Vector2(-dir.Y, dir.X) * (sign * offset);
                if (Areas.At(at.X, at.Y) == Area.Road)
                    TryAdd(layout, Lamp(Area.Road, at.X, at.Y, 1.7f + 0.3f * ForestFloor.Hash(lamp, 0, 40)));
                lamp++;
                next += 12f;
            }
            travelled += length;
        }
    }

    /// <summary>Props around a circle, tried every 2 degrees, at least <paramref name="spacing"/> meters apart along it.</summary>
    private static void Ring(Layout layout, Vector2 center, float radius, float spacing, Func<float, float, Prop> make)
    {
        float last = float.NegativeInfinity;
        for (int step = 0; step < 180; step++)
        {
            float angle = step * MathF.Tau / 180f;
            if ((angle - last) * radius < spacing)
                continue;
            if (TryAdd(layout, make(center.X + MathF.Sin(angle) * radius, center.Y + MathF.Cos(angle) * radius)))
                last = angle;
        }
    }

    private static Prop Lamp(Area area, float x, float z, float height) => new(PropKind.Lamp, area, x, z, 0f, 0.3f, height);

    /// <summary>Adds the prop when all its circles keep clear of everything; the pieces of a fence may meet each other.</summary>
    private static bool TryAdd(Layout layout, Prop prop, bool fence = false)
    {
        for (int k = 0; k < prop.CircleCount; k++)
            if (!Fits(layout, prop, prop.Circle(k), fence))
                return false;
        layout.Props.Add(prop);
        for (int k = 0; k < prop.CircleCount; k++)
            layout.Solid.Add(prop.Circle(k));
        return true;
    }

    private static bool Fits(Layout layout, Prop prop, Circle c, bool fence)
    {
        float x = c.X, z = c.Z, r = c.Radius, keep = r + Clearance;
        if (Areas.At(x, z) != prop.Area)
            return false;
        if (prop.Area != Area.Road)
        {
            var bounds = Areas.Bounds(prop.Area);
            if (!bounds.Contains(x, z, r + 2f) || !Areas.Walkable(x, z, keep))
                return false;
        }
        if (Areas.RoadDistance(x, z) < Areas.RoadHalfWidth + keep)
            return false;
        foreach (var spot in layout.Spots)
            if (Vector2.Distance(spot, new Vector2(x, z)) < keep + 0.5f)
                return false;
        foreach (var b in Buildings.All)
            if (b.Contains(x, z, keep))
                return false;
        foreach (var o in layout.Solid)
        {
            if (fence && o.Radius == FencePostRadius)
                continue;
            if (Vector2.Distance(o.Center, new Vector2(x, z)) < r + o.Radius + Clearance)
                return false;
        }
        switch (prop.Area)
        {
            case Area.Forest:
                if (ForestFloor.PolylineDistance(ForestFloor.PathPoints, x, z) < ForestFloor.PathHalfWidth + keep
                    || ForestFloor.PolylineDistance(ForestFloor.TrailPoints, x, z) < ForestFloor.TrailHalfWidth + keep
                    || ForestFloor.ClearingDepth(x, z) > -keep)
                    return false;
                // The road gaps north and south and the way in through them, the west gap and the Deep Forest path.
                if (MathF.Abs(x) < Areas.ForestGapHalfWidth + keep && MathF.Abs(z) > Areas.ForestHalfSize - 12f)
                    return false;
                if (Vector2.Distance(new Vector2(x, z), Areas.ForestGapWest) < 12f + r || Areas.DeepForestPath.Depth(x, z) > -keep)
                    return false;
                break;
            case Area.DeepForest:
                if (Areas.DeepForestPath.Depth(x, z) > -keep)
                    return false;
                var entrance = DeepForestLayout.Entrance;
                if (Vector2.Distance(new Vector2(MathF.Max(x, entrance.X - 10f), z), entrance) < DeepForestLayout.ClearingRadius + 4f + r
                    && x > entrance.X - 10f - DeepForestLayout.ClearingRadius - 4f - r)
                    return false;
                foreach (var s in DeepForestLayout.SpawnPoints)
                    if (Vector2.Distance(s, new Vector2(x, z)) < DeepForestLayout.ClearingRadius + r)
                        return false;
                break;
            case Area.Town:
                if (Areas.TownPathDistance(x, z) < Areas.TownPathHalfWidth + keep)
                    return false;
                // The fountain's ring of paving stays open to walk around.
                if (Vector2.Distance(Buildings.TownSquare, new Vector2(x, z)) < 3.5f + 4f + r)
                    return false;
                break;
            case Area.PetCity:
                if (CityAvenueDistance(x, z) < CityAvenueHalfWidth + keep)
                    return false;
                // The plaza's middle (Pet City's fountain) and its tiled ring stay open.
                if (Vector2.Distance(Buildings.CityPlaza, new Vector2(x, z)) < 10f + r)
                    return false;
                break;
        }
        return true;
    }

    /// <summary>Yaw whose front (-Z at 0) faces the direction (dx, dz).</summary>
    private static float Facing(float dx, float dz) => MathF.Atan2(-dx, -dz);
}
