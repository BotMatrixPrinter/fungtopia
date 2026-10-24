using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Forest;
using Fungtopia.Input;
using Fungtopia.Monsters;
using Fungtopia.Player;
using Fungtopia.Shell;
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

    /// <summary>A bus that drops everything, for the allocation test.</summary>
    private sealed class NullBus : IMessageBus
    {
        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct { }
        public void Publish<T>(in T message) where T : struct { }
    }

    private readonly FakeBus _bus = new();
    private readonly BowModule _bow = new();
    private readonly List<MonsterState> _monsters = new();
    private PlayerState _player;
    private long _tick;

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

    private static PlayerInput Input(bool fire, Vector3 aim) =>
        new(Vector2.Zero, Vector2.Zero, false, fire, true, Vector3.Normalize(aim));

    /// <summary>One tick as the host runs it: Input, Player and Monsters publish, then the bow ticks.</summary>
    private void TickWith(bool fire, Vector3 aim)
    {
        _bus.Publish(Input(fire, aim));
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
    public void ArrowStartsAtChestHeightAndFliesAlongTheAim()
    {
        var aim = Vector3.Normalize(new Vector3(0.3f, 0.1f, -1f));
        Shoot(36, aim);
        var a = OnlyArrow();
        var flat = Vector3.Normalize(new Vector3(aim.X, 0f, aim.Z));
        var start = Spot + new Vector3(0f, BowModule.ChestHeight, 0f) + flat * BowModule.LaunchForward;
        // It was launched at the start and has flown one tick since.
        Assert.True(Vector3.Distance(start, a.PreviousPosition) < 1e-4f, $"{a.PreviousPosition} vs {start}");
        var step = a.Position - a.PreviousPosition;
        Assert.Equal(45f * Dt, step.Length(), 2);
        Assert.True(Vector3.Dot(Vector3.Normalize(step), aim) > 0.999f);
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
        var v = OnlyArrow().Velocity;
        Assert.True(Vector3.Dot(Vector3.Normalize(v), -Vector3.UnitX) > 0.999f);
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

    // ---- Hits ----

    [Fact]
    public void HittingAMonsterPublishesItsDamageAndPoisonAndStopsTheArrow()
    {
        _monsters.Add(Monster(7, OnGround(Spot.X, Spot.Z - 6f)));
        _monsters.Add(Monster(8, OnGround(Spot.X, Spot.Z - 9f))); // right behind it
        Shoot(36, North);
        Run(30, false, North);
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
        Shoot(drawTicks, North);
        Run(20, false, North);
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
        // Its sphere sits right on the arrow's line (at chest height), 6.675 m ahead: between two ticks' positions.
        var feet = new Vector3(Spot.X, Spot.Y + BowModule.ChestHeight - 0.3f, Spot.Z - 6.675f);
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

    [Fact]
    public void TicksDoNotAllocate()
    {
        var bow = new BowModule();
        bow.Start(new NullBus());
        var player = PlayerAt(Spot);
        var monster = Monster(1, OnGround(Spot.X, Spot.Z - 20f));
        var fire = Input(true, North);
        var release = Input(false, North);
        void Frame(in PlayerInput input, long tick)
        {
            bow.Handle(input);
            bow.Handle(player);
            bow.Handle(monster);
            bow.Tick(new TickContext(tick, Step));
        }
        for (int i = 0; i < 200; i++)
            Frame(i % 10 < 8 ? fire : release, i); // warm up
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 200; i < 2000; i++)
            Frame(i % 10 < 8 ? fire : release, i);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }
}
