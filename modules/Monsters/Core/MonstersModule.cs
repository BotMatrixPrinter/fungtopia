using System;
using System.Numerics;
using Fungtopia.Forest;
using Fungtopia.Player;
using Fungtopia.Shell;

namespace Fungtopia.Monsters;

/// <summary>
/// The mushroom monsters of the Mushroom Forest: keeps 6 caterpillars and 6 ants spread over the
/// forest, away from the player's start. They wander slowly, chase the alive player within
/// <see cref="ChaseRange"/>, and bite in <see cref="AttackReach"/> every <see cref="AttackInterval"/>
/// with <see cref="DamagePlayer"/>. <see cref="HitMonster"/> hurts them at once and poisons them;
/// at 0 health they publish <see cref="MonsterKilled"/> once and a new one of the same kind
/// comes <see cref="RespawnDelay"/> later somewhere free, away from the player. Publishes one
/// <see cref="MonsterState"/> per monster every tick. Random choices come from <see cref="Seed"/>.
/// </summary>
public sealed class MonstersModule : IModule, IMessageHandler<PlayerState>, IMessageHandler<HitMonster>
{
    public const int CaterpillarCount = 6;
    public const int AntCount = 6;
    public const int Count = CaterpillarCount + AntCount;

    /// <summary>Distance (meters, horizontal) within which a monster chases the alive player.</summary>
    public const float ChaseRange = 10f;
    /// <summary>Distance (meters, horizontal, between centers) within which a monster bites.</summary>
    public const float AttackReach = 1.5f;
    /// <summary>Seconds between two bites of one monster.</summary>
    public const float AttackInterval = 1.2f;
    /// <summary>Seconds from a defeat until a new monster of that kind appears.</summary>
    public const float RespawnDelay = 20f;
    /// <summary>No monster ever appears closer than this to <see cref="ForestBounds.PlayerStart"/>.</summary>
    public const float SpawnAwayFromStart = 30f;
    /// <summary>A respawned monster appears at least this far from the player.</summary>
    public const float SpawnAwayFromPlayer = 25f;
    /// <summary>Wanted spacing between monsters when one appears (relaxed if the forest is crowded).</summary>
    public const float SpawnSpacing = 18f;
    /// <summary>Monsters keep this far inside the forest's edge.</summary>
    public const float EdgeMargin = 3f;
    /// <summary>Fastest turn, radians per second (the view shows <see cref="Monster.Facing"/>).</summary>
    public const float TurnSpeed = 6f;

    /// <summary>The numbers of one kind of monster.</summary>
    public readonly struct KindStats
    {
        public readonly int MaxHealth;
        public readonly int BiteDamage;
        public readonly float WanderSpeed;
        public readonly float ChaseSpeed;
        public readonly float Radius;

        public KindStats(int maxHealth, int biteDamage, float wanderSpeed, float chaseSpeed, float radius)
        {
            MaxHealth = maxHealth;
            BiteDamage = biteDamage;
            WanderSpeed = wanderSpeed;
            ChaseSpeed = chaseSpeed;
            Radius = radius;
        }
    }

    public static KindStats StatsOf(MonsterKind kind) => kind switch
    {
        MonsterKind.Caterpillar => new KindStats(8, 1, 0.5f, 1.8f, 0.55f),
        MonsterKind.Ant => new KindStats(5, 2, 0.9f, 2.8f, 0.45f),
        // The hard monsters come later; numbers for them are a placeholder.
        MonsterKind.Tarantula => new KindStats(20, 3, 0.7f, 3.0f, 0.8f),
        _ => new KindStats(16, 3, 0.8f, 3.2f, 0.6f),
    };

    private readonly Monster[] _monsters = new Monster[Count];
    private readonly MonsterState[] _states = new MonsterState[Count];
    private IMessageBus? _bus;
    private Rng _rng;
    private int _nextId = 1;
    private PlayerState _player;
    private bool _hasPlayer;

    /// <summary>The seed of the monsters' random choices; set it before <see cref="Start"/>.</summary>
    public uint Seed { get; set; } = 0x5EED_F00Du;

    /// <summary>Number of ticks run; the view uses it to see new ticks.</summary>
    public long Ticks { get; private set; }

    /// <summary>Every monster slot (caterpillars first); a slot keeps its kind across respawns.</summary>
    public ReadOnlySpan<Monster> Monsters => _monsters;

    /// <summary>The states published on the latest tick (after <see cref="Start"/>: the starting ones).</summary>
    public ReadOnlySpan<MonsterState> States => _states;

    public MonstersModule()
    {
        for (int i = 0; i < Count; i++)
            _monsters[i] = new Monster(i < CaterpillarCount ? MonsterKind.Caterpillar : MonsterKind.Ant);
    }

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        _rng = new Rng(Seed);
        bus.Subscribe<PlayerState>(this);
        bus.Subscribe<HitMonster>(this);
        // Alternate kinds so both spread over the whole forest.
        for (int n = 0; n < Count; n++)
        {
            int i = n % 2 == 0 ? n / 2 : CaterpillarCount + n / 2;
            Spawn(_monsters[i], ForestBounds.PlayerStart);
        }
        UpdateStates();
    }

    /// <summary>The monster with <paramref name="monsterId"/>, or null when none has it now.</summary>
    public Monster? Find(int monsterId)
    {
        foreach (var m in _monsters)
            if (m.Id == monsterId)
                return m;
        return null;
    }

    /// <summary>Puts monster <paramref name="index"/> on the ground at (x, z) of <paramref name="feet"/> (tests, previews).</summary>
    public void Place(int index, Vector3 feet)
    {
        var m = _monsters[index];
        m.Position = new Vector3(feet.X, Terrain.HeightAt(feet.X, feet.Z), feet.Z);
        m.Velocity = Vector3.Zero;
        _states[index] = m.ToState();
    }

    public void Handle(in PlayerState message)
    {
        _player = message;
        _hasPlayer = true;
    }

    public void Handle(in HitMonster message)
    {
        var m = Find(message.MonsterId);
        if (m == null || !m.Alive)
            return;
        if (message.PoisonDamagePerSecond > 0f && message.PoisonSeconds > 0f
            && float.IsFinite(message.PoisonDamagePerSecond) && float.IsFinite(message.PoisonSeconds))
        {
            // Stacking poison refreshes how long it lasts, never how fast it hurts.
            if (m.PoisonSeconds <= 0.0)
            {
                m.PoisonRate = message.PoisonDamagePerSecond;
                m.PoisonCarry = 0.0;
            }
            m.PoisonSeconds = message.PoisonSeconds;
            m.PoisonSource = message.Source;
        }
        if (message.Damage > 0)
            Hurt(m, message.Damage, message.Source);
    }

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("MonstersModule.Tick before Start.");
        float dt = (float)context.StepSeconds;
        Ticks++;
        bool playerHere = _hasPlayer && _player.Alive;
        Vector3 playerAt = _hasPlayer ? _player.Position : ForestBounds.PlayerStart;

        foreach (var m in _monsters)
        {
            if (!m.Alive)
            {
                m.DeadSeconds += dt;
                if (m.DeadSeconds >= RespawnDelay)
                    Spawn(m, playerAt);
                continue;
            }
            m.HurtSeconds += dt;
            m.SinceBite += dt;
            if (m.BiteCooldown > 0f)
                m.BiteCooldown = MathF.Max(0f, m.BiteCooldown - dt);
            Poison(m, context.StepSeconds);
            if (!m.Alive)
                continue;
            Move(m, dt, playerHere, playerAt);
        }
        Separate();
        UpdateStates();
        for (int i = 0; i < Count; i++)
            _bus.Publish(in _states[i]);
    }

    private void Poison(Monster m, double dt)
    {
        if (m.PoisonSeconds <= 0.0)
            return;
        m.PoisonCarry += m.PoisonRate * Math.Min(dt, m.PoisonSeconds);
        m.PoisonSeconds -= dt;
        // A hair of slack so 2/s for 3 s is 6, not 5.999.
        int damage = (int)(m.PoisonCarry + 1e-6);
        m.PoisonCarry -= damage;
        if (m.PoisonSeconds <= 1e-9)
        {
            m.PoisonSeconds = 0.0;
            m.PoisonCarry = 0.0;
        }
        if (damage > 0)
            Hurt(m, damage, m.PoisonSource);
    }

    private void Hurt(Monster m, int damage, int source)
    {
        m.Health = Math.Max(0, m.Health - damage);
        m.HurtSeconds = 0f;
        if (m.Health > 0)
            return;
        m.Alive = false;
        m.DeadSeconds = 0f;
        m.PoisonSeconds = 0.0;
        m.PoisonCarry = 0.0;
        m.Velocity = Vector3.Zero;
        _bus!.Publish(new MonsterKilled(m.Id, m.Kind, MonsterRewards.For(m.Kind), m.Position, source));
    }

    private void Move(Monster m, float dt, bool playerHere, Vector3 playerAt)
    {
        var stats = StatsOf(m.Kind);
        Vector3 start = m.Position;
        float speed;
        float dx = playerAt.X - start.X, dz = playerAt.Z - start.Z;
        float distance = MathF.Sqrt(dx * dx + dz * dz);

        m.Chasing = playerHere && distance <= ChaseRange;
        if (m.Chasing)
        {
            if (distance > 1e-4f)
                m.Heading = MathF.Atan2(-dx, -dz);
            // Walk up to just inside reach, then stand and bite.
            speed = MathF.Min(stats.ChaseSpeed, MathF.Max(0f, distance - AttackReach * 0.7f) / dt);
            if (distance <= AttackReach && m.BiteCooldown <= 1e-4f) // slack for float steps
            {
                m.BiteCooldown = AttackInterval;
                m.SinceBite = 0f;
                _bus!.Publish(new DamagePlayer(stats.BiteDamage, start));
            }
            m.WanderSeconds = 0f; // pick a fresh wander when the chase ends
        }
        else
        {
            m.WanderSeconds -= dt;
            if (m.WanderSeconds <= 0f)
            {
                m.WanderSeconds = 2f + 3f * _rng.NextFloat();
                m.Resting = _rng.NextFloat() < 0.3f;
                m.Heading = (_rng.NextFloat() * 2f - 1f) * MathF.PI;
            }
            if (!ForestBounds.Contains(start.X, start.Z, EdgeMargin + 3f))
            {
                // Near the edge: head back toward the middle.
                m.Heading = MathF.Atan2(start.X, start.Z);
                m.Resting = false;
            }
            speed = m.Resting ? 0f : stats.WanderSpeed;
        }

        m.Facing = TurnToward(m.Facing, m.Heading, TurnSpeed * dt);
        float x = start.X - MathF.Sin(m.Heading) * speed * dt;
        float z = start.Z - MathF.Cos(m.Heading) * speed * dt;
        Resolve(ref x, ref z, stats.Radius);
        var end = new Vector3(x, Terrain.HeightAt(x, z), z);
        m.Position = end;
        m.Velocity = (end - start) / dt;
    }

    /// <summary>Pushes overlapping alive monsters apart, so a chasing crowd does not merge.</summary>
    private void Separate()
    {
        for (int i = 0; i < Count; i++)
        {
            var a = _monsters[i];
            if (!a.Alive)
                continue;
            for (int j = i + 1; j < Count; j++)
            {
                var b = _monsters[j];
                if (!b.Alive)
                    continue;
                float dx = b.Position.X - a.Position.X, dz = b.Position.Z - a.Position.Z;
                float r = StatsOf(a.Kind).Radius + StatsOf(b.Kind).Radius;
                float d2 = dx * dx + dz * dz;
                if (d2 >= r * r)
                    continue;
                float d = MathF.Sqrt(d2);
                if (d < 1e-5f)
                {
                    dx = 1f;
                    dz = 0f;
                    d = 1f;
                }
                float push = (r - d) * 0.5f / d;
                Nudge(a, -dx * push, -dz * push);
                Nudge(b, dx * push, dz * push);
            }
        }
    }

    private static void Nudge(Monster m, float dx, float dz)
    {
        float x = m.Position.X + dx, z = m.Position.Z + dz;
        Resolve(ref x, ref z, StatsOf(m.Kind).Radius);
        m.Position = new Vector3(x, Terrain.HeightAt(x, z), z);
    }

    /// <summary>Pushes (x, z) out of every trunk it overlaps and keeps it inside the forest.</summary>
    private static void Resolve(ref float x, ref float z, float radius)
    {
        for (int pass = 0; pass < 3; pass++)
        {
            bool moved = false;
            foreach (var o in Obstacles.All)
            {
                float dx = x - o.X, dz = z - o.Z, r = o.Radius + radius;
                float d2 = dx * dx + dz * dz;
                if (d2 >= r * r)
                    continue;
                float d = MathF.Sqrt(d2);
                if (d < 1e-5f)
                {
                    dx = 0f;
                    dz = 1f;
                    d = 1f;
                }
                float push = (r + 1e-4f) / d;
                x = o.X + dx * push;
                z = o.Z + dz * push;
                moved = true;
            }
            float limit = ForestBounds.HalfSize - EdgeMargin;
            x = Math.Clamp(x, -limit, limit);
            z = Math.Clamp(z, -limit, limit);
            if (!moved)
                break;
        }
    }

    /// <summary>Gives the slot a new monster of its kind at a free random spot away from <paramref name="avoid"/>.</summary>
    private void Spawn(Monster m, Vector3 avoid)
    {
        var stats = StatsOf(m.Kind);
        float awayFromPlayer = MathF.Max(SpawnAwayFromPlayer, Vector3.Distance(avoid, ForestBounds.PlayerStart) < 1f ? SpawnAwayFromStart : 0f);
        float limit = ForestBounds.HalfSize - EdgeMargin - 2f;
        float x = 0f, z = 0f;
        bool found = false;
        for (int attempt = 0; attempt < 600 && !found; attempt++)
        {
            x = (_rng.NextFloat() * 2f - 1f) * limit;
            z = (_rng.NextFloat() * 2f - 1f) * limit;
            // After many tries in a crowded forest the wanted spacing gives way.
            float spacing = attempt < 300 ? SpawnSpacing : attempt < 500 ? SpawnSpacing * 0.5f : 0f;
            found = !Obstacles.Blocks(x, z, stats.Radius + 0.5f)
                && HorizontalDistance(x, z, ForestBounds.PlayerStart) >= SpawnAwayFromStart
                && HorizontalDistance(x, z, avoid) >= awayFromPlayer
                && !NearOtherMonster(m, x, z, spacing);
        }
        if (!found)
            return; // try again next tick (the forest is never this full)
        m.Id = _nextId++;
        m.Position = new Vector3(x, Terrain.HeightAt(x, z), z);
        m.Velocity = Vector3.Zero;
        m.Health = stats.MaxHealth;
        m.Alive = true;
        m.DeadSeconds = 0f;
        m.HurtSeconds = 1000f;
        m.SinceBite = 1000f;
        m.BiteCooldown = 0f;
        m.PoisonSeconds = 0.0;
        m.PoisonCarry = 0.0;
        m.Heading = m.Facing = (_rng.NextFloat() * 2f - 1f) * MathF.PI;
        m.WanderSeconds = 0f;
        m.Chasing = false;
    }

    private bool NearOtherMonster(Monster self, float x, float z, float spacing)
    {
        if (spacing <= 0f)
            return false;
        foreach (var o in _monsters)
            if (o != self && o.Id != 0 && o.Alive && HorizontalDistance(x, z, o.Position) < spacing)
                return true;
        return false;
    }

    private static float HorizontalDistance(float x, float z, Vector3 p)
    {
        float dx = x - p.X, dz = z - p.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private static float TurnToward(float from, float to, float step)
    {
        float diff = WrapAngle(to - from);
        return WrapAngle(from + Math.Clamp(diff, -step, step));
    }

    private static float WrapAngle(float a)
    {
        if (!float.IsFinite(a))
            return 0f;
        a %= MathF.Tau;
        if (a > MathF.PI)
            a -= MathF.Tau;
        else if (a <= -MathF.PI)
            a += MathF.Tau;
        return a;
    }

    private void UpdateStates()
    {
        for (int i = 0; i < Count; i++)
            _states[i] = _monsters[i].ToState();
    }

    /// <summary>A small deterministic random generator (xorshift32).</summary>
    private struct Rng
    {
        private uint _state;

        public Rng(uint seed) => _state = seed == 0 ? 0x9E3779B9u : seed;

        /// <summary>A value in [0, 1).</summary>
        public float NextFloat()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return (x >> 8) * (1f / 16777216f);
        }
    }
}

/// <summary>One monster slot of <see cref="MonstersModule"/>: what the view draws besides <see cref="MonsterState"/>.</summary>
public sealed class Monster
{
    internal Monster(MonsterKind kind) => Kind = kind;

    public int Id { get; internal set; }
    public MonsterKind Kind { get; }
    public Vector3 Position { get; internal set; }
    /// <summary>Velocity on the latest tick, meters per second.</summary>
    public Vector3 Velocity { get; internal set; }
    /// <summary>Yaw, Godot's convention (0 faces -Z, positive turns left); turns smoothly.</summary>
    public float Facing { get; internal set; }
    public int Health { get; internal set; }
    public int MaxHealth => MonstersModule.StatsOf(Kind).MaxHealth;
    public float Radius => MonstersModule.StatsOf(Kind).Radius;
    public bool Alive { get; internal set; }
    public bool Chasing { get; internal set; }
    public bool Poisoned => PoisonSeconds > 0.0;
    /// <summary>Poison seconds left.</summary>
    public double PoisonSeconds { get; internal set; }
    /// <summary>Poison damage per second while <see cref="Poisoned"/>.</summary>
    public float PoisonRate { get; internal set; }
    /// <summary>Seconds since it was last hurt.</summary>
    public float HurtSeconds { get; internal set; } = 1000f;
    /// <summary>Seconds since its last bite.</summary>
    public float SinceBite { get; internal set; } = 1000f;
    /// <summary>Seconds since its defeat while not <see cref="Alive"/>.</summary>
    public float DeadSeconds { get; internal set; }

    internal float Heading;
    internal float WanderSeconds;
    internal bool Resting;
    internal float BiteCooldown;
    internal double PoisonCarry;
    internal int PoisonSource;

    internal MonsterState ToState() => new(Id, Kind, Position, Radius, Health, MaxHealth, Alive, Poisoned);
}
