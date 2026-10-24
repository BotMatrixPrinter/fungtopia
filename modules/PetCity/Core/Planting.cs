using System;
using Fungtopia.Map;

namespace Fungtopia.PetCity;

/// <summary>
/// Where something standing on the uneven ground (<see cref="Ground.HeightAt"/>) is planted:
/// <see cref="Base"/>, the bottom of what is drawn, at the lowest ground under its footprint less
/// <see cref="Planting.Sink"/>, so no part of its base floats; and <see cref="High"/>, the highest
/// ground under it, where its visible height starts (its top stays at <see cref="High"/> plus its height).
/// </summary>
public readonly struct Footing
{
    public readonly float Base;
    public readonly float Low;
    public readonly float High;

    public Footing(float @base, float low, float high)
    {
        Base = @base;
        Low = low;
        High = high;
    }

    /// <summary>How far the drawn object reaches below the high side: the extra stem, plinth or skirt.</summary>
    public float Extension => High - Base;
}

/// <summary>
/// Plants objects on the Map's ground: samples <see cref="Ground.HeightAt"/> at the center and around the
/// footprint, and sets the base at the lowest sample sunk by <see cref="Sink"/>.
/// </summary>
public static class Planting
{
    /// <summary>How far every base goes into the ground below the lowest ground under it (meters).</summary>
    public const float Sink = 0.15f;

    /// <summary>Fewest points sampled on each ring of a round footprint (more on wide ones, <see cref="RimPointsFor"/>).</summary>
    public const int RimPoints = 16;

    /// <summary>Most spacing between a round footprint's samples (meters).</summary>
    public const float SampleSpacing = 0.5f;

    /// <summary>Points sampled along each side of a rectangular footprint (corners included).</summary>
    public const int SidePoints = 7;

    /// <summary>Round footprints at least this wide (radius, meters) get a root flare (<see cref="FlareRadius"/>).</summary>
    public const float FlareMinRadius = 0.5f;

    /// <summary>Points on the rim of a footprint of <paramref name="radius"/>: at least <see cref="RimPoints"/>, at most <see cref="SampleSpacing"/> apart.</summary>
    public static int RimPointsFor(float radius) => Math.Max(RimPoints, (int)MathF.Ceiling(MathF.Tau * radius / SampleSpacing));

    /// <summary>Rings sampled inside a footprint of <paramref name="radius"/> (the rim the last), at most a meter apart.</summary>
    public static int RingsFor(float radius) => Math.Max(2, (int)MathF.Ceiling(radius));

    /// <summary>Point <paramref name="k"/> of <paramref name="count"/> on a circle of <paramref name="radius"/> around (x, z).</summary>
    public static (float X, float Z) RimPoint(float x, float z, float radius, int k, int count)
    {
        float a = k * MathF.Tau / count;
        return (x + MathF.Sin(a) * radius, z + MathF.Cos(a) * radius);
    }

    /// <summary>Plants a round base of <paramref name="radius"/> centered on (x, z): its center and rings of points out to its rim.</summary>
    public static Footing Round(float x, float z, float radius)
    {
        float low = Ground.HeightAt(x, z), high = low;
        int rings = RingsFor(radius), count = RimPointsFor(radius);
        for (int ring = 1; ring <= rings; ring++)
        for (int k = 0; k < count; k++)
        {
            var (px, pz) = RimPoint(x, z, radius * ring / rings, k, count);
            float h = Ground.HeightAt(px, pz);
            low = MathF.Min(low, h);
            high = MathF.Max(high, h);
        }
        return new Footing(low - Sink, low, high);
    }

    /// <summary>
    /// A point of a building's footprint grid: <paramref name="i"/>, <paramref name="j"/> in
    /// 0..<see cref="SidePoints"/>-1 across its width and depth (edges included).
    /// </summary>
    public static System.Numerics.Vector2 GridPoint(in Building b, int i, int j)
    {
        float u = (float)i / (SidePoints - 1) - 0.5f, v = (float)j / (SidePoints - 1) - 0.5f;
        return b.ToWorld(u * b.Width, v * b.Depth);
    }

    /// <summary>Plants a building's whole rectangular footprint (a grid over it, its edges and corners).</summary>
    public static Footing Rect(in Building b)
    {
        float low = float.MaxValue, high = float.MinValue;
        for (int i = 0; i < SidePoints; i++)
        for (int j = 0; j < SidePoints; j++)
        {
            var p = GridPoint(b, i, j);
            float h = Ground.HeightAt(p.X, p.Y);
            low = MathF.Min(low, h);
            high = MathF.Max(high, h);
        }
        return new Footing(low - Sink, low, high);
    }

    /// <summary>
    /// The bottom radius of the root flare a round base of <paramref name="radius"/> gets (wider than
    /// it, widening into the ground), or 0 for a thin one, which gets none.
    /// </summary>
    public static float FlareRadius(float radius) => radius >= FlareMinRadius ? radius * 1.25f : 0f;
}
