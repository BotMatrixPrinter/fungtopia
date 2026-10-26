using System.Numerics;

namespace Fungtopia.Forest;

/// <summary>
/// The colors and cap shading of every mushroom the forest draws (giant, medium, ring and tiny
/// ones), as linear-ish RGB in 0..1 (X = red, Y = green, Z = blue). "Vivid" (Tony, 2026-10-25):
/// caps clearly saturated and bright (cherry/tomato reds, rich purples, oranges and browns) so
/// they pop against the greens and browns, crisp pure white spots, a soft glossy highlight and a
/// light rim on each cap, clean light cream stems, and glowing caps about twice as bright.
/// The view reads these; nothing else in the forest takes its colors from here.
/// </summary>
public static class ForestPalette
{
    /// <summary>Ordinary caps: cherry and tomato reds, warm oranges and browns, purples.</summary>
    public static readonly Vector3[] Caps =
    {
        new(0.94f, 0.06f, 0.1f), new(1f, 0.2f, 0.1f), new(0.88f, 0.04f, 0.16f),
        new(0.86f, 0.42f, 0.1f), new(0.95f, 0.58f, 0.16f), new(0.76f, 0.32f, 0.08f),
        new(0.66f, 0.2f, 0.94f), new(0.9f, 0.18f, 0.98f),
    };

    /// <summary>Glowing caps: violet, pink and cyan, and bioluminescent blue and green.</summary>
    public static readonly Vector3[] GlowCaps =
    {
        new(0.62f, 0.3f, 1f), new(1f, 0.3f, 0.82f), new(0.15f, 0.85f, 1f), new(0.12f, 0.45f, 1f), new(0.3f, 1f, 0.38f),
    };

    // Each kind's caps (ForestSpecies.Colors): saturated and bright, so the kinds read apart.

    /// <summary>Fly agarics: the three cherry/tomato reds of <see cref="Caps"/>.</summary>
    public static readonly Vector3[] FlyAgaricCaps = { Caps[0], Caps[1], Caps[2] };

    /// <summary>Boletes: warm chestnut and caramel browns.</summary>
    public static readonly Vector3[] BoleteCaps = { Caps[3], new(0.9f, 0.44f, 0.12f), new(0.86f, 0.36f, 0.1f) };

    /// <summary>Chanterelles: egg-yolk gold and apricot.</summary>
    public static readonly Vector3[] ChanterelleCaps = { new(1f, 0.72f, 0.06f), new(1f, 0.6f, 0.04f), new(0.98f, 0.8f, 0.14f) };

    /// <summary>Parasols: toasted caramel (the scales a darker shade of it).</summary>
    public static readonly Vector3[] ParasolCaps = { new(0.95f, 0.6f, 0.22f), new(0.9f, 0.52f, 0.18f) };

    /// <summary>Ink caps: white shaggy bells; this is the ink at their rims, violet and blue.</summary>
    public static readonly Vector3[] InkCaps = { new(0.45f, 0.16f, 0.9f), new(0.2f, 0.25f, 0.85f) };

    /// <summary>Morels: honey and amber honeycombs.</summary>
    public static readonly Vector3[] MorelCaps = { new(0.95f, 0.62f, 0.18f), new(0.85f, 0.5f, 0.12f) };

    /// <summary>Enoki: butter yellow and peach tiny caps.</summary>
    public static readonly Vector3[] EnokiCaps = { new(1f, 0.88f, 0.3f), new(1f, 0.7f, 0.42f) };

    /// <summary>Amethyst deceivers: the purples of <see cref="Caps"/> and a deep violet.</summary>
    public static readonly Vector3[] AmethystCaps = { Caps[6], Caps[7], new(0.5f, 0.12f, 1f) };

    /// <summary>Puffballs: warm peach and soft apricot balls.</summary>
    public static readonly Vector3[] PuffballCaps = { new(1f, 0.78f, 0.45f), new(1f, 0.66f, 0.42f) };

    /// <summary>Scarlet elf cups.</summary>
    public static readonly Vector3[] CupCaps = { new(1f, 0.05f, 0.12f), new(1f, 0.16f, 0.1f) };

    /// <summary>Coral fungi: bubblegum pink, orange and coral.</summary>
    public static readonly Vector3[] CoralCaps = { new(1f, 0.35f, 0.6f), new(1f, 0.5f, 0.12f), new(1f, 0.42f, 0.32f) };

    /// <summary>Jelly fungi: glossy orange and lemon.</summary>
    public static readonly Vector3[] JellyCaps = { new(1f, 0.55f, 0.04f), new(1f, 0.82f, 0.08f) };

    /// <summary>Shelf (bracket) fungi on the giant stems: tangerine and russet, banded.</summary>
    public static readonly Vector3[] ShelfCaps = { new(1f, 0.55f, 0.12f), new(0.9f, 0.36f, 0.1f), new(1f, 0.74f, 0.2f) };

    /// <summary>The Deep Forest's dark caps near the west gap: deep but rich purples, blues and dusky reds.</summary>
    public static readonly Vector3[] DarkCaps =
    {
        new(0.42f, 0.14f, 0.62f), new(0.22f, 0.2f, 0.62f), new(0.6f, 0.1f, 0.3f), new(0.1f, 0.32f, 0.56f),
    };

    /// <summary>Stems run between these two clean light creams (the root flare a shade darker).</summary>
    public static readonly Vector3 StemLight = new(1f, 0.97f, 0.88f);

    public static readonly Vector3 StemShade = new(0.98f, 0.93f, 0.8f);

    /// <summary>The caps' spots: crisp pure white.</summary>
    public static readonly Vector3 Spot = new(1f, 1f, 1f);

    /// <summary>The pale gills under the caps.</summary>
    public static readonly Vector3 Gills = new(1f, 0.93f, 0.8f);

    /// <summary>Strength of the soft glossy band on the lit side of each cap (white specular).</summary>
    public const float CapGloss = 0.32f;

    /// <summary>Strength of the light rim along each cap's silhouette (the toon light's rim is 0.18).</summary>
    public const float CapRim = 0.42f;

    /// <summary>How much the glowing caps' emission is scaled at night: twice what it was (1).</summary>
    public const float GlowEmission = 2f;

    /// <summary>
    /// The glow by day, as a part of <see cref="GlowEmission"/>: half, so by day glowing caps shine
    /// as before (GlowEmission x DayGlow = 1) and do not wash out to white in the sun.
    /// </summary>
    public const float DayGlow = 0.5f;

    // The rest of the world the forest draws (Tony, 2026-10-26: "make the whole world more vibrant
    // and colorful"): lush saturated greens with yellow-green highlights, warm golden paths, many
    // flower hues; never washed out to white or neon.

    /// <summary>The forest floor's grass, its yellow-green sunny patches, moss and the clearings' meadows.</summary>
    public static readonly Vector3 Grass = new(0.32f, 0.74f, 0.1f);

    public static readonly Vector3 GrassLight = new(0.6f, 0.84f, 0.12f);

    public static readonly Vector3 Moss = new(0.12f, 0.56f, 0.2f);

    public static readonly Vector3 Meadow = new(0.44f, 0.78f, 0.1f);

    /// <summary>The dirt paths: warm golden, clearly lighter than the grass.</summary>
    public static readonly Vector3 Dirt = new(1f, 0.74f, 0.22f);

    /// <summary>The overgrown trail's soil, darker toward the Deep Forest.</summary>
    public static readonly Vector3 Soil = new(0.66f, 0.44f, 0.2f);

    public static readonly Vector3 SoilDark = new(0.42f, 0.3f, 0.18f);

    /// <summary>Ferns run between these two greens.</summary>
    public static readonly Vector3 FernDark = new(0.12f, 0.6f, 0.16f);

    public static readonly Vector3 FernLight = new(0.45f, 0.82f, 0.1f);

    /// <summary>Flower petals: many clear hues.</summary>
    public static readonly Vector3[] Flowers =
    {
        new(1f, 0.98f, 0.92f), new(1f, 0.85f, 0.1f), new(1f, 0.35f, 0.62f), new(0.55f, 0.45f, 1f), new(1f, 0.45f, 0.2f),
        new(0.95f, 0.15f, 0.25f), new(0.3f, 0.65f, 1f), new(0.8f, 0.3f, 1f), new(1f, 0.5f, 0.82f), new(0.2f, 0.85f, 0.95f),
    };

    /// <summary>The flowers' stems.</summary>
    public static readonly Vector3 FlowerStem = new(0.22f, 0.62f, 0.12f);

    /// <summary>How much more the glowing caps shine at night than they did by day before (night emission = GlowEmission x NightGlow).</summary>
    public const float NightGlow = 2.4f;
}
