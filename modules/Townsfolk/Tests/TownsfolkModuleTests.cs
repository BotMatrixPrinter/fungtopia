using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Fungtopia.DayNight;
using Fungtopia.Map;
using Fungtopia.Player;
using Fungtopia.Shell;
using Xunit;

namespace Fungtopia.Townsfolk.Tests;

public class TownsfolkModuleTests
{
    /// <summary>Fake of the shell's bus: hands messages to the module, counts anything it publishes.</summary>
    private sealed class FakeBus : IMessageBus
    {
        public int Published;
        public readonly List<TeleportPlayer> Teleports = new();
        public readonly List<Type> Subscribed = new();

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct => Subscribed.Add(typeof(T));

        public void Publish<T>(in T message) where T : struct
        {
            Published++;
            if (message is TeleportPlayer teleport)
                Teleports.Add(teleport);
        }
    }

    private const double Step = 1.0 / 60;

    private static readonly Vector3 Plaza = new(0f, Ground.HeightAt(0f, -215f), -215f);

    private sealed class Rig
    {
        public readonly FakeBus Bus = new();
        public readonly TownsfolkModule Town = new();
        public long Ticks;
        public float Hour = 11f;

        public Rig()
        {
            Town.Start(Bus);
            Player(Plaza + new Vector3(0f, 0f, 30f));
        }

        public void Player(Vector3 at, bool alive = true) =>
            Town.Handle(new PlayerState(at, 0f, Vector3.Zero, true, 10, 10, alive, CharacterKind.Girl));

        /// <summary>Ticks for <paramref name="seconds"/>, the clock running at the game's pace (50 s an hour) unless frozen; calls <paramref name="each"/> after every tick.</summary>
        public void Run(float seconds, Action? each = null, bool clock = false)
        {
            int n = (int)(seconds * 60);
            for (int k = 0; k < n; k++)
            {
                if (clock)
                    Hour = (Hour + (float)Step * 24f / TimeOfDay.RealSecondsPerDay) % 24f;
                Town.Handle(new TimeOfDay(1, Hour, TimeOfDay.IsNightAt(Hour)));
                Town.Tick(new TickContext(Ticks++, Step));
                each?.Invoke();
            }
        }
    }

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);

    [Fact]
    public void TickBeforeStartThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new TownsfolkModule().Tick(new TickContext(0, Step)));
    }

    [Fact]
    public void ReadsThePlayerAndTheClockAndPublishesNothing()
    {
        var rig = new Rig();
        Assert.Contains(typeof(PlayerState), rig.Bus.Subscribed);
        Assert.Contains(typeof(TimeOfDay), rig.Bus.Subscribed);
        rig.Run(60f);
        Assert.Equal(0, rig.Bus.Published);
    }

    [Fact]
    public void TheScreenshotStartTeleportsThePlayerOnceToThePlaza()
    {
        var rig = new Rig();
        rig.Town.TeleportPlayerOnce(TownsfolkModule.ScreenshotSpot, TownsfolkModule.ScreenshotYaw);
        rig.Run(2f);
        var teleport = Assert.Single(rig.Bus.Teleports);
        Assert.Equal(1, rig.Bus.Published);
        Assert.Equal(TownsfolkModule.ScreenshotSpot, teleport.Position);
        var at = new Vector2(teleport.Position.X, teleport.Position.Z);
        Assert.True(CityLife.Clear(at, 0.5f));
        Assert.True(Vector2.Distance(at, Buildings.CityPlaza) < 25f);
    }

    [Fact]
    public void TwentyFiveFolkWithAVariedSeededMixOfLooks()
    {
        var a = new TownsfolkModule().Folk.ToArray();
        var b = new TownsfolkModule().Folk.ToArray();
        Assert.Equal(25, a.Length);
        // The same every run.
        for (int i = 0; i < a.Length; i++)
            Assert.Equal(a[i].Look, b[i].Look);
        var looks = a.Select(f => f.Look).ToList();
        Assert.True(looks.Select(l => l.Skin).Distinct().Count() >= 4);
        Assert.True(looks.Select(l => l.Outfit).Distinct().Count() >= 6);
        Assert.True(looks.Select(l => l.Legs).Distinct().Count() >= 4);
        // Dresses (the triangle) and capsules, both plenty.
        Assert.InRange(looks.Count(l => l.Shape == BodyShape.Dress), 6, 19);
        // Bow ties only on capsules, most of them; bows only on heads without a hat.
        Assert.All(looks.Where(l => l.Accessory == Accessory.BowTie), l => Assert.Equal(BodyShape.Capsule, l.Shape));
        Assert.All(looks.Where(l => l.Accessory == Accessory.Bow), l => Assert.Equal(HatStyle.None, l.Hat));
        Assert.True(looks.Count(l => l.Accessory == Accessory.BowTie) >= 4);
        Assert.True(looks.Count(l => l.Accessory == Accessory.Bow) >= 2);
        Assert.True(looks.Select(l => l.Hat).Where(h => h != HatStyle.None).Distinct().Count() >= 3);
        Assert.InRange(looks.Count(l => l.Hat != HatStyle.None), 5, 18);
        Assert.Equal(25, looks.Select(l => (l.Skin, l.Shape, l.Outfit, l.Legs, l.Accessory, l.Hat, l.HatColor)).Distinct().Count());
        // Heights between 1.19 and 1.37 m, spread out; the model (1.30 m, its body 25% shorter than
        // the old 1.47 m one's) is drawn scaled to them, each at their own size as before
        // (0.92..1.05 times the model): the shorter body takes the head down with it.
        Assert.Equal(1.30f, Look.ModelHeight);
        Assert.All(looks, l => Assert.InRange(l.Height, 1.19f, 1.37f));
        Assert.True(looks.Max(l => l.Height) - looks.Min(l => l.Height) > 0.1f);
        Assert.All(looks, l => Assert.Equal(l.Height / Look.ModelHeight, l.Scale, 4));
        Assert.All(looks, l => Assert.InRange(l.Scale, 1.35f / 1.47f - 0.005f, 1.55f / 1.47f + 0.005f));
    }

    [Fact]
    public void WalkingSwingsTheLegsInTurn()
    {
        // Standing still: straight legs.
        var still = Stride.Walk(1.3f, 0f);
        Assert.All(new[] { still.HipL, still.KneeL, still.HipR, still.KneeR }, a => Assert.Equal(0f, a, 5));
        float maxSwing = 0f;
        bool leftAhead = false, rightAhead = false;
        for (int i = 0; i < 64; i++)
        {
            var legs = Stride.Walk(i * MathF.Tau / 64f, 1f);
            // One leg forward while the other is back; knees only bend back.
            Assert.Equal(-legs.HipL, legs.HipR, 4);
            Assert.True(legs.KneeL <= 0f && legs.KneeR <= 0f);
            maxSwing = MathF.Max(maxSwing, MathF.Abs(legs.HipL));
            leftAhead |= legs.HipL > 0.3f;
            rightAhead |= legs.HipR > 0.3f;
            // Feet stay near the ground.
            var (_, ankle) = Stride.Joints(legs.HipL, legs.KneeL, 1f);
            Assert.InRange(ankle.Y, 0f, 0.2f);
        }
        Assert.True(leftAhead && rightAhead);
        Assert.InRange(maxSwing, 0.35f, 0.7f);
    }

    [Fact]
    public void SittingBendsTheLegsOverTheBenchEdge()
    {
        foreach (float scale in new[] { 1.19f / Look.ModelHeight, 1f, 1.37f / Look.ModelHeight })
        {
            float lift = Stride.SitLift(scale);
            // The body's bottom on the seat.
            Assert.Equal(Stride.SeatTop, Stride.BodyBottom * scale + lift, 3);
            for (int i = 0; i < 20; i++)
            {
                var legs = Stride.Sit(i * 0.37f);
                foreach (var (hip, knee) in new[] { (legs.HipL, legs.KneeL), (legs.HipR, legs.KneeR) })
                {
                    var (kneeAt, ankle) = Stride.Joints(hip, knee, scale, lift);
                    // The thigh lies along the seat out past its front edge (forward is -Z) ...
                    Assert.True(-kneeAt.Z > Stride.SeatEdge, $"knee {kneeAt}");
                    Assert.InRange(kneeAt.Y, Stride.SeatTop, Stride.SeatTop + 0.12f);
                    // ... and the shin hangs down in front of it, the foot off the ground.
                    Assert.True(ankle.Y < kneeAt.Y - 0.1f * scale, $"ankle {ankle}");
                    Assert.True(-ankle.Z > Stride.SeatEdge);
                    Assert.True(ankle.Y > 0.1f);
                }
            }
        }
    }

    [Fact]
    public void TheSpotsAreInPetCityAndClear()
    {
        Assert.True(CityLife.Doors.Length >= 10);
        Assert.True(CityLife.Seats.Length >= 10);
        Assert.True(CityLife.Windows.Length >= 10);
        Assert.True(CityLife.BusyCount > 50);
        foreach (var door in CityLife.Doors)
            Assert.True(CityLife.Clear(door.At, TownsfolkModule.FolkRadius), $"door {door.At}");
        foreach (var at in CityLife.SeatApproach)
            Assert.True(CityLife.Clear(at, TownsfolkModule.FolkRadius), $"seat {at}");
        foreach (var at in CityLife.Open)
            Assert.True(CityLife.InCity(at) && Areas.PetCity.Contains(at.X, at.Y));
        // Every seat is on a Pet City bench.
        foreach (var seat in CityLife.Seats)
        {
            var bench = Props.All[seat.Index];
            Assert.Equal(PropKind.Bench, bench.Kind);
            Assert.Equal(Area.PetCity, bench.Area);
            Assert.True(Vector2.Distance(seat.At, bench.Center) <= bench.Radius);
        }
        // Crows perch on roofs, high up; butterflies' flowers are on the planters and the plaza.
        Assert.Equal(Buildings.All.ToArray().Count(b => b.Area == Area.PetCity), CityLife.Roofs.Length);
        Assert.All(CityLife.Roofs.ToArray(), r => Assert.True(r.Y > Ground.HeightAt(r.X, r.Z) + 10f));
        Assert.True(CityLife.Flowers.Length >= 10);
    }

    [Fact]
    public void BusyLifeOverTenMinutesStaysInPetCityOffObstaclesAndClearOfThePlayer()
    {
        var rig = new Rig();
        // The player walks slowly up the avenue and back, then stands on the plaza.
        var seen = new HashSet<FolkState>();
        var chats = 0;
        var sits = 0;
        var browsing = 0;
        var cameOut = 0;
        var wasInside = new bool[25];
        var pairs = new HashSet<(int, int)>();
        float worstPlayerGap = float.MaxValue;
        var player = Plaza + new Vector3(0f, 0f, 30f);
        rig.Run(600f, () =>
        {
            float t = (float)rig.Town.Seconds;
            if (t < 120f)
                player = new Vector3(0f, 0f, -170f - t * 0.35f);
            else
                player = new Vector3(2f, 0f, -212f);
            player.Y = Ground.HeightAt(player.X, player.Z);
            rig.Player(player);
            var folk = rig.Town.Folk;
            for (int i = 0; i < folk.Length; i++)
            {
                var f = folk[i];
                seen.Add(f.State);
                if (wasInside[i] && f.Visible)
                    cameOut++;
                wasInside[i] = !f.Visible;
                if (!f.Visible)
                    continue;
                var at = Flat(f.Position);
                Assert.True(CityLife.InCity(at, 0.3f), $"folk {i} left Pet City at {at}");
                Assert.Equal(Ground.HeightAt(at.X, at.Y), f.Position.Y, 3);
                if (f.State == FolkState.Sitting)
                {
                    sits++;
                    Assert.Contains(CityLife.Seats.ToArray(), s => Vector2.Distance(s.At, at) < 0.05f);
                    continue;
                }
                Assert.True(Areas.Walkable(at.X, at.Y, 0.1f) && !Ground.Blocks(at.X, at.Y, 0.1f), $"folk {i} on an obstacle at {at}");
                if (f.State == FolkState.Walking && f.Speed > 0f)
                    worstPlayerGap = MathF.Min(worstPlayerGap, Vector2.Distance(at, Flat(player)));
                if (f.State == FolkState.Browsing)
                    browsing++;
                if (f.State == FolkState.Chatting)
                {
                    chats++;
                    var other = folk[f.Partner];
                    Assert.Equal(FolkState.Chatting, other.State);
                    Assert.Equal(i, other.Partner);
                    Assert.NotEqual(f.Speaking, other.Speaking);
                    Assert.InRange(Vector2.Distance(at, Flat(other.Position)), 0.5f, 2.2f);
                    pairs.Add((Math.Min(i, f.Partner), Math.Max(i, f.Partner)));
                }
            }
        });
        Assert.Contains(FolkState.Walking, seen);
        Assert.Contains(FolkState.Inside, seen);
        Assert.True(pairs.Count >= 4, $"chatting pairs {pairs.Count}");
        Assert.True(chats > 0 && sits > 0 && browsing > 0);
        Assert.True(cameOut >= 5, $"came out {cameOut}");
        Assert.True(worstPlayerGap >= TownsfolkModule.PlayerClearance - 0.4f, $"walked within {worstPlayerGap} m of the player");
        // Most are out by day.
        Assert.True(rig.Town.Folk.ToArray().Count(f => f.Visible) >= 15);
    }

    [Fact]
    public void ChattersFaceEachOther()
    {
        var rig = new Rig();
        bool checkedOne = false;
        rig.Run(300f, () =>
        {
            var folk = rig.Town.Folk;
            for (int i = 0; i < folk.Length; i++)
            {
                var f = folk[i];
                // After a moment of turning, each faces the other.
                if (f.State != FolkState.Chatting || f.Timer > 5f)
                    continue;
                var toOther = Vector2.Normalize(Flat(folk[f.Partner].Position - f.Position));
                Assert.True(Vector2.Dot(CityLife.Facing(f.Yaw), toOther) > 0.9f);
                checkedOne = true;
            }
        });
        Assert.True(checkedOne);
    }

    [Fact]
    public void StandingFolkStepAsideWhenThePlayerWalksUp()
    {
        var rig = new Rig();
        rig.Run(120f);
        // Walk the player right onto the first one standing still (not sitting).
        int target = -1;
        for (int k = 0; k < 600 && target < 0; k++)
        {
            rig.Run(0.1f);
            var folk = rig.Town.Folk;
            for (int i = 0; i < folk.Length; i++)
                if (folk[i].State is FolkState.Browsing or FolkState.Chatting && folk[i].Timer > 4f)
                    target = i;
        }
        Assert.True(target >= 0);
        var start = rig.Town.Folk[target].Position;
        rig.Player(start);
        rig.Run(1.5f);
        var now = rig.Town.Folk[target];
        Assert.True(Vector2.Distance(Flat(now.Position), Flat(start)) >= TownsfolkModule.StepAsideRadius - 0.1f);
        Assert.Equal(0, rig.Bus.Published);
    }

    [Fact]
    public void OnlyNightOwlsStayOutAtNightAndTheRestComeOutAfterDawn()
    {
        var rig = new Rig { Hour = 16.5f };
        rig.Run(1f);
        var owls = rig.Town.Folk.ToArray().Count(f => f.NightOwl);
        Assert.InRange(owls, 7, 10);
        // To 22:00 at the game's pace (50 s an hour).
        int lateOut = 0;
        rig.Run(5.5f * 50f, () =>
        {
            if (rig.Hour >= 20f && rig.Hour < 21f)
                lateOut = Math.Max(lateOut, rig.Town.Folk.ToArray().Count(f => !f.NightOwl && f.Visible));
        }, clock: true);
        Assert.Equal(0, lateOut);
        var folk = rig.Town.Folk.ToArray();
        Assert.True(rig.Town.IsNight);
        Assert.All(folk.Where(f => !f.NightOwl), f => Assert.False(f.Visible));
        // The owls are mostly out (some may pop into a shop).
        Assert.True(folk.Count(f => f.Visible) >= owls - 3);
        // On to 9:30 the next morning.
        rig.Run(11.5f * 50f, clock: true);
        folk = rig.Town.Folk.ToArray();
        Assert.All(folk, f => Assert.False(f.InForNight));
        Assert.True(folk.Count(f => f.Visible) >= 17, $"out by day: {folk.Count(f => f.Visible)}");
    }

    [Fact]
    public void ComingBackAtNightFindsTheRestIndoors()
    {
        var rig = new Rig();
        rig.Run(5f);
        rig.Player(new Vector3(0f, 0f, 215f));
        rig.Hour = 23f;
        rig.Run(5f);
        Assert.False(rig.Town.Active);
        rig.Player(Plaza);
        rig.Run(0.1f);
        Assert.True(rig.Town.Active);
        Assert.All(rig.Town.Folk.ToArray().Where(f => !f.NightOwl), f => Assert.True(f.InForNight && !f.Visible));
        Assert.True(rig.Town.ButterfliesOut < 1f);
        rig.Run(5f);
        Assert.Equal(0f, rig.Town.ButterfliesOut);
    }

    [Fact]
    public void FarFromPetCityNothingRuns()
    {
        var rig = new Rig();
        rig.Player(new Vector3(0f, 0f, 0f)); // the forest's middle
        var before = rig.Town.Folk.ToArray().Select(f => f.Position).ToArray();
        rig.Run(20f);
        Assert.False(rig.Town.Active);
        Assert.Equal(0.0, rig.Town.Seconds);
        Assert.Equal(before, rig.Town.Folk.ToArray().Select(f => f.Position).ToArray());
        Assert.False(TownsfolkModule.Near(0f, 0f));
        Assert.True(TownsfolkModule.Near(0f, -140f));
        Assert.True(TownsfolkModule.Near(Plaza.X, Plaza.Z));
        // Before any PlayerState it is not active either.
        var fresh = new TownsfolkModule();
        fresh.Start(new FakeBus());
        fresh.Tick(new TickContext(0, Step));
        Assert.False(fresh.Active);
    }

    [Fact]
    public void PetsRoamChaseNapAndWalkWithTheirOwners()
    {
        var rig = new Rig();
        var pets = rig.Town.Pets.ToArray();
        Assert.Equal(TownsfolkModule.RoamingPetCount + TownsfolkModule.OwnedPetCount, pets.Length);
        var roaming = pets.Take(TownsfolkModule.RoamingPetCount).ToArray();
        Assert.Equal(10, roaming.Length);
        Assert.All(roaming, p => Assert.Equal(-1, p.Owner));
        foreach (CritterKind kind in Enum.GetValues<CritterKind>())
            Assert.True(roaming.Count(p => p.Kind == kind) >= 3, kind.ToString());
        Assert.All(pets.Skip(10), p => Assert.True(p.Owner >= 0 && p.Kind != CritterKind.Crow));
        Assert.True(pets.Select(p => p.Color).Distinct().Count() >= 5);

        var seen = new HashSet<CritterState>();
        float worstLeash = 0f;
        bool perchedHigh = false;
        rig.Run(400f, () =>
        {
            var all = rig.Town.Pets;
            var folk = rig.Town.Folk;
            for (int i = 0; i < all.Length; i++)
            {
                var p = all[i];
                seen.Add(p.State);
                if (!p.Visible)
                {
                    Assert.True(p.Owner >= 0 && !folk[p.Owner].Visible);
                    continue;
                }
                var at = Flat(p.Position);
                Assert.True(CityLife.InCity(at, 0f), $"pet {i} left Pet City at {at}");
                if (p.State == CritterState.Perch)
                    perchedHigh |= p.Position.Y > Ground.HeightAt(at.X, at.Y) + 10f;
                if (p.Owner >= 0 && folk[p.Owner].State == FolkState.Walking)
                    worstLeash = MathF.Max(worstLeash, Vector2.Distance(at, Flat(folk[p.Owner].Position)));
                if (p.State == CritterState.Chase)
                    Assert.Equal(CritterState.Flee, all[p.Other].State);
            }
        });
        foreach (var state in new[] { CritterState.Wander, CritterState.Sniff, CritterState.Nap, CritterState.Chase, CritterState.Flee,
                     CritterState.Follow, CritterState.Sit, CritterState.Hop, CritterState.Peck, CritterState.Fly, CritterState.Perch })
            Assert.Contains(state, seen);
        Assert.True(perchedHigh);
        Assert.True(worstLeash < 3f, $"a pet strayed {worstLeash} m from its walking owner");
    }

    [Fact]
    public void ButterfliesFlutterOverTheFlowersByDay()
    {
        var rig = new Rig();
        Assert.Equal(TownsfolkModule.ButterflyCount, rig.Town.Butterflies.Length);
        var moved = 0f;
        rig.Run(120f, () =>
        {
            foreach (var b in rig.Town.Butterflies)
            {
                moved += Vector3.Distance(b.Position, b.Previous);
                // Round its flower, or on the way from a nearby one.
                var flower = CityLife.Flowers[b.Flower];
                Assert.True(Vector2.Distance(Flat(b.Position), Flat(flower)) < TownsfolkModule.MaxFlowerHop + 3f);
                Assert.True(CityLife.Flowers.ToArray().Min(f => Vector2.Distance(Flat(b.Position), Flat(f))) < 8f);
                float above = b.Position.Y - Ground.HeightAt(b.Position.X, b.Position.Z);
                Assert.InRange(above, 0.2f, 4f);
            }
        });
        Assert.Equal(1f, rig.Town.ButterfliesOut);
        Assert.True(moved > 100f);
    }

    [Fact]
    public void TheSameRunEveryTime()
    {
        var a = new Rig();
        var b = new Rig();
        a.Run(60f);
        b.Run(60f);
        Assert.Equal(a.Town.Folk.ToArray().Select(f => f.Position), b.Town.Folk.ToArray().Select(f => f.Position));
        Assert.Equal(a.Town.Pets.ToArray().Select(f => f.Position), b.Town.Pets.ToArray().Select(f => f.Position));
    }
}
