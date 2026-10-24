using System.Numerics;
using Fungtopia.Map;

namespace Fungtopia.Forest;

/// <summary>
/// The walkable area: a 200 m x 200 m square centered on the origin (|x| and |z| at most
/// <see cref="HalfSize"/>), walled in by dense giant mushrooms just outside it. North is -Z
/// (Godot's forward), south is +Z. Forwards to the Map module's <see cref="Areas.Forest"/>;
/// the Map's roads, towns and Deep Forest lie beyond it.
/// </summary>
public static class ForestBounds
{
    /// <summary>Half the side of the walkable square, in meters (<see cref="Areas.ForestHalfSize"/>).</summary>
    public const float HalfSize = Areas.ForestHalfSize;

    /// <summary>Where the player starts: on the ground in the clearing near the south edge.</summary>
    public static readonly Vector3 PlayerStart = new(0f, Ground.HeightAt(0f, 84f), 84f);

    /// <summary>Whether (x, z) is inside the square, keeping <paramref name="margin"/> meters from its edge (<see cref="AreaBounds.Contains"/> of <see cref="Areas.Forest"/>).</summary>
    public static bool Contains(float x, float z, float margin = 0f) => Areas.Forest.Contains(x, z, margin);
}
