using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Bow;
using Fungtopia.Dialogue;
using Fungtopia.Input;
using Fungtopia.Map;
using Fungtopia.Shell;
using Fungtopia.StartScreen;
using Fungtopia.Wardrobe;
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

    /// <summary>Most tests play after the start screen: "Start!" pressed with the Girl.</summary>
    public PlayerModuleTests()
    {
        _player.Start(_bus);
        PressStart(_bus, GameStart.Girl);
    }

    /// <summary>The tick "Start!" is pressed, as StartScreen publishes it: GameStart, then the screen closed.</summary>
    private static void PressStart(FakeBus bus, int character)
    {
        bus.Publish(new GameStart(character, GameStart.Cat));
        bus.Publish(new StartScreenState(false));
    }

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
        Run(0.5, Moving(0f, 1f));
        Assert.Equal(30, _bus.States.Count);
        Assert.All(_bus.States, s => Assert.Equal(CharacterKind.Girl, s.Character));
        Assert.Equal(_player.Position, _bus.States[^1].Position);
        Assert.Empty(_bus.Defeats);
    }

    // ---- The start screen ----

    /// <summary>A player whose game has not started yet (the start screen is open).</summary>
    private static (FakeBus bus, PlayerModule player) BehindTheStartScreen()
    {
        var bus = new FakeBus();
        var player = new PlayerModule();
        player.Start(bus);
        return (bus, player);
    }

    /// <summary>One tick behind the start screen: StartScreen and Input publish, then the player ticks.</summary>
    private static void TickOpen(FakeBus bus, PlayerModule player, long tick, in PlayerInput input)
    {
        bus.Publish(new StartScreenState(true));
        bus.Publish(input);
        player.Tick(new TickContext(tick, Step));
    }

    [Fact]
    public void IsHiddenOnlyBeforeTheStartScreenOpens()
    {
        var (bus, player) = BehindTheStartScreen();
        Assert.False(player.Visible);
        player.Tick(new TickContext(0, Step));
        Assert.False(player.Visible);
        Assert.False(player.Started);
        // The start screen's first state shows the character; it stays shown while open.
        for (int i = 1; i < 60; i++)
        {
            TickOpen(bus, player, i, Idle);
            Assert.True(player.Visible);
        }
        Assert.False(player.Started);
        // "Start!": still shown, now in play.
        PressStart(bus, GameStart.Girl);
        Assert.True(player.Visible);
        Assert.True(player.Started);
        player.Tick(new TickContext(60, Step));
        Assert.True(player.Visible);
    }

    [Fact]
    public void StandsIdleAtTheHomeDoorFacingTheCameraWhileTheStartScreenIsOpen()
    {
        var (bus, player) = BehindTheStartScreen();
        // Every kind of input and some damage: all ignored while open.
        var input = new PlayerInput(new Vector2(0.6f, 0.8f), new Vector2(0.3f, 0f), true, true, true, Vector3.Normalize(new Vector3(1f, 0f, -1f)));
        for (int i = 0; i < 120; i++)
        {
            bus.Publish(new BowState(0.8f, 0));
            TickOpen(bus, player, i, input);
            player.Handle(new DamagePlayer(3, Vector3.Zero));
            Assert.True(player.Visible);
        }
        Assert.Equal(120, bus.States.Count);
        var house = Buildings.First(BuildingKind.PlayerHouse);
        Assert.All(bus.States, s =>
        {
            Assert.Equal(Places.HomeDoor.Position, s.Position);
            Assert.Equal(Places.HomeDoor.Yaw, s.Facing);
            Assert.Equal(Vector3.Zero, s.Velocity);
            Assert.True(s.Grounded);
            Assert.True(s.Alive);
            Assert.Equal(10, s.Health);
            // Facing out of the house, toward the square.
            AssertDirection(house.Front, FacingDirection(s.Facing));
        });
        // The Camera's front view looks back along the facing (yaw Facing + π, give or take its
        // 15° orbit) from in front of the chest: the facing points from the feet at the camera.
        float cameraYaw = player.Facing + MathF.PI;
        var cameraView = FacingDirection(cameraYaw);
        var cameraAt = Flat(player.Position) - cameraView * 2.5f;
        AssertDirection(cameraAt - Flat(player.Position), FacingDirection(player.Facing));
        AssertDirection(Flat(player.Position) - cameraAt, cameraView);
        // Idle only: no walking, no jump, no aim, the bow on the back, no hit flash.
        Assert.Equal(0f, player.CameraYaw);
        Assert.False(player.HasAim);
        Assert.Equal(0f, player.Archery.Weight);
        Assert.False(ArcheryPose.ArrowVisible(player.Archery));
        Assert.Equal(0f, player.InvulnerableSeconds);
    }

    [Theory]
    [InlineData(CharacterKind.Girl)]
    [InlineData(CharacterKind.Boy)]
    public void WhileTheStartScreenIsOpenItIsTheSettingsCharacterDressedLiveByTheWardrobe(CharacterKind setting)
    {
        // The StartScreen Contract tells the character only at GameStart: until then the setting.
        var (bus, player) = BehindTheStartScreen();
        player.Character = setting;
        TickOpen(bus, player, 0, Idle);
        Assert.Equal(setting, bus.States[^1].Character);
        Assert.False(player.Look.Custom);
        int version = player.AppearanceVersion;
        // The look step sends SetLook / Equip; the Wardrobe publishes the new AppearanceState.
        bus.Publish(Dressed("striped_tee", bottom: "shorts", shoes: "sneakers", skin: 3, hairStyle: 1, hairColor: 2, eyeColor: 4));
        TickOpen(bus, player, 1, Idle);
        Assert.Equal(version + 1, player.AppearanceVersion);
        Assert.Equal(setting, player.Look.Character);
        Assert.Equal(Looks.SkinTones[3], player.Look.SkinColor);
        Assert.Equal(ModelOf("striped_tee"), player.Look.Top);
        bus.Publish(Dressed("overalls_top", bottom: "overalls", shoes: "rain_boots", skin: 1, hairStyle: 1, hairColor: 2, eyeColor: 4));
        TickOpen(bus, player, 2, Idle);
        Assert.Equal(version + 2, player.AppearanceVersion);
        Assert.Equal(Looks.SkinTones[1], player.Look.SkinColor);
        Assert.Equal(ModelOf("overalls"), player.Look.Bottom);
        Assert.True(player.Visible);
    }

    [Fact]
    public void IgnoresInputWhileTheStartScreenIsOpenEvenWhenShown()
    {
        var (bus, player) = BehindTheStartScreen();
        bus.Publish(new GameStart(GameStart.Girl, GameStart.Cat));
        for (int i = 0; i < 30; i++)
            TickOpen(bus, player, i, Moving(0f, 1f, jump: true));
        Assert.Equal(Places.HomeDoor.Position, player.Position);
        Assert.True(player.Grounded);
    }

    [Theory]
    [InlineData(GameStart.Girl, CharacterKind.Girl)]
    [InlineData(GameStart.Boy, CharacterKind.Boy)]
    public void OnGameStartShowsTheChosenCharacterAtTheHomeDoorAndWalks(int chosen, CharacterKind expected)
    {
        var (bus, player) = BehindTheStartScreen();
        // The setting says the other one: the start screen's choice wins.
        player.Character = expected == CharacterKind.Boy ? CharacterKind.Girl : CharacterKind.Boy;
        for (int i = 0; i < 10; i++)
            TickOpen(bus, player, i, Moving(0f, 1f));
        PressStart(bus, chosen);
        Assert.True(player.Visible);
        Assert.Equal(expected, player.Character);
        Assert.Equal(Places.HomeDoor.Position, player.Position);
        Assert.Equal(Places.HomeDoor.Yaw, player.Facing);

        bus.Publish(Moving(0f, 1f));
        player.Tick(new TickContext(10, Step));
        var s = bus.States[^1];
        Assert.Equal(expected, s.Character);
        Assert.True(Vector3.Distance(Places.HomeDoor.Position, s.Position) > 0.05f, "walks after the start");

        // A second GameStart changes nothing.
        bus.Publish(new GameStart(1 - chosen, GameStart.Dog));
        Assert.Equal(expected, player.Character);
        Assert.Equal(s.Position, player.Position);
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

    // ---- Archery pose (the bow from the back into the hands) ----

    private const float Dt = (float)Step;

    private static ArcheryState Pose(ArcheryState s, float draw, double seconds)
    {
        int ticks = (int)Math.Round(seconds / Step);
        for (int i = 0; i < ticks; i++)
            s = ArcheryPose.Advance(s, draw, Dt);
        return s;
    }

    /// <summary>Draws like the Bow module: Draw rises by dt / 0.6 s up to 1 while Fire is held.</summary>
    private static ArcheryState DrawFor(ArcheryState s, double seconds)
    {
        int ticks = (int)Math.Round(seconds / Step);
        for (int i = 1; i <= ticks; i++)
            s = ArcheryPose.Advance(s, Math.Min(1f, i * Dt / 0.6f), Dt);
        return s;
    }

    private static void AssertNear(Vector3 expected, Vector3 actual, float tolerance = 1e-4f) =>
        Assert.True(Vector3.Distance(expected, actual) < tolerance, $"expected {expected}, got {actual}");

    [Theory]
    [InlineData(0f, ArcheryPose.LongAgo, 0f)]   // idle, never shot: bow on the back
    [InlineData(0.01f, ArcheryPose.LongAgo, 1f)] // as soon as Draw > 0
    [InlineData(1f, 0f, 1f)]
    [InlineData(0f, 0f, 1f)]                    // just released: still in hand
    [InlineData(0f, 0.59f, 1f)]
    [InlineData(0f, 0.61f, 0f)]                 // about 0.6 s without drawing: back onto the back
    public void ArcheryTargetWeightComesFromDrawAndTimeSinceRelease(float draw, float sinceRelease, float expected)
    {
        Assert.Equal(expected, ArcheryPose.TargetWeight(draw, sinceRelease));
    }

    [Fact]
    public void IdleAndWalkingKeepTheBowOnTheBack()
    {
        var s = Pose(ArcheryState.Relaxed, 0f, 2.0);
        Assert.Equal(0f, s.Weight);
        Assert.False(ArcheryPose.BowInHand(s));
        Assert.False(ArcheryPose.ArrowVisible(s));
        Assert.Equal(0f, ArcheryPose.StringPull(s));
    }

    [Fact]
    public void DrawingBlendsIntoTheArcheryPoseInAboutAFifteenthOfASecondWithoutPopping()
    {
        var s = ArcheryState.Relaxed;
        float previous = 0f;
        for (int i = 1; i <= 9; i++)
        {
            s = ArcheryPose.Advance(s, Math.Min(1f, i * Dt / 0.6f), Dt);
            Assert.True(s.Weight - previous <= Dt / ArcheryPose.BlendSeconds + 1e-5f, $"tick {i} jumps {s.Weight - previous}");
            Assert.True(s.Weight > previous);
            previous = s.Weight;
        }
        Assert.True(ArcheryPose.BowInHand(s));
        Assert.True(ArcheryPose.ArrowVisible(s));
        Assert.Equal(1f, s.Weight, 3); // 0.15 s
        Assert.Equal(0f, ArcheryPose.Eased(0f));
        Assert.Equal(1f, ArcheryPose.Eased(1f));
        Assert.True(ArcheryPose.Eased(0.1f) < 0.1f);
    }

    [Fact]
    public void TheStringIsPulledBackByTheDrawWithTheRightHandAndArrowOnIt()
    {
        var s = DrawFor(ArcheryState.Relaxed, 0.3);
        Assert.Equal(0.5f, s.Draw, 3);
        Assert.Equal(0.5f * BowGeometry.FullDrawPull, ArcheryPose.StringPull(s), 4);
        var aim = ArcheryPose.BowForward(ArcheryPose.AimLocal(0.2f, 0.1f));
        var grip = ArcheryPose.HoldLocal;
        var hand = ArcheryPose.RightHand(s, grip, aim);
        AssertNear(ArcheryPose.Nock(grip, aim, ArcheryPose.StringPull(s)), hand);
        // Straight behind the bow along its forward, the string's rest plus the pull.
        AssertNear(grip - aim * (ArcheryPose.StringRest + 0.5f * BowGeometry.FullDrawPull), hand);

        s = DrawFor(ArcheryState.Relaxed, 1.0);
        Assert.Equal(1f, s.Draw);
        AssertNear(grip - aim * (ArcheryPose.StringRest + BowGeometry.FullDrawPull), ArcheryPose.RightHand(s, grip, aim));
        Assert.True(ArcheryPose.ArrowVisible(s));
    }

    [Fact]
    public void ASlightSquashPulsesAsTheDrawReachesFull()
    {
        var s = DrawFor(ArcheryState.Relaxed, 0.55);
        Assert.Equal(0f, ArcheryPose.FullDrawSquash(s));
        s = DrawFor(ArcheryState.Relaxed, 0.6 + 0.1);
        float squash = ArcheryPose.FullDrawSquash(s);
        Assert.True(squash > 0.5f && squash <= 1f, $"squash {squash}");
        s = Pose(s, 1f, 0.3);
        Assert.Equal(0f, ArcheryPose.FullDrawSquash(s)); // holding at full stays unsquashed
        Assert.Equal(1f, s.Weight);
    }

    [Fact]
    public void OnReleaseTheStringSnapsTheArrowIsGoneAndTheHandFollowsThrough()
    {
        var drawn = DrawFor(ArcheryState.Relaxed, 1.0);
        var s = ArcheryPose.Advance(drawn, 0f, Dt);
        Assert.Equal(0f, s.SecondsSinceRelease);
        Assert.Equal(1f, s.ReleasedDraw);
        Assert.False(ArcheryPose.ArrowVisible(s));
        Assert.True(ArcheryPose.BowInHand(s));
        Assert.Equal(0f, ArcheryPose.FollowThrough(s));

        // The string flies forward past rest (a negative pull) within a few ticks, then settles.
        float least = float.MaxValue;
        var t = s;
        for (int i = 0; i < 12; i++)
        {
            t = ArcheryPose.Advance(t, 0f, Dt);
            least = Math.Min(least, ArcheryPose.StringPull(t));
        }
        Assert.True(least < 0f && least > -0.1f, $"least pull {least}");
        Assert.True(ArcheryPose.StringPull(Pose(s, 0f, 0.1)) < BowGeometry.FullDrawPull * 0.1f);
        Assert.Equal(0f, ArcheryPose.StringPull(Pose(s, 0f, 0.35)));

        // The right hand follows through, back and outward: away from the head, on the hold point's side.
        var after = Pose(s, 0f, 0.2);
        Assert.Equal(1f, ArcheryPose.FollowThrough(after));
        var aim = ArcheryPose.BowForward(ArcheryPose.AimLocal(0f, 0f));
        var grip = ArcheryPose.HoldLocal;
        var atRelease = ArcheryPose.RightHand(drawn, grip, aim);
        var followed = ArcheryPose.RightHand(after, grip, aim);
        Assert.True(Vector3.Dot(followed - atRelease, -aim) > 0.1f); // back
        Assert.True(Vector3.Dot(followed - atRelease, ArcheryPose.HoldSide * Vector3.Cross(aim, Vector3.UnitY)) > 0.05f); // outward
        Assert.True(MathF.Abs(followed.X) > MathF.Abs(atRelease.X));
    }

    [Fact]
    public void TheHeldBowPointsALittleOffTheAimAwayFromTheHeadSoTheStringHandIsBesideIt()
    {
        // Whichever side of the head BowGeometry's hold point is on.
        Assert.Equal(BowGeometry.HoldRight < 0f ? -1f : 1f, ArcheryPose.HoldSide);
        var aim = ArcheryPose.AimLocal(0.1f, 0.3f);
        var forward = ArcheryPose.BowForward(aim);
        Assert.Equal(1f, forward.Length(), 4);
        var (yaw, pitch) = ArcheryPose.AimAngles(forward, true, 0f);
        // Turned to the left (positive yaw) for a hold point right of the head, to the right for one left of it.
        Assert.Equal(0.1f + ArcheryPose.HoldSide * ArcheryPose.BowSkew, yaw, 4);
        Assert.Equal(0.3f, pitch, 4);
        // The head tilts away from the hold point's side (positive roll is to the left).
        Assert.Equal(ArcheryPose.HoldSide * ArcheryPose.HeadTilt, ArcheryPose.HeadRoll);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.7f)]
    [InlineData(0.3f)]
    [InlineData(-0.3f)]
    [InlineData(-0.6f)]
    public void TheStringHandStaysOutsideTheHeadOnTheHoldPointsSide(float aimPitch)
    {
        // Aiming along the facing (the feet turn toward the aim while drawing), over the whole pitch range.
        const float aimYaw = 0f;
        // The head as characters.py builds it: a 0.4 m ball, its middle 0.368 m above the neck at
        // 0.72 m, tilted by HeadRoll about the neck (positive to the left).
        const float neck = 0.72f, headRadius = 0.4f;
        float roll = ArcheryPose.HeadRoll;
        var head = new Vector3(-MathF.Sin(roll) * 0.368f, neck + MathF.Cos(roll) * 0.368f, 0f);
        var grip = ArcheryPose.HoldLocal;
        var forward = ArcheryPose.BowForward(ArcheryPose.AimLocal(aimYaw, aimPitch));
        var drawn = DrawFor(ArcheryState.Relaxed, 1.0);
        var after = Pose(ArcheryPose.Advance(drawn, 0f, Dt), 0f, 0.2);
        foreach (var hand in new[]
        {
            ArcheryPose.RightHand(DrawFor(ArcheryState.Relaxed, 0.3), grip, forward),
            ArcheryPose.RightHand(drawn, grip, forward),
            ArcheryPose.RightHand(after, grip, forward),
        })
        {
            // On the hold point's side of the head's middle, further out than the bow hand...
            Assert.True(ArcheryPose.HoldSide * hand.X > ArcheryPose.HoldSide * grip.X, $"hand {hand}, grip {grip}");
            // ...behind the bow, and not in the head.
            Assert.True(Vector3.Dot(hand - grip, forward) < -ArcheryPose.StringRest + 1e-4f, $"hand {hand}");
            Assert.True(Vector3.Distance(hand, head) > headRadius, $"hand {hand} in the head at {head}");
        }
        // The bow hand reaches forward from the body, not into the head either.
        Assert.True(grip.Z < -0.2f, $"grip {grip}");
        Assert.True(Vector3.Distance(grip, head) > headRadius, $"grip {grip}");
    }

    [Fact]
    public void AboutSixTenthsOfASecondAfterReleaseTheBowSwingsBackOntoTheBack()
    {
        var s = ArcheryPose.Advance(DrawFor(ArcheryState.Relaxed, 1.0), 0f, Dt);
        s = Pose(s, 0f, 0.55);
        Assert.Equal(1f, s.Weight);
        float previous = s.Weight;
        for (int i = 0; i < 6; i++)
        {
            s = ArcheryPose.Advance(s, 0f, Dt);
            Assert.True(previous - s.Weight <= Dt / ArcheryPose.BlendSeconds + 1e-5f);
            previous = s.Weight;
        }
        Assert.True(s.Weight < 1f && s.Weight > 0f, $"weight {s.Weight} at 0.65 s");
        s = Pose(s, 0f, 0.15);
        Assert.Equal(0f, s.Weight);
        Assert.False(ArcheryPose.BowInHand(s));
    }

    [Fact]
    public void DrawingAgainSoonAfterAShotKeepsTheBowInHand()
    {
        var s = ArcheryPose.Advance(DrawFor(ArcheryState.Relaxed, 1.0), 0f, Dt);
        s = Pose(s, 0f, 0.3);
        s = DrawFor(s, 0.2);
        Assert.Equal(1f, s.Weight);
        Assert.True(ArcheryPose.ArrowVisible(s));
        Assert.Equal(0f, ArcheryPose.FollowThrough(s));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1.3f)]
    [InlineData(-2.4f)]
    [InlineData(3.1f)]
    public void TheBowHandIsAtBowGeometrysHoldPointForAnyFacing(float facing)
    {
        var feet = new Vector3(3f, 0.4f, -7f);
        var back = new Vector3(0f, 0.55f, 0.25f);
        var grip = ArcheryPose.BowGrip(1f, back);
        Assert.Equal(ArcheryPose.HoldLocal, grip);
        AssertNear(BowGeometry.HoldPoint(feet, facing), ArcheryPose.ToWorld(feet, facing, grip));
        // The model frame's -Z is the facing and +X its right, as BowGeometry has them.
        AssertNear(feet + new Vector3(-MathF.Sin(facing), 0f, -MathF.Cos(facing)), ArcheryPose.ToWorld(feet, facing, -Vector3.UnitZ));
        AssertNear(feet + new Vector3(MathF.Cos(facing), 0f, -MathF.Sin(facing)), ArcheryPose.ToWorld(feet, facing, Vector3.UnitX));
    }

    [Fact]
    public void TheBowMovesSmoothlyFromTheBackToTheHoldPoint()
    {
        var back = new Vector3(0f, 0.55f, 0.25f);
        AssertNear(back, ArcheryPose.BowGrip(0f, back));
        var previous = back;
        for (int i = 1; i <= 100; i++)
        {
            var p = ArcheryPose.BowGrip(i / 100f, back);
            Assert.True(Vector3.Distance(previous, p) < 0.04f, $"jump at {i}");
            previous = p;
        }
        AssertNear(ArcheryPose.HoldLocal, previous);
        // It swings over the left shoulder, not through the head.
        Assert.True(ArcheryPose.BowGrip(0.5f, back).X < -0.1f);
    }

    [Fact]
    public void AimPitchAndYawAreClamped()
    {
        float facing = 0.8f;
        var forward = new Vector3(-MathF.Sin(facing), 0f, -MathF.Cos(facing));
        var (yaw, pitch) = ArcheryPose.AimAngles(Vector3.Normalize(forward + Vector3.UnitY * 0.3f), true, facing);
        Assert.Equal(0f, yaw, 4);
        Assert.Equal(MathF.Atan(0.3f), pitch, 4);
        // The local aim turned by the facing is the world aim.
        AssertNear(Vector3.Normalize(forward + Vector3.UnitY * 0.3f), ArcheryPose.ToWorld(Vector3.Zero, facing, ArcheryPose.AimLocal(yaw, pitch)));

        Assert.Equal(ArcheryPose.MaxAimPitch, ArcheryPose.AimAngles(Vector3.Normalize(forward + Vector3.UnitY * 5f), true, facing).Pitch);
        Assert.Equal(ArcheryPose.MaxAimPitch, ArcheryPose.AimAngles(Vector3.UnitY, true, facing).Pitch);
        Assert.Equal(ArcheryPose.MinAimPitch, ArcheryPose.AimAngles(Vector3.Normalize(forward - Vector3.UnitY * 5f), true, facing).Pitch);
        Assert.Equal(ArcheryPose.MinAimPitch, ArcheryPose.AimAngles(-Vector3.UnitY, true, facing).Pitch);

        // To the left (positive yaw) and far behind: clamped to the side the feet are turning to.
        var left = new Vector3(-MathF.Sin(facing + 0.3f), 0f, -MathF.Cos(facing + 0.3f));
        Assert.Equal(0.3f, ArcheryPose.AimAngles(left, true, facing).Yaw, 4);
        var farRight = new Vector3(-MathF.Sin(facing - 2f), 0f, -MathF.Cos(facing - 2f));
        Assert.Equal(-ArcheryPose.MaxAimYaw, ArcheryPose.AimAngles(farRight, true, facing).Yaw);

        Assert.Equal((0f, 0f), ArcheryPose.AimAngles(Vector3.UnitY, false, facing));
        Assert.Equal((0f, 0f), ArcheryPose.AimAngles(new Vector3(float.NaN, 0f, 1f), true, facing));
        AssertNear(-Vector3.UnitZ, ArcheryPose.AimLocal(0f, 0f));
        Assert.Equal(1f, ArcheryPose.AimLocal(0.5f, 0.4f).Length(), 4);
    }

    [Fact]
    public void ThePlayerAdvancesItsArcheryPoseFromTheBowStateAndKeepsTheAim()
    {
        var aim = Vector3.Normalize(new Vector3(0.2f, 0.3f, -1f));
        Assert.Equal(0f, _player.Archery.Weight);
        Assert.False(ArcheryPose.BowInHand(_player.Archery));
        for (int i = 1; i <= 60; i++)
        {
            _bus.Publish(new BowState(Math.Min(1f, i * Dt / 0.6f), 0));
            TickWith(Moving(0f, 1f, fire: true, aim: aim));
        }
        Assert.Equal(1f, _player.Archery.Draw);
        Assert.Equal(1f, _player.Archery.Weight);
        Assert.True(_player.HasAim);
        AssertNear(aim, _player.AimDirection);
        // Walking on while drawing.
        Assert.True(Flat(_player.Velocity).Length() > 4f);

        _bus.Publish(new BowState(0f, 1));
        TickWith(Moving(0f, 1f, aim: aim));
        Assert.Equal(0f, _player.Archery.SecondsSinceRelease);
        Assert.Equal(1f, _player.Archery.ReleasedDraw);
        Assert.False(ArcheryPose.ArrowVisible(_player.Archery));
        for (int i = 0; i < 50; i++)
        {
            _bus.Publish(new BowState(0f, 1));
            TickWith(Idle);
        }
        Assert.Equal(0f, _player.Archery.Weight);
    }

    // ---- Performance ----

    // ---- Look (the character drawn from the Wardrobe's AppearanceState) ----

    private static AppearanceState Dressed(string top = "", string bottom = "", string shoes = "", string head = "",
        string face = "", string back = "", int skin = 0, int hairStyle = 0, int hairColor = 0, int eyeColor = 0) =>
        new(skin, hairStyle, hairColor, eyeColor, top, bottom, shoes, head, face, back);

    private static string ModelOf(string id)
    {
        Assert.True(Catalog.TryGet(id, out var item), id);
        return item.ModelPath;
    }

    [Theory]
    [InlineData(CharacterKind.Girl)]
    [InlineData(CharacterKind.Boy)]
    public void WithoutAnAppearanceTheCharacterKeepsItsBuiltInLookAndOutfit(CharacterKind kind)
    {
        var look = CharacterLook.Default(kind);
        Assert.False(look.Custom);
        Assert.Equal("", look.HairModel);
        foreach (Slot slot in Enum.GetValues<Slot>())
            Assert.Equal("", look.Model(slot));
        Assert.Equal(Rig.BuiltinSkin, look.SkinColor);
        Assert.Equal(Rig.BuiltinEye, look.EyeColor);
        Assert.Equal(Rig.BuiltinHair(kind), look.HairColor);
        Assert.True(look.Shows(BuiltinPieces.Hair | BuiltinPieces.Top | BuiltinPieces.Shoes));
        // The Girl's ribbon bow and dress (no trousers); the Boy's shirt and trousers (no ribbon).
        Assert.Equal(kind == CharacterKind.Girl, look.Shows(BuiltinPieces.Head));
        Assert.Equal(kind == CharacterKind.Boy, look.Shows(BuiltinPieces.Bottom));
    }

    [Fact]
    public void SkinHairAndEyeColorsAreTheChosenLooks()
    {
        var look = CharacterLook.From(Dressed(skin: 5, hairStyle: 4, hairColor: 6, eyeColor: 2), CharacterKind.Boy);
        Assert.True(look.Custom);
        Assert.Equal(Looks.SkinTones[5], look.SkinColor);
        Assert.Equal(Looks.HairColors[6], look.HairColor);
        Assert.Equal(Looks.EyeColors[2], look.EyeColor);
        // The chosen hair style's model replaces the built-in hair.
        Assert.Equal(Looks.HairStyles[4].ModelPath, look.HairModel);
        Assert.Equal("res://modules/Wardrobe/Models/Hair/top_bun.glb", look.HairModel);
        Assert.False(look.Shows(BuiltinPieces.Hair));
    }

    [Theory]
    [InlineData(CharacterKind.Girl)]
    [InlineData(CharacterKind.Boy)]
    public void NothingWornShowsTheOldOutfitWithTheChosenColorsAndHair(CharacterKind kind)
    {
        var look = CharacterLook.From(Dressed(skin: 3, hairStyle: 1), kind);
        foreach (Slot slot in Enum.GetValues<Slot>())
            Assert.Equal("", look.Model(slot));
        Assert.Equal(Rig.Pieces(kind) & ~BuiltinPieces.Hair, look.Builtin);
        Assert.Equal(Looks.HairStyles[1].ModelPath, look.HairModel);
    }

    [Fact]
    public void EachWornItemsModelGoesInItsSlotAndReplacesThatBuiltInPiece()
    {
        var look = CharacterLook.From(Dressed("striped_tee", "jeans", "sneakers", "mushroom_hat", "round_glasses",
            "mushroom_backpack"), CharacterKind.Boy);
        Assert.Equal(ModelOf("striped_tee"), look.Top);
        Assert.Equal("res://modules/Wardrobe/Models/Clothes/striped_tee.glb", look.Model(Slot.Top));
        Assert.Equal(ModelOf("jeans"), look.Model(Slot.Bottom));
        Assert.Equal(ModelOf("sneakers"), look.Model(Slot.Shoes));
        Assert.Equal(ModelOf("mushroom_hat"), look.Model(Slot.Head));
        Assert.Equal(ModelOf("round_glasses"), look.Model(Slot.Face));
        Assert.Equal(ModelOf("mushroom_backpack"), look.Model(Slot.Back));
        Assert.Equal(BuiltinPieces.None, look.Builtin);
    }

    [Fact]
    public void AnEmptySlotKeepsItsBuiltInPieceAlone()
    {
        // Only shoes worn: the Boy keeps his shirt and trousers, the Girl her dress and ribbon.
        var boy = CharacterLook.From(Dressed(shoes: "rain_boots"), CharacterKind.Boy);
        Assert.Equal(BuiltinPieces.Top | BuiltinPieces.Bottom, boy.Builtin);
        var girl = CharacterLook.From(Dressed(head: "beanie"), CharacterKind.Girl);
        Assert.Equal(BuiltinPieces.Top | BuiltinPieces.Shoes, girl.Builtin);
        Assert.Equal(ModelOf("beanie"), girl.Head);
        // A top over the Boy's built-in trousers.
        var tee = CharacterLook.From(Dressed("sweater_vest"), CharacterKind.Boy);
        Assert.Equal(BuiltinPieces.Bottom | BuiltinPieces.Shoes, tee.Builtin);
    }

    [Theory]
    [InlineData(CharacterKind.Girl, "spotted_dress")]
    [InlineData(CharacterKind.Boy, "party_dress")]
    public void ADressReplacesTheBottom(CharacterKind kind, string dress)
    {
        // Even when a bottom is (wrongly) still listed with it, and with no bottom: no built-in trousers.
        var look = CharacterLook.From(Dressed(dress, "jeans", "mary_janes"), kind);
        Assert.Equal(ModelOf(dress), look.Top);
        Assert.Equal("", look.Bottom);
        Assert.False(look.Shows(BuiltinPieces.Bottom));
        Assert.False(look.Shows(BuiltinPieces.Top));
        Assert.False(CharacterLook.From(Dressed(dress), kind).Shows(BuiltinPieces.Bottom));
        // Not a dress: the bottom shows.
        Assert.Equal(ModelOf("jeans"), CharacterLook.From(Dressed("striped_tee", "jeans"), kind).Bottom);
    }

    [Fact]
    public void ABottomUnderTheGirlsBuiltInDressStillShows()
    {
        var look = CharacterLook.From(Dressed(bottom: "jeans"), CharacterKind.Girl);
        Assert.Equal(ModelOf("jeans"), look.Bottom);
        Assert.True(look.Shows(BuiltinPieces.Top));
    }

    [Fact]
    public void UnknownWrongSlotAndUnfittingItemsAreNotWorn()
    {
        // "flower_dress" fits only the Girl; sneakers are not a top; "cape" is not in the catalog.
        var boy = CharacterLook.From(Dressed("flower_dress", "skirt", "cape", head: "sneakers"), CharacterKind.Boy);
        Assert.Equal("", boy.Top);
        Assert.Equal("", boy.Bottom);
        Assert.Equal("", boy.Shoes);
        Assert.Equal("", boy.Head);
        Assert.Equal(Rig.Pieces(CharacterKind.Boy) & ~BuiltinPieces.Hair, boy.Builtin);
        var girl = CharacterLook.From(Dressed("flower_dress", "skirt"), CharacterKind.Girl);
        Assert.Equal(ModelOf("flower_dress"), girl.Top);
        Assert.Equal("", girl.Bottom); // under the dress
        Assert.Equal(ModelOf("skirt"), CharacterLook.From(Dressed("striped_tee", "skirt"), CharacterKind.Girl).Bottom);
    }

    [Fact]
    public void OutOfRangeLooksKeepTheBuiltInColorsAndHair()
    {
        var look = CharacterLook.From(new AppearanceState(-1, 99, 99, 6, "", "", "", "", "", ""), CharacterKind.Girl);
        Assert.Equal(Rig.BuiltinSkin, look.SkinColor);
        Assert.Equal(Rig.BuiltinHair(CharacterKind.Girl), look.HairColor);
        Assert.Equal(Rig.BuiltinEye, look.EyeColor);
        Assert.Equal("", look.HairModel);
        Assert.True(look.Shows(BuiltinPieces.Hair));
    }

    [Fact]
    public void WornModelsAttachToTheRigsHeadBodyAndLegParts()
    {
        foreach (var part in new[] { "Head", "Body", "LegL", "LegR" })
            Assert.True(Rig.IsAttachPoint(part), part);
        foreach (var other in new[] { "ArmL", "HandR", "BackBow", "Hair", "" })
            Assert.False(Rig.IsAttachPoint(other), other);
        Assert.Equal("DefaultTop", Rig.NodePrefix(BuiltinPieces.Top));
        Assert.Equal("Hair", Rig.NodePrefix(BuiltinPieces.Hair));
    }

    [Fact]
    public void ThePlayerFollowsTheLatestAppearanceForItsCharacter()
    {
        Assert.False(_player.HasAppearance);
        Assert.False(_player.Look.Custom);
        int version = _player.AppearanceVersion;
        var look = Dressed("party_dress", shoes: "sneakers", skin: 4, hairStyle: 2, hairColor: 5, eyeColor: 3);
        _bus.Publish(look);
        TickWith(Idle);
        Assert.True(_player.HasAppearance);
        Assert.Equal(version + 1, _player.AppearanceVersion);
        Assert.Equal(ModelOf("party_dress"), _player.Look.Top);
        Assert.Equal(Looks.SkinTones[4], _player.Look.SkinColor);
        Assert.Equal(CharacterKind.Girl, _player.Look.Character);
        // The same look every tick is not a change; a new one applies at once.
        _bus.Publish(look);
        TickWith(Idle);
        Assert.Equal(version + 1, _player.AppearanceVersion);
        _bus.Publish(Dressed("party_dress", shoes: "rain_boots", skin: 4, hairStyle: 2, hairColor: 5, eyeColor: 3));
        Assert.Equal(version + 2, _player.AppearanceVersion);
        Assert.Equal(ModelOf("rain_boots"), _player.Look.Shoes);
    }

    [Fact]
    public void TheLookIsOnTheCharacterTheStartScreenPicked()
    {
        var (bus, player) = BehindTheStartScreen();
        bus.Publish(Dressed(bottom: "jeans"));
        PressStart(bus, GameStart.Boy);
        player.Tick(new TickContext(0, Step));
        Assert.Equal(CharacterKind.Boy, player.Look.Character);
        Assert.True(player.Look.Shows(BuiltinPieces.Top));
        Assert.False(player.Look.Shows(BuiltinPieces.Bottom));
        Assert.Equal(ModelOf("jeans"), player.Look.Bottom);
    }

    [Fact]
    public void TicksWithoutAllocating()
    {
        var bus = new NullBus();
        var player = new PlayerModule();
        player.Start(bus);
        player.Handle(new GameStart(GameStart.Girl, GameStart.Cat));
        var input = Moving(0.6f, 0.8f, fire: true, aim: new Vector3(0.3f, 0f, -1f), jump: true);
        var look = new AppearanceState(2, 4, 5, 2, "party_dress", "", "sneakers", "mushroom_hat", "round_glasses", "little_backpack");
        var otherLook = new AppearanceState(1, 0, 0, 0, "striped_tee", "jeans", "", "", "", "");
        player.Handle(input);
        player.Handle(look);
        _ = player.Look;
        player.Handle(otherLook);
        _ = player.Look;
        player.Tick(new TickContext(0, Step)); // warm up
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 600; i++)
        {
            player.Handle(input);
            player.Handle(new DamagePlayer(1, Vector3.Zero));
            player.Handle(new DialogueState(i % 200 < 100, 1));
            player.Handle(new BowState(i % 60 < 40 ? (i % 60) / 36f : 0f, 0));
            player.Handle(new StartScreenState(false));
            player.Handle(i % 120 < 60 ? look : otherLook);
            _ = player.Look;
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
