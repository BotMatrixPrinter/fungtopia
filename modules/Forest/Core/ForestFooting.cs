using System;
using Fungtopia.Map;

namespace Fungtopia.Forest;

/// <summary>
/// How the view plants things on the uneven ground (<see cref="Ground.HeightAt"/>) so they never
/// float: the ground is sampled at the footprint's center, <see cref="RimSamples"/> points on its
/// radius and <see cref="InnerSamples"/> at half of it, and the base goes at the lowest of them,
/// a small sink deeper. A mushroom's stem reaches down to that base while its cap stays where it
/// would stand on the ground under its center (so the stem above the high side looks the same),
/// lifted only if the ground under the cap's rim would hide it. Pure; allocation-free.
/// </summary>
public static class ForestFooting
{
    /// <summary>Ground samples on the footprint's radius.</summary>
    public const int RimSamples = 16;

    /// <summary>Ground samples at half the footprint's radius.</summary>
    public const int InnerSamples = 8;

    /// <summary>How far (meters) a base sinks below the lowest ground under it (less for small things, see <see cref="SinkFor"/>).</summary>
    public const float Sink = 0.15f;

    /// <summary>Stems at least this wide (meters in radius) get a root flare (<see cref="PlantedMushroom.SkirtHeight"/>).</summary>
    public const float FlareMinRadius = 0.6f;

    /// <summary>The skirt's top, as a fraction of the stem's radius (<see cref="ForestScenery.SkirtProfile"/> at 1).</summary>
    public const float SkirtTopRadius = 0.55f;

    /// <summary>The height fraction of the skirt up to which it keeps its full width; the high side of the foot's ground is at or below it.</summary>
    public const float SkirtGroundFraction = 0.5f;

    /// <summary>The most of the stem's height the skirt covers.</summary>
    public const float SkirtMaxFraction = 0.8f;

    /// <summary>The sink for something <paramref name="height"/> tall: <see cref="Sink"/>, or 15 % of its height when smaller.</summary>
    public static float SinkFor(float height) => MathF.Min(Sink, 0.15f * height);

    /// <summary>The lowest ground at (x, z) and on its footprint of <paramref name="radius"/>.</summary>
    public static float Lowest(float x, float z, float radius)
    {
        float low = Ground.HeightAt(x, z);
        for (int k = 0; k < RimSamples; k++)
        {
            float a = k * MathF.Tau / RimSamples;
            low = MathF.Min(low, Ground.HeightAt(x + MathF.Cos(a) * radius, z + MathF.Sin(a) * radius));
        }
        for (int k = 0; k < InnerSamples; k++)
        {
            float a = (k + 0.5f) * MathF.Tau / InnerSamples;
            low = MathF.Min(low, Ground.HeightAt(x + MathF.Cos(a) * radius * 0.5f, z + MathF.Sin(a) * radius * 0.5f));
        }
        return low;
    }

    /// <summary>The highest ground at (x, z) and on its footprint of <paramref name="radius"/>.</summary>
    public static float Highest(float x, float z, float radius)
    {
        float high = Ground.HeightAt(x, z);
        for (int k = 0; k < RimSamples; k++)
        {
            float a = k * MathF.Tau / RimSamples;
            high = MathF.Max(high, Ground.HeightAt(x + MathF.Cos(a) * radius, z + MathF.Sin(a) * radius));
        }
        for (int k = 0; k < InnerSamples; k++)
        {
            float a = (k + 0.5f) * MathF.Tau / InnerSamples;
            high = MathF.Max(high, Ground.HeightAt(x + MathF.Cos(a) * radius * 0.5f, z + MathF.Sin(a) * radius * 0.5f));
        }
        return high;
    }

    /// <summary>Where the base of something <paramref name="height"/> tall with a footprint of <paramref name="radius"/> at (x, z) goes: at or below all the ground under it.</summary>
    public static float BaseY(float x, float z, float radius, float height) => Lowest(x, z, radius) - SinkFor(height);

    /// <summary>
    /// Plants <paramref name="m"/>: its stem from the base (under the lowest ground of its foot)
    /// up to its cap, the cap's top <see cref="DrawnMushroom.Height"/> above the ground at its
    /// center (higher only where the ground under the cap's rim rises close to it).
    /// </summary>
    public static PlantedMushroom Plant(in DrawnMushroom m)
    {
        float ground = Ground.HeightAt(m.X, m.Z);
        float baseY = BaseY(m.X, m.Z, m.StemRadius, m.Height);
        float capY = ground + m.Height - m.CapHeight;
        // The cap's rim (0.93 of its radius, a little above its origin) keeps clear of the ground under it.
        float clearance = MathF.Min(0.3f, 0.25f * (m.Height - m.CapHeight));
        capY = MathF.Max(capY, Highest(m.X, m.Z, m.CapRadius * 0.93f) + clearance);
        float stemTop = capY + m.CapHeight * 0.18f;
        float skirt = 0f;
        if (m.StemRadius >= FlareMinRadius)
        {
            // Full width from the base up to the high side of the foot (at most SkirtGroundFraction of
            // the skirt), then narrowing into the stem above it, over at most SkirtMaxFraction of the stem.
            float depth = Highest(m.X, m.Z, m.StemRadius) - baseY;
            skirt = MathF.Min(MathF.Max(depth / SkirtGroundFraction, depth + 0.4f * m.StemRadius), (stemTop - baseY) * SkirtMaxFraction);
        }
        return new PlantedMushroom(baseY, stemTop - baseY, capY, skirt);
    }
}

/// <summary>
/// A mushroom planted on the ground: its stem from <see cref="BaseY"/> up <see cref="StemHeight"/>
/// meters, its cap's origin (the bottom of the cap mesh) at <see cref="CapY"/>, and for big stems a
/// root flare <see cref="SkirtHeight"/> tall from the base (0: none).
/// </summary>
public readonly record struct PlantedMushroom(float BaseY, float StemHeight, float CapY, float SkirtHeight);
