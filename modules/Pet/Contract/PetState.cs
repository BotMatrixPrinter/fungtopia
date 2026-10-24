using System.Numerics;

namespace Fungtopia.Pet;

/// <summary>A kind of pet.</summary>
public enum PetKind
{
    /// <summary>The Mushroom Cat (the default starter pet).</summary>
    Cat,

    /// <summary>The Mushroom Dog (the other starter pet).</summary>
    Dog,

    /// <summary>The Mushroom Crow (3,000 M¢ in Pet City): flies beside the player and pecks monsters.</summary>
    Crow,
}

/// <summary>
/// The active pet (the one that follows the player and fights) at the end of a fixed tick,
/// published by the Pet module exactly once per tick.
/// </summary>
public readonly struct PetState
{
    public readonly PetKind Kind;

    /// <summary>
    /// World position of its feet, meters (Y up): on the ground for the Cat and the Dog; in the
    /// air while the Crow flies (about 2 m up while following).
    /// </summary>
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

/// <summary>
/// Adds a pet to the ones the player owns (nothing new if they already own it) and makes it the
/// active one. Sent by whoever gives pets (Lucy's Pet Store).
/// </summary>
public readonly struct AdoptPet
{
    public readonly PetKind Kind;

    public AdoptPet(PetKind kind)
    {
        Kind = kind;
    }
}

/// <summary>Makes an owned pet the active one; ignored for a pet the player does not own.</summary>
public readonly struct SetActivePet
{
    public readonly PetKind Kind;

    public SetActivePet(PetKind kind)
    {
        Kind = kind;
    }
}

/// <summary>
/// The pets the player owns and which one is active, published by the Pet module exactly once
/// per tick. Owned pets that are not active stay at home (the Home module shows them).
/// </summary>
public readonly struct OwnedPets
{
    public readonly bool Cat;
    public readonly bool Dog;
    public readonly bool Crow;

    /// <summary>The pet that follows the player and fights (always an owned one).</summary>
    public readonly PetKind Active;

    public OwnedPets(bool cat, bool dog, bool crow, PetKind active)
    {
        Cat = cat;
        Dog = dog;
        Crow = crow;
        Active = active;
    }

    /// <summary>Whether the player owns <paramref name="kind"/>.</summary>
    public bool Owns(PetKind kind) => kind switch
    {
        PetKind.Cat => Cat,
        PetKind.Dog => Dog,
        PetKind.Crow => Crow,
        _ => false,
    };

    /// <summary>Whether <paramref name="kind"/> is owned and not active: it stays at home.</summary>
    public bool AtHome(PetKind kind) => Owns(kind) && kind != Active;
}
