using System;

namespace Fungtopia.PetCity;

/// <summary>A color swatch as the models carry it (sRGB, 0..1 per channel).</summary>
public readonly record struct Swatch(float R, float G, float B)
{
    public float Max => MathF.Max(R, MathF.Max(G, B));
    public float Min => MathF.Min(R, MathF.Min(G, B));

    /// <summary>HSV value (brightness): the strongest channel.</summary>
    public float Value => Max;

    /// <summary>HSV saturation: 0 for grays and white, 1 for pure colors.</summary>
    public float Saturation => Max <= 0f ? 0f : (Max - Min) / Max;
}

/// <summary>
/// The look of Pet City's mushrooms (the game's "vivid" rule): caps in saturated, bright colors
/// with a soft glossy highlight and a light rim, crisp pure white spots, clean light cream stems,
/// and glowing lamp bulbs twice as bright at night. Only mushroom materials are listed here; every
/// other color of the city is the model's or the view's own.
/// </summary>
public static class PetCityLook
{
    /// <summary>The little mushrooms' caps (potted mushrooms, the water tower, the pets' mushroom hats): cherry red.</summary>
    public static readonly Swatch Cap = new(1f, 0.12f, 0.12f);

    /// <summary>The pet store's big spotted cap roof and the city lamps' caps: bright pink.</summary>
    public static readonly Swatch StoreCap = new(1f, 0.3f, 0.6f);

    /// <summary>White spots on every cap.</summary>
    public static readonly Swatch Spots = new(1f, 1f, 1f);

    /// <summary>Mushroom stems: a clean light cream.</summary>
    public static readonly Swatch Stem = new(1f, 0.96f, 0.86f);

    /// <summary>The city lamps' glowing bulbs (under their spotted caps).</summary>
    public static readonly Swatch LampGlow = new(1f, 0.9f, 0.58f);

    /// <summary>
    /// A city block's big spotted mushroom cap roof (<c>RoofCap</c>), per style: richer, brighter
    /// versions of the style's roof color (red, orange, blue, pink).
    /// </summary>
    public static readonly Swatch[] StyleCaps =
    {
        new(1f, 0.1f, 0.16f),
        new(1f, 0.5f, 0.08f),
        new(0.2f, 0.42f, 1f),
        new(1f, 0.28f, 0.62f),
    };

    /// <summary>The glb materials that draw mushrooms (caps, spots, stems, the lamps' glowing bulbs).</summary>
    public static readonly string[] MushroomMaterials = { "Cap", "CapRoof", "StoreCap", "Spots", "Stem", "LampGlow" };

    /// <summary>The cap materials, drawn with <see cref="CapGloss"/> and <see cref="CapRim"/>.</summary>
    public static readonly string[] CapMaterials = { "Cap", "CapRoof", "StoreCap" };

    /// <summary>Strength of the soft specular band on each cap (0 for every other surface).</summary>
    public const float CapGloss = 0.4f;

    /// <summary>Strength of the light rim along each cap's edge (0 for every other surface).</summary>
    public const float CapRim = 0.35f;

    /// <summary>The emission of the other glowing surfaces at night (the store's ceiling lamps), and of the lamp bulbs before.</summary>
    public const float GlowNight = 1.6f;

    /// <summary>The lamp bulbs' emission at night: twice <see cref="GlowNight"/>, so they clearly glow.</summary>
    public const float LampGlowNight = 2f * GlowNight;

    /// <summary>The lamp bulbs' (and other glow's) emission in daylight.</summary>
    public const float GlowDay = 0.5f;

    public static bool IsMushroom(string material) => Array.IndexOf(MushroomMaterials, material) >= 0;

    public static bool IsCap(string material) => Array.IndexOf(CapMaterials, material) >= 0;

    /// <summary>The color of a city block's <c>RoofCap</c> in style <paramref name="style"/>.</summary>
    public static Swatch StyleCap(int style) => StyleCaps[style % StyleCaps.Length];
}
