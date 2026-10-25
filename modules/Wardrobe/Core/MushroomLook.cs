using System.Numerics;

namespace Fungtopia.Wardrobe;

/// <summary>
/// How the mushrooms on clothes look (the game's "vivid mushrooms" rule): caps a saturated cherry
/// red, crisp pure white spots, clean light cream stems and gills, and in the toon shading a soft
/// glossy highlight band on the caps and a light rim along the mushroom's edge. The model scripts
/// use these colors (sRGB, like a Blender swatch); <see cref="Gloss"/> and <see cref="Rim"/> are
/// what the view gives each surface by its glTF material name. Every other material is untouched.
/// </summary>
public static class MushroomLook
{
    /// <summary>A mushroom cap's red (the hat, the backpack, the hoodie's pocket mushroom, the spotted dress).</summary>
    public static readonly Vector3 CapRed = new(0.95f, 0.06f, 0.1f);

    /// <summary>The spots on a cap: pure white.</summary>
    public static readonly Vector3 Spot = new(1f, 1f, 1f);

    /// <summary>A stem and the gills under a cap: clean light cream.</summary>
    public static readonly Vector3 Stem = new(1f, 0.95f, 0.84f);

    /// <summary>The material names of mushroom caps, spots, stems and gills in the item models.</summary>
    public const string CapMaterial = "Cap", SpotsMaterial = "Spots", StemMaterial = "Stem", GillsMaterial = "Gills";

    /// <summary>The items with a mushroom on them.</summary>
    public static readonly string[] Items = { "mushroom_hat", "mushroom_backpack", "mushroom_hoodie", "spotted_dress" };

    /// <summary>Whether the glTF material <paramref name="material"/> is part of a mushroom.</summary>
    public static bool IsMushroom(string material) =>
        material is CapMaterial or SpotsMaterial or StemMaterial or GillsMaterial;

    /// <summary>The strength (0..1) of the toon shader's glossy highlight band: caps and their spots only.</summary>
    public static float Gloss(string material) => material is CapMaterial or SpotsMaterial ? 1f : 0f;

    /// <summary>Extra rim light (added to the shader's gentle 0.22) along a mushroom's edge.</summary>
    public static float Rim(string material) => IsMushroom(material) ? 0.3f : 0f;
}
