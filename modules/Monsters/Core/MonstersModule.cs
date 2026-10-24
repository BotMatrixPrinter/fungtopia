using System;
using System.Numerics;
using Fungtopia.Map;
using Fungtopia.Pet;
using Fungtopia.Player;
using Fungtopia.Shell;

namespace Fungtopia.Monsters;

/// <summary>
/// The mushroom monsters: keeps 12 caterpillars and 12 ants spread over Mushroom Forest, away from
/// its entrances, and 8 tarantulas and 8 ladybugs at the Deep Forest's spawn spots
/// (<see cref="DeepForestLayout.SpawnPoints"/>) and, when those are taken, free spots between its
/// trunks, each kind in its own area, on the world's
/// <see cref="Ground"/> and around its obstacles. They wander slowly and chase the alive player
/// within their <see cref="KindStats.ChaseRange"/>. Caterpillars and ants bite in
/// <see cref="AttackReach"/> every <see cref="AttackInterval"/>; the tarantula crouches and
/// pounces, the ladybug curls up and rolls into a charge that knocks the player back
/// (<see cref="KnockBackPlayer"/>) and takes half damage from the front of its shell.
/// <see cref="HitMonster"/> hurts them at once and poisons them; at 0 health they publish
/// <see cref="MonsterKilled"/> once and a new one of the same kind comes <see cref="RespawnDelayOf"/>
/// later (8 s, hard ones 15 s) somewhere free in its area, away from the player. Publishes one <see cref="MonsterState"/>
/// per monster every tick. Random choices come from <see cref="Seed"/>.
/// </summary>
public sealed class MonstersModule : IModule, IMessageHandler<PlayerState>, IMessageHandler<PetState>,
    IMessageHandler<HitMonster>
{
    public const int CaterpillarCount = 12;
    public const int AntCount = 12;
    public const int TarantulaCount = 8;
    public const int LadybugCount = 8;
    public const int NormalCount = CaterpillarCount + AntCount;
    public const int HardCount = TarantulaCount + LadybugCount;
    public const int Count = NormalCount + HardCount;

    /// <summary>Distance (meters, horizontal, between centers) within which a monster bites.</summary>
    public const float AttackReach = 1.5f;
    /// <summary>Seconds between two bites of one monster.</summary>
    public const float AttackInterval = 1.2f;
    /// <summary>Seconds from the defeat of a caterpillar or an ant until a new one appears.</summary>
    public const float NormalRespawnDelay = 8f;
    /// <summary>Seconds from the defeat of a tarantula or a ladybug until a new one appears.</summary>
    public const float HardRespawnDelay = 15f;
    /// <summary>No caterpillar or ant ever appears closer than this to one of the forest's entrances.</summary>
    public const float SpawnAwayFromStart = 30f;
    /// <summary>A respawned monster appears at least this far from the player.</summary>
    public const float SpawnAwayFromPlayer = 25f;
    /// <summary>Wanted spacing between monsters when one appears (relaxed if the area is crowded).</summary>
    public const float SpawnSpacing = 18f;
    /// <summary>Monsters keep this far inside their area's edge.</summary>
    public const float EdgeMargin = 3f;
    /// <summary>A Deep Forest monster's next roaming goal is at least this far away (meters).</summary>
    public const float RoamMinDistance = 20f;
    /// <summary>... and at most this far.</summary>
    public const float RoamMaxDistance = 60f;
    /// <summary>Fastest turn, radians per second (the view shows <see cref="Monster.Facing"/>).</summary>
    public const float TurnSpeed = 6f;

    /// <summary>A tarantula within this distance of the player (and rested) crouches to pounce.</summary>
    public const float PounceRange = 4.5f;
    /// <summary>Seconds the tarantula crouches, still, before it leaps.</summary>
    public const float PounceWindup = 0.5f;
    /// <summary>Seconds of the leap; it lands at the end and hurts the player if in <see cref="PounceReach"/>.</summary>
    public const float PounceSeconds = 0.4f;
    /// <summary>Distance (horizontal, between centers) at which a landing tarantula hits the player.</summary>
    public const float PounceReach = 1.8f;
    /// <summary>Seconds from a landing until the tarantula can pounce again (it stalks meanwhile).</summary>
    public const float PounceCooldown = 2f;
    /// <summary>Highest point of the leap above the ground, meters (for the view).</summary>
    public const float PounceHeight = 0.9f;
    /// <summary>A ladybug within this distance of the player (and rested) curls up to charge.</summary>
    public const float ChargeRange = 7f;
    /// <summary>Seconds the ladybug curls up, still, before it rolls.</summary>
    public const float ChargeWindup = 0.6f;
    /// <summary>Rolling speed, meters per second, in the direction it had when it started.</summary>
    public const float ChargeSpeed = 7f;
    /// <summary>Longest roll, seconds (it stops sooner when it hits the player or a trunk).</summary>
    public const float ChargeSeconds = 1.2f;
    /// <summary>Distance (horizontal, between centers) at which a rolling ladybug hits the player.</summary>
    public const float ChargeReach = 1.5f;
    /// <summary>Seconds from the end of a roll until the ladybug can charge again.</summary>
    public const float ChargeCooldown = 3f;
    /// <summary>How far a charge pushes the player (<see cref="KnockBackPlayer.Distance"/>), meters.</summary>
    public const float KnockBackDistance = 3f;
    /// <summary>Hits from within this angle (radians) of where a ladybug faces strike its shell.</summary>
    public const float ShellFrontAngle = MathF.PI / 3f;
    /// <summary>The share of a hit's damage a ladybug takes from the front.</summary>
    public const float ShellFrontDamage = 0.5f;

    /// <summary>Where the player comes into Mushroom Forest (x, z): its south, north and west gaps.</summary>
    private static readonly Vector2[] s_forestEntrances = { Areas.ForestGapSouth, Areas.ForestGapNorth, Areas.ForestGapWest };

    /// <summary>The numbers of one kind of monster.</summary>
    public readonly struct KindStats
    {
        public readonly int MaxHealth;
        /// <summary>Damage of a bite (caterpillar, ant), a pounce (tarantula) or a charge (ladybug).</summary>
        public readonly int BiteDamage;
        public readonly float WanderSpeed;
        public readonly float ChaseSpeed;
        public readonly float Radius;
        /// <summary>Distance (meters, horizontal) within which it chases the alive player.</summary>
        public readonly float ChaseRange;
        /// <summary>The area it lives in: <see cref="Area.Forest"/> or <see cref="Area.DeepForest"/>.</summary>
        public readonly Area Home;

        public KindStats(int maxHealth, int biteDamage, float wanderSpeed, float chaseSpeed, float radius,
            float chaseRange, Area home)
        {
            MaxHealth = maxHealth;
            BiteDamage = biteDamage;
            WanderSpeed = wanderSpeed;
            ChaseSpeed = chaseSpeed;
            Radius = radius;
            ChaseRange = chaseRange;
            Home = home;
        }
    }

    /// <summary>Seconds from a defeat of <paramref name="kind"/> until a new one appears.</summary>
    public static float RespawnDelayOf(MonsterKind kind) =>
        MonsterRewards.IsHard(kind) ? HardRespawnDelay : NormalRespawnDelay;

    public static KindStats StatsOf(MonsterKind kind) => kind switch
    {
        MonsterKind.Caterpillar => new KindStats(8, 1, 0.5f, 1.8f, 0.55f, 10f, Area.Forest),
        MonsterKind.Ant => new KindStats(5, 2, 0.9f, 2.8f, 0.45f, 10f, Area.Forest),
        MonsterKind.Tarantula => new KindStats(18, 3, 1.2f, 3.6f, 0.8f, 12f, Area.DeepForest),
        _ => new KindStats(25, 2, 0.4f, 1.2f, 0.75f, 12f, Area.DeepForest),
    };

    private readonly Monster[] _monsters = new Monster[Count];
    private readonly MonsterState[] _states = new MonsterState[Count];
    private IMessageBus? _bus;
    private Rng _rng;
    private int _nextId = 1;
    private PlayerState _player;
    private bool _hasPlayer;
    private PetState _pet;
    private bool _hasPet;

    /// <summary>The seed of the monsters' random choices; set it before <see cref="Start"/>.</summary>
    public uint Seed { get; set; } = 0x5EED_F00Du;

    /// <summary>Number of ticks run; the view uses it to see new ticks.</summary>
    public long Ticks { get; private set; }

    /// <summary>
    /// Every monster slot: caterpillars, ants, tarantulas, ladybugs (in that order, see the
    /// counts); a slot keeps its kind across respawns.
    /// </summary>
    public ReadOnlySpan<Monster> Monsters => _monsters;

    /// <summary>The states published on the latest tick (after <see cref="Start"/>: the starting ones).</summary>
    public ReadOnlySpan<MonsterState> States => _states;

    public MonstersModule()
    {
        for (int i = 0; i < Count; i++)
            _monsters[i] = new Monster(i < CaterpillarCount ? MonsterKind.Caterpillar
                : i < NormalCount ? MonsterKind.Ant
                : i < NormalCount + TarantulaCount ? MonsterKind.Tarantula
                : MonsterKind.Ladybug);
    }

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        _rng = new Rng(Seed);
        bus.Subscribe<PlayerState>(this);
        bus.Subscribe<PetState>(this);
        bus.Subscribe<HitMonster>(this);
        // Alternate kinds so both spread over the whole area.
        var nobody = new Vector3(float.NaN, 0f, float.NaN);
        for (int n = 0; n < NormalCount; n++)
            Spawn(_monsters[n % 2 == 0 ? n / 2 : CaterpillarCount + n / 2], nobody);
        for (int n = 0; n < HardCount; n++)
            Spawn(_monsters[NormalCount + (n % 2 == 0 ? n / 2 : TarantulaCount + n / 2)], nobody);
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

    /// <summary>
    /// Puts monster <paramref name="index"/> on the ground at (x, z) of <paramref name="feet"/>,
    /// standing still with no attack under way (tests, previews); <paramref name="facing"/> turns it.
    /// </summary>
    public void Place(int index, Vector3 feet, float? facing = null)
    {
        var m = _monsters[index];
        m.Position = new Vector3(feet.X, Ground.HeightAt(feet.X, feet.Z), feet.Z);
        m.Velocity = Vector3.Zero;
        m.Action = MonsterAction.None;
        m.ActionSeconds = 0f;
        if (facing is float yaw)
            m.Heading = m.Facing = WrapAngle(yaw);
        _states[index] = m.ToState();
    }

    public void Handle(in PlayerState message)
    {
        _player = message;
        _hasPlayer = true;
    }

    public void Handle(in PetState message)
    {
        _pet = message;
        _hasPet = true;
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
        if (message.Damage <= 0)
            return;
        int damage = message.Damage;
        if (m.Kind == MonsterKind.Ladybug && FromTheFront(m, message.Source))
        {
            // The shell takes half; halves add up over hits, so two 1-point hits make 1.
            m.ShellCarry += damage * ShellFrontDamage;
            damage = (int)(m.ShellCarry + 1e-4f);
            m.ShellCarry -= damage;
            m.HurtSeconds = 0f;
            if (damage <= 0)
                return;
        }
        Hurt(m, damage, message.Source);
    }

    /// <summary>
    /// Whether the hit's source (the player for 0, the pet for 1, where they were on the latest
    /// tick) is within <see cref="ShellFrontAngle"/> of where <paramref name="m"/> faces.
    /// </summary>
    private bool FromTheFront(Monster m, int source)
    {
        Vector3 from;
        if (source == 1 && _hasPet)
            from = _pet.Position;
        else if (source == 0 && _hasPlayer)
            from = _player.Position;
        else
            return false;
        float dx = from.X - m.Position.X, dz = from.Z - m.Position.Z;
        float d = MathF.Sqrt(dx * dx + dz * dz);
        if (d < 1e-4f)
            return false;
        float fx = -MathF.Sin(m.Facing), fz = -MathF.Cos(m.Facing);
        return (dx * fx + dz * fz) / d >= MathF.Cos(ShellFrontAngle);
    }

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("MonstersModule.Tick before Start.");
        float dt = (float)context.StepSeconds;
        Ticks++;
        bool playerHere = _hasPlayer && _player.Alive;
        Vector3 playerAt = _hasPlayer ? _player.Position : new Vector3(float.NaN, 0f, float.NaN);

        foreach (var m in _monsters)
        {
            if (!m.Alive)
            {
                m.DeadSeconds += dt;
                if (m.DeadSeconds >= RespawnDelayOf(m.Kind) - 1e-3f) // 1/60 s steps add up a hair short
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
        m.Action = MonsterAction.None;
        _bus!.Publish(new MonsterKilled(m.Id, m.Kind, MonsterRewards.For(m.Kind), m.Position, source));
    }

    private void Move(Monster m, float dt, bool playerHere, Vector3 playerAt)
    {
        var stats = StatsOf(m.Kind);
        var bounds = Areas.Bounds(stats.Home);
        Vector3 start = m.Position;
        float speed;
        float dx = playerAt.X - start.X, dz = playerAt.Z - start.Z;
        float distance = MathF.Sqrt(dx * dx + dz * dz);
        m.ActionSeconds += dt;

        m.Chasing = playerHere && distance <= stats.ChaseRange;
        if (m.Action == MonsterAction.Leap || m.Action == MonsterAction.Charge)
        {
            // Mid-leap or mid-roll: it goes on along its line whatever the player does.
            Lunge(m, dt, stats, bounds, playerHere, playerAt);
            return;
        }
        if (m.Action != MonsterAction.None && !m.Chasing)
            m.Action = MonsterAction.None; // the player got away while it crouched
        if (m.Chasing)
        {
            if (distance > 1e-4f)
                m.Heading = MathF.Atan2(-dx, -dz);
            speed = m.Kind switch
            {
                MonsterKind.Tarantula => Stalk(m, stats, distance, dt, playerAt),
                MonsterKind.Ladybug => Roll(m, stats, distance, dt, playerAt),
                _ => Bite(m, stats, distance, dt),
            };
            m.WanderSeconds = 0f; // pick a fresh wander when the chase ends
        }
        else
        {
            m.WanderSeconds -= dt;
            if (m.WanderSeconds <= 0f)
            {
                if (stats.Home == Area.DeepForest)
                    Roam(m, stats);
                else
                {
                    m.WanderSeconds = 2f + 3f * _rng.NextFloat();
                    m.Resting = _rng.NextFloat() < 0.3f;
                    m.Heading = (_rng.NextFloat() * 2f - 1f) * MathF.PI;
                }
            }
            if (!bounds.Contains(start.X, start.Z, EdgeMargin + 3f))
            {
                // Near the edge: head back toward the middle.
                var c = bounds.Center;
                m.Heading = MathF.Atan2(start.X - c.X, start.Z - c.Y);
                m.Resting = false;
            }
            speed = m.Resting ? 0f : stats.WanderSpeed;
        }

        m.Facing = TurnToward(m.Facing, m.Heading, TurnSpeed * dt);
        float x = start.X - MathF.Sin(m.Heading) * speed * dt;
        float z = start.Z - MathF.Cos(m.Heading) * speed * dt;
        if (!Resolve(ref x, ref z, stats.Radius, bounds))
            (x, z) = (start.X, start.Z); // wedged between obstacles: stay put this tick
        var end = new Vector3(x, Ground.HeightAt(x, z), z);
        m.Position = end;
        m.Velocity = (end - start) / dt;
    }

    /// <summary>A caterpillar or an ant: walks up to just inside reach, then stands and bites.</summary>
    private float Bite(Monster m, in KindStats stats, float distance, float dt)
    {
        if (distance <= AttackReach && m.BiteCooldown <= 1e-4f) // slack for float steps
        {
            m.BiteCooldown = AttackInterval;
            m.SinceBite = 0f;
            _bus!.Publish(new DamagePlayer(stats.BiteDamage, m.Position));
        }
        return MathF.Min(stats.ChaseSpeed, MathF.Max(0f, distance - AttackReach * 0.7f) / dt);
    }

    /// <summary>
    /// The tarantula: runs in, and when rested and within <see cref="PounceRange"/> crouches for
    /// <see cref="PounceWindup"/>, then leaps at where the player is. While it rests it stalks,
    /// creeping up to about half the pounce range.
    /// </summary>
    private float Stalk(Monster m, in KindStats stats, float distance, float dt, Vector3 playerAt)
    {
        if (m.Action == MonsterAction.Crouch)
        {
            if (m.ActionSeconds < PounceWindup - 1e-4f)
                return 0f;
            // Leap to just in front of the player, along a line fixed now.
            float leap = MathF.Max(0f, distance - (stats.Radius + 0.5f));
            float dirX = distance > 1e-4f ? (playerAt.X - m.Position.X) / distance : -MathF.Sin(m.Facing);
            float dirZ = distance > 1e-4f ? (playerAt.Z - m.Position.Z) / distance : -MathF.Cos(m.Facing);
            m.LungeVelocity = new Vector3(dirX, 0f, dirZ) * (leap / PounceSeconds);
            m.Action = MonsterAction.Leap;
            m.ActionSeconds = 0f;
            return 0f;
        }
        if (distance <= PounceRange && m.BiteCooldown <= 1e-4f)
        {
            m.Action = MonsterAction.Crouch;
            m.ActionSeconds = 0f;
            return 0f;
        }
        float keep = m.BiteCooldown > 1e-4f ? PounceRange * 0.6f : PounceRange * 0.8f;
        return MathF.Min(stats.ChaseSpeed, MathF.Max(0f, distance - keep) / dt);
    }

    /// <summary>
    /// The ladybug: plods in, and when rested and within <see cref="ChargeRange"/> curls up for
    /// <see cref="ChargeWindup"/>, then rolls straight at where the player was.
    /// </summary>
    private float Roll(Monster m, in KindStats stats, float distance, float dt, Vector3 playerAt)
    {
        if (m.Action == MonsterAction.Curl)
        {
            if (m.ActionSeconds < ChargeWindup - 1e-4f)
                return 0f;
            float dirX = distance > 1e-4f ? (playerAt.X - m.Position.X) / distance : -MathF.Sin(m.Facing);
            float dirZ = distance > 1e-4f ? (playerAt.Z - m.Position.Z) / distance : -MathF.Cos(m.Facing);
            m.LungeVelocity = new Vector3(dirX, 0f, dirZ) * ChargeSpeed;
            m.Action = MonsterAction.Charge;
            m.ActionSeconds = 0f;
            return 0f;
        }
        if (distance <= ChargeRange && m.BiteCooldown <= 1e-4f)
        {
            m.Action = MonsterAction.Curl;
            m.ActionSeconds = 0f;
            return 0f;
        }
        return MathF.Min(stats.ChaseSpeed, MathF.Max(0f, distance - AttackReach * 0.7f) / dt);
    }

    /// <summary>One tick of a tarantula's leap or a ladybug's roll along <see cref="Monster.LungeVelocity"/>.</summary>
    private void Lunge(Monster m, float dt, in KindStats stats, in AreaBounds bounds, bool playerHere, Vector3 playerAt)
    {
        Vector3 start = m.Position;
        var v = m.LungeVelocity;
        if (v.X * v.X + v.Z * v.Z > 1e-6f)
            m.Heading = MathF.Atan2(-v.X, -v.Z);
        m.Facing = TurnToward(m.Facing, m.Heading, TurnSpeed * 2f * dt);
        float x = start.X + v.X * dt, z = start.Z + v.Z * dt;
        if (!Resolve(ref x, ref z, stats.Radius, bounds))
            (x, z) = (start.X, start.Z);
        var end = new Vector3(x, Ground.HeightAt(x, z), z);
        m.Position = end;
        m.Velocity = (end - start) / dt;
        float dx = playerAt.X - x, dz = playerAt.Z - z;
        float distance = MathF.Sqrt(dx * dx + dz * dz);

        if (m.Action == MonsterAction.Leap)
        {
            if (m.ActionSeconds < PounceSeconds - 1e-4f)
                return;
            // Landed.
            if (playerHere && distance <= PounceReach)
                Strike(m, stats.BiteDamage);
            EndLunge(m, PounceCooldown);
            return;
        }
        if (playerHere && distance <= ChargeReach)
        {
            Strike(m, stats.BiteDamage);
            Vector3 away = distance > 1e-4f ? new Vector3(dx / distance, 0f, dz / distance)
                : Vector3.Normalize(new Vector3(v.X, 0f, v.Z));
            _bus!.Publish(new KnockBackPlayer(away, KnockBackDistance, end));
            EndLunge(m, ChargeCooldown);
            return;
        }
        // Bonked into a trunk or the edge (moved less than half as far as it rolls), or rolled out.
        float moved = new Vector2(end.X - start.X, end.Z - start.Z).Length();
        if (m.ActionSeconds >= ChargeSeconds - 1e-4f || moved < ChargeSpeed * dt * 0.5f)
            EndLunge(m, ChargeCooldown);
    }

    private void Strike(Monster m, int damage)
    {
        m.SinceBite = 0f;
        _bus!.Publish(new DamagePlayer(damage, m.Position));
    }

    private static void EndLunge(Monster m, float cooldown)
    {
        m.Action = MonsterAction.None;
        m.ActionSeconds = 0f;
        m.BiteCooldown = cooldown;
        m.WanderSeconds = 0f;
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
        var stats = StatsOf(m.Kind);
        float x = m.Position.X + dx, z = m.Position.Z + dz;
        if (!Resolve(ref x, ref z, stats.Radius, Areas.Bounds(stats.Home)))
            return; // no room that way
        m.Position = new Vector3(x, Ground.HeightAt(x, z), z);
    }

    /// <summary>
    /// A Deep Forest monster's next stretch of wandering: it walks toward a roaming goal, a free spot
    /// <see cref="RoamMinDistance"/>..<see cref="RoamMaxDistance"/> away somewhere in its area, so
    /// it does not stay in its spawn clearing. Each stretch re-aims at the goal (a little off, so it
    /// winds around the trunks), and a new goal comes when it got there or got stuck on the way.
    /// </summary>
    private void Roam(Monster m, in KindStats stats)
    {
        var p = m.Position;
        float moved = HorizontalDistance(m.RoamFrom.X, m.RoamFrom.Y, p);
        bool stuck = !m.Resting && moved < stats.WanderSpeed * m.RoamStretch * 0.3f;
        if (!m.HasGoal || stuck || HorizontalDistance(m.Goal.X, m.Goal.Y, p) < 3f)
            m.HasGoal = RoamGoal(p, stats, out m.Goal);
        m.WanderSeconds = m.RoamStretch = 3f + 3f * _rng.NextFloat();
        m.RoamFrom = new Vector2(p.X, p.Z);
        m.Resting = !stuck && _rng.NextFloat() < 0.15f;
        float jitter = (_rng.NextFloat() * 2f - 1f) * (stuck ? MathF.PI : 0.35f);
        m.Heading = m.HasGoal && !stuck
            ? WrapAngle(MathF.Atan2(p.X - m.Goal.X, p.Z - m.Goal.Y) + jitter)
            : WrapAngle(m.Heading + jitter); // stuck: try another way for a stretch
    }

    private bool RoamGoal(Vector3 from, in KindStats stats, out Vector2 goal)
    {
        var b = Areas.Bounds(stats.Home);
        float margin = EdgeMargin + 4f;
        for (int attempt = 0; attempt < 40; attempt++)
        {
            float x = b.MinX + margin + _rng.NextFloat() * (b.MaxX - b.MinX - 2f * margin);
            float z = b.MinZ + margin + _rng.NextFloat() * (b.MaxZ - b.MinZ - 2f * margin);
            float d = HorizontalDistance(x, z, from);
            if (d >= RoamMinDistance && d <= RoamMaxDistance && Areas.Walkable(x, z, stats.Radius + 1f)
                && !Ground.Blocks(x, z, stats.Radius + 1f))
            {
                goal = new Vector2(x, z);
                return true;
            }
        }
        goal = default;
        return false;
    }

    /// <summary>
    /// Pushes (x, z) out of every obstacle of <see cref="Ground.Obstacles"/> it overlaps and keeps
    /// it inside <paramref name="bounds"/> (its area), <see cref="EdgeMargin"/> from the edge.
    /// False when it is still in an obstacle after that (wedged between two): the caller keeps the
    /// spot it came from.
    /// </summary>
    private static bool Resolve(ref float x, ref float z, float radius, in AreaBounds bounds)
    {
        for (int pass = 0; pass < 3; pass++)
        {
            x = Math.Clamp(x, bounds.MinX + EdgeMargin, bounds.MaxX - EdgeMargin);
            z = Math.Clamp(z, bounds.MinZ + EdgeMargin, bounds.MaxZ - EdgeMargin);
            if (!Ground.Blocks(x, z, radius))
                return true;
            foreach (var o in Ground.Obstacles)
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
            }
        }
        x = Math.Clamp(x, bounds.MinX + EdgeMargin, bounds.MaxX - EdgeMargin);
        z = Math.Clamp(z, bounds.MinZ + EdgeMargin, bounds.MaxZ - EdgeMargin);
        return !Ground.Blocks(x, z, radius);
    }

    /// <summary>
    /// Gives the slot a new monster of its kind at a free spot of its area, away from
    /// <paramref name="avoid"/> (the player; NaN when there is none yet) and other monsters:
    /// caterpillars and ants anywhere in the forest away from its entrances, tarantulas and
    /// ladybugs at one of the Deep Forest's spawn spots or, when those are taken or too near the
    /// player, anywhere free in the Deep Forest away from its entrance.
    /// </summary>
    private void Spawn(Monster m, Vector3 avoid)
    {
        var stats = StatsOf(m.Kind);
        float x = 0f, z = 0f;
        bool found = stats.Home == Area.DeepForest
            ? SpawnSpot(m, avoid, SpawnSpacing, out x, out z) || FreeSpot(m, stats, avoid, out x, out z)
            : FreeSpot(m, stats, avoid, out x, out z);
        if (!found)
            return; // try again next tick (the area is never this full for long)
        m.Id = _nextId++;
        m.Position = new Vector3(x, Ground.HeightAt(x, z), z);
        m.Velocity = Vector3.Zero;
        m.Health = stats.MaxHealth;
        m.Alive = true;
        m.DeadSeconds = 0f;
        m.HurtSeconds = 1000f;
        m.SinceBite = 1000f;
        m.BiteCooldown = 0f;
        m.PoisonSeconds = 0.0;
        m.PoisonCarry = 0.0;
        m.ShellCarry = 0f;
        m.Action = MonsterAction.None;
        m.ActionSeconds = 0f;
        m.Heading = m.Facing = (_rng.NextFloat() * 2f - 1f) * MathF.PI;
        m.WanderSeconds = 0f;
        m.HasGoal = false;
        m.Chasing = false;
    }

    /// <summary>
    /// A random spot of the monster's area: walkable, clear of obstacles, away from the area's
    /// entrances and the player, and <see cref="SpawnSpacing"/> from other monsters (relaxed after
    /// many tries in a crowded area; in the Deep Forest a taken spawn spot is the last resort).
    /// </summary>
    private bool FreeSpot(Monster m, in KindStats stats, Vector3 avoid, out float x, out float z)
    {
        var b = Areas.Bounds(stats.Home);
        float margin = EdgeMargin + 2f;
        x = z = 0f;
        for (int attempt = 0; attempt < 600; attempt++)
        {
            x = b.MinX + margin + _rng.NextFloat() * (b.MaxX - b.MinX - 2f * margin);
            z = b.MinZ + margin + _rng.NextFloat() * (b.MaxZ - b.MinZ - 2f * margin);
            float spacing = attempt < 300 ? SpawnSpacing : attempt < 500 ? SpawnSpacing * 0.5f : 0f;
            if (!Ground.Blocks(x, z, stats.Radius + 0.5f)
                && Areas.Walkable(x, z, stats.Radius)
                && Areas.At(x, z) == stats.Home
                && AwayFromEntrances(stats.Home, x, z)
                && !(HorizontalDistance(x, z, avoid) < SpawnAwayFromPlayer)
                && !NearOtherMonster(m, x, z, spacing))
                return true;
        }
        return stats.Home == Area.DeepForest && SpawnSpot(m, avoid, 0f, out x, out z);
    }

    private static bool AwayFromEntrances(Area home, float x, float z)
    {
        if (home == Area.DeepForest)
            return Vector2.Distance(new Vector2(x, z), DeepForestLayout.Entrance) >= DeepForestLayout.SpawnMinEntranceDistance;
        foreach (var e in s_forestEntrances)
            if (Vector2.Distance(new Vector2(x, z), e) < SpawnAwayFromStart)
                return false;
        return true;
    }

    /// <summary>
    /// A Deep Forest spawn spot away from the player and <paramref name="spacing"/> from other
    /// monsters (tried from a random one).
    /// </summary>
    private bool SpawnSpot(Monster m, Vector3 avoid, float spacing, out float x, out float z)
    {
        var spots = DeepForestLayout.SpawnPoints;
        int first = (int)(_rng.NextFloat() * spots.Length);
        for (int k = 0; k < spots.Length; k++)
        {
            var s = spots[(first + k) % spots.Length];
            if (!(HorizontalDistance(s.X, s.Y, avoid) < SpawnAwayFromPlayer) && !NearOtherMonster(m, s.X, s.Y, spacing))
            {
                x = s.X;
                z = s.Y;
                return true;
            }
        }
        x = z = 0f;
        return false;
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

/// <summary>What a hard monster's attack is doing right now.</summary>
public enum MonsterAction
{
    None,
    /// <summary>A tarantula crouching, about to pounce.</summary>
    Crouch,
    /// <summary>A tarantula in the air, pouncing.</summary>
    Leap,
    /// <summary>A ladybug curling up into a ball, about to roll.</summary>
    Curl,
    /// <summary>A ladybug rolling at the player.</summary>
    Charge,
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
    /// <summary>Seconds since its last bite (or landed pounce, or charge that hit).</summary>
    public float SinceBite { get; internal set; } = 1000f;
    /// <summary>Seconds since its defeat while not <see cref="Alive"/>.</summary>
    public float DeadSeconds { get; internal set; }
    /// <summary>The hard monsters' attack under way (crouch, leap, curl, charge).</summary>
    public MonsterAction Action { get; internal set; }
    /// <summary>Seconds since <see cref="Action"/> began.</summary>
    public float ActionSeconds { get; internal set; }

    internal float Heading;
    internal float WanderSeconds;
    internal bool Resting;
    internal float BiteCooldown;
    internal double PoisonCarry;
    internal int PoisonSource;
    internal float ShellCarry;
    internal Vector3 LungeVelocity;
    /// <summary>A Deep Forest monster's roaming goal (x, z) while <see cref="HasGoal"/>.</summary>
    internal Vector2 Goal;
    internal bool HasGoal;
    /// <summary>Where its current stretch of roaming began (x, z), and how long the stretch is.</summary>
    internal Vector2 RoamFrom;
    internal float RoamStretch;

    internal MonsterState ToState() => new(Id, Kind, Position, Radius, Health, MaxHealth, Alive, Poisoned);
}
