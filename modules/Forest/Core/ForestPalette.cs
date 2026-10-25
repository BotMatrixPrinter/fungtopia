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

    /// <summary>Glowing caps: violet, pink and cyan.</summary>
    public static readonly Vector3[] GlowCaps =
    {
        new(0.62f, 0.3f, 1f), new(1f, 0.3f, 0.82f), new(0.15f, 0.85f, 1f),
    };

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

    /// <summary>How much more the glowing caps shine at night than they did by day before (night emission = GlowEmission x NightGlow).</summary>
    public const float NightGlow = 2.4f;
}
