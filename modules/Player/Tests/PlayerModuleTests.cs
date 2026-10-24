using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Forest;
using Fungtopia.Input;
using Fungtopia.Shell;
using Xunit;

namespace Fungtopia.Player.Tests;

public class PlayerModuleTests
{
    private const double Step = 1.0 / 60;

    /// <summary>Fake of the shell's bus: delivers to subscribers and records what the player publishes.</summary>
    private sealed class FakeBus : IMessageBus
    {
        private readonly Dictionary<Type, List<object>> _handlers = new();
        public readonly List<PlayerState> States = new();
        public readonly List<PlayerDefeated> Defeats = new();

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
                _handlers[typeof(T)] = list = new List<object>();
            list.Add(handler);
        }

        public void Publish<T>(in T message) where T : struct
        {
            if (message is PlayerState state)
                States.Add(state);
            else if (message is PlayerDefeated defeated)
                Defeats.Add(defeated);
            if (_handlers.TryGetValue(typeof(T), out var list))
                foreach (var handler in list)
                    ((IMessageHandler<T>)handler).Handle(in message);
        }
    }

    private readonly FakeBus _bus = new();
    private readonly PlayerModule _player = new();
    private long _tick;

    public PlayerModuleTests() => _player.Start(_bus);

    private static PlayerInput Moving(float x, float y, bool fire = false, Vector3? aim = null, bool jump = false) =>
        new(new Vector2(x, y), Vector2.Zero, jump, fire, aim.HasValue, aim.HasValue ? Vector3.Normalize(aim.Value) : Vector3.Zero);

    private static PlayerInput Idle => default;

    /// <summary>One tick as the host runs it: Input publishes first, then the player ticks.</summary>
    private void TickWith(in PlayerInput input)
    {
        _bus.Publish(input);
        _player.Tick(new TickContext(_tick++, Step));
    }

    private void Run(double seconds, PlayerInput input)
    {
        int ticks = (int)Math.Round(seconds / Step);
        for (int i = 0; i < ticks; i++)
            TickWith(input);
    }

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);

    private static Vector2 FacingDirection(float facing) => new(-MathF.Sin(facing), -MathF.Cos(facing));

    private static void AssertDirection(Vector2 expected, Vector2 actual, float tolerance = 0.02f)
    {
        Assert.True(Vector2.Distance(Vector2.Normalize(expected), Vector2.Normalize(actual)) < tolerance,
            $"expected direction {Vector2.Normalize(expected)}, got {Vector2.Normalize(actual)}");
    }

    /// <summary>The middle clearing: open ground with no trunks for many meters around.</summary>
    private static readonly Vector3 Middle = new(0f, 0f, -5f);

    // ---- Start and state ----

    [Fact]
    public void StartsAtThePlayerStartWithFullHealthAsTheGirl()
    {
        TickWith(Idle);
        var s = Assert.Single(_bus.States);
        Assert.Equal(ForestBounds.PlayerStart, s.Position);
        Assert.Equal(10, s.Health);
        Assert.Equal(10, s.MaxHealth);
        Assert.True(s.Alive);
        Assert.True(s.Grounded);
        Assert.Equal(CharacterKind.Girl, s.Character);
    }

    [Fact]
    public void PublishesOneStateEveryTickWithTheChosenCharacter()
    {
        _player.Character = CharacterKind.Boy;
        Run(0.5, Moving(0f, 1f));
        Assert.Equal(30, _bus.States.Count);
        Assert.All(_bus.States, s => Assert.Equal(CharacterKind.Boy, s.Character));
        Assert.Equal(_player.Position, _bus.States[^1].Position);
        Assert.Empty(_bus.Defeats);
    }

    [Fact]
    public void TickBeforeStartThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new PlayerModule().Tick(new TickContext(0, Step)));
    }

    // ---- Movement relative to the camera ----

    [Fact]
    public void WalksForwardAlongTheCameraAtFiveMetersPerSecond()
    {
        _player.PlaceAt(Middle);
        Run(1.0, Moving(0f, 1f));
        var s = _bus.States[^1];
        AssertDirection(new Vector2(0f, -1f), Flat(s.Velocity)); // camera yaw 0 looks along -Z
        Assert.Equal(5f, Flat(s.Velocity).Length(), 2);
        Assert.Equal(5f, Vector2.Distance(Flat(Middle), Flat(s.Position)), 1);
    }

    [Fact]
    public void StrafesRightOfTheCamera()
    {
        _player.PlaceAt(Middle);
        Run(0.5, Moving(1f, 0f));
        AssertDirection(new Vector2(1f, 0f), Flat(_bus.States[^1].Velocity));
    }

    [Fact]
    public void MovesRelativeToTheCameraYawAccumulatedFromLook()
    {
        _player.PlaceAt(Middle);
        // Turn the camera right by a quarter turn over three ticks: forward is now +X (east).
        var look = new PlayerInput(Vector2.Zero, new Vector2(MathF.PI / 6f, 0f), false, false, false, Vector3.Zero);
        for (int i = 0; i < 3; i++)
            TickWith(look);
        Assert.Equal(-MathF.PI / 2f, _player.CameraYaw, 3);
        Run(0.5, Moving(0f, 1f));
        AssertDirection(new Vector2(1f, 0f), Flat(_bus.States[^1].Velocity));
        Run(0.5, Moving(-1f, 0f)); // left of a camera looking east is north (-Z)
        AssertDirection(new Vector2(0f, -1f), Flat(_bus.States[^1].Velocity));
    }

    [Fact]
    public void MovesRelativeToTheAimDirectionWhenThereIsOne()
    {
        _player.PlaceAt(Middle);
        // The camera looks south-west and a little down.
        Run(0.5, Moving(0f, 1f, aim: new Vector3(-1f, -0.4f, 1f)));
        var v = _bus.States[^1].Velocity;
        AssertDirection(new Vector2(-1f, 1f), Flat(v));
        Assert.Equal(5f, Flat(v).Length(), 2);
    }

    [Fact]
    public void DiagonalMoveIsNotFasterThanWalking()
    {
        _player.PlaceAt(Middle);
        Run(0.5, Moving(1f, 1f));
        Assert.Equal(5f, Flat(_bus.States[^1].Velocity).Length(), 2);
    }

    [Fact]
    public void StandsStillWithoutInput()
    {
        _player.PlaceAt(Middle);
        Run(1.0, Idle);
        Assert.Equal(Vector3.Zero, _bus.States[^1].Velocity);
        Assert.Equal(Flat(Middle), Flat(_player.Position));
    }

    // ---- Facing ----

    [Fact]
    public void TurnsSmoothlyToFaceTheMoveDirection()
    {
        _player.PlaceAt(Middle, facing: 0f);
        TickWith(Moving(1f, 0f)); // walk east while facing north
        float first = _bus.States[^1].Facing;
        Assert.True(first < 0f && first > -0.3f, $"one tick turned to {first}, not a small step right");
        Run(0.5, Moving(1f, 0f));
        AssertDirection(new Vector2(1f, 0f), FacingDirection(_bus.States[^1].Facing));
    }

    [Fact]
    public void TurnsTheShortWayRound()
    {
        _player.PlaceAt(Middle, facing: 3f); // nearly south, turned a little left
        TickWith(Moving(0f, -1f)); // walk south (+Z, yaw ±π): the short way is further left
        Assert.True(_bus.States[^1].Facing > 3f || _bus.States[^1].Facing < -3f);
        Run(0.5, Moving(0f, -1f));
        AssertDirection(new Vector2(0f, 1f), FacingDirection(_bus.States[^1].Facing));
    }

    [Fact]
    public void KeepsFacingWhenStopping()
    {
        _player.PlaceAt(Middle);
        Run(0.5, Moving(1f, 0f));
        float facing = _bus.States[^1].Facing;
        Run(0.5, Idle);
        Assert.Equal(facing, _bus.States[^1].Facing);
    }

    [Fact]
    public void FacesTheAimWhileFireIsHeldEvenWhenWalkingElsewhere()
    {
        _player.PlaceAt(Middle);
        // Aim west (camera looks west), walk to the camera's right (north).
        Run(0.5, Moving(1f, 0f, fire: true, aim: new Vector3(-1f, 0.2f, 0f)));
        var s = _bus.States[^1];
        AssertDirection(new Vector2(-1f, 0f), FacingDirection(s.Facing));
        AssertDirection(new Vector2(0f, -1f), Flat(s.Velocity));
        // Fire released: turns back toward the move direction.
        Run(0.5, Moving(1f, 0f, aim: new Vector3(-1f, 0.2f, 0f)));
        AssertDirection(new Vector2(0f, -1f), FacingDirection(_bus.States[^1].Facing));
    }

    // ---- Jump and ground ----

    [Fact]
    public void JumpsAboutOnePointTwoMetersAndLands()
    {
        _player.PlaceAt(Middle);
        float ground = _player.Position.Y;
        TickWith(Moving(0f, 0f, jump: true));
        Assert.False(_bus.States[^1].Grounded);
        Assert.True(_bus.States[^1].Velocity.Y > 0f);
        float apex = ground;
        int airborne = 1;
        for (int i = 0; i < 120 && !_bus.States[^1].Grounded; i++)
        {
            TickWith(Idle);
            apex = MathF.Max(apex, _bus.States[^1].Position.Y);
            airborne++;
        }
        Assert.Equal(1.2f, apex - ground, 1);
        Assert.True(_bus.States[^1].Grounded);
        Assert.Equal(ground, _bus.States[^1].Position.Y, 4);
        Assert.InRange(airborne * Step, 0.5, 1.4);
    }

    [Fact]
    public void CannotJumpAgainInTheAir()
    {
        _player.PlaceAt(Middle);
        float ground = _player.Position.Y;
        TickWith(Moving(0f, 0f, jump: true));
        Run(0.2, Idle);
        TickWith(Moving(0f, 0f, jump: true));
        float apex = ground;
        for (int i = 0; i < 120; i++)
        {
            TickWith(Idle);
            apex = MathF.Max(apex, _player.Position.Y);
        }
        Assert.True(apex - ground < 1.35f, $"jumped {apex - ground} m");
    }

    [Fact]
    public void JumpsWhileWalkingAndKeepsMoving()
    {
        _player.PlaceAt(Middle);
        TickWith(Moving(0f, 1f, jump: true));
        Run(0.3, Moving(0f, 1f));
        var s = _bus.States[^1];
        Assert.False(s.Grounded);
        Assert.Equal(5f, Flat(s.Velocity).Length(), 2);
    }

    [Fact]
    public void FollowsTheGroundUpAndDownHills()
    {
        // North up the path from the start for 6 s, over rolling ground.
        float lowest = float.MaxValue, highest = float.MinValue;
        for (int i = 0; i < 360; i++)
        {
            TickWith(Moving(0f, 1f));
            var p = _bus.States[^1].Position;
            Assert.True(_bus.States[^1].Grounded);
            Assert.Equal(Terrain.HeightAt(p.X, p.Z), p.Y, 4);
            lowest = MathF.Min(lowest, p.Y);
            highest = MathF.Max(highest, p.Y);
        }
        Assert.True(highest - lowest > 0.1f, $"the walk only changed height by {highest - lowest} m");
    }

    // ---- Obstacles and bounds ----

    /// <summary>A trunk with no other trunk within 10 m of its edge.</summary>
    private static Obstacle LoneTrunk()
    {
        foreach (var o in Obstacles.All)
        {
            bool alone = true;
            foreach (var other in Obstacles.All)
                if (!other.Equals(o) && Vector2.Distance(o.Center, other.Center) < o.Radius + other.Radius + 10f)
                    alone = false;
            if (alone && ForestBounds.Contains(o.X, o.Z, o.Radius + 10f))
                return o;
        }
        throw new InvalidOperationException("no lone trunk in the forest");
    }

    [Fact]
    public void NeverWalksIntoATrunk()
    {
        var trunk = LoneTrunk();
        _player.PlaceAt(new Vector3(trunk.X, 0f, trunk.Z + trunk.Radius + 3f));
        for (int i = 0; i < 180; i++)
        {
            TickWith(Moving(0f, 1f)); // straight north at its center
            Assert.True(Vector2.Distance(trunk.Center, Flat(_player.Position)) >= trunk.Radius + PlayerModule.Radius - 0.001f);
        }
        // Pressed against it and stopped.
        Assert.Equal(trunk.Radius + PlayerModule.Radius, Vector2.Distance(trunk.Center, Flat(_player.Position)), 2);
        Assert.True(Flat(_bus.States[^1].Velocity).Length() < 0.1f);
    }

    [Fact]
    public void SlidesAroundATrunkWhenWalkingIntoItsSide()
    {
        var trunk = LoneTrunk();
        float startZ = trunk.Z + trunk.Radius + 3f;
        _player.PlaceAt(new Vector3(trunk.X + 0.4f * trunk.Radius, 0f, startZ));
        for (int i = 0; i < 240; i++)
        {
            TickWith(Moving(0f, 1f));
            Assert.True(Vector2.Distance(trunk.Center, Flat(_player.Position)) >= trunk.Radius + PlayerModule.Radius - 0.001f);
        }
        // Slid past it on the east side and walked on north.
        Assert.True(_player.Position.Z < trunk.Z - trunk.Radius, $"stuck at z {_player.Position.Z}, trunk at {trunk.Z}");
        Assert.True(_player.Position.X > trunk.X);
    }

    [Fact]
    public void StaysInsideTheForestAndSlidesAlongItsEdge()
    {
        _player.PlaceAt(new Vector3(0f, 0f, 92f)); // the start clearing, by the south edge
        float limit = ForestBounds.HalfSize - PlayerModule.Radius;
        Run(3.0, Moving(0f, -1f)); // south
        Assert.Equal(limit, _player.Position.Z, 3);
        Assert.True(Flat(_bus.States[^1].Velocity).Length() < 0.01f);
        // South-east: blocked to the south, slides east.
        float x = _player.Position.X;
        Run(0.5, Moving(1f, -1f));
        Assert.Equal(limit, _player.Position.Z, 3);
        Assert.True(_player.Position.X > x + 1f);
        Assert.True(_bus.States[^1].Velocity.X > 3f);
        // The east edge, at the end of the west-east path.
        _player.PlaceAt(new Vector3(96f, 0f, -40f));
        Run(2.0, Moving(1f, 0f));
        Assert.Equal(limit, _player.Position.X, 3);
        Assert.True(ForestBounds.Contains(_player.Position.X, _player.Position.Z));
    }

    // ---- Damage, defeat and comeback ----

    [Fact]
    public void DamageLowersHealth()
    {
        _bus.Publish(new DamagePlayer(3, Vector3.Zero));
        TickWith(Idle);
        Assert.Equal(7, _bus.States[^1].Health);
        Assert.True(_bus.States[^1].Alive);
    }

    [Fact]
    public void IsInvulnerableForAboutAFractionOfASecondAfterAHit()
    {
        _bus.Publish(new DamagePlayer(2, Vector3.Zero));
        TickWith(Idle);
        _bus.Publish(new DamagePlayer(2, Vector3.Zero)); // too soon: ignored
        Run(0.5, Idle);
        _bus.Publish(new DamagePlayer(2, Vector3.Zero)); // still within 0.7 s: ignored
        TickWith(Idle);
        Assert.Equal(8, _bus.States[^1].Health);
        Run(0.25, Idle);
        _bus.Publish(new DamagePlayer(2, Vector3.Zero)); // after 0.7 s: hurts
        TickWith(Idle);
        Assert.Equal(6, _bus.States[^1].Health);
    }

    [Fact]
    public void IgnoresNonPositiveDamage()
    {
        _bus.Publish(new DamagePlayer(0, Vector3.Zero));
        _bus.Publish(new DamagePlayer(-4, Vector3.Zero));
        TickWith(Idle);
        Assert.Equal(10, _bus.States[^1].Health);
        _bus.Publish(new DamagePlayer(1, Vector3.Zero)); // and those did not start invulnerability
        TickWith(Idle);
        Assert.Equal(9, _bus.States[^1].Health);
    }

    private void Defeat()
    {
        for (int hits = 0; hits < 5; hits++)
        {
            _bus.Publish(new DamagePlayer(2, Vector3.Zero));
            Run(0.8, Idle);
        }
    }

    [Fact]
    public void AtZeroHealthIsDefeatedOnce()
    {
        _player.PlaceAt(Middle);
        Defeat();
        var s = _bus.States[^1];
        Assert.Equal(0, s.Health);
        Assert.False(s.Alive);
        var defeat = Assert.Single(_bus.Defeats);
        Assert.Equal(Flat(Middle), Flat(defeat.Position));
        // More hits change nothing and publish no second defeat.
        _bus.Publish(new DamagePlayer(5, Vector3.Zero));
        TickWith(Idle);
        Assert.Single(_bus.Defeats);
        Assert.Equal(0, _bus.States[^1].Health);
    }

    [Fact]
    public void OverkillStopsAtZero()
    {
        _bus.Publish(new DamagePlayer(25, Vector3.Zero));
        TickWith(Idle);
        Assert.Equal(0, _bus.States[^1].Health);
        Assert.False(_bus.States[^1].Alive);
        Assert.Single(_bus.Defeats);
    }

    [Fact]
    public void CannotMoveOrJumpWhileDefeated()
    {
        _player.PlaceAt(Middle);
        _bus.Publish(new DamagePlayer(10, Vector3.Zero));
        TickWith(Idle);
        var at = _player.Position;
        Run(1.0, Moving(1f, 1f, jump: true));
        Assert.Equal(at, _player.Position);
    }

    [Fact]
    public void ComesBackAtTheStartWithFullHealthAfterThreeSeconds()
    {
        _player.PlaceAt(Middle);
        _bus.Publish(new DamagePlayer(10, Vector3.Zero));
        TickWith(Idle);
        Run(2.9, Idle);
        Assert.False(_bus.States[^1].Alive);
        Run(0.2, Idle);
        var s = _bus.States[^1];
        Assert.True(s.Alive);
        Assert.Equal(10, s.Health);
        Assert.Equal(ForestBounds.PlayerStart, s.Position);
        Assert.True(s.Grounded);
        Assert.Single(_bus.Defeats);
        // It can be hurt and defeated again.
        Run(1.0, Moving(0f, 1f));
        _bus.Publish(new DamagePlayer(10, Vector3.Zero));
        TickWith(Idle);
        Assert.Equal(2, _bus.Defeats.Count);
    }

    // ---- Performance ----

    [Fact]
    public void TicksWithoutAllocating()
    {
        var bus = new NullBus();
        var player = new PlayerModule();
        player.Start(bus);
        var input = Moving(0.6f, 0.8f, fire: true, aim: new Vector3(0.3f, 0f, -1f), jump: true);
        player.Handle(input);
        player.Tick(new TickContext(0, Step)); // warm up
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 600; i++)
        {
            player.Handle(input);
            player.Handle(new DamagePlayer(1, Vector3.Zero));
            player.Tick(new TickContext(i + 1, Step));
        }
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    private sealed class NullBus : IMessageBus
    {
        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct { }
        public void Publish<T>(in T message) where T : struct { }
    }
}
