using System;
using System.Numerics;
using Fungtopia.Bow;
using Fungtopia.Forest;
using Fungtopia.Input;
using Fungtopia.Map;
using Fungtopia.Player;
using Fungtopia.Shell;
using Fungtopia.StartScreen;
using Fungtopia.Wardrobe;

namespace Fungtopia.Camera;

/// <summary>
/// The third-person camera: orbits a point above the player's head, a little over the right
/// shoulder, turned by <see cref="PlayerInput.Look"/>. Follows the player smoothly, pulls in
/// when the ground or a giant mushroom trunk would block the view and eases back out after.
/// While the wardrobe, the shop or the start screen is open it swings round to a still front view
/// of the player standing in the left half of the screen (kept inside the walls at home), and
/// swings back when they close. While the bow is drawn it eases to an aim camera beside the head
/// on the bow's side (over the right shoulder), looking the same way, so the bow held on the right
/// shows beside the big head.
/// Publishes <see cref="CameraPose"/> every tick.
/// </summary>
public sealed class CameraModule : IModule, IMessageHandler<PlayerState>, IMessageHandler<PlayerInput>,
    IMessageHandler<WardrobeState>, IMessageHandler<StartScreenState>, IMessageHandler<BowState>
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

    /// <summary>Front view: distance from the player's chest to the camera, meters.</summary>
    public const float FrontDistance = 2.75f;
    /// <summary>
    /// Front view: height of the camera (and the point it looks at) above the feet, meters: the
    /// chest, about half the head's height (<see cref="BowGeometry.HeadUp"/>; the body is 0.36..0.88 m).
    /// </summary>
    public const float ChestHeight = 0.55f;
    /// <summary>The camera's vertical field of view (the view's <c>Camera3D.Fov</c>), radians: 70 degrees.</summary>
    public const float FieldOfView = CameraPose.DefaultFieldOfView;
    /// <summary>The screen's width over its height the front view is framed for (1920 x 1080).</summary>
    public const float FrameAspect = 16f / 9f;
    /// <summary>
    /// Front view: where the chest shows across the screen, -1 its left edge, 1 its right edge:
    /// the middle of the left half, clear of a panel on the right.
    /// </summary>
    public const float FrameScreenX = -0.5f;
    /// <summary>Front view: how far the camera turns right from looking straight at the chest, radians.</summary>
    public static readonly float FrameAngle = MathF.Atan(-FrameScreenX * MathF.Tan(FieldOfView / 2f) * FrameAspect);
    /// <summary>How long the swing to the front view, or back, takes, seconds.</summary>
    public const float TransitionSeconds = 1f;
    /// <summary>The home room's height, floor to ceiling, meters (the Map draws it 4 m high).</summary>
    public const float RoomHeight = 4f;
    /// <summary>Front view at home: the least room the camera may pull in to before it goes round to another side, meters.</summary>
    public const float MinRoomDistance = 2.2f;
    /// <summary>Front view at home: how much the camera rises per meter it pulls in, so the whole player still shows.</summary>
    public const float RaisePerMeter = 0.5f;
    /// <summary>Front view at home: the step of the search for a side of the player with room for the camera, radians.</summary>
    public const float RoomSearchStep = 10f * MathF.PI / 180f;

    /// <summary>
    /// Aim camera: how far its orbit point sits to the side of the head's center
    /// (<see cref="BowGeometry.HeadUp"/>), meters, on <see cref="AimSideSign"/>'s side: the bow's.
    /// </summary>
    public const float AimSide = 0.9f;
    /// <summary>
    /// Aim camera: which side of the head its orbit point is on, 1 right, -1 left: the side of
    /// <see cref="BowGeometry"/>'s hold point (<see cref="BowGeometry.HoldRight"/>), so it looks over that shoulder.
    /// </summary>
    public static float AimSideSign => BowGeometry.HoldRight < 0f ? -1f : 1f;
    /// <summary>Aim camera: how far its orbit point sits above the head's center, meters.</summary>
    public const float AimRaise = 0.25f;
    /// <summary>Aim camera: distance from its orbit point to the camera when nothing is in the way, meters.</summary>
    public const float AimDistance = 2.75f;
    /// <summary>Aim camera: the vertical field of view at a full draw, radians: 62 degrees.</summary>
    public const float AimFieldOfView = 62f * MathF.PI / 180f;
    /// <summary>How long the ease to the aim camera, or back, takes, seconds.</summary>
    public const float AimTransitionSeconds = 0.25f;
    /// <summary>How long the aim camera stays after a shot (the draw back at 0), seconds.</summary>
    public const float AimHoldSeconds = 0.4f;

    private const int GroundSamples = 32;
    private const int GroundRefineSteps = 6;

    private IMessageBus? _bus;
    private Vector3 _feet = ForestBounds.PlayerStart;
    private Vector3 _head;
    private bool _following;
    private float _facing;
    private bool _wardrobeOpen;
    private bool _startOpen;
    private float _transition;
    private float _frontYaw;
    private float _draw;
    private float _aimHold;
    private float _aim;
    private float _zoom;
    private float _lastWanted = Distance;

    public float Yaw { get; private set; }
    public float Pitch { get; private set; } = StartPitch;

    /// <summary>The smoothed orbit center (above the feet, before the shoulder offset).</summary>
    public Vector3 Head => _head;

    /// <summary>The current distance from the shoulder point to the camera, meters.</summary>
    public float CurrentDistance { get; private set; } = Distance;

    /// <summary>A screen that wants the front view (wardrobe, shop, start screen) is open.</summary>
    public bool FrontViewWanted => _wardrobeOpen || _startOpen;

    /// <summary>How far the camera is into the front view, eased: 0 the normal orbit, 1 the front view.</summary>
    public float Framing => SmoothStep(_transition);

    /// <summary>
    /// The yaw of the front view's arm (the camera's horizontal direction as seen from the
    /// player's chest, plus π): fixed when the front view begins, from the player's facing, so
    /// turning the character on the screen does not move the camera. At home it may be turned
    /// away from a wall that leaves no room in front.
    /// </summary>
    public float FrontYaw => _frontYaw;

    /// <summary>The bow is drawn, or was shot less than <see cref="AimHoldSeconds"/> ago (and no screen wants the front view).</summary>
    public bool AimWanted => _aimHold > 0f && !FrontViewWanted;

    /// <summary>How far the camera is into the aim camera, eased: 0 the normal orbit, 1 the aim camera.</summary>
    public float Aiming => SmoothStep(_aim);

    /// <summary>
    /// The aim camera's orbit point for a head orbit center <paramref name="head"/> (<see cref="PivotHeight"/>
    /// above the feet) and yaw <paramref name="yaw"/>, nothing in the way: <see cref="AimSide"/> to the
    /// bow's side (<see cref="AimSideSign"/>) of the head's center and <see cref="AimRaise"/> above it.
    /// </summary>
    public static Vector3 AimPivot(Vector3 head, float yaw) =>
        head + new Vector3(0f, BowGeometry.HeadUp + AimRaise - PivotHeight, 0f) + Right(yaw) * (AimSideSign * AimSide);

    /// <summary>
    /// The aim camera for a player with feet at <paramref name="feet"/>, with nothing in the way:
    /// <see cref="AimDistance"/> behind <see cref="AimPivot"/> along the view of <paramref name="yaw"/>
    /// and <paramref name="pitch"/>, looking that same way, at <see cref="AimFieldOfView"/>.
    /// </summary>
    public static CameraPose AimView(Vector3 feet, float yaw, float pitch)
    {
        Vector3 pivot = AimPivot(feet + new Vector3(0f, PivotHeight, 0f), yaw);
        return new CameraPose(pivot - ViewDirection(yaw, pitch) * AimDistance,
            Quaternion.CreateFromYawPitchRoll(yaw, pitch, 0f), yaw, AimFieldOfView);
    }

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
        bus.Subscribe<WardrobeState>(this);
        bus.Subscribe<StartScreenState>(this);
        bus.Subscribe<BowState>(this);
        _head = _feet + new Vector3(0f, PivotHeight, 0f);
        Pose = Compute(float.PositiveInfinity);
    }

    public void Handle(in PlayerState message)
    {
        Vector3 p = message.Position;
        if (float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z))
            _feet = p;
        if (float.IsFinite(message.Facing))
            _facing = message.Facing;
    }

    public void Handle(in WardrobeState message) => _wardrobeOpen = message.Open;

    public void Handle(in StartScreenState message) => _startOpen = message.Open;

    public void Handle(in BowState message) => _draw = float.IsFinite(message.Draw) ? Math.Clamp(message.Draw, 0f, 1f) : 0f;

    public void Handle(in PlayerInput message)
    {
        // The open screens own the input; the orbit comes back to where it was.
        if (FrontViewWanted)
            return;
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
        bool snapped = false;
        if (!_following || Vector3.DistanceSquared(target, _head) > SnapDistance * SnapDistance)
        {
            _head = target;
            _following = true;
            snapped = true;
        }
        else
        {
            _head += (target - _head) * (1f - MathF.Exp(-FollowSharpness * dt));
        }

        if (FrontViewWanted)
        {
            // Fixed for the whole view (a teleport, like the start screen's, chooses again).
            if (_transition <= 0f || snapped)
                _frontYaw = ChooseFrontYaw(_feet, _facing);
            _transition += dt / TransitionSeconds;
            if (_transition > 1f - 1e-4f)
                _transition = 1f;
        }
        else
        {
            _transition -= dt / TransitionSeconds;
            if (_transition < 1e-4f)
                _transition = 0f;
        }

        // The aim camera: while drawing, and a moment after the shot.
        _aimHold = _draw > 0f ? AimHoldSeconds : MathF.Max(0f, _aimHold - dt);
        float step = dt / AimTransitionSeconds;
        bool aim = AimWanted;
        _aim = Math.Clamp(_aim + (aim ? step : -step), 0f, 1f);
        // The zoom follows the draw, no faster than the ease, so a shot does not jump it.
        float zoom = aim ? _draw : 0f;
        _zoom = zoom > _zoom ? MathF.Min(zoom, _zoom + step) : MathF.Max(zoom, _zoom - step);

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

    /// <summary>Whether feet at <paramref name="feet"/> are in the home room (<see cref="Areas.Home"/>).</summary>
    public static bool AtHome(Vector3 feet) =>
        Areas.Home.Contains(feet.X, feet.Z, -1f) && MathF.Abs(feet.Y - Areas.HomeFloorY) < RoomHeight;

    /// <summary>
    /// The arm yaw of the front view of a player with feet at <paramref name="feet"/> facing
    /// <paramref name="facing"/>: straight in front (<paramref name="facing"/> + π), or at home,
    /// when the walls leave less than <see cref="MinRoomDistance"/> there, the nearest side (in
    /// <see cref="RoomSearchStep"/>s) that has that room, else the one with the most.
    /// </summary>
    public static float ChooseFrontYaw(Vector3 feet, float facing)
    {
        float front = WrapAngle(facing + MathF.PI);
        if (!AtHome(feet))
            return front;
        Vector3 chest = feet + new Vector3(0f, ChestHeight, 0f);
        float best = front, bestFree = -1f;
        int steps = (int)MathF.Round(MathF.PI / RoomSearchStep);
        for (int k = 0; k <= steps; k++)
        {
            for (int side = 1; side >= -1; side -= 2)
            {
                float yaw = WrapAngle(front + side * k * RoomSearchStep);
                float free = RoomFreeLength(chest, -Forward(yaw));
                if (free >= MinRoomDistance)
                    return yaw;
                if (free > bestFree)
                {
                    best = yaw;
                    bestFree = free;
                }
                if (k == 0)
                    break;
            }
        }
        return best;
    }

    /// <summary>
    /// The front view of a player with feet at <paramref name="feet"/> (arm yaw <paramref name="frontYaw"/>,
    /// nothing in the way): <see cref="FrontDistance"/> from the chest on that side, level, at
    /// <see cref="ChestHeight"/>, turned right by <see cref="FrameAngle"/> so the player shows in the
    /// left half of the screen.
    /// </summary>
    public static CameraPose FrontView(Vector3 feet, float frontYaw)
    {
        Vector3 chest = feet + new Vector3(0f, ChestHeight, 0f);
        float yaw = WrapAngle(frontYaw - FrameAngle);
        return new CameraPose(chest - Forward(frontYaw) * FrontDistance, Quaternion.CreateFromYawPitchRoll(yaw, 0f, 0f), yaw);
    }

    /// <summary>
    /// How far one can go from <paramref name="from"/> along unit <paramref name="direction"/>
    /// inside the home room (<see cref="Areas.Home"/>, floor to <see cref="RoomHeight"/>), keeping
    /// <see cref="Clearance"/> from the walls, floor and ceiling; 0 from outside it.
    /// </summary>
    public static float RoomFreeLength(Vector3 from, Vector3 direction)
    {
        var room = Areas.Home;
        float floor = Areas.HomeFloorY;
        float free = float.PositiveInfinity;
        free = MathF.Min(free, SlabExit(from.X, direction.X, room.MinX + Clearance, room.MaxX - Clearance));
        free = MathF.Min(free, SlabExit(from.Y, direction.Y, floor + Clearance, floor + RoomHeight - Clearance));
        free = MathF.Min(free, SlabExit(from.Z, direction.Z, room.MinZ + Clearance, room.MaxZ - Clearance));
        return free;
    }

    private static float SlabExit(float p, float d, float min, float max)
    {
        if (p < min || p > max)
            return 0f;
        if (d > 1e-6f)
            return (max - p) / d;
        if (d < -1e-6f)
            return (min - p) / d;
        return float.PositiveInfinity;
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
        // At home the walls, floor and ceiling are what can be in the way.
        bool home = AtHome(_feet);
        Vector3 right = Right(Yaw);
        Vector3 shoulder = _head + right * (home ? MathF.Min(ShoulderOffset, RoomFreeLength(_head, right)) : FreeLength(_head, right, ShoulderOffset));
        float wanted = Distance;
        float aiming = Aiming;
        if (aiming > 0f)
        {
            // The aim camera's orbit point, beside the head on the bow's side (kept clear of trunks and walls there).
            Vector3 raised = _head + new Vector3(0f, BowGeometry.HeadUp + AimRaise - PivotHeight, 0f);
            Vector3 side = right * AimSideSign;
            float aside = home ? MathF.Min(AimSide, RoomFreeLength(raised, side)) : FreeLength(raised, side, AimSide);
            shoulder = Vector3.Lerp(shoulder, raised + side * aside, aiming);
            wanted = Distance + (AimDistance - Distance) * aiming;
        }
        Vector3 back = -ViewDirection(Yaw, Pitch);
        float free = home ? MathF.Min(wanted, RoomFreeLength(shoulder, back)) : FreeLength(shoulder, back, wanted);
        // Pull in at once so the view is never blocked; ease back out (but follow the aim's own
        // lengthening at once, so leaving the aim camera is as smooth as entering it).
        if (free <= CurrentDistance || !float.IsFinite(dt))
            CurrentDistance = free;
        else
            CurrentDistance = MathF.Min(free, MathF.Max(CurrentDistance + (free - CurrentDistance) * (1f - MathF.Exp(-ReturnSharpness * dt)),
                CurrentDistance + MathF.Max(0f, wanted - _lastWanted)));
        _lastWanted = wanted;
        float fov = FieldOfView + (AimFieldOfView - FieldOfView) * SmoothStep(_zoom);
        float framing = Framing;
        if (framing <= 0f)
        {
            var orientation = Quaternion.CreateFromYawPitchRoll(Yaw, Pitch, 0f);
            return new CameraPose(shoulder + back * CurrentDistance, orientation, Yaw, fov);
        }

        // Swing round the player: blend the arm's yaw, pitch, length and center, not the
        // positions, so the camera never passes through them.
        Vector3 chest = _head + new Vector3(0f, ChestHeight - PivotHeight, 0f);
        float frontDistance = FrontDistance, frontPitch = 0f;
        if (home)
        {
            // Short of room: pull in, and rise a little to look down on the whole player.
            float flat = MathF.Min(FrontDistance, RoomFreeLength(chest, -Forward(_frontYaw)));
            float raise = (FrontDistance - flat) * RaisePerMeter;
            frontPitch = -MathF.Atan2(raise, flat);
            frontDistance = MathF.Sqrt(flat * flat + raise * raise);
        }
        float armYaw = WrapAngle(Yaw + WrapAngle(_frontYaw - Yaw) * framing);
        float pitch = Pitch + (frontPitch - Pitch) * framing;
        Vector3 center = Vector3.Lerp(shoulder, chest, framing);
        Vector3 away = -ViewDirection(armYaw, pitch);
        float distance = CurrentDistance + (frontDistance - CurrentDistance) * framing;
        distance = home ? MathF.Min(distance, RoomFreeLength(center, away)) : FreeLength(center, away, distance);
        float yaw = WrapAngle(armYaw - FrameAngle * framing);
        return new CameraPose(center + away * distance, Quaternion.CreateFromYawPitchRoll(yaw, pitch, 0f), yaw, fov);
    }

    private static float SmoothStep(float t) => t * t * (3f - 2f * t);

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
