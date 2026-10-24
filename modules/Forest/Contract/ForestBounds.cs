using System.Numerics;

namespace Fungtopia.Forest;

/// <summary>
/// The walkable area: a 200 m x 200 m square centered on the origin (|x| and |z| at most
/// <see cref="HalfSize"/>), walled in by dense giant mushrooms just outside it. North is -Z
/// (Godot's forward), south is +Z.
/// </summary>
public static class ForestBounds
{
    /// <summary>Half the side of the walkable square, in meters.</summary>
    public const float HalfSize = 100f;

    /// <summary>Where the player starts: on the ground in the clearing near the south edge.</summary>
    public static readonly Vector3 PlayerStart = new(0f, Terrain.HeightAt(0f, 84f), 84f);

    /// <summary>Whether (x, z) is inside the square, keeping <paramref name="margin"/> meters from its edge.</summary>
    public static bool Contains(float x, float z, float margin = 0f)
    {
        float limit = HalfSize - margin;
        return x >= -limit && x <= limit && z >= -limit && z <= limit;
    }
}
