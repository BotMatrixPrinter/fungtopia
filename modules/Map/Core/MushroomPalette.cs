using System;
using System.Numerics;

namespace Fungtopia.Map;

/// <summary>
/// The colors and glow of the mushrooms the Map draws (giant mushrooms and fungi on the cliffs, the
/// little mushroom lamps beside the roads), as sRGB 0..1 triples. "Vivid" (Tony, 2026-10-25,
/// the same rule for every mushroom in the game): caps clearly saturated and bright, spots pure
/// white, stems a clean light cream, glowing mushrooms about twice as bright as before. Each kind
/// (Tony, 2026-10-26) has its own recognizable colors.
/// </summary>
public static class MushroomPalette
{
    /// <summary>Fly agaric caps: cherry and tomato red.</summary>
    public static readonly Vector3[] FlyAgaric = { new(0.96f, 0.08f, 0.12f), new(1.0f, 0.24f, 0.1f) };
    /// <summary>Fat boletes: caramel and chestnut brown.</summary>
    public static readonly Vector3[] Porcini = { new(0.9f, 0.5f, 0.16f), new(0.84f, 0.4f, 0.12f) };
    /// <summary>Chanterelle funnels: golden yellow and apricot.</summary>
    public static readonly Vector3[] Chanterelle = { new(1.0f, 0.74f, 0.08f), new(1.0f, 0.6f, 0.1f) };
    /// <summary>Parasols: warm tan-orange (white scales on top).</summary>
    public static readonly Vector3[] Parasol = { new(1.0f, 0.66f, 0.32f), new(0.96f, 0.56f, 0.26f) };
    /// <summary>Shaggy ink caps, cartoon-tinted: candy pink and lilac.</summary>
    public static readonly Vector3[] InkCap = { new(1.0f, 0.36f, 0.68f), new(0.68f, 0.38f, 1.0f) };
    /// <summary>Morels: honey gold.</summary>
    public static readonly Vector3[] Morel = { new(0.96f, 0.68f, 0.26f), new(0.9f, 0.58f, 0.2f) };
    /// <summary>Enoki's tiny caps: butter and peach.</summary>
    public static readonly Vector3[] Enoki = { new(1.0f, 0.84f, 0.34f), new(1.0f, 0.58f, 0.36f) };
    /// <summary>Amethyst caps: violet and orchid pink.</summary>
    public static readonly Vector3[] Amethyst = { new(0.66f, 0.16f, 0.98f), new(0.92f, 0.18f, 1.0f) };
    /// <summary>Puffballs: soft cream with a peach blush (the one pale kind).</summary>
    public static readonly Vector3[] Puffball = { new(1.0f, 0.95f, 0.84f), new(1.0f, 0.9f, 0.8f) };
    /// <summary>Scarlet cups: scarlet and raspberry.</summary>
    public static readonly Vector3[] ScarletCup = { new(1.0f, 0.1f, 0.18f), new(1.0f, 0.16f, 0.36f) };
    /// <summary>Coral fungi: coral pink and tangerine.</summary>
    public static readonly Vector3[] Coral = { new(1.0f, 0.34f, 0.5f), new(1.0f, 0.52f, 0.16f) };
    /// <summary>Jelly fungi: amber orange and lemon.</summary>
    public static readonly Vector3[] Jelly = { new(1.0f, 0.56f, 0.04f), new(1.0f, 0.82f, 0.1f) };
    /// <summary>Bioluminescent caps: cyan blue and mint green.</summary>
    public static readonly Vector3[] GlowCap = { new(0.1f, 0.8f, 1.0f), new(0.24f, 1.0f, 0.5f) };
    /// <summary>Shelf fungi on the big stems: orange and ochre.</summary>
    public static readonly Vector3[] Bracket = { new(1.0f, 0.56f, 0.18f), new(0.94f, 0.7f, 0.2f) };

    /// <summary>The cap colors of <paramref name="kind"/>.</summary>
    public static Vector3[] Colors(FungusKind kind) => kind switch
    {
        FungusKind.FlyAgaric => FlyAgaric,
        FungusKind.Porcini => Porcini,
        FungusKind.Chanterelle => Chanterelle,
        FungusKind.Parasol => Parasol,
        FungusKind.InkCap => InkCap,
        FungusKind.Morel => Morel,
        FungusKind.Enoki => Enoki,
        FungusKind.Amethyst => Amethyst,
        FungusKind.Puffball => Puffball,
        FungusKind.ScarletCup => ScarletCup,
        FungusKind.Coral => Coral,
        FungusKind.Jelly => Jelly,
        FungusKind.GlowCap => GlowCap,
        FungusKind.Bracket => Bracket,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Lamp caps: warm golden, candy pink, sky blue, mint, violet and tangerine.</summary>
    public static readonly Vector3[] LampColors =
    {
        new(1.0f, 0.78f, 0.12f), new(1.0f, 0.16f, 0.48f), new(0.14f, 0.7f, 1.0f),
        new(0.2f, 1.0f, 0.55f), new(0.66f, 0.18f, 1.0f), new(1.0f, 0.5f, 0.1f),
    };

    /// <summary>The two ends of the stems' clean light cream (each stem picks a shade between).</summary>
    public static readonly Vector3 StemLight = new(1.0f, 0.97f, 0.88f);
    public static readonly Vector3 StemDark = new(0.98f, 0.92f, 0.8f);

    /// <summary>The caps' spots: crisp pure white.</summary>
    public static readonly Vector3 SpotColor = new(1f, 1f, 1f);

    /// <summary>Emission of a glowing cliff cap (the bioluminescent kind) and of a lamp (was 1.1 and 1.4).</summary>
    public const float GiantGlow = 2.2f;
    public const float LampGlow = 2.8f;
}
