using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Numerics;
using Fungtopia.Map;
using Fungtopia.Monsters;
using Fungtopia.Player;
using Fungtopia.Save;
using Fungtopia.Shell;
using Fungtopia.StartScreen;
using Xunit;

namespace Fungtopia.Pet.Tests;

public class PetModuleTests
{
    private const double Step = 1.0 / 60;

    /// <summary>Fake of the shell's bus: delivers to subscribers and records what the pet publishes.</summary>
    private sealed class FakeBus : IMessageBus
    {
        private readonly Dictionary<Type, List<object>> _handlers = new();
        public readonly List<PetState> States = new();
        public readonly List<(long Tick, HitMonster Hit)> Hits = new();
        public readonly List<OwnedPets> Owned = new();
        public readonly List<SavePart> Parts = new();
        /// <summary>Every message published, by type name, to see that restoring has no side effects.</summary>
        public readonly List<Type> Published = new();
        public long Tick;

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
                _handlers[typeof(T)] = list = new List<object>();
            list.Add(handler);
        }

        public void Publish<T>(in T message) where T : struct
        {
            if (message is PetState state)
                States.Add(state);
            else if (message is HitMonster hit)
                Hits.Add((Tick, hit));
            else if (message is OwnedPets owned)
                Owned.Add(owned);
            else if (message is SavePart part)
                Parts.Add(part);
            Published.Add(typeof(T));
            if (_handlers.TryGetValue(typeof(T), out var list))
                foreach (var handler in list)
                    ((IMessageHandler<T>)handler).Handle(in message);
        }
    }

    /// <summary>A bus that drops everything.</summary>
    private sealed class NullBus : IMessageBus
    {
        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct { }
        public void Publish<T>(in T message) where T : struct { }
    }

    /// <summary>The middle clearing: open ground with no trunks for many meters around.</summary>
    private static readonly Vector3 Middle = new(0f, 0f, -5f);

    private readonly FakeBus _bus = new();
    private readonly PetModule _pet = new();
    private PlayerState _player;
    private readonly List<MonsterState> _monsters = new();
    private long _tick;

    public PetModuleTests()
    {
        _pet.Start(_bus);
        _bus.Publish(new GameStart(GameStart.Girl, GameStart.Cat));
        _player = PlayerAt(Middle);
    }

    private static Vector3 OnGround(float x, float z) => new(x, Ground.HeightAt(x, z), z);

    private static Vector3 OnGround(Vector3 v) => OnGround(v.X, v.Z);

    private static PlayerState PlayerAt(Vector3 feet, bool alive = true, float facing = 0f) =>
        new(OnGround(feet), facing, Vector3.Zero, true, alive ? 10 : 0, 10, alive, CharacterKind.Girl);

    private static MonsterState Monster(int id, Vector3 feet, bool alive = true, float radius = 0.5f) =>
        new(id, MonsterKind.Ant, OnGround(feet), radius, alive ? 5 : 0, 5, alive, false);

    /// <summary>One tick as the host runs it: the player and monsters publish, then the pet ticks.</summary>
    private void TickOnce()
    {
        _bus.Tick = _tick;
        _bus.Publish(_player);
        foreach (var m in _monsters)
            _bus.Publish(m);
        _pet.Tick(new TickContext(_tick++, Step));
    }

    private void Run(double seconds, Action? eachTick = null)
    {
        int ticks = (int)Math.Round(seconds / Step);
        for (int i = 0; i < ticks; i++)
        {
            eachTick?.Invoke();
            TickOnce();
        }
    }

    private static float Flat(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X, dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private float ToPlayer => Flat(_pet.Position, _player.Position);

    private void AssertOnGround()
    {
        Assert.Equal(Ground.HeightAt(_pet.Position.X, _pet.Position.Z), _pet.Position.Y, 3);
    }

    // ---- Start, settings and state ----

    [Theory]
    [InlineData(GameStart.Cat, PetKind.Cat)]
    [InlineData(GameStart.Dog, PetKind.Dog)]
    public void TheChosenStarterIsOwnedAndActive(int starter, PetKind kind)
    {
        var pet = new PetModule();
        var bus = new FakeBus();
        pet.Start(bus);
        bus.Publish(_player);
        bus.Publish(new GameStart(GameStart.Boy, starter));
        Assert.True(pet.Started);
        Assert.Equal(kind, pet.Kind);
        pet.Tick(new TickContext(0, Step));
        var owned = bus.Owned[^1];
        Assert.True(owned.Owns(kind));
        Assert.Equal(kind, owned.Active);
        Assert.False(owned.AtHome(kind));
        Assert.False(owned.Owns(kind == PetKind.Cat ? PetKind.Dog : PetKind.Cat));
        Assert.False(owned.Crow);
        Assert.Equal(kind, bus.States[^1].Kind);
        // It comes out beside the player, at once.
        Assert.True(pet.Teleported);
        Assert.InRange(Flat(pet.Position, _player.Position), 1f, 2.5f);
    }

    [Fact]
    public void NoPetBeforeTheGameStarts()
    {
        var pet = new PetModule();
        var bus = new FakeBus();
        pet.Start(bus);
        Assert.False(pet.Started);
        Assert.False(pet.Owned.Cat || pet.Owned.Dog || pet.Owned.Crow);
        var start = pet.Position;
        for (int i = 0; i < 60; i++)
        {
            bus.Publish(_player);
            bus.Publish(Monster(1, Middle + new Vector3(1f, 0f, 0f)));
            bus.Publish(new AdoptPet(PetKind.Crow));
            bus.Publish(new SetActivePet(PetKind.Dog));
            bus.Publish(new TeleportPlayer(OnGround(Middle), 0f));
            pet.Tick(new TickContext(i, Step));
        }
        Assert.Empty(bus.States);
        Assert.Empty(bus.Owned);
        Assert.Empty(bus.Hits);
        Assert.Equal(start, pet.Position);
        Assert.False(pet.Owned.Cat || pet.Owned.Dog || pet.Owned.Crow);

        // Then the start screen's choice comes: only it is owned.
        bus.Publish(new GameStart(GameStart.Girl, GameStart.Dog));
        pet.Tick(new TickContext(60, Step));
        Assert.Equal(new OwnedPets(false, true, false, PetKind.Dog), bus.Owned[^1]);
        Assert.Single(bus.States);
    }

    [Fact]
    public void OnlyTheFirstGameStartCounts()
    {
        _bus.Publish(new GameStart(GameStart.Girl, GameStart.Dog));
        TickOnce();
        Assert.Equal(new OwnedPets(true, false, false, PetKind.Cat), _bus.Owned[^1]);
    }

    [Fact]
    public void AnUnknownStarterIsTheCat()
    {
        var pet = new PetModule();
        pet.Start(new FakeBus());
        pet.Handle(new GameStart(GameStart.Girl, 7));
        Assert.Equal(new OwnedPets(true, false, false, PetKind.Cat), pet.Owned);
    }

    [Fact]
    public void WaitsBesideTheHomeDoorBeforeThePlayerIsKnown()
    {
        var pet = new PetModule();
        pet.Start(new NullBus());
        pet.Handle(new GameStart(GameStart.Girl, GameStart.Cat));
        var door = Places.HomeDoor.Position;
        Assert.InRange(Flat(pet.Position, door), 1f, 5f);
        Assert.True(Areas.Walkable(pet.Position.X, pet.Position.Z, PetModule.Radius));
        Assert.False(Ground.Blocks(pet.Position.X, pet.Position.Z, PetModule.Radius));
        Assert.Equal(Ground.HeightAt(pet.Position.X, pet.Position.Z), pet.Position.Y, 3);
    }

    [Fact]
    public void PublishesItsStateOnceEveryTick()
    {
        _pet.PlaceAt(Middle + new Vector3(3f, 0f, 0f), 1f);
        Run(1.0);
        Assert.Equal(60, _bus.States.Count);
        var last = _bus.States[^1];
        Assert.Equal(_pet.Position, last.Position);
        Assert.Equal(_pet.Facing, last.Facing);
        Assert.False(last.Attacking);
    }

    [Fact]
    public void TickBeforeStartThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new PetModule().Tick(new TickContext(0, Step)));
    }

    [Fact]
    public void WaitsWhereItIsUntilThePlayerIsKnown()
    {
        var pet = new PetModule();
        var bus = new FakeBus();
        pet.Start(bus);
        bus.Publish(new GameStart(GameStart.Girl, GameStart.Cat));
        var start = pet.Position;
        for (int i = 0; i < 60; i++)
            pet.Tick(new TickContext(i, Step));
        Assert.Equal(start, pet.Position);
        Assert.Equal(60, bus.States.Count);
    }

    // ---- Following ----

    [Fact]
    public void RunsToThePlayerAndStopsAboutTwoMetersAway()
    {
        _pet.PlaceAt(Middle + new Vector3(10f, 0f, 3f));
        Run(4.0);
        Assert.InRange(ToPlayer, 1.6f, 2.6f);
        AssertOnGround();
        // Then it stays put instead of jittering.
        var settled = _pet.Position;
        Run(1.0);
        Assert.True(Flat(settled, _pet.Position) < 0.05f);
    }

    [Fact]
    public void FacesWhereItRuns()
    {
        _pet.PlaceAt(Middle + new Vector3(10f, 0f, 0f), 0f);
        Run(0.5);
        // Running toward -X: facing +π/2 (the direction (-sin, -cos) is (-1, 0)).
        Assert.Equal(MathF.PI / 2f, _pet.Facing, 2);
    }

    [Fact]
    public void DoesNotMoveWhenAlreadyCloseToThePlayer()
    {
        _pet.PlaceAt(Middle + new Vector3(1.8f, 0f, 0f));
        var start = _pet.Position;
        Run(1.0);
        Assert.True(Flat(start, _pet.Position) < 0.01f);
    }

    [Fact]
    public void KeepsUpWithAWalkingPlayer()
    {
        _player = PlayerAt(Middle + new Vector3(-10f, 0f, 0f));
        _pet.PlaceAt(Middle + new Vector3(-12f, 0f, 0f));
        float farthest = 0f;
        // The player walks 20 m east across the clearing at 5 m/s.
        Run(4.0, () =>
        {
            _player = PlayerAt(_player.Position + new Vector3(5f * (float)Step, 0f, 0f));
            farthest = MathF.Max(farthest, ToPlayer);
        });
        Assert.True(farthest < 4f, $"fell {farthest} m behind");
        Run(2.0);
        Assert.InRange(ToPlayer, 1.6f, 2.6f);
        AssertOnGround();
    }

    [Fact]
    public void CatchesUpFasterThanItTrotsWhenFar()
    {
        _pet.PlaceAt(Middle + new Vector3(15f, 0f, 0f));
        Run(0.5);
        float farSpeed = (15f - Flat(_pet.Position, Middle)) / 0.5f;
        Assert.True(farSpeed > PlayerSpeed + 2f, $"catch-up speed {farSpeed} m/s");
        Assert.True(farSpeed <= PetModule.CatchUpSpeed + 0.01f);
    }

    private const float PlayerSpeed = 5f;

    // ---- Teleporting ----

    [Fact]
    public void TeleportsBesideThePlayerWhenOver30MetersAway()
    {
        _pet.PlaceAt(Middle + new Vector3(0f, 0f, 35f));
        TickOnce();
        Assert.True(ToPlayer < 3f, $"{ToPlayer} m from the player");
        Assert.False(Ground.Blocks(_pet.Position.X, _pet.Position.Z, PetModule.Radius));
        AssertOnGround();
    }

    [Fact]
    public void RunsInsteadOfTeleportingWithin30Meters()
    {
        _player = PlayerAt(new Vector3(0f, 0f, 84f));
        _pet.PlaceAt(new Vector3(0f, 0f, 84f - 25f));
        TickOnce();
        Assert.True(ToPlayer > 24f);
    }

    [Fact]
    public void TeleportsWhenThePlayerJumpsFarAway()
    {
        // The player comes back at the start after defeat: far from the pet.
        _pet.PlaceAt(Middle + new Vector3(2f, 0f, 0f));
        Run(0.5);
        _player = PlayerAt(Places.HomeDoor.Position);
        TickOnce();
        Assert.True(ToPlayer < 3f);
        Assert.True(_pet.Teleported);
    }

    // ---- Choosing a target ----

    [Fact]
    public void PicksTheNearestAliveMonsterWithin8MetersOfThePlayer()
    {
        _pet.PlaceAt(Middle + new Vector3(2f, 0f, 0f));
        _monsters.Add(Monster(1, Middle + new Vector3(-6f, 0f, 0f)));
        _monsters.Add(Monster(2, Middle + new Vector3(0f, 0f, 4f)));
        _monsters.Add(Monster(3, Middle + new Vector3(0f, 0f, -2f), alive: false));
        TickOnce();
        Assert.Equal(2, _pet.TargetId);
        Assert.True(_bus.States[^1].Attacking);
    }

    [Fact]
    public void IgnoresMonstersFartherThan8MetersFromThePlayer()
    {
        _pet.PlaceAt(Middle + new Vector3(9f, 0f, 0f));
        // Right next to the pet, but 9.5 m from the player.
        _monsters.Add(Monster(1, Middle + new Vector3(9.5f, 0f, 0f)));
        Run(1.0);
        Assert.Equal(-1, _pet.TargetId);
        Assert.False(_bus.States[^1].Attacking);
        Assert.Empty(_bus.Hits);
    }

    [Fact]
    public void ForgetsMonstersThatAreNoLongerPublished()
    {
        _pet.PlaceAt(Middle + new Vector3(2f, 0f, 0f));
        _monsters.Add(Monster(1, Middle + new Vector3(-6f, 0f, 0f)));
        TickOnce();
        Assert.Equal(1, _pet.TargetId);
        _monsters.Clear();
        TickOnce();
        Assert.Equal(-1, _pet.TargetId);
    }

    // ---- Biting ----

    [Fact]
    public void RunsToTheMonsterAndBitesItEverySecond()
    {
        _pet.PlaceAt(Middle + new Vector3(2f, 0f, 0f));
        var monster = Middle + new Vector3(-6f, 0f, 2f);
        _monsters.Add(Monster(7, monster));
        Run(5.0);

        Assert.True(Flat(_pet.Position, monster) < 0.5f + PetModule.Radius + PetModule.BiteReach + 0.01f);
        Assert.InRange(_bus.Hits.Count, 3, 5);
        foreach (var (_, hit) in _bus.Hits)
        {
            Assert.Equal(7, hit.MonsterId);
            Assert.Equal(1, hit.Damage);
            Assert.Equal(0f, hit.PoisonDamagePerSecond);
            Assert.Equal(0f, hit.PoisonSeconds);
            Assert.Equal(1, hit.Source);
        }
        for (int i = 1; i < _bus.Hits.Count; i++)
            Assert.Equal(60, _bus.Hits[i].Tick - _bus.Hits[i - 1].Tick);
        // The first bite comes as soon as it gets there: about 6 m at catch-up speed, well under 2 s.
        Assert.True(_bus.Hits[0].Tick < 120);
        // It looks at what it bites.
        var toMonster = Vector2.Normalize(new Vector2(monster.X - _pet.Position.X, monster.Z - _pet.Position.Z));
        var facing = new Vector2(-MathF.Sin(_pet.Facing), -MathF.Cos(_pet.Facing));
        Assert.True(Vector2.Dot(toMonster, facing) > 0.95f);
    }

    [Fact]
    public void DoesNotBiteBeforeItReachesTheMonster()
    {
        _pet.PlaceAt(Middle + new Vector3(2f, 0f, 0f));
        _monsters.Add(Monster(7, Middle + new Vector3(-7f, 0f, 0f)));
        Run(0.4);
        Assert.Empty(_bus.Hits);
        Assert.True(_pet.Attacking);
    }

    [Fact]
    public void ChasesAMovingTarget()
    {
        _pet.PlaceAt(Middle + new Vector3(2f, 0f, 0f));
        var monster = Middle + new Vector3(-5f, 0f, 0f);
        _monsters.Add(Monster(4, monster));
        Run(2.0);
        Assert.NotEmpty(_bus.Hits);
        // The monster scurries 3 m away; the pet follows it there and keeps biting.
        monster += new Vector3(0f, 0f, 3f);
        _monsters[0] = Monster(4, monster);
        int before = _bus.Hits.Count;
        Run(2.0);
        Assert.True(Flat(_pet.Position, monster) < 0.5f + PetModule.Radius + PetModule.BiteReach + 0.01f);
        Assert.True(_bus.Hits.Count > before);
    }

    // ---- Returning ----

    [Fact]
    public void ReturnsToThePlayerWhenItsTargetIsDefeated()
    {
        _pet.PlaceAt(Middle + new Vector3(2f, 0f, 0f));
        var monster = Middle + new Vector3(-7f, 0f, 0f);
        _monsters.Add(Monster(5, monster));
        Run(3.0);
        Assert.NotEmpty(_bus.Hits);
        _monsters[0] = Monster(5, monster, alive: false);
        int hits = _bus.Hits.Count;
        Run(3.0);
        Assert.Equal(hits, _bus.Hits.Count);
        Assert.Equal(-1, _pet.TargetId);
        Assert.False(_bus.States[^1].Attacking);
        Assert.InRange(ToPlayer, 1.6f, 2.6f);
    }

    [Fact]
    public void ReturnsWhenThePlayerWalksAwayFromItsTarget()
    {
        _pet.PlaceAt(Middle + new Vector3(2f, 0f, 0f));
        _monsters.Add(Monster(5, Middle + new Vector3(-7f, 0f, 0f)));
        Run(2.0);
        Assert.Equal(5, _pet.TargetId);
        // The player walks 10 m east: the monster is now 17 m from the player.
        _player = PlayerAt(Middle + new Vector3(10f, 0f, 0f));
        Run(4.0);
        Assert.Equal(-1, _pet.TargetId);
        Assert.InRange(ToPlayer, 1.6f, 2.6f);
    }

    [Fact]
    public void GoesForTheNextMonsterAfterATargetIsDefeated()
    {
        _pet.PlaceAt(Middle + new Vector3(2f, 0f, 0f));
        _monsters.Add(Monster(1, Middle + new Vector3(-4f, 0f, 0f)));
        _monsters.Add(Monster(2, Middle + new Vector3(5f, 0f, 3f)));
        Run(2.0);
        Assert.Equal(1, _pet.TargetId);
        _monsters[0] = Monster(1, Middle + new Vector3(-4f, 0f, 0f), alive: false);
        Run(3.0);
        Assert.Equal(2, _pet.TargetId);
        Assert.Equal(2, _bus.Hits[^1].Hit.MonsterId);
    }

    // ---- The defeated player ----

    [Fact]
    public void SitsByThePlayerWhileTheyAreDefeated()
    {
        _pet.PlaceAt(Middle + new Vector3(6f, 0f, 0f));
        _monsters.Add(Monster(1, Middle + new Vector3(-3f, 0f, 0f)));
        _player = PlayerAt(Middle, alive: false);
        Run(3.0);
        Assert.True(_pet.Sitting);
        Assert.True(ToPlayer < 1.6f, $"{ToPlayer} m from the player");
        Assert.Equal(-1, _pet.TargetId);
        Assert.Empty(_bus.Hits);
        var sat = _pet.Position;
        Run(1.0);
        Assert.Equal(sat, _pet.Position);

        // When the player is back, so is the pet.
        _player = PlayerAt(Middle);
        Run(0.1);
        Assert.False(_pet.Sitting);
        Run(2.0);
        Assert.Equal(1, _pet.TargetId);
    }

    // ---- Obstacles and terrain ----

    /// <summary>
    /// A giant mushroom trunk with open ground 4 m beyond it on both sides along X, so the player
    /// can stand on one side and the pet start on the other.
    /// </summary>
    private static Circle TrunkWithRoomAround()
    {
        var all = Ground.Obstacles;
        for (int i = 0; i < Ground.ForestTrunkCount; i++)
        {
            var o = all[i];
            float gap = o.Radius + 4f;
            if (o.Radius > 1.4f && Areas.Forest.Contains(o.X - gap, o.Z, 2f) && Areas.Forest.Contains(o.X + gap, o.Z, 2f)
                && !Ground.Blocks(o.X - gap, o.Z, 1f) && !Ground.Blocks(o.X + gap, o.Z, 1f))
                return o;
        }
        throw new InvalidOperationException("no trunk with room around it");
    }

    [Fact]
    public void RunsAroundGiantMushroomsInsteadOfThroughThem()
    {
        var trunk = TrunkWithRoomAround();
        float gap = trunk.Radius + 4f;
        _player = PlayerAt(new Vector3(trunk.X - gap, 0f, trunk.Z));
        _pet.PlaceAt(new Vector3(trunk.X + gap, 0f, trunk.Z));
        float closest = float.MaxValue;
        Run(6.0, () =>
        {
            closest = MathF.Min(closest, Flat(_pet.Position, new Vector3(trunk.X, 0f, trunk.Z)));
            Assert.False(Ground.Blocks(_pet.Position.X, _pet.Position.Z, PetModule.Radius - 0.01f),
                $"inside a trunk at {_pet.Position}");
            AssertOnGround();
        });
        // It steers around with room to spare instead of scraping along the trunk.
        Assert.True(closest >= trunk.Radius + PetModule.Radius + 0.1f, $"came within {closest} m of the trunk's center");
        Assert.InRange(ToPlayer, 1.6f, 2.6f);
    }

    [Fact]
    public void StaysOffTrunksWhileChasingAroundTheForest()
    {
        // The player wanders through the trees; the pet follows through them without overlapping any.
        var trunk = TrunkWithRoomAround();
        var center = new Vector3(trunk.X, 0f, trunk.Z);
        _player = PlayerAt(center + new Vector3(trunk.Radius + 4f, 0f, 0f));
        _pet.PlaceAt(_player.Position + new Vector3(1f, 0f, 1f));
        float angle = 0f;
        Run(12.0, () =>
        {
            // Circle the trunk at 4 m/s, 4 m out from its edge.
            angle += 4f / (trunk.Radius + 4f) * (float)Step;
            var feet = center + new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)) * (trunk.Radius + 4f);
            if (!Ground.Blocks(feet.X, feet.Z, 0.4f))
                _player = PlayerAt(feet);
            Assert.False(Ground.Blocks(_pet.Position.X, _pet.Position.Z, PetModule.Radius - 0.01f));
            Assert.True(ToPlayer < 6f, $"{ToPlayer} m behind");
        });
    }

    [Fact]
    public void FollowsTheTerrainUpAndDownHills()
    {
        _player = PlayerAt(new Vector3(0f, 0f, 84f));
        _pet.PlaceAt(new Vector3(0f, 0f, 84f - 20f));
        float lowest = float.MaxValue, highest = float.MinValue;
        Run(4.0, () =>
        {
            AssertOnGround();
            lowest = MathF.Min(lowest, _pet.Position.Y);
            highest = MathF.Max(highest, _pet.Position.Y);
        });
        Assert.True(highest - lowest > 0.05f, "the path should not be flat");
    }

    [Fact]
    public void StaysOnWalkableGroundWhenThePlayerIsBeyondIt()
    {
        // The east edge of Mushroom Town; the (fake) player stands in the cliffs beyond it.
        float z = FreeZAlongTownsEastEdge();
        float edge = Areas.Town.MaxX;
        _player = PlayerAt(new Vector3(edge + 6f, 0f, z));
        _pet.PlaceAt(new Vector3(edge - 4f, 0f, z));
        Run(3.0, () =>
        {
            Assert.True(Areas.Walkable(_pet.Position.X, _pet.Position.Z, PetModule.Radius - 0.01f),
                $"off the walkable ground at {_pet.Position}");
            AssertOnGround();
        });
        // It went as far as it could.
        Assert.True(_pet.Position.X > edge - 1.5f, $"stopped at {_pet.Position}");
    }

    private static float FreeZAlongTownsEastEdge()
    {
        for (float z = Areas.Town.MinZ + 10f; z < Areas.Town.MaxZ - 10f; z += 1f)
        {
            bool free = true;
            for (float x = Areas.Town.MaxX - 6f; x <= Areas.Town.MaxX; x += 0.25f)
                free &= Areas.Walkable(x, z, 0f) && !Ground.Blocks(x, z, 1f) && !Ground.Blocks(x, z + 1f, 1f) && !Ground.Blocks(x, z - 1f, 1f);
            if (free && Areas.Walkable(Areas.Town.MaxX - 0.5f, z, PetModule.Radius))
                return z;
        }
        throw new InvalidOperationException("no free spot along the town's east edge");
    }

    /// <summary>The south road's center line, from the forest to Mushroom Town.</summary>
    private static List<Vector2> SouthRoad()
    {
        var road = new List<Vector2>();
        var all = new List<Vector2>();
        foreach (var p in Areas.RoadPoints)
        {
            if (float.IsNaN(p.X))
            {
                if (all.Count > 0 && all[0].Y > 0f)
                    road = all;
                all = new List<Vector2>();
                continue;
            }
            all.Add(p);
        }
        if (all.Count > 0 && all[0].Y > 0f)
            road = all;
        if (road[0].Y > road[^1].Y)
            road.Reverse();
        return road;
    }

    [Fact]
    public void FollowsThePlayerAlongTheRoadIntoTown()
    {
        var road = SouthRoad();
        _player = PlayerAt(new Vector3(road[0].X, 0f, road[0].Y));
        _pet.PlaceAt(_player.Position + new Vector3(1.5f, 0f, -1f));
        int next = 1;
        float farthest = 0f;
        // The player walks the whole road at 5 m/s, then on into the town.
        Run(30.0, () =>
        {
            if (next < road.Count)
            {
                var at = new Vector2(_player.Position.X, _player.Position.Z);
                var to = road[next] - at;
                float step = 5f * (float)Step;
                var moved = to.Length() <= step ? road[next++] : at + Vector2.Normalize(to) * step;
                _player = PlayerAt(new Vector3(moved.X, 0f, moved.Y));
            }
            farthest = MathF.Max(farthest, ToPlayer);
            Assert.False(Ground.Blocks(_pet.Position.X, _pet.Position.Z, PetModule.Radius - 0.01f));
            Assert.True(Areas.Walkable(_pet.Position.X, _pet.Position.Z, PetModule.Radius - 0.01f),
                $"off the walkable ground at {_pet.Position}");
            AssertOnGround();
        });
        Assert.Equal(road.Count, next);
        Assert.False(_pet.Teleported);
        Assert.True(farthest < 5f, $"fell {farthest} m behind");
        Assert.Equal(Area.Town, Areas.At(_pet.Position.X, _pet.Position.Z));
        Assert.InRange(ToPlayer, 1.6f, 2.6f);
    }

    // ---- Owning pets ----

    [Fact]
    public void StartsOwningOnlyTheStarterAndPublishesTheOwnedPetsEveryTick()
    {
        Run(0.5);
        Assert.Equal(30, _bus.Owned.Count);
        var owned = _bus.Owned[^1];
        Assert.True(owned.Cat);
        Assert.False(owned.Dog);
        Assert.False(owned.Crow);
        Assert.Equal(PetKind.Cat, owned.Active);
    }

    [Fact]
    public void AdoptingAPetAddsItAndMakesItActive()
    {
        TickOnce();
        _bus.Publish(new AdoptPet(PetKind.Crow));
        TickOnce();
        var owned = _bus.Owned[^1];
        Assert.True(owned.Cat);
        Assert.True(owned.Crow);
        Assert.False(owned.Dog);
        Assert.Equal(PetKind.Crow, owned.Active);
        Assert.Equal(PetKind.Crow, _bus.States[^1].Kind);
        Assert.False(owned.AtHome(PetKind.Crow));
        Assert.True(owned.AtHome(PetKind.Cat));

        // Adopting one you own already just makes it active.
        _bus.Publish(new AdoptPet(PetKind.Cat));
        TickOnce();
        Assert.Equal(new OwnedPets(true, false, true, PetKind.Cat), _bus.Owned[^1]);
    }

    [Fact]
    public void SetActivePetSwitchesOnlyToAnOwnedPet()
    {
        _bus.Publish(new AdoptPet(PetKind.Crow));
        _bus.Publish(new SetActivePet(PetKind.Cat));
        TickOnce();
        Assert.Equal(PetKind.Cat, _bus.Owned[^1].Active);
        Assert.Equal(PetKind.Cat, _bus.States[^1].Kind);

        // The Dog is not owned: nothing changes.
        _bus.Publish(new SetActivePet(PetKind.Dog));
        _bus.Publish(new SetActivePet((PetKind)42));
        _bus.Publish(new AdoptPet((PetKind)42));
        TickOnce();
        Assert.Equal(new OwnedPets(true, false, true, PetKind.Cat), _bus.Owned[^1]);
        Assert.Equal(PetKind.Cat, _bus.States[^1].Kind);

        _bus.Publish(new SetActivePet(PetKind.Crow));
        TickOnce();
        Assert.Equal(PetKind.Crow, _bus.Owned[^1].Active);
    }

    [Fact]
    public void OnlyTheActivePetFollowsAndFights()
    {
        _pet.PlaceAt(Middle + new Vector3(2f, 0f, 0f));
        var monster = Middle + new Vector3(-4f, 0f, 2f);
        _monsters.Add(Monster(7, monster));
        Run(3.0);
        Assert.All(_bus.States, s => Assert.Equal(PetKind.Cat, s.Kind));
        Assert.True(_bus.Hits.Count >= 2);
        Assert.Equal(60, _bus.Hits[^1].Tick - _bus.Hits[^2].Tick);

        // The Crow is adopted: the cat goes home, the crow appears beside the player and takes over.
        _bus.Publish(new AdoptPet(PetKind.Crow));
        Assert.True(Flat(_pet.Position, _player.Position) < 2f);
        int before = _bus.Hits.Count, states = _bus.States.Count;
        Run(4.0);
        // Still exactly one pet out: one state a tick, all of it the crow's.
        Assert.Equal(states + 240, _bus.States.Count);
        for (int i = states; i < _bus.States.Count; i++)
            Assert.Equal(PetKind.Crow, _bus.States[i].Kind);
        Assert.True(_bus.Owned[^1].AtHome(PetKind.Cat));
        // Its hits come at the crow's pace, not the cat's.
        Assert.True(_bus.Hits.Count - before >= 3);
        for (int i = before + 1; i < _bus.Hits.Count; i++)
            Assert.Equal(48, _bus.Hits[i].Tick - _bus.Hits[i - 1].Tick);

        // When the monster is defeated the crow comes back to the player, not the cat.
        _monsters.Clear();
        _player = PlayerAt(Middle + new Vector3(6f, 0f, 0f));
        Run(3.0);
        Assert.Equal(PetKind.Crow, _bus.States[^1].Kind);
        Assert.True(ToPlayer < 2f);
    }

    // ---- The Mushroom Crow ----

    private void AdoptCrow()
    {
        _bus.Publish(new AdoptPet(PetKind.Crow));
        Assert.Equal(PetKind.Crow, _pet.Kind);
    }

    private float Height => _pet.Position.Y - Ground.HeightAt(_pet.Position.X, _pet.Position.Z);

    [Fact]
    public void TheCrowHoversAboutTwoMetersUpBesideThePlayer()
    {
        TickOnce();
        AdoptCrow();
        _pet.PlaceAt(Middle + new Vector3(-8f, 0f, 6f));
        Assert.Equal(PetModule.HoverHeight, Height, 2);
        // The player faces -Z: their right is +X.
        Run(4.0, () => Assert.InRange(Height, 1.5f, 2.5f));
        Assert.Equal(2f, Height, 2);
        Assert.Equal(2f, _bus.States[^1].Position.Y - Ground.HeightAt(_pet.Position.X, _pet.Position.Z), 2);
        var offset = _pet.Position - _player.Position;
        Assert.InRange(offset.X, 0.7f, 1.7f);
        Assert.InRange(MathF.Abs(offset.Z), 0f, 0.5f);

        // The player turns around (now their right is -X) and walks on: the crow keeps at their side.
        _player = PlayerAt(Middle + new Vector3(0f, 0f, 6f), facing: MathF.PI);
        Run(3.0);
        offset = _pet.Position - _player.Position;
        Assert.InRange(offset.X, -1.7f, -0.7f);
        Assert.InRange(MathF.Abs(offset.Z), 0f, 0.5f);
        Assert.Equal(2f, Height, 2);
    }

    [Fact]
    public void TheCrowSwoopsDownAndPecksEveryEightTenthsOfASecond()
    {
        TickOnce();
        AdoptCrow();
        _pet.PlaceAt(Middle + new Vector3(2f, 0f, 0f));
        var monster = Middle + new Vector3(-6f, 0f, 2f);
        _monsters.Add(Monster(3, monster));
        float lowest = float.MaxValue;
        var heightsAtPecks = new List<float>();
        for (int i = 0; i < 300; i++)
        {
            int hits = _bus.Hits.Count;
            TickOnce();
            lowest = MathF.Min(lowest, Height);
            if (_bus.Hits.Count > hits)
                heightsAtPecks.Add(Height);
            // Still high in the air while it flies over to the monster.
            if (i == 10)
                Assert.True(Height > 1.5f, $"{Height} m up");
        }
        Assert.InRange(_bus.Hits.Count, 5, 7);
        foreach (var (_, hit) in _bus.Hits)
        {
            Assert.Equal(3, hit.MonsterId);
            Assert.Equal(1, hit.Damage);
            Assert.Equal(PetModule.BiteSource, hit.Source);
        }
        for (int i = 1; i < _bus.Hits.Count; i++)
            Assert.Equal(48, _bus.Hits[i].Tick - _bus.Hits[i - 1].Tick);
        // It pecks only once it has swooped down.
        Assert.All(heightsAtPecks, h => Assert.True(h <= PetModule.PeckHeight + 0.01f, $"pecked from {h} m"));
        Assert.True(Flat(_pet.Position, monster) < 0.5f + PetModule.Radius + PetModule.BiteReach + 0.01f);
        Assert.True(_bus.States[^1].Attacking);

        // The monster is defeated: back up to its hover beside the player.
        _monsters[0] = Monster(3, monster, alive: false);
        Run(3.0);
        Assert.Equal(2f, Height, 2);
        Assert.InRange(ToPlayer, 0.7f, 1.7f);
    }

    [Fact]
    public void TheCrowPecksFasterThanTheCatAndDogBite()
    {
        Assert.Equal(0.8f, PetModule.AttackInterval(PetKind.Crow));
        Assert.Equal(1f, PetModule.AttackInterval(PetKind.Cat));
        Assert.Equal(1f, PetModule.AttackInterval(PetKind.Dog));
    }

    [Fact]
    public void TheCatAndDogStayOnTheGround()
    {
        _pet.PlaceAt(Middle + new Vector3(6f, 0f, 0f));
        Run(2.0, () => Assert.Equal(0f, Height, 3));
        _bus.Publish(new AdoptPet(PetKind.Dog));
        Run(2.0, () => Assert.Equal(0f, Height, 3));
    }

    [Fact]
    public void TheCrowLandsBesideTheDefeatedPlayer()
    {
        TickOnce();
        AdoptCrow();
        _player = PlayerAt(Middle, alive: false);
        Run(3.0);
        Assert.True(_pet.Sitting);
        Assert.Equal(0f, Height, 3);
        Assert.True(ToPlayer < 1.6f);
    }

    // ---- Teleporting with the player ----

    [Fact]
    public void ComesAlongWhenThePlayerIsTeleportedIntoTheHouseAndOut()
    {
        _player = PlayerAt(Places.HomeDoor.Position, facing: Places.HomeDoor.Yaw);
        _pet.PlaceAt(Places.HomeDoor.Position + new Vector3(2f, 0f, 0f));
        Run(1.0);
        Assert.NotEqual(Area.Home, Areas.At(_pet.Position.X, _pet.Position.Z));

        // Through the door: the pet appears inside with the player at once.
        var inside = Places.HomeInterior;
        _bus.Publish(new TeleportPlayer(inside.Position, inside.Yaw));
        Assert.True(_pet.Teleported);
        Assert.Equal(Area.Home, Areas.At(_pet.Position.X, _pet.Position.Z));
        Assert.True(Flat(_pet.Position, inside.Position) < 3f);
        Assert.True(Areas.Walkable(_pet.Position.X, _pet.Position.Z, PetModule.Radius));
        AssertOnGround();
        _player = PlayerAt(inside.Position, facing: inside.Yaw);
        Run(2.0, () =>
        {
            Assert.Equal(Area.Home, Areas.At(_pet.Position.X, _pet.Position.Z));
            AssertOnGround();
        });

        // And out again.
        var door = Places.HomeDoor;
        _bus.Publish(new TeleportPlayer(door.Position, door.Yaw));
        Assert.NotEqual(Area.Home, Areas.At(_pet.Position.X, _pet.Position.Z));
        Assert.True(Flat(_pet.Position, door.Position) < 3f);
        AssertOnGround();
        _player = PlayerAt(door.Position, facing: door.Yaw);
        Run(1.0);
        Assert.NotEqual(Area.Home, Areas.At(_pet.Position.X, _pet.Position.Z));
    }

    [Fact]
    public void StaysWithTheTeleportedPlayerEvenBeforeTheirNextState()
    {
        // The pet may tick before the Player module publishes where the teleport put them.
        _pet.PlaceAt(Middle + new Vector3(2f, 0f, 0f));
        TickOnce();
        var clinic = Places.ClinicDoor;
        _bus.Publish(new TeleportPlayer(clinic.Position, clinic.Yaw));
        _pet.Tick(new TickContext(_tick++, Step));
        // That tick reports the pop (the view jumps instead of gliding there).
        Assert.True(_pet.Teleported);
        Assert.Equal(Vector3.Zero, _pet.Velocity);
        Assert.True(Flat(_pet.Position, clinic.Position) < 3f);
        Assert.True(Areas.Walkable(_pet.Position.X, _pet.Position.Z, PetModule.Radius));
        Assert.False(Ground.Blocks(_pet.Position.X, _pet.Position.Z, PetModule.Radius));
    }

    [Fact]
    public void TheCrowFliesAlongOnATeleport()
    {
        TickOnce();
        AdoptCrow();
        var sign = Places.SignpostPetCity;
        _bus.Publish(new TeleportPlayer(sign.Position, sign.Yaw));
        Assert.True(Flat(_pet.Position, sign.Position) < 3f);
        Assert.Equal(PetModule.HoverHeight, Height, 2);
        Assert.Equal(Area.PetCity, Areas.At(_pet.Position.X, _pet.Position.Z));
    }

    [Fact]
    public void WakesUpWithThePlayerAfterADefeat()
    {
        _player = PlayerAt(Middle, alive: false);
        Run(3.0);
        Assert.True(_pet.Sitting);
        var clinic = Places.ClinicDoor;
        _bus.Publish(new TeleportPlayer(clinic.Position, clinic.Yaw));
        Assert.False(_pet.Sitting);
        Assert.True(Flat(_pet.Position, clinic.Position) < 3f);
    }

    [Fact]
    public void NeverWalksIntoTheHouseOnItsOwn()
    {
        // The player stands by the door outside; the pet starts (somehow) inside the house: it
        // comes out to them instead of staying in.
        var door = Places.HomeDoor;
        _player = PlayerAt(door.Position, facing: door.Yaw);
        _pet.PlaceAt(Places.HomeInterior.Position);
        TickOnce();
        Assert.NotEqual(Area.Home, Areas.At(_pet.Position.X, _pet.Position.Z));
        Run(2.0, () => Assert.NotEqual(Area.Home, Areas.At(_pet.Position.X, _pet.Position.Z)));
    }

    // ---- Save games ----

    /// <summary>Publishes a CollectSave and returns the Pet's one answer.</summary>
    private static SavePart Collect(FakeBus bus, int requestId)
    {
        int before = bus.Parts.Count;
        bus.Publish(new CollectSave(requestId));
        Assert.Equal(before + 1, bus.Parts.Count);
        return bus.Parts[^1];
    }

    private static PetModule StartedPet(FakeBus bus, List<string> log, int starter = GameStart.Cat)
    {
        var pet = new PetModule { Log = log.Add };
        pet.Start(bus);
        bus.Publish(new GameStart(GameStart.Girl, starter));
        return pet;
    }

    private static void AssertSameOwned(OwnedPets expected, OwnedPets actual)
    {
        Assert.Equal(expected.Cat, actual.Cat);
        Assert.Equal(expected.Dog, actual.Dog);
        Assert.Equal(expected.Crow, actual.Crow);
        Assert.Equal(expected.Active, actual.Active);
    }

    [Fact]
    public void AnswersCollectSaveWithOnePetPartHoldingVersionedCompactJson()
    {
        _bus.Publish(new AdoptPet(PetKind.Crow));
        var part = Collect(_bus, 42);
        Assert.Equal(42, part.RequestId);
        Assert.Equal(SaveModules.Pet, part.Module);
        Assert.DoesNotContain("\n", part.Json);
        Assert.DoesNotContain(" ", part.Json);
        using var doc = System.Text.Json.JsonDocument.Parse(part.Json);
        Assert.Equal(PetModule.SaveVersion, doc.RootElement.GetProperty("v").GetInt32());
    }

    [Theory]
    [InlineData(GameStart.Cat, false, PetKind.Cat)]
    [InlineData(GameStart.Dog, false, PetKind.Dog)]
    [InlineData(GameStart.Cat, true, PetKind.Crow)]
    [InlineData(GameStart.Dog, true, PetKind.Dog)]
    [InlineData(GameStart.Cat, true, PetKind.Cat)]
    public void SaveThenRestoreGivesBackTheSameOwnedAndActivePets(int starter, bool adoptCrow, PetKind active)
    {
        var log = new List<string>();
        var bus = new FakeBus();
        var pet = StartedPet(bus, log, starter);
        if (adoptCrow)
            bus.Publish(new AdoptPet(PetKind.Crow));
        bus.Publish(new SetActivePet(active));
        var original = pet.Owned;
        Assert.Equal(active, original.Active);
        string json = Collect(bus, 1).Json;

        // A different game: another module instance, started with the other starter.
        var bus2 = new FakeBus();
        var restored = StartedPet(bus2, log, starter == GameStart.Cat ? GameStart.Dog : GameStart.Cat);
        bus2.Publish(new RestorePart(SaveModules.Pet, json));
        AssertSameOwned(original, restored.Owned);
        Assert.True(restored.Started);
        Assert.Equal(active, restored.Kind);
        Assert.Equal(active == PetKind.Crow ? PetModule.HoverHeight : 0f, restored.Altitude);

        // Into a module that has not seen GameStart (loading from the title screen).
        var bus3 = new FakeBus();
        var fresh = new PetModule { Log = log.Add };
        fresh.Start(bus3);
        bus3.Publish(new RestorePart(SaveModules.Pet, json));
        AssertSameOwned(original, fresh.Owned);
        Assert.True(fresh.Started);
        // A GameStart after the load does not replace the restored pets.
        bus3.Publish(new GameStart(GameStart.Girl, starter == GameStart.Cat ? GameStart.Dog : GameStart.Cat));
        AssertSameOwned(original, fresh.Owned);
        // It saves the same state again, and publishes it every tick.
        Assert.Equal(json, Collect(bus3, 2).Json);
        fresh.Tick(new TickContext(0, Step));
        AssertSameOwned(original, bus3.Owned[^1]);
        Assert.Equal(active, bus3.States[^1].Kind);
        Assert.Empty(log);
    }

    [Fact]
    public void SaveBeforeTheGameStartsRestoresToNoPet()
    {
        var log = new List<string>();
        var bus = new FakeBus();
        var pet = new PetModule { Log = log.Add };
        pet.Start(bus);
        string json = Collect(bus, 1).Json;
        var bus2 = new FakeBus();
        var other = StartedPet(bus2, log);
        bus2.Publish(new RestorePart(SaveModules.Pet, json));
        Assert.False(other.Started);
        AssertSameOwned(pet.Owned, other.Owned);
        other.Tick(new TickContext(0, Step));
        Assert.Empty(bus2.States);
        Assert.Empty(log);
    }

    [Fact]
    public void RestoringHasNoSideEffects()
    {
        var log = new List<string>();
        var bus = new FakeBus();
        var pet = StartedPet(bus, log);
        bus.Publish(new AdoptPet(PetKind.Crow));
        string json = Collect(bus, 1).Json;

        var bus2 = new FakeBus();
        var restored = new PetModule { Log = log.Add };
        restored.Start(bus2);
        bus2.Publish(new RestorePart(SaveModules.Pet, json));
        // Only the RestorePart itself went over the bus: no adoption, purchase, bite or anything else.
        Assert.Equal(new[] { typeof(RestorePart) }, bus2.Published);
        Assert.Empty(log);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"v\":99,\"cat\":true,\"dog\":true,\"crow\":true,\"active\":\"Crow\"}")]
    [InlineData("{\"cat\":true,\"dog\":true,\"crow\":true,\"active\":\"Crow\"}")]
    [InlineData("{\"v\":1,\"cat\":true,\"dog\":false,\"crow\":false,\"active\":\"Dog\"}")]
    [InlineData("{\"v\":1,\"cat\":true,\"dog\":false,\"crow\":false,\"active\":\"Unicorn\"}")]
    [InlineData("{\"v\":1,\"cat\":\"yes\",\"dog\":false,\"crow\":false,\"active\":\"Cat\"}")]
    [InlineData("{\"v\":1,\"cat\":false,\"dog\":false,\"crow\":false,\"active\":\"Crow\"}")]
    public void BadJsonKeepsTheCurrentStateAndLogsOnce(string json)
    {
        var log = new List<string>();
        var bus = new FakeBus();
        var pet = StartedPet(bus, log, GameStart.Dog);
        var before = pet.Owned;
        bus.Publish(new RestorePart(SaveModules.Pet, json));
        AssertSameOwned(before, pet.Owned);
        Assert.True(pet.Started);
        Assert.Single(log);

        // Before the game starts too: still no pet.
        var bus2 = new FakeBus();
        var log2 = new List<string>();
        var fresh = new PetModule { Log = log2.Add };
        fresh.Start(bus2);
        bus2.Publish(new RestorePart(SaveModules.Pet, json));
        Assert.False(fresh.Started);
        Assert.Single(log2);
    }

    [Fact]
    public void NullJsonKeepsDefaults()
    {
        var log = new List<string>();
        var bus = new FakeBus();
        var pet = StartedPet(bus, log);
        var before = pet.Owned;
        bus.Publish(new RestorePart(SaveModules.Pet, null!));
        AssertSameOwned(before, pet.Owned);
        Assert.Single(log);
    }

    [Fact]
    public void IgnoresOtherModulesParts()
    {
        var log = new List<string>();
        var bus = new FakeBus();
        var pet = StartedPet(bus, log);
        bus.Publish(new AdoptPet(PetKind.Crow));
        var crowGame = Collect(bus, 1).Json;
        var bus2 = new FakeBus();
        var other = StartedPet(bus2, log);
        bus2.Publish(new RestorePart(SaveModules.Wallet, crowGame));
        bus2.Publish(new RestorePart(SaveModules.Home, "garbage"));
        Assert.False(other.Owns(PetKind.Crow));
        Assert.Equal(PetKind.Cat, other.Kind);
        Assert.Empty(log);
    }

    // ---- dog.glb (the model the view loads): one smooth body and head, no lumps ----

    /// <summary>Reads a GLB's JSON and, per node, the positions of each of its primitives by material name.</summary>
    private static Dictionary<string, (Vector3 Pivot, Dictionary<string, List<Vector3>> Parts)> ReadGlb(string path)
    {
        var bytes = File.ReadAllBytes(path);
        int jsonLength = BitConverter.ToInt32(bytes, 12);
        using var doc = JsonDocument.Parse(new ReadOnlyMemory<byte>(bytes, 20, jsonLength));
        var root = doc.RootElement;
        int binStart = 20 + jsonLength + 8;
        var materials = root.GetProperty("materials").EnumerateArray().Select(m => m.GetProperty("name").GetString()!).ToArray();
        var accessors = root.GetProperty("accessors");
        var views = root.GetProperty("bufferViews");
        var meshes = root.GetProperty("meshes");
        var nodes = new Dictionary<string, (Vector3, Dictionary<string, List<Vector3>>)>();
        foreach (var node in root.GetProperty("nodes").EnumerateArray())
        {
            if (!node.TryGetProperty("mesh", out var meshIndex)) continue;
            var t = node.TryGetProperty("translation", out var tr)
                ? new Vector3(tr[0].GetSingle(), tr[1].GetSingle(), tr[2].GetSingle()) : Vector3.Zero;
            var parts = new Dictionary<string, List<Vector3>>();
            foreach (var prim in meshes[meshIndex.GetInt32()].GetProperty("primitives").EnumerateArray())
            {
                var acc = accessors[prim.GetProperty("attributes").GetProperty("POSITION").GetInt32()];
                var view = views[acc.GetProperty("bufferView").GetInt32()];
                int offset = binStart + (view.TryGetProperty("byteOffset", out var vo) ? vo.GetInt32() : 0)
                    + (acc.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0);
                int stride = view.TryGetProperty("byteStride", out var bs) ? bs.GetInt32() : 12;
                var name = materials[prim.GetProperty("material").GetInt32()];
                if (!parts.TryGetValue(name, out var list)) parts[name] = list = new List<Vector3>();
                for (int i = 0; i < acc.GetProperty("count").GetInt32(); i++)
                {
                    int o = offset + i * stride;
                    list.Add(new Vector3(BitConverter.ToSingle(bytes, o), BitConverter.ToSingle(bytes, o + 4),
                        BitConverter.ToSingle(bytes, o + 8)));
                }
            }
            nodes[node.GetProperty("name").GetString()!] = (t, parts);
        }
        return nodes;
    }

    private static string DogGlb()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "modules", "Pet", "Models", "dog.glb")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "modules", "Pet", "Models", "dog.glb");
    }

    /// <summary>How far p is off the ellipsoid surface: 0 on it, below 0 inside, above 0 outside.</summary>
    private static float OffEllipsoid(Vector3 p, Vector3 center, Vector3 radii)
    {
        var q = (p - center) / radii;
        return q.Length() - 1;
    }

    [Fact]
    public void DogBodyFurIsOneSmoothEllipsoidWithoutClumps()
    {
        var dog = ReadGlb(DogGlb());
        var fur = dog["Body"].Parts["DogFur"];
        // Fit the ellipsoid to the fur's bounding box: with clumps sticking out, most of the fur
        // would then lie off it (the clumps' own vertices, and the body's inside the box).
        var min = fur.Aggregate(Vector3.Min);
        var max = fur.Aggregate(Vector3.Max);
        var center = (min + max) / 2;
        var radii = (max - min) / 2;
        Assert.All(fur, p => Assert.InRange(OffEllipsoid(p, center, radii), -0.01f, 0.01f));
        // A full round body (about 0.42 x 0.4 x 0.6 m, glTF Y up), centered on the Body pivot.
        Assert.InRange(radii.X, 0.18f, 0.24f);
        Assert.InRange(radii.Y, 0.17f, 0.23f);
        Assert.InRange(radii.Z, 0.27f, 0.33f);
        Assert.InRange(center.Length(), 0f, 0.01f);
    }

    [Fact]
    public void DogHeadFurIsOneSmoothHeadWithEarsAndNoCheekLumps()
    {
        var dog = ReadGlb(DogGlb());
        var (pivot, parts) = dog["Head"];
        // Head center (0, 0.68 up, 0.32 forward) in glTF (forward is -Z), relative to the neck pivot.
        var center = new Vector3(0, 0.68f, -0.32f) - pivot;
        var fur = parts["DogFur"];
        // Below the ears' bases every fur vertex is on the one round head (cheek spheres stuck out
        // at its lower sides); the ears are the only fur above it.
        var face = fur.Where(p => p.Y - center.Y < 0.1f).ToList();
        Assert.NotEmpty(face);
        var min = face.Aggregate(Vector3.Min);
        var max = face.Aggregate(Vector3.Max);
        var radii = new Vector3((max.X - min.X) / 2, (max.Z - min.Z) / 2, (max.Z - min.Z) / 2);
        Assert.All(face, p => Assert.InRange(OffEllipsoid(p, center, radii), -0.01f, 0.01f));
        Assert.InRange(radii.X, 0.24f, 0.3f);
        Assert.Contains(fur, p => p.Y - center.Y > 0.3f);
    }

    [Fact]
    public void DogKeepsItsPartsAndDetails()
    {
        var dog = ReadGlb(DogGlb());
        foreach (var node in new[] { "Body", "Head", "LegFL", "LegFR", "LegBL", "LegBR", "Tail" })
            Assert.Contains("DogFur", dog[node].Parts.Keys);
        // Cream chest, the little mushrooms on the back, the collar and its tag.
        foreach (var mat in new[] { "DogLight", "Stem", "Cap", "Spots", "Collar", "Tag" })
            Assert.Contains(mat, dog["Body"].Parts.Keys);
        foreach (var mat in new[] { "DogLight", "DogNose", "Eye", "Tongue", "Blush", "DogEar" })
            Assert.Contains(mat, dog["Head"].Parts.Keys);
    }
}
