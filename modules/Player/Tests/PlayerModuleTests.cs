using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Dialogue;
using Fungtopia.Input;
using Fungtopia.Map;
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
    public void StartsInMushroomTownAtTheHomeDoorFacingOutWithFullHealthAsTheGirl()
    {
        TickWith(Idle);
        var s = Assert.Single(_bus.States);
        Assert.Equal(Places.HomeDoor.Position, s.Position);
        Assert.Equal(Places.HomeDoor.Yaw, s.Facing);
        Assert.Equal(Area.Town, s.Area);
        // Facing out: away from the house, toward the town square.
        var house = Buildings.First(BuildingKind.PlayerHouse);
        AssertDirection(house.Front, FacingDirection(s.Facing));
        Assert.True(PlayerModule.CanStand(s.Position.X, s.Position.Z));
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
        // North up the forest path from its south clearing for 6 s, over rolling ground.
        _player.PlaceAt(new Vector3(0f, 0f, 84f));
        float lowest = float.MaxValue, highest = float.MinValue;
        for (int i = 0; i < 360; i++)
        {
            TickWith(Moving(0f, 1f));
            var p = _bus.States[^1].Position;
            Assert.True(_bus.States[^1].Grounded);
            Assert.Equal(Ground.HeightAt(p.X, p.Z), p.Y, 4);
            lowest = MathF.Min(lowest, p.Y);
            highest = MathF.Max(highest, p.Y);
        }
        Assert.True(highest - lowest > 0.1f, $"the walk only changed height by {highest - lowest} m");
    }

    // ---- Obstacles and bounds ----

    /// <summary>A trunk with no other trunk within 10 m of its edge.</summary>
    private static Circle LoneTrunk()
    {
        var all = Ground.Obstacles;
        for (int i = 0; i < Ground.ForestTrunkCount; i++)
        {
            var o = all[i];
            bool alone = true;
            for (int k = 0; k < all.Length; k++)
                if (k != i && Vector2.Distance(o.Center, all[k].Center) < o.Radius + all[k].Radius + 10f)
                    alone = false;
            if (alone && Areas.Forest.Contains(o.X, o.Z, o.Radius + 10f))
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
    public void StaysInsideTheForestAndSlidesAlongItsEdgeAwayFromTheRoads()
    {
        // The east edge, at the end of the west-east path: no road leaves there.
        float limit = Areas.ForestHalfSize - PlayerModule.Radius;
        _player.PlaceAt(new Vector3(96f, 0f, -40f));
        Run(2.0, Moving(1f, 0f)); // east
        Assert.Equal(limit, _player.Position.X, 2);
        Assert.True(Flat(_bus.States[^1].Velocity).Length() < 0.01f);
        // North-east: blocked to the east, slides north.
        float z = _player.Position.Z;
        Run(0.4, Moving(1f, 1f));
        Assert.Equal(limit, _player.Position.X, 2);
        Assert.True(_player.Position.Z < z - 1f);
        Assert.True(_bus.States[^1].Velocity.Z < -3f);
        Assert.Equal(Area.Forest, _bus.States[^1].Area);
    }

    /// <summary>Walks toward (x, z) by pointing the camera at it and pushing forward, for at most <paramref name="seconds"/>.</summary>
    private void WalkTo(float x, float z, double seconds = 20.0)
    {
        for (int i = 0; i < seconds / Step; i++)
        {
            var to = new Vector3(x - _player.Position.X, 0f, z - _player.Position.Z);
            if (to.Length() < 0.2f)
                return;
            TickWith(Moving(0f, 1f, aim: to));
            var p = _player.Position;
            Assert.True(PlayerModule.CanStand(p.X, p.Z), $"stood on unwalkable or blocked ground at ({p.X}, {p.Z})");
            Assert.Equal(Ground.HeightAt(p.X, p.Z), p.Y, 3);
        }
        throw new InvalidOperationException($"did not reach ({x}, {z}), stuck at {_player.Position}");
    }

    /// <summary>Walks a road's center line (NaN-separated in <see cref="Areas.RoadPoints"/>) and returns the areas passed, in order.</summary>
    private List<Area> WalkRoad(int road)
    {
        var points = Areas.RoadPoints;
        int line = 0, first = -1, last = -1;
        for (int i = 0; i < points.Length; i++)
        {
            if (float.IsNaN(points[i].X)) { line++; continue; }
            if (line != road) continue;
            if (first < 0) first = i;
            last = i;
        }
        _player.PlaceAt(new Vector3(points[first].X, 0f, points[first].Y));
        var areas = new List<Area> { Areas.At(_player.Position.X, _player.Position.Z) };
        int seen = _bus.States.Count;
        for (int i = first + 1; i <= last; i++)
        {
            WalkTo(points[i].X, points[i].Y);
            for (; seen < _bus.States.Count; seen++)
                if (_bus.States[seen].Area != areas[^1])
                    areas.Add(_bus.States[seen].Area);
        }
        return areas;
    }

    [Fact]
    public void WalksFromTheForestAlongTheSouthRoadIntoMushroomTown()
    {
        Assert.Equal(new[] { Area.Forest, Area.Road, Area.Town }, WalkRoad(0));
        Assert.Equal(Area.Town, Areas.At(_player.Position.X, _player.Position.Z));
        // And on to the town square's signpost and the clinic.
        WalkTo(Places.SignpostTown.Position.X, Places.SignpostTown.Position.Z - 2f);
        WalkTo(10f, 200f);
        WalkTo(Places.ClinicDoor.Position.X, Places.ClinicDoor.Position.Z);
        Assert.Equal(Area.Town, _bus.States[^1].Area);
    }

    [Fact]
    public void WalksFromTheForestAlongTheNorthRoadIntoPetCity()
    {
        Assert.Equal(new[] { Area.Forest, Area.Road, Area.PetCity }, WalkRoad(1));
        WalkTo(Places.LucySpot.Position.X, Places.LucySpot.Position.Z);
        Assert.Equal(Area.PetCity, _bus.States[^1].Area);
    }

    [Fact]
    public void WalksFromHomeOutOfTownAndIntoTheForest()
    {
        WalkTo(-10f, 200f);
        WalkTo(0f, 160f);
        WalkTo(0f, 150f);
        WalkTo(0f, 104f);
        WalkTo(0f, 90f);
        Assert.Equal(Area.Forest, _bus.States[^1].Area);
    }

    [Fact]
    public void CannotLeaveTheRoadSideways()
    {
        _player.PlaceAt(new Vector3(0f, 0f, 102f)); // the straight first stretch of the south road
        Assert.Equal(Area.Road, Areas.At(0f, 102f));
        Run(2.0, Moving(1f, 0f)); // east, off the road
        Assert.Equal(Areas.RoadHalfWidth - PlayerModule.Radius, _player.Position.X, 2);
        Assert.True(Flat(_bus.States[^1].Velocity).Length() < 0.01f);
        Run(2.0, Moving(-1f, 0f)); // west, off the other side
        Assert.Equal(-(Areas.RoadHalfWidth - PlayerModule.Radius), _player.Position.X, 2);
        Assert.Equal(Area.Road, _bus.States[^1].Area);
    }

    [Fact]
    public void CannotLeaveMushroomTownOverItsEdge()
    {
        _player.PlaceAt(new Vector3(0f, 0f, 282f));
        Run(3.0, Moving(0f, -1f)); // south, out of town
        Assert.Equal(Areas.Town.MaxZ - PlayerModule.Radius, _player.Position.Z, 2);
        Assert.True(Areas.Walkable(_player.Position.X, _player.Position.Z, PlayerModule.Radius - 0.01f));
        Assert.Equal(Area.Town, _bus.States[^1].Area);
        // Nor out of the forest's south edge beside the road gap.
        _player.PlaceAt(new Vector3(-30f, 0f, 96f));
        Run(3.0, Moving(0f, -1f));
        Assert.Equal(Areas.ForestHalfSize - PlayerModule.Radius, _player.Position.Z, 2);
    }

    [Fact]
    public void CannotWalkIntoABuilding()
    {
        var house = Buildings.First(BuildingKind.PlayerHouse); // its door faces east
        var door = Places.HomeDoor.Position;
        for (int i = 0; i < 180; i++)
        {
            TickWith(Moving(-1f, 0f)); // west, straight at the house
            Assert.False(house.Contains(_player.Position.X, _player.Position.Z, PlayerModule.Radius - 0.05f),
                $"walked into the house at {_player.Position}");
        }
        Assert.True(_player.Position.X < door.X - 0.2f, "did not walk up to the wall");
        Assert.True(Flat(_bus.States[^1].Velocity).Length() < 0.1f, "still moving into the wall");
        // The clinic across the square blocks too.
        var clinic = Buildings.First(BuildingKind.Clinic); // its door faces west
        _player.PlaceAt(Places.ClinicDoor.Position);
        Run(3.0, Moving(1f, 0f)); // east, into it
        Assert.False(clinic.Contains(_player.Position.X, _player.Position.Z, PlayerModule.Radius - 0.05f));
        Assert.True(Flat(_bus.States[^1].Velocity).Length() < 0.1f);
    }

    // ---- Teleport ----

    [Fact]
    public void TeleportMovesThereAtOnceWithNoVelocity()
    {
        Run(0.5, Moving(1f, 0f)); // walking
        var to = Places.ClinicDoor;
        _bus.Publish(new TeleportPlayer(to.Position, to.Yaw));
        Assert.Equal(to.Position, _player.Position);
        Assert.Equal(to.Yaw, _player.Facing, 5);
        Assert.Equal(Vector3.Zero, _player.Velocity);
        Assert.True(_player.Grounded);
        TickWith(Idle);
        var s = _bus.States[^1];
        Assert.Equal(to.Position, s.Position); // no sliding
        Assert.Equal(Vector3.Zero, s.Velocity);
        Assert.Equal(Area.Town, s.Area);
        // Walking on starts from there, not from the old spot.
        TickWith(Moving(0f, 1f));
        Assert.True(Vector2.Distance(Flat(to.Position), Flat(_player.Position)) <= PlayerModule.WalkSpeed * Step + 1e-3f);
    }

    [Fact]
    public void TeleportsIntoTheHouseAndAcrossTheWorld()
    {
        _bus.Publish(new TeleportPlayer(Places.HomeInterior.Position, Places.HomeInterior.Yaw));
        TickWith(Idle);
        Assert.Equal(Area.Home, _bus.States[^1].Area);
        Assert.Equal(Areas.HomeFloorY, _bus.States[^1].Position.Y, 3);
        Run(1.0, Moving(0f, 1f)); // walks around the room
        Assert.Equal(Area.Home, _bus.States[^1].Area);
        Assert.True(Flat(_bus.States[^1].Velocity).Length() > 1f);
        var sign = Places.Signpost(Area.PetCity);
        _bus.Publish(new TeleportPlayer(sign.Position + new Vector3(4.5f, 0f, 0f), sign.Yaw));
        TickWith(Idle);
        Assert.Equal(Area.PetCity, _bus.States[^1].Area);
        Assert.Equal(sign.Position.Z, _bus.States[^1].Position.Z, 3);
    }

    [Fact]
    public void TeleportWhileAliveKeepsHealth()
    {
        _bus.Publish(new DamagePlayer(3, Vector3.Zero));
        TickWith(Idle);
        _bus.Publish(new TeleportPlayer(Places.ClinicDoor.Position, 0f));
        TickWith(Idle);
        Assert.Equal(7, _bus.States[^1].Health);
        Assert.True(_bus.States[^1].Alive);
    }

    // ---- Dialogue ----

    [Fact]
    public void StandsStillWhileADialogueIsOpen()
    {
        _player.PlaceAt(Middle);
        _bus.Publish(new DialogueState(true, 3));
        var at = _player.Position;
        Run(1.0, Moving(1f, 1f, jump: true));
        Assert.Equal(at, _player.Position);
        Assert.Equal(Vector3.Zero, _bus.States[^1].Velocity);
        Assert.True(_bus.States[^1].Grounded);
        // A jump pressed while talking is not kept for later.
        _bus.Publish(new DialogueState(false, -1));
        TickWith(Idle);
        Assert.True(_bus.States[^1].Grounded);
        // Closed: walks and jumps again.
        Run(0.5, Moving(0f, 1f));
        Assert.True(Flat(_bus.States[^1].Velocity).Length() > 4f);
        TickWith(Moving(0f, 1f, jump: true));
        Assert.False(_bus.States[^1].Grounded);
    }

    // ---- Damage, defeat and coming back ----

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
    public void StaysDownAfterDefeatUntilATeleportBringsItBackWithFullHealth()
    {
        _player.PlaceAt(Middle);
        _bus.Publish(new DamagePlayer(10, Vector3.Zero));
        TickWith(Idle);
        var fell = _player.Position;
        Run(30.0, Moving(0f, 1f)); // no automatic comeback, however long
        var down = _bus.States[^1];
        Assert.False(down.Alive);
        Assert.Equal(0, down.Health);
        Assert.Equal(fell, down.Position);
        Assert.Single(_bus.Defeats);
        // The next morning at the clinic.
        var clinic = Places.ClinicDoor;
        _bus.Publish(new TeleportPlayer(clinic.Position, clinic.Yaw));
        TickWith(Idle);
        var s = _bus.States[^1];
        Assert.True(s.Alive);
        Assert.Equal(10, s.Health);
        Assert.Equal(clinic.Position, s.Position);
        Assert.Equal(Area.Town, s.Area);
        Assert.True(s.Grounded);
        Assert.Equal(0f, _player.InvulnerableSeconds);
        Assert.Single(_bus.Defeats);
        // It can walk, be hurt and be defeated again.
        Run(0.5, Moving(0f, 1f));
        Assert.True(Flat(_bus.States[^1].Velocity).Length() > 1f);
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
            player.Handle(new DialogueState(i % 200 < 100, 1));
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
