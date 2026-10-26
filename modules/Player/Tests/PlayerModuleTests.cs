using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Fungtopia.Bow;
using Fungtopia.Dialogue;
using Fungtopia.Input;
using Fungtopia.Map;
using Fungtopia.Save;
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
        public readonly List<SavePart> Parts = new();
        /// <summary>Every message published, of any type.</summary>
        public readonly List<object> All = new();

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
                _handlers[typeof(T)] = list = new List<object>();
            list.Add(handler);
        }

        public void Publish<T>(in T message) where T : struct
        {
            All.Add(message);
            if (message is SavePart part)
                Parts.Add(part);
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

    // ---- The wardrobe and the shop ----

    /// <summary>One tick with a wardrobe screen open, as published in some order, then the player ticks.</summary>
    private void TickWardrobe(in PlayerInput input, bool shop = false, float? previewYaw = null, bool stateFirst = true)
    {
        if (stateFirst)
            _bus.Publish(new WardrobeState(true, shop));
        if (previewYaw.HasValue)
            _bus.Publish(new PreviewYaw(previewYaw.Value));
        _bus.Publish(new BowState(1f, 0));
        _bus.Publish(input);
        if (!stateFirst)
            _bus.Publish(new WardrobeState(true, shop));
        _player.Tick(new TickContext(_tick++, Step));
    }

    private void CloseWardrobe() => _bus.Publish(new WardrobeState(false, false));

    /// <summary>Every kind of input at once: move, look, jump, fire with an aim.</summary>
    private static PlayerInput Everything(float x, float y) =>
        new(new Vector2(x, y), new Vector2(0.3f, 0.1f), true, true, true, Vector3.Normalize(new Vector3(1f, -0.2f, -1f)));

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void StandsIdleWhateverTheInputWhileTheWardrobeOrTheShopIsOpen(bool shop, bool stateFirst)
    {
        _player.PlaceAt(Middle, 0.5f);
        // Walking, drawing the bow and in the air when the screen opens.
        Run(0.3, Moving(1f, 0f));
        _bus.Publish(new BowState(1f, 0));
        TickWith(Moving(0f, 1f, fire: true, aim: new Vector3(0f, 0f, -1f), jump: true));
        Assert.False(_bus.States[^1].Grounded);
        Assert.True(_player.Archery.Weight > 0f);
        var at = _player.Position;
        float facing = _player.Facing;
        float cameraYaw = _player.CameraYaw;
        int states = _bus.States.Count;

        float[] directions = { 0f, 0.5f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f, 4.5f, 5f, 5.5f };
        foreach (float a in directions)
        {
            for (int i = 0; i < 20; i++)
            {
                TickWardrobe(Everything(MathF.Cos(a), MathF.Sin(a)), shop, stateFirst: stateFirst);
                var state = _bus.States[^1];
                // No walking, no jump: on the ground below where it was, standing still.
                Assert.Equal(at.X, state.Position.X);
                Assert.Equal(at.Z, state.Position.Z);
                Assert.Equal(Ground.HeightAt(at.X, at.Z), state.Position.Y);
                Assert.Equal(Vector3.Zero, state.Velocity);
                Assert.True(state.Grounded);
                // No turning toward the move or the aim, no camera turn from Look.
                Assert.Equal(facing, state.Facing);
                Assert.Equal(cameraYaw, _player.CameraYaw);
                // No drawing the bow, no aim: the idle pose only.
                Assert.False(_player.HasAim);
                Assert.Equal(ArcheryState.Relaxed, _player.Archery);
                Assert.False(ArcheryPose.ArrowVisible(_player.Archery));
            }
        }
        Assert.Equal(states + directions.Length * 20, _bus.States.Count);
        Assert.True(_player.Visible);
    }

    [Fact]
    public void TurnsTheCharacterSmoothlyByThePreviewYawWhileTheWardrobeIsOpen()
    {
        _player.PlaceAt(Middle, 1f);
        float facing = _player.Facing;
        // Opening faces the camera: the front view looks back along Facing.
        TickWardrobe(Idle, previewYaw: 0f);
        Assert.Equal(0f, _player.PreviewTurn);
        Assert.Equal(facing, _player.ShownFacing, 5);

        // Turned to the screen's right by 1 rad: the character eases there, no pop, no overshoot.
        float last = 0f;
        for (int i = 0; i < 60; i++)
        {
            TickWardrobe(Idle, previewYaw: 1f);
            float turn = _player.PreviewTurn;
            Assert.True(turn > last || turn == 1f, $"tick {i}: {turn} after {last}");
            Assert.True(turn <= 1f + 1e-5f);
            Assert.True(turn - last < 0.25f, $"tick {i} jumped {turn - last}");
            if (i == 0)
                Assert.True(turn < 0.5f, "eases, does not snap");
            last = turn;
        }
        Assert.Equal(1f, _player.PreviewTurn, 3);
        // Positive yaw turns left (counterclockwise from above): the front goes to the right of
        // the camera looking back at the character (camera yaw Facing + π).
        Assert.Equal(PlayerModule.WrapAngle(facing + 1f), _player.ShownFacing, 3);
        var cameraRight = new Vector2(MathF.Cos(facing + MathF.PI), -MathF.Sin(facing + MathF.PI));
        Assert.True(Vector2.Dot(FacingDirection(_player.ShownFacing), cameraRight) > 0.8f);
        // The facing itself (what the camera frames) does not turn, and the player stays put.
        Assert.Equal(facing, _player.Facing);
        Assert.Equal(facing, _bus.States[^1].Facing);

        // The latest PreviewYaw counts; it follows the short way round across ±π.
        Run(1.0, Idle); // no PreviewYaw this second: the latest one (1) still holds
        Assert.Equal(1f, _player.PreviewTurn, 3);
        for (int i = 0; i < 60; i++)
            TickWardrobe(Idle, previewYaw: 3f);
        for (int i = 0; i < 60; i++)
        {
            float before = _player.PreviewTurn;
            TickWardrobe(Idle, previewYaw: -3f);
            Assert.True(MathF.Abs(PlayerModule.WrapAngle(_player.PreviewTurn - before)) < 0.15f);
        }
        Assert.Equal(-3f, _player.PreviewTurn, 3);
        Assert.Equal(PlayerModule.WrapAngle(facing - 3f), _player.ShownFacing, 3);
        Assert.Equal(facing, _player.Facing);
    }

    [Fact]
    public void TurnsByThePreviewYawOnTheStartScreenToo()
    {
        var (bus, player) = BehindTheStartScreen();
        for (int i = 0; i < 60; i++)
        {
            bus.Publish(new PreviewYaw(-1.5f));
            TickOpen(bus, player, i, Everything(0f, 1f));
        }
        Assert.Equal(-1.5f, player.PreviewTurn, 3);
        Assert.Equal(PlayerModule.WrapAngle(Places.HomeDoor.Yaw - 1.5f), player.ShownFacing, 3);
        Assert.Equal(Places.HomeDoor.Yaw, player.Facing);
        Assert.Equal(Places.HomeDoor.Position, player.Position);
    }

    [Fact]
    public void ControlComesBackAsBeforeOnceTheWardrobeCloses()
    {
        _player.PlaceAt(Middle, -0.7f);
        var at = _player.Position;
        float facing = _player.Facing;
        for (int i = 0; i < 60; i++)
            TickWardrobe(Everything(0f, 1f), previewYaw: 2f);
        Assert.Equal(2f, _player.PreviewTurn, 3);
        CloseWardrobe();
        // Closed: facing as before opening, the turn eases back to it.
        TickWith(Idle);
        Assert.Equal(facing, _player.Facing);
        Assert.True(_player.PreviewTurn < 2f && _player.PreviewTurn > 1f, "eases back, no pop");
        Run(1.0, Idle);
        Assert.Equal(0f, _player.PreviewTurn, 3);
        Assert.Equal(facing, _player.ShownFacing, 3);
        Assert.Equal(at, _player.Position);
        // Walks, turns toward the move, jumps and draws the bow again.
        Run(0.5, Moving(1f, 0f));
        Assert.True(Flat(_bus.States[^1].Velocity).Length() > 4f);
        AssertDirection(new Vector2(1f, 0f), FacingDirection(_player.Facing));
        TickWith(Moving(0f, 1f, jump: true));
        Assert.False(_bus.States[^1].Grounded);
        _bus.Publish(new BowState(1f, 0));
        TickWith(Moving(0f, 0f, fire: true, aim: new Vector3(0f, 0f, -1f)));
        Assert.True(_player.Archery.Weight > 0f);
        Assert.True(_player.HasAim);

        // The next screen starts facing the camera again, whatever the last one's turn was.
        Run(1.0, Idle);
        TickWardrobe(Idle);
        Assert.Equal(0f, _player.PreviewTurn, 3);
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

    // ---- Healing ----

    [Fact]
    public void HealingRaisesHealthAndCapsAtMax()
    {
        _bus.Publish(new DamagePlayer(5, Vector3.Zero));
        TickWith(Idle);
        Assert.False(_bus.States[^1].FullHealth);
        _bus.Publish(new HealPlayer(1)); // a Berry Muffin
        TickWith(Idle);
        Assert.Equal(6, _bus.States[^1].Health);
        Assert.False(_bus.States[^1].FullHealth);
        _bus.Publish(new HealPlayer(3)); // a Mushroom Soup
        TickWith(Idle);
        Assert.Equal(9, _bus.States[^1].Health);
        _bus.Publish(new HealPlayer(3)); // more than is missing: stops at max
        TickWith(Idle);
        Assert.Equal(10, _bus.States[^1].Health);
        Assert.Equal(_bus.States[^1].MaxHealth, _bus.States[^1].Health);
        Assert.True(_bus.States[^1].FullHealth);
        _bus.Publish(new HealPlayer(5)); // already full: stays at max
        TickWith(Idle);
        Assert.Equal(10, _bus.States[^1].Health);
        Assert.True(_bus.States[^1].FullHealth);
    }

    [Fact]
    public void FullHealingFillsUpToMax()
    {
        _bus.Publish(new DamagePlayer(8, Vector3.Zero));
        TickWith(Idle);
        _bus.Publish(new HealPlayer(HealPlayer.Full)); // a Spore Pie
        TickWith(Idle);
        Assert.Equal(999, HealPlayer.Full);
        Assert.Equal(10, _bus.States[^1].Health);
        Assert.True(_bus.States[^1].FullHealth);
        _bus.Publish(new HealPlayer(int.MaxValue)); // no overflow
        TickWith(Idle);
        Assert.Equal(10, _bus.States[^1].Health);
    }

    [Fact]
    public void IgnoresNonPositiveHealing()
    {
        _bus.Publish(new DamagePlayer(4, Vector3.Zero));
        _bus.Publish(new HealPlayer(0));
        _bus.Publish(new HealPlayer(-3));
        TickWith(Idle);
        Assert.Equal(6, _bus.States[^1].Health);
    }

    [Fact]
    public void NoHealingWhileDefeated()
    {
        _bus.Publish(new DamagePlayer(10, Vector3.Zero));
        TickWith(Idle);
        _bus.Publish(new HealPlayer(3));
        _bus.Publish(new HealPlayer(HealPlayer.Full));
        TickWith(Idle);
        var s = _bus.States[^1];
        Assert.Equal(0, s.Health);
        Assert.False(s.Alive);
        Assert.False(s.FullHealth);
        // Still down until a teleport brings it back.
        Run(1.0, Idle);
        Assert.False(_bus.States[^1].Alive);
        Assert.Single(_bus.Defeats);
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

    // ---- Saved games (the Save module's CollectSave / RestorePart) ----

    /// <summary>The Player part the module answers a CollectSave with.</summary>
    private static string Collect(FakeBus bus, int requestId = 7)
    {
        bus.Parts.Clear();
        bus.Publish(new CollectSave(requestId));
        var part = Assert.Single(bus.Parts);
        Assert.Equal(requestId, part.RequestId);
        Assert.Equal(SaveModules.Player, part.Module);
        return part.Json;
    }

    /// <summary>A fresh game behind the start screen that gets <paramref name="json"/> restored.</summary>
    private static (FakeBus bus, PlayerModule player, List<string> log) RestoredFrom(string json)
    {
        var (bus, player) = BehindTheStartScreen();
        var log = new List<string>();
        player.Log = log.Add;
        bus.All.Clear();
        bus.Publish(new RestorePart(SaveModules.Player, json));
        return (bus, player, log);
    }

    private static void AssertSameState(PlayerState expected, PlayerState actual)
    {
        Assert.True(Vector3.Distance(expected.Position, actual.Position) < 1e-4f, $"expected {expected.Position}, got {actual.Position}");
        Assert.Equal(expected.Facing, actual.Facing, 5);
        Assert.Equal(expected.Area, actual.Area);
        Assert.Equal(expected.Character, actual.Character);
        Assert.Equal(expected.Health, actual.Health);
        Assert.Equal(expected.MaxHealth, actual.MaxHealth);
        Assert.Equal(expected.Alive, actual.Alive);
        Assert.Equal(expected.Grounded, actual.Grounded);
        Assert.Equal(Vector3.Zero, actual.Velocity);
    }

    [Fact]
    public void SaveThenRestoreGivesBackPositionAreaFacingCharacterAndHealth()
    {
        // The Boy, walked into the forest clearing, turned, hit twice.
        var (bus, player) = BehindTheStartScreen();
        PressStart(bus, GameStart.Boy);
        player.PlaceAt(Middle, 0.3f);
        bus.Publish(new DamagePlayer(3, Vector3.Zero));
        for (int i = 0; i < 60; i++)
        {
            bus.Publish(Moving(1f, 0.5f));
            player.Tick(new TickContext(i, Step));
        }
        bus.Publish(new DamagePlayer(2, Vector3.Zero));
        for (int i = 0; i < 5; i++)
        {
            bus.Publish(Idle);
            player.Tick(new TickContext(60 + i, Step));
        }
        var original = player.State;
        Assert.Equal(CharacterKind.Boy, original.Character);
        Assert.Equal(5, original.Health);
        Assert.Equal(Area.Forest, original.Area);
        Assert.NotEqual(Middle, original.Position);

        string json = Collect(bus);
        var (restoredBus, restored, log) = RestoredFrom(json);

        AssertSameState(original, restored.State);
        Assert.True(restored.Started);
        Assert.True(restored.Visible);
        Assert.Empty(log);
        // It ticks on from there: the next published state is the same spot.
        restoredBus.Publish(new StartScreenState(false));
        restoredBus.Publish(Idle);
        restored.Tick(new TickContext(0, Step));
        AssertSameState(original, restoredBus.States[^1]);
        // And it saves the same again.
        Assert.Equal(json, Collect(restoredBus));
    }

    [Fact]
    public void TheSavedPartIsCompactVersionedJson()
    {
        _player.PlaceAt(Middle, 1f);
        string json = Collect(_bus);
        Assert.DoesNotContain("\n", json);
        Assert.DoesNotContain(" ", json);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(PlayerModule.SaveVersion, root.GetProperty("version").GetInt32());
        Assert.Equal("Forest", root.GetProperty("area").GetString());
        Assert.Equal("Girl", root.GetProperty("character").GetString());
        Assert.Equal(10, root.GetProperty("health").GetInt32());
        Assert.Equal(1f, root.GetProperty("facing").GetSingle(), 5);
        Assert.Equal(Middle.X, root.GetProperty("x").GetSingle(), 4);
        Assert.Equal(Middle.Z, root.GetProperty("z").GetSingle(), 4);
    }

    [Fact]
    public void SaveThenRestoreInsideTheHouseKeepsThePlayerOnTheInteriorFloor()
    {
        _bus.Publish(new TeleportPlayer(Places.HomeInterior.Position, Places.HomeInterior.Yaw));
        Run(0.5, Moving(0.3f, 1f));
        var original = _player.State;
        Assert.Equal(Area.Home, original.Area);

        var (bus, restored, _) = RestoredFrom(Collect(_bus));
        AssertSameState(original, restored.State);
        Assert.Equal(Areas.HomeFloorY, restored.Position.Y, 4);
        Assert.True(PlayerModule.CanStand(restored.Position.X, restored.Position.Z));
        bus.Publish(new StartScreenState(false));
        bus.Publish(Idle);
        restored.Tick(new TickContext(0, Step));
        Assert.Equal(Area.Home, bus.States[^1].Area);
    }

    [Fact]
    public void ASavedSpotThatIsNoLongerStandableComesBackAtTheAreasEntry()
    {
        // In the house but inside its wall, and in the forest on a giant trunk.
        var wall = $"{{\"version\":1,\"x\":594,\"y\":-50,\"z\":600,\"facing\":1,\"area\":\"Home\",\"character\":\"Girl\",\"health\":4}}";
        var (_, home, _) = RestoredFrom(wall);
        Assert.Equal(Places.HomeInterior.Position, home.Position);
        Assert.Equal(Area.Home, home.State.Area);
        Assert.Equal(4, home.Health);

        var trunk = Ground.Obstacles[0];
        Assert.Equal(Area.Forest, Areas.At(trunk.X, trunk.Z));
        var onTrunk = $"{{\"version\":1,\"x\":{trunk.X.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"y\":0,\"z\":{trunk.Z.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"facing\":0,\"area\":\"Forest\",\"character\":\"Boy\",\"health\":10}}";
        var (_, forest, _) = RestoredFrom(onTrunk);
        Assert.Equal(Places.SignpostForestSouth.Position, forest.Position);
        Assert.True(PlayerModule.CanStand(forest.Position.X, forest.Position.Z));
        Assert.Equal(CharacterKind.Boy, forest.Character);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{")]
    [InlineData("[1,2]")]
    [InlineData("\"Player\"")]
    [InlineData("{}")]
    [InlineData("{\"version\":2,\"x\":0,\"y\":0,\"z\":-5,\"facing\":1,\"area\":\"Forest\",\"character\":\"Boy\",\"health\":3}")]
    [InlineData("{\"x\":0,\"y\":0,\"z\":-5,\"facing\":1,\"area\":\"Forest\",\"character\":\"Boy\",\"health\":3}")]
    [InlineData("{\"version\":1,\"x\":\"far\",\"y\":0,\"z\":-5,\"facing\":1,\"area\":\"Forest\",\"character\":\"Boy\",\"health\":3}")]
    [InlineData("{\"version\":1,\"x\":0,\"y\":0,\"z\":-5,\"facing\":1,\"area\":\"Moon\",\"character\":\"Boy\",\"health\":3}")]
    [InlineData("{\"version\":1,\"x\":0,\"y\":0,\"z\":-5,\"facing\":1,\"area\":\"Forest\",\"character\":\"Cat\",\"health\":3}")]
    [InlineData("{\"version\":1,\"x\":0,\"y\":0,\"z\":-5,\"facing\":1,\"area\":\"Forest\",\"character\":\"Boy\",\"health\":99}")]
    [InlineData("{\"version\":1,\"x\":0,\"y\":0,\"z\":-5,\"facing\":1,\"area\":\"Forest\",\"character\":\"Boy\",\"health\":-1}")]
    public void BadSavedJsonKeepsTheDefaultsAndLogsOnce(string json)
    {
        var (bus, player, log) = RestoredFrom(json);
        var fresh = new PlayerModule();
        fresh.Start(new FakeBus());

        AssertSameState(fresh.State, player.State);
        Assert.Equal(Places.HomeDoor.Position, player.Position);
        Assert.Equal(PlayerModule.MaxHealth, player.Health);
        Assert.Equal(CharacterKind.Girl, player.Character);
        Assert.False(player.Started);
        Assert.Empty(bus.All.FindAll(m => m is not RestorePart));
        Assert.Single(log);
        Assert.DoesNotContain("ERROR", log[0]);

        // Logged once: a second bad part says nothing more.
        bus.Publish(new RestorePart(SaveModules.Player, json));
        Assert.Single(log);
    }

    [Fact]
    public void BadSavedJsonInPlayKeepsThePlayerWhereItIs()
    {
        _player.PlaceAt(Middle, 0.5f);
        _bus.Publish(new DamagePlayer(4, Vector3.Zero));
        var before = _player.State;
        _player.Log = _ => { };
        _bus.Publish(new RestorePart(SaveModules.Player, "{\"version\":99}"));
        AssertSameState(before, _player.State);
    }

    [Fact]
    public void OtherModulesPartsAreIgnored()
    {
        string json = $"{{\"version\":1,\"x\":0,\"y\":0,\"z\":-5,\"facing\":1,\"area\":\"Forest\",\"character\":\"Boy\",\"health\":3}}";
        var (bus, player) = BehindTheStartScreen();
        var log = new List<string>();
        player.Log = log.Add;
        bus.Publish(new RestorePart(SaveModules.Wallet, json));
        bus.Publish(new RestorePart(SaveModules.Wardrobe, "not json"));
        Assert.Equal(Places.HomeDoor.Position, player.Position);
        Assert.Equal(CharacterKind.Girl, player.Character);
        Assert.Empty(log);
    }

    [Fact]
    public void RestoringHasNoSideEffects()
    {
        // A save made while defeated (health 0) and mid-hit.
        _player.PlaceAt(Middle, 0f);
        for (int i = 0; i < 4; i++)
        {
            _bus.Publish(new DamagePlayer(3, Vector3.Zero));
            Run(1.0, Idle);
        }
        Assert.False(_player.Alive);
        string json = Collect(_bus);

        var (bus, player, log) = RestoredFrom(json);
        // Nothing published while restoring: no defeat, no state, no save part.
        Assert.Empty(bus.All.FindAll(m => m is not RestorePart));
        Assert.Empty(log);
        // It comes back standing, as after the clinic, with no defeat on its next ticks.
        Assert.True(player.Alive);
        Assert.Equal(PlayerModule.MaxHealth, player.Health);
        Assert.Equal(0f, player.InvulnerableSeconds);
        bus.Publish(new StartScreenState(false));
        for (int i = 0; i < 10; i++)
        {
            bus.Publish(Idle);
            player.Tick(new TickContext(i, Step));
        }
        Assert.Empty(bus.Defeats);
        Assert.True(Vector2.Distance(Flat(Middle), Flat(player.Position)) < 1e-3f);
    }

    [Fact]
    public void ALoadedGameIsInPlayAndALaterGameStartDoesNotMoveIt()
    {
        string json = $"{{\"version\":1,\"x\":0,\"y\":0,\"z\":-5,\"facing\":1,\"area\":\"Forest\",\"character\":\"Boy\",\"health\":6}}";
        var (bus, player, _) = RestoredFrom(json);
        Assert.True(player.Started);
        bus.Publish(new GameStart(GameStart.Girl, GameStart.Cat));
        bus.Publish(new StartScreenState(false));
        bus.Publish(Moving(0f, 1f));
        player.Tick(new TickContext(0, Step));
        Assert.Equal(CharacterKind.Boy, player.Character);
        Assert.Equal(6, player.Health);
        Assert.Equal(Area.Forest, player.State.Area);
        Assert.True(Vector2.Distance(new Vector2(0f, -5f), Flat(player.Position)) < 0.2f);
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

    /// <summary>The pose's states to check: a quick tap, half and full draws, just after a shot and following through.</summary>
    private static ArcheryState[] PoseStates()
    {
        var full = DrawFor(ArcheryState.Relaxed, 1.0);
        var shot = ArcheryPose.Advance(full, 0f, Dt);
        return new[]
        {
            DrawFor(ArcheryState.Relaxed, 0.05), DrawFor(ArcheryState.Relaxed, 0.3), full,
            shot, Pose(shot, 0f, 0.05), Pose(shot, 0f, 0.2), Pose(shot, 0f, 0.5),
        };
    }

    private static readonly float[] s_pitches = { ArcheryPose.MinAimPitch, 0f, 0.35f, ArcheryPose.MaxAimPitch };

    [Fact]
    public void AtALevelAimTheHeldBowStandsUprightTiltedALittleAwayFromTheHead()
    {
        var f = ArcheryPose.Solve(DrawFor(ArcheryState.Relaxed, 0.3), 0f, 0f);
        var up = Vector3.Transform(Vector3.UnitY, f.Bow);
        float tilt = MathF.Acos(Math.Clamp(up.Y, -1f, 1f));
        Assert.True(tilt > 0.05f && tilt < 0.25f, $"bow up {up}, {tilt} rad off world up");
        // The top leans out, away from the head (which is on the other side of the hold point).
        Assert.True(ArcheryPose.HoldSide * up.X > 0f, $"bow up {up}");
        // The bow's -Z (its arc's bulge) points along the arrow, its string (+Z) toward the character.
        AssertNear(f.Forward, Vector3.Transform(-Vector3.UnitZ, f.Bow), 1e-3f);
        AssertNear(-Vector3.UnitZ, f.Forward);
        // Aiming up the limbs stay square to the arrow, still about upright.
        var high = ArcheryPose.Solve(DrawFor(ArcheryState.Relaxed, 0.3), 0f, 0.5f);
        Assert.Equal(0f, Vector3.Dot(Vector3.Transform(Vector3.UnitY, high.Bow), high.Forward), 3);
        Assert.True(Vector3.Transform(Vector3.UnitY, high.Bow).Y > 0.8f);
    }

    [Fact]
    public void TheBowHandIsOnTheGripAtTheHoldPointWhateverTheAimAndDraw()
    {
        foreach (float pitch in s_pitches)
            foreach (float yaw in new[] { -0.3f, 0f, 0.3f })
                foreach (var s in PoseStates())
                {
                    var f = ArcheryPose.Solve(s, yaw, pitch);
                    Assert.Equal(ArcheryPose.HoldLocal, f.Grip);
                    Assert.True(Vector3.Distance(f.HandL, ArcheryPose.HoldLocal) < 0.02f,
                        $"pitch {pitch} yaw {yaw} draw {s.Draw}: left hand {f.HandL}, hold {ArcheryPose.HoldLocal}");
                }
    }

    [Fact]
    public void TheArrowPointsFromTheHoldPointAtTheBowsTarget()
    {
        var feet = new Vector3(3f, 0.4f, -7f);
        const float facing = 0.6f;
        var forward = new Vector3(-MathF.Sin(facing), 0f, -MathF.Cos(facing));
        var target = BowGeometry.HoldPoint(feet, facing) + forward * 12f + new Vector3(0f, 2f, 0f) + new Vector3(forward.Z, 0f, -forward.X) * 1.5f;
        var (yaw, pitch) = ArcheryPose.ArrowAim(feet, facing, target, -Vector3.UnitZ, true);
        var f = ArcheryPose.Solve(DrawFor(ArcheryState.Relaxed, 0.5), yaw, pitch);
        var arrowWorld = ArcheryPose.ToWorld(Vector3.Zero, facing, f.Forward);
        AssertNear(Vector3.Normalize(target - BowGeometry.HoldPoint(feet, facing)), arrowWorld, 1e-3f);
        // The nock is straight behind the grip along the arrow.
        AssertNear(f.Grip - f.Forward * (ArcheryPose.StringRest + f.Pull), f.Nock);
        // Without a target (zero) it is the input's aim.
        var aim = Vector3.Normalize(forward + Vector3.UnitY * 0.2f);
        Assert.Equal(ArcheryPose.AimAngles(aim, true, facing), ArcheryPose.ArrowAim(feet, facing, Vector3.Zero, aim, true));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.35f)]
    [InlineData(ArcheryPose.MaxAimPitch)]
    [InlineData(ArcheryPose.MinAimPitch)]
    public void TheStringHandPullsTheNockBackFurtherAsTheDrawGrows(float pitch)
    {
        float previous = float.MinValue;
        for (int i = 1; i <= 36; i++)
        {
            var s = ArcheryPose.Advance(DrawFor(ArcheryState.Relaxed, (i - 1) * Step), Math.Min(1f, i * Dt / 0.6f), Dt);
            var f = ArcheryPose.Solve(s, 0f, pitch);
            // The visible pull is the draw's, shortened only where the right arm cannot reach further back.
            Assert.True(f.Pull <= s.Draw * BowGeometry.FullDrawPull + 1e-4f && f.Pull > 0f, $"draw {s.Draw}: pull {f.Pull}");
            Assert.True(Vector3.Distance(f.HandR, f.Nock) < 0.03f, $"draw {s.Draw}: right hand {f.HandR}, nock {f.Nock}");
            float back = Vector3.Dot(f.HandR - f.Grip, -f.Forward);
            Assert.True(back > previous - 0.002f, $"draw {s.Draw}: {back} after {previous}");
            previous = Math.Max(previous, back);
            // The string bends into a V at the nock and the limbs flex with the pull.
            Assert.Equal(ArcheryPose.MaxLimbFlex * f.Pull / BowGeometry.FullDrawPull, f.Flex, 4);
        }
        Assert.True(previous > ArcheryPose.StringRest + 0.5f * BowGeometry.FullDrawPull, $"full draw {previous}");
        // At a level aim nearly the whole pull shows.
        Assert.True(ArcheryPose.Solve(DrawFor(ArcheryState.Relaxed, 1.0), 0f, 0f).Pull > 0.75f * BowGeometry.FullDrawPull);
    }

    [Fact]
    public void ArmsKeepTheirLengthAndOnlyTurnAtTheShoulders()
    {
        foreach (float pitch in s_pitches)
            foreach (var s in PoseStates())
            {
                var f = ArcheryPose.Solve(s, 0.2f, pitch);
                foreach (var (shoulder, turn, hand, side) in new[] { (f.ShoulderL, f.ArmL, f.HandL, -1f), (f.ShoulderR, f.ArmR, f.HandR, 1f) })
                {
                    Assert.Equal(1f, turn.Length(), 4); // a pure rotation, no scale
                    Assert.Equal(ArcheryPose.ArmLength, Vector3.Distance(shoulder, hand), 4);
                    AssertNear(shoulder + Vector3.Transform(ArcheryPose.ArmRestDirection(side), turn) * ArcheryPose.ArmLength, hand);
                    AssertNear(ArcheryPose.Shoulder(f.Body, f.Hips, side), shoulder);
                }
                Assert.Equal(1f, f.Body.Length(), 4);
                Assert.Equal(1f, f.Head.Length(), 4);
                Assert.Equal(1f, f.Bow.Length(), 4);
            }
        // At rest the big hands float well out beside the middle of the body (0.36 to 0.88 m).
        var restHand = ArcheryPose.ShoulderLRest + ArcheryPose.ArmLRestDirection * ArcheryPose.ArmLength;
        Assert.InRange(restHand.Y, 0.36f + 0.52f * 0.35f, 0.36f + 0.52f * 0.55f);
        Assert.InRange(restHand.X, -0.43f, -0.36f);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0.3f, 0.35f)]
    [InlineData(-0.3f, ArcheryPose.MinAimPitch)]
    public void TheBodyTurnsRightOfTheAimWithItsChestStillTowardTheTargetsSide(float yaw, float pitch)
    {
        var aim = ArcheryPose.AimLocal(yaw, pitch);
        var flatAim = Vector3.Normalize(new Vector3(aim.X, 0f, aim.Z));
        foreach (var s in PoseStates())
        {
            var f = ArcheryPose.Solve(s, yaw, pitch);
            var chest = Vector3.Transform(-Vector3.UnitZ, f.Body);
            var flatChest = Vector3.Normalize(new Vector3(chest.X, 0f, chest.Z));
            // The chest faces the target's side (less than a quarter turn away), turned to the right of it.
            Assert.True(Vector3.Dot(flatChest, flatAim) > 0.05f, $"draw {s.Draw}: chest {flatChest}, aim {flatAim}");
            Assert.True(Vector3.Cross(flatChest, flatAim).Y > 0f, $"draw {s.Draw}: chest {flatChest} not right of the aim {flatAim}");
            // So the left shoulder comes forward, toward the target, for the left arm to reach across.
            Assert.True(Vector3.Dot(f.ShoulderL - f.ShoulderR, flatAim) > 0.5f * 2f * MathF.Abs(ArcheryPose.ShoulderLRest.X), $"draw {s.Draw}: shoulders {f.ShoulderL} {f.ShoulderR}");
            // Upright enough: the body leans less than about 45 degrees; the hips slide at most a little.
            Assert.True(Vector3.Transform(Vector3.UnitY, f.Body).Y > 0.7f, $"body up {Vector3.Transform(Vector3.UnitY, f.Body)}");
            Assert.Equal(ArcheryPose.BodyPivot.Y, f.Hips.Y, 4);
            Assert.True(MathF.Abs(f.Hips.X) <= ArcheryPose.MaxHipShift + 1e-4f && MathF.Abs(f.Hips.Z) <= ArcheryPose.MaxHipShift + 1e-4f, $"hips {f.Hips}");
            // The head looks along the arrow (yaw and most of the pitch).
            var look = Vector3.Transform(-Vector3.UnitZ, f.Head);
            Assert.True(Vector3.Dot(look, aim) > 0.95f, $"head looks {look}, aim {aim}");
        }
    }

    private static readonly float[] s_aimYaws = { -0.3f, 0f, 0.3f };

    [Fact]
    public void TheLeftHandHoldsTheBowOnTheRightReachingAcrossInFrontOfTheChest()
    {
        Assert.True(ArcheryPose.HoldLocal.X > 0.4f); // the hold point is on the right
        Assert.Equal(1f, ArcheryPose.HoldSide);
        foreach (float pitch in s_pitches)
            foreach (float yaw in s_aimYaws)
                foreach (var s in PoseStates())
                {
                    var f = ArcheryPose.Solve(s, yaw, pitch);
                    var chest = Vector3.Transform(-Vector3.UnitZ, f.Body);
                    Assert.True(Vector3.Distance(f.HandL, ArcheryPose.HoldLocal) < 0.02f, $"pitch {pitch} yaw {yaw} draw {s.Draw}: left hand {f.HandL}");
                    // The left arm goes from the left shoulder across to the right and out in front of the chest.
                    Assert.True(f.HandL.X - f.ShoulderL.X > 0.2f, $"left hand {f.HandL}, shoulder {f.ShoulderL}");
                    Assert.True(Vector3.Dot(f.HandL - f.ShoulderL, chest) > 0.15f, $"left hand {f.HandL}, shoulder {f.ShoulderL}, chest {chest}");
                }
    }

    [Fact]
    public void BothHandsAndElbowsStayInFrontOfTheBackNeverBehindTheNeckOrHead()
    {
        foreach (float pitch in s_pitches)
            foreach (float yaw in s_aimYaws)
                foreach (var s in PoseStates())
                {
                    var f = ArcheryPose.Solve(s, yaw, pitch);
                    var chest = Vector3.Transform(-Vector3.UnitZ, f.Body);
                    // The torso's back plane: through its back, square to the chest's facing.
                    var back = f.Hips + Vector3.Transform(new Vector3(0f, 0f, ArcheryPose.TorsoBack), f.Body);
                    var flatAim = Vector3.Normalize(new Vector3(f.Forward.X, 0f, f.Forward.Z));
                    var head = ArcheryPose.HeadCenter(f);
                    // The arms never bend: an elbow is the middle of its arm.
                    var points = new[]
                    {
                        ("left hand", f.HandL), ("right hand", f.HandR),
                        ("left elbow", (f.ShoulderL + f.HandL) * 0.5f), ("right elbow", (f.ShoulderR + f.HandR) * 0.5f),
                    };
                    foreach (var (name, p) in points)
                    {
                        string at = $"pitch {pitch} yaw {yaw} draw {s.Draw} since {s.SecondsSinceRelease}: {name} {p}";
                        float front = Vector3.Dot(p - back, chest);
                        Assert.True(front > 0.1f, $"{at} is {front} m in front of the back");
                        Assert.Equal(front, ArcheryPose.InFrontOfBack(f, p), 4);
                        // Not far behind the neck or the head along the arrow (the string hand draws back
                        // beside the small round head, at chest height).
                        Assert.True(Vector3.Dot(p - f.Neck, flatAim) > -0.5f, $"{at} is behind the neck {f.Neck}");
                        Assert.True(Vector3.Dot(p - head, flatAim) > -0.55f, $"{at} is behind the head {head}");
                    }
                    // The right arm stays on the right, never reaching round behind the head to the left.
                    Assert.True(f.HandR.X > head.X + 0.2f && (f.ShoulderR.X + f.HandR.X) * 0.5f > head.X, $"right hand {f.HandR}, head {head}");
                }
    }

    [Fact]
    public void AtFullDrawTheRightHandDrawsToTheRightOfTheHeadBelowTheChin()
    {
        var full = DrawFor(ArcheryState.Relaxed, 1.0);
        foreach (float pitch in new[] { ArcheryPose.MinAimPitch, 0f, 0.35f }) // down, level, 20 degrees up
            foreach (float yaw in s_aimYaws)
            {
                var f = ArcheryPose.Solve(full, yaw, pitch);
                var head = ArcheryPose.HeadCenter(f);
                var right = Vector3.Normalize(Vector3.Cross(f.Forward, Vector3.UnitY));
                string at = $"pitch {pitch} yaw {yaw}: right hand {f.HandR}, head {head}";
                Assert.True(Vector3.Distance(f.HandR, f.Nock) < 0.03f, at);
                // On the head's right, clear of it, at chest height below the chin (the short invisible
                // arms reach no higher), not out in front of the head nor far behind it.
                Assert.True(Vector3.Dot(f.HandR - head, right) > ArcheryPose.HeadRadius + 0.05f, at);
                Assert.True(f.HandR.Y < head.Y - ArcheryPose.HeadRadius, at);
                float d = Vector3.Distance(f.HandR, head);
                Assert.True(d > ArcheryPose.HeadRadius + 0.1f && d < 0.75f, $"{at}: {d} m from the head's middle");
                Assert.InRange(Vector3.Dot(f.HandR - head, Vector3.Normalize(new Vector3(f.Forward.X, 0f, f.Forward.Z))), -0.55f, 0.1f);
                // In front of the back.
                Assert.True(ArcheryPose.InFrontOfBack(f, f.HandR) > 0.1f, at);
            }
    }

    [Fact]
    public void TheHeadStaysOnTheNeckClearOfTheBowAndTheStringHand()
    {
        foreach (float pitch in s_pitches)
            foreach (float yaw in s_aimYaws)
            foreach (var s in PoseStates())
            {
                var f = ArcheryPose.Solve(s, yaw, pitch);
                AssertNear(f.Hips + Vector3.Transform(ArcheryPose.NeckRest - ArcheryPose.BodyPivot, f.Body), f.Neck);
                var head = ArcheryPose.HeadCenter(f);
                Assert.True(Vector3.Distance(head, f.Grip) > ArcheryPose.HeadRadius + 0.1f, $"pitch {pitch}: grip {f.Grip}, head {head}");
                Assert.True(Vector3.Distance(head, f.HandR) > ArcheryPose.HeadRadius, $"pitch {pitch} draw {s.Draw}: hand {f.HandR}, head {head}");
                // Seen from the aim camera (right of the head) the bow is out beside it.
                Assert.True(ArcheryPose.HoldSide * (f.Grip.X - head.X) > ArcheryPose.HeadRadius + 0.02f, $"grip {f.Grip}, head {head}");
            }
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(0f)]
    [InlineData(0.35f)]
    [InlineData(ArcheryPose.MaxAimPitch)]
    [InlineData(ArcheryPose.MinAimPitch)]
    public void ThePoseMovesSmoothlyThroughADrawAndAShot(float pitch)
    {
        var s = ArcheryState.Relaxed;
        var previous = ArcheryPose.Solve(DrawFor(ArcheryState.Relaxed, Step), 0f, pitch);
        for (int i = 2; i <= 36 + 40; i++)
        {
            s = ArcheryPose.Advance(s, i <= 36 ? Math.Min(1f, i * Dt / 0.6f) : 0f, Dt);
            var f = ArcheryPose.Solve(s, 0f, pitch);
            if (i == 37) // the shot: the string snaps, the hand flies off it
            {
                previous = f;
                continue;
            }
            Assert.True(Vector3.Distance(f.HandR, previous.HandR) < 0.06f, $"tick {i}: right hand jumps {previous.HandR} -> {f.HandR}");
            Assert.True(Vector3.Distance(f.ShoulderL, previous.ShoulderL) < 0.04f, $"tick {i}: shoulder jumps {previous.ShoulderL} -> {f.ShoulderL}");
            previous = f;
        }
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

        // The right hand flies back off the string.
        var after = Pose(s, 0f, 0.2);
        Assert.Equal(1f, ArcheryPose.FollowThrough(after));
        var drawnPose = ArcheryPose.Solve(drawn, 0f, 0f);
        var afterPose = ArcheryPose.Solve(after, 0f, 0f);
        var aim = drawnPose.Forward;
        // Up and out to the right off the string, not forward (never round behind the head).
        Assert.True(Vector3.Dot(afterPose.HandR - drawnPose.HandR, -aim) > 0f, $"{drawnPose.HandR} -> {afterPose.HandR}");
        Assert.True(afterPose.HandR.Y - drawnPose.HandR.Y > 0.05f, $"{drawnPose.HandR} -> {afterPose.HandR}");
        Assert.True(afterPose.HandR.X - drawnPose.HandR.X > 0f, $"{drawnPose.HandR} -> {afterPose.HandR}");
        // The string is straight again, the limbs unbent.
        Assert.Equal(0f, afterPose.Pull, 2);
        Assert.Equal(0f, afterPose.Flex, 2);
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
        var back = new Vector3(0f, 0.74f, 0.17f);
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
        var back = new Vector3(0f, 0.74f, 0.17f);
        AssertNear(back, ArcheryPose.BowGrip(0f, back));
        var previous = back;
        for (int i = 1; i <= 100; i++)
        {
            var p = ArcheryPose.BowGrip(i / 100f, back);
            Assert.True(Vector3.Distance(previous, p) < 0.04f, $"jump at {i}");
            previous = p;
        }
        AssertNear(ArcheryPose.HoldLocal, previous);
        // It swings over the right shoulder (rising above it), not through the head, into the left hand.
        var shoulderR = new Vector3(-ArcheryPose.ShoulderLRest.X, ArcheryPose.ShoulderLRest.Y, ArcheryPose.ShoulderLRest.Z);
        float highest = float.MinValue;
        for (int i = 10; i < 100; i++)
        {
            var p = ArcheryPose.BowGrip(i / 100f, back);
            Assert.True(p.X > 0f, $"at {i / 100f} the bow is left of the middle: {p}");
            if (MathF.Abs(p.Z - shoulderR.Z) < 0.15f)
                highest = Math.Max(highest, p.Y);
        }
        Assert.True(ArcheryPose.BowGrip(0.5f, back).X > shoulderR.X, $"{ArcheryPose.BowGrip(0.5f, back)}");
        Assert.True(highest > shoulderR.Y + 0.1f, $"highest over the shoulder {highest}");
        var held = ArcheryPose.Solve(DrawFor(ArcheryState.Relaxed, 0.3), 0f, 0f);
        Assert.True(Vector3.Distance(held.HandL, previous) < 0.02f, "into the left hand");
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
        Assert.True(look.Shows(BuiltinPieces.Top | BuiltinPieces.Shoes));
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
        // The chosen hair style's model goes on the bald head; the built-in outfit stays.
        Assert.Equal(Looks.HairStyles[4].ModelPath, look.HairModel);
        Assert.Equal("res://modules/Wardrobe/Models/Hair/top_bun.glb", look.HairModel);
        Assert.Equal(Rig.Pieces(CharacterKind.Boy), look.Builtin);
    }

    [Theory]
    [InlineData(CharacterKind.Girl)]
    [InlineData(CharacterKind.Boy)]
    public void NoHairIsTheBareRoundHeadAndTheBuiltInModelsHaveNoHair(CharacterKind kind)
    {
        // "No hair" (appended to the styles) and an out-of-range style both leave the head bare.
        Assert.Equal("", Looks.HairStyles[Looks.NoHair].ModelPath);
        Assert.Equal("", CharacterLook.From(Dressed(hairStyle: Looks.NoHair), kind).HairModel);
        Assert.Equal("", CharacterLook.From(Dressed(hairStyle: 99), kind).HairModel);
        Assert.Equal("", CharacterLook.Default(kind).HairModel);
        // No built-in hair piece any more: the pieces are the outfit only.
        Assert.Equal(BuiltinPieces.Top | BuiltinPieces.Shoes | (kind == CharacterKind.Girl ? BuiltinPieces.Head : BuiltinPieces.Bottom),
            Rig.Pieces(kind));
        Assert.DoesNotContain("Hair", Enum.GetNames<BuiltinPieces>());
    }

    [Theory]
    [InlineData(CharacterKind.Girl)]
    [InlineData(CharacterKind.Boy)]
    public void NothingWornShowsTheOldOutfitWithTheChosenColorsAndHair(CharacterKind kind)
    {
        var look = CharacterLook.From(Dressed(skin: 3, hairStyle: 1), kind);
        foreach (Slot slot in Enum.GetValues<Slot>())
            Assert.Equal("", look.Model(slot));
        Assert.Equal(Rig.Pieces(kind), look.Builtin);
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
        Assert.Equal(Rig.Pieces(CharacterKind.Boy), boy.Builtin);
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
        Assert.Equal(Rig.Pieces(CharacterKind.Girl), look.Builtin);
    }

    /// <summary>
    /// girl.glb or boy.glb (modules/Player/Models) read back: each node's name, parent, mesh and
    /// world transform (glTF's frame, Godot's: X right, Y up, -Z forward), and its mesh's vertices
    /// by material.
    /// </summary>
    private sealed class Glb
    {
        public readonly Dictionary<string, int> Index = new();
        public readonly List<(string Name, int Parent, int Mesh, Matrix4x4 World)> Nodes = new();
        private readonly JsonElement _json;
        private readonly byte[] _bin;

        public Glb(string kind)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "modules", "Player", "Models")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            var bytes = File.ReadAllBytes(Path.Combine(dir!.FullName, "modules", "Player", "Models", kind + ".glb"));
            int jsonLength = BitConverter.ToInt32(bytes, 12);
            _json = JsonDocument.Parse(new ReadOnlyMemory<byte>(bytes, 20, jsonLength)).RootElement;
            int binStart = 20 + jsonLength;
            _bin = bytes[(binStart + 8)..(binStart + 8 + BitConverter.ToInt32(bytes, binStart))];
            var nodes = _json.GetProperty("nodes");
            var parents = Enumerable.Repeat(-1, nodes.GetArrayLength()).ToArray();
            for (int i = 0; i < parents.Length; i++)
                if (nodes[i].TryGetProperty("children", out var children))
                    foreach (var child in children.EnumerateArray())
                        parents[child.GetInt32()] = i;
            var world = new Matrix4x4?[parents.Length];
            Matrix4x4 World(int i) => world[i] ??= Local(nodes[i]) * (parents[i] < 0 ? Matrix4x4.Identity : World(parents[i]));
            for (int i = 0; i < parents.Length; i++)
            {
                string name = nodes[i].GetProperty("name").GetString()!;
                Index[name] = i;
                Nodes.Add((name, parents[i], nodes[i].TryGetProperty("mesh", out var mesh) ? mesh.GetInt32() : -1, World(i)));
            }
        }

        private static Matrix4x4 Local(JsonElement node)
        {
            float[] Floats(string key, params float[] fallback) =>
                node.TryGetProperty(key, out var v) ? v.EnumerateArray().Select(x => x.GetSingle()).ToArray() : fallback;
            var t = Floats("translation", 0, 0, 0);
            var r = Floats("rotation", 0, 0, 0, 1);
            var s = Floats("scale", 1, 1, 1);
            return Matrix4x4.CreateScale(s[0], s[1], s[2]) * Matrix4x4.CreateFromQuaternion(new Quaternion(r[0], r[1], r[2], r[3])) *
                Matrix4x4.CreateTranslation(t[0], t[1], t[2]);
        }

        public bool Has(string name) => Index.ContainsKey(name);

        public (string Name, int Parent, int Mesh, Matrix4x4 World) Node(string name)
        {
            Assert.True(Index.ContainsKey(name), $"no node {name}");
            return Nodes[Index[name]];
        }

        public Vector3 Origin(string name) => Node(name).World.Translation;

        public string ParentOf(string name) => Node(name).Parent < 0 ? "" : Nodes[Node(name).Parent].Name;

        /// <summary>The base color of the material named <paramref name="material"/> (glTF's baseColorFactor, RGB).</summary>
        public Vector3 Color(string material)
        {
            foreach (var m in _json.GetProperty("materials").EnumerateArray())
                if (m.GetProperty("name").GetString() == material)
                {
                    var c = m.GetProperty("pbrMetallicRoughness").GetProperty("baseColorFactor");
                    return new Vector3(c[0].GetSingle(), c[1].GetSingle(), c[2].GetSingle());
                }
            Assert.Fail($"no material {material}");
            return default;
        }

        /// <summary>The materials of the node's own mesh.</summary>
        public IEnumerable<string> Materials(string name)
        {
            int mesh = Node(name).Mesh;
            if (mesh < 0)
                yield break;
            foreach (var primitive in _json.GetProperty("meshes")[mesh].GetProperty("primitives").EnumerateArray())
                yield return _json.GetProperty("materials")[primitive.GetProperty("material").GetInt32()].GetProperty("name").GetString()!;
        }

        /// <summary>The world positions of the node's own mesh's vertices (only those in <paramref name="material"/> when given).</summary>
        public List<Vector3> Vertices(string name, string? material = null)
        {
            var result = new List<Vector3>();
            var node = Node(name);
            if (node.Mesh < 0)
                return result;
            foreach (var primitive in _json.GetProperty("meshes")[node.Mesh].GetProperty("primitives").EnumerateArray())
            {
                string mat = _json.GetProperty("materials")[primitive.GetProperty("material").GetInt32()].GetProperty("name").GetString()!;
                if (material != null && mat != material)
                    continue;
                var accessor = _json.GetProperty("accessors")[primitive.GetProperty("attributes").GetProperty("POSITION").GetInt32()];
                var view = _json.GetProperty("bufferViews")[accessor.GetProperty("bufferView").GetInt32()];
                int start = (view.TryGetProperty("byteOffset", out var vo) ? vo.GetInt32() : 0) +
                    (accessor.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0);
                int stride = view.TryGetProperty("byteStride", out var st) ? st.GetInt32() : 12;
                for (int i = 0; i < accessor.GetProperty("count").GetInt32(); i++)
                {
                    int at = start + i * stride;
                    var local = new Vector3(BitConverter.ToSingle(_bin, at), BitConverter.ToSingle(_bin, at + 4), BitConverter.ToSingle(_bin, at + 8));
                    result.Add(Vector3.Transform(local, node.World));
                }
            }
            return result;
        }

        /// <summary>The vertices of the node and every node under it.</summary>
        public List<Vector3> VerticesUnder(string name)
        {
            var result = Vertices(name);
            for (int i = 0; i < Nodes.Count; i++)
                if (Nodes[i].Parent == Index[name])
                    result.AddRange(VerticesUnder(Nodes[i].Name));
            return result;
        }
    }

    private static float Radius(Vector3 v) => MathF.Sqrt(v.X * v.X + v.Z * v.Z);

    /// <summary>The widest the vertices get round the vertical axis between heights y0 and y1.</summary>
    private static float WidestBetween(List<Vector3> vertices, float y0, float y1) =>
        vertices.Where(v => v.Y >= y0 && v.Y <= y1).Max(Radius);

    [Theory]
    [InlineData("girl", CharacterKind.Girl)]
    [InlineData("boy", CharacterKind.Boy)]
    public void TheModelsKeepTheRigsPartsAndPivots(string file, CharacterKind kind)
    {
        var glb = new Glb(file);
        foreach (var part in new[] { "Body", "Head", "LegL", "LegR", "ArmL", "ArmR", "HandL", "HandR", "BackBow", "HandBow",
                     "HandBowLimbTop", "HandBowLimbBottom", "NockArrow" })
            Assert.True(glb.Has(part), part);
        Assert.True(Vector3.Distance(ArcheryPose.NeckRest, glb.Origin("Head")) < 0.001f);
        Assert.True(Vector3.Distance(ArcheryPose.BodyPivot, glb.Origin("Body")) < 0.001f);
        Assert.True(Vector3.Distance(ArcheryPose.ShoulderLRest, glb.Origin("ArmL")) < 0.001f);
        Assert.True(Vector3.Distance(ArcheryPose.ShoulderLRest * new Vector3(-1, 1, 1), glb.Origin("ArmR")) < 0.001f);
        // The legs' pivots at the hips, under the body, the hands at the (invisible) arms' ends.
        foreach (var (leg, side) in new[] { ("LegL", -1f), ("LegR", 1f) })
        {
            var hip = glb.Origin(leg);
            Assert.Equal(ArcheryPose.BodyPivot.Y, hip.Y, 3);
            Assert.InRange(hip.X * side, 0.105f, 0.115f); // wider apart than the old 0.08
        }
        foreach (var (arm, hand, side) in new[] { ("ArmL", "HandL", 1f), ("ArmR", "HandR", -1f) })
        {
            var reach = glb.Origin(hand) - glb.Origin(arm);
            Assert.Equal(ArcheryPose.ArmLength, reach.Length(), 3);
            Assert.True(Vector3.Distance(ArcheryPose.ArmLRestDirection * new Vector3(side, 1, 1), Vector3.Normalize(reach)) < 0.001f);
        }
        Assert.True(Vector3.Distance(glb.Origin("BackBow"), glb.Origin("HandBow")) < 0.001f);
        // The bow sized to the shorter body: the held bow's tips as far from the grip as the pose
        // has them (ArcheryPose.TipHalfSpan), the slung one on the back between the hips and the head.
        Assert.True(Matrix4x4.Invert(glb.Node("HandBow").World, out var held));
        var limbs = glb.VerticesUnder("HandBow").Select(v => Vector3.Transform(v, held)).ToList();
        foreach (var tip in new[] { limbs.Max(v => v.Y), -limbs.Min(v => v.Y) })
            Assert.InRange(tip, ArcheryPose.TipHalfSpan, ArcheryPose.TipHalfSpan + 0.07f);
        Assert.InRange(ArcheryPose.TipHalfSpan, 0.35f, 0.42f);
        var slung = glb.Vertices("BackBow");
        Assert.True(slung.Min(v => v.Y) > ArcheryPose.BodyPivot.Y - 0.1f && slung.Max(v => v.Y) < BowGeometry.HeadUp - 0.1f,
            $"the slung bow from {slung.Min(v => v.Y)} to {slung.Max(v => v.Y)}");
        Assert.Equal("HandBow", glb.ParentOf("HandBowLimbTop"));
        Assert.Equal("HandBow", glb.ParentOf("HandBowLimbBottom"));

        // The built-in pieces on their parts: exactly those Rig.Pieces lists; no hair, sleeves or trouser legs.
        Assert.False(glb.Has("Hair"));
        Assert.DoesNotContain(glb.Nodes, n => n.Name.Contains("Hair", StringComparison.Ordinal));
        Assert.Equal("Body", glb.ParentOf("DefaultTop"));
        Assert.Equal("LegL", glb.ParentOf("DefaultShoesLegL"));
        Assert.Equal("LegR", glb.ParentOf("DefaultShoesLegR"));
        foreach (var piece in new[] { BuiltinPieces.Top, BuiltinPieces.Bottom, BuiltinPieces.Shoes, BuiltinPieces.Head })
            Assert.Equal((Rig.Pieces(kind) & piece) != 0, glb.Nodes.Any(n => n.Name.StartsWith(Rig.NodePrefix(piece), StringComparison.Ordinal)));
        Assert.Equal(kind == CharacterKind.Girl ? "Head" : "Body",
            glb.ParentOf(kind == CharacterKind.Girl ? "DefaultHead" : "DefaultBottom"));
        Assert.DoesNotContain(glb.Nodes, n => n.Name.StartsWith("DefaultTopArm", StringComparison.Ordinal) ||
            n.Name.StartsWith("DefaultBottomLeg", StringComparison.Ordinal));
    }

    /// <summary>The body's skin of girl.glb or boy.glb: its bottom, top and height.</summary>
    private static (List<Vector3> Skin, float Bottom, float Top, float Height) BodyOf(Glb glb)
    {
        var skin = glb.Vertices("Body", "Skin");
        float bottom = skin.Min(v => v.Y), top = skin.Max(v => v.Y);
        return (skin, bottom, top, top - bottom);
    }

    [Theory]
    [InlineData("girl")]
    [InlineData("boy")]
    public void TheModelsAreABigBaldRoundHeadWithOnlyTwoBigEyesFloatingOverTheBody(string file)
    {
        var glb = new Glb(file);
        // About 1.30 m tall (the body 25% shorter than before, everything above it lowered with it):
        // the head a plain ball 0.38 m across, its top at 1.30 m, its center at BowGeometry.HeadUp
        // (1.11 m) over the feet.
        var head = glb.Vertices("Head", "Skin");
        var min = head.Aggregate(Vector3.Min);
        var max = head.Aggregate(Vector3.Max);
        Assert.Equal(1.30f, max.Y, 2);
        Assert.Equal(1.11f, BowGeometry.HeadUp, 3);
        Assert.Equal(BowGeometry.HeadUp, (min.Y + max.Y) / 2, 2);
        Assert.Equal(BowGeometry.HeadUp, ArcheryPose.NeckRest.Y + ArcheryPose.HeadAboveNeck, 3);
        Assert.Equal(0f, (min.X + max.X) / 2, 2);
        Assert.Equal(0.38f, max.X - min.X, 2);
        Assert.Equal(0.38f, max.Y - min.Y, 2);
        Assert.Equal(0.38f, max.Z - min.Z, 2);
        Assert.Equal(0.19f, ArcheryPose.HeadRadius, 3);
        var center = (min + max) / 2;
        Assert.True(head.All(v => MathF.Abs(Vector3.Distance(v, center) - 0.19f) < 0.003f), "a plain round ball");
        Assert.InRange(glb.VerticesUnder("Head").Concat(glb.VerticesUnder("Body")).Concat(glb.VerticesUnder("LegL")).Max(v => v.Y), 1.29f, 1.38f);

        // Only two big dark oval eyes with white sparkles (low on the face, wide apart) and small
        // soft cheeks: no mouth, no nose, no hair.
        Assert.Equal(new[] { "Blush", "Eye", "EyeShine", "Skin" }, glb.Materials("Head").Distinct().OrderBy(m => m, StringComparer.Ordinal));
        var eyes = glb.Vertices("Head", "Eye");
        foreach (float side in new[] { -1f, 1f })
        {
            var eye = eyes.Where(v => v.X * side > 0).ToList();
            var lo = eye.Aggregate(Vector3.Min);
            var hi = eye.Aggregate(Vector3.Max);
            Assert.InRange(hi.Y - lo.Y, 0.08f, 0.12f);                // big
            Assert.InRange((hi.Y - lo.Y) / (hi.X - lo.X), 1.2f, 2f);  // oval, taller than wide
            float middle = (lo.Y + hi.Y) / 2;
            Assert.InRange(middle, center.Y - 0.07f, center.Y);       // low-middle on the face
            Assert.InRange((lo.X + hi.X) / 2 * side, 0.06f, 0.1f);     // wide apart
            Assert.True(lo.Z < center.Z - 0.12f, "on the front");
            var shine = glb.Vertices("Head", "EyeShine").Where(v => v.X * side > 0).ToList();
            Assert.True(shine.Count > 0 && shine.Min(v => v.Z) < lo.Z + 0.005f, "a white sparkle on the eye");
        }
        var blush = glb.Vertices("Head", "Blush");
        Assert.True(blush.Count > 0);
        foreach (float side in new[] { -1f, 1f })
        {
            var cheek = blush.Where(v => v.X * side > 0).ToList();
            var size = cheek.Aggregate(Vector3.Max) - cheek.Aggregate(Vector3.Min);
            Assert.True(size.X < 0.07f && size.Y < 0.05f, "small cheeks");
        }

        // About 4 cm of air between the head and the body and its clothes: no neck.
        var body = BodyOf(glb);
        float bodyTop = glb.VerticesUnder("Body").Max(v => v.Y);
        Assert.InRange(min.Y - body.Top, 0.035f, 0.05f);
        Assert.InRange(min.Y - bodyTop, 0.02f, 0.05f);
        Assert.InRange(glb.VerticesUnder("Head").Min(v => v.Y) - bodyTop, 0.02f, 0.05f);
    }

    /// <summary>
    /// The body's outline seen from the front: its skin's radius at each height (rings of the
    /// solid of revolution), bottom to top.
    /// </summary>
    private static (float Y, float R)[] Outline(List<Vector3> skin) =>
        skin.GroupBy(v => MathF.Round(v.Y, 4)).Select(g => (g.Key, g.Max(Radius))).OrderBy(r => r.Key).ToArray();

    [Theory]
    [InlineData("girl", CharacterKind.Girl)]
    [InlineData("boy", CharacterKind.Boy)]
    public void TheBodiesAreShorterAndRoundAtTheBaseTheGirlsATriangleAndTheBoysAnEggWithABigBowTie(string file, CharacterKind kind)
    {
        var glb = new Glb(file);
        var (skin, bottom, top, h) = BodyOf(glb);
        // About 25% shorter than the old body (0.36 to 1.05 m): 0.36 to 0.88 m, round all the way round, smooth.
        Assert.Equal(0.36f, bottom, 2);
        Assert.InRange(top, 0.87f, 0.89f);
        Assert.InRange(h / 0.69f, 0.72f, 0.78f);
        Assert.True(skin.Count > 500, "smooth");
        var ring = skin.Where(v => v.Y > bottom + h * 0.3f && v.Y < bottom + h * 0.35f).Select(Radius).ToList();
        Assert.True(ring.Max() - ring.Min() < 0.02f, "round");
        // Rounder at the base: the widest low down, then the outline curves softly round under it
        // to the middle of the bottom (no sharp rim: it never turns more than 25 degrees between
        // neighboring rings), and up over a rounded top.
        var outline = Outline(skin);
        float widest = outline.Max(r => r.R);
        float widestY = outline.First(r => r.R == widest).Y;
        Assert.InRange(widestY, bottom + 0.05f, bottom + h * 0.3f);
        Assert.True(skin.Where(v => v.Y < bottom + 0.01f).Max(Radius) < 0.65f * widest, "a round bottom");
        Assert.True(skin.Where(v => v.Y > top - 0.01f).Max(Radius) < 0.08f, "a rounded top");
        for (int i = 1; i + 1 < outline.Length; i++)
        {
            var a = new Vector2(outline[i].R - outline[i - 1].R, outline[i].Y - outline[i - 1].Y);
            var b = new Vector2(outline[i + 1].R - outline[i].R, outline[i + 1].Y - outline[i].Y);
            float turn = MathF.Acos(Math.Clamp(Vector2.Dot(Vector2.Normalize(a), Vector2.Normalize(b)), -1f, 1f));
            Assert.True(turn < 25f * MathF.PI / 180f, $"a sharp edge at {outline[i].Y}: {turn * 180f / MathF.PI} degrees");
        }
        // Above the widest it only narrows, up to the top.
        for (int i = 1; i < outline.Length; i++)
            if (outline[i - 1].Y >= widestY)
                Assert.True(outline[i].R <= outline[i - 1].R + 1e-4f, $"wider again at {outline[i].Y}");
        if (kind == CharacterKind.Girl)
        {
            // A triangle from the front, about 0.42 m wide low down, narrowing steadily to a narrow
            // rounded top.
            Assert.Equal(0.42f, 2 * widest, 2);
            float last = float.MaxValue;
            for (int i = 1; i < 5; i++)
            {
                float slice = WidestBetween(skin, bottom + h * i / 5f, bottom + h * (i + 1) / 5f);
                Assert.True(slice < last - 0.02f, $"slice {i}: {slice} not narrower than {last}");
                last = slice;
            }
            Assert.True(WidestBetween(skin, bottom + h * 0.8f, top) < 0.45f * widest, "a narrow top");
        }
        else
        {
            // A soft egg: about 0.28 m wide near the top, widest (0.38 m) low down.
            Assert.Equal(0.38f, 2 * widest, 2);
            Assert.InRange(2 * WidestBetween(skin, bottom + h * 0.78f, bottom + h * 0.82f), 0.25f, 0.31f);
            Assert.True(WidestBetween(skin, bottom + h * 0.45f, bottom + h * 0.55f) < widest - 0.015f, "wider low down than in the middle");
            // The big red bow tie at the top, about as wide as the body there, in front; two buttons below it.
            var tie = glb.Vertices("DefaultTop", "Ribbon");
            var lo = tie.Aggregate(Vector3.Min);
            var hi = tie.Aggregate(Vector3.Max);
            Assert.InRange(hi.X - lo.X, 0.24f, 0.32f);
            Assert.InRange((lo.Y + hi.Y) / 2, bottom + h * 0.75f, top - 0.05f);
            Assert.True(hi.Y < top + 0.01f, "under the head");
            Assert.True(lo.Z < -0.15f, "in front");
            var buttons = glb.Vertices("DefaultTop", "Spots");
            Assert.True(buttons.Count > 0 && buttons.Max(v => v.Y) < lo.Y && buttons.All(v => v.Z < -0.13f));
        }

        // The outfit colors the body: it covers it from its top down to its bottom.
        var clothes = glb.VerticesUnder("DefaultTop").Concat(glb.Has("DefaultBottom") ? glb.VerticesUnder("DefaultBottom") : new List<Vector3>()).ToList();
        Assert.True(clothes.Min(v => v.Y) <= bottom + 0.01f);
        Assert.True(clothes.Max(v => v.Y) >= top - 0.01f);
        if (glb.Has("DefaultBottom"))
        {
            // The Boy's trousers: a band round the rounded base of the egg, under it too.
            var trousers = glb.Vertices("DefaultBottom");
            Assert.True(trousers.Max(v => v.Y) < bottom + h * 0.4f);
            Assert.True(trousers.Min(v => v.Y) < bottom);
            Assert.True(trousers.Max(Radius) > WidestBetween(skin, bottom, bottom + h * 0.3f));
        }
    }

    [Theory]
    [InlineData("girl")]
    [InlineData("boy")]
    public void TheModelsHaveBigFloatingOvalHandsWithFingerLinesAndTwoTubeLegsButNoArms(string file)
    {
        var glb = new Glb(file);
        // No arm meshes: the arms are only pivots. The skin is the head, the body, the hands and the legs.
        foreach (var arm in new[] { "ArmL", "ArmR" })
            Assert.Equal(-1, glb.Node(arm).Mesh);
        Assert.Equal(new[] { "Body", "HandL", "HandR", "Head", "LegL", "LegR" },
            glb.Nodes.Where(n => glb.Materials(n.Name).Contains("Skin")).Select(n => n.Name).OrderBy(n => n, StringComparer.Ordinal));

        var (skin, bottom, top, h) = BodyOf(glb);
        var head = glb.Vertices("Head", "Skin");
        float headWidth = head.Max(v => v.X) - head.Min(v => v.X);
        foreach (var (hand, side) in new[] { ("HandL", -1f), ("HandR", 1f) })
        {
            // Big ovals (0.14 m wide, 0.18 m tall: about half the head's width), floating well out
            // beside the body about its middle, with three short dark finger lines.
            var v = glb.Vertices(hand, "Skin");
            var size = v.Aggregate(Vector3.Max) - v.Aggregate(Vector3.Min);
            Assert.Equal(0.14f, size.X, 2);
            Assert.Equal(0.18f, size.Y, 2);
            Assert.InRange(size.X / headWidth, 0.33f, 0.5f);
            var center = glb.Origin(hand);
            Assert.True(center.X * side > 0, hand);
            Assert.InRange(center.Y, bottom + h * 0.35f, top - h * 0.4f); // beside the middle of the body
            float inner = v.Min(p => p.X * side);
            Assert.True(inner > WidestBetween(skin, center.Y - 0.1f, center.Y + 0.1f) + 0.07f, $"{hand} is not well out from the body");
            var lines = glb.Vertices(hand, "Finger");
            Assert.True(lines.Count > 0, $"{hand} has no finger lines");
            Assert.True(lines.All(p => p.Y > center.Y && p.Z < center.Z), "on the front of the hand's top");
            var xs = lines.Select(p => MathF.Round((p.X - center.X) * 1000f / 26f)).Distinct().Count();
            Assert.Equal(3, xs);
            Assert.True(lines.All(p => MathF.Abs(p.X - center.X) < 0.05f && p.Y - center.Y < 0.08f), "short lines on the hand");
        }
        var hips = new[] { glb.Origin("LegL"), glb.Origin("LegR") };
        foreach (var (leg, side) in new[] { ("LegL", -1f), ("LegR", 1f) })
        {
            // A plain straight tube (0.06 m radius, as long as before) from under the body down to
            // the ground, coming out from under the body's base, inside its outline seen from the
            // front (the leg, and its foot, no further out than the base's widest).
            var hip = glb.Origin(leg);
            var tube = glb.Vertices(leg, "Skin");
            Assert.True(tube.Where(p => p.Y > 0.15f && p.Y < bottom).All(p => MathF.Abs(MathF.Sqrt((p.X - hip.X) * (p.X - hip.X) + p.Z * p.Z) - 0.06f) < 0.003f), $"{leg} is not a straight 0.06 m tube");
            Assert.True(tube.Max(p => p.Y) > bottom && tube.Max(p => p.Y) < bottom + 0.16f, $"{leg} does not come out from under the body");
            Assert.InRange(tube.Max(p => p.Y) - tube.Min(p => p.Y), 0.47f, 0.5f);
            float baseR = WidestBetween(skin, bottom, bottom + h * 0.3f);
            Assert.True(tube.Max(p => MathF.Abs(p.X)) < baseR, $"{leg} is not inside the base");
            Assert.True(glb.Vertices("DefaultShoes" + leg).Max(p => MathF.Abs(p.X)) < baseR, $"{leg}'s foot is not inside the base");
            Assert.InRange(bottom / 1.30f, 0.2f, 0.3f);
            // Its foot (the built-in shoe): rounded, on the ground, a little wider than the leg.
            var v = glb.Vertices("DefaultShoes" + leg);
            var lowest = v.Aggregate(Vector3.Min);
            var highest = v.Aggregate(Vector3.Max);
            Assert.InRange(lowest.Y, -0.005f, 0.005f);
            Assert.InRange(highest.X - lowest.X, 0.13f, 0.17f);
            Assert.InRange(highest.Y - lowest.Y, 0.08f, 0.14f);
            Assert.True(highest.Y < bottom - 0.15f, "the feet are at the ends of the legs");
            Assert.True(MathF.Abs((lowest.X + highest.X) / 2 - hip.X) < 0.002f, $"{leg}'s foot is under it");
        }
        Assert.Equal(0f, hips[0].X + hips[1].X, 3);
    }

    /// <summary>Hue in degrees (0..360), saturation and value (0..1) of an RGB color.</summary>
    private static (float Hue, float Saturation, float Value) Hsv(Vector3 c)
    {
        float max = MathF.Max(c.X, MathF.Max(c.Y, c.Z)), min = MathF.Min(c.X, MathF.Min(c.Y, c.Z)), d = max - min;
        float hue = d == 0 ? 0 : max == c.X ? 60 * ((c.Y - c.Z) / d) : max == c.Y ? 60 * ((c.Z - c.X) / d + 2) : 60 * ((c.X - c.Y) / d + 4);
        return ((hue + 360) % 360, max == 0 ? 0 : d / max, max);
    }

    [Theory]
    [InlineData("girl")]
    [InlineData("boy")]
    public void TheCarriedMushroomsAreVivid(string file)
    {
        var glb = new Glb(file);
        // The bow's tips are mushroom-red caps of their own, not the ribbon's color (the grip wrap keeps that).
        foreach (var part in new[] { "BackBow", "HandBowLimbTop", "HandBowLimbBottom" })
        {
            Assert.Contains(Mushrooms.Red, glb.Materials(part));
            Assert.DoesNotContain("Ribbon", glb.Materials(part));
        }
        Assert.Contains("Ribbon", glb.Materials("HandBow"));
        Assert.Equal(new[] { "Fletch", Mushrooms.PoisonCap, Mushrooms.PoisonSpots, "Shaft", Mushrooms.Stem },
            glb.Materials("NockArrow").Distinct().OrderBy(m => m, StringComparer.Ordinal));
        foreach (var m in new[] { Mushrooms.Red, Mushrooms.PoisonCap, Mushrooms.PoisonSpots, Mushrooms.Stem })
            Assert.True(Vector3.Distance(Mushrooms.Color(m)!.Value, glb.Color(m)) < 0.002f, m);
        Assert.Null(Mushrooms.Color("Ribbon"));

        // A pure tomato red: saturated and bright, not pink or brownish (the old tips were the ribbon's pinkish red).
        var red = Hsv(Mushrooms.RedColor);
        Assert.True(red.Hue < 8 || red.Hue > 355, $"red hue {red.Hue}");
        Assert.InRange(red.Saturation, 0.9f, 1f);
        Assert.InRange(red.Value, 0.9f, 1f);
        // Still poison purple, but richer and brighter than the old (0.56, 0.24, 0.72).
        var purple = Hsv(Mushrooms.PoisonCapColor);
        var oldPurple = Hsv(new Vector3(0.56f, 0.24f, 0.72f));
        Assert.InRange(purple.Hue, 265f, 290f);
        Assert.True(purple.Saturation >= 0.85f && purple.Saturation > oldPurple.Saturation + 0.15f, $"purple saturation {purple.Saturation}");
        Assert.True(purple.Value >= 0.9f && purple.Value > oldPurple.Value, $"purple value {purple.Value}");
        // Glowing green spots; a clean light cream stem (light, warm, barely tinted).
        var spots = Hsv(Mushrooms.PoisonSpotsColor);
        Assert.InRange(spots.Hue, 90f, 130f);
        Assert.True(spots.Saturation >= 0.7f && spots.Value >= 0.95f);
        var stem = Mushrooms.StemColor;
        Assert.True(stem.X >= stem.Y && stem.Y >= stem.Z && stem.Z >= 0.8f && stem.X >= 0.98f, $"stem {stem}");

        // Every other color stays as it was.
        var ribbon = file == "girl" ? new Vector3(0.95f, 0.24f, 0.36f) : new Vector3(0.92f, 0.22f, 0.24f);
        Assert.True(Vector3.Distance(ribbon, glb.Color("Ribbon")) < 0.002f);
        Assert.True(Vector3.Distance(new Vector3(0.62f, 0.38f, 0.2f), glb.Color("Wood")) < 0.002f);
        Assert.True(Vector3.Distance(new Vector3(0.78f, 0.6f, 0.38f), glb.Color("Shaft")) < 0.002f);
        Assert.True(Vector3.Distance(new Vector3(0.5f, 0.85f, 0.45f), glb.Color("Fletch")) < 0.002f);
    }

    [Fact]
    public void MushroomCapsAreGlossyAndRimLitAndTheGlowSpotsGlowTwiceAsBright()
    {
        Assert.True(Mushrooms.IsCap(Mushrooms.Red));
        Assert.True(Mushrooms.IsCap(Mushrooms.PoisonCap));
        foreach (var other in new[] { Mushrooms.Stem, Mushrooms.PoisonSpots, "Ribbon", "Wood", "Skin", "" })
            Assert.False(Mushrooms.IsCap(other), other);
        Assert.True(Mushrooms.Glows(Mushrooms.PoisonSpots));
        Assert.False(Mushrooms.Glows(Mushrooms.PoisonCap));
        Assert.False(Mushrooms.Glows("EyeShine"));
        Assert.InRange(Mushrooms.Gloss, 0.3f, 1f);
        Assert.InRange(Mushrooms.RimLight, 0.15f, 0.6f);
        Assert.Equal(2 * 0.6f, Mushrooms.GlowLight, 3);
    }

    [Fact]
    public void WornModelsAttachToTheRigsHeadBodyAndLegParts()
    {
        foreach (var part in new[] { "Head", "Body", "LegL", "LegR" })
            Assert.True(Rig.IsAttachPoint(part), part);
        foreach (var other in new[] { "ArmL", "HandR", "BackBow", "Hair", "BodyGirl", "BodyBoy", "" })
            Assert.False(Rig.IsAttachPoint(other), other);
        Assert.Equal("DefaultTop", Rig.NodePrefix(BuiltinPieces.Top));
        Assert.Equal("DefaultShoes", Rig.NodePrefix(BuiltinPieces.Shoes));
        // A model's object goes on the part of its name; one shaped for one body only on that character's body.
        foreach (var part in new[] { "Head", "Body", "LegL", "LegR" })
            for (int character = 0; character < 2; character++)
                Assert.Equal(part, Attach.PartFor(part, character));
        Assert.Equal(Rig.Body, Attach.PartFor(Attach.BodyGirl, 0));
        Assert.Equal("", Attach.PartFor(Attach.BodyGirl, 1));
        Assert.Equal(Rig.Body, Attach.PartFor(Attach.BodyBoy, 1));
        Assert.Equal("", Attach.PartFor(Attach.BodyBoy, 0));
        Assert.Equal("", Attach.PartFor("ArmL", 0));
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
}
