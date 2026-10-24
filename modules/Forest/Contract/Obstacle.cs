using System.Numerics;

namespace Fungtopia.Forest;

/// <summary>A giant mushroom trunk: a circle on the ground (x/z, meters) that walkers cannot enter.</summary>
public readonly struct Obstacle
{
    public readonly float X;
    public readonly float Z;
    public readonly float Radius;

    public Obstacle(float x, float z, float radius)
    {
        X = x;
        Z = z;
        Radius = radius;
    }

    /// <summary>Center as (x, z).</summary>
    public Vector2 Center => new(X, Z);
}

/// <summary>An open round clearing on the ground (x/z, meters): no giant mushrooms in it.</summary>
public readonly struct Clearing
{
    public readonly float X;
    public readonly float Z;
    public readonly float Radius;

    public Clearing(float x, float z, float radius)
    {
        X = x;
        Z = z;
        Radius = radius;
    }

    /// <summary>Center as (x, z).</summary>
    public Vector2 Center => new(X, Z);
}
