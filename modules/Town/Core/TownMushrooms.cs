using System;
using System.Numerics;

namespace Fungtopia.Town;

/// <summary>The kinds of small mushrooms and fungi Town scatters in its gardens (one town.glb object each, <c>Mushroom{Kind}</c>).</summary>
public enum MushroomKind
{
    /// <summary>Fly agaric: a round red cap with white spots.</summary>
    FlyAgaric,
    /// <summary>A fat brown bolete (porcini) on a thick stem.</summary>
    Porcini,
    /// <summary>A golden funnel-shaped chanterelle.</summary>
    Chanterelle,
    /// <summary>A tall thin parasol with a wide flat cap.</summary>
    Parasol,
    /// <summary>A shaggy ink cap: a tall white bell with inky edges.</summary>
    InkCap,
    /// <summary>A morel: a honeycomb cone.</summary>
    Morel,
    /// <summary>A cluster of tall thin enoki-like stems with tiny caps.</summary>
    Enoki,
    /// <summary>A purple amethyst cap.</summary>
    Amethyst,
    /// <summary>Round white puffballs.</summary>
    Puffball,
    /// <summary>Scarlet cup fungi.</summary>
    CupFungus,
    /// <summary>Branching pink and orange coral fungus.</summary>
    Coral,
    /// <summary>Wavy golden jelly fungus.</summary>
    Jelly,
    /// <summary>Glowing blue conical caps (bioluminescent: they glow at night).</summary>
    GlowBlue,
    /// <summary>Glowing green wavy caps (bioluminescent: they glow at night).</summary>
    GlowGreen,
}

/// <summary>The shape of a mushroom's cap (or fruiting body).</summary>
public enum CapShape { Dome, Flat, Funnel, Bell, Conical, Wavy, Round, Cup, Branching, Honeycomb }

/// <summary>
/// One kind as town.py models it: <see cref="ModelWidth"/> across (x and z) and
/// <see cref="ModelHeight"/> tall, drawn at <see cref="MinScale"/>..<see cref="MaxScale"/> of that
/// size; <see cref="Clumped"/> kinds are already a small cluster in one object (they come single
/// or in pairs).
/// </summary>
public readonly struct MushroomShape
{
    public readonly MushroomKind Kind;
    public readonly CapShape Cap;
    public readonly float ModelWidth;
    public readonly float ModelHeight;
    public readonly float MinScale;
    public readonly float MaxScale;
    public readonly bool Clumped;
    public readonly bool Glows;

    public MushroomShape(MushroomKind kind, CapShape cap, float width, float height, float minScale, float maxScale, bool clumped = false, bool glows = false)
    {
        Kind = kind;
        Cap = cap;
        ModelWidth = width;
        ModelHeight = height;
        MinScale = minScale;
        MaxScale = maxScale;
        Clumped = clumped;
        Glows = glows;
    }
}

/// <summary>
/// The garden mushrooms' variety: their kinds and model sizes, the fixed seed of each spot, and the
/// group of mushrooms (a single, a pair or a cluster of 3..5 of different heights, proportions and
/// tilts) each seed grows. Plain math, the same every run.
/// </summary>
public static class TownMushrooms
{
    /// <summary>Every kind, in <see cref="MushroomKind"/> order, with its size as town.py models it.</summary>
    public static readonly MushroomShape[] Shapes =
    {
        new(MushroomKind.FlyAgaric, CapShape.Dome, 0.3f, 0.32f, 0.6f, 1.15f),
        new(MushroomKind.Porcini, CapShape.Dome, 0.3f, 0.26f, 0.6f, 1.2f),
        new(MushroomKind.Chanterelle, CapShape.Funnel, 0.22f, 0.22f, 0.65f, 1.3f),
        new(MushroomKind.Parasol, CapShape.Flat, 0.36f, 0.38f, 0.6f, 0.95f),
        new(MushroomKind.InkCap, CapShape.Bell, 0.16f, 0.34f, 0.6f, 1.05f),
        new(MushroomKind.Morel, CapShape.Honeycomb, 0.14f, 0.26f, 0.65f, 1.3f),
        new(MushroomKind.Enoki, CapShape.Dome, 0.2f, 0.3f, 0.7f, 1.15f, clumped: true),
        new(MushroomKind.Amethyst, CapShape.Wavy, 0.22f, 0.22f, 0.65f, 1.4f),
        new(MushroomKind.Puffball, CapShape.Round, 0.2f, 0.16f, 0.6f, 1.6f),
        new(MushroomKind.CupFungus, CapShape.Cup, 0.24f, 0.1f, 0.8f, 1.6f, clumped: true),
        new(MushroomKind.Coral, CapShape.Branching, 0.24f, 0.22f, 0.7f, 1.4f, clumped: true),
        new(MushroomKind.Jelly, CapShape.Wavy, 0.22f, 0.14f, 0.8f, 1.6f, clumped: true),
        new(MushroomKind.GlowBlue, CapShape.Conical, 0.2f, 0.26f, 0.65f, 1.3f, glows: true),
        new(MushroomKind.GlowGreen, CapShape.Wavy, 0.24f, 0.2f, 0.7f, 1.4f, clumped: true, glows: true),
    };

    /// <summary>Number of kinds.</summary>
    public static int KindCount => Shapes.Length;

    /// <summary>Most a mushroom leans from upright (radians).</summary>
    public const float MaxTilt = 0.26f;

    /// <summary>Groups of the same kind stay at least this far apart (meters), so no two neighbors are alike.</summary>
    public const float NeighborDistance = 3f;

    /// <summary>A spot whose seed's kind grows too near the same kind tries the kind this many further on (coprime with <see cref="KindCount"/>, so it tries every kind).</summary>
    public const int KindStep = 5;

    /// <summary>A group's mushrooms stand within this distance (meters) of its first one.</summary>
    public const float GroupRadius = 0.36f;

    /// <summary>The size of a kind as modelled.</summary>
    public static MushroomShape Shape(MushroomKind kind) => Shapes[(int)kind];

    /// <summary>The town.glb object drawing a kind.</summary>
    public static string ModelName(MushroomKind kind) => $"Mushroom{kind}";

    /// <summary>The fixed seed of the spot (x, z): an integer hash of its position to the centimeter.</summary>
    public static uint Seed(float x, float z)
    {
        uint h = (uint)(int)MathF.Round(x * 100f) * 0x9E3779B1u;
        h ^= (uint)(int)MathF.Round(z * 100f) * 0x85EBCA77u;
        return Mix(h);
    }

    /// <summary>The <paramref name="n"/>th number drawn from <paramref name="seed"/> (0 .. 1).</summary>
    public static float Unit(uint seed, int n) => (Mix(seed + (uint)n * 0x632BE5ABu) & 0xFFFFFF) / 16777216f;

    /// <summary>How many mushrooms the group of a kind at a spot of <paramref name="seed"/> has: 1, 2 or 3..5 (clumped kinds 1 or 2).</summary>
    public static int GroupSize(MushroomKind kind, uint seed)
    {
        float u = Unit(seed, 1);
        if (Shape(kind).Clumped)
            return u < 0.6f ? 1 : 2;
        return u < 0.35f ? 1 : u < 0.65f ? 2 : 3 + (int)(Unit(seed, 2) * 3f);
    }

    /// <summary>
    /// The <paramref name="member"/>th mushroom of a group of <paramref name="count"/> of a kind
    /// rooted at (x, z): its offset, yaw, tilt (leaning out from the group) and size. The first is
    /// the biggest and upright-ish; the others smaller, each a different height and proportion.
    /// Everything stays under <see cref="TownLayout.DecorationMaxHeight"/> and
    /// <see cref="TownLayout.DecorationMaxWidth"/>.
    /// </summary>
    public static (float X, float Z, float Yaw, float Tilt, float Width, float Height) Member(MushroomKind kind, uint seed, int member, int count, float x, float z)
    {
        var shape = Shape(kind);
        int n = 10 + member * 6;
        float yaw = Unit(seed, n) * MathF.Tau;
        float scale = shape.MinScale + (shape.MaxScale - shape.MinScale) * (0.55f + 0.45f * Unit(seed, 3));
        float tilt = (Unit(seed, n + 1) - 0.5f) * MaxTilt;
        float ox = 0f, oz = 0f;
        float share = 1f;
        if (member > 0)
        {
            // Each clearly shorter than the one before, round the first, leaning out.
            share = 0.84f - 0.14f * (member - 1) - 0.06f * Unit(seed, n + 2);
            float angle = Unit(seed, 4) * MathF.Tau + member * MathF.Tau / (count - 1) + Unit(seed, n + 3) * 0.6f;
            float r = MathF.Min(shape.ModelWidth * scale * 0.55f + 0.05f + 0.08f * Unit(seed, n + 4), GroupRadius);
            ox = MathF.Sin(angle) * r;
            oz = MathF.Cos(angle) * r;
            yaw = angle;
            tilt = MaxTilt * (0.45f + 0.55f * Unit(seed, n + 1));
        }
        // Proportions: a bit squatter and wider, or taller and thinner.
        float wide = 0.8f + 0.4f * Unit(seed, n + 5);
        float height = MathF.Min(shape.ModelHeight * scale * share, TownLayout.DecorationMaxHeight - 0.02f);
        float width = MathF.Min(shape.ModelWidth * scale * MathF.Sqrt(share) * wide, TownLayout.DecorationMaxWidth - 0.02f);
        return (x + ox, z + oz, yaw, tilt, width, height);
    }

    private static uint Mix(uint h)
    {
        h ^= h >> 16;
        h *= 0x7FEB352Du;
        h ^= h >> 15;
        h *= 0x846CA68Bu;
        h ^= h >> 16;
        return h;
    }
}
