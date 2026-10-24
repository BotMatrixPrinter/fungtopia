using System;
using System.Numerics;
using Fungtopia.Dialogue;
using Fungtopia.Input;
using Fungtopia.Map;
using Fungtopia.Shell;

namespace Fungtopia.Player;

/// <summary>
/// The player character in the whole world (Fungtopia.Map): starts at the home door in Mushroom
/// Town, walks relative to the camera, turns smoothly, jumps, follows the ground, slides along
/// trunks, buildings and the edge of the walkable ground, stands still while a dialogue is open,
/// takes damage, stays down after defeat until the next <see cref="TeleportPlayer"/>.
/// Publishes <see cref="PlayerState"/> every tick and <see cref="PlayerDefeated"/> once per defeat.
/// </summary>
public sealed class PlayerModule : IModule, IMessageHandler<PlayerInput>, IMessageHandler<DamagePlayer>,
    IMessageHandler<TeleportPlayer>, IMessageHandler<DialogueState>
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

    private IMessageBus? _bus;
    private PlayerInput _input;
    private bool _jumpQueued;
    private bool _defeatPending;
    private bool _talking;

    /// <summary>The setting that picks the character (Girl by default).</summary>
    public CharacterKind Character { get; set; } = CharacterKind.Girl;

    public Vector3 Position { get; private set; } = Places.HomeDoor.Position;
    public float Facing { get; private set; } = Places.HomeDoor.Yaw;
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
        bus.Subscribe<TeleportPlayer>(this);
        bus.Subscribe<DialogueState>(this);
        State = MakeState();
    }

    /// <summary>Puts the player standing on the ground at (x, z) of <paramref name="feet"/>.</summary>
    public void PlaceAt(Vector3 feet, float facing = 0f)
    {
        Position = new Vector3(feet.X, Ground.HeightAt(feet.X, feet.Z), feet.Z);
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

    /// <summary>Moves there at once; after a defeat also brings the player back with full health.</summary>
    public void Handle(in TeleportPlayer message)
    {
        Vector3 p = message.Position;
        if (!float.IsFinite(p.X) || !float.IsFinite(p.Z))
            return;
        if (!Alive)
        {
            Alive = true;
            Health = MaxHealth;
            DefeatedSeconds = 0f;
            InvulnerableSeconds = 0f;
        }
        _jumpQueued = false;
        PlaceAt(p, message.Facing);
    }

    public void Handle(in DialogueState message) => _talking = message.Open;

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
            DefeatedSeconds += dt;

        Move(dt);
        _jumpQueued = false;

        State = MakeState();
        _bus.Publish(State);
    }

    private void Move(float dt)
    {
        Vector3 start = Position;
        float vy = Velocity.Y;

        // The wanted horizontal velocity, from the stick relative to the camera's yaw.
        bool canWalk = Alive && !_talking;
        Vector2 move = canWalk ? _input.Move : Vector2.Zero;
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

        if (canWalk && _jumpQueued && Grounded)
        {
            vy = MathF.Sqrt(2f * Gravity * JumpHeight);
            Grounded = false;
        }

        float x = start.X + wantX * dt;
        float z = start.Z + wantZ * dt;
        ResolveCollisions(start.X, start.Z, ref x, ref z);

        float ground = Ground.HeightAt(x, z);
        float y;
        if (Grounded)
        {
            // Walking: stay on the ground, also down slopes (the walkable ground has no ledges).
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

    /// <summary>Whether a player standing at (x, z) is on walkable ground and clear of obstacles.</summary>
    public static bool CanStand(float x, float z) =>
        Areas.Walkable(x, z, Radius) && !Ground.Blocks(x, z, Radius);

    /// <summary>
    /// Pushes (x, z) out of every obstacle it overlaps (trunks, buildings) and back onto the
    /// walkable ground, each along its normal so motion along the edge stays (sliding). Where
    /// that does not settle (a tight corner) the player stays at (startX, startZ).
    /// </summary>
    private static void ResolveCollisions(float startX, float startZ, ref float x, ref float z)
    {
        for (int pass = 0; pass < 4; pass++)
        {
            bool moved = false;
            if (Ground.Blocks(x, z, Radius))
            {
                foreach (var o in Ground.Obstacles)
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
                    float push = (r + 1e-4f) / d;
                    x = o.X + dx * push;
                    z = o.Z + dz * push;
                }
                moved = true;
            }
            if (!Areas.Walkable(x, z, Radius))
            {
                // Back inside along the walkable ground's normal (the gradient of its depth).
                const float h = 0.01f;
                float gx = Areas.Depth(x + h, z) - Areas.Depth(x - h, z);
                float gz = Areas.Depth(x, z + h) - Areas.Depth(x, z - h);
                float g = MathF.Sqrt(gx * gx + gz * gz);
                if (g < 1e-6f)
                    break;
                float push = Radius - Areas.Depth(x, z) + 1e-3f;
                x += gx / g * push;
                z += gz / g * push;
                moved = true;
            }
            if (!moved)
                return;
        }
        if (!CanStand(x, z) && CanStand(startX, startZ))
        {
            x = startX;
            z = startZ;
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
        new(Position, Facing, Velocity, Grounded, Health, MaxHealth, Alive, Character, Areas.At(Position.X, Position.Z));
}
