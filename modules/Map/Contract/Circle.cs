using System.Numerics;

namespace Fungtopia.Map;

/// <summary>A circle on the ground (x/z, meters): an obstacle footprint walkers cannot enter.</summary>
public readonly struct Circle
{
    public readonly float X;
    public readonly float Z;
    public readonly float Radius;

    public Circle(float x, float z, float radius)
    {
        X = x;
        Z = z;
        Radius = radius;
    }

    /// <summary>Center as (x, z).</summary>
    public Vector2 Center => new(X, Z);
}
