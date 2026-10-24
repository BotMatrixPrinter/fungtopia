namespace Fungtopia.Forest;

/// <summary>
/// The Mushroom Forest's ground. Forwards to the Map module, which owns the world's ground:
/// use <see cref="Fungtopia.Map.Ground"/> in new code.
/// </summary>
public static class Terrain
{
    /// <summary>Largest |height| the ground reaches inside <see cref="ForestBounds"/>, in meters.</summary>
    public const float MaxAbsHeight = 1.9f;

    /// <summary>
    /// Ground height (meters, Y up) at world position (x, z): forwards to
    /// <see cref="Fungtopia.Map.Ground.HeightAt"/>. Inside <see cref="ForestBounds"/> gentle rolling
    /// hills (never steeper than about 1 in 6); beyond it the Map's roads, towns and cliffs.
    /// Pure, deterministic and allocation-free; defined everywhere.
    /// </summary>
    public static float HeightAt(float x, float z) => Fungtopia.Map.Ground.HeightAt(x, z);
}
