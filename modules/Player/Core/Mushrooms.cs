using System.Numerics;

namespace Fungtopia.Player;

/// <summary>
/// The mushroom parts the character carries, drawn "vivid" (Tony, 2026-10-25; the same rule for
/// every mushroom in the game): the bow's mushroom-red tips (material <see cref="Red"/>) and the
/// nocked arrow's poison mushroom tip (a purple <see cref="PoisonCap"/> with glowing green
/// <see cref="PoisonSpots"/> on a cream <see cref="Stem"/>). characters.py gives the materials
/// these colors (RGB 0..1, as in its COLORS); the view gives the caps a glossy highlight and a
/// rim light, and the glow spots their glow.
/// </summary>
public static class Mushrooms
{
    /// <summary>Material names.</summary>
    public const string Red = "MushroomRed", PoisonCap = "PoisonCap", PoisonSpots = "PoisonSpots", Stem = "Stem";

    /// <summary>The bow tips: a pure tomato red.</summary>
    public static readonly Vector3 RedColor = new(0.96f, 0.1f, 0.06f);
    /// <summary>The arrow's poison cap: a rich, bright purple.</summary>
    public static readonly Vector3 PoisonCapColor = new(0.62f, 0.12f, 0.95f);
    /// <summary>The poison cap's glow spots: a bright toxic green.</summary>
    public static readonly Vector3 PoisonSpotsColor = new(0.5f, 1.0f, 0.25f);
    /// <summary>The arrow's mushroom stem: a clean light cream.</summary>
    public static readonly Vector3 StemColor = new(1.0f, 0.96f, 0.86f);

    /// <summary>Strength of the soft specular band on a cap (the toon shader's <c>gloss</c>).</summary>
    public const float Gloss = 0.55f;
    /// <summary>Extra rim light along a cap's edge (the toon shader's <c>rim_light</c>).</summary>
    public const float RimLight = 0.35f;
    /// <summary>The glow spots' emission (the toon shader's <c>self_light</c>): twice the old 0.6.</summary>
    public const float GlowLight = 1.2f;

    /// <summary>Whether the material is a mushroom cap (glossy, rim lit).</summary>
    public static bool IsCap(string material) => material is Red or PoisonCap;

    /// <summary>Whether the material glows (the cap's glow spots).</summary>
    public static bool Glows(string material) => material is PoisonSpots;

    /// <summary>The color characters.py gives a mushroom material, or null for any other.</summary>
    public static Vector3? Color(string material) => material switch
    {
        Red => RedColor,
        PoisonCap => PoisonCapColor,
        PoisonSpots => PoisonSpotsColor,
        Stem => StemColor,
        _ => null,
    };
}
