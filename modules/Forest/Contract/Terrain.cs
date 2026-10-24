using System;

namespace Fungtopia.Forest;

/// <summary>The Mushroom Forest's ground.</summary>
public static class Terrain
{
    /// <summary>Largest |height| the ground ever reaches, in meters.</summary>
    public const float MaxAbsHeight = 1.9f;

    /// <summary>
    /// Ground height (meters, Y up) at world position (x, z): gentle rolling hills made of a few
    /// fixed sine waves. Pure, deterministic and allocation-free; defined everywhere, also
    /// outside <see cref="ForestBounds"/>. Never steeper than about 1 in 6.
    /// </summary>
    public static float HeightAt(float x, float z)
    {
        return 1.1f * MathF.Sin(0.031f * x + 0.4f) * MathF.Cos(0.027f * z - 1.1f)
             + 0.5f * MathF.Sin(0.052f * x - 0.061f * z + 2.3f)
             + 0.3f * MathF.Sin(0.13f * x + 1.7f) * MathF.Sin(0.11f * z + 0.3f);
    }
}
