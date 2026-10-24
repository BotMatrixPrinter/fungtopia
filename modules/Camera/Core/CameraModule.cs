using System;
using System.Numerics;
using Fungtopia.Forest;
using Fungtopia.Input;
using Fungtopia.Player;
using Fungtopia.Shell;

namespace Fungtopia.Camera;

/// <summary>
/// The third-person camera: orbits a point above the player's head, a little over the right
/// shoulder, turned by <see cref="PlayerInput.Look"/>. Follows the player smoothly, pulls in
/// when the ground or a giant mushroom trunk would block the view and eases back out after.
/// Publishes <see cref="CameraPose"/> every tick.
/// </summary>
public sealed class CameraModule : IModule, IMessageHandler<PlayerState>, IMessageHandler<PlayerInput>
{
    /// <summary>Height of the orbit point above the player's feet, meters.</summary>
    public const float PivotHeight = 1.6f;
    /// <summary>Orbit distance when nothing is in the way, meters.</summary>
    public const float Distance = 5f;
    /// <summary>How far the orbit point sits to the right of the player's head, meters.</summary>
    public const float ShoulderOffset = 0.5f;
    /// <summary>Lowest pitch (looking down), radians: -35 degrees.</summary>
    public const float MinPitch = -35f * MathF.PI / 180f;
    /// <summary>Highest pitch (looking up), radians: +60 degrees.</summary>
    public const float MaxPitch = 60f * MathF.PI / 180f;
    /// <summary>Pitch at the start, radians: looking slightly down.</summary>
    public const float StartPitch = -12f * MathF.PI / 180f;
    /// <summary>How quickly the orbit point catches up with the player, per second.</summary>
    public const float FollowSharpness = 12f;
    /// <summary>How quickly the camera eases back out after a pull-in, per second.</summary>
    public const float ReturnSharpness = 4f;
    /// <summary>A player jump farther than this in one tick (a comeback) snaps the camera, meters.</summary>
    public const float SnapDistance = 4f;
    /// <summary>Room kept between the camera and the ground or a trunk, meters.</summary>
    public const float Clearance = 0.3f;

    private const int GroundSamples = 32;
    private const int GroundRefineSteps = 6;

    private IMessageBus? _bus;
    private Vector3 _feet = ForestBounds.PlayerStart;
    private Vector3 _head;
    private bool _following;

    public float Yaw { get; private set; }
    public float Pitch { get; private set; } = StartPitch;

    /// <summary>The smoothed orbit center (above the feet, before the shoulder offset).</summary>
    public Vector3 Head => _head;

    /// <summary>The current distance from the shoulder point to the camera, meters.</summary>
    public float CurrentDistance { get; private set; } = Distance;

    /// <summary>Number of ticks run; the view uses it to see new ticks.</summary>
    public long Ticks { get; private set; }

    /// <summary>The pose published on the latest tick.</summary>
    public CameraPose Pose { get; private set; }

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        bus.Subscribe<PlayerState>(this);
        bus.Subscribe<PlayerInput>(this);
        _head = _feet + new Vector3(0f, PivotHeight, 0f);
        Pose = Compute(float.PositiveInfinity);
    }

    public void Handle(in PlayerState message)
    {
        Vector3 p = message.Position;
        if (float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z))
            _feet = p;
    }

    public void Handle(in PlayerInput message)
    {
        Vector2 look = message.Look;
        if (float.IsFinite(look.X))
            Yaw = WrapAngle(Yaw - look.X);
        if (float.IsFinite(look.Y))
            Pitch = Math.Clamp(Pitch + look.Y, MinPitch, MaxPitch);
    }

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("CameraModule.Tick before Start.");
        float dt = (float)context.StepSeconds;
        Ticks++;

        Vector3 target = _feet + new Vector3(0f, PivotHeight, 0f);
        if (!_following || Vector3.DistanceSquared(target, _head) > SnapDistance * SnapDistance)
        {
            _head = target;
            _following = true;
        }
        else
        {
            _head += (target - _head) * (1f - MathF.Exp(-FollowSharpness * dt));
        }

        Pose = Compute(dt);
        _bus.Publish(Pose);
    }

    /// <summary>The horizontal view direction for <paramref name="yaw"/> (Godot's convention).</summary>
    public static Vector3 Forward(float yaw) => new(-MathF.Sin(yaw), 0f, -MathF.Cos(yaw));

    /// <summary>The screen-right direction for <paramref name="yaw"/>.</summary>
    public static Vector3 Right(float yaw) => new(MathF.Cos(yaw), 0f, -MathF.Sin(yaw));

    /// <summary>The view direction for yaw and pitch (unit length).</summary>
    public static Vector3 ViewDirection(float yaw, float pitch)
    {
        float c = MathF.Cos(pitch);
        return new Vector3(-MathF.Sin(yaw) * c, MathF.Sin(pitch), -MathF.Cos(yaw) * c);
    }

    /// <summary>
    /// How far one can go from <paramref name="from"/> along unit <paramref name="direction"/>,
    /// up to <paramref name="length"/>, keeping <see cref="Clearance"/> from the ground and trunks.
    /// </summary>
    public static float FreeLength(Vector3 from, Vector3 direction, float length)
    {
        float free = MathF.Min(length, TrunkFreeLength(from, direction, length));
        return MathF.Min(free, GroundFreeLength(from, direction, free));
    }

    private CameraPose Compute(float dt)
    {
        Vector3 right = Right(Yaw);
        Vector3 shoulder = _head + right * FreeLength(_head, right, ShoulderOffset);
        Vector3 back = -ViewDirection(Yaw, Pitch);
        float free = FreeLength(shoulder, back, Distance);
        // Pull in at once so the view is never blocked; ease back out.
        if (free <= CurrentDistance || !float.IsFinite(dt))
            CurrentDistance = free;
        else
            CurrentDistance += (free - CurrentDistance) * (1f - MathF.Exp(-ReturnSharpness * dt));
        var orientation = Quaternion.CreateFromYawPitchRoll(Yaw, Pitch, 0f);
        return new CameraPose(shoulder + back * CurrentDistance, orientation, Yaw);
    }

    private static float GroundFreeLength(Vector3 from, Vector3 direction, float length)
    {
        if (length <= 0f)
            return 0f;
        if (!AboveGround(from))
            return 0f;
        float clear = 0f;
        for (int i = 1; i <= GroundSamples; i++)
        {
            float t = length * i / GroundSamples;
            if (AboveGround(from + direction * t))
            {
                clear = t;
                continue;
            }
            // The first blocked sample: narrow down where the ground starts.
            float blocked = t;
            for (int k = 0; k < GroundRefineSteps; k++)
            {
                float mid = 0.5f * (clear + blocked);
                if (AboveGround(from + direction * mid))
                    clear = mid;
                else
                    blocked = mid;
            }
            return clear;
        }
        return length;
    }

    private static bool AboveGround(Vector3 p) => p.Y >= Terrain.HeightAt(p.X, p.Z) + Clearance;

    /// <summary>Trunks are tall vertical cylinders: the first entry into a trunk widened by the clearance.</summary>
    private static float TrunkFreeLength(Vector3 from, Vector3 direction, float length)
    {
        float best = length;
        float dx = direction.X, dz = direction.Z;
        float a = dx * dx + dz * dz;
        if (a < 1e-8f)
            return best;
        float horizontal = MathF.Sqrt(a) * length;
        foreach (var o in Obstacles.All)
        {
            float r = o.Radius + Clearance;
            float ox = from.X - o.X, oz = from.Z - o.Z;
            float reach = horizontal + r;
            if (ox * ox + oz * oz > reach * reach)
                continue;
            float b = ox * dx + oz * dz;
            float c = ox * ox + oz * oz - r * r;
            if (c < 0f)
            {
                // Starting inside the widened trunk: stay put unless heading away from it.
                if (b < 0f)
                    best = 0f;
                continue;
            }
            float disc = b * b - a * c;
            if (disc < 0f || b >= 0f)
                continue;
            float t = (-b - MathF.Sqrt(disc)) / a;
            if (t < best)
                best = MathF.Max(0f, t);
        }
        return best;
    }

    private static float WrapAngle(float a)
    {
        a %= MathF.Tau;
        if (a > MathF.PI)
            a -= MathF.Tau;
        else if (a <= -MathF.PI)
            a += MathF.Tau;
        return a;
    }
}
