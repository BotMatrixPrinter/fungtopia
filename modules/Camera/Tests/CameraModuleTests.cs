using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Bow;
using Fungtopia.Forest;
using Fungtopia.Input;
using Fungtopia.Map;
using Fungtopia.Player;
using Fungtopia.Shell;
using Fungtopia.StartScreen;
using Fungtopia.Wardrobe;
using Xunit;

namespace Fungtopia.Camera.Tests;

public class CameraModuleTests
{
    private const double Step = 1.0 / 60;
    private const float Degree = MathF.PI / 180f;

    /// <summary>Fake of the shell's bus: delivers player, input, bow and screen messages and records every pose published.</summary>
    private sealed class FakeBus : IMessageBus
    {
        private IMessageHandler<PlayerState>? _state;
        private IMessageHandler<PlayerInput>? _input;
        private IMessageHandler<WardrobeState>? _wardrobe;
        private IMessageHandler<StartScreenState>? _start;
        private IMessageHandler<BowState>? _bow;
        public readonly List<CameraPose> Poses = new(1024);
        public int OtherMessages;

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct
        {
            if (handler is IMessageHandler<PlayerState> state && typeof(T) == typeof(PlayerState))
                _state = state;
            if (handler is IMessageHandler<PlayerInput> input && typeof(T) == typeof(PlayerInput))
                _input = input;
            if (handler is IMessageHandler<WardrobeState> wardrobe && typeof(T) == typeof(WardrobeState))
                _wardrobe = wardrobe;
            if (handler is IMessageHandler<StartScreenState> start && typeof(T) == typeof(StartScreenState))
                _start = start;
            if (handler is IMessageHandler<BowState> bow && typeof(T) == typeof(BowState))
                _bow = bow;
        }

        public void Publish<T>(in T message) where T : struct
        {
            if (message is CameraPose pose)
                Poses.Add(pose);
            else if (message is PlayerState state)
                _state?.Handle(in state);
            else if (message is PlayerInput input)
                _input?.Handle(in input);
            else if (message is WardrobeState wardrobe)
                _wardrobe?.Handle(in wardrobe);
            else if (message is StartScreenState start)
                _start?.Handle(in start);
            else if (message is BowState bow)
                _bow?.Handle(in bow);
            else
                OtherMessages++;
        }
    }

    private static (CameraModule camera, FakeBus bus) Started()
    {
        var bus = new FakeBus();
        var camera = new CameraModule();
        camera.Start(bus);
        return (camera, bus);
    }

    private static PlayerState State(Vector3 feet) =>
        new(feet, 0f, Vector3.Zero, true, 10, 10, true, CharacterKind.Girl);

    private static Vector3 OnGround(float x, float z) => new(x, Terrain.HeightAt(x, z), z);

    private static void Look(FakeBus bus, float yawRight, float pitchUp) =>
        bus.Publish(new PlayerInput(Vector2.Zero, new Vector2(yawRight, pitchUp), false, false, false, Vector3.Zero));

    private static void Run(CameraModule camera, FakeBus bus, Vector3 feet, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            bus.Publish(State(feet));
            camera.Tick(new TickContext(camera.Ticks, Step));
        }
    }

    /// <summary>Ticks with the player standing at <paramref name="feet"/> facing <paramref name="facing"/>, with the screens' states.</summary>
    private static void RunScreens(CameraModule camera, FakeBus bus, Vector3 feet, float facing, bool wardrobe, bool start, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            bus.Publish(new PlayerState(feet, facing, Vector3.Zero, true, 10, 10, true, CharacterKind.Girl));
            bus.Publish(new WardrobeState(wardrobe, false));
            bus.Publish(new StartScreenState(start));
            camera.Tick(new TickContext(camera.Ticks, Step));
        }
    }

    /// <summary>Where <paramref name="point"/> shows: x right, y up, as tangents of the angle from the view's center; z its depth ahead.</summary>
    private static Vector3 OnScreen(CameraPose pose, Vector3 point)
    {
        Vector3 local = Vector3.Transform(point - pose.Position, Quaternion.Inverse(pose.Orientation));
        return new Vector3(local.X / -local.Z, local.Y / -local.Z, -local.Z);
    }

    private static Vector3 ShoulderOf(CameraModule camera) => camera.Head + CameraModule.Right(camera.Yaw) * CameraModule.ShoulderOffset;

    // ---- the pose and its message ----

    [Fact]
    public void PublishesOneCameraPosePerTick()
    {
        var (camera, bus) = Started();
        Run(camera, bus, ForestBounds.PlayerStart, 3);
        Assert.Equal(3, bus.Poses.Count);
        Assert.Equal(0, bus.OtherMessages);
        Assert.Equal(camera.Pose.Position, bus.Poses[2].Position);
        Assert.Equal(3, camera.Ticks);
    }

    [Fact]
    public void TickBeforeStartThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new CameraModule().Tick(new TickContext(0, Step)));
    }

    [Fact]
    public void RestsAboutFiveMetersBehindAndOverTheRightShoulder()
    {
        var (camera, bus) = Started();
        var feet = ForestBounds.PlayerStart; // the open start clearing
        Run(camera, bus, feet, 30);

        var pose = bus.Poses[^1];
        var head = feet + new Vector3(0f, CameraModule.PivotHeight, 0f);
        Assert.Equal(0f, Vector3.Distance(head, camera.Head), 3);
        Vector3 forward = CameraModule.Forward(pose.Yaw), right = CameraModule.Right(pose.Yaw);
        Vector3 offset = pose.Position - head;
        Assert.InRange(Vector3.Dot(offset, forward), -5.2f, -4.5f);       // behind
        Assert.InRange(Vector3.Dot(offset, right), 0.3f, 0.7f);           // right shoulder
        Assert.InRange(offset.Y, 0.5f, 1.5f);                             // a little above, looking down
        Assert.Equal(CameraModule.Distance, Vector3.Distance(ShoulderOf(camera), pose.Position), 3);
    }

    [Fact]
    public void OrientationLooksAlongYawAndPitch()
    {
        var (camera, bus) = Started();
        Look(bus, 0.7f, 0.3f);
        Run(camera, bus, ForestBounds.PlayerStart, 1);
        var pose = bus.Poses[^1];

        Vector3 look = Vector3.Transform(-Vector3.UnitZ, pose.Orientation);
        Vector3 expected = CameraModule.ViewDirection(camera.Yaw, camera.Pitch);
        Assert.True(Vector3.Distance(expected, look) < 1e-4f, $"looks along {look}, expected {expected}");
        Vector3 up = Vector3.Transform(Vector3.UnitY, pose.Orientation);
        Assert.True(up.Y > 0.5f, "the camera stays upright");
        Vector3 screenRight = Vector3.Transform(Vector3.UnitX, pose.Orientation);
        Assert.True(Vector3.Distance(CameraModule.Right(camera.Yaw), screenRight) < 1e-4f);
        Assert.Equal(camera.Yaw, pose.Yaw);
    }

    // ---- orbit limits ----

    [Fact]
    public void LookRightTurnsTheYawRightInGodotsConvention()
    {
        var (camera, bus) = Started();
        Assert.Equal(0f, camera.Yaw);
        Look(bus, 0.25f, 0f);
        Assert.Equal(-0.25f, camera.Yaw, 5);
        // Looking right from facing -Z turns the view toward +X.
        Assert.True(CameraModule.Forward(camera.Yaw).X > 0f);
    }

    [Fact]
    public void YawWrapsAroundInsteadOfGrowing()
    {
        var (camera, bus) = Started();
        for (int i = 0; i < 100; i++)
            Look(bus, 0.5f, 0f);
        Assert.InRange(camera.Yaw, -MathF.PI, MathF.PI);
        float expected = MathF.IEEERemainder(-50f, MathF.Tau);
        Assert.Equal(expected, camera.Yaw, 3);
    }

    [Fact]
    public void PitchIsClampedToAboutMinus35AndPlus60Degrees()
    {
        var (camera, bus) = Started();
        for (int i = 0; i < 50; i++)
            Look(bus, 0f, 0.1f);
        Assert.Equal(60f * Degree, camera.Pitch, 4);
        Assert.Equal(CameraModule.MaxPitch, camera.Pitch);

        Look(bus, 0f, -100f);
        Assert.Equal(-35f * Degree, camera.Pitch, 4);
        Assert.Equal(CameraModule.MinPitch, camera.Pitch);

        // Back from the limit at once, no wind-up.
        Look(bus, 0f, 0.1f);
        Assert.Equal(-35f * Degree + 0.1f, camera.Pitch, 4);
    }

    [Fact]
    public void LookingDownRaisesTheCameraAboveThePlayer()
    {
        var (camera, bus) = Started();
        Look(bus, 0f, -1f); // to the -35 degree limit
        Run(camera, bus, ForestBounds.PlayerStart, 5);
        var offset = bus.Poses[^1].Position - ShoulderOf(camera);
        Assert.Equal(CameraModule.Distance * MathF.Sin(35f * Degree), offset.Y, 2);
    }

    [Fact]
    public void IgnoresNonFiniteInput()
    {
        var (camera, bus) = Started();
        Look(bus, float.NaN, float.PositiveInfinity);
        Run(camera, bus, ForestBounds.PlayerStart, 2);
        Assert.Equal(0f, camera.Yaw);
        Assert.Equal(CameraModule.StartPitch, camera.Pitch);
        Assert.True(float.IsFinite(bus.Poses[^1].Position.X));
    }

    // ---- following the player ----

    [Fact]
    public void FollowsAStepSmoothlyAndCatchesUp()
    {
        var (camera, bus) = Started();
        var start = ForestBounds.PlayerStart;
        Run(camera, bus, start, 10);
        var moved = OnGround(start.X + 1f, start.Z);
        var target = moved + new Vector3(0f, CameraModule.PivotHeight, 0f);
        float before = Vector3.Distance(camera.Head, target);

        Run(camera, bus, moved, 1);
        float afterOne = Vector3.Distance(camera.Head, target);
        Assert.True(afterOne < before, "moves toward the player");
        Assert.True(afterOne > 0.6f * before, "but does not jump there in one tick");

        Run(camera, bus, moved, 60);
        Assert.True(Vector3.Distance(camera.Head, target) < 0.01f, "caught up within a second");
    }

    [Fact]
    public void FollowsAWalkingPlayerWithoutJumps()
    {
        var (camera, bus) = Started();
        var start = ForestBounds.PlayerStart;
        Run(camera, bus, start, 10);
        const float speed = 5f;
        Vector3 last = bus.Poses[^1].Position;
        for (int i = 1; i <= 120; i++)
        {
            // Walk north along the clear middle of the start clearing.
            var feet = OnGround(start.X, start.Z - speed * (float)Step * i);
            Run(camera, bus, feet, 1);
            Vector3 now = bus.Poses[^1].Position;
            Assert.True(Vector3.Distance(last, now) < speed * (float)Step * 1.5f, $"tick {i}: camera jumped");
            Assert.True(Vector3.Distance(camera.Head, feet + new Vector3(0f, CameraModule.PivotHeight, 0f)) < 0.6f,
                $"tick {i}: camera fell behind");
            last = now;
        }
    }

    [Fact]
    public void SnapsToAPlayerWhoComesBackFarAway()
    {
        var (camera, bus) = Started();
        Run(camera, bus, OnGround(0f, -5f), 30);
        Run(camera, bus, ForestBounds.PlayerStart, 1);
        Assert.Equal(0f, Vector3.Distance(camera.Head, ForestBounds.PlayerStart + new Vector3(0f, CameraModule.PivotHeight, 0f)), 4);
    }

    // ---- pulling in ----

    [Fact]
    public void PullsInAboveTheGroundWhenLookingUp()
    {
        var (camera, bus) = Started();
        var feet = ForestBounds.PlayerStart;
        Look(bus, 0f, 10f); // +60 degrees: the full orbit would be under the ground
        Run(camera, bus, feet, 10);
        var pose = bus.Poses[^1];

        Assert.True(camera.CurrentDistance < CameraModule.Distance - 1f, $"distance {camera.CurrentDistance}");
        Assert.True(camera.CurrentDistance > 0.5f, "still behind the player");
        float ground = Terrain.HeightAt(pose.Position.X, pose.Position.Z);
        Assert.True(pose.Position.Y >= ground + CameraModule.Clearance - 0.01f, $"camera at {pose.Position.Y}, ground {ground}");
        // Every point between the shoulder and the camera is above the ground.
        Vector3 shoulder = ShoulderOf(camera);
        for (int i = 0; i <= 50; i++)
        {
            Vector3 p = Vector3.Lerp(shoulder, pose.Position, i / 50f);
            Assert.True(p.Y >= Terrain.HeightAt(p.X, p.Z), $"sight line under the ground at {i}");
        }
    }

    [Fact]
    public void PullsInInFrontOfAGiantMushroomTrunkAndEasesBackOut()
    {
        var trunk = FindTrunk();
        float gap = 1.2f;
        // The player stands just outside the trunk, facing away from it, so the camera's orbit
        // behind them would go into the trunk.
        Vector2 away = Vector2.Normalize(new Vector2(1f, 0.3f));
        var feet = OnGround(trunk.X + away.X * (trunk.Radius + gap), trunk.Z + away.Y * (trunk.Radius + gap));
        float yaw = MathF.Atan2(-away.X, -away.Y);

        var (camera, bus) = Started();
        Look(bus, -yaw, 0f);
        Look(bus, 0f, -CameraModule.StartPitch); // level
        Run(camera, bus, feet, 30);
        var pose = bus.Poses[^1];

        Assert.True(camera.CurrentDistance < gap + CameraModule.ShoulderOffset + 0.2f, $"distance {camera.CurrentDistance}");
        Assert.True(camera.CurrentDistance > 0.2f);
        float dx = pose.Position.X - trunk.X, dz = pose.Position.Z - trunk.Z;
        Assert.True(MathF.Sqrt(dx * dx + dz * dz) >= trunk.Radius + CameraModule.Clearance - 0.01f, "camera outside the trunk");
        Vector3 shoulder = ShoulderOf(camera);
        for (int i = 0; i <= 50; i++)
        {
            Vector3 p = Vector3.Lerp(shoulder, pose.Position, i / 50f);
            float px = p.X - trunk.X, pz = p.Z - trunk.Z;
            Assert.True(px * px + pz * pz >= trunk.Radius * trunk.Radius, $"sight line through the trunk at {i}");
        }

        // Turning around clears the view: the camera eases back out, not in one jump.
        Look(bus, MathF.PI, 0f);
        Run(camera, bus, feet, 1);
        float pulled = camera.CurrentDistance;
        Assert.True(pulled < CameraModule.Distance - 1f, $"eases out, not snaps ({pulled})");
        Run(camera, bus, feet, 120);
        Assert.Equal(CameraModule.Distance, camera.CurrentDistance, 1);
    }

    /// <summary>A trunk with open ground (no other trunk) around it, well inside the forest.</summary>
    private static Obstacle FindTrunk()
    {
        foreach (var o in Obstacles.All)
        {
            if (!ForestBounds.Contains(o.X, o.Z, 20f))
                continue;
            bool lonely = true;
            foreach (var other in Obstacles.All)
            {
                if (other.X == o.X && other.Z == o.Z)
                    continue;
                float dx = other.X - o.X, dz = other.Z - o.Z, keep = o.Radius + other.Radius + 7f;
                lonely &= dx * dx + dz * dz > keep * keep;
            }
            if (lonely)
                return o;
        }
        throw new InvalidOperationException("no lonely trunk in the forest");
    }

    [Fact]
    public void FreeLengthStopsAtTheGroundAndTrunks()
    {
        var head = ForestBounds.PlayerStart + new Vector3(0f, CameraModule.PivotHeight, 0f);
        Assert.Equal(5f, CameraModule.FreeLength(head, Vector3.UnitY, 5f), 4);
        float down = CameraModule.FreeLength(head, -Vector3.UnitY, 5f);
        Assert.Equal(CameraModule.PivotHeight - CameraModule.Clearance, down, 1);

        var trunk = FindTrunk();
        var from = new Vector3(trunk.X + trunk.Radius + 3f, 50f, trunk.Z);
        float toTrunk = CameraModule.FreeLength(from, -Vector3.UnitX, 10f);
        Assert.Equal(3f - CameraModule.Clearance, toTrunk, 2);
    }

    // ---- the front view (wardrobe, shop, start screen) ----

    private const float Facing = 0.8f;
    private static readonly int TransitionTicks = (int)MathF.Ceiling(CameraModule.TransitionSeconds * 60f);

    /// <summary>Where <paramref name="point"/> shows on a 1920 x 1080 screen at 70 degrees: x -1 left edge .. 1 right edge, y -1 bottom .. 1 top.</summary>
    private static Vector2 OnScreenNormalized(CameraPose pose, Vector3 point)
    {
        var s = OnScreen(pose, point);
        Assert.True(s.Z > 0.5f, $"{point} is not in front of the camera");
        float tanHalf = MathF.Tan(35f * Degree);
        return new Vector2(s.X / (tanHalf * 16f / 9f), s.Y / tanHalf);
    }

    /// <summary>
    /// The whole character (feet to the top of the big head, <see cref="BowGeometry.HeadUp"/> +
    /// its radius, 1.30 m, and a tall hat, 0.2 m more; 0.45 m to each side across the view) shows
    /// in the left half of the screen, top to bottom, the chest near the middle of that half.
    /// </summary>
    private static void AssertFramedInTheLeftHalf(CameraPose pose, Vector3 feet)
    {
        var chest = OnScreenNormalized(pose, feet + new Vector3(0f, CameraModule.ChestHeight, 0f));
        Assert.InRange(chest.X, -0.65f, -0.35f);
        Vector3 across = Vector3.Transform(Vector3.UnitX, pose.Orientation);
        across = Vector3.Normalize(new Vector3(across.X, 0f, across.Z)) * 0.45f;
        float hatTop = BowGeometry.HeadUp + BowGeometry.HeadRadius + 0.2f;
        foreach (float h in new[] { 0f, CameraModule.ChestHeight, hatTop })
        {
            foreach (float side in new[] { -1f, 1f })
            {
                var p = OnScreenNormalized(pose, feet + new Vector3(0f, h, 0f) + across * side);
                Assert.True(p.X > -0.98f && p.X < -0.02f, $"height {h}, side {side}: x {p.X} not in the left half");
                Assert.True(p.Y > -0.98f && p.Y < 0.98f, $"height {h}, side {side}: y {p.Y} off the screen");
            }
        }
    }

    /// <summary>The camera keeps <see cref="CameraModule.Clearance"/> inside the home room's walls, floor and ceiling.</summary>
    private static void AssertInsideTheRoom(Vector3 p, string what)
    {
        var room = Areas.Home;
        float c = CameraModule.Clearance - 0.01f;
        Assert.True(room.Contains(p.X, p.Z, c), $"{what}: camera at {p} outside the walls");
        Assert.InRange(p.Y, Areas.HomeFloorY + c, Areas.HomeFloorY + CameraModule.RoomHeight - c);
    }

    private static Vector3 HomeFeet(float x, float z) => new(x, Areas.HomeFloorY, z);

    [Fact]
    public void TheFrontViewLooksAtTheChestOfTheShorterBodyBelowTheLoweredHead()
    {
        // The body is 0.36..0.88 m, the head's center BowGeometry.HeadUp (1.11 m): the chest, below the middle of the character.
        Assert.InRange(CameraModule.ChestHeight, 0.36f + 0.52f * 0.25f, 0.36f + 0.52f * 0.6f);
        Assert.InRange(CameraModule.ChestHeight, BowGeometry.HeadUp * 0.45f, BowGeometry.HeadUp * 0.55f);
    }

    [Fact]
    public void FramesThePlayerFullBodyFromTheFrontInTheLeftHalfWhileTheWardrobeIsOpen()
    {
        var (camera, bus) = Started();
        var feet = ForestBounds.PlayerStart;
        RunScreens(camera, bus, feet, Facing, false, false, 30);
        RunScreens(camera, bus, feet, Facing, true, false, TransitionTicks + 1);
        Assert.Equal(1f, camera.Framing);

        var pose = bus.Poses[^1];
        var chest = feet + new Vector3(0f, CameraModule.ChestHeight, 0f);
        Vector3 facingDir = CameraModule.Forward(Facing);
        Vector3 offset = pose.Position - chest;
        // Straight in front of the player, 2.5..3 m from the chest, at chest height.
        Assert.InRange(Vector3.Dot(offset, facingDir), 2.5f, 3f);
        Assert.InRange(offset.Length(), 2.5f, 3f);
        Assert.Equal(0f, Vector3.Dot(offset, CameraModule.Right(Facing)), 3);
        Assert.Equal(0f, offset.Y, 3);
        // Level and upright, turned so the player stands in the left half (the panel is on the right).
        Vector3 look = Vector3.Transform(-Vector3.UnitZ, pose.Orientation);
        Assert.Equal(0f, look.Y, 4);
        Assert.True(Vector3.Transform(Vector3.UnitY, pose.Orientation).Y > 0.99f);
        AssertFramedInTheLeftHalf(pose, feet);
        // The published pose's yaw is the view's.
        Assert.Equal(CameraModule.ChooseFrontYaw(feet, Facing), camera.FrontYaw, 5);
        Assert.Equal(MathF.Atan2(-look.X, -look.Z), pose.Yaw, 4);
        Assert.True(Vector3.Distance(CameraModule.FrontView(feet, camera.FrontYaw).Position, pose.Position) < 1e-3f);
        Assert.Equal(CameraModule.FrontView(feet, camera.FrontYaw).Yaw, pose.Yaw, 4);
    }

    [Fact]
    public void TheStartScreenAlsoFramesTheFrontView()
    {
        var (camera, bus) = Started();
        var feet = ForestBounds.PlayerStart;
        RunScreens(camera, bus, feet, 0f, false, true, TransitionTicks + 1);
        Assert.True(camera.FrontViewWanted);
        Assert.Equal(1f, camera.Framing);
        var pose = bus.Poses[^1];
        Assert.True(Vector3.Distance(CameraModule.FrontView(feet, MathF.PI).Position, pose.Position) < 1e-3f);
        // Facing north (-Z): the camera is north of the player, looking south.
        Assert.True(pose.Position.Z < feet.Z - 2.4f);
        Assert.True(Vector3.Transform(-Vector3.UnitZ, pose.Orientation).Z > 0.8f);
        AssertFramedInTheLeftHalf(pose, feet);
    }

    [Fact]
    public void StaysStillWhileOpenWhenThePlayerTurnsTheCharacter()
    {
        var (camera, bus) = Started();
        var feet = ForestBounds.PlayerStart;
        RunScreens(camera, bus, feet, Facing, true, false, TransitionTicks + 1);
        var framed = bus.Poses[^1];
        // No slow orbit: the camera holds still, even while the character is turned round (PreviewYaw).
        for (int i = 0; i < 20 * 60; i++)
        {
            RunScreens(camera, bus, feet, Facing + MathF.Sin(i * 0.01f) * 2f, true, false, 1);
            var pose = bus.Poses[^1];
            Assert.True(Vector3.Distance(framed.Position, pose.Position) < 1e-4f, $"tick {i}: moved");
            Assert.True(MathF.Abs(Quaternion.Dot(framed.Orientation, pose.Orientation)) > 0.999999f, $"tick {i}: turned");
        }
    }

    [Fact]
    public void ATeleportWhileOpenFramesThePlayerWhereTheyLand()
    {
        var (camera, bus) = Started();
        RunScreens(camera, bus, OnGround(0f, 70f), 1f, false, true, TransitionTicks + 1);
        var feet = ForestBounds.PlayerStart;
        RunScreens(camera, bus, feet, 0f, false, true, 2);
        Assert.Equal(MathF.PI, MathF.Abs(camera.FrontYaw), 4);
        Assert.True(Vector3.Distance(CameraModule.FrontView(feet, MathF.PI).Position, bus.Poses[^1].Position) < 1e-3f);
    }

    [Fact]
    public void SwingsSmoothlyRoundToTheFrontWithoutPassingThroughThePlayer()
    {
        var (camera, bus) = Started();
        var feet = ForestBounds.PlayerStart;
        RunScreens(camera, bus, feet, Facing, false, false, 30);
        var chest = feet + new Vector3(0f, CameraModule.ChestHeight, 0f);
        var front = CameraModule.FrontView(feet, CameraModule.ChooseFrontYaw(feet, Facing)).Position;
        Vector3 last = bus.Poses[^1].Position;
        Quaternion lastTurn = bus.Poses[^1].Orientation;
        float firstStep = -1f;
        for (int i = 1; i <= TransitionTicks; i++)
        {
            RunScreens(camera, bus, feet, Facing, true, false, 1);
            var pose = bus.Poses[^1];
            if (i == 1)
                firstStep = Vector3.Distance(last, pose.Position);
            Assert.True(Vector3.Distance(last, pose.Position) < 0.4f, $"tick {i}: jumped {Vector3.Distance(last, pose.Position)} m");
            Assert.True(MathF.Abs(Quaternion.Dot(lastTurn, pose.Orientation)) > 0.998f, $"tick {i}: turned too fast");
            Assert.True(Vector3.Distance(pose.Position, chest) > 2f, $"tick {i}: too close to the player");
            if (i == TransitionTicks / 2)
                Assert.True(Vector3.Distance(pose.Position, front) > 1f, "still on the way halfway through");
            last = pose.Position;
            lastTurn = pose.Orientation;
        }
        // Eased in and out: slow at the start and the end, fast in the middle.
        Assert.True(firstStep < 0.05f, $"first step {firstStep}");
        Assert.True(Vector3.Distance(bus.Poses[^1].Position, bus.Poses[^2].Position) < 0.05f);
        Assert.Equal(1f, camera.Framing);
    }

    // ---- the front view at home (the wardrobe) ----

    [Fact]
    public void AtHomeWithRoomInFrontFramesStraightFromTheFront()
    {
        // Where you come in, facing north into the room: 6 m to the north wall.
        var feet = HomeFeet(600f, 601f);
        var (camera, bus) = Started();
        RunScreens(camera, bus, feet, 0f, false, false, 10);
        RunScreens(camera, bus, feet, 0f, true, false, TransitionTicks + 1);
        var pose = bus.Poses[^1];
        Assert.True(Vector3.Distance(CameraModule.FrontView(feet, MathF.PI).Position, pose.Position) < 1e-3f);
        AssertInsideTheRoom(pose.Position, "open");
        AssertFramedInTheLeftHalf(pose, feet);
    }

    [Fact]
    public void AtHomeALittleShortOfRoomItPullsInAndRisesSlightly()
    {
        // Facing north, 2.6 m from the north wall: 2.3 m of room in front, less than the 2.75 m.
        var feet = HomeFeet(600f, 597.6f);
        var chest = feet + new Vector3(0f, CameraModule.ChestHeight, 0f);
        var (camera, bus) = Started();
        RunScreens(camera, bus, feet, 0f, true, false, TransitionTicks + 1);
        var pose = bus.Poses[^1];
        AssertInsideTheRoom(pose.Position, "open");
        Vector3 offset = pose.Position - chest;
        Assert.Equal(0f, offset.X, 3);                                  // still straight in front
        Assert.InRange(-offset.Z, 2.2f, 2.31f);                         // pulled in to the wall's clearance
        Assert.InRange(offset.Y, 0.1f, 0.4f);                           // raised a little
        Assert.True(Vector3.Transform(-Vector3.UnitZ, pose.Orientation).Y < 0f, "looks a little down");
        AssertFramedInTheLeftHalf(pose, feet);
    }

    [Theory]
    [InlineData(604.5f, 596.6f, 0f)]                 // at the wardrobe in the north-east corner, facing it
    [InlineData(605f, 600f, -MathF.PI / 2f)]         // close to the east wall, facing it
    [InlineData(595f, 595.8f, MathF.PI / 4f)]        // in the north-west corner, facing into it
    [InlineData(600f, 604.2f, MathF.PI)]             // by the door, facing it
    public void AtHomeTheCameraStaysInsideTheWallsAndStillFramesThePlayer(float x, float z, float facing)
    {
        var feet = HomeFeet(x, z);
        var (camera, bus) = Started();
        RunScreens(camera, bus, feet, facing, false, false, 10);
        for (int i = 1; i <= TransitionTicks + 1; i++)
        {
            RunScreens(camera, bus, feet, facing, true, false, 1);
            AssertInsideTheRoom(bus.Poses[^1].Position, $"opening, tick {i}");
        }
        var pose = bus.Poses[^1];
        AssertFramedInTheLeftHalf(pose, feet);
        var chest = feet + new Vector3(0f, CameraModule.ChestHeight, 0f);
        float distance = Vector3.Distance(pose.Position, chest);
        Assert.InRange(distance, 2.2f, 3f);
        // No room in front: it goes round to the nearest side with room rather than clip through the wall.
        Assert.True(CameraModule.RoomFreeLength(chest, Vector3.Normalize(pose.Position - chest)) >= distance - 1e-3f);
        for (int i = 1; i <= TransitionTicks + 1; i++)
        {
            RunScreens(camera, bus, feet, facing, false, false, 1);
            AssertInsideTheRoom(bus.Poses[^1].Position, $"closing, tick {i}");
        }
    }

    [Fact]
    public void RoomFreeLengthStopsAtTheWallsFloorAndCeiling()
    {
        var p = new Vector3(600f, Areas.HomeFloorY + 1f, 600f);
        float c = CameraModule.Clearance;
        Assert.Equal(Areas.Home.MaxX - 600f - c, CameraModule.RoomFreeLength(p, Vector3.UnitX), 3);
        Assert.Equal(600f - Areas.Home.MinZ - c, CameraModule.RoomFreeLength(p, -Vector3.UnitZ), 3);
        Assert.Equal(1f - c, CameraModule.RoomFreeLength(p, -Vector3.UnitY), 3);
        Assert.Equal(CameraModule.RoomHeight - 1f - c, CameraModule.RoomFreeLength(p, Vector3.UnitY), 3);
        Assert.Equal(0f, CameraModule.RoomFreeLength(new Vector3(610f, Areas.HomeFloorY + 1f, 600f), -Vector3.UnitX));
    }

    [Fact]
    public void ReturnsSmoothlyToTheOrbitBehindWhenClosed()
    {
        var (camera, bus) = Started();
        var feet = ForestBounds.PlayerStart;
        Look(bus, 0.4f, -0.1f);
        RunScreens(camera, bus, feet, Facing, false, false, 30);
        var before = bus.Poses[^1];
        float yaw = camera.Yaw, pitch = camera.Pitch;

        RunScreens(camera, bus, feet, Facing, true, false, 200);
        // The screens own the input: looking around while open does not move the orbit.
        Look(bus, 1f, 0.5f);
        Assert.Equal(yaw, camera.Yaw);
        Assert.Equal(pitch, camera.Pitch);

        Vector3 last = bus.Poses[^1].Position;
        var chest = feet + new Vector3(0f, CameraModule.ChestHeight, 0f);
        for (int i = 1; i <= TransitionTicks; i++)
        {
            RunScreens(camera, bus, feet, Facing, false, false, 1);
            var pose = bus.Poses[^1];
            Assert.True(Vector3.Distance(last, pose.Position) < 0.4f, $"tick {i}: jumped");
            Assert.True(Vector3.Distance(pose.Position, chest) > 2f, $"tick {i}: too close to the player");
            if (i == TransitionTicks / 2)
                Assert.True(Vector3.Distance(pose.Position, before.Position) > 1f, "still on the way back halfway through");
            last = pose.Position;
        }
        Assert.Equal(0f, camera.Framing);
        var after = bus.Poses[^1];
        Assert.True(Vector3.Distance(before.Position, after.Position) < 1e-3f, "back where it was");
        Assert.True(MathF.Abs(Quaternion.Dot(before.Orientation, after.Orientation)) > 0.99999f);
        Assert.Equal(before.Yaw, after.Yaw, 5);

        // Looking around works again.
        Look(bus, 0.2f, 0f);
        Assert.Equal(yaw - 0.2f, camera.Yaw, 5);
    }

    [Fact]
    public void ReopeningOnTheWayBackTurnsAroundWithoutAJump()
    {
        var (camera, bus) = Started();
        var feet = ForestBounds.PlayerStart;
        RunScreens(camera, bus, feet, Facing, false, true, 120);
        RunScreens(camera, bus, feet, Facing, false, false, TransitionTicks / 3);
        float framing = camera.Framing;
        Assert.InRange(framing, 0.2f, 0.95f);
        Vector3 last = bus.Poses[^1].Position;
        RunScreens(camera, bus, feet, Facing, true, false, 1);
        Assert.True(camera.Framing > framing, "heads back to the front");
        Assert.True(Vector3.Distance(last, bus.Poses[^1].Position) < 0.4f);
    }

    // ---- the aim camera (drawing the bow) ----

    private static readonly int AimTicks = (int)MathF.Ceiling(CameraModule.AimTransitionSeconds * 60f);
    private static readonly int HoldTicks = (int)MathF.Ceiling(CameraModule.AimHoldSeconds * 60f);

    /// <summary>The round head seen as a ball: 0.38 m wide, plus its ink outline and a little room.</summary>
    private const float HeadOutlineRadius = 0.22f;

    /// <summary>Ticks with the player standing at <paramref name="feet"/> facing the camera's way (as while drawing) and the bow drawn <paramref name="draw"/>.</summary>
    private static void RunBow(CameraModule camera, FakeBus bus, Vector3 feet, float draw, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            bus.Publish(new PlayerState(feet, camera.Yaw, Vector3.Zero, true, 10, 10, true, CharacterKind.Girl));
            bus.Publish(new BowState(draw, 0));
            camera.Tick(new TickContext(camera.Ticks, Step));
        }
    }

    private static void PitchTo(FakeBus bus, CameraModule camera, float pitch) => Look(bus, 0f, pitch - camera.Pitch);

    private static float AngleBetween(Vector3 a, Vector3 b) =>
        MathF.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(a), Vector3.Normalize(b)), -1f, 1f));

    [Fact]
    public void TheAimOffsetIsRightOfAndAboveTheHeadOnTheHoldPointsSide()
    {
        var feet = ForestBounds.PlayerStart;
        var head = feet + new Vector3(0f, BowGeometry.HeadUp, 0f);
        // The bow is held on the right, so the camera looks over the right shoulder.
        Assert.True(BowGeometry.HoldRight > 0f);
        Assert.Equal(1f, CameraModule.AimSideSign);
        foreach (float yaw in new[] { 0f, 1.1f, -2.4f })
        {
            Vector3 pivot = CameraModule.AimPivot(feet + new Vector3(0f, CameraModule.PivotHeight, 0f), yaw);
            Vector3 offset = pivot - head;
            Assert.Equal(0.9f, Vector3.Dot(offset, CameraModule.Right(yaw)), 2);    // 0.9 m right
            // On the hold point's side of the head.
            Vector3 hold = BowGeometry.HoldPoint(feet, yaw) - head;
            Assert.True(Vector3.Dot(offset, CameraModule.Right(yaw)) * Vector3.Dot(hold, CameraModule.Right(yaw)) > 0f);
            Assert.Equal(0.25f, offset.Y, 2);                                         // 0.25 m higher
            Assert.Equal(0f, Vector3.Dot(offset, CameraModule.Forward(yaw)), 3);      // over the head, not ahead
        }

        // Drawing in the open clearing, level: the camera is there, 2.5-3 m back.
        var (camera, bus) = Started();
        PitchTo(bus, camera, 0f);
        RunBow(camera, bus, feet, 0f, 30);
        RunBow(camera, bus, feet, 0.5f, AimTicks + 30);
        var pose = bus.Poses[^1];
        Vector3 fromHead = pose.Position - head;
        Assert.Equal(1f, camera.Aiming, 4);
        Assert.InRange(Vector3.Dot(fromHead, CameraModule.Right(camera.Yaw)), 0.85f, 0.95f);
        Assert.InRange(fromHead.Y, 0.2f, 0.3f);
        Assert.InRange(-Vector3.Dot(fromHead, CameraModule.Forward(camera.Yaw)), 2.5f, 3f);
        Assert.True(Vector3.Distance(CameraModule.AimView(feet, camera.Yaw, 0f).Position, pose.Position) < 0.02f);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(20f, 0f)]
    [InlineData(-20f, 0f)]
    [InlineData(20f, 2f)]
    [InlineData(-20f, -1.3f)]
    public void TheHeadDoesNotHideTheBowHand(float pitchDegrees, float yaw)
    {
        var feet = ForestBounds.PlayerStart;
        var (camera, bus) = Started();
        Look(bus, -yaw, 0f);
        PitchTo(bus, camera, pitchDegrees * Degree);
        RunBow(camera, bus, feet, 0f, 30);
        RunBow(camera, bus, feet, 1f, AimTicks + 30);
        Assert.Equal(pitchDegrees * Degree, camera.Pitch, 4);

        foreach (var pose in new[] { bus.Poses[^1], CameraModule.AimView(feet, camera.Yaw, camera.Pitch) })
        {
            Vector3 head = feet + new Vector3(0f, BowGeometry.HeadUp, 0f);
            Vector3 hold = BowGeometry.HoldPoint(feet, camera.Yaw); // the character faces the aim
            Vector3 toHead = head - pose.Position, toHold = hold - pose.Position;
            float outline = MathF.Asin(HeadOutlineRadius / toHead.Length());
            float apart = AngleBetween(toHead, toHold);
            Assert.True(apart > outline + 1f * Degree,
                $"pitch {pitchDegrees}: the hold point is {apart / Degree:F1} degrees from the head's center, its outline {outline / Degree:F1}");
            // And it is on the screen, beside the head on its right (the camera's side), beyond it.
            Vector3 s = OnScreen(pose, hold);
            Assert.True(s.Z > toHead.Length(), "the bow hand is beyond the head");
            Assert.True(s.X > OnScreen(pose, head).X, "the bow hand shows right of the head");
            Assert.True(MathF.Abs(s.X) < MathF.Tan(CameraModule.AimFieldOfView / 2f) * CameraModule.FrameAspect, "the bow hand is on the screen");
            Assert.True(MathF.Abs(s.Y) < MathF.Tan(CameraModule.AimFieldOfView / 2f), "the bow hand is on the screen");
        }
    }

    [Fact]
    public void TheAimCameraLooksTheSameWayAsTheNormalCamera()
    {
        var feet = ForestBounds.PlayerStart;
        var (camera, bus) = Started();
        Look(bus, 0.7f, 0.2f);
        RunBow(camera, bus, feet, 0f, 30);
        var normal = bus.Poses[^1];
        for (int i = 0; i < AimTicks + HoldTicks + 40; i++)
        {
            RunBow(camera, bus, feet, i < 50 ? MathF.Min(1f, i / 36f) : 0f, 1);
            var pose = bus.Poses[^1];
            Assert.True(Quaternion.Dot(normal.Orientation, pose.Orientation) > 1f - 1e-6f, $"tick {i}: the view turned");
            Assert.Equal(normal.Yaw, pose.Yaw, 5);
        }
        Vector3 look = Vector3.Transform(-Vector3.UnitZ, bus.Poses[^1].Orientation);
        Assert.True(Vector3.Distance(CameraModule.ViewDirection(camera.Yaw, camera.Pitch), look) < 1e-4f);
    }

    [Fact]
    public void EasesSmoothlyToTheAimCameraAndBackAfterTheShot()
    {
        var feet = ForestBounds.PlayerStart;
        var (camera, bus) = Started();
        PitchTo(bus, camera, 0f);
        RunBow(camera, bus, feet, 0f, 60);
        var normal = bus.Poses[^1];
        Assert.Equal(CameraModule.FieldOfView, normal.FieldOfView, 5);
        var aim = CameraModule.AimView(feet, camera.Yaw, 0f);

        // Draw for a full second (full after 0.6 s), shoot, then wait.
        var trace = new List<CameraPose>();
        int drawTicks = 60;
        for (int i = 0; i < drawTicks + HoldTicks + AimTicks + 30; i++)
        {
            RunBow(camera, bus, feet, i < drawTicks ? MathF.Min(1f, (i + 1) / 36f) : 0f, 1);
            trace.Add(bus.Poses[^1]);
        }
        var previous = normal;
        float biggest = 0f;
        foreach (var pose in trace)
        {
            float moved = Vector3.Distance(previous.Position, pose.Position);
            biggest = MathF.Max(biggest, moved);
            Assert.True(MathF.Abs(pose.FieldOfView - previous.FieldOfView) < 1f * Degree, "the zoom does not jump");
            previous = pose;
        }
        // About 2.6 m in 0.25 s, eased: no step much over its average.
        float total = Vector3.Distance(normal.Position, aim.Position);
        Assert.True(biggest < 2.5f * total / AimTicks, $"a step of {biggest} m");

        // In the aim camera within the ease, zoomed at the full draw.
        Assert.True(Vector3.Distance(trace[AimTicks + 1].Position, aim.Position) < 0.02f);
        var full = trace[drawTicks - 1];
        Assert.True(Vector3.Distance(full.Position, aim.Position) < 0.02f);
        Assert.Equal(CameraModule.AimFieldOfView, full.FieldOfView, 3);
        Assert.InRange(CameraModule.AimFieldOfView, 55f * Degree, 66f * Degree); // slightly lower
        // Halfway through the ease it is halfway there.
        float half = Vector3.Distance(trace[AimTicks / 2].Position, normal.Position) / total;
        Assert.InRange(half, 0.3f, 0.7f);

        // Still there for a moment after the shot, then back to the normal camera.
        Assert.True(Vector3.Distance(trace[drawTicks + HoldTicks - 3].Position, aim.Position) < 0.02f, "holds after the shot");
        var back = trace[^1];
        Assert.True(Vector3.Distance(back.Position, normal.Position) < 0.02f, $"back at {back.Position}, normal {normal.Position}");
        Assert.Equal(CameraModule.FieldOfView, back.FieldOfView, 5);
        Assert.Equal(0f, camera.Aiming);
        Assert.Equal(CameraModule.Distance, camera.CurrentDistance, 2);
    }

    [Fact]
    public void TheAimCameraPullsInInFrontOfATrunk()
    {
        var trunk = FindTrunk();
        float gap = 0.8f;
        Vector2 away = Vector2.Normalize(new Vector2(1f, 0.3f));
        var feet = OnGround(trunk.X + away.X * (trunk.Radius + gap), trunk.Z + away.Y * (trunk.Radius + gap));
        float yaw = MathF.Atan2(-away.X, -away.Y);

        var (camera, bus) = Started();
        Look(bus, -yaw, 0f);
        PitchTo(bus, camera, 0f);
        RunBow(camera, bus, feet, 1f, AimTicks + 30);
        var pose = bus.Poses[^1];

        Assert.Equal(1f, camera.Aiming, 4);
        Assert.True(camera.CurrentDistance < gap + 0.3f, $"distance {camera.CurrentDistance}");
        float dx = pose.Position.X - trunk.X, dz = pose.Position.Z - trunk.Z;
        Assert.True(MathF.Sqrt(dx * dx + dz * dz) >= trunk.Radius + CameraModule.Clearance - 0.01f, "camera outside the trunk");
    }

    [Fact]
    public void TheAimCameraKeepsAboveTheGroundLookingUp()
    {
        var (camera, bus) = Started();
        var feet = ForestBounds.PlayerStart;
        Look(bus, 0f, 10f); // +60 degrees
        RunBow(camera, bus, feet, 1f, AimTicks + 30);
        var pose = bus.Poses[^1];
        Assert.True(camera.CurrentDistance < CameraModule.AimDistance - 0.5f, $"distance {camera.CurrentDistance}");
        Assert.True(pose.Position.Y >= Terrain.HeightAt(pose.Position.X, pose.Position.Z) + CameraModule.Clearance - 0.01f);
    }

    [Fact]
    public void TheFrontViewIsNotTurnedIntoTheAimCamera()
    {
        var feet = OnGround(0f, 74f);
        var (camera, bus) = Started();
        for (int i = 0; i < TransitionTicks + 30; i++)
        {
            bus.Publish(new BowState(1f, 0));
            RunScreens(camera, bus, feet, Facing, true, false, 1);
        }
        Assert.False(camera.AimWanted);
        Assert.Equal(0f, camera.Aiming);
        var pose = bus.Poses[^1];
        var front = CameraModule.FrontView(feet, camera.FrontYaw);
        Assert.True(Vector3.Distance(front.Position, pose.Position) < 0.05f);
        Assert.Equal(CameraModule.FieldOfView, pose.FieldOfView, 5);
        AssertFramedInTheLeftHalf(pose, feet);
    }

    // ---- determinism ----

    [Fact]
    public void IsDeterministic()
    {
        CameraPose RunOnce()
        {
            var (camera, bus) = Started();
            for (int i = 0; i < 90; i++)
            {
                Look(bus, 0.02f, MathF.Sin(i * 0.1f) * 0.05f);
                Run(camera, bus, OnGround(MathF.Sin(i * 0.05f) * 3f, 84f - i * 0.08f), 1);
            }
            for (int i = 0; i < 150; i++)
                RunScreens(camera, bus, OnGround(0f, 76f), Facing, false, i < 100, 1);
            return bus.Poses[^1];
        }
        var a = RunOnce();
        var b = RunOnce();
        Assert.Equal(a.Position, b.Position);
        Assert.Equal(a.Orientation, b.Orientation);
    }
}
