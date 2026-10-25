namespace Fungtopia.Food;

/// <summary>
/// How the foods' surfaces (food.glb's materials, by name) are shaded: the mushrooms' glossy
/// highlight and rim light, and how brightly the pie's spores glow. The view reads it; the colors
/// themselves are in Models/food.py.
/// </summary>
public static class FoodLook
{
    /// <summary>The mushrooms' parts: caps, stems and white spots.</summary>
    public static bool IsMushroom(string surface) => surface is "Cap" or "Stem" or "Spots";

    /// <summary>The rim light of every surface but a mushroom cap.</summary>
    public const float Rim = 0.3f;

    /// <summary>The light rim along a mushroom cap's edge.</summary>
    public const float CapRim = 0.6f;

    /// <summary>The soft specular band on a mushroom cap (and its spots).</summary>
    public const float CapGloss = 0.4f;

    /// <summary>Emission of glowing bits (the pie's glow spores); twice the old 1.2.</summary>
    public const float GlowStrength = 2.4f;

    /// <summary>Strength of the glossy highlight on <paramref name="surface"/> (0: none).</summary>
    public static float Gloss(string surface) => surface is "Cap" or "Spots" ? CapGloss : 0f;

    /// <summary>Strength of the rim light on <paramref name="surface"/>.</summary>
    public static float RimLight(string surface) => surface is "Cap" ? CapRim : Rim;

    /// <summary>Emission of <paramref name="surface"/> (0: it does not glow).</summary>
    public static float Glow(string surface) => surface is "Glow" ? GlowStrength : 0f;
}
