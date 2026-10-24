using System.Numerics;
using Fungtopia.Map;

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

    /// <summary>False from defeat until the next <see cref="TeleportPlayer"/>.</summary>
    public readonly bool Alive;

    public readonly CharacterKind Character;

    /// <summary>The place of the world the player is in: <see cref="Areas.At"/> at <see cref="Position"/>.</summary>
    public readonly Area Area;

    /// <summary>A state whose <see cref="Area"/> is <see cref="Areas.At"/> at <paramref name="position"/>.</summary>
    public PlayerState(Vector3 position, float facing, Vector3 velocity, bool grounded, int health, int maxHealth,
        bool alive, CharacterKind character)
        : this(position, facing, velocity, grounded, health, maxHealth, alive, character, Areas.At(position.X, position.Z))
    {
    }

    public PlayerState(Vector3 position, float facing, Vector3 velocity, bool grounded, int health, int maxHealth,
        bool alive, CharacterKind character, Area area)
    {
        Position = position;
        Facing = facing;
        Velocity = velocity;
        Grounded = grounded;
        Health = health;
        MaxHealth = maxHealth;
        Alive = alive;
        Character = character;
        Area = area;
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

/// <summary>
/// Moves the player to <see cref="Position"/> (feet, put on the ground there) facing
/// <see cref="Facing"/> (yaw, like <see cref="PlayerState.Facing"/>) at once, with no velocity.
/// After a defeat it also brings the player back with full health (fast travel, waking up at the
/// clinic, entering and leaving the house).
/// </summary>
public readonly struct TeleportPlayer
{
    public readonly Vector3 Position;

    public readonly float Facing;

    public TeleportPlayer(Vector3 position, float facing)
    {
        Position = position;
        Facing = facing;
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
