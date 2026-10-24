using System.Numerics;

namespace Fungtopia.Monsters;

/// <summary>
/// The cute mushroom monsters: the normal ones (Caterpillar, Ant) live in Mushroom Forest, the hard
/// ones (Tarantula, Ladybug) in the Deep Forest.
/// </summary>
public enum MonsterKind
{
    Caterpillar,
    Ant,
    /// <summary>A hard monster of the Deep Forest: stalks, then pounces.</summary>
    Tarantula,
    /// <summary>A hard monster of the Deep Forest: rolls into a charge that knocks the player back.</summary>
    Ladybug,
}

/// <summary>Mushroom Coin (M¢) rewards from the design.</summary>
public static class MonsterRewards
{
    /// <summary>Reward for a normal monster (Caterpillar, Ant).</summary>
    public const int Normal = 50;

    /// <summary>Reward for a hard monster (Tarantula, Ladybug).</summary>
    public const int Hard = 200;

    public static bool IsHard(MonsterKind kind) => kind is MonsterKind.Tarantula or MonsterKind.Ladybug;

    /// <summary>The M¢ earned for defeating a monster of <paramref name="kind"/>.</summary>
    public static int For(MonsterKind kind) => IsHard(kind) ? Hard : Normal;
}

/// <summary>
/// One monster at the end of a fixed tick, published by the Monsters module every tick for
/// every monster (also while it is defeated and waiting to be replaced).
/// </summary>
public readonly struct MonsterState
{
    /// <summary>Unique for each monster that ever spawned; a respawned monster gets a new one.</summary>
    public readonly int MonsterId;

    public readonly MonsterKind Kind;

    /// <summary>World position of its feet on the ground, meters (Y up).</summary>
    public readonly Vector3 Position;

    /// <summary>Its round footprint (and roughly its body), meters: what arrows hit.</summary>
    public readonly float Radius;

    public readonly int Health;

    public readonly int MaxHealth;

    public readonly bool Alive;

    /// <summary>Taking poison damage over time.</summary>
    public readonly bool Poisoned;

    public MonsterState(int monsterId, MonsterKind kind, Vector3 position, float radius, int health, int maxHealth,
        bool alive, bool poisoned)
    {
        MonsterId = monsterId;
        Kind = kind;
        Position = position;
        Radius = radius;
        Health = health;
        MaxHealth = maxHealth;
        Alive = alive;
        Poisoned = poisoned;
    }
}

/// <summary>Published by the bow (and the pet) when it hits a monster.</summary>
public readonly struct HitMonster
{
    public readonly int MonsterId;

    /// <summary>Health taken at once (ignored when 0 or less).</summary>
    public readonly int Damage;

    /// <summary>Poison damage per second afterwards (none when 0 or less).</summary>
    public readonly float PoisonDamagePerSecond;

    /// <summary>How long the poison lasts, seconds.</summary>
    public readonly float PoisonSeconds;

    /// <summary>Who hit: 0 the player, 1 the pet.</summary>
    public readonly int Source;

    public HitMonster(int monsterId, int damage, float poisonDamagePerSecond, float poisonSeconds, int source)
    {
        MonsterId = monsterId;
        Damage = damage;
        PoisonDamagePerSecond = poisonDamagePerSecond;
        PoisonSeconds = poisonSeconds;
        Source = source;
    }
}

/// <summary>Published once when a monster's health reaches 0.</summary>
public readonly struct MonsterKilled
{
    public readonly int MonsterId;

    public readonly MonsterKind Kind;

    /// <summary>Mushroom Coins (M¢) earned: <see cref="MonsterRewards.For"/>.</summary>
    public readonly int Reward;

    /// <summary>Where it was defeated (feet).</summary>
    public readonly Vector3 Position;

    /// <summary>Who defeated it (the source of the last hit or poison): 0 the player, 1 the pet.</summary>
    public readonly int Source;

    public MonsterKilled(int monsterId, MonsterKind kind, int reward, Vector3 position, int source)
    {
        MonsterId = monsterId;
        Kind = kind;
        Reward = reward;
        Position = position;
        Source = source;
    }
}

/// <summary>
/// Published by the Monsters module when a monster's hit shoves the player away (the ladybug's
/// charge), right after its <c>Fungtopia.Player.DamagePlayer</c>. For the player to apply.
/// </summary>
public readonly struct KnockBackPlayer
{
    /// <summary>Unit horizontal direction (Y 0) to push the player, away from the monster.</summary>
    public readonly Vector3 Direction;

    /// <summary>How far to push the player, meters.</summary>
    public readonly float Distance;

    /// <summary>Where the push came from (the monster's feet).</summary>
    public readonly Vector3 From;

    public KnockBackPlayer(Vector3 direction, float distance, Vector3 from)
    {
        Direction = direction;
        Distance = distance;
        From = from;
    }
}
