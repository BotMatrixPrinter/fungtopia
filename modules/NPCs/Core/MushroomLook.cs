namespace Fungtopia.NPCs;

/// <summary>
/// How the NPCs' mushroom parts are drawn ("vivid", the same rule for every mushroom in the game):
/// Pip's mushroom-cap chef's hat (<c>Hat</c>, with its red <c>HatSpots</c>) gets a light rim light
/// along its edge, and the cap itself a soft glossy highlight (a small specular band in the toon
/// shading; not on the little spots, where a glint would look like an eye); every other surface
/// keeps the plain toon look. The view (<c>NpcFigure</c>) reads this per material.
/// </summary>
public static class MushroomLook
{
    /// <summary>Strength of the glossy highlight band on a mushroom cap (the toon shader's <c>gloss</c>).</summary>
    public const float CapGloss = 0.55f;

    /// <summary>The rim light along a mushroom cap's edge (the toon shader's <c>rim</c>); other surfaces keep <see cref="PlainRim"/>.</summary>
    public const float CapRim = 0.45f;

    /// <summary>The gentle rim every other surface has.</summary>
    public const float PlainRim = 0.22f;

    /// <summary>True for the materials of a mushroom cap: Pip's hat and its spots.</summary>
    public static bool IsCap(string material) => material is "Hat" or "HatSpots";

    /// <summary>The <c>gloss</c> for a material: <see cref="CapGloss"/> on the cap itself, else 0.</summary>
    public static float Gloss(string material) => material is "Hat" ? CapGloss : 0f;

    /// <summary>The <c>rim</c> for a material: <see cref="CapRim"/> on a cap, else <see cref="PlainRim"/>.</summary>
    public static float Rim(string material) => IsCap(material) ? CapRim : PlainRim;
}
