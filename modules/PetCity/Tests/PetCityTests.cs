using System;
using System.Collections.Generic;
using Fungtopia.DayNight;
using Fungtopia.Map;
using Fungtopia.Shell;
using Xunit;

namespace Fungtopia.PetCity.Tests;

public class PetCityTests
{
    /// <summary>Fake of the shell's bus: delivers TimeOfDay to its subscribers and counts publishes.</summary>
    private sealed class FakeBus : IMessageBus
    {
        public readonly List<IMessageHandler<TimeOfDay>> TimeHandlers = new();
        public int Subscriptions;
        public int Published;

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct
        {
            Subscriptions++;
            if (handler is IMessageHandler<TimeOfDay> time && typeof(T) == typeof(TimeOfDay))
                TimeHandlers.Add(time);
        }

        public void Publish<T>(in T message) where T : struct
        {
            Published++;
            if (message is TimeOfDay time)
                foreach (var h in TimeHandlers)
                    h.Handle(time);
        }
    }

    // Layout

    [Fact]
    public void EveryPetCityBuildingIsDrawnExactlyOnce()
    {
        var all = Buildings.All;
        var drawn = new int[all.Length];
        foreach (var p in PetCityLayout.Pieces)
            drawn[p.BuildingIndex]++;
        int cityBuildings = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].Area == Area.PetCity)
            {
                cityBuildings++;
                Assert.True(drawn[i] == 1, $"Pet City building {i} ({all[i].Kind}) drawn {drawn[i]} times");
            }
            else
            {
                Assert.True(drawn[i] == 0, $"building {i} ({all[i].Kind}) is in {all[i].Area}, not Pet City, but drawn");
            }
        }
        Assert.True(cityBuildings >= 2);
        // And nothing else: Pet City draws no building of its own.
        Assert.Equal(cityBuildings, PetCityLayout.Pieces.Length);
    }

    [Fact]
    public void EachBuildingIsDrawnExactlyOnItsMapFootprint()
    {
        foreach (var p in PetCityLayout.Pieces)
        {
            var b = Buildings.All[p.BuildingIndex];
            Assert.Equal(b.Kind == BuildingKind.PetStore ? PieceKind.PetStore : PieceKind.CityBlock, p.Kind);
            Assert.Equal(b.X, p.Position.X);
            Assert.Equal(b.Z, p.Position.Z);
            Assert.Equal(b.Rotation, p.Yaw);
            Assert.Equal(b.Width, p.Size.X);
            Assert.Equal(b.Height, p.Size.Y);
            Assert.Equal(b.Depth, p.Size.Z);
            // Planted on its whole footprint: no part of its base floats.
            Assert.Equal(PetCityLayout.FootingOf(b), p.Footing);
            Assert.Equal(p.Footing.Base, p.Position.Y);
            AssertPlantedOnRect(b, p.Footing);
        }
    }

    [Fact]
    public void TheOnePetStoreIsTheMapsPetStore()
    {
        int stores = 0;
        foreach (var p in PetCityLayout.Pieces)
            if (p.Kind == PieceKind.PetStore)
            {
                stores++;
                var store = Buildings.First(BuildingKind.PetStore);
                Assert.Equal(store.X, p.Position.X);
                Assert.Equal(store.Z, p.Position.Z);
            }
        Assert.Equal(1, stores);
    }

    [Fact]
    public void CityBlocksHaveStoriesThatFillTheirHeightAndVaryInLook()
    {
        var styles = new HashSet<int>();
        foreach (var p in PetCityLayout.Pieces)
        {
            if (p.Kind != PieceKind.CityBlock)
            {
                Assert.Equal(0, p.Stories);
                continue;
            }
            Assert.InRange(p.Stories, PetCityLayout.MinStories, PetCityLayout.MaxStories);
            // The model is scaled to the building's height by at most a fifth.
            float scale = p.Size.Y / PetCityLayout.ModelHeight(p.Stories);
            Assert.InRange(scale, 0.8f, 1.2f);
            Assert.InRange(p.Style, 0, PetCityLayout.StyleCount - 1);
            styles.Add(p.Style);
        }
        Assert.Equal(PetCityLayout.StyleCount, styles.Count);
    }

    [Fact]
    public void StoriesForRoundsToTheNearestModel()
    {
        Assert.Equal(5, PetCityLayout.StoriesFor(PetCityLayout.ModelHeight(5)));
        Assert.Equal(5, PetCityLayout.StoriesFor(PetCityLayout.ModelHeight(5) + 1.4f));
        Assert.Equal(6, PetCityLayout.StoriesFor(PetCityLayout.ModelHeight(5) + 1.6f));
        Assert.Equal(PetCityLayout.MinStories, PetCityLayout.StoriesFor(1f));
        Assert.Equal(PetCityLayout.MaxStories, PetCityLayout.StoriesFor(500f));
    }

    [Fact]
    public void TheCounterIsAtLucysSpotInTheStoresOpenFront()
    {
        var counter = PetCityLayout.Counter;
        var spot = Places.LucySpot;
        var store = Buildings.First(BuildingKind.PetStore);
        // Inside the store's footprint, along its front edge, facing out like the spot.
        Assert.True(store.Contains(counter.Position.X, counter.Position.Z), "the counter is outside the pet store");
        Assert.False(store.Contains(counter.Position.X, counter.Position.Z, -PetCityLayout.CounterDepth), "the counter is not at the store's front");
        Assert.Equal(spot.Yaw, counter.Yaw);
        // Right behind Lucy's spot: its customer side is FrontGap from the spot, on the spot's line.
        var d = spot.Position - counter.Position;
        float along = d.X * spot.Facing.X + d.Z * spot.Facing.Z;
        float across = d.X * spot.Facing.Z - d.Z * spot.Facing.X;
        Assert.Equal(Places.FrontGap + PetCityLayout.CounterDepth * 0.5f, along, 3);
        Assert.Equal(0f, across, 3);
    }

    [Fact]
    public void TheDrawnPropsAreExactlyTheMapsPetCityProps()
    {
        // The Map's Pet City props, in order, and nothing else.
        var expected = new List<int>();
        var all = Props.All;
        for (int i = 0; i < all.Length; i++)
            if (all[i].Area == Area.PetCity)
                expected.Add(i);
        var drawn = PetCityLayout.CityProps;
        Assert.Equal(expected.Count, drawn.Length);
        for (int k = 0; k < drawn.Length; k++)
        {
            var d = drawn[k];
            Assert.Equal(expected[k], d.PropIndex);
            var p = all[d.PropIndex];
            Assert.Equal(p.Kind, d.Kind);
            Assert.Equal(p.X, d.Position.X);
            Assert.Equal(p.Z, d.Position.Z);
            Assert.Equal(p.Rotation, d.Yaw);
            Assert.Equal(p.Radius, d.Radius);
            Assert.Equal(p.Height, d.Height);
        }
    }

    [Fact]
    public void ThePlazaHasLampsBenchesPlantersAndTheStatue()
    {
        var count = new Dictionary<PropKind, int>();
        foreach (var p in PetCityLayout.CityProps)
            count[p.Kind] = count.GetValueOrDefault(p.Kind) + 1;
        Assert.True(count.GetValueOrDefault(PropKind.Lamp) >= 8, "too few lamps");
        Assert.True(count.GetValueOrDefault(PropKind.Bench) >= 4, "too few benches");
        Assert.True(count.GetValueOrDefault(PropKind.Planter) >= 2, "too few planters");
        Assert.Equal(1, count.GetValueOrDefault(PropKind.Statue));
    }

    [Fact]
    public void EveryPropHasAModelThatScalesToItsSize()
    {
        foreach (var p in PetCityLayout.CityProps)
        {
            var model = PetCityLayout.PropModel(p.Kind);
            Assert.True(model.HasValue, $"no model for a {p.Kind}");
            // Built near the Map's sizes, so scaling keeps its look.
            Assert.InRange(p.Radius / model!.Value.Radius, 0.5f, 2f);
            Assert.InRange(p.Height / model.Value.Height, 0.5f, 2f);
        }
        Assert.Null(PetCityLayout.PropModel(PropKind.MediumMushroom));
    }

    [Fact]
    public void EveryDrawnPropIsSolidAndStandsOnTheGround()
    {
        foreach (var p in PetCityLayout.CityProps)
        {
            Assert.Equal(Area.PetCity, Areas.At(p.Position.X, p.Position.Z));
            // Walkers cannot pass through anything drawn.
            Assert.True(Ground.Blocks(p.Position.X, p.Position.Z), $"prop {p.PropIndex} ({p.Kind}) is not an obstacle");
            Assert.Equal(PetCityLayout.FootingOf(Props.All[p.PropIndex]), p.Footing);
            Assert.Equal(p.Footing.Base, p.Position.Y);
            AssertPlantedOnRound(p.Position.X, p.Position.Z, p.Radius, p.Footing);
            // Drawn down to its base, its top (a lamp's cap) its full height above the high side.
            Assert.Equal(p.Footing.High + p.Height, p.Position.Y + PetCityLayout.DrawnHeight(p), 4);
        }
    }

    [Fact]
    public void OnlyBigRoundPropsGetARootFlareThatWidensIntoTheGround()
    {
        bool any = false;
        foreach (var p in PetCityLayout.CityProps)
        {
            float flare = PetCityLayout.FlareRadius(p);
            if (p.Kind is PropKind.Lamp or PropKind.Bench)
            {
                Assert.Equal(0f, flare);
                continue;
            }
            Assert.True(flare > p.Radius && flare < p.Radius * 1.5f, $"prop {p.PropIndex} ({p.Kind}) flare {flare}");
            any = true;
        }
        Assert.True(any);
        Assert.Equal(0f, Planting.FlareRadius(0.3f));
        Assert.True(Planting.FlareRadius(2f) > 2f);
        // A flare hides the base's edge without covering much of it.
        Assert.InRange(PetCityLayout.FlareAbove, 0.05f, 0.2f);
        Assert.InRange(PetCityLayout.PlinthAbove, 0.02f, 0.2f);
    }

    // Planting

    /// <summary>
    /// Sample positions all over the walkable map, a footprint of <paramref name="radius"/> on it: the
    /// forest's hills, the rough Deep Forest (roots, lumps, dips), the towns and roads.
    /// </summary>
    private static IEnumerable<(float X, float Z)> SamplePositions(float radius)
    {
        int n = 0;
        for (float x = -279f; x <= 90f; x += 3.7f)
        for (float z = -289f; z <= 289f; z += 4.3f)
            if (Areas.Walkable(x, z, radius))
            {
                n++;
                yield return (x, z);
            }
        Assert.True(n > 1000, $"only {n} sample positions");
    }

    private static void AssertPlantedOnRound(float x, float z, float radius, Footing f)
    {
        float low = Ground.HeightAt(x, z), high = low;
        void Check(float px, float pz)
        {
            float g = Ground.HeightAt(px, pz);
            Assert.True(f.Base <= g, $"base {f.Base} floats above the ground {g} at ({px}, {pz})");
            low = MathF.Min(low, g);
            high = MathF.Max(high, g);
        }
        Check(x, z);
        // The rim at 8 and many more points, and rings inside it every quarter meter.
        int rings = Math.Max(2, (int)MathF.Ceiling(radius * 4f));
        for (int ring = 1; ring <= rings; ring++)
        for (int k = 0; k < 64; k++)
        {
            float a = (k + 0.5f * (ring % 2)) * MathF.Tau / 64f, r = radius * ring / rings;
            Check(x + MathF.Sin(a) * r, z + MathF.Cos(a) * r);
        }
        AssertSunkJustBelow(f, low, high);
    }

    private static void AssertPlantedOnRect(in Building b, Footing f)
    {
        float low = float.MaxValue, high = float.MinValue;
        for (int i = 0; i <= 12; i++)
        for (int j = 0; j <= 12; j++)
        {
            var p = b.ToWorld((i / 12f - 0.5f) * b.Width, (j / 12f - 0.5f) * b.Depth);
            float g = Ground.HeightAt(p.X, p.Y);
            // Between the grid points the ground may bulge a little lower; the sink covers it.
            bool sampled = i % 2 == 0 && j % 2 == 0;
            Assert.True(f.Base <= g + (sampled ? 0f : 0.05f), $"building base {f.Base} floats above the ground {g} at ({p.X}, {p.Y})");
            if (sampled)
            {
                low = MathF.Min(low, g);
                high = MathF.Max(high, g);
            }
        }
        AssertSunkJustBelow(f, low, high);
    }

    /// <summary><paramref name="low"/>, <paramref name="high"/>: the lowest and highest ground the test found under the footprint.</summary>
    private static void AssertSunkJustBelow(Footing f, float low, float high)
    {
        // Into the ground by a small sink under the lowest point, never deep.
        Assert.InRange(f.Low - f.Base, Planting.Sink - 1e-4f, Planting.Sink + 1e-4f);
        Assert.InRange(Planting.Sink, 0.1f, 0.2f);
        Assert.InRange(low - f.Base, 0f, 0.3f);
        // Its high side is the highest ground under it (to the samples' precision).
        Assert.True(f.High >= f.Low);
        Assert.True(MathF.Abs(f.High - high) < 0.05f, $"high side {f.High}, the ground's highest {high}");
        // Extended down by the drop, so the height above the high side stays the same.
        Assert.Equal(f.High - f.Base, f.Extension, 4);
    }

    [Theory]
    [InlineData(0.3f)]
    [InlineData(0.6f)]
    [InlineData(1.1f)]
    [InlineData(2.5f)]
    [InlineData(3.5f)]
    public void ARoundBaseTouchesTheGroundEverywhereUnderItAndSinksOnlyALittle(float radius)
    {
        float maxDrop = 0f;
        foreach (var (x, z) in SamplePositions(radius))
        {
            var f = Planting.Round(x, z, radius);
            AssertPlantedOnRound(x, z, radius, f);
            maxDrop = MathF.Max(maxDrop, f.High - f.Low);
        }
        // The samples include sloped ground, where a base at the center height would float.
        Assert.True(maxDrop > MathF.Min(0.15f, 0.2f * radius), $"no sloped sample (largest drop {maxDrop})");
    }

    [Fact]
    public void ABuildingBaseTouchesTheGroundUnderItsWholeFootprint()
    {
        foreach (var (x, z) in SamplePositions(8f))
        {
            var b = new Building(BuildingKind.CityBlock, Area.PetCity, x, z, x * 0.13f, 12f, 9f, 20f);
            AssertPlantedOnRect(b, Planting.Rect(b));
        }
    }

    [Fact]
    public void OnFlatGroundTheBaseIsJustTheSinkBelowIt()
    {
        var home = Areas.Home.Center;
        var f = Planting.Round(home.X, home.Y, 1f);
        Assert.Equal(Areas.HomeFloorY - Planting.Sink, f.Base, 4);
        Assert.Equal(Planting.Sink, f.Extension, 4);
    }

    [Fact]
    public void EverythingDrawnIsAMapBuildingOrAMapPropOfPetCity()
    {
        // Pet City places nothing standing of its own: every piece is a Pet City building of the
        // Map and every prop a Pet City prop of the Map, so walkers collide with all of it.
        foreach (var p in PetCityLayout.Pieces)
        {
            Assert.InRange(p.BuildingIndex, 0, Buildings.All.Length - 1);
            Assert.Equal(Area.PetCity, Buildings.All[p.BuildingIndex].Area);
        }
        foreach (var p in PetCityLayout.CityProps)
        {
            Assert.InRange(p.PropIndex, 0, Props.All.Length - 1);
            Assert.Equal(Area.PetCity, Props.All[p.PropIndex].Area);
        }
        Assert.Equal(new[] { PieceKind.PetStore, PieceKind.CityBlock }, Enum.GetValues<PieceKind>());
    }

    [Fact]
    public void TheLayoutIsTheSameEveryTime()
    {
        var a = PetCityLayout.Pieces.ToArray();
        var b = PetCityLayout.Pieces.ToArray();
        Assert.Equal(a, b);
        Assert.Equal(PetCityLayout.CityProps.ToArray(), PetCityLayout.CityProps.ToArray());
    }

    // Module

    [Fact]
    public void StartsSubscribedToTheClockAndTicksWithoutPublishing()
    {
        var bus = new FakeBus();
        var module = new PetCityModule();
        Assert.False(module.Started);
        module.Start(bus);
        Assert.True(module.Started);
        Assert.Single(bus.TimeHandlers);
        for (int t = 0; t < 600; t++)
            module.Tick(new TickContext(t, 1.0 / 60));
        Assert.Equal(0, bus.Published);
    }

    [Fact]
    public void TickBeforeStartThrows()
    {
        var module = new PetCityModule();
        Assert.Throws<InvalidOperationException>(() => module.Tick(new TickContext(0, 1.0 / 60)));
    }

    [Fact]
    public void StartWithoutABusThrows()
    {
        Assert.Throws<ArgumentNullException>(() => new PetCityModule().Start(null!));
    }

    [Fact]
    public void WindowsAreLitAtNightOnly()
    {
        var bus = new FakeBus();
        var module = new PetCityModule();
        module.Start(bus);
        Assert.False(module.WindowsLit);
        bus.Publish(new TimeOfDay(1, 21f, TimeOfDay.IsNightAt(21f)));
        Assert.True(module.IsNight);
        Assert.True(module.WindowsLit);
        bus.Publish(new TimeOfDay(2, 7f, TimeOfDay.IsNightAt(7f)));
        Assert.False(module.IsNight);
        Assert.False(module.WindowsLit);
        bus.Publish(new TimeOfDay(2, 3f, true));
        Assert.True(module.WindowsLit);
    }

    [Fact]
    public void HandlingTheClockDoesNotAllocate()
    {
        var module = new PetCityModule();
        module.Start(new FakeBus());
        var night = new TimeOfDay(1, 22f, true);
        var day = new TimeOfDay(2, 12f, false);
        for (int t = 0; t < 300; t++)
        {
            module.Handle(t % 2 == 0 ? night : day); // warm up
            module.Tick(new TickContext(t, 1.0 / 60));
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int t = 300; t < 1300; t++)
        {
            module.Handle(t % 2 == 0 ? night : day);
            module.Tick(new TickContext(t, 1.0 / 60));
        }
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }
}
