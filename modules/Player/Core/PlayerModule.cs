using System;
using System.Numerics;
using Fungtopia.Forest;
using Fungtopia.Input;
using Fungtopia.Shell;

namespace Fungtopia.Player;

/// <summary>
/// The player character in the Mushroom Forest: walks relative to the camera, turns smoothly,
/// jumps, follows the ground, slides along giant mushroom trunks and the forest's edge, takes
/// damage, and comes back at the start a while after defeat. Publishes <see cref="PlayerState"/>
/// every tick and <see cref="PlayerDefeated"/> once per defeat.
/// </summary>
public sealed class PlayerModule : IModule, IMessageHandler<PlayerInput>, IMessageHandler<DamagePlayer>
{
    public const float WalkSpeed = 5f;
    public const float JumpHeight = 1.2f;
    public const float Gravity = 20f;
    /// <summary>Fastest turn, radians per second.</summary>
    public const float TurnSpeed = 10f;
    /// <summary>The player's round footprint, meters.</summary>
    public const float Radius = 0.4f;
    public const int MaxHealth = 10;
    public const float InvulnerableAfterHit = 0.7f;
    public const float ComebackDelay = 3f;

    private IMessageBus? _bus;
    private PlayerInput _input;
    private bool _jumpQueued;
    private bool _defeatPending;

    /// <summary>The setting that picks the character (Girl by default).</summary>
    public CharacterKind Character { get; set; } = CharacterKind.Girl;

    public Vector3 Position { get; private set; } = ForestBounds.PlayerStart;
    public float Facing { get; private set; }
    public Vector3 Velocity { get; private set; }
    public bool Grounded { get; private set; } = true;
    public int Health { get; private set; } = MaxHealth;
    public bool Alive { get; private set; } = true;

    /// <summary>The camera's yaw as the player knows it (Godot's convention, like <see cref="Facing"/>).</summary>
    public float CameraYaw { get; private set; }

    /// <summary>Seconds of invulnerability left after a hit (the view flashes meanwhile).</summary>
    public float InvulnerableSeconds { get; private set; }

    /// <summary>Seconds since defeat while not <see cref="Alive"/>.</summary>
    public float DefeatedSeconds { get; private set; }

    /// <summary>Number of ticks run; the view uses it to see new ticks.</summary>
    public long Ticks { get; private set; }

    /// <summary>The state published on the latest tick.</summary>
    public PlayerState State { get; private set; }

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        bus.Subscribe<PlayerInput>(this);
        bus.Subscribe<DamagePlayer>(this);
        State = MakeState();
    }

    /// <summary>Puts the player standing on the ground at (x, z) of <paramref name="feet"/>.</summary>
    public void PlaceAt(Vector3 feet, float facing = 0f)
    {
        Position = new Vector3(feet.X, Terrain.HeightAt(feet.X, feet.Z), feet.Z);
        Facing = WrapAngle(facing);
        Velocity = Vector3.Zero;
        Grounded = true;
        State = MakeState();
    }

    public void Handle(in PlayerInput message)
    {
        _input = message;
        if (message.Jump)
            _jumpQueued = true;
        Vector3 aim = message.AimDirection;
        if (message.HasAim && aim.X * aim.X + aim.Z * aim.Z > 0.0001f)
            CameraYaw = MathF.Atan2(-aim.X, -aim.Z);
        else if (float.IsFinite(message.Look.X))
            CameraYaw = WrapAngle(CameraYaw - message.Look.X);
    }

    public void Handle(in DamagePlayer message)
    {
        if (!Alive || message.Amount <= 0 || InvulnerableSeconds > 0f)
            return;
        Health = Math.Max(0, Health - message.Amount);
        InvulnerableSeconds = InvulnerableAfterHit;
        if (Health == 0)
        {
            Alive = false;
            DefeatedSeconds = 0f;
            _defeatPending = true;
        }
    }

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("PlayerModule.Tick before Start.");
        float dt = (float)context.StepSeconds;
        Ticks++;

        if (InvulnerableSeconds > 0f)
            InvulnerableSeconds = MathF.Max(0f, InvulnerableSeconds - dt);

        if (_defeatPending)
        {
            _defeatPending = false;
            _bus.Publish(new PlayerDefeated(Position));
        }
        if (!Alive)
        {
            DefeatedSeconds += dt;
            if (DefeatedSeconds >= ComebackDelay)
                ComeBack();
        }

        Move(dt);
        _jumpQueued = false;

        State = MakeState();
        _bus.Publish(State);
    }

    private void ComeBack()
    {
        Alive = true;
        Health = MaxHealth;
        DefeatedSeconds = 0f;
        InvulnerableSeconds = 0f;
        PlaceAt(ForestBounds.PlayerStart, 0f);
    }

    private void Move(float dt)
    {
        Vector3 start = Position;
        float vy = Velocity.Y;

        // The wanted horizontal velocity, from the stick relative to the camera's yaw.
        Vector2 move = Alive ? _input.Move : Vector2.Zero;
        if (!float.IsFinite(move.X) || !float.IsFinite(move.Y))
            move = Vector2.Zero;
        float length = move.Length();
        if (length > 1f)
            move /= length;
        float sin = MathF.Sin(CameraYaw), cos = MathF.Cos(CameraYaw);
        // Camera forward is (-sin, -cos) in (x, z); its right is (cos, -sin).
        float wantX = (move.X * cos - move.Y * sin) * WalkSpeed;
        float wantZ = (-move.X * sin - move.Y * cos) * WalkSpeed;

        // Facing: toward the aim while Fire is held, else toward the move direction.
        if (Alive)
        {
            Vector3 aim = _input.AimDirection;
            if (_input.Fire && _input.HasAim && aim.X * aim.X + aim.Z * aim.Z > 0.0001f)
                TurnToward(MathF.Atan2(-aim.X, -aim.Z), dt);
            else if (_input.Fire)
                TurnToward(CameraYaw, dt);
            else if (length > 0.05f)
                TurnToward(MathF.Atan2(-wantX, -wantZ), dt);
        }

        if (Alive && _jumpQueued && Grounded)
        {
            vy = MathF.Sqrt(2f * Gravity * JumpHeight);
            Grounded = false;
        }

        float x = start.X + wantX * dt;
        float z = start.Z + wantZ * dt;
        ResolveCollisions(ref x, ref z);

        float ground = Terrain.HeightAt(x, z);
        float y;
        if (Grounded)
        {
            // Walking: stay on the ground, also down slopes (the forest has no ledges).
            y = ground;
            vy = 0f;
        }
        else
        {
            // Exact for constant gravity, so the jump peaks at JumpHeight whatever the step.
            y = start.Y + vy * dt - 0.5f * Gravity * dt * dt;
            vy -= Gravity * dt;
            if (y <= ground)
            {
                y = ground;
                vy = 0f;
                Grounded = true;
            }
        }

        Position = new Vector3(x, y, z);
        Velocity = new Vector3((x - start.X) / dt, vy, (z - start.Z) / dt);
    }

    /// <summary>Pushes (x, z) out of every trunk it overlaps and back inside the forest's square.</summary>
    private static void ResolveCollisions(ref float x, ref float z)
    {
        for (int pass = 0; pass < 3; pass++)
        {
            bool moved = false;
            foreach (var o in Obstacles.All)
            {
                float dx = x - o.X, dz = z - o.Z, r = o.Radius + Radius;
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
                // Out to the trunk's edge along its normal; motion along the edge stays (sliding).
                float push = (r + 1e-4f) / d;
                x = o.X + dx * push;
                z = o.Z + dz * push;
                moved = true;
            }
            float limit = ForestBounds.HalfSize - Radius;
            x = Math.Clamp(x, -limit, limit);
            z = Math.Clamp(z, -limit, limit);
            if (!moved)
                break;
        }
    }

    private void TurnToward(float target, float dt)
    {
        float diff = WrapAngle(target - Facing);
        float step = TurnSpeed * dt;
        Facing = WrapAngle(Facing + Math.Clamp(diff, -step, step));
    }

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

    private PlayerState MakeState() =>
        new(Position, Facing, Velocity, Grounded, Health, MaxHealth, Alive, Character);
}
