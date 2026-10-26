using System;
using System.Collections.Generic;

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

    /// <summary>HSV hue in degrees (0..360; 0 for grays).</summary>
    public float Hue
    {
        get
        {
            float d = Max - Min;
            if (d <= 0f)
                return 0f;
            float h = Max == R ? (G - B) / d : Max == G ? 2f + (B - R) / d : 4f + (R - G) / d;
            return (h * 60f + 360f) % 360f;
        }
    }
}

/// <summary>
/// The look of Pet City: its mushrooms (the game's "vivid" rule): caps in saturated, bright colors
/// with a soft glossy highlight and a light rim, crisp pure white spots, clean light cream stems,
/// and glowing lamp bulbs twice as bright at night; and the rest of the city's paints, candy-bright
/// (<see cref="Paints"/>, <see cref="StyleWalls"/>, <see cref="StyleRoofs"/>).
/// </summary>
public static class PetCityLook
{
    /// <summary>The roof terrace's mushroom water tower and the pets' mushroom hats: cherry red.</summary>
    public static readonly Swatch Cap = new(1f, 0.12f, 0.12f);

    /// <summary>The pet store's big spotted cap roof only: bright pink.</summary>
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

    /// <summary>
    /// The city lamps' mushroom caps (glb <c>LampCap</c>, recolored per lamp by
    /// <see cref="PetCityFungi.LampCapOf"/>): pink, cherry red, orange, purple, sky blue, golden yellow.
    /// </summary>
    public static readonly Swatch[] LampCaps =
    {
        new(1f, 0.3f, 0.6f),
        new(1f, 0.14f, 0.16f),
        new(1f, 0.5f, 0.1f),
        new(0.7f, 0.3f, 1f),
        new(0.2f, 0.62f, 1f),
        new(1f, 0.78f, 0.08f),
    };

    /// <summary>The glb materials that draw mushrooms (caps, spots, stems, the lamps' glowing bulbs, the little fungi's parts).</summary>
    public static readonly string[] MushroomMaterials =
        { "Cap", "CapRoof", "StoreCap", "LampCap", "FungusCap", "FungusGlow", "Spots", "Stem", "LampGlow", "MorelPit", "Ink" };

    /// <summary>The cap materials, drawn with <see cref="CapGloss"/> and <see cref="CapRim"/>.</summary>
    public static readonly string[] CapMaterials = { "Cap", "CapRoof", "StoreCap", "LampCap", "FungusCap", "FungusGlow" };

    /// <summary>
    /// The little fungi's materials that are white in the glb and tinted per mushroom
    /// (<see cref="Fungus.Tint"/>); <c>FungusGlow</c> also glows (<see cref="FungusGlowDay"/>, <see cref="FungusGlowNight"/>).
    /// </summary>
    public static readonly string[] TintedMaterials = { "FungusCap", "FungusGlow" };

    /// <summary>The bioluminescent fungi's glow in daylight: a soft shine.</summary>
    public const float FungusGlowDay = 0.35f;

    /// <summary>The bioluminescent fungi's glow at night: clearly glowing, like the lamp bulbs.</summary>
    public const float FungusGlowNight = 2.4f;

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

    // The rest of the city (the "vibrant world" rule): candy-bright paints, clean bright stone, lush
    // greens with yellow-green highlights, flowers in many hues; never washed out to white or neon.

    /// <summary>
    /// The glb's other materials, by name, as petcity.py builds them (the tests check the .glb against
    /// these). The pets' own colors (fur, eyes, ...) are not listed: they stay as they were.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, Swatch> Paints = new Dictionary<string, Swatch>
    {
        ["Wall"] = new(1f, 0.7f, 0.56f),
        ["Roof"] = new(1f, 0.32f, 0.42f),
        ["Trim"] = new(1f, 0.96f, 0.86f),
        ["Plinth"] = new(0.84f, 0.8f, 0.92f),
        ["Window"] = new(0.5f, 0.8f, 1f),
        ["Frame"] = new(1f, 0.99f, 0.96f),
        ["Door"] = new(0.86f, 0.44f, 0.2f),
        ["Awning"] = new(1f, 0.34f, 0.5f),
        ["AwningStripe"] = new(1f, 0.98f, 0.92f),
        ["FlowerBox"] = new(0.86f, 0.5f, 0.26f),
        ["Leaf"] = new(0.28f, 0.82f, 0.22f),
        ["LeafLight"] = new(0.64f, 0.92f, 0.16f),
        ["LeafDeep"] = new(0.1f, 0.68f, 0.32f),
        ["Flower"] = new(1f, 0.84f, 0.1f),
        ["FlowerPink"] = new(1f, 0.36f, 0.68f),
        ["FlowerBlue"] = new(0.32f, 0.56f, 1f),
        ["FlowerPurple"] = new(0.7f, 0.36f, 1f),
        ["FlowerOrange"] = new(1f, 0.52f, 0.12f),
        ["StoreWall"] = new(0.62f, 0.88f, 1f),
        ["StoreRoof"] = new(1f, 0.56f, 0.72f),
        ["FloorTile"] = new(1f, 0.92f, 0.76f),
        ["SignBoard"] = new(1f, 0.98f, 0.9f),
        ["SignTrim"] = new(1f, 0.4f, 0.62f),
        ["SignText"] = new(0.94f, 0.18f, 0.42f),
        ["Heart"] = new(1f, 0.3f, 0.5f),
        ["Glow"] = new(1f, 0.9f, 0.58f),
        ["Wood"] = new(0.9f, 0.56f, 0.28f),
        ["WoodDark"] = new(0.66f, 0.36f, 0.18f),
        ["CounterTop"] = new(1f, 0.62f, 0.74f),
        ["Bag1"] = new(0.3f, 0.6f, 1f),
        ["Bag2"] = new(1f, 0.78f, 0.2f),
        ["Bag3"] = new(0.4f, 0.86f, 0.36f),
        ["Bowl"] = new(1f, 0.32f, 0.38f),
        ["Cushion"] = new(0.72f, 0.54f, 1f),
        ["Bars"] = new(1f, 0.82f, 0.3f),
        ["Straw"] = new(1f, 0.86f, 0.42f),
        ["Stone"] = new(0.92f, 0.9f, 0.97f),
        ["Bronze"] = new(0.36f, 0.86f, 0.72f),
        ["Pot"] = new(1f, 0.52f, 0.32f),
    };

    /// <summary>The paints that are meant to be light neutrals (cream, white, stone), not colors.</summary>
    public static readonly string[] Neutrals = { "Trim", "Frame", "AwningStripe", "SignBoard", "FloorTile", "Plinth", "Stone" };

    /// <summary>The greens of leaves and moss: lush, a yellow-green highlight and a deeper blue-green.</summary>
    public static readonly string[] Greens = { "Leaf", "LeafLight", "LeafDeep" };

    /// <summary>The flowers' hues: yellow, pink, blue, purple, orange.</summary>
    public static readonly string[] Flowers = { "Flower", "FlowerPink", "FlowerBlue", "FlowerPurple", "FlowerOrange" };

    /// <summary>A city block's walls per style (candy mint, lilac, lemon, coral).</summary>
    public static readonly Swatch[] StyleWalls =
    {
        new(0.52f, 0.95f, 0.76f),
        new(0.78f, 0.6f, 1f),
        new(1f, 0.86f, 0.3f),
        new(1f, 0.7f, 0.52f),
    };

    /// <summary>A city block's roof and parapet per style (strawberry, tangerine, blueberry, sky).</summary>
    public static readonly Swatch[] StyleRoofs =
    {
        new(1f, 0.28f, 0.4f),
        new(1f, 0.56f, 0.18f),
        new(0.34f, 0.5f, 1f),
        new(0.36f, 0.7f, 1f),
    };

    /// <summary>Clean bright stone for the plinths (a hint of each building's color is mixed in by the view).</summary>
    public static readonly Swatch PlinthStone = new(0.88f, 0.86f, 0.94f);

    /// <summary>The planters' root flare (terracotta) and the statue's (stone).</summary>
    public static readonly Swatch PlanterFlare = new(1f, 0.6f, 0.4f), StatueFlare = new(0.86f, 0.86f, 0.92f);

    public static bool IsMushroom(string material) => Array.IndexOf(MushroomMaterials, material) >= 0;

    public static bool IsCap(string material) => Array.IndexOf(CapMaterials, material) >= 0;

    /// <summary>The color of a city block's <c>RoofCap</c> in style <paramref name="style"/>.</summary>
    public static Swatch StyleCap(int style) => StyleCaps[style % StyleCaps.Length];
}
