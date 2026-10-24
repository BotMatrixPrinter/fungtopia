using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Forest;
using Fungtopia.Input;
using Fungtopia.Player;
using Fungtopia.Shell;
using Xunit;

namespace Fungtopia.Camera.Tests;

public class CameraModuleTests
{
    private const double Step = 1.0 / 60;
    private const float Degree = MathF.PI / 180f;

    /// <summary>Fake of the shell's bus: delivers player and input messages and records every pose published.</summary>
    private sealed class FakeBus : IMessageBus
    {
        private IMessageHandler<PlayerState>? _state;
        private IMessageHandler<PlayerInput>? _input;
        public readonly List<CameraPose> Poses = new(1024);
        public int OtherMessages;

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct
        {
            if (handler is IMessageHandler<PlayerState> state && typeof(T) == typeof(PlayerState))
                _state = state;
            if (handler is IMessageHandler<PlayerInput> input && typeof(T) == typeof(PlayerInput))
                _input = input;
        }

        public void Publish<T>(in T message) where T : struct
        {
            if (message is CameraPose pose)
                Poses.Add(pose);
            else if (message is PlayerState state)
                _state?.Handle(in state);
            else if (message is PlayerInput input)
                _input?.Handle(in input);
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

    // ---- performance ----

    [Fact]
    public void TicksWithoutAllocating()
    {
        var (camera, bus) = Started();
        Run(camera, bus, ForestBounds.PlayerStart, 5);
        bus.Poses.Clear();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 200; i++)
        {
            Look(bus, 0.01f, 0.005f);
            Run(camera, bus, OnGround(0f, 84f - i * 0.05f), 1);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

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
            return bus.Poses[^1];
        }
        var a = RunOnce();
        var b = RunOnce();
        Assert.Equal(a.Position, b.Position);
        Assert.Equal(a.Orientation, b.Orientation);
    }
}
