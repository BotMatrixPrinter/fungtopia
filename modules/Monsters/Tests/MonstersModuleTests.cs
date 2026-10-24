using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Map;
using Fungtopia.Pet;
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
        public readonly List<KnockBackPlayer> KnockBacks = new(64);
        public bool Record = true;
        public long Tick;
        private IMessageHandler<PlayerState>? _player;
        private IMessageHandler<HitMonster>? _hit;
        private IMessageHandler<PetState>? _pet;

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct
        {
            if (handler is IMessageHandler<PlayerState> p && typeof(T) == typeof(PlayerState))
                _player = p;
            if (handler is IMessageHandler<HitMonster> h && typeof(T) == typeof(HitMonster))
                _hit = h;
            if (handler is IMessageHandler<PetState> q && typeof(T) == typeof(PetState))
                _pet = q;
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
            else if (message is KnockBackPlayer b)
                KnockBacks.Add(b);
        }

        public void SendPlayer(Vector3 feet, bool alive = true) =>
            _player!.Handle(new PlayerState(new Vector3(feet.X, Ground.HeightAt(feet.X, feet.Z), feet.Z), 0f,
                Vector3.Zero, true, alive ? 10 : 0, 10, alive, CharacterKind.Girl));

        public void SendPet(Vector3 feet) => _pet!.Handle(new PetState(PetKind.Cat, feet, 0f, true));

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

    private const int FirstTarantula = MonstersModule.NormalCount;
    private const int FirstLadybug = MonstersModule.NormalCount + MonstersModule.TarantulaCount;

    private static bool AtSpawnSpot(Vector3 p)
    {
        foreach (var s in DeepForestLayout.SpawnPoints)
            if (Flat(p, new Vector3(s.X, 0f, s.Y)) < 0.01f)
                return true;
        return false;
    }

    /// <summary>A spot in the forest with no trunk within <paramref name="clear"/> meters, far from the entrances.</summary>
    private static Vector3 OpenSpot(float clear = 14f)
    {
        for (float z = -80f; z < 40f; z += 2f)
        for (float x = -80f; x < 80f; x += 2f)
            if (!Ground.Blocks(x, z, clear))
                return new Vector3(x, Ground.HeightAt(x, z), z);
        throw new InvalidOperationException("no open spot");
    }

    /// <summary>A Deep Forest spawn spot: open ground for <see cref="DeepForestLayout.ClearingRadius"/> around it.</summary>
    private static Vector3 DeepSpot(int k = 0)
    {
        var s = DeepForestLayout.SpawnPoints[k];
        return new Vector3(s.X, Ground.HeightAt(s.X, s.Y), s.Y);
    }

    /// <summary>Puts every monster but <paramref name="keep"/> far away in a corner of its area, out of the way.</summary>
    private static void Isolate(MonstersModule monsters, FakeBus bus, int keep, Vector3 at, Vector3 player, float? facing = null)
    {
        // A player far from the corners keeps them all wandering there.
        for (int i = 0; i < MonstersModule.Count; i++)
            if (i != keep)
                monsters.Place(i, i < MonstersModule.NormalCount
                    ? new Vector3(i % 2 == 0 ? -90f : 90f, 0f, -90f + i * 0.1f)
                    : new Vector3(-270f + i * 0.1f, 0f, at.Z < 0f ? 85f : -85f));
        monsters.Place(keep, at, facing);
        bus.SendPlayer(player);
    }

    [Fact]
    public void KeepsSixCaterpillarsAndSixAntsInTheForestAwayFromItsEntrances()
    {
        var (monsters, _) = Started();
        var states = monsters.States.ToArray();
        Assert.Equal(20, states.Length);
        Assert.Equal(6, Array.FindAll(states, s => s.Kind == MonsterKind.Caterpillar).Length);
        Assert.Equal(6, Array.FindAll(states, s => s.Kind == MonsterKind.Ant).Length);
        var ids = new HashSet<int>();
        foreach (var s in states)
        {
            Assert.True(s.Alive);
            Assert.True(ids.Add(s.MonsterId));
            Assert.False(Ground.Blocks(s.Position.X, s.Position.Z, s.Radius));
            Assert.True(Areas.Walkable(s.Position.X, s.Position.Z, s.Radius));
            Assert.Equal(Ground.HeightAt(s.Position.X, s.Position.Z), s.Position.Y, 4);
            Assert.Equal(s.Health, s.MaxHealth);
            Assert.False(s.Poisoned);
            if (MonsterRewards.IsHard(s.Kind))
                continue;
            Assert.Equal(Area.Forest, Areas.At(s.Position.X, s.Position.Z));
            Assert.True(Areas.Forest.Contains(s.Position.X, s.Position.Z, s.Radius));
            foreach (var entrance in new[] { Areas.ForestGapSouth, Areas.ForestGapNorth, Areas.ForestGapWest })
                Assert.True(Flat(s.Position, new Vector3(entrance.X, 0f, entrance.Y)) >= MonstersModule.SpawnAwayFromStart);
        }
        Assert.Equal(8, Array.Find(states, s => s.Kind == MonsterKind.Caterpillar).MaxHealth);
        Assert.Equal(5, Array.Find(states, s => s.Kind == MonsterKind.Ant).MaxHealth);
    }

    [Fact]
    public void KeepsFourTarantulasAndFourLadybugsAtTheDeepForestSpawnSpots()
    {
        var (monsters, _) = Started();
        var states = monsters.States.ToArray();
        var hard = Array.FindAll(states, s => MonsterRewards.IsHard(s.Kind));
        Assert.Equal(8, hard.Length);
        Assert.Equal(4, Array.FindAll(hard, s => s.Kind == MonsterKind.Tarantula).Length);
        Assert.Equal(4, Array.FindAll(hard, s => s.Kind == MonsterKind.Ladybug).Length);
        foreach (var s in hard)
            AssertOnFreeDeepForestGround(s);
        foreach (var s in hard)
            Assert.True(AtSpawnSpot(s.Position), $"{s.Kind} at {s.Position} is not at a spawn spot");
        // Each at its own spot, spread over the area.
        for (int i = 0; i < hard.Length; i++)
        for (int j = i + 1; j < hard.Length; j++)
            Assert.True(Flat(hard[i].Position, hard[j].Position) >= 15f, $"hard monsters {i} and {j} start bunched up");
        Assert.Contains(hard, s => s.Position.Z < -20f);
        Assert.Contains(hard, s => s.Position.Z > 20f);
        Assert.Contains(hard, s => s.Position.X < -230f);
        Assert.Contains(hard, s => s.Position.X > -170f);
        // None of the normal ones in the Deep Forest.
        Assert.DoesNotContain(states, s => !MonsterRewards.IsHard(s.Kind) && Areas.At(s.Position.X, s.Position.Z) == Area.DeepForest);
        Assert.Equal(18, Array.Find(states, s => s.Kind == MonsterKind.Tarantula).MaxHealth);
        Assert.Equal(25, Array.Find(states, s => s.Kind == MonsterKind.Ladybug).MaxHealth);
        Assert.Equal(MonsterKind.Tarantula, monsters.Monsters[FirstTarantula].Kind);
        Assert.Equal(MonsterKind.Ladybug, monsters.Monsters[FirstLadybug].Kind);
    }

    /// <summary>Alive, in the Deep Forest, on walkable ground clear of every obstacle, feet at ground height.</summary>
    private static void AssertOnFreeDeepForestGround(in MonsterState s)
    {
        var p = s.Position;
        Assert.True(s.Alive, $"{s.Kind} {s.MonsterId} is not alive");
        Assert.Equal(Area.DeepForest, Areas.At(p.X, p.Z));
        Assert.True(DeepForestLayout.Bounds.Contains(p.X, p.Z, s.Radius), $"{s.Kind} at {p} is not inside the Deep Forest");
        Assert.True(Areas.Walkable(p.X, p.Z, s.Radius), $"{s.Kind} at {p} is not on walkable ground");
        Assert.False(Ground.Blocks(p.X, p.Z, s.Radius), $"{s.Kind} at {p} is inside an obstacle");
        Assert.Equal(Ground.HeightAt(p.X, p.Z), p.Y, 4);
    }

    [Theory]
    [InlineData(7u)]
    [InlineData(42u)]
    [InlineData(0x5EEDF00Du)]
    public void HardMonstersRoamTheWholeDeepForestOnFreeGround(uint seed)
    {
        var (monsters, bus) = Started(seed);
        bus.SendPlayer(Places.HomeDoor.Position); // far away: nobody to chase
        bus.Record = false;
        var start = monsters.States.ToArray();
        var farthest = new float[MonstersModule.Count];
        var lastMinute = monsters.States.ToArray();
        int quietMinutes = 0;
        for (int t = 1; t <= 60 * 480; t++)
        {
            monsters.Tick(new TickContext(t, Step));
            var states = monsters.States;
            for (int i = MonstersModule.NormalCount; i < MonstersModule.Count; i++)
            {
                AssertOnFreeDeepForestGround(states[i]);
                farthest[i] = MathF.Max(farthest[i], Flat(states[i].Position, start[i].Position));
            }
            if (t % 3600 != 0)
                continue;
            // Every minute each of them has moved on.
            for (int i = MonstersModule.NormalCount; i < MonstersModule.Count; i++)
                if (Flat(states[i].Position, lastMinute[i].Position) < 3f)
                    quietMinutes++;
            lastMinute = states.ToArray();
        }
        Assert.True(quietMinutes <= 4, $"{quietMinutes} minutes a hard monster hardly moved");
        // Each leaves its spawn clearing and wanders far over the area, ladybugs too.
        for (int i = MonstersModule.NormalCount; i < MonstersModule.Count; i++)
            Assert.True(farthest[i] >= 25f, $"{start[i].Kind} {i} only got {farthest[i]} m from where it appeared");
        var end = monsters.States[MonstersModule.NormalCount..].ToArray();
        Assert.Equal(4, Array.FindAll(end, s => s.Kind == MonsterKind.Ladybug && s.Alive).Length);
        Assert.Equal(4, Array.FindAll(end, s => s.Kind == MonsterKind.Tarantula && s.Alive).Length);
    }

    [Fact]
    public void SpreadsOverTheForest()
    {
        var (monsters, _) = Started();
        var states = monsters.States[..MonstersModule.NormalCount].ToArray();
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
        Assert.Equal(60, bus.States.Count);
        var ids = new HashSet<int>();
        for (int i = 0; i < 20; i++)
            ids.Add(bus.States[40 + i].MonsterId);
        Assert.Equal(20, ids.Count);
        Assert.Empty(bus.Damage);
    }

    [Fact]
    public void WandersSlowlyInsideItsAreaAndAroundTrunks()
    {
        var (monsters, bus) = Started();
        bus.SendPlayer(Places.HomeDoor.Position, alive: false); // nobody to chase
        var previous = monsters.States.ToArray();
        var first = monsters.States.ToArray();
        var maxSpeed = new float[4];
        for (int t = 0; t < 60 * 120; t++)
        {
            bus.Record = false;
            monsters.Tick(new TickContext(t, Step));
            var states = monsters.States;
            for (int i = 0; i < states.Length; i++)
            {
                var s = states[i];
                var home = MonsterRewards.IsHard(s.Kind) ? Areas.DeepForest : Areas.Forest;
                Assert.True(home.Contains(s.Position.X, s.Position.Z, s.Radius), $"{s.Kind} left its area at {s.Position}");
                Assert.True(Areas.Walkable(s.Position.X, s.Position.Z, s.Radius), $"{s.Kind} off the ground at {s.Position}");
                Assert.False(Ground.Blocks(s.Position.X, s.Position.Z, s.Radius * 0.9f), $"{s.Kind} inside a trunk at {s.Position}");
                Assert.Equal(Ground.HeightAt(s.Position.X, s.Position.Z), s.Position.Y, 4);
                int k = (int)s.Kind;
                maxSpeed[k] = MathF.Max(maxSpeed[k], Flat(s.Position, previous[i].Position) / (float)Step);
                previous[i] = s;
            }
        }
        // Slow: no faster than its kind's wander (plus a little nudge from a neighbour).
        for (int k = 0; k < 4; k++)
            Assert.True(maxSpeed[k] <= MonstersModule.StatsOf((MonsterKind)k).WanderSpeed + 0.2f, $"{(MonsterKind)k} wandered at {maxSpeed[k]} m/s");
        // But they do move about.
        int moved = 0;
        for (int i = 0; i < 20; i++)
            if (Flat(first[i].Position, previous[i].Position) > 3f)
                moved++;
        Assert.True(moved >= 14, $"only {moved} monsters wandered");
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
    [InlineData(0, MonsterKind.Caterpillar, 50)]
    [InlineData(6, MonsterKind.Ant, 50)]
    [InlineData(FirstTarantula, MonsterKind.Tarantula, 200)]
    [InlineData(FirstLadybug, MonsterKind.Ladybug, 200)]
    public void KilledOnceWithItsReward(int index, MonsterKind kind, int reward)
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
        Assert.Equal(reward, killed.Reward);
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
        Assert.False(Ground.Blocks(m.Position.X, m.Position.Z, m.Radius));
        Assert.Equal(Area.Forest, Areas.At(m.Position.X, m.Position.Z));
        Assert.Null(monsters.Find(oldId));
        bus.Hit(oldId, 100); // the old one is gone
        Assert.Equal(5, m.Health);

        var states = monsters.States.ToArray();
        Assert.All(states, s => Assert.True(s.Alive));
        Assert.Equal(6, Array.FindAll(states, s => s.Kind == MonsterKind.Ant).Length);
        Assert.Single(bus.Killed);
    }

    [Fact]
    public void HardMonstersAreTougherThanNormalOnes()
    {
        var ant = MonstersModule.StatsOf(MonsterKind.Ant);
        var cat = MonstersModule.StatsOf(MonsterKind.Caterpillar);
        var tarantula = MonstersModule.StatsOf(MonsterKind.Tarantula);
        var ladybug = MonstersModule.StatsOf(MonsterKind.Ladybug);
        Assert.Equal(18, tarantula.MaxHealth);
        Assert.Equal(3, tarantula.BiteDamage);
        Assert.Equal(25, ladybug.MaxHealth);
        Assert.Equal(2, ladybug.BiteDamage);
        Assert.Equal(Area.DeepForest, tarantula.Home);
        Assert.Equal(Area.DeepForest, ladybug.Home);
        Assert.Equal(Area.Forest, ant.Home);
        Assert.Equal(Area.Forest, cat.Home);
        // The tarantula is fast (but the player can still outrun it); the ladybug is slow.
        Assert.True(tarantula.ChaseSpeed > ant.ChaseSpeed);
        Assert.True(tarantula.ChaseSpeed < 5f);
        Assert.True(ladybug.ChaseSpeed < cat.ChaseSpeed);
        Assert.True(ladybug.WanderSpeed < cat.WanderSpeed);
        Assert.Equal(12f, tarantula.ChaseRange);
        Assert.Equal(12f, ladybug.ChaseRange);
        Assert.Equal(10f, ant.ChaseRange);
    }

    [Theory]
    [InlineData(FirstTarantula)]
    [InlineData(FirstLadybug)]
    public void HardMonstersChaseWithinTwelveMeters(int index)
    {
        var (monsters, bus) = Started();
        var spot = DeepSpot();
        Isolate(monsters, bus, index, spot, spot + new Vector3(0f, 0f, 13f));
        Run(monsters, bus, Step);
        Assert.False(monsters.Monsters[index].Chasing);
        Assert.True(Flat(monsters.Monsters[index].Position, spot) < 0.05f);

        monsters.Place(index, spot);
        bus.SendPlayer(spot + new Vector3(0f, 0f, 11f));
        Run(monsters, bus, 0.5);
        var m = monsters.Monsters[index];
        Assert.True(m.Chasing);
        float speed = MonstersModule.StatsOf(m.Kind).ChaseSpeed;
        Assert.Equal(0.5f * speed, Flat(m.Position, spot), 1);
        Assert.Empty(bus.Damage);
    }

    [Fact]
    public void TarantulaStalksThenPouncesForThree()
    {
        var (monsters, bus) = Started();
        var spot = DeepSpot();
        var player = spot + new Vector3(3f, 0f, 0f);
        Isolate(monsters, bus, FirstTarantula, spot, player);
        var m = monsters.Monsters[FirstTarantula];

        // It crouches still for 0.5 s...
        Run(monsters, bus, Step);
        Assert.Equal(MonsterAction.Crouch, m.Action);
        Run(monsters, bus, 29 * Step);
        Assert.Equal(MonsterAction.Crouch, m.Action);
        Assert.True(Flat(m.Position, spot) < 0.01f);
        // ...then leaps (much faster than it runs) for 0.4 s and lands by the player.
        Run(monsters, bus, 2 * Step);
        Assert.Equal(MonsterAction.Leap, m.Action);
        Assert.True(new Vector2(m.Velocity.X, m.Velocity.Z).Length() > MonstersModule.StatsOf(MonsterKind.Tarantula).ChaseSpeed);
        Assert.Empty(bus.Damage);
        Run(monsters, bus, 4.0 - 32 * Step);
        Assert.Equal(new long[] { 55, 229 }, bus.DamageTicks.ToArray());
        Assert.All(bus.Damage, d => Assert.Equal(3, d.Amount));
        Assert.InRange(Flat(m.Position, player), 0.5f, MonstersModule.PounceReach);
        Assert.Empty(bus.KnockBacks);
    }

    [Fact]
    public void ThePlayerCanDodgeAPounce()
    {
        var (monsters, bus) = Started();
        var spot = DeepSpot();
        Isolate(monsters, bus, FirstTarantula, spot, spot + new Vector3(3f, 0f, 0f));
        Run(monsters, bus, 31 * Step); // it has just leapt
        Assert.Equal(MonsterAction.Leap, monsters.Monsters[FirstTarantula].Action);
        bus.SendPlayer(spot + new Vector3(3f, 0f, 3f)); // stepped aside
        Run(monsters, bus, 0.5);
        Assert.Empty(bus.Damage);
        Assert.Equal(MonsterAction.None, monsters.Monsters[FirstTarantula].Action);
    }

    [Fact]
    public void LadybugCurlsUpThenRollsIntoAChargeThatKnocksBack()
    {
        var (monsters, bus) = Started();
        var spot = DeepSpot();
        var player = spot + new Vector3(5.2f, 0f, 0f);
        Isolate(monsters, bus, FirstLadybug, spot, player);
        var m = monsters.Monsters[FirstLadybug];

        // Curled up still for 0.6 s.
        Run(monsters, bus, 36 * Step);
        Assert.Equal(MonsterAction.Curl, m.Action);
        Assert.True(Flat(m.Position, spot) < 0.01f);
        Run(monsters, bus, 2 * Step);
        Assert.Equal(MonsterAction.Charge, m.Action);
        Assert.Equal(MonstersModule.ChargeSpeed, new Vector2(m.Velocity.X, m.Velocity.Z).Length(), 2);
        // 7 m/s from 5.2 m to within 1.5 m: 32 ticks of rolling.
        Run(monsters, bus, 3.0 - 38 * Step);
        Assert.Equal(new long[] { 69 }, bus.DamageTicks.ToArray());
        Assert.Equal(2, bus.Damage[0].Amount);
        var push = Assert.Single(bus.KnockBacks);
        Assert.Equal(MonstersModule.KnockBackDistance, push.Distance);
        Assert.Equal(1f, push.Direction.X, 3); // away from the ladybug, toward +X
        Assert.Equal(0f, push.Direction.Y);
        Assert.InRange(player.X - push.From.X, 1.35f, MonstersModule.ChargeReach); // where it hit
        // It rests 3 s, curls again and rolls into the (unmoved) player.
        Run(monsters, bus, 2.0);
        Assert.Equal(new long[] { 69, 286 }, bus.DamageTicks.ToArray());
        Assert.Equal(2, bus.KnockBacks.Count);
    }

    [Fact]
    public void ALadybugChargeMissesAPlayerWhoStepsAside()
    {
        var (monsters, bus) = Started();
        var spot = DeepSpot();
        Isolate(monsters, bus, FirstLadybug, spot, spot + new Vector3(5.2f, 0f, 0f));
        Run(monsters, bus, 38 * Step);
        bus.SendPlayer(spot + new Vector3(3f, 0f, 3.5f));
        Run(monsters, bus, MonstersModule.ChargeSeconds + 0.1);
        Assert.Empty(bus.Damage);
        Assert.Empty(bus.KnockBacks);
        var m = monsters.Monsters[FirstLadybug];
        Assert.Equal(MonsterAction.None, m.Action);
        Assert.True(m.Velocity.Length() < MonstersModule.ChargeSpeed * 0.5f);
    }

    [Fact]
    public void LadybugTakesHalfDamageFromTheFrontOfItsShell()
    {
        var (monsters, bus) = Started();
        var spot = DeepSpot();
        // Facing 0 faces -Z: the player 6 m to the north is in front of it.
        Isolate(monsters, bus, FirstLadybug, spot, spot + new Vector3(0f, 0f, -6f), facing: 0f);
        var m = monsters.Monsters[FirstLadybug];
        bus.Hit(m.Id, 4);
        Assert.Equal(23, m.Health);
        bus.Hit(m.Id, 1);
        Assert.Equal(23, m.Health); // half a point, kept
        bus.Hit(m.Id, 1);
        Assert.Equal(22, m.Health);
        bus.Hit(m.Id, 3);
        Assert.Equal(21, m.Health); // 1.5, with half a point kept
        // From the side (well outside the front) and from behind: full damage.
        bus.SendPlayer(spot + new Vector3(6f, 0f, 0f));
        bus.Hit(m.Id, 4);
        Assert.Equal(17, m.Health);
        bus.SendPlayer(spot + new Vector3(0f, 0f, 6f));
        bus.Hit(m.Id, 4);
        Assert.Equal(13, m.Health);
        // The pet in front, the player behind: the pet's bite is halved, the player's arrow is not.
        bus.SendPet(spot + new Vector3(-1f, 0f, -2f));
        bus.Hit(m.Id, 2, source: 1);
        Assert.Equal(12, m.Health);
        bus.Hit(m.Id, 2, source: 0);
        Assert.Equal(10, m.Health);
    }

    [Fact]
    public void TarantulaTakesFullDamageFromTheFront()
    {
        var (monsters, bus) = Started();
        var spot = DeepSpot();
        Isolate(monsters, bus, FirstTarantula, spot, spot + new Vector3(0f, 0f, -6f), facing: 0f);
        var m = monsters.Monsters[FirstTarantula];
        bus.Hit(m.Id, 4);
        Assert.Equal(14, m.Health);
    }

    [Theory]
    [InlineData(FirstTarantula, 18)]
    [InlineData(FirstLadybug, 25)]
    public void HardMonstersArePoisonedLikeTheOthers(int index, int health)
    {
        var (monsters, bus) = Started();
        var spot = DeepSpot();
        // In front of the ladybug: poison is not halved by its shell.
        Isolate(monsters, bus, index, spot, spot + new Vector3(0f, 0f, -20f), facing: 0f);
        var m = monsters.Monsters[index];
        bus.Hit(m.Id, 0, poisonPerSecond: 2f, poisonSeconds: 3f);
        Assert.True(m.Poisoned);
        Run(monsters, bus, 1.0);
        Assert.Equal(health - 2, m.Health);
        Assert.True(monsters.States[index].Poisoned);
        Run(monsters, bus, 3.0);
        Assert.Equal(health - 6, m.Health);
        Assert.False(m.Poisoned);

        // And poison can finish one, for its 200 M¢.
        bus.Hit(m.Id, 0, poisonPerSecond: 10f, poisonSeconds: 5f, source: 1);
        Run(monsters, bus, 3.0);
        var killed = Assert.Single(bus.Killed);
        Assert.Equal(m.Kind, killed.Kind);
        Assert.Equal(200, killed.Reward);
        Assert.Equal(1, killed.Source);
    }

    [Theory]
    [InlineData(FirstTarantula, MonsterKind.Tarantula)]
    [InlineData(FirstLadybug + 1, MonsterKind.Ladybug)]
    public void ANewHardMonsterComesTwentySecondsLaterAtADeepForestSpotAwayFromThePlayer(int index, MonsterKind kind)
    {
        var (monsters, bus) = Started();
        var m = monsters.Monsters[index];
        int oldId = m.Id;
        var player = m.Position; // standing right where it was
        bus.SendPlayer(player);
        bus.Hit(oldId, 100);
        Run(monsters, bus, 19.9);
        Assert.False(m.Alive);
        for (int t = 0; t < 12 && !m.Alive; t++)
            Run(monsters, bus, Step); // stop on the tick it comes back, before it walks off
        Assert.True(m.Alive);
        Assert.Equal(kind, m.Kind);
        Assert.NotEqual(oldId, m.Id);
        Assert.Equal(MonstersModule.StatsOf(kind).MaxHealth, m.Health);
        Assert.True(AtSpawnSpot(m.Position));
        Assert.True(Flat(m.Position, player) >= MonstersModule.SpawnAwayFromPlayer);
        Assert.Equal(Area.DeepForest, Areas.At(m.Position.X, m.Position.Z));
        var states = monsters.States.ToArray();
        Assert.Equal(4, Array.FindAll(states, s => s.Kind == kind).Length);
        Assert.All(states, s => Assert.True(s.Alive));
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
        bus.SendPet(monsters.Monsters[0].Position);
        bus.Hit(monsters.Monsters[1].Id, 1, 2f, 3f);
        bus.Hit(monsters.Monsters[FirstLadybug].Id, 1);
        bus.Hit(monsters.Monsters[2].Id, 100);
        Run(monsters, bus, 1.0); // warm up
        long before = GC.GetAllocatedBytesForCurrentThread();
        Run(monsters, bus, 25.0); // includes the respawn
        bus.Hit(monsters.Monsters[3].Id, 100);
        // A tarantula pouncing and a ladybug charging at the player, far apart.
        var deep = DeepSpot();
        Isolate(monsters, bus, FirstTarantula, deep, deep + new Vector3(3f, 0f, 0f));
        monsters.Place(FirstLadybug, deep + new Vector3(6f, 0f, 0f));
        Run(monsters, bus, 6.0);
        long after = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(before, after);
    }

    [Fact]
    public void TickBeforeStartThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new MonstersModule().Tick(new TickContext(0, Step)));
    }
}
