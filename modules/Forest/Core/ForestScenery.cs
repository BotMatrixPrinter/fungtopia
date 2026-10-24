using System;
using System.Collections.Generic;
using Fungtopia.Map;

namespace Fungtopia.Forest;

/// <summary>
/// What the view draws standing in the forest, and how big: a giant mushroom on every forest
/// trunk (<see cref="Giants"/>, stem exactly the trunk's circle), a medium mushroom on every Map
/// prop of kind <see cref="PropKind.MediumMushroom"/> in <see cref="Area.Forest"/>
/// (<see cref="MediumMushrooms"/>, exactly at the prop, stem radius the prop's radius, as tall as
/// the prop), and small walkable ground decoration (<see cref="Decoration"/>: tiny mushrooms,
/// ferns, flowers, grass tufts), every piece lower than <see cref="DecorationMaxHeight"/> and
/// narrower than <see cref="DecorationMaxRadius"/>. Anything taller stands on an obstacle.
/// Fixed: the same every run.
/// </summary>
public static class ForestScenery
{
    /// <summary>Decoration (meters) is always lower than this: anything taller is solid and comes from the Map.</summary>
    public const float DecorationMaxHeight = 0.4f;

    /// <summary>Decoration's footprint radius (meters) is always smaller than this (0.6 m across).</summary>
    public const float DecorationMaxRadius = 0.3f;

    /// <summary>Height (meters) of the fern mesh at scale 1.</summary>
    public const float FernMeshHeight = 0.5f;

    /// <summary>Footprint radius (meters) of the fern mesh at scale 1.</summary>
    public const float FernMeshRadius = 0.6f;

    /// <summary>Height (meters) of the tallest blade of the grass tuft mesh at scale 1.</summary>
    public const float GrassMeshHeight = 0.32f;

    /// <summary>Footprint radius (meters) of the grass tuft mesh at scale 1.</summary>
    public const float GrassMeshRadius = 0.16f;

    /// <summary>Height (meters) of the flower mesh at scale 1.</summary>
    public const float FlowerMeshHeight = 1f;

    /// <summary>Footprint radius (meters) of the flower mesh at scale 1.</summary>
    public const float FlowerMeshRadius = 0.6f;

    /// <summary>The widest the stem mesh gets at radius scale 1 (its bulging foot): never wider than its circle.</summary>
    public const float StemMeshMaxRadius = 1f;

    /// <summary>The stem mesh's radius at height fraction <paramref name="t"/> (0 foot .. 1 top), at most <see cref="StemMeshMaxRadius"/>.</summary>
    public static float StemProfile(float t) => 0.72f + 0.28f * MathF.Pow(1f - t, 4f) - 0.1f * t;

    private static readonly DrawnMushroom[] s_giants = PlaceGiants();
    private static readonly DrawnMushroom[] s_medium = PlaceMedium();
    private static readonly Decoration[] s_decoration = PlaceDecoration();

    /// <summary>One giant mushroom per forest trunk (the first <see cref="Ground.ForestTrunkCount"/> of <see cref="Obstacles.All"/>), in order.</summary>
    public static ReadOnlySpan<DrawnMushroom> Giants => s_giants;

    /// <summary>One medium mushroom per Map prop of kind MediumMushroom in the forest, in <see cref="Props.All"/> order.</summary>
    public static ReadOnlySpan<DrawnMushroom> MediumMushrooms => s_medium;

    /// <summary>Small walkable ground decoration, every piece under <see cref="DecorationMaxHeight"/>.</summary>
    public static ReadOnlySpan<Decoration> Decoration => s_decoration;

    private static float Rand(int i, int k) => ForestRandom.Value(i, k);

    /// <summary>A cap of <paramref name="capRadius"/>, <paramref name="capFraction"/> of it high, no higher than half the mushroom.</summary>
    private static DrawnMushroom Make(DecorKind kind, float x, float z, float yaw, float stemRadius, float height, float capRadius, float capFraction, int id) =>
        new(kind, x, z, yaw, stemRadius, height, capRadius, MathF.Min(capRadius * capFraction, height * 0.5f), id);

    private static DrawnMushroom[] PlaceGiants()
    {
        var all = Obstacles.All;
        var giants = new DrawnMushroom[Ground.ForestTrunkCount];
        for (int i = 0; i < giants.Length; i++)
        {
            var o = all[i];
            float height = 6f + o.Radius * 2.6f + 4f * Rand(i, 20);
            giants[i] = Make(DecorKind.Mushroom, o.X, o.Z, Rand(i, 1) * MathF.Tau, o.Radius, height,
                o.Radius * (2.6f + 1.2f * Rand(i, 21)), 0.5f + 0.3f * Rand(i, 6), i);
        }
        return giants;
    }

    private static DrawnMushroom[] PlaceMedium()
    {
        var list = new List<DrawnMushroom>();
        int id = 20000;
        foreach (var p in Props.All)
        {
            if (p.Kind != PropKind.MediumMushroom || p.Area != Area.Forest)
                continue;
            list.Add(Make(DecorKind.Mushroom, p.X, p.Z, p.Rotation, p.Radius, p.Height,
                p.Radius * (1.5f + 0.5f * Rand(id, 21)), 0.5f + 0.25f * Rand(id, 6), id));
            id++;
        }
        return list.ToArray();
    }

    private static Decoration[] PlaceDecoration()
    {
        var list = new List<Decoration>();

        // Tiny mushroom clusters in the open.
        for (int c = 0; c < 420; c++)
        {
            float cx = (Rand(c, 40) * 2f - 1f) * ForestBounds.HalfSize, cz = (Rand(c, 41) * 2f - 1f) * ForestBounds.HalfSize;
            if (!ForestProps.Open(cx, cz, 0.5f))
                continue;
            int count = 3 + (int)(Rand(c, 42) * 5f);
            for (int k = 0; k < count; k++)
            {
                int id = 30000 + c * 8 + k;
                float a = k * 2.4f + Rand(c, 43) * 6f, d = 0.1f + 0.4f * Rand(id, 44);
                float x = cx + MathF.Cos(a) * d, z = cz + MathF.Sin(a) * d;
                float size = 0.05f + 0.1f * Rand(id, 45);
                if (ForestProps.Open(x, z, size))
                    list.Add(Mushroom(x, z, size, size * (1.3f + 1.2f * Rand(id, 46)), id));
            }
        }

        // Ferns gather at trunk feet and along the paths' edges.
        int f = 0;
        foreach (var o in Obstacles.All)
        {
            for (int k = 0; k < 3; k++, f++)
            {
                float a = Rand(f, 50) * MathF.Tau, d = o.Radius + 0.4f + 1.4f * Rand(f, 51);
                AddFern(list, o.X + MathF.Cos(a) * d, o.Z + MathF.Sin(a) * d, f, 50);
            }
        }
        for (int k = 0; k < 900; k++, f++)
        {
            float x = (Rand(f, 55) * 2f - 1f) * ForestBounds.HalfSize, z = (Rand(f, 56) * 2f - 1f) * ForestBounds.HalfSize;
            if (ForestLayout.ClearingDepth(x, z) < 1f)
                AddFern(list, x, z, f, 55);
        }

        // Flowers in the meadows of the clearings and scattered through the forest.
        for (int k = 0; k < 2200; k++)
        {
            int id = 40000 + k;
            float x = (Rand(id, 65) * 2f - 1f) * ForestBounds.HalfSize, z = (Rand(id, 66) * 2f - 1f) * ForestBounds.HalfSize;
            if (ForestLayout.ClearingDepth(x, z) < -6f && Rand(id, 67) > 0.25f)
                continue;
            float height = 0.12f + 0.16f * Rand(id, 68);
            if (ForestProps.Open(x, z, 0.1f))
                list.Add(new Decoration(DecorKind.Flower, x, z, Rand(id, 69) * MathF.Tau, height, height * FlowerMeshRadius / FlowerMeshHeight, 0f, id));
        }

        // Ankle-high grass tufts everywhere but on paths and trunks: on the trail and in the gap
        // too (the overgrowth), and off the square away from the Map's walkable ground.
        for (int k = 0; k < 40000; k++)
        {
            float x = (Rand(k, 60) * 2f - 1f) * (ForestBounds.HalfSize + 6f), z = (Rand(k, 61) * 2f - 1f) * (ForestBounds.HalfSize + 6f);
            if (ForestLayout.PathDistance(x, z) < ForestLayout.PathHalfWidth + 0.2f || Obstacles.Blocks(x, z, 0.1f))
                continue;
            if (!ForestBounds.Contains(x, z) && Areas.Walkable(x, z, -0.5f))
                continue; // the Map's roads and the Deep Forest path run out through the gaps
            float scale = 0.7f + 0.5f * Rand(k, 63);
            list.Add(new Decoration(DecorKind.Grass, x, z, Rand(k, 62) * MathF.Tau, GrassMeshHeight * scale, GrassMeshRadius * scale, 0f, k));
        }

        // A few tiny dark Deep Forest mushrooms beside the trail at the gap's mouth.
        foreach (var m in ForestProps.DarkMushrooms)
        {
            int id = 50000 + list.Count;
            var d = Mushroom(m.X, m.Z, m.Size, m.Size * 1.7f, id);
            list.Add(d with { Kind = DecorKind.DarkMushroom });
        }
        return list.ToArray();
    }

    private static Decoration Mushroom(float x, float z, float capRadius, float height, int id)
    {
        float capHeight = MathF.Min(capRadius * (0.5f + 0.3f * Rand(id, 6)), height * 0.5f);
        return new Decoration(DecorKind.Mushroom, x, z, Rand(id, 1) * MathF.Tau, height, capRadius, capHeight, id);
    }

    private static void AddFern(List<Decoration> list, float x, float z, int f, int salt)
    {
        if (!ForestProps.Open(x, z, 0.3f))
            return;
        // Small enough to stay under the decoration limits (0.48 x 0.6 m = 0.288 m in radius).
        float scale = 0.32f + 0.16f * Rand(f, salt + 3);
        list.Add(new Decoration(DecorKind.Fern, x, z, Rand(f, salt + 2) * MathF.Tau, FernMeshHeight * scale, FernMeshRadius * scale, 0f, f));
    }
}

/// <summary>What a piece of the forest's scenery is.</summary>
public enum DecorKind
{
    Mushroom,
    DarkMushroom,
    Fern,
    Flower,
    Grass,
}

/// <summary>
/// A mushroom the view draws: at (x, z) on the ground, turned by <see cref="Yaw"/>, a stem never
/// wider than <see cref="StemRadius"/> and a cap of <see cref="CapRadius"/> (it may overhang) and
/// <see cref="CapHeight"/>, <see cref="Height"/> meters tall above the ground in all.
/// </summary>
public readonly record struct DrawnMushroom(DecorKind Kind, float X, float Z, float Yaw, float StemRadius, float Height, float CapRadius, float CapHeight, int Id);

/// <summary>
/// A piece of small ground decoration at (x, z), turned by <see cref="Yaw"/>, <see cref="Height"/>
/// tall and <see cref="Radius"/> across in radius (a mushroom's cap radius, its stem a third of it;
/// <see cref="CapHeight"/> only for mushrooms).
/// </summary>
public readonly record struct Decoration(DecorKind Kind, float X, float Z, float Yaw, float Height, float Radius, float CapHeight, int Id);
