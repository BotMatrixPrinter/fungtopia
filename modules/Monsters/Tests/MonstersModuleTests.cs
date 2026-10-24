using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Forest;
using Fungtopia.Player;
using Fungtopia.Shell;
using Xunit;

namespace Fungtopia.Monsters.Tests;

public class MonstersModuleTests
{
    private const double Step = 1.0 / 60;

    /// <summary>Fake of the shell's bus: records what the module publishes and delivers messages to it.</summary>
    private sealed class FakeBus : IMessageBus
    {
        public readonly List<MonsterState> States = new(4096);
        public readonly List<DamagePlayer> Damage = new(64);
        public readonly List<MonsterKilled> Killed = new(64);
        public readonly List<long> DamageTicks = new(64);
        public bool Record = true;
        public long Tick;
        private IMessageHandler<PlayerState>? _player;
        private IMessageHandler<HitMonster>? _hit;

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct
        {
            if (handler is IMessageHandler<PlayerState> p && typeof(T) == typeof(PlayerState))
                _player = p;
            if (handler is IMessageHandler<HitMonster> h && typeof(T) == typeof(HitMonster))
                _hit = h;
        }

        public void Publish<T>(in T message) where T : struct
        {
            if (!Record)
                return;
            if (message is MonsterState s)
                States.Add(s);
            else if (message is DamagePlayer d)
            {
                Damage.Add(d);
                DamageTicks.Add(Tick);
            }
            else if (message is MonsterKilled k)
                Killed.Add(k);
        }

        public void SendPlayer(Vector3 feet, bool alive = true) =>
            _player!.Handle(new PlayerState(new Vector3(feet.X, Terrain.HeightAt(feet.X, feet.Z), feet.Z), 0f,
                Vector3.Zero, true, alive ? 10 : 0, 10, alive, CharacterKind.Girl));

        public void Hit(int id, int damage, float poisonPerSecond = 0f, float poisonSeconds = 0f, int source = 0) =>
            _hit!.Handle(new HitMonster(id, damage, poisonPerSecond, poisonSeconds, source));
    }

    private static (MonstersModule, FakeBus) Started(uint seed = 7)
    {
        var bus = new FakeBus();
        var monsters = new MonstersModule { Seed = seed };
        monsters.Start(bus);
        return (monsters, bus);
    }

    private static void Run(MonstersModule monsters, FakeBus bus, double seconds)
    {
        int ticks = (int)Math.Round(seconds / Step);
        for (int i = 0; i < ticks; i++)
        {
            bus.Tick = monsters.Ticks + 1;
            monsters.Tick(new TickContext(monsters.Ticks, Step));
        }
    }

    private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    /// <summary>A spot in the forest with no trunk within <paramref name="clear"/> meters, far from the start.</summary>
    private static Vector3 OpenSpot(float clear = 14f)
    {
        for (float z = -80f; z < 40f; z += 2f)
        for (float x = -80f; x < 80f; x += 2f)
            if (!Obstacles.Blocks(x, z, clear))
                return new Vector3(x, Terrain.HeightAt(x, z), z);
        throw new InvalidOperationException("no open spot");
    }

    /// <summary>Puts every monster but <paramref name="keep"/> far away in a corner, out of the way.</summary>
    private static void Isolate(MonstersModule monsters, FakeBus bus, int keep, Vector3 at, Vector3 player)
    {
        // A player far from the corner keeps them all wandering there.
        for (int i = 0; i < MonstersModule.Count; i++)
            if (i != keep)
                monsters.Place(i, new Vector3(i % 2 == 0 ? -90f : 90f, 0f, -90f + i * 0.1f));
        monsters.Place(keep, at);
        bus.SendPlayer(player);
    }

    [Fact]
    public void KeepsSixCaterpillarsAndSixAntsAwayFromTheStart()
    {
        var (monsters, _) = Started();
        var states = monsters.States.ToArray();
        Assert.Equal(12, states.Length);
        Assert.Equal(6, Array.FindAll(states, s => s.Kind == MonsterKind.Caterpillar).Length);
        Assert.Equal(6, Array.FindAll(states, s => s.Kind == MonsterKind.Ant).Length);
        var ids = new HashSet<int>();
        foreach (var s in states)
        {
            Assert.True(s.Alive);
            Assert.True(ids.Add(s.MonsterId));
            Assert.True(ForestBounds.Contains(s.Position.X, s.Position.Z, s.Radius));
            Assert.False(Obstacles.Blocks(s.Position.X, s.Position.Z, s.Radius));
            Assert.True(Flat(s.Position, ForestBounds.PlayerStart) >= MonstersModule.SpawnAwayFromStart);
            Assert.Equal(Terrain.HeightAt(s.Position.X, s.Position.Z), s.Position.Y, 4);
            Assert.Equal(s.Health, s.MaxHealth);
            Assert.False(s.Poisoned);
        }
        Assert.Equal(8, Array.Find(states, s => s.Kind == MonsterKind.Caterpillar).MaxHealth);
        Assert.Equal(5, Array.Find(states, s => s.Kind == MonsterKind.Ant).MaxHealth);
    }

    [Fact]
    public void SpreadsOverTheForest()
    {
        var (monsters, _) = Started();
        var states = monsters.States.ToArray();
        for (int i = 0; i < states.Length; i++)
        for (int j = i + 1; j < states.Length; j++)
            Assert.True(Flat(states[i].Position, states[j].Position) >= 9f, $"monsters {i} and {j} start bunched up");
        // Some in the north half, some in the south half, some east, some west.
        Assert.Contains(states, s => s.Position.Z < -30f);
        Assert.Contains(states, s => s.Position.X < -30f);
        Assert.Contains(states, s => s.Position.X > 30f);
    }

    [Fact]
    public void PublishesEveryMonsterEveryTick()
    {
        var (monsters, bus) = Started();
        Run(monsters, bus, 3 * Step);
        Assert.Equal(36, bus.States.Count);
        var ids = new HashSet<int>();
        for (int i = 0; i < 12; i++)
            ids.Add(bus.States[24 + i].MonsterId);
        Assert.Equal(12, ids.Count);
        Assert.Empty(bus.Damage);
    }

    [Fact]
    public void WandersSlowlyInsideTheForestAndAroundTrunks()
    {
        var (monsters, bus) = Started();
        bus.SendPlayer(ForestBounds.PlayerStart, alive: false); // nobody to chase
        var previous = monsters.States.ToArray();
        var first = monsters.States.ToArray();
        float maxSpeed = 0f;
        for (int t = 0; t < 60 * 120; t++)
        {
            bus.Record = false;
            monsters.Tick(new TickContext(t, Step));
            var states = monsters.States;
            for (int i = 0; i < states.Length; i++)
            {
                var s = states[i];
                Assert.True(ForestBounds.Contains(s.Position.X, s.Position.Z, s.Radius), $"{s.Kind} left the forest at {s.Position}");
                Assert.False(Obstacles.Blocks(s.Position.X, s.Position.Z, s.Radius * 0.9f), $"{s.Kind} inside a trunk at {s.Position}");
                maxSpeed = MathF.Max(maxSpeed, Flat(s.Position, previous[i].Position) / (float)Step);
                previous[i] = s;
            }
        }
        // Slow: no faster than an ant's wander (plus a little nudge from a neighbour).
        Assert.True(maxSpeed <= MonstersModule.StatsOf(MonsterKind.Ant).WanderSpeed + 0.2f, $"wandered at {maxSpeed} m/s");
        // But they do move about.
        int moved = 0;
        for (int i = 0; i < 12; i++)
            if (Flat(first[i].Position, previous[i].Position) > 3f)
                moved++;
        Assert.True(moved >= 8, $"only {moved} monsters wandered");
    }

    [Fact]
    public void ChasesTheAlivePlayerWithinTenMeters()
    {
        var (monsters, bus) = Started();
        var spot = OpenSpot();
        Isolate(monsters, bus, 0, spot, spot + new Vector3(8f, 0f, 0f));
        Run(monsters, bus, 1.0);
        var m = monsters.Monsters[0];
        Assert.True(m.Chasing);
        float speed = MonstersModule.StatsOf(MonsterKind.Caterpillar).ChaseSpeed;
        Assert.Equal(8f - speed, Flat(m.Position, spot + new Vector3(8f, 0f, 0f)), 1);
    }

    [Fact]
    public void DoesNotChaseAFarOrDefeatedPlayer()
    {
        var (monsters, bus) = Started();
        var spot = OpenSpot();
        Isolate(monsters, bus, 0, spot, spot + new Vector3(12f, 0f, 0f));
        Run(monsters, bus, 1.0);
        Assert.False(monsters.Monsters[0].Chasing);
        Assert.True(Flat(monsters.Monsters[0].Position, spot) <= MonstersModule.StatsOf(MonsterKind.Caterpillar).WanderSpeed + 0.01f);

        monsters.Place(0, spot);
        bus.SendPlayer(spot + new Vector3(5f, 0f, 0f), alive: false);
        Run(monsters, bus, 1.0);
        Assert.False(monsters.Monsters[0].Chasing);
        Assert.Empty(bus.Damage);
    }

    [Fact]
    public void AntsChaseFasterThanCaterpillars()
    {
        var cat = MonstersModule.StatsOf(MonsterKind.Caterpillar);
        var ant = MonstersModule.StatsOf(MonsterKind.Ant);
        Assert.True(ant.ChaseSpeed > cat.ChaseSpeed);
        Assert.True(ant.WanderSpeed > cat.WanderSpeed);
        Assert.True(ant.ChaseSpeed < 5f, "the player (5 m/s) can outrun them");

        var (monsters, bus) = Started();
        var spot = OpenSpot();
        var player = spot + new Vector3(9f, 0f, 0f);
        Isolate(monsters, bus, 6, spot, player);
        Run(monsters, bus, 1.0);
        Assert.Equal(9f - ant.ChaseSpeed, Flat(monsters.Monsters[6].Position, player), 1);
    }

    [Theory]
    [InlineData(0, 1)] // a caterpillar bites for 1
    [InlineData(6, 2)] // an ant bites for 2
    public void BitesInReachEveryAttackInterval(int index, int damage)
    {
        var (monsters, bus) = Started();
        var spot = OpenSpot();
        var player = spot + new Vector3(1f, 0f, 0f);
        Isolate(monsters, bus, index, spot, player);
        Run(monsters, bus, 3.0);
        // At once on reaching, then every 1.2 s: at 0, 1.2 and 2.4 s.
        Assert.Equal(3, bus.Damage.Count);
        Assert.All(bus.Damage, d => Assert.Equal(damage, d.Amount));
        Assert.Equal(1, bus.DamageTicks[0]);
        Assert.Equal(73, bus.DamageTicks[1]);
        Assert.Equal(145, bus.DamageTicks[2]);
        Assert.True(Flat(bus.Damage[0].From, spot) < 0.1f);
        // It stays in reach and does not walk into the player.
        float d = Flat(monsters.Monsters[index].Position, player);
        Assert.InRange(d, 0.5f, MonstersModule.AttackReach);
    }

    [Fact]
    public void WalksIntoReachBeforeBiting()
    {
        var (monsters, bus) = Started();
        var spot = OpenSpot();
        Isolate(monsters, bus, 0, spot, spot + new Vector3(5f, 0f, 0f));
        Run(monsters, bus, 1.0);
        Assert.Empty(bus.Damage); // 1.8 m/s: still 3.2 m away after 1 s
        Run(monsters, bus, 2.0);
        Assert.Single(bus.Damage);
        Assert.Equal(1, bus.Damage[0].Amount);
    }

    [Fact]
    public void HitTakesHealthAtOnce()
    {
        var (monsters, bus) = Started();
        var m = monsters.Monsters[0];
        bus.Hit(m.Id, 3);
        Assert.Equal(5, m.Health);
        Run(monsters, bus, Step);
        var s = bus.States.Find(x => x.MonsterId == m.Id);
        Assert.Equal(5, s.Health);
        Assert.Equal(8, s.MaxHealth);
        Assert.False(s.Poisoned);

        bus.Hit(9999, 3); // no such monster
        bus.Hit(m.Id, 0);
        bus.Hit(m.Id, -4);
        Assert.Equal(5, m.Health);
        Assert.Empty(bus.Killed);
    }

    [Fact]
    public void PoisonHurtsOverTime()
    {
        var (monsters, bus) = Started();
        var m = monsters.Monsters[0]; // 8 HP
        bus.Hit(m.Id, 1, poisonPerSecond: 2f, poisonSeconds: 3f);
        Assert.Equal(7, m.Health);
        Assert.True(m.Poisoned);
        Run(monsters, bus, 1.0);
        Assert.Equal(5, m.Health);
        Assert.True(monsters.States[0].Poisoned);
        Run(monsters, bus, 2.0);
        Assert.Equal(1, m.Health);
        Run(monsters, bus, 2.0);
        Assert.Equal(1, m.Health); // worn off
        Assert.False(m.Poisoned);
        Assert.False(monsters.States[0].Poisoned);
        Assert.Empty(bus.Killed);
    }

    [Fact]
    public void StackedPoisonRefreshesTheDurationNotTheRate()
    {
        var (monsters, bus) = Started();
        var m = monsters.Monsters[0]; // 8 HP
        bus.Hit(m.Id, 0, poisonPerSecond: 1f, poisonSeconds: 2f);
        Run(monsters, bus, 1.0);
        Assert.Equal(7, m.Health);
        // A second, stronger poison: the rate stays 1/s, the 2 s start again.
        bus.Hit(m.Id, 0, poisonPerSecond: 5f, poisonSeconds: 2f);
        Run(monsters, bus, 1.0);
        Assert.Equal(6, m.Health);
        Run(monsters, bus, 1.0);
        Assert.Equal(5, m.Health);
        Assert.False(m.Poisoned);
        Run(monsters, bus, 2.0);
        Assert.Equal(5, m.Health);
    }

    [Theory]
    [InlineData(0, MonsterKind.Caterpillar)]
    [InlineData(6, MonsterKind.Ant)]
    public void KilledOnceWithItsReward(int index, MonsterKind kind)
    {
        var (monsters, bus) = Started();
        var m = monsters.Monsters[index];
        int id = m.Id;
        var at = m.Position;
        bus.Hit(id, 100, source: 1);
        bus.Hit(id, 100, source: 0); // already defeated
        Run(monsters, bus, 2.0);
        var killed = Assert.Single(bus.Killed);
        Assert.Equal(id, killed.MonsterId);
        Assert.Equal(kind, killed.Kind);
        Assert.Equal(50, killed.Reward);
        Assert.Equal(1, killed.Source);
        Assert.Equal(at, killed.Position);
        Assert.False(m.Alive);
        Assert.Equal(0, m.Health);
        var last = monsters.States[index];
        Assert.False(last.Alive);
        Assert.Equal(id, last.MonsterId);
        Assert.Equal(at, last.Position); // stays put while defeated
    }

    [Fact]
    public void PoisonCanFinishAMonster()
    {
        var (monsters, bus) = Started();
        var m = monsters.Monsters[6]; // ant, 5 HP
        bus.Hit(m.Id, 3, poisonPerSecond: 1f, poisonSeconds: 5f, source: 0);
        Run(monsters, bus, 1.5);
        Assert.Empty(bus.Killed);
        Run(monsters, bus, 1.0);
        var killed = Assert.Single(bus.Killed);
        Assert.Equal(MonsterKind.Ant, killed.Kind);
        Assert.Equal(0, killed.Source);
        Assert.False(m.Poisoned);
        Run(monsters, bus, 5.0);
        Assert.Single(bus.Killed);
    }

    [Fact]
    public void RewardsFromTheDesign()
    {
        Assert.Equal(50, MonsterRewards.For(MonsterKind.Caterpillar));
        Assert.Equal(50, MonsterRewards.For(MonsterKind.Ant));
        Assert.Equal(200, MonsterRewards.For(MonsterKind.Tarantula));
        Assert.Equal(200, MonsterRewards.For(MonsterKind.Ladybug));
    }

    [Fact]
    public void DefeatedMonstersDoNotBite()
    {
        var (monsters, bus) = Started();
        var spot = OpenSpot();
        Isolate(monsters, bus, 0, spot, spot + new Vector3(1f, 0f, 0f));
        bus.Hit(monsters.Monsters[0].Id, 100);
        Run(monsters, bus, 3.0);
        Assert.Empty(bus.Damage);
    }

    [Fact]
    public void ANewMonsterOfThatKindComesTwentySecondsLaterAwayFromThePlayer()
    {
        var (monsters, bus) = Started();
        var player = new Vector3(-40f, 0f, -40f);
        bus.SendPlayer(player);
        var m = monsters.Monsters[6];
        int oldId = m.Id;
        bus.Hit(oldId, 100);
        Run(monsters, bus, 19.9);
        Assert.False(m.Alive);
        Assert.Equal(oldId, m.Id);
        Run(monsters, bus, 0.2);
        Assert.True(m.Alive);
        Assert.Equal(MonsterKind.Ant, m.Kind);
        Assert.NotEqual(oldId, m.Id);
        Assert.Equal(5, m.Health);
        Assert.True(Flat(m.Position, player) >= MonstersModule.SpawnAwayFromPlayer);
        Assert.True(Flat(m.Position, ForestBounds.PlayerStart) >= MonstersModule.SpawnAwayFromStart);
        Assert.False(Obstacles.Blocks(m.Position.X, m.Position.Z, m.Radius));
        Assert.True(ForestBounds.Contains(m.Position.X, m.Position.Z, m.Radius));
        Assert.Null(monsters.Find(oldId));
        bus.Hit(oldId, 100); // the old one is gone
        Assert.Equal(5, m.Health);

        var states = monsters.States.ToArray();
        Assert.All(states, s => Assert.True(s.Alive));
        Assert.Equal(6, Array.FindAll(states, s => s.Kind == MonsterKind.Ant).Length);
        Assert.Single(bus.Killed);
    }

    [Fact]
    public void SameSeedSameForest()
    {
        var (a, busA) = Started(42);
        var (b, busB) = Started(42);
        var (c, _) = Started(43);
        Run(a, busA, 10.0);
        Run(b, busB, 10.0);
        Assert.Equal(a.States.ToArray(), b.States.ToArray());
        Assert.NotEqual(a.States[0].Position, c.States[0].Position);
    }

    [Fact]
    public void TicksWithoutAllocating()
    {
        var (monsters, bus) = Started();
        bus.Record = false;
        bus.SendPlayer(monsters.Monsters[0].Position + new Vector3(1f, 0f, 0f));
        bus.Hit(monsters.Monsters[1].Id, 1, 2f, 3f);
        bus.Hit(monsters.Monsters[2].Id, 100);
        Run(monsters, bus, 1.0); // warm up
        long before = GC.GetAllocatedBytesForCurrentThread();
        Run(monsters, bus, 25.0); // includes the respawn
        bus.Hit(monsters.Monsters[3].Id, 100);
        long after = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(before, after);
    }

    [Fact]
    public void TickBeforeStartThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new MonstersModule().Tick(new TickContext(0, Step)));
    }
}
