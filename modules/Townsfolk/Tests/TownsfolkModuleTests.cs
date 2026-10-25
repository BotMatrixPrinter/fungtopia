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
        Assert.True(looks.Select(l => l.Hair).Distinct().Count() >= 4);
        Assert.True(looks.Select(l => l.HairColor).Distinct().Count() >= 5);
        Assert.True(looks.Select(l => l.Outfit).Distinct().Count() >= 6);
        Assert.True(looks.Select(l => l.Hat).Where(h => h != HatStyle.None).Distinct().Count() >= 3);
        Assert.InRange(looks.Count(l => l.Hat != HatStyle.None), 10, 24);
        Assert.Contains(looks, l => l.Trousers);
        Assert.Contains(looks, l => !l.Trousers);
        Assert.Equal(25, looks.Select(l => (l.Skin, l.Hair, l.HairColor, l.Outfit, l.Hat, l.HatColor)).Distinct().Count());
    }

    [Fact]
    public void FolkAreSeededHeightsBetween1_65And1_95AboutAnAverage1_80()
    {
        var a = new TownsfolkModule().Folk.ToArray();
        var heights = a.Select(f => f.Look.Height).ToList();
        Assert.All(heights, h => Assert.InRange(h, Looks.MinHeight, Looks.MaxHeight));
        Assert.Equal(1.65f, Looks.MinHeight);
        Assert.Equal(1.95f, Looks.MaxHeight);
        Assert.InRange(heights.Average(), 1.76f, 1.84f);
        // Slightly varied: short and tall ones among them, hardly two the same.
        Assert.True(heights.Max() - heights.Min() > 0.2f);
        Assert.True(heights.Count(h => h < 1.75f) >= 3 && heights.Count(h => h > 1.85f) >= 3);
        Assert.True(heights.Distinct().Count() >= 20);
        // The same rig, scaled uniformly by height / 1.80.
        Assert.All(a, f => Assert.Equal(f.Look.Height / 1.8f, f.Look.Scale, 5));
        var mixed = Looks.Mix(new Random(7));
        Assert.Equal(mixed, Looks.Mix(new Random(7)));
    }

    [Fact]
    public void WalkingTakesRealStridesWithThePlantedFootStill()
    {
        foreach (float height in new[] { 1.65f, 1.8f, 1.95f })
        {
            float scale = height / Body.Height, speed = 1.2f, dt = 1f / 60f;
            float cycle = 0f, x = 0f;
            float maxHip = 0f, maxKnee = 0f, maxSwingClear = 0f;
            float plantedFrom = float.NaN, worstSlide = 0f;
            for (int k = 0; k < 600; k++)
            {
                cycle += Body.CycleRate(speed, scale) * dt;
                x += speed * dt;
                var g = Body.Legs(cycle, speed);
                var l = Body.Foot(g.HipL, g.KneeL, g.Drop);
                var r = Body.Foot(g.HipR, g.KneeR, g.Drop);
                // The lower foot is always on the ground, neither in it nor over it.
                Assert.InRange(MathF.Min(l.Height, r.Height), -0.001f, 0.001f);
                Assert.True(l.Height > -0.001f && r.Height > -0.001f);
                maxHip = MathF.Max(maxHip, MathF.Abs(g.HipL));
                maxKnee = MathF.Max(maxKnee, -g.KneeL);
                maxSwingClear = MathF.Max(maxSwingClear, l.Height * scale);
                // Planted (the first half of its cycle), the left foot stays where it was put down.
                float u = cycle - MathF.Floor(cycle);
                float footX = x + l.Forward * scale;
                if (u > 0.04f && u < 0.46f)
                {
                    if (float.IsNaN(plantedFrom))
                        plantedFrom = footX;
                    worstSlide = MathF.Max(worstSlide, MathF.Abs(footX - plantedFrom));
                    Assert.Equal(0f, g.KneeL);
                }
                else
                    plantedFrom = float.NaN;
            }
            Assert.True(maxHip > 0.25f && maxHip < 0.5f, $"hip swing {maxHip}");
            Assert.True(maxKnee > 0.6f, $"knee bend {maxKnee}");
            Assert.True(maxSwingClear > 0.04f, $"swinging foot lifted {maxSwingClear}");
            Assert.True(worstSlide < 0.03f, $"{height} m: planted foot slid {worstSlide}");
            // A grown-up's step: about 0.6 m at 1.2 m/s, longer for the taller.
            float step = speed / (2f * Body.CycleRate(speed, scale));
            Assert.InRange(step, 0.55f * scale, 0.65f * scale);
        }
        // Standing: straight legs together, the body not lowered.
        var still = Body.Legs(0.3f, 0f);
        Assert.Equal(0f, still.HipL, 4);
        Assert.Equal(0f, still.HipR, 4);
        Assert.Equal(0f, still.KneeL, 4);
        Assert.Equal(0f, still.Drop, 4);
    }

    [Fact]
    public void SittingBendsTheHipsAndKneesToFitTheBenches()
    {
        foreach (float height in new[] { 1.65f, 1.72f, 1.8f, 1.88f, 1.95f })
        {
            float scale = height / Body.Height;
            var sit = Body.Sit(scale);
            // The hips come down onto the seat: the thighs' tubes rest on its top.
            float hipHeight = sit.Lift + Body.HipY * scale;
            Assert.Equal(Body.SeatHeight + Body.LegRadius * scale, hipHeight, 3);
            // The feet stand on the ground.
            var foot = Body.Foot(sit.Hip, sit.Knee, 0f);
            Assert.Equal(0f, sit.Lift + foot.Height * scale, 2);
            // Thighs forward (about level) and the shins about upright: bent about square at hips and knees.
            Assert.InRange(sit.Hip, 1.1f, 1.6f);
            Assert.InRange(-sit.Knee, 1.1f, 1.7f);
            Assert.InRange(sit.Hip + sit.Knee, -0.1f, 0.6f);
            // The knees over the seat's front (0.13 m in front of the seat spot), the back short of the
            // backrest (0.33 m behind it), the shins clear of the seat's front edge.
            float knee = Body.Thigh * MathF.Sin(sit.Hip) * scale - sit.Back;
            Assert.True(knee - Body.LegRadius * scale > 0.13f, $"{height} m: knees at {knee}");
            Assert.True(sit.Back + 0.11f * scale < 0.31f, $"{height} m: back at {sit.Back}");
        }
    }

    [Fact]
    public void ChattersLookEachOtherInTheEyesAtTheirOwnHeights()
    {
        float shortOne = 1.65f / Body.Height, tallOne = 1.95f / Body.Height;
        Assert.Equal(0f, Body.LookPitch(1f, 1f, 1.2f), 5);
        float up = Body.LookPitch(shortOne, tallOne, 1.2f), down = Body.LookPitch(tallOne, shortOne, 1.2f);
        Assert.True(up > 0.15f && up < 0.3f, $"looks up {up}");
        Assert.Equal(-up, down, 5);
        // The head's tip is the angle to the other's eyes.
        Assert.Equal(MathF.Atan2(Body.HeadY * (tallOne - shortOne), 1.2f), up, 5);
        // Pairs who meet stand at a talking distance, each at their own height: the chats really pair short and tall.
        var rig = new Rig();
        float biggest = 0f;
        rig.Run(300f, () =>
        {
            var folk = rig.Town.Folk;
            for (int i = 0; i < folk.Length; i++)
                if (folk[i].State == FolkState.Chatting && folk[i].Partner >= 0)
                {
                    var other = folk[folk[i].Partner];
                    float apart = Vector2.Distance(Flat(folk[i].Position), Flat(other.Position));
                    Assert.InRange(apart, 0.8f, 1.8f);
                    biggest = MathF.Max(biggest, MathF.Abs(other.Look.Height - folk[i].Look.Height));
                }
        });
        Assert.True(biggest > 0.05f, $"height gap {biggest}");
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
