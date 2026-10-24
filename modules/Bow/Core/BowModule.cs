using System;
using System.Numerics;
using Fungtopia.Forest;
using Fungtopia.Input;
using Fungtopia.Monsters;
using Fungtopia.Player;
using Fungtopia.Shell;

namespace Fungtopia.Bow;

/// <summary>
/// The player's bow: draws while Fire is held, shoots a poison mushroom-tipped arrow on release
/// toward the aim, flies the arrows of a fixed pool under light gravity, hits monsters
/// (<see cref="HitMonster"/>), and sticks arrows in the ground and in giant mushroom trunks for a
/// while. Publishes <see cref="BowState"/> every tick.
/// </summary>
public sealed class BowModule : IModule, IMessageHandler<PlayerInput>, IMessageHandler<PlayerState>,
    IMessageHandler<MonsterState>
{
    /// <summary>Seconds of holding Fire from 0 to a full draw.</summary>
    public const float FullDrawSeconds = 0.6f;
    /// <summary>Arrow speed at the smallest draw, meters per second.</summary>
    public const float MinSpeed = 25f;
    /// <summary>Arrow speed at a full draw, meters per second.</summary>
    public const float MaxSpeed = 45f;
    /// <summary>Light gravity on arrows, meters per second squared.</summary>
    public const float Gravity = 4f;
    public const int MinDamage = 1;
    public const int MaxDamage = 3;
    public const float PoisonDamagePerSecond = 1f;
    public const float PoisonSeconds = 3f;
    /// <summary>The player's <see cref="HitMonster.Source"/>.</summary>
    public const int PlayerSource = 0;
    /// <summary>Seconds a stuck arrow stays before it vanishes.</summary>
    public const float StickSeconds = 5f;
    /// <summary>A flying arrow that hit nothing vanishes after this many seconds.</summary>
    public const float MaxFlightSeconds = 6f;
    /// <summary>Trunks are hit up to this high above the ground (the shortest giant mushroom's stem); caps are above.</summary>
    public const float TrunkHeight = 6f;
    /// <summary>A flying arrow this far outside the forest square vanishes (it is in the mushroom wall).</summary>
    public const float OutsideMargin = 10f;
    public const int PoolSize = 24;
    public const int MaxMonsters = 256;

    private readonly Arrow[] _arrows = new Arrow[PoolSize];
    private MonsterState[] _monsters = new MonsterState[MaxMonsters];
    private MonsterState[] _incoming = new MonsterState[MaxMonsters];
    private int _monsterCount;
    private int _incomingCount;

    private IMessageBus? _bus;
    private PlayerInput _input;
    private bool _drawing;
    private int _shots;

    /// <summary>How far the bow is drawn, 0..1.</summary>
    public float Draw { get; private set; }

    /// <summary>The latest player state.</summary>
    public PlayerState Player { get; private set; }

    /// <summary>Whether a <see cref="PlayerState"/> has arrived yet.</summary>
    public bool HasPlayer { get; private set; }

    /// <summary>The direction the bow points while drawing (the latest shot direction).</summary>
    public Vector3 AimDirection { get; private set; } = -Vector3.UnitZ;

    /// <summary>Number of ticks run; the view uses it to see new ticks.</summary>
    public long Ticks { get; private set; }

    /// <summary>The state published on the latest tick.</summary>
    public BowState State { get; private set; }

    /// <summary>The arrow pool (every slot, free ones included).</summary>
    public ReadOnlySpan<Arrow> Arrows => _arrows;

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        bus.Subscribe<PlayerInput>(this);
        bus.Subscribe<PlayerState>(this);
        bus.Subscribe<MonsterState>(this);
    }

    public void Handle(in PlayerInput message) => _input = message;

    public void Handle(in PlayerState message)
    {
        Player = message;
        HasPlayer = true;
    }

    /// <summary>Monster states arrive once per tick for every monster; gathered until the next tick.</summary>
    public void Handle(in MonsterState message)
    {
        if (_incomingCount < MaxMonsters)
            _incoming[_incomingCount++] = message;
    }

    /// <summary>Arrow speed for a draw of 0..1.</summary>
    public static float SpeedFor(float draw) => MinSpeed + (MaxSpeed - MinSpeed) * Math.Clamp(draw, 0f, 1f);

    /// <summary>Hit damage for a draw of 0..1: 1 for a quick shot, 2 from a half draw, 3 near a full one.</summary>
    public static int DamageFor(float draw) =>
        MinDamage + (int)MathF.Round((MaxDamage - MinDamage) * Math.Clamp(draw, 0f, 1f));

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("BowModule.Tick before Start.");
        float dt = (float)context.StepSeconds;
        Ticks++;

        // The monsters published since the last tick are the ones arrows can hit now.
        (_monsters, _incoming) = (_incoming, _monsters);
        _monsterCount = _incomingCount;
        _incomingCount = 0;

        UpdateDraw(dt);
        int inFlight = MoveArrows(dt);

        State = new BowState(Draw, inFlight);
        _bus.Publish(State);
    }

    private void UpdateDraw(float dt)
    {
        bool alive = HasPlayer && Player.Alive;
        if (!alive)
        {
            _drawing = false;
            Draw = 0f;
            return;
        }
        Vector3 aim = ShotDirection();
        if (_input.Fire)
        {
            _drawing = true;
            Draw = MathF.Min(1f, Draw + dt / FullDrawSeconds);
            AimDirection = aim;
        }
        else if (_drawing)
        {
            _drawing = false;
            AimDirection = aim;
            Shoot(aim, Draw);
            Draw = 0f;
        }
    }

    /// <summary>The input's aim when it is valid, else the player's facing.</summary>
    private Vector3 ShotDirection()
    {
        Vector3 aim = _input.AimDirection;
        if (_input.HasAim && float.IsFinite(aim.X) && float.IsFinite(aim.Y) && float.IsFinite(aim.Z)
            && aim.LengthSquared() > 1e-6f)
            return Vector3.Normalize(aim);
        float facing = float.IsFinite(Player.Facing) ? Player.Facing : 0f;
        return new Vector3(-MathF.Sin(facing), 0f, -MathF.Cos(facing));
    }

    private void Shoot(Vector3 direction, float draw)
    {
        int slot = FreeSlot();
        // From the character's bow hand (the Player module draws the bow there).
        float facing = float.IsFinite(Player.Facing) ? Player.Facing : 0f;
        Vector3 start = BowGeometry.HoldPoint(Player.Position, facing);
        _arrows[slot] = new Arrow
        {
            Phase = ArrowPhase.Flying,
            Position = start,
            PreviousPosition = start,
            Velocity = direction * SpeedFor(draw),
            Direction = direction,
            Draw = draw,
            Shot = ++_shots,
        };
    }

    /// <summary>A free slot, else the one shot longest ago (stuck arrows go before flying ones).</summary>
    private int FreeSlot()
    {
        int oldestStuck = -1, oldestFlying = -1;
        for (int i = 0; i < PoolSize; i++)
        {
            ref Arrow a = ref _arrows[i];
            if (a.Phase == ArrowPhase.Free)
                return i;
            if (a.Phase == ArrowPhase.Stuck && (oldestStuck < 0 || a.Shot < _arrows[oldestStuck].Shot))
                oldestStuck = i;
            if (a.Phase == ArrowPhase.Flying && (oldestFlying < 0 || a.Shot < _arrows[oldestFlying].Shot))
                oldestFlying = i;
        }
        return oldestStuck >= 0 ? oldestStuck : oldestFlying;
    }

    private int MoveArrows(float dt)
    {
        int inFlight = 0;
        for (int i = 0; i < PoolSize; i++)
        {
            ref Arrow a = ref _arrows[i];
            a.PreviousPosition = a.Position;
            if (a.Phase == ArrowPhase.Stuck)
            {
                a.StuckSeconds += dt;
                if (a.StuckSeconds >= StickSeconds)
                    a.Phase = ArrowPhase.Free;
                continue;
            }
            if (a.Phase != ArrowPhase.Flying)
                continue;

            a.FlightSeconds += dt;
            Vector3 from = a.Position;
            Vector3 velocity = a.Velocity - new Vector3(0f, Gravity * dt, 0f);
            Vector3 to = from + velocity * dt;
            a.Velocity = velocity;
            if (velocity.LengthSquared() > 1e-8f)
                a.Direction = Vector3.Normalize(velocity);

            float tMonster = FirstMonsterHit(from, to, out int monster);
            float tGround = GroundHit(from, to);
            float tTrunk = TrunkHit(from, to);
            float t = MathF.Min(tMonster, MathF.Min(tGround, tTrunk));

            if (t <= 1f && t == tMonster)
            {
                a.Position = Vector3.Lerp(from, to, t);
                a.Phase = ArrowPhase.Free;
                a.Velocity = Vector3.Zero;
                _bus!.Publish(new HitMonster(_monsters[monster].MonsterId, DamageFor(a.Draw), PoisonDamagePerSecond,
                    PoisonSeconds, PlayerSource));
            }
            else if (t <= 1f)
            {
                a.Position = Vector3.Lerp(from, to, t);
                a.Phase = ArrowPhase.Stuck;
                a.StuckIn = t == tGround ? ArrowStuckIn.Ground : ArrowStuckIn.Trunk;
                a.StuckSeconds = 0f;
                a.Velocity = Vector3.Zero;
            }
            else
            {
                a.Position = to;
                if (a.FlightSeconds >= MaxFlightSeconds || !ForestBounds.Contains(to.X, to.Z, -OutsideMargin))
                    a.Phase = ArrowPhase.Free;
                else
                    inFlight++;
            }
        }
        return inFlight;
    }

    /// <summary>
    /// The earliest fraction (0..1) of the segment from→to inside a living monster's sphere
    /// (center a radius above its feet), or +infinity.
    /// </summary>
    private float FirstMonsterHit(Vector3 from, Vector3 to, out int index)
    {
        index = -1;
        float best = float.PositiveInfinity;
        Vector3 d = to - from;
        float dd = d.LengthSquared();
        for (int i = 0; i < _monsterCount; i++)
        {
            ref readonly MonsterState m = ref _monsters[i];
            if (!m.Alive || !(m.Radius > 0f))
                continue;
            Vector3 center = m.Position + new Vector3(0f, m.Radius, 0f);
            float t = SegmentSphere(from, d, dd, center, m.Radius);
            if (t < best)
            {
                best = t;
                index = i;
            }
        }
        return best;
    }

    private static float SegmentSphere(Vector3 from, Vector3 d, float dd, Vector3 center, float radius)
    {
        Vector3 f = from - center;
        float c = f.LengthSquared() - radius * radius;
        if (c <= 0f)
            return 0f; // starts inside
        if (dd <= 0f)
            return float.PositiveInfinity;
        float b = Vector3.Dot(f, d);
        if (b >= 0f)
            return float.PositiveInfinity; // moving away
        float disc = b * b - dd * c;
        if (disc < 0f)
            return float.PositiveInfinity;
        float t = (-b - MathF.Sqrt(disc)) / dd;
        return t <= 1f ? t : float.PositiveInfinity;
    }

    /// <summary>The fraction (0..1) of the segment where it goes into the ground, or +infinity.</summary>
    private static float GroundHit(Vector3 from, Vector3 to)
    {
        if (to.Y - Terrain.HeightAt(to.X, to.Z) > 0f)
            return float.PositiveInfinity;
        if (from.Y - Terrain.HeightAt(from.X, from.Z) <= 0f)
            return 0f;
        float lo = 0f, hi = 1f;
        for (int i = 0; i < 16; i++)
        {
            float mid = 0.5f * (lo + hi);
            Vector3 p = Vector3.Lerp(from, to, mid);
            if (p.Y - Terrain.HeightAt(p.X, p.Z) > 0f)
                lo = mid;
            else
                hi = mid;
        }
        return hi;
    }

    /// <summary>
    /// The earliest fraction (0..1) of the segment where it enters a giant mushroom trunk (a
    /// circle in x/z, up to <see cref="TrunkHeight"/> above the ground), or +infinity.
    /// </summary>
    private static float TrunkHit(Vector3 from, Vector3 to)
    {
        float best = float.PositiveInfinity;
        var p = new Vector2(from.X, from.Z);
        var d = new Vector2(to.X - from.X, to.Z - from.Z);
        float dd = d.LengthSquared();
        // Only trunks near the segment: it is at most a meter long.
        float reach = MathF.Sqrt(dd) + 4f;
        foreach (var o in Obstacles.All)
        {
            Vector2 f = p - o.Center;
            float r = o.Radius;
            if (MathF.Abs(f.X) > reach + r || MathF.Abs(f.Y) > reach + r)
                continue;
            float c = f.LengthSquared() - r * r;
            float t;
            if (c <= 0f)
                t = 0f;
            else
            {
                if (dd <= 0f)
                    continue;
                float b = Vector2.Dot(f, d);
                if (b >= 0f)
                    continue;
                float disc = b * b - dd * c;
                if (disc < 0f)
                    continue;
                t = (-b - MathF.Sqrt(disc)) / dd;
                if (t > 1f)
                    continue;
            }
            if (t >= best)
                continue;
            float y = from.Y + (to.Y - from.Y) * t;
            if (y <= Terrain.HeightAt(o.X, o.Z) + TrunkHeight)
                best = t;
        }
        return best;
    }
}
