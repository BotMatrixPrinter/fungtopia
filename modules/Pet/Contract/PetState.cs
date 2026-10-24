using System.Numerics;

namespace Fungtopia.Pet;

/// <summary>Which pet follows the player.</summary>
public enum PetKind
{
    /// <summary>The Mushroom Cat (the default starter pet).</summary>
    Cat,

    /// <summary>The Mushroom Dog (the other starter pet).</summary>
    Dog,

    /// <summary>Comes later: 3,000 M¢ in Pet City.</summary>
    Crow,
}

/// <summary>The pet at the end of a fixed tick, published by the Pet module exactly once per tick.</summary>
public readonly struct PetState
{
    public readonly PetKind Kind;

    /// <summary>World position of its feet on the ground, meters (Y up).</summary>
    public readonly Vector3 Position;

    /// <summary>
    /// Yaw in radians, Godot's convention: 0 faces -Z, positive turns left. The facing direction
    /// is (-sin Facing, 0, -cos Facing).
    /// </summary>
    public readonly float Facing;

    /// <summary>Going for a monster: running to it or biting it.</summary>
    public readonly bool Attacking;

    public PetState(PetKind kind, Vector3 position, float facing, bool attacking)
    {
        Kind = kind;
        Position = position;
        Facing = facing;
        Attacking = attacking;
    }
}
