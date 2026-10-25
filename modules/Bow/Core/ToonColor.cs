using System;
using System.Numerics;

namespace Fungtopia.Bow;

/// <summary>
/// The color steps the view's toon shading uses (pure math, so the tests can check what the view
/// hands the shader).
/// </summary>
public static class ToonColor
{
    /// <summary>
    /// The shader's linear <c>glow</c> for a glTF emission as Godot imports it. The importer stores
    /// emission sRGB-encoded (<c>linear_to_srgb(emissiveFactor)</c>), so one step gives back
    /// Blender's value; like the albedo, that value is an sRGB swatch, so a second step makes it
    /// linear. The energy multiplier is applied last, unconverted.
    /// </summary>
    public static Vector3 Glow(Vector3 importedEmission, float energy) =>
        SrgbToLinear(SrgbToLinear(importedEmission)) * energy;

    /// <summary>Godot's <c>Color.SrgbToLinear</c>, per channel.</summary>
    public static Vector3 SrgbToLinear(Vector3 c) => new(Channel(c.X), Channel(c.Y), Channel(c.Z));

    private static float Channel(float c) =>
        c < 0.04045f ? c * (1f / 12.92f) : MathF.Pow((c + 0.055f) * (1f / 1.055f), 2.4f);
}
