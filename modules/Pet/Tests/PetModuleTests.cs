using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Forest;
using Fungtopia.Monsters;
using Fungtopia.Player;
using Fungtopia.Shell;
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
            if (_handlers.TryGetValue(typeof(T), out var list))
                foreach (var handler in list)
                    ((IMessageHandler<T>)handler).Handle(in message);
        }
    }

    /// <summary>A bus that drops everything, for measuring the pet's own allocations.</summary>
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
        _player = PlayerAt(Middle);
    }

    private static Vector3 Ground(float x, float z) => new(x, Terrain.HeightAt(x, z), z);

    private static Vector3 Ground(Vector3 v) => Ground(v.X, v.Z);

    private static PlayerState PlayerAt(Vector3 feet, bool alive = true, float facing = 0f) =>
        new(Ground(feet), facing, Vector3.Zero, true, alive ? 10 : 0, 10, alive, CharacterKind.Girl);

    private static MonsterState Monster(int id, Vector3 feet, bool alive = true, float radius = 0.5f) =>
        new(id, MonsterKind.Ant, Ground(feet), radius, alive ? 5 : 0, 5, alive, false);

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
        Assert.Equal(Terrain.HeightAt(_pet.Position.X, _pet.Position.Z), _pet.Position.Y, 3);
    }

    // ---- Start, settings and state ----

    [Fact]
    public void IsACatByDefault()
    {
        Assert.Equal(PetKind.Cat, new PetModule().Kind);
        TickOnce();
        Assert.Equal(PetKind.Cat, _bus.States[^1].Kind);
    }

    [Fact]
    public void TheSettingPicksTheDog()
    {
        _pet.Kind = PetKind.Dog;
        TickOnce();
        Assert.Equal(PetKind.Dog, _bus.States[^1].Kind);
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
        Assert.False(Obstacles.Blocks(_pet.Position.X, _pet.Position.Z, PetModule.Radius));
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
        _player = PlayerAt(ForestBounds.PlayerStart);
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
    private static Obstacle TrunkWithRoomAround()
    {
        foreach (var o in Obstacles.All)
        {
            float gap = o.Radius + 4f;
            if (o.Radius > 1.4f && ForestBounds.Contains(o.X - gap, o.Z, 2f) && ForestBounds.Contains(o.X + gap, o.Z, 2f)
                && !Obstacles.Blocks(o.X - gap, o.Z, 1f) && !Obstacles.Blocks(o.X + gap, o.Z, 1f))
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
            Assert.False(Obstacles.Blocks(_pet.Position.X, _pet.Position.Z, PetModule.Radius - 0.01f),
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
            if (!Obstacles.Blocks(feet.X, feet.Z, 0.4f))
                _player = PlayerAt(feet);
            Assert.False(Obstacles.Blocks(_pet.Position.X, _pet.Position.Z, PetModule.Radius - 0.01f));
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
    public void StaysInsideTheForest()
    {
        float edge = ForestBounds.HalfSize - 1f;
        _player = PlayerAt(new Vector3(edge, 0f, 30f));
        _pet.PlaceAt(new Vector3(edge - 3f, 0f, 30f));
        Run(3.0);
        Assert.True(ForestBounds.Contains(_pet.Position.X, _pet.Position.Z));
    }

    // ---- Performance ----

    [Fact]
    public void DoesNotAllocatePerTick()
    {
        var pet = new PetModule();
        pet.Start(new NullBus());
        var player = PlayerAt(Middle);
        var monsters = new MonsterState[12];
        for (int i = 0; i < monsters.Length; i++)
            monsters[i] = Monster(i + 1, Middle + new Vector3(-5f + i, 0f, 3f + (i % 3)));
        pet.PlaceAt(Middle + new Vector3(2f, 0f, 0f));
        void Tick(long t)
        {
            pet.Handle(in player);
            for (int i = 0; i < monsters.Length; i++)
                pet.Handle(in monsters[i]);
            pet.Tick(new TickContext(t, Step));
        }
        Tick(0);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int t = 1; t < 600; t++)
            Tick(t);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }
}
