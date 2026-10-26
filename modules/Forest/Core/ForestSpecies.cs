using System;
using System.Numerics;

namespace Fungtopia.Forest;

/// <summary>
/// The kinds of mushrooms and fungi the forest grows (Tony, 2026-10-26: "a variety of many
/// different types of mushrooms and fungi"), each cartoon-simplified, and how each looks: its cap
/// shapes, surface pattern, proportions, stem and colors (<see cref="ForestPalette"/>), and which
/// sizes of mushroom it may be (<see cref="Tier"/>). <see cref="ForestScenery"/> picks one per spot
/// from a fixed hash, never the same as its nearest neighbors'. Fixed data; nothing per tick.
/// </summary>
public static class ForestSpecies
{
    private static readonly MushroomLook[] s_looks =
    {
        // Fly agaric: round red caps (domes, older ones flatter) with crisp white spots.
        new(MushroomKind.FlyAgaric, Tier.All, new[] { CapShape.Dome, CapShape.Flat }, CapPattern.Spots, Habit.Any,
            Glows: false, Stemless: false, GiantCap: (2.6f, 3.6f), CapPerHeight: (0.42f, 0.58f), CapDepth: (0.5f, 0.75f),
            Slender: (0.75f, 0.95f), StemOfCap: 0.3f, GiantHeight: 1f, Underside: 0.15f),
        // Fat brown boletes / porcini: a fat bun of a cap on a fat stem.
        new(MushroomKind.Bolete, Tier.All, new[] { CapShape.Dome }, CapPattern.Plain, Habit.Any,
            Glows: false, Stemless: false, GiantCap: (1.9f, 2.5f), CapPerHeight: (0.5f, 0.62f), CapDepth: (0.6f, 0.85f),
            Slender: (0.95f, 1f), StemOfCap: 0.55f, GiantHeight: 0.78f, Underside: 0.55f),
        // Golden chanterelles: funnels with wavy rims, ridges running down the stem.
        new(MushroomKind.Chanterelle, Tier.All, new[] { CapShape.Funnel }, CapPattern.Plain, Habit.Any,
            Glows: false, Stemless: false, GiantCap: (2.2f, 3f), CapPerHeight: (0.38f, 0.52f), CapDepth: (0.45f, 0.65f),
            Slender: (0.7f, 0.88f), StemOfCap: 0.3f, GiantHeight: 0.9f, Underside: 0.75f),
        // Tall thin parasols with wide flat scaly caps.
        new(MushroomKind.Parasol, Tier.All, new[] { CapShape.Flat }, CapPattern.Scales, Habit.SingleOrPair,
            Glows: false, Stemless: false, GiantCap: (3.2f, 4.3f), CapPerHeight: (0.42f, 0.55f), CapDepth: (0.2f, 0.3f),
            Slender: (0.55f, 0.7f), StemOfCap: 0.14f, GiantHeight: 1.3f, Underside: 0.1f),
        // Shaggy ink caps: tall white shaggy bells, inky at the rim.
        new(MushroomKind.InkCap, Tier.All, new[] { CapShape.Bell }, CapPattern.Shaggy, Habit.Any,
            Glows: false, Stemless: false, GiantCap: (1.4f, 1.9f), CapPerHeight: (0.2f, 0.27f), CapDepth: (1.4f, 1.9f),
            Slender: (0.6f, 0.78f), StemOfCap: 0.32f, GiantHeight: 1.2f, Underside: 0.3f),
        // Morels: honeycomb cones on pale stems.
        new(MushroomKind.Morel, Tier.All, new[] { CapShape.Cone }, CapPattern.Honeycomb, Habit.Any,
            Glows: false, Stemless: false, GiantCap: (1.5f, 2f), CapPerHeight: (0.22f, 0.3f), CapDepth: (1.3f, 1.8f),
            Slender: (0.85f, 1f), StemOfCap: 0.6f, GiantHeight: 0.85f, Underside: 0.4f),
        // Enoki: tufts of tall thin stems with tiny caps.
        new(MushroomKind.Enoki, Tier.Small | Tier.Medium, new[] { CapShape.Dome }, CapPattern.Plain, Habit.Tuft,
            Glows: false, Stemless: false, GiantCap: (2f, 2f), CapPerHeight: (0.07f, 0.11f), CapDepth: (0.55f, 0.75f),
            Slender: (1f, 1f), StemOfCap: 0.28f, GiantHeight: 1f, Underside: 0.3f),
        // Amethyst deceivers: purple caps, wavy, domed or funnelled.
        new(MushroomKind.Amethyst, Tier.All, new[] { CapShape.Wavy, CapShape.Dome, CapShape.Funnel }, CapPattern.Plain, Habit.Any,
            Glows: false, Stemless: false, GiantCap: (2.4f, 3.2f), CapPerHeight: (0.38f, 0.52f), CapDepth: (0.4f, 0.6f),
            Slender: (0.7f, 0.9f), StemOfCap: 0.28f, GiantHeight: 1f, Underside: 0.6f),
        // Puffballs: round balls straight on the ground.
        new(MushroomKind.Puffball, Tier.Small | Tier.Medium, new[] { CapShape.Ball }, CapPattern.Spots, Habit.Any,
            Glows: false, Stemless: true, GiantCap: (1f, 1f), CapPerHeight: (0.5f, 0.6f), CapDepth: (2f, 2f),
            Slender: (1f, 1f), StemOfCap: 0.6f, GiantHeight: 1f, Underside: 0.2f),
        // Scarlet elf cups: open red cups on the ground.
        new(MushroomKind.CupFungus, Tier.Small, new[] { CapShape.Cup }, CapPattern.Plain, Habit.Any,
            Glows: false, Stemless: true, GiantCap: (1f, 1f), CapPerHeight: (0.9f, 1.3f), CapDepth: (2f, 2f),
            Slender: (1f, 1f), StemOfCap: 0.4f, GiantHeight: 1f, Underside: 0.6f),
        // Coral fungi: branching pink and orange bushes.
        new(MushroomKind.Coral, Tier.Small | Tier.Medium, new[] { CapShape.Coral }, CapPattern.Plain, Habit.SingleOrPair,
            Glows: false, Stemless: true, GiantCap: (1f, 1f), CapPerHeight: (0.5f, 0.65f), CapDepth: (2f, 2f),
            Slender: (1f, 1f), StemOfCap: 0.35f, GiantHeight: 1f, Underside: 1f),
        // Jelly fungi: glossy wobbly orange blobs.
        new(MushroomKind.Jelly, Tier.Small, new[] { CapShape.Wavy }, CapPattern.Jelly, Habit.Any,
            Glows: false, Stemless: true, GiantCap: (1f, 1f), CapPerHeight: (0.8f, 1.1f), CapDepth: (2f, 2f),
            Slender: (1f, 1f), StemOfCap: 0.6f, GiantHeight: 1f, Underside: 0.8f),
        // Bioluminescent ones: blue, green, cyan, violet and pink, clearly glowing at night.
        new(MushroomKind.Glowing, Tier.All, new[] { CapShape.Dome, CapShape.Bell, CapShape.Cone }, CapPattern.Spots, Habit.Any,
            Glows: true, Stemless: false, GiantCap: (2.3f, 3.3f), CapPerHeight: (0.3f, 0.5f), CapDepth: (0.5f, 0.95f),
            Slender: (0.65f, 0.9f), StemOfCap: 0.3f, GiantHeight: 1.05f, Underside: 0.3f),
    };

    /// <summary>Every kind's look, indexed by <see cref="MushroomKind"/>.</summary>
    public static ReadOnlySpan<MushroomLook> All => s_looks;

    /// <summary>How <paramref name="kind"/> looks.</summary>
    public static MushroomLook Of(MushroomKind kind) => s_looks[(int)kind];

    /// <summary>The kinds that may grow at <paramref name="tier"/>, in <see cref="MushroomKind"/> order.</summary>
    public static MushroomKind[] For(Tier tier) => Array.FindAll(Array.ConvertAll(s_looks, l => l.Kind), k => (Of(k).Tiers & tier) != 0);

    /// <summary>The colors <paramref name="kind"/>'s caps take (<see cref="ForestPalette"/>).</summary>
    public static Vector3[] Colors(MushroomKind kind) => kind switch
    {
        MushroomKind.FlyAgaric => ForestPalette.FlyAgaricCaps,
        MushroomKind.Bolete => ForestPalette.BoleteCaps,
        MushroomKind.Chanterelle => ForestPalette.ChanterelleCaps,
        MushroomKind.Parasol => ForestPalette.ParasolCaps,
        MushroomKind.InkCap => ForestPalette.InkCaps,
        MushroomKind.Morel => ForestPalette.MorelCaps,
        MushroomKind.Enoki => ForestPalette.EnokiCaps,
        MushroomKind.Amethyst => ForestPalette.AmethystCaps,
        MushroomKind.Puffball => ForestPalette.PuffballCaps,
        MushroomKind.CupFungus => ForestPalette.CupCaps,
        MushroomKind.Coral => ForestPalette.CoralCaps,
        MushroomKind.Jelly => ForestPalette.JellyCaps,
        _ => ForestPalette.GlowCaps,
    };
}

/// <summary>A kind of mushroom or fungus.</summary>
public enum MushroomKind
{
    FlyAgaric,
    Bolete,
    Chanterelle,
    Parasol,
    InkCap,
    Morel,
    Enoki,
    Amethyst,
    Puffball,
    CupFungus,
    Coral,
    Jelly,
    Glowing,
}

/// <summary>The cap (or, for stemless fungi, body) meshes the view draws.</summary>
public enum CapShape
{
    Dome,
    Flat,
    Funnel,
    Bell,
    Cone,
    Wavy,
    Ball,
    Cup,
    Coral,
}

/// <summary>What the cap shader paints on a cap's top.</summary>
public enum CapPattern
{
    Plain,
    Spots,
    Scales,
    Honeycomb,
    Shaggy,
    Bands,
    Jelly,
}

/// <summary>The sizes a kind may grow at: tiny ground fungi, the Map's medium props, giant trunks.</summary>
[Flags]
public enum Tier
{
    None = 0,
    Small = 1,
    Medium = 2,
    Giant = 4,
    All = Small | Medium | Giant,
}

/// <summary>How a kind grows on one spot: alone or in pairs, in clusters too, or in tufts of many thin stems.</summary>
public enum Habit
{
    SingleOrPair,
    Any,
    Tuft,
}

/// <summary>
/// How a kind looks. <c>GiantCap</c>: a giant's cap radius in stem radii; <c>CapPerHeight</c>: a
/// medium or tiny one's cap radius per meter of height; <c>CapDepth</c>: cap height per cap radius;
/// <c>Slender</c>: how much the stem narrows above its foot (1 not at all); <c>StemOfCap</c>: a
/// medium or tiny one's stem radius per cap radius; <c>GiantHeight</c>: a giant's height factor;
/// <c>Underside</c>: how much the cap's color tints its gills (or a cup's outside).
/// Stemless kinds are one body from the ground up (puffballs, cups, corals, jellies).
/// </summary>
public readonly record struct MushroomLook(
    MushroomKind Kind, Tier Tiers, CapShape[] Shapes, CapPattern Pattern, Habit Habit, bool Glows, bool Stemless,
    (float Min, float Max) GiantCap, (float Min, float Max) CapPerHeight, (float Min, float Max) CapDepth,
    (float Min, float Max) Slender, float StemOfCap, float GiantHeight, float Underside);
