using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Forest;
using Fungtopia.Input;
using Fungtopia.Monsters;
using Fungtopia.Player;
using Fungtopia.Shell;
using MapGround = Fungtopia.Map.Ground;
using Xunit;

namespace Fungtopia.Bow.Tests;

public class BowModuleTests
{
    private const double Step = 1.0 / 60;
    private const float Dt = 1f / 60;

    /// <summary>Fake of the shell's bus: delivers to subscribers and records what the bow publishes.</summary>
    private sealed class FakeBus : IMessageBus
    {
        private readonly Dictionary<Type, List<object>> _handlers = new();
        public readonly List<BowState> States = new();
        public readonly List<HitMonster> Hits = new();

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
                _handlers[typeof(T)] = list = new List<object>();
            list.Add(handler);
        }

        public void Publish<T>(in T message) where T : struct
        {
            if (message is BowState state)
                States.Add(state);
            else if (message is HitMonster hit)
                Hits.Add(hit);
            if (_handlers.TryGetValue(typeof(T), out var list))
                foreach (var handler in list)
                    ((IMessageHandler<T>)handler).Handle(in message);
        }
    }

    private readonly FakeBus _bus = new();
    private readonly BowModule _bow = new();
    private readonly List<MonsterState> _monsters = new();
    private PlayerState _player;
    private long _tick;
    /// <summary>The camera (the aim ray's origin); null puts it 3 m straight behind the hold point along the aim.</summary>
    private Vector3? _camera;

    /// <summary>15 m south of the middle clearing's center: open ground for 30 m to the north.</summary>
    private static readonly Vector3 Spot = OnGround(0f, 10f);

    private static readonly Vector3 North = new(0f, 0f, -1f);

    public BowModuleTests()
    {
        _bow.Start(_bus);
        _player = PlayerAt(Spot);
    }

    private static Vector3 OnGround(float x, float z) => new(x, Terrain.HeightAt(x, z), z);

    private static PlayerState PlayerAt(Vector3 feet, bool alive = true, float facing = 0f) =>
        new(feet, facing, Vector3.Zero, true, alive ? 10 : 0, 10, alive, CharacterKind.Girl);

    private static PlayerInput Input(bool fire, Vector3 aim, Vector3 camera) =>
        new(Vector2.Zero, Vector2.Zero, false, fire, true, Vector3.Normalize(aim), false, false, camera);

    private Vector3 Hold => BowGeometry.HoldPoint(_player.Position, _player.Facing);

    private Vector3 CameraFor(Vector3 aim) => _camera ?? Hold - Vector3.Normalize(aim) * 3f;

    /// <summary>Over the left shoulder, as the camera sits while drawing (facing 0: left is -X, behind is +Z).</summary>
    private static Vector3 ShoulderCamera(Vector3 feet) => feet + new Vector3(-0.6f, 1.7f, 2.6f);

    /// <summary>One tick as the host runs it: Input, Player and Monsters publish, then the bow ticks.</summary>
    private void TickWith(bool fire, Vector3 aim)
    {
        _bus.Publish(Input(fire, aim, CameraFor(aim)));
        _bus.Publish(_player);
        foreach (var m in _monsters)
            _bus.Publish(m);
        _bow.Tick(new TickContext(_tick++, Step));
    }

    private void Run(int ticks, bool fire, Vector3 aim)
    {
        for (int i = 0; i < ticks; i++)
            TickWith(fire, aim);
    }

    /// <summary>Holds Fire for <paramref name="drawTicks"/> ticks, then releases (one more tick): one shot.</summary>
    private void Shoot(int drawTicks, Vector3 aim)
    {
        Run(drawTicks, true, aim);
        TickWith(false, aim);
    }

    private Arrow OnlyArrow()
    {
        Arrow? found = null;
        foreach (var a in _bow.Arrows)
            if (a.Phase != ArrowPhase.Free)
            {
                Assert.Null(found);
                found = a;
            }
        Assert.NotNull(found);
        return found.Value;
    }

    private int UsedSlots()
    {
        int n = 0;
        foreach (var a in _bow.Arrows)
            if (a.Phase != ArrowPhase.Free)
                n++;
        return n;
    }

    private static MonsterState Monster(int id, Vector3 feet, float radius = 0.55f, bool alive = true) =>
        new(id, MonsterKind.Caterpillar, feet, radius, alive ? 3 : 0, 3, alive, false);

    /// <summary>From the bow hand of the player (facing 0) to the middle of a monster's sphere.</summary>
    private Vector3 AimAt(MonsterState m) =>
        Vector3.Normalize(m.Position + new Vector3(0f, m.Radius, 0f) - BowGeometry.HoldPoint(_player.Position, 0f));

    // ---- Draw timing ----

    [Fact]
    public void PublishesOneStateEveryTick()
    {
        Run(10, false, North);
        Assert.Equal(10, _bus.States.Count);
        Assert.All(_bus.States, s => Assert.Equal(0f, s.Draw));
        Assert.All(_bus.States, s => Assert.Equal(0, s.ArrowsInFlight));
    }

    [Fact]
    public void DrawsToFullInSixTenthsOfASecondWhileFireIsHeld()
    {
        Run(18, true, North); // 0.3 s
        Assert.Equal(0.5f, _bus.States[^1].Draw, 3);
        Run(17, true, North);
        Assert.True(_bus.States[^1].Draw < 1f);
        Run(1, true, North); // 0.6 s
        Assert.Equal(1f, _bus.States[^1].Draw, 4);
        Run(60, true, North); // holding longer stays full and shoots nothing
        Assert.Equal(1f, _bus.States[^1].Draw);
        Assert.Equal(0, UsedSlots());
        Assert.All(_bus.States, s => Assert.Equal(0, s.ArrowsInFlight));
    }

    [Fact]
    public void ReleasingFiresOneArrowAndRelaxesTheBow()
    {
        Shoot(36, North);
        var s = _bus.States[^1];
        Assert.Equal(0f, s.Draw);
        Assert.Equal(1, s.ArrowsInFlight);
        Assert.Equal(1, UsedSlots());
        Run(5, false, North); // not holding Fire shoots nothing more
        Assert.Equal(1, UsedSlots());
        Assert.Equal(1, _bus.States[^1].ArrowsInFlight);
    }

    [Fact]
    public void ArrowSpeedGoesFrom25To45MetersPerSecondByDraw()
    {
        Assert.Equal(25f, BowModule.SpeedFor(0f));
        Assert.Equal(35f, BowModule.SpeedFor(0.5f));
        Assert.Equal(45f, BowModule.SpeedFor(1f));

        Shoot(36, North);
        Assert.Equal(45f, OnlyArrow().Velocity.Length(), 0);
        Assert.Equal(1f, OnlyArrow().Draw, 4);
    }

    [Fact]
    public void AQuickTapShootsASlowArrow()
    {
        Shoot(1, North);
        var a = OnlyArrow();
        Assert.Equal(1f / 36, a.Draw, 4);
        Assert.InRange(a.Velocity.Length(), 25f, 26f);
    }

    [Fact]
    public void NoDrawingOrFiringWhileNotAlive()
    {
        _player = PlayerAt(Spot, alive: false);
        Shoot(40, North);
        Assert.All(_bus.States, s => Assert.Equal(0f, s.Draw));
        Assert.Equal(0, UsedSlots());

        // Defeated mid-draw: the draw is lost and releasing later shoots nothing.
        _player = PlayerAt(Spot);
        Run(20, true, North);
        Assert.True(_bus.States[^1].Draw > 0.5f);
        _player = PlayerAt(Spot, alive: false);
        TickWith(true, North);
        _player = PlayerAt(Spot);
        TickWith(false, North);
        Assert.Equal(0, UsedSlots());
        Assert.Equal(0f, _bus.States[^1].Draw);
    }

    // ---- Flight and gravity ----

    [Fact]
    public void ArrowStartsAtTheBowHandAndFliesAlongTheAim()
    {
        var aim = Vector3.Normalize(new Vector3(0.3f, 0.1f, -1f));
        Shoot(36, aim);
        var a = OnlyArrow();
        // Facing 0 (-Z): Right is +X (the hold point is left of it, HoldRight < 0), Forward is -Z.
        var start = Spot + new Vector3(BowGeometry.HoldRight, BowGeometry.HoldUp, -BowGeometry.HoldForward);
        // It was launched at the start and has flown one tick since.
        Assert.True(Vector3.Distance(start, a.PreviousPosition) < 1e-4f, $"{a.PreviousPosition} vs {start}");
        var step = a.Position - a.PreviousPosition;
        Assert.Equal(45f * Dt, step.Length(), 2);
        // The camera is right behind the hold point: along the aim, raised only a little against gravity.
        AssertAlongAimRaisedALittle(aim, a.Velocity);
    }

    /// <summary>Same heading as <paramref name="aim"/>, pitched up by gravity's lift only (under 4 degrees).</summary>
    private static void AssertAlongAimRaisedALittle(Vector3 aim, Vector3 velocity)
    {
        var flatAim = Vector2.Normalize(new Vector2(aim.X, aim.Z));
        var flatV = Vector2.Normalize(new Vector2(velocity.X, velocity.Z));
        Assert.True(Vector2.Dot(flatAim, flatV) > 0.99999f, $"{velocity} vs {aim}");
        float pitchAim = MathF.Asin(Vector3.Normalize(aim).Y), pitchV = MathF.Asin(Vector3.Normalize(velocity).Y);
        Assert.InRange(pitchV - pitchAim, 0f, 4f * MathF.PI / 180f);
    }

    [Fact]
    public void TheHoldPointIsOutInTheExtendedLeftHandClearOfTheHead()
    {
        Assert.Equal(new Vector3(BowGeometry.HoldRight, BowGeometry.HoldUp, BowGeometry.HoldForward), BowGeometry.HoldOffset);
        Assert.Equal(-0.45f, BowGeometry.HoldRight, 3); // 0.45 m to the left
        Assert.Equal(0.80f, BowGeometry.HoldUp, 3);
        Assert.Equal(0.45f, BowGeometry.HoldForward, 3);
        Assert.InRange(BowGeometry.FullDrawPull, 0.15f, 0.4f);
        // Clear of the chibi head (about 0.86 m wide, centered about 1.09 m above the feet).
        var head = new Vector3(0f, BowGeometry.HeadUp, 0f);
        Assert.Equal(1.09f, BowGeometry.HeadUp, 2);
        Assert.Equal(0.6f, BowGeometry.MinHeadClearance, 3);
        Assert.True(Vector3.Distance(BowGeometry.HoldOffset, head) >= BowGeometry.MinHeadClearance);
        // Seen from straight behind, the hand is outside the head's outline (half width 0.43 m).
        Assert.True(MathF.Abs(BowGeometry.HoldRight) > 0.43f || BowGeometry.HoldUp < BowGeometry.HeadUp - 0.43f);
        // In the world, for any facing, it is left of the facing.
        foreach (float facing in new[] { 0f, 1f, MathF.PI / 2, -2.5f })
        {
            var feet = new Vector3(3f, 2f, -7f);
            var left = new Vector3(-MathF.Cos(facing), 0f, MathF.Sin(facing));
            Assert.Equal(0.45f, Vector3.Dot(BowGeometry.HoldPoint(feet, facing) - feet, left), 4);
            Assert.True(Vector3.Distance(BowGeometry.HoldPoint(feet, facing), feet + head) >= BowGeometry.MinHeadClearance);
        }
    }

    [Theory]
    [InlineData(0f, -0.45f, -0.45f)]          // facing -Z: left -X
    [InlineData(MathF.PI / 2, -0.45f, 0.45f)]  // facing -X: left +Z
    [InlineData(MathF.PI, 0.45f, 0.45f)]       // facing +Z: left +X
    [InlineData(-MathF.PI / 2, 0.45f, -0.45f)] // facing +X: left -Z
    public void TheHoldPointTurnsWithTheFacing(float facing, float dx, float dz)
    {
        var feet = new Vector3(3f, 2f, -7f);
        var p = BowGeometry.HoldPoint(feet, facing);
        Assert.Equal(feet.X + dx, p.X, 4);
        Assert.Equal(feet.Y + BowGeometry.HoldUp, p.Y, 4);
        Assert.Equal(feet.Z + dz, p.Z, 4);
    }

    [Fact]
    public void ArrowsLeaveFromTheBowHandRotatedByTheFacingNotTheAim()
    {
        // Facing +X (turned right), aiming a little left of it and up: the start follows the facing.
        float facing = -MathF.PI / 2;
        _player = PlayerAt(Spot, facing: facing);
        var aim = Vector3.Normalize(new Vector3(1f, 0.15f, -0.3f));
        Shoot(36, aim);
        var a = OnlyArrow();
        var start = Spot + new Vector3(BowGeometry.HoldForward, BowGeometry.HoldUp, BowGeometry.HoldRight);
        Assert.True(Vector3.Distance(start, a.PreviousPosition) < 1e-4f, $"{a.PreviousPosition} vs {start}");
        Assert.Equal(BowGeometry.HoldPoint(Spot, facing), a.PreviousPosition);
        AssertAlongAimRaisedALittle(aim, a.Velocity);
    }

    [Fact]
    public void WithoutAnAimItShootsWhereThePlayerFaces()
    {
        _player = PlayerAt(Spot, facing: MathF.PI / 2); // facing -X
        _bus.Publish(new PlayerInput(Vector2.Zero, Vector2.Zero, false, true, false, Vector3.Zero));
        _bus.Publish(_player);
        _bow.Tick(new TickContext(_tick++, Step));
        _bus.Publish(new PlayerInput(Vector2.Zero, Vector2.Zero, false, false, false, Vector3.Zero));
        _bus.Publish(_player);
        _bow.Tick(new TickContext(_tick++, Step));
        var a = OnlyArrow();
        Assert.Equal(BowGeometry.HoldPoint(Spot, MathF.PI / 2), a.PreviousPosition);
        // Shot level along the facing, from the hold point: no lift (one tick of gravity since).
        var launch = Vector3.Normalize(a.Velocity + new Vector3(0f, BowModule.Gravity * Dt, 0f));
        Assert.True(Vector3.Distance(-Vector3.UnitX, launch) < 1e-4f, $"{launch}");
        Assert.True(MathF.Abs(MathF.Asin(Vector3.Normalize(a.Velocity).Y)) < 0.07f, $"{a.Velocity}");
        Assert.True(Vector3.Distance(-Vector3.UnitX, _bow.AimDirection) < 1e-4f, $"{_bow.AimDirection}");
        // Its target is on the facing's line from the hold point.
        var target = _bus.States[^1].Target;
        var hold = BowGeometry.HoldPoint(Spot, MathF.PI / 2);
        Assert.Equal(hold.Y, target.Y, 3);
        Assert.Equal(hold.Z, target.Z, 3);
        Assert.True(target.X < hold.X - 1f);
    }

    [Fact]
    public void LightGravityBendsTheFlightDownAtFourMetersPerSecondSquared()
    {
        var aim = Vector3.Normalize(new Vector3(0f, 0.2f, -1f));
        Shoot(36, aim);
        var first = OnlyArrow();
        Run(30, false, aim); // half a second more
        var later = OnlyArrow();
        Assert.Equal(ArrowPhase.Flying, later.Phase);
        // Horizontal speed stays; vertical speed drops by g * t.
        float flat(Vector3 v) => new Vector2(v.X, v.Z).Length();
        Assert.Equal(flat(first.Velocity), flat(later.Velocity), 3);
        Assert.Equal(first.Velocity.Y - BowModule.Gravity * 0.5f, later.Velocity.Y, 3);
        // Position follows the same ballistic path (each tick moves by its new velocity).
        float expectedY = first.Position.Y;
        float vy = first.Velocity.Y;
        for (int i = 0; i < 30; i++)
        {
            vy -= BowModule.Gravity * Dt;
            expectedY += vy * Dt;
        }
        Assert.Equal(expectedY, later.Position.Y, 3);
        Assert.Equal(first.Position.Z + first.Velocity.Z * 0.5f, later.Position.Z, 2);
        // It points along its flight: a little more downward than at the start.
        Assert.True(later.Direction.Y < first.Direction.Y);
        Assert.True(Vector3.Dot(later.Direction, Vector3.Normalize(later.Velocity)) > 0.9999f);
    }

    [Fact]
    public void AnArrowThatHitsNothingVanishesEventually()
    {
        Shoot(36, Vector3.Normalize(new Vector3(0f, 1f, -0.05f))); // straight up
        Run((int)(BowModule.MaxFlightSeconds * 60) + 1, false, North);
        Assert.Equal(0, UsedSlots());
        Assert.Equal(0, _bus.States[^1].ArrowsInFlight);
    }

    // ---- The crosshair's target ----

    /// <summary>Draws for a few ticks with the camera at <paramref name="camera"/> along <paramref name="aim"/>; the published target.</summary>
    private Vector3 TargetWhileDrawing(Vector3 camera, Vector3 aim)
    {
        _camera = camera;
        Run(5, true, aim);
        Assert.True(_bus.States[^1].Draw > 0f);
        Assert.Equal(_bow.Target, _bus.States[^1].Target);
        return _bus.States[^1].Target;
    }

    private static float DistanceToRay(Vector3 p, Vector3 origin, Vector3 aim)
    {
        aim = Vector3.Normalize(aim);
        var f = p - origin;
        return (f - aim * Vector3.Dot(f, aim)).Length();
    }

    [Fact]
    public void TheTargetIsTheNearSideOfTheFirstMonsterOnTheCrosshair()
    {
        var camera = ShoulderCamera(Spot);
        var near = Monster(1, OnGround(Spot.X + 1f, Spot.Z - 12f));
        var far = Monster(2, OnGround(Spot.X + 2f, Spot.Z - 24f)); // right behind it on the same ray
        _monsters.Add(far);
        _monsters.Add(near);
        var center = near.Position + new Vector3(0f, near.Radius, 0f);
        var aim = Vector3.Normalize(center - camera);
        var target = TargetWhileDrawing(camera, aim);
        Assert.True(DistanceToRay(target, camera, aim) < 1e-3f);
        Assert.Equal(near.Radius, Vector3.Distance(target, center), 2);
        Assert.True(Vector3.Dot(target - center, aim) < 0f); // the side facing the camera
    }

    [Fact]
    public void MonstersBetweenTheCameraAndTheCharacterAreNotTheTarget()
    {
        var camera = ShoulderCamera(Spot);
        var aim = Vector3.Normalize(new Vector3(0f, 0.1f, -1f));
        // A monster right behind the player, on the camera's ray.
        var behind = camera + aim * 1.2f;
        _monsters.Add(Monster(1, behind - new Vector3(0f, 0.3f, 0f), radius: 0.3f));
        var target = TargetWhileDrawing(camera, aim);
        Assert.True(Vector3.Dot(target - Hold, aim) > 0f, $"{target}");
    }

    [Fact]
    public void TheTargetIsOnTheFirstSolidObstacleOnTheCrosshair()
    {
        // East of a forest giant mushroom's stem, facing west (-X) toward it.
        var trunk = MapGround.Obstacles[MapGround.ForestTrunkCount / 2];
        var feet = OnGround(trunk.X + trunk.Radius + 3f, trunk.Z);
        _player = PlayerAt(feet, facing: MathF.PI / 2);
        var camera = feet + new Vector3(2.6f, 1.7f, 0.6f); // behind and over the left shoulder
        var aim = Vector3.Normalize(new Vector3(trunk.X, feet.Y + 1.5f, trunk.Z) - camera);
        var target = TargetWhileDrawing(camera, aim);
        Assert.True(DistanceToRay(target, camera, aim) < 1e-3f);
        Assert.Equal(trunk.Radius, Vector2.Distance(new Vector2(target.X, target.Z), new Vector2(trunk.X, trunk.Z)), 2);
        Assert.True(target.X > trunk.X);
        Assert.True(MapGround.Blocks(target.X - 0.05f, target.Z));
    }

    [Fact]
    public void TheTargetIsOnTheGroundWhereTheCrosshairMeetsIt()
    {
        var camera = ShoulderCamera(Spot);
        var spot = OnGround(Spot.X + 0.5f, Spot.Z - 9f);
        var aim = Vector3.Normalize(spot - camera);
        var target = TargetWhileDrawing(camera, aim);
        Assert.True(DistanceToRay(target, camera, aim) < 1e-3f);
        Assert.Equal(MapGround.HeightAt(target.X, target.Z), target.Y, 2);
        Assert.True(Vector3.Distance(target, spot) < 0.2f, $"{target} vs {spot}");
    }

    [Fact]
    public void WithNothingOnTheCrosshairTheTargetIs60MetersAlongIt()
    {
        var camera = ShoulderCamera(Spot);
        var aim = Vector3.Normalize(new Vector3(0.2f, 0.6f, -1f)); // up into the sky
        var target = TargetWhileDrawing(camera, aim);
        Assert.True(Vector3.Distance(camera + aim * BowModule.MaxAimDistance, target) < 1e-3f, $"{target}");
        Assert.Equal(60f, BowModule.MaxAimDistance);
    }

    /// <summary>Ticks until the shot arrow is gone or <paramref name="seconds"/> pass; its closest approach to <paramref name="point"/>.</summary>
    private float ClosestApproach(Vector3 point, Vector3 aim, float seconds)
    {
        float best = float.PositiveInfinity;
        for (int i = 0; i < seconds * 60; i++)
        {
            Arrow a = default;
            foreach (var arrow in _bow.Arrows)
                if (arrow.Shot == 1)
                    a = arrow;
            // The tick's straight segment from its previous to its new position.
            var d = a.Position - a.PreviousPosition;
            float t = d.LengthSquared() > 0f ? Math.Clamp(Vector3.Dot(point - a.PreviousPosition, d) / d.LengthSquared(), 0f, 1f) : 0f;
            best = MathF.Min(best, Vector3.Distance(point, a.PreviousPosition + d * t));
            if (a.Phase != ArrowPhase.Flying)
                break;
            TickWith(false, aim);
        }
        return best;
    }

    [Theory]
    [InlineData(10f, 1.5f, 0.5f, 36)]
    [InlineData(10f, -2f, -1f, 1)]
    [InlineData(25f, 3f, 1f, 18)]
    [InlineData(25f, -1f, -2f, 1)]
    [InlineData(45f, -4f, 2f, 36)]
    [InlineData(45f, 2f, -3f, 1)]
    public void ArrowsFlyFromTheBowToWhereTheCrosshairIs(float distance, float side, float up, int drawTicks)
    {
        // High above the forest (nothing else in the way); the camera over the left shoulder.
        var feet = Spot + new Vector3(0f, 15f, 0f);
        _player = PlayerAt(feet);
        var camera = ShoulderCamera(feet);
        var center = Hold + new Vector3(side, up, -distance);
        _monsters.Add(Monster(5, center - new Vector3(0f, 0.15f, 0f), radius: 0.15f));
        var aim = Vector3.Normalize(center - camera);
        _camera = camera;
        Shoot(drawTicks, aim);
        var target = _bus.States[^1].Target;
        Assert.True(Vector3.Distance(target, center) < 0.16f, $"{target}");
        Assert.Equal(BowGeometry.HoldPoint(feet, 0f), OnlyArrow().PreviousPosition);
        Assert.True(ClosestApproach(target, aim, 3f) < 0.3f);
        Assert.Equal(5, Assert.Single(_bus.Hits).MonsterId);
    }

    [Theory]
    [InlineData(10f)]
    [InlineData(25f)]
    [InlineData(45f)]
    public void ArrowsLandOnGroundTargets(float distance)
    {
        // A long gentle downhill look from up on something: the crosshair rests on the ground.
        var feet = Spot + new Vector3(0f, 4f, 0f);
        _player = PlayerAt(feet);
        var camera = ShoulderCamera(feet);
        var spot = OnGround(Hold.X, Hold.Z - distance);
        var aim = Vector3.Normalize(spot - camera);
        _camera = camera;
        Shoot(36, aim);
        var target = _bus.States[^1].Target;
        Assert.True(Vector3.Distance(target, spot) < 0.3f, $"{target} vs {spot}");
        Assert.True(ClosestApproach(target, aim, 3f) < 0.3f);
        var a = OnlyArrow();
        Assert.Equal(ArrowPhase.Stuck, a.Phase);
        Assert.True(Vector3.Distance(a.Position, target) < 0.3f, $"{a.Position} vs {target}");
    }

    [Fact]
    public void WithTheCameraRightBehindTheHoldPointNothingChanges()
    {
        // The old line: from the hold point along the camera's aim; the arrow goes where it pointed.
        var aim = Vector3.Normalize(new Vector3(0.1f, 0.25f, -1f));
        Shoot(36, aim); // the default camera: 3 m straight behind the hold point
        var target = _bus.States[^1].Target;
        Assert.True(DistanceToRay(target, Hold, aim) < 1e-3f);
        Assert.Equal(BowModule.MaxAimDistance - 3f, Vector3.Distance(Hold, target), 2);
        var a = OnlyArrow();
        Assert.Equal(Hold, a.PreviousPosition);
        AssertAlongAimRaisedALittle(aim, a.Velocity);
        // The launch velocity (before its first tick of gravity) is the bow's aim.
        var launch = Vector3.Normalize(a.Velocity + new Vector3(0f, BowModule.Gravity * Dt, 0f));
        Assert.True(Vector3.Distance(_bow.AimDirection, launch) < 1e-4f, $"{_bow.AimDirection} vs {launch}");
        Assert.True(ClosestApproach(target, aim, 3f) < 0.3f);
    }

    // ---- Hits ----

    [Fact]
    public void HittingAMonsterPublishesItsDamageAndPoisonAndStopsTheArrow()
    {
        _monsters.Add(Monster(7, OnGround(Spot.X, Spot.Z - 6f)));
        _monsters.Add(Monster(8, OnGround(Spot.X, Spot.Z - 9f))); // right behind it
        var aim = AimAt(_monsters[0]);
        Shoot(36, aim);
        Run(30, false, aim);
        var hit = Assert.Single(_bus.Hits);
        Assert.Equal(7, hit.MonsterId);
        Assert.Equal(3, hit.Damage);
        Assert.Equal(1f, hit.PoisonDamagePerSecond);
        Assert.Equal(3f, hit.PoisonSeconds);
        Assert.Equal(0, hit.Source);
        Assert.Equal(0, UsedSlots());
        Assert.Equal(0, _bus.States[^1].ArrowsInFlight);
    }

    [Theory]
    [InlineData(1, 1)]   // a quick tap
    [InlineData(18, 2)]  // half drawn
    [InlineData(36, 3)]  // full
    public void DamageGoesFromOneToThreeByDraw(int drawTicks, int damage)
    {
        _monsters.Add(Monster(3, OnGround(Spot.X, Spot.Z - 4f)));
        var aim = AimAt(_monsters[0]);
        Shoot(drawTicks, aim);
        Run(20, false, aim);
        Assert.Equal(damage, Assert.Single(_bus.Hits).Damage);
    }

    [Fact]
    public void DamageForCoversTheRange()
    {
        Assert.Equal(1, BowModule.DamageFor(0f));
        Assert.Equal(1, BowModule.DamageFor(0.2f));
        Assert.Equal(2, BowModule.DamageFor(0.5f));
        Assert.Equal(3, BowModule.DamageFor(0.9f));
        Assert.Equal(3, BowModule.DamageFor(1f));
    }

    [Fact]
    public void ArrowsHitTheMonstersSphereNotJustItsFeet()
    {
        // A big monster whose sphere (center one radius above its feet) reaches over the arrow,
        // and a small one 1.5 m to the side that the arrow passes.
        _monsters.Add(Monster(1, OnGround(Spot.X + 1.5f, Spot.Z - 5f), radius: 0.4f));
        _monsters.Add(Monster(2, OnGround(Spot.X, Spot.Z - 10f), radius: 1.2f));
        Shoot(36, North);
        Run(30, false, North);
        Assert.Equal(2, Assert.Single(_bus.Hits).MonsterId);
    }

    [Fact]
    public void DefeatedMonstersAreNotHit()
    {
        _monsters.Add(Monster(5, OnGround(Spot.X, Spot.Z - 5f), alive: false));
        Shoot(36, North);
        Run(20, false, North);
        Assert.Empty(_bus.Hits);
    }

    [Fact]
    public void OnlyMonstersPublishedThisTickCanBeHit()
    {
        _monsters.Add(Monster(9, OnGround(Spot.X, Spot.Z - 8f)));
        TickWith(false, North);
        _monsters.Clear(); // it went away (no more states)
        Shoot(36, North);
        Run(20, false, North);
        Assert.Empty(_bus.Hits);
    }

    [Fact]
    public void AFastArrowDoesNotTunnelThroughASmallMonster()
    {
        // 45 m/s is 0.75 m per tick, more than this monster's diameter.
        // Its sphere sits right on the arrow's line (from the bow hand), 6.675 m ahead: between two ticks' positions.
        var hand = BowGeometry.HoldPoint(Spot, 0f);
        var feet = new Vector3(hand.X, hand.Y - 0.3f, hand.Z - 6.675f);
        _monsters.Add(Monster(4, feet, radius: 0.3f));
        Shoot(36, North);
        Run(20, false, North);
        Assert.Equal(4, Assert.Single(_bus.Hits).MonsterId);
    }

    // ---- Sticking ----

    [Fact]
    public void ArrowsStickInTheGroundForFiveSecondsThenVanish()
    {
        var down = Vector3.Normalize(new Vector3(0f, -0.5f, -1f));
        Shoot(36, down);
        Run(10, false, down);
        var a = OnlyArrow();
        Assert.Equal(ArrowPhase.Stuck, a.Phase);
        Assert.Equal(ArrowStuckIn.Ground, a.StuckIn);
        Assert.Equal(Terrain.HeightAt(a.Position.X, a.Position.Z), a.Position.Y, 2);
        Assert.Equal(Vector3.Zero, a.Velocity);
        Assert.True(a.Direction.Y < 0f); // it points into the ground
        Assert.Equal(0, _bus.States[^1].ArrowsInFlight);

        // Stays put until 5 s after it stuck.
        float stuckSoFar = a.StuckSeconds;
        Run((int)MathF.Round((BowModule.StickSeconds - stuckSoFar) * 60) - 2, false, down);
        var still = OnlyArrow();
        Assert.Equal(ArrowPhase.Stuck, still.Phase);
        Assert.Equal(a.Position, still.Position);
        Run(3, false, down);
        Assert.Equal(0, UsedSlots());
    }

    [Fact]
    public void ArrowsStickInGiantMushroomTrunks()
    {
        // Stand 2 m out from a trunk's side, on its east, and shoot west at it.
        var trunk = Obstacles.All[Obstacles.Count / 2];
        _player = PlayerAt(OnGround(trunk.X + trunk.Radius + 2f, trunk.Z));
        var west = -Vector3.UnitX;
        Shoot(36, west);
        Run(10, false, west);
        var a = OnlyArrow();
        Assert.Equal(ArrowPhase.Stuck, a.Phase);
        Assert.Equal(ArrowStuckIn.Trunk, a.StuckIn);
        Assert.Equal(trunk.Radius, Vector2.Distance(new Vector2(a.Position.X, a.Position.Z), trunk.Center), 2);
        Assert.True(a.Position.Y > Terrain.HeightAt(a.Position.X, a.Position.Z));

        Run(5 * 60, false, west);
        Assert.Equal(0, UsedSlots());
    }

    [Fact]
    public void ArrowsFlyOverATrunkAboveItsStem()
    {
        var trunk = Obstacles.All[Obstacles.Count / 3];
        // Standing high above the ground (on something): the arrow starts over the stems.
        var feet = OnGround(trunk.X + trunk.Radius + 2f, trunk.Z) + new Vector3(0f, BowModule.TrunkHeight + 1f, 0f);
        _player = PlayerAt(feet);
        var west = -Vector3.UnitX;
        Shoot(36, west);
        Run(10, false, west);
        var a = OnlyArrow();
        Assert.Equal(ArrowPhase.Flying, a.Phase);
        Assert.True(a.Position.X < trunk.X - trunk.Radius);
    }

    // ---- Pool ----

    [Fact]
    public void FreedSlotsAreReused()
    {
        var down = Vector3.Normalize(new Vector3(0f, -0.5f, -1f));
        for (int i = 0; i < 3; i++)
            Shoot(2, down);
        Run(10, false, down);
        Assert.Equal(3, UsedSlots());
        Run(5 * 60, false, down); // all vanish
        Assert.Equal(0, UsedSlots());

        Shoot(2, down);
        var a = OnlyArrow();
        Assert.Equal(4, a.Shot);
        Assert.Equal(BowModule.PoolSize, _bow.Arrows.Length);
    }

    [Fact]
    public void AFullPoolReusesTheOldestArrow()
    {
        var down = Vector3.Normalize(new Vector3(0f, -0.5f, -1f));
        for (int i = 0; i < BowModule.PoolSize; i++)
            Shoot(1, down);
        Run(10, false, down);
        Assert.Equal(BowModule.PoolSize, UsedSlots());

        int oldestSlot = -1;
        for (int i = 0; i < BowModule.PoolSize; i++)
            if (_bow.Arrows[i].Shot == 1)
                oldestSlot = i;
        Assert.True(oldestSlot >= 0);

        Shoot(1, North);
        Assert.Equal(BowModule.PoolSize, UsedSlots());
        Assert.Equal(BowModule.PoolSize + 1, _bow.Arrows[oldestSlot].Shot);
        Assert.Equal(ArrowPhase.Flying, _bow.Arrows[oldestSlot].Phase);
        Assert.Equal(1, _bus.States[^1].ArrowsInFlight);
        foreach (var a in _bow.Arrows)
            Assert.NotEqual(1, a.Shot); // the first arrow is gone
    }

    // ---- Engine rules ----

    [Fact]
    public void TickBeforeStartThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new BowModule().Tick(new TickContext(0, Step)));
    }
}
