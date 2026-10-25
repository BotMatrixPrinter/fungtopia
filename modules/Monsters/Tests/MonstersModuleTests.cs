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

    private const int FirstAnt = MonstersModule.CaterpillarCount;
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
    public void KeepsTwelveCaterpillarsAndTwelveAntsInTheForestAwayFromItsEntrances()
    {
        Assert.Equal(12, MonstersModule.CaterpillarCount);
        Assert.Equal(12, MonstersModule.AntCount);
        Assert.Equal(40, MonstersModule.Count);
        var (monsters, _) = Started();
        var states = monsters.States.ToArray();
        Assert.Equal(40, states.Length);
        Assert.Equal(12, Array.FindAll(states, s => s.Kind == MonsterKind.Caterpillar).Length);
        Assert.Equal(12, Array.FindAll(states, s => s.Kind == MonsterKind.Ant).Length);
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
    public void KeepsEightTarantulasAndEightLadybugsInTheDeepForestFillingItsSpawnSpotsFirst()
    {
        Assert.Equal(8, MonstersModule.TarantulaCount);
        Assert.Equal(8, MonstersModule.LadybugCount);
        var (monsters, _) = Started();
        var states = monsters.States.ToArray();
        var hard = Array.FindAll(states, s => MonsterRewards.IsHard(s.Kind));
        Assert.Equal(16, hard.Length);
        Assert.Equal(8, Array.FindAll(hard, s => s.Kind == MonsterKind.Tarantula).Length);
        Assert.Equal(8, Array.FindAll(hard, s => s.Kind == MonsterKind.Ladybug).Length);
        foreach (var s in hard)
        {
            AssertOnFreeDeepForestGround(s);
            Assert.True(Flat(s.Position, new Vector3(DeepForestLayout.Entrance.X, 0f, DeepForestLayout.Entrance.Y))
                >= DeepForestLayout.SpawnMinEntranceDistance, $"{s.Kind} at {s.Position} is near the entrance");
        }
        // The ten spawn spots are too few for sixteen: each spot is taken once, the rest are free spots between the trunks.
        Assert.Equal(DeepForestLayout.SpawnPoints.Length, Array.FindAll(hard, s => AtSpawnSpot(s.Position)).Length);
        foreach (var spot in DeepForestLayout.SpawnPoints)
            Assert.Contains(hard, s => Flat(s.Position, new Vector3(spot.X, 0f, spot.Y)) < 0.01f);
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
        Assert.Equal(8, Array.FindAll(end, s => s.Kind == MonsterKind.Ladybug && s.Alive).Length);
        Assert.Equal(8, Array.FindAll(end, s => s.Kind == MonsterKind.Tarantula && s.Alive).Length);
    }

    [Theory]
    [InlineData(7u)]
    [InlineData(42u)]
    [InlineData(0x5EEDF00Du)]
    public void SpreadsOverBothAreas(uint seed)
    {
        var (monsters, _) = Started(seed);
        var states = monsters.States.ToArray();
        // No two monsters start closer than the spawn spacing, in either area.
        AssertSpread(states, MonstersModule.SpawnSpacing);
        var normal = states[..MonstersModule.NormalCount];
        var hard = states[MonstersModule.NormalCount..];
        // Some in the north half, some in the south half, some east, some west.
        foreach (var group in new[] { normal, hard })
        {
            float midX = MonsterRewards.IsHard(group[0].Kind) ? (Areas.DeepForest.MinX + Areas.DeepForest.MaxX) / 2f : 0f;
            Assert.Contains(group, s => s.Position.Z < -30f);
            Assert.Contains(group, s => s.Position.Z > 30f);
            Assert.Contains(group, s => s.Position.X < midX - 30f);
            Assert.Contains(group, s => s.Position.X > midX + 30f);
        }
    }

    private static void AssertSpread(MonsterState[] states, float spacing)
    {
        for (int i = 0; i < states.Length; i++)
        for (int j = i + 1; j < states.Length; j++)
            if (states[i].Alive && states[j].Alive)
                Assert.True(Flat(states[i].Position, states[j].Position) >= spacing,
                    $"{states[i].Kind} {i} at {states[i].Position} and {states[j].Kind} {j} at {states[j].Position} are bunched up");
    }

    [Fact]
    public void PublishesEveryMonsterEveryTick()
    {
        var (monsters, bus) = Started();
        Run(monsters, bus, 3 * Step);
        Assert.Equal(3 * MonstersModule.Count, bus.States.Count);
        var ids = new HashSet<int>();
        for (int i = 0; i < MonstersModule.Count; i++)
            ids.Add(bus.States[2 * MonstersModule.Count + i].MonsterId);
        Assert.Equal(MonstersModule.Count, ids.Count);
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
        for (int i = 0; i < MonstersModule.Count; i++)
            if (Flat(first[i].Position, previous[i].Position) > 3f)
                moved++;
        Assert.True(moved >= MonstersModule.Count * 7 / 10, $"only {moved} monsters wandered");
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
        Isolate(monsters, bus, FirstAnt, spot, player);
        Run(monsters, bus, 1.0);
        Assert.Equal(9f - ant.ChaseSpeed, Flat(monsters.Monsters[FirstAnt].Position, player), 1);
    }

    [Theory]
    [InlineData(0, 1)] // a caterpillar bites for 1
    [InlineData(FirstAnt, 2)] // an ant bites for 2
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
    [InlineData(FirstAnt, MonsterKind.Ant, 50)]
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
        var m = monsters.Monsters[FirstAnt]; // ant, 5 HP
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
    public void ANewMonsterOfThatKindComesEightSecondsLaterAwayFromThePlayer()
    {
        var (monsters, bus) = Started();
        var player = new Vector3(-40f, 0f, -40f);
        bus.SendPlayer(player);
        var m = monsters.Monsters[MonstersModule.CaterpillarCount];
        int oldId = m.Id;
        bus.Hit(oldId, 100);
        Run(monsters, bus, 7.9);
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
        Assert.Equal(12, Array.FindAll(states, s => s.Kind == MonsterKind.Ant).Length);
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
    public void ANewHardMonsterComesFifteenSecondsLaterAtADeepForestSpotAwayFromThePlayer(int index, MonsterKind kind)
    {
        var (monsters, bus) = Started();
        var m = monsters.Monsters[index];
        int oldId = m.Id;
        var player = m.Position; // standing right where it was
        bus.SendPlayer(player);
        bus.Hit(oldId, 100);
        Run(monsters, bus, 14.9);
        Assert.False(m.Alive);
        for (int t = 0; t < 12 && !m.Alive; t++)
            Run(monsters, bus, Step); // stop on the tick it comes back, before it walks off
        Assert.True(m.Alive);
        Assert.Equal(kind, m.Kind);
        Assert.NotEqual(oldId, m.Id);
        Assert.Equal(MonstersModule.StatsOf(kind).MaxHealth, m.Health);
        AssertOnFreeDeepForestGround(monsters.States[index]);
        Assert.True(Flat(m.Position, player) >= MonstersModule.SpawnAwayFromPlayer);
        var states = monsters.States.ToArray();
        Assert.Equal(8, Array.FindAll(states, s => s.Kind == kind).Length);
        Assert.All(states, s => Assert.True(s.Alive));
    }

    [Fact]
    public void NormalMonstersComeBackAfterEightSecondsHardOnesAfterFifteen()
    {
        Assert.Equal(8f, MonstersModule.NormalRespawnDelay);
        Assert.Equal(15f, MonstersModule.HardRespawnDelay);
        Assert.Equal(8f, MonstersModule.RespawnDelayOf(MonsterKind.Caterpillar));
        Assert.Equal(8f, MonstersModule.RespawnDelayOf(MonsterKind.Ant));
        Assert.Equal(15f, MonstersModule.RespawnDelayOf(MonsterKind.Tarantula));
        Assert.Equal(15f, MonstersModule.RespawnDelayOf(MonsterKind.Ladybug));

        // One of each kind defeated on the same tick, the player far away: each back on its tick.
        var (monsters, bus) = Started();
        bus.SendPlayer(Places.HomeDoor.Position);
        int[] slots = { 0, FirstAnt, FirstTarantula, FirstLadybug };
        foreach (int i in slots)
            bus.Hit(monsters.Monsters[i].Id, 1000);
        var back = new long[slots.Length];
        for (int t = 1; t <= 60 * 16; t++)
        {
            Run(monsters, bus, Step);
            for (int k = 0; k < slots.Length; k++)
                if (back[k] == 0 && monsters.Monsters[slots[k]].Alive)
                    back[k] = t;
        }
        Assert.Equal(new long[] { 480, 480, 900, 900 }, back);
        // Rewards stay as they were.
        Assert.Equal(new[] { 50, 50, 200, 200 }, bus.Killed.ConvertAll(k => k.Reward).ToArray());
    }

    [Theory]
    [InlineData(7u, 0f, 0f)] // the player in the middle of Mushroom Forest
    [InlineData(42u, -40f, 50f)]
    [InlineData(7u, -200f, 0f)] // ... in the middle of the Deep Forest
    [InlineData(42u, -150f, -65f)] // ... on a Deep Forest spawn spot
    public void RespawnsOnFreeGroundAwayFromThePlayerAndSpreadOut(uint seed, float px, float pz)
    {
        var (monsters, bus) = Started(seed);
        bus.Record = false;
        var player = new Vector3(px, Ground.HeightAt(px, pz), pz);
        var ids = new int[MonstersModule.Count];
        for (int i = 0; i < ids.Length; i++)
            ids[i] = monsters.Monsters[i].Id;
        int respawns = 0, hardOffSpots = 0, next = 0;
        for (int t = 1; t <= 60 * 120; t++)
        {
            bus.SendPlayer(player, alive: false); // standing there, nobody chases
            if (t % 15 == 0)
            {
                // Four kills a second, round the slots: the areas stay busy with respawns.
                var victim = monsters.Monsters[next];
                next = (next + 1) % MonstersModule.Count;
                if (victim.Alive)
                    bus.Hit(victim.Id, 1000);
            }
            monsters.Tick(new TickContext(t, Step));
            var states = monsters.States.ToArray();
            for (int i = 0; i < states.Length; i++)
            {
                var s = states[i];
                if (!s.Alive || s.MonsterId == ids[i])
                    continue;
                ids[i] = s.MonsterId;
                respawns++;
                var p = s.Position;
                var home = MonsterRewards.IsHard(s.Kind) ? Area.DeepForest : Area.Forest;
                Assert.Equal(home, Areas.At(p.X, p.Z));
                Assert.True(Areas.Bounds(home).Contains(p.X, p.Z, MonstersModule.EdgeMargin), $"{s.Kind} at {p} is at the edge");
                Assert.True(Areas.Walkable(p.X, p.Z, s.Radius), $"{s.Kind} at {p} is not on walkable ground");
                Assert.False(Ground.Blocks(p.X, p.Z, s.Radius), $"{s.Kind} at {p} is inside an obstacle");
                Assert.Equal(Ground.HeightAt(p.X, p.Z), p.Y, 4);
                Assert.True(Flat(p, player) >= MonstersModule.SpawnAwayFromPlayer, $"{s.Kind} came back {Flat(p, player)} m from the player");
                // Not on top of another monster: the full spawn spacing to every other alive one.
                for (int j = 0; j < states.Length; j++)
                    if (j != i && states[j].Alive)
                        Assert.True(Flat(p, states[j].Position) >= MonstersModule.SpawnSpacing,
                            $"{s.Kind} came back {Flat(p, states[j].Position)} m from {states[j].Kind} {j}");
                if (home == Area.DeepForest && !AtSpawnSpot(p))
                    hardOffSpots++;
            }
        }
        Assert.True(respawns >= 300, $"only {respawns} respawns");
        Assert.True(hardOffSpots > 0, "no hard monster came back away from the spawn spots");
    }

    [Fact]
    public void AHardMonsterComesBackAtAFreeSpotWhenEverySpawnSpotIsTaken()
    {
        var (monsters, bus) = Started();
        var player = Places.HomeDoor.Position;
        bus.SendPlayer(player, alive: false);
        var m = monsters.Monsters[FirstLadybug];
        bus.Hit(m.Id, 1000);
        Run(monsters, bus, 14.9);
        // Every spawn spot has a monster on it just before the ladybug comes back.
        var spots = DeepForestLayout.SpawnPoints;
        for (int k = 0, i = FirstTarantula; k < spots.Length; i++)
            if (i != FirstLadybug)
                monsters.Place(i, new Vector3(spots[k].X, 0f, spots[k++].Y));
        for (int t = 0; t < 12 && !m.Alive; t++)
            Run(monsters, bus, Step);
        Assert.True(m.Alive);
        var state = monsters.States[FirstLadybug];
        AssertOnFreeDeepForestGround(state);
        Assert.False(AtSpawnSpot(m.Position));
        foreach (var s in spots)
            Assert.True(Flat(m.Position, new Vector3(s.X, 0f, s.Y)) >= MonstersModule.SpawnSpacing);
    }

    [Theory]
    [InlineData(7u)]
    [InlineData(42u)]
    public void SameSeedSameRespawns(uint seed)
    {
        var (a, busA) = Started(seed);
        var (b, busB) = Started(seed);
        foreach (var (monsters, bus) in new[] { (a, busA), (b, busB) })
        {
            bus.SendPlayer(new Vector3(-150f, 0f, -65f));
            for (int i = 0; i < MonstersModule.Count; i += 3)
                bus.Hit(monsters.Monsters[i].Id, 1000);
            Run(monsters, bus, 16.0);
        }
        Assert.Equal(a.States.ToArray(), b.States.ToArray());
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
    public void TickBeforeStartThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new MonstersModule().Tick(new TickContext(0, Step)));
    }
}

/// <summary>
/// The monsters' mushrooms are vivid (the game's rule for every mushroom): the exported models'
/// cap colors are clearly saturated and bright, spots pure white, stems a clean light cream, every
/// other color as it was; the view makes caps glossy with a rim and glowing caps twice as bright.
/// </summary>
public class MushroomLookTests
{
    private static readonly string[] Models = { "caterpillar", "ant", "tarantula", "ladybug" };

    /// <summary>The colors of everything that is not a mushroom, as they were before the mushrooms went vivid.</summary>
    private static readonly Dictionary<string, (float R, float G, float B)> Unchanged = new()
    {
        ["Eye"] = (0.07f, 0.04f, 0.06f),
        ["EyeShine"] = (1f, 1f, 1f),
        ["Blush"] = (1f, 0.45f, 0.48f),
        ["Mouth"] = (0.4f, 0.12f, 0.14f),
        ["Body"] = (0.55f, 0.82f, 0.32f),
        ["BodyLight"] = (0.85f, 0.93f, 0.5f),
        ["Feet"] = (0.36f, 0.58f, 0.22f),
        ["BackDot"] = (1f, 0.96f, 0.9f),
        ["Shell"] = (0.72f, 0.36f, 0.2f),
        ["Stripe"] = (0.4f, 0.2f, 0.14f),
        ["Leg"] = (0.42f, 0.22f, 0.15f),
        ["Brow"] = (0.25f, 0.12f, 0.1f),
        ["Fur"] = (0.4f, 0.27f, 0.4f),
        ["FurLight"] = (0.58f, 0.42f, 0.56f),
        ["FurDark"] = (0.24f, 0.16f, 0.26f),
        ["Fang"] = (1f, 0.97f, 0.9f),
        ["LadyShell"] = (0.9f, 0.16f, 0.14f),
        ["LadySpot"] = (0.1f, 0.07f, 0.09f),
        ["Face"] = (0.99f, 0.86f, 0.72f),
    };

    private static string ModelsDir()
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "project.godot")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return System.IO.Path.Combine(dir!.FullName, "modules", "Monsters", "Models");
    }

    /// <summary>The base colors of a .glb's materials, by material name.</summary>
    private static Dictionary<string, (float R, float G, float B)> MaterialsOf(string model)
    {
        var bytes = System.IO.File.ReadAllBytes(System.IO.Path.Combine(ModelsDir(), model + ".glb"));
        int jsonLength = BitConverter.ToInt32(bytes, 12);
        var json = System.Text.Json.JsonDocument.Parse(new ReadOnlyMemory<byte>(bytes, 20, jsonLength)).RootElement;
        var result = new Dictionary<string, (float, float, float)>();
        foreach (var m in json.GetProperty("materials").EnumerateArray())
        {
            // glTF leaves out a white baseColorFactor (its default).
            string name = m.GetProperty("name").GetString()!;
            if (m.TryGetProperty("pbrMetallicRoughness", out var pbr) && pbr.TryGetProperty("baseColorFactor", out var c))
                result[name] = (c[0].GetSingle(), c[1].GetSingle(), c[2].GetSingle());
            else
                result[name] = (1f, 1f, 1f);
        }
        return result;
    }

    private static (float Hue, float Saturation, float Value) Hsv((float R, float G, float B) c)
    {
        float max = MathF.Max(c.R, MathF.Max(c.G, c.B)), min = MathF.Min(c.R, MathF.Min(c.G, c.B));
        float d = max - min;
        float hue = d == 0f ? 0f : max == c.R ? 60f * (((c.G - c.B) / d + 6f) % 6f) : max == c.G ? 60f * ((c.B - c.R) / d + 2f) : 60f * ((c.R - c.G) / d + 4f);
        return (hue, max == 0f ? 0f : d / max, max);
    }

    [Fact]
    public void EveryModelHasVividMushroomCaps()
    {
        foreach (var model in Models)
        {
            var mats = MaterialsOf(model);
            Assert.True(mats.ContainsKey("Cap"), $"{model}: no red mushroom cap");
            Assert.True(mats.ContainsKey("Spots") && mats.ContainsKey("Stem"), $"{model}: no spots or stem");
            foreach (var (name, color) in mats)
            {
                if (!MonsterLook.IsCap(name))
                    continue;
                var (_, s, v) = Hsv(color);
                // Before: saturation 0.37 (cream) to 0.78 (red); now every cap is clearly saturated and bright.
                Assert.True(s >= 0.85f, $"{model}: cap {name} {color} saturation {s}");
                Assert.True(v >= 0.95f, $"{model}: cap {name} {color} brightness {v}");
            }
        }
    }

    [Fact]
    public void RedCapIsAPureCherryRed()
    {
        var (hue, s, v) = Hsv(MaterialsOf("caterpillar")["Cap"]);
        Assert.True(hue <= 3f || hue >= 350f, $"hue {hue}: not brownish or pinkish");
        Assert.True(s >= 0.9f && v >= 0.95f, $"{s} {v}");
    }

    [Fact]
    public void LadybugCapsAreFourDistinctVividColors()
    {
        var mats = MaterialsOf("ladybug");
        var caps = new List<string>();
        foreach (var name in mats.Keys)
            if (MonsterLook.IsCap(name))
                caps.Add(name);
        caps.Sort(StringComparer.Ordinal);
        Assert.Equal(new[] { "Cap", "CapGold", "CapPink", "CapTan" }, caps);
        var hues = new List<float>();
        foreach (var cap in caps)
            hues.Add(Hsv(mats[cap]).Hue);
        // Red ~0, orange ~30, gold ~45, pink ~330: no two the same color.
        for (int i = 0; i < hues.Count; i++)
            for (int j = i + 1; j < hues.Count; j++)
            {
                float d = MathF.Abs(hues[i] - hues[j]);
                Assert.True(MathF.Min(d, 360f - d) >= 10f, $"{caps[i]} and {caps[j]} look alike");
            }
    }

    [Fact]
    public void SpotsArePureWhiteAndStemsCleanLightCream()
    {
        foreach (var model in Models)
        {
            var mats = MaterialsOf(model);
            Assert.Equal((1f, 1f, 1f), mats["Spots"]);
            var stem = mats["Stem"];
            var (hue, s, v) = Hsv(stem);
            Assert.True(v >= 0.98f && s >= 0.1f && s <= 0.2f && hue >= 30f && hue <= 50f, $"{model}: stem {stem}");
        }
    }

    [Fact]
    public void EveryOtherColorStaysAsItWas()
    {
        int seen = 0;
        foreach (var model in Models)
            foreach (var (name, color) in MaterialsOf(model))
            {
                if (MonsterLook.IsMushroom(name))
                    continue;
                Assert.True(Unchanged.TryGetValue(name, out var was), $"{model}: unexpected material {name}");
                Assert.True(MathF.Abs(color.R - was.R) < 1e-4f && MathF.Abs(color.G - was.G) < 1e-4f && MathF.Abs(color.B - was.B) < 1e-4f,
                    $"{model}: {name} changed to {color}");
                seen++;
            }
        Assert.True(seen >= Unchanged.Count);
    }

    [Fact]
    public void CapsAreGlossyWithARimAndNothingElseIs()
    {
        foreach (var cap in new[] { "Cap", "CapTan", "CapPink", "CapGold" })
        {
            Assert.True(MonsterLook.IsMushroom(cap));
            Assert.InRange(MonsterLook.Gloss(cap), 0.2f, 0.7f);
            Assert.InRange(MonsterLook.Rim(cap), 0.15f, 0.6f);
        }
        Assert.True(MonsterLook.IsMushroom("Spots") && MonsterLook.IsMushroom("Stem"));
        foreach (var other in Unchanged.Keys)
        {
            Assert.False(MonsterLook.IsMushroom(other), other);
            Assert.Equal(0f, MonsterLook.Gloss(other));
            Assert.Equal(0f, MonsterLook.Rim(other));
        }
    }

    [Fact]
    public void GlowingCapsGlowTwiceAsBrightAndOtherGlowsStay()
    {
        // The ladybug's caps glowed at 0.2 before.
        foreach (var cap in new[] { "Cap", "CapTan", "CapPink", "CapGold" })
            Assert.Equal(0.4f, MonsterLook.SelfLight(MonsterKind.Ladybug, cap), 4);
        Assert.Equal(0f, MonsterLook.SelfLight(MonsterKind.Caterpillar, "Cap"));
        Assert.Equal(0f, MonsterLook.SelfLight(MonsterKind.Tarantula, "Cap"));
        Assert.Equal(0.32f, MonsterLook.SelfLight(MonsterKind.Ladybug, "LadyShell"), 4);
        Assert.Equal(0.6f, MonsterLook.SelfLight(MonsterKind.Ant, "EyeShine"), 4);
        Assert.Equal(0f, MonsterLook.SelfLight(MonsterKind.Ladybug, "Face"));
        Assert.Equal(0f, MonsterLook.SelfLight(MonsterKind.Ladybug, "Stem"));
        // Spots stay crisp white in shade, a faint light, far below a glowing cap's.
        Assert.InRange(MonsterLook.SelfLight(MonsterKind.Ant, "Spots"), 0.1f, 0.3f);
    }

    [Fact]
    public void ToonShaderDrawsTheGlossAndRim()
    {
        string shader = System.IO.File.ReadAllText(System.IO.Path.Combine(ModelsDir(), "..", "Godot", "Shaders", "toon.gdshader"));
        Assert.Contains("uniform float gloss", shader);
        Assert.Contains("uniform float rim_light", shader);
        string light = shader[shader.IndexOf("void light()", StringComparison.Ordinal)..];
        Assert.Contains("gloss", light);
        Assert.Contains("rim_light", light);
        Assert.Contains("SPECULAR_LIGHT", light);
        string view = System.IO.File.ReadAllText(System.IO.Path.Combine(ModelsDir(), "..", "Godot", "MonsterModel.cs"));
        Assert.Contains("MonsterLook.Gloss(name)", view);
        Assert.Contains("MonsterLook.Rim(name)", view);
        Assert.Contains("MonsterLook.SelfLight(kind, name)", view);
    }
}
