using System;
using System.Numerics;
using Fungtopia.Forest;
using Fungtopia.Monsters;
using Fungtopia.Player;
using Fungtopia.Shell;

namespace Fungtopia.Pet;

/// <summary>
/// The player's pet (a Mushroom Cat or a Mushroom Dog): follows the player at about 2 m,
/// catches up when far and teleports beside them when over 30 m away; goes for the nearest
/// alive monster within 8 m of the player, bites it every second and comes back; runs around
/// giant mushroom trunks and follows the ground; sits by the defeated player. Publishes
/// <see cref="PetState"/> every tick and <see cref="HitMonster"/> for each bite.
/// </summary>
public sealed class PetModule : IModule, IMessageHandler<PlayerState>, IMessageHandler<MonsterState>
{
    /// <summary>How far from the player the pet waits, meters.</summary>
    public const float FollowDistance = 2f;
    /// <summary>A following pet stays put until the player is this much farther than <see cref="FollowDistance"/>.</summary>
    public const float FollowSlack = 0.5f;
    /// <summary>Running speed near the player, meters per second (the player walks at 5).</summary>
    public const float TrotSpeed = 6f;
    /// <summary>Running speed when far behind, meters per second.</summary>
    public const float CatchUpSpeed = 9f;
    /// <summary>Running speed toward a monster, meters per second.</summary>
    public const float AttackSpeed = 7f;
    /// <summary>Over this horizontal distance from the player the pet appears beside them.</summary>
    public const float TeleportDistance = 30f;
    /// <summary>The pet goes for monsters within this horizontal distance of the player.</summary>
    public const float AttackRange = 8f;
    /// <summary>It gives up on a target this far from the player.</summary>
    public const float GiveUpRange = 10f;
    /// <summary>Gap between the pet's and the monster's footprints within which it can bite, meters.</summary>
    public const float BiteReach = 0.4f;
    public const float BiteInterval = 1f;
    public const int BiteDamage = 1;
    /// <summary><see cref="HitMonster.Source"/> of the pet's bites.</summary>
    public const int BiteSource = 1;
    /// <summary>How far from the defeated player the pet sits, meters.</summary>
    public const float SitDistance = 1.1f;
    /// <summary>The pet's round footprint, meters.</summary>
    public const float Radius = 0.35f;
    /// <summary>Fastest turn, radians per second.</summary>
    public const float TurnSpeed = 12f;
    /// <summary>Extra room the pet keeps from trunks when steering around them, meters.</summary>
    public const float Clearance = 0.3f;
    /// <summary>How far ahead the pet looks for trunks in its way, meters.</summary>
    public const float LookAhead = 3f;
    /// <summary>Monsters remembered per tick (more are ignored).</summary>
    public const int MaxMonsters = 128;

    private IMessageBus? _bus;
    private PlayerState _player;
    private bool _hasPlayer;
    private readonly MonsterState[] _monsters = new MonsterState[MaxMonsters];
    private int _monsterCount;
    private float _biteCooldown;
    private bool _following;
    private int _avoidIndex = -1;
    private float _avoidSide;

    /// <summary>The setting that picks the pet (Cat by default).</summary>
    public PetKind Kind { get; set; } = PetKind.Cat;

    public Vector3 Position { get; private set; } =
        Ground(ForestBounds.PlayerStart.X + 1.5f, ForestBounds.PlayerStart.Z + 1f);
    public float Facing { get; private set; }

    /// <summary>Velocity this tick, meters per second (zero on a teleport).</summary>
    public Vector3 Velocity { get; private set; }

    /// <summary>The monster it goes for, or -1.</summary>
    public int TargetId { get; private set; } = -1;

    public bool Attacking => TargetId >= 0;

    /// <summary>Sitting by the defeated player.</summary>
    public bool Sitting { get; private set; }

    /// <summary>Whether it appeared beside the player on the latest tick.</summary>
    public bool Teleported { get; private set; }

    /// <summary>Seconds since its last bite (the view pounces on a bite).</summary>
    public float SinceBite { get; private set; } = 100f;

    /// <summary>Number of ticks run; the view uses it to see new ticks.</summary>
    public long Ticks { get; private set; }

    /// <summary>The state published on the latest tick.</summary>
    public PetState State { get; private set; }

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        bus.Subscribe<PlayerState>(this);
        bus.Subscribe<MonsterState>(this);
        State = MakeState();
    }

    /// <summary>Puts the pet standing on the ground at (x, z) of <paramref name="feet"/>.</summary>
    public void PlaceAt(Vector3 feet, float facing = 0f)
    {
        Position = Ground(feet.X, feet.Z);
        Facing = WrapAngle(facing);
        Velocity = Vector3.Zero;
        _following = false;
        State = MakeState();
    }

    public void Handle(in PlayerState message)
    {
        _player = message;
        _hasPlayer = true;
    }

    public void Handle(in MonsterState message)
    {
        if (_monsterCount < _monsters.Length)
            _monsters[_monsterCount++] = message;
    }

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("PetModule.Tick before Start.");
        float dt = (float)context.StepSeconds;
        Ticks++;
        Teleported = false;
        SinceBite += dt;
        if (_biteCooldown > 0f)
            _biteCooldown -= dt;

        Vector3 start = Position;
        if (_hasPlayer)
            Think(dt);
        Velocity = Teleported || dt <= 0f ? Vector3.Zero : (Position - start) / dt;

        _monsterCount = 0; // the Monsters module publishes every monster again next tick
        State = MakeState();
        _bus.Publish(State);
    }

    private void Think(float dt)
    {
        Vector3 player = _player.Position;
        if (Flat(Position, player) > TeleportDistance)
            TeleportBeside(player);

        if (!_player.Alive)
        {
            TargetId = -1;
            _following = false;
            if (!Sitting)
                Sitting = !MoveToward(player, SitDistance, Speed(Flat(Position, player) - SitDistance), dt);
            if (Sitting)
                TurnToward(Yaw(player - Position), dt);
            return;
        }
        Sitting = false;

        ChooseTarget(player);
        if (TargetId >= 0)
        {
            ref readonly var m = ref FindMonster(TargetId);
            float reach = m.Radius + Radius + BiteReach;
            if (Flat(Position, m.Position) > reach)
                MoveToward(m.Position, reach - 0.1f, AttackSpeed, dt);
            if (Flat(Position, m.Position) <= reach)
            {
                TurnToward(Yaw(m.Position - Position), dt);
                if (_biteCooldown <= 1e-4f)
                {
                    _biteCooldown = BiteInterval;
                    SinceBite = 0f;
                    _bus!.Publish(new HitMonster(m.MonsterId, BiteDamage, 0f, 0f, BiteSource));
                }
            }
            _following = false;
            return;
        }

        // Following: wait near the player, start again once they are a little farther away.
        float distance = Flat(Position, player);
        if (!_following && distance > FollowDistance + FollowSlack)
            _following = true;
        if (_following)
            _following = MoveToward(player, FollowDistance, Speed(distance - FollowDistance), dt);
        if (!_following)
            TurnToward(Yaw(player - Position), dt * 0.4f);
    }

    /// <summary>Keeps a target while it is alive and near the player; else picks the nearest one to the player.</summary>
    private void ChooseTarget(Vector3 player)
    {
        if (TargetId >= 0)
        {
            int i = IndexOf(TargetId);
            if (i >= 0 && _monsters[i].Alive && Flat(_monsters[i].Position, player) <= GiveUpRange)
                return;
            TargetId = -1;
        }
        float best = AttackRange;
        for (int i = 0; i < _monsterCount; i++)
        {
            ref readonly var m = ref _monsters[i];
            if (!m.Alive)
                continue;
            float d = Flat(m.Position, player);
            if (d <= best)
            {
                best = d;
                TargetId = m.MonsterId;
            }
        }
    }

    private int IndexOf(int monsterId)
    {
        for (int i = 0; i < _monsterCount; i++)
            if (_monsters[i].MonsterId == monsterId)
                return i;
        return -1;
    }

    private ref readonly MonsterState FindMonster(int monsterId) => ref _monsters[IndexOf(monsterId)];

    /// <summary>Running speed for a pet <paramref name="behind"/> meters short of where it wants to be.</summary>
    private static float Speed(float behind)
    {
        float top = TrotSpeed + (CatchUpSpeed - TrotSpeed) * Math.Clamp((behind - 3f) / 4f, 0f, 1f);
        // Slows down as it arrives, so it settles instead of stopping dead.
        return Math.Clamp(behind * 4f, 1.5f, top);
    }

    /// <summary>
    /// Runs toward <paramref name="goal"/> until <paramref name="stopAt"/> meters from it,
    /// steering around trunks and staying on the ground. Returns whether it still has a way to go.
    /// </summary>
    private bool MoveToward(Vector3 goal, float stopAt, float speed, float dt)
    {
        var pos = new Vector2(Position.X, Position.Z);
        var to = new Vector2(goal.X, goal.Z) - pos;
        float distance = to.Length();
        float left = distance - stopAt;
        if (left <= 0.01f)
            return false;
        Vector2 dir = Steer(pos, to / distance, distance);
        float step = MathF.Min(speed * dt, left);
        Vector2 next = pos + dir * step;
        ResolveCollisions(ref next);
        if ((next - pos).LengthSquared() > 1e-8f)
            TurnToward(Yaw(new Vector3(next.X - pos.X, 0f, next.Y - pos.Y)), dt);
        Position = Ground(next.X, next.Y);
        return true;
    }

    /// <summary>
    /// The direction to run in: <paramref name="dir"/>, or past the edge of the nearest trunk in
    /// the way (kept on the same side as long as that trunk is in the way).
    /// </summary>
    private Vector2 Steer(Vector2 pos, Vector2 dir, float distance)
    {
        float look = MathF.Min(distance, LookAhead);
        int hit = -1;
        float hitAlong = float.MaxValue;
        var all = Obstacles.All;
        for (int i = 0; i < all.Length; i++)
        {
            var rel = all[i].Center - pos;
            float r = all[i].Radius + Radius + Clearance;
            float along = Vector2.Dot(rel, dir);
            // Behind, beyond the goal, past where it looks, or beside the way.
            if (along <= 0f || along > distance || along - r > look)
                continue;
            float across = dir.X * rel.Y - dir.Y * rel.X;
            if (MathF.Abs(across) >= r || along >= hitAlong)
                continue;
            hit = i;
            hitAlong = along;
        }
        if (hit < 0)
        {
            _avoidIndex = -1;
            return dir;
        }

        var o = all[hit];
        var toCenter = o.Center - pos;
        float d = toCenter.Length();
        float rr = o.Radius + Radius + Clearance;
        var u = toCenter / d;
        // Graze the edge of the padded trunk; once inside the padding, run along it and a little out.
        float angle = d > rr ? MathF.Asin(rr / d) : MathF.PI * 0.5f + 0.25f;
        if (hit != _avoidIndex)
        {
            _avoidIndex = hit;
            float across = dir.X * toCenter.Y - dir.Y * toCenter.X;
            // The trunk is left of the way (across > 0): go right of it, turning clockwise from it.
            _avoidSide = across >= 0f ? -1f : 1f;
        }
        float a = angle * _avoidSide;
        float sin = MathF.Sin(a), cos = MathF.Cos(a);
        return new Vector2(u.X * cos - u.Y * sin, u.X * sin + u.Y * cos);
    }

    /// <summary>Pushes (x, z) out of every trunk it overlaps and back inside the forest's square.</summary>
    private static void ResolveCollisions(ref Vector2 p)
    {
        for (int pass = 0; pass < 3; pass++)
        {
            bool moved = false;
            foreach (var o in Obstacles.All)
            {
                float dx = p.X - o.X, dz = p.Y - o.Z, r = o.Radius + Radius;
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
                p = new Vector2(o.X + dx * push, o.Z + dz * push);
                moved = true;
            }
            float limit = ForestBounds.HalfSize - Radius;
            p = new Vector2(Math.Clamp(p.X, -limit, limit), Math.Clamp(p.Y, -limit, limit));
            if (!moved)
                break;
        }
    }

    /// <summary>Appears on free ground beside the player: to their right, left, behind or in front.</summary>
    private void TeleportBeside(Vector3 player)
    {
        float f = _player.Facing;
        // The player's right is (cos f, -sin f) in (x, z); their forward (-sin f, -cos f).
        var right = new Vector2(MathF.Cos(f), -MathF.Sin(f));
        var forward = new Vector2(-MathF.Sin(f), -MathF.Cos(f));
        var at = new Vector2(player.X, player.Z);
        Vector2 spot = at;
        for (int ring = 1; ring <= 3; ring++)
        {
            float d = 1.5f * ring;
            if (Free(at + right * d, ref spot) || Free(at - right * d, ref spot)
                || Free(at - forward * d, ref spot) || Free(at + forward * d, ref spot))
                break;
        }
        ResolveCollisions(ref spot);
        Position = Ground(spot.X, spot.Y);
        Facing = WrapAngle(f);
        _following = false;
        _avoidIndex = -1;
        Teleported = true;
    }

    private static bool Free(Vector2 p, ref Vector2 spot)
    {
        if (!ForestBounds.Contains(p.X, p.Y, Radius) || Obstacles.Blocks(p.X, p.Y, Radius + Clearance))
            return false;
        spot = p;
        return true;
    }

    private void TurnToward(float target, float dt)
    {
        float diff = WrapAngle(target - Facing);
        float step = TurnSpeed * dt;
        Facing = WrapAngle(Facing + Math.Clamp(diff, -step, step));
    }

    /// <summary>The yaw (Godot's convention) of a horizontal direction.</summary>
    private static float Yaw(Vector3 direction) => MathF.Atan2(-direction.X, -direction.Z);

    private static float Flat(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X, dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private static Vector3 Ground(float x, float z) => new(x, Terrain.HeightAt(x, z), z);

    /// <summary>The angle in (-π, π].</summary>
    public static float WrapAngle(float a)
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

    private PetState MakeState() => new(Kind, Position, Facing, Attacking);
}
