using System;
using System.Numerics;

namespace Fungtopia.Map;

/// <summary>The kinds of mushrooms and fungi the Map draws on the cliffs (Tony, 2026-10-26: "a variety of many different types").</summary>
public enum FungusKind
{
    FlyAgaric, Porcini, Chanterelle, Parasol, InkCap, Morel, Enoki, Amethyst, Puffball, ScarletCup, Coral, Jelly, GlowCap,
    /// <summary>A shelf fungus growing out of a big stem (never a spot's own kind).</summary>
    Bracket,
}

/// <summary>The cap meshes the view has, one MultiMesh each.</summary>
public enum CapShape { Dome, Flat, Funnel, Bell, Wavy, Cone, Ball, Cup, Coral, Jelly, Shelf }

/// <summary>
/// One mushroom of a group, relative to the group's spot on the ground: its foot at (X, Z) and
/// <see cref="Lift"/> meters up, its stem tilted <see cref="Tilt"/> radians toward yaw
/// <see cref="TiltYaw"/>, the cap on the stem's top. <see cref="StemHeight"/> 0 means no stem
/// (the cap sits on the ground, or on a stem for a bracket). Pattern 0 is spots, 1 honeycomb pits.
/// </summary>
public readonly record struct Fungus(
    FungusKind Kind, CapShape Shape, float X, float Z, float Lift, float Yaw, float Tilt, float TiltYaw,
    float StemRadius, float StemHeight, float CapRadius, float CapHeight, Vector3 Color, float Spots, int Pattern, float Glow);

/// <summary>
/// Picks the kind, shape, size, proportions, tilt and colors of every mushroom the Map draws from
/// a fixed seed per spot, so the world is the same every run: pure, deterministic, no clock and no
/// unseeded randomness.
/// </summary>
public static class Fungi
{
    /// <summary>The most mushrooms one cliff spot's group holds (an enoki cluster plus brackets).</summary>
    public const int MaxGroup = 12;

    /// <summary>Every kind a cliff spot may be (not <see cref="FungusKind.Bracket"/>).</summary>
    public const int SpotKinds = (int)FungusKind.GlowCap + 1;

    /// <summary>The cap shapes a lamp may have: each still a clear glowing cap on a stem.</summary>
    public static readonly CapShape[] LampShapes = { CapShape.Dome, CapShape.Bell, CapShape.Flat, CapShape.Funnel, CapShape.Wavy, CapShape.Cone };

    /// <summary>A fixed pseudo-random value in [0, 1) for (i, k).</summary>
    public static float Hash(int i, int k)
    {
        uint h = unchecked((uint)i * 2654435761u ^ (uint)k * 2246822519u ^ 0x7f4a7c15u);
        h = (h ^ (h >> 15)) * 2246822519u;
        h = (h ^ (h >> 13)) * 3266489917u;
        h ^= h >> 16;
        return (h >> 8) * (1f / 16777216f);
    }

    /// <summary>
    /// The kind of each cell of a <paramref name="columns"/> x <paramref name="rows"/> grid of spots
    /// (index column * rows + row): seeded per cell, and never the same as any of its eight neighbors.
    /// </summary>
    public static FungusKind[] CliffKinds(int columns, int rows)
    {
        var kinds = new FungusKind[columns * rows];
        for (int c = 0; c < columns; c++)
        for (int r = 0; r < rows; r++)
        {
            for (int salt = 0; ; salt++)
            {
                var kind = (FungusKind)(int)(Hash(c * rows + r, 100 + salt) * SpotKinds);
                bool clash = false;
                // The neighbors already chosen: the column before (three) and the row before.
                for (int dr = -1; dr <= 1 && c > 0; dr++)
                    clash |= r + dr >= 0 && r + dr < rows && kinds[(c - 1) * rows + r + dr] == kind;
                clash |= r > 0 && kinds[c * rows + r - 1] == kind;
                if (!clash)
                {
                    kinds[c * rows + r] = kind;
                    break;
                }
            }
        }
        return kinds;
    }

    /// <summary>
    /// The group of mushrooms at one cliff spot of <paramref name="kind"/>: a single one, a pair or
    /// a cluster of different heights (an enoki cluster always), some with brackets on their big
    /// stem. <paramref name="size"/> (about 0.9..2.3) scales it all. Returns how many it wrote.
    /// </summary>
    public static int CliffGroup(FungusKind kind, int seed, float size, Span<Fungus> group)
    {
        if (group.Length < MaxGroup)
            throw new ArgumentException($"needs room for {MaxGroup}", nameof(group));
        int n = 0;
        float height = 6f + size * 2.6f + 5f * Hash(seed, 1);
        if (kind == FungusKind.Enoki)
        {
            // A bunch of tall thin stems from one foot, fanning out, each with a tiny cap.
            int count = 5 + (int)(Hash(seed, 2) * 5f);
            for (int i = 0; i < count; i++)
            {
                float a = (i + 0.5f * Hash(seed, 10 + i)) * MathF.Tau / count;
                float spread = size * 0.35f * Hash(seed, 20 + i);
                group[n++] = One(kind, seed * 31 + i, size * (0.8f + 0.4f * Hash(seed, 30 + i)), height * (0.35f + 0.4f * Hash(seed, 40 + i)),
                    MathF.Sin(a) * spread, MathF.Cos(a) * spread, 0.12f + 0.2f * Hash(seed, 50 + i), a);
            }
            return n;
        }

        // The main one, nearly upright.
        var main = One(kind, seed, size, height, 0f, 0f, 0.1f * Hash(seed, 3), Hash(seed, 4) * MathF.Tau);
        group[n++] = main;
        // Brackets on a big upright stem.
        if (main.StemRadius >= 0.9f && main.StemHeight >= 5f && Hash(seed, 5) < 0.55f)
        {
            int brackets = 1 + (int)(Hash(seed, 6) * 3f);
            for (int i = 0; i < brackets; i++)
            {
                float a = Hash(seed, 60 + i) * MathF.Tau;
                float r = main.StemRadius * (0.55f + 0.25f * Hash(seed, 70 + i));
                var color = MushroomPalette.Colors(FungusKind.Bracket);
                group[n++] = new Fungus(FungusKind.Bracket, CapShape.Shelf,
                    MathF.Sin(a) * main.StemRadius * 0.95f, MathF.Cos(a) * main.StemRadius * 0.95f,
                    main.StemHeight * (0.25f + 0.45f * Hash(seed, 80 + i)), a, 0f, 0f, 0f, 0f, r, r * 0.3f,
                    color[(int)(Hash(seed, 90 + i) * color.Length)], 0f, 0, 0f);
            }
        }
        // Companions: none, one (a pair) or two or three (a cluster), smaller and leaning out.
        float roll = Hash(seed, 7);
        int companions = roll < 0.4f ? 0 : roll < 0.72f ? 1 : 2 + (int)(Hash(seed, 8) * 2f);
        for (int i = 0; i < companions; i++)
        {
            // Mostly the same kind; sometimes a little one of another kind at its foot.
            var other = Hash(seed, 110 + i) < 0.6f ? kind : FootKinds[(int)(Hash(seed, 120 + i) * FootKinds.Length)];
            float scale = 0.35f + 0.4f * Hash(seed, 130 + i);
            float a = (i + 0.3f + 0.4f * Hash(seed, 140 + i)) * MathF.Tau / companions + Hash(seed, 9) * MathF.Tau;
            float reach = size * (1.6f + 1.4f * Hash(seed, 150 + i));
            group[n++] = One(other, seed * 7 + 1000 + i, size * scale, height * scale * (0.7f + 0.5f * Hash(seed, 160 + i)),
                MathF.Sin(a) * reach, MathF.Cos(a) * reach, 0.08f + 0.22f * Hash(seed, 170 + i), a);
        }
        return n;
    }

    /// <summary>
    /// Where <paramref name="bracket"/> grows on <paramref name="main"/>'s stem, as an offset from
    /// the main stem's foot: its (X, Lift, Z) relative to the main one, leaned with the main stem
    /// (<see cref="Fungus.Tilt"/> toward <see cref="Fungus.TiltYaw"/>), so it stays on the stem.
    /// </summary>
    public static Vector3 OnStem(in Fungus main, in Fungus bracket)
    {
        var lean = Quaternion.CreateFromAxisAngle(new Vector3(MathF.Cos(main.TiltYaw), 0f, -MathF.Sin(main.TiltYaw)), main.Tilt);
        return Vector3.Transform(new Vector3(bracket.X - main.X, bracket.Lift, bracket.Z - main.Z), lean);
    }

    /// <summary>Small kinds that grow at a big mushroom's foot.</summary>
    private static readonly FungusKind[] FootKinds =
        { FungusKind.Puffball, FungusKind.ScarletCup, FungusKind.Coral, FungusKind.Jelly, FungusKind.Morel, FungusKind.FlyAgaric, FungusKind.GlowCap };

    /// <summary>One mushroom of <paramref name="kind"/>, its proportions the kind's, varied by its seed.</summary>
    private static Fungus One(FungusKind kind, int seed, float size, float height, float x, float z, float tilt, float tiltYaw)
    {
        float u = Hash(seed, 200), v = Hash(seed, 201), w = Hash(seed, 202);
        CapShape shape;
        float stem, tall, cap, capRatio, spots = 0f, glow = 0f;
        int pattern = 0;
        switch (kind)
        {
            case FungusKind.FlyAgaric:
                // Round red domes, older ones opening flat, crisp white spots.
                shape = w < 0.75f ? CapShape.Dome : CapShape.Flat;
                stem = size; tall = 1f; cap = 2.4f + 1.2f * u; capRatio = shape == CapShape.Dome ? 0.55f + 0.3f * v : 0.3f + 0.1f * v; spots = 1f;
                break;
            case FungusKind.Porcini:
                shape = CapShape.Dome; stem = 1.6f * size; tall = 0.55f + 0.15f * w; cap = 2.2f + 0.8f * u; capRatio = 0.75f + 0.2f * v;
                break;
            case FungusKind.Chanterelle:
                shape = w < 0.7f ? CapShape.Funnel : CapShape.Wavy; stem = 0.8f * size; tall = 0.6f + 0.2f * w; cap = 2f + 0.8f * u; capRatio = 0.6f + 0.3f * v;
                break;
            case FungusKind.Parasol:
                shape = CapShape.Flat; stem = 0.4f * size; tall = 1.15f + 0.25f * w; cap = 3f + 1.2f * u; capRatio = 0.28f + 0.14f * v; spots = 0.55f;
                break;
            case FungusKind.InkCap:
                shape = CapShape.Bell; stem = 0.55f * size; tall = 0.8f + 0.2f * w; cap = 1.3f + 0.5f * u; capRatio = 1.6f + 0.6f * v; spots = 0.4f;
                break;
            case FungusKind.Morel:
                shape = CapShape.Cone; stem = 0.75f * size; tall = 0.35f + 0.15f * w; cap = 1.1f + 0.3f * u; capRatio = 1.8f + 0.6f * v; pattern = 1;
                break;
            case FungusKind.Enoki:
                shape = CapShape.Dome; stem = 0.2f * size; tall = 1f; cap = 0.45f + 0.15f * u; capRatio = 0.6f + 0.2f * v;
                break;
            case FungusKind.Amethyst:
                shape = w < 0.6f ? CapShape.Wavy : CapShape.Dome; stem = 0.75f * size; tall = 0.75f + 0.2f * w; cap = 2.2f + 0.8f * u; capRatio = 0.4f + 0.2f * v;
                break;
            case FungusKind.Puffball:
                shape = CapShape.Ball; stem = 0f; tall = 0f; cap = 1.4f + 1f * u; capRatio = 0.85f + 0.15f * v; spots = 0.35f;
                break;
            case FungusKind.ScarletCup:
                shape = CapShape.Cup; stem = 0.4f * size; tall = 0.1f; cap = 1.5f + 0.8f * u; capRatio = 0.7f + 0.2f * v;
                break;
            case FungusKind.Coral:
                shape = CapShape.Coral; stem = 0f; tall = 0f; cap = 1.8f + 1f * u; capRatio = 1.1f + 0.5f * v;
                break;
            case FungusKind.Jelly:
                shape = CapShape.Jelly; stem = 0f; tall = 0f; cap = 1.6f + 0.9f * u; capRatio = 0.8f + 0.25f * v;
                break;
            case FungusKind.GlowCap:
                shape = w < 0.5f ? CapShape.Bell : CapShape.Dome; stem = 0.6f * size; tall = 0.85f + 0.2f * w; cap = 1.8f + 0.8f * u;
                capRatio = shape == CapShape.Bell ? 1.1f + 0.4f * v : 0.55f + 0.25f * v; spots = 0.8f; glow = MushroomPalette.GiantGlow;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
        // Stemless kinds sit upright on the ground.
        if (tall == 0f)
            tilt = 0f;
        var colors = MushroomPalette.Colors(kind);
        float capRadius = cap * size;
        return new Fungus(kind, shape, x, z, 0f, Hash(seed, 203) * MathF.Tau, tilt, tiltYaw,
            stem, height * tall, capRadius, capRadius * capRatio, colors[(int)(Hash(seed, 204) * colors.Length)], spots, pattern, glow);
    }

    /// <summary>
    /// Road lamp <paramref name="index"/> (in <see cref="Props.All"/> order) of prop height and radius:
    /// a glowing cap on a thin stem exactly <paramref name="height"/> tall at the prop's spot, its cap
    /// no wider than the prop's footprint. Shapes and colors vary; neighboring lamps differ in color.
    /// </summary>
    public static Fungus Lamp(int index, float height, float radius)
    {
        var shape = LampShapes[(int)(Hash(index, 300) * LampShapes.Length)];
        float capRadius = radius * (0.8f + 0.2f * Hash(index, 301));
        float capRatio = shape switch
        {
            CapShape.Bell => 1.1f + 0.3f * Hash(index, 302),
            CapShape.Cone => 1.3f + 0.4f * Hash(index, 302),
            CapShape.Flat => 0.32f + 0.1f * Hash(index, 302),
            _ => 0.55f + 0.3f * Hash(index, 302),
        };
        var colors = MushroomPalette.LampColors;
        return new Fungus(FungusKind.GlowCap, shape, 0f, 0f, 0f, Hash(index, 303) * MathF.Tau, 0.05f * Hash(index, 304), Hash(index, 305) * MathF.Tau,
            0.1f, height, capRadius, capRadius * capRatio, colors[index % colors.Length], shape == CapShape.Cone ? 0f : 0.7f, 0, MushroomPalette.LampGlow);
    }
}
