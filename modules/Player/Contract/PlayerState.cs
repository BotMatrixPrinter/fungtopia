using System.Numerics;

namespace Fungtopia.Player;

/// <summary>Which player character is played.</summary>
public enum CharacterKind
{
    Girl,
    Boy,
}

/// <summary>
/// The player at the end of a fixed tick, published by the Player module exactly once per tick.
/// </summary>
public readonly struct PlayerState
{
    /// <summary>World position of the feet, meters (Y up).</summary>
    public readonly Vector3 Position;

    /// <summary>
    /// Yaw in radians, Godot's convention: 0 faces -Z, positive turns left (counterclockwise seen
    /// from above). The facing direction is (-sin Facing, 0, -cos Facing).
    /// </summary>
    public readonly float Facing;

    /// <summary>Velocity this tick, meters per second.</summary>
    public readonly Vector3 Velocity;

    /// <summary>Standing on the ground.</summary>
    public readonly bool Grounded;

    public readonly int Health;

    public readonly int MaxHealth;

    /// <summary>False from defeat until the player comes back.</summary>
    public readonly bool Alive;

    public readonly CharacterKind Character;

    public PlayerState(Vector3 position, float facing, Vector3 velocity, bool grounded, int health, int maxHealth,
        bool alive, CharacterKind character)
    {
        Position = position;
        Facing = facing;
        Velocity = velocity;
        Grounded = grounded;
        Health = health;
        MaxHealth = maxHealth;
        Alive = alive;
        Character = character;
    }
}

/// <summary>Published by monsters (or anything else) to hurt the player.</summary>
public readonly struct DamagePlayer
{
    /// <summary>Health to take away (ignored when 0 or less).</summary>
    public readonly int Amount;

    /// <summary>Where the hit came from (world position).</summary>
    public readonly Vector3 From;

    public DamagePlayer(int amount, Vector3 from)
    {
        Amount = amount;
        From = from;
    }
}

/// <summary>Published once when the player's health reaches 0.</summary>
public readonly struct PlayerDefeated
{
    /// <summary>Where the player fell (feet).</summary>
    public readonly Vector3 Position;

    public PlayerDefeated(Vector3 position)
    {
        Position = position;
    }
}
