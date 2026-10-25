using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
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

    // Mushrooms (the game's "vivid" rule)

    /// <summary>The colors the mushroom caps had before they were made vivid (glb Cap, StoreRoof; the view's style roofs).</summary>
    private static readonly Swatch[] DullCaps =
    {
        new(0.9f, 0.3f, 0.26f), new(0.98f, 0.56f, 0.7f),
        new(0.94f, 0.38f, 0.44f), new(0.98f, 0.62f, 0.36f), new(0.5f, 0.6f, 0.96f), new(0.96f, 0.5f, 0.72f),
    };

    private static Swatch[] Caps() => new[]
    {
        PetCityLook.Cap, PetCityLook.StoreCap,
        PetCityLook.StyleCap(0), PetCityLook.StyleCap(1), PetCityLook.StyleCap(2), PetCityLook.StyleCap(3),
    };

    [Fact]
    public void MushroomCapsAreSaturatedAndBright()
    {
        var caps = Caps();
        for (int i = 0; i < caps.Length; i++)
        {
            Assert.True(caps[i].Value >= 0.98f, $"cap {i} {caps[i]} is not bright");
            Assert.True(caps[i].Saturation >= 0.7f, $"cap {i} {caps[i]} is not saturated");
            Assert.True(caps[i].Saturation >= DullCaps[i].Saturation + 0.1f, $"cap {i} {caps[i]} is not richer than {DullCaps[i]}");
            Assert.True(caps[i].Value >= DullCaps[i].Value, $"cap {i} {caps[i]} is darker than {DullCaps[i]}");
        }
        // The red caps are a pure cherry/tomato red: full red, little green or blue.
        foreach (var red in new[] { PetCityLook.Cap, PetCityLook.StyleCap(0) })
        {
            Assert.Equal(1f, red.R);
            Assert.True(red.G <= 0.2f && red.B <= 0.2f, $"{red} is not a pure red");
        }
        Assert.Equal(PetCityLayout.StyleCount, PetCityLook.StyleCaps.Length);
    }

    [Fact]
    public void SpotsArePureWhiteAndStemsALightCream()
    {
        Assert.Equal(new Swatch(1f, 1f, 1f), PetCityLook.Spots);
        var stem = PetCityLook.Stem;
        Assert.True(stem.Value >= 0.98f && stem.Min >= 0.8f, $"{stem} is not light");
        Assert.True(stem.Saturation is > 0.05f and < 0.2f, $"{stem} is not a cream");
        Assert.True(stem.R >= stem.G && stem.G >= stem.B, $"{stem} is not warm");
    }

    [Fact]
    public void CapsAreGlossyWithARimAndLampBulbsGlowTwiceAsBrightAtNight()
    {
        Assert.InRange(PetCityLook.CapGloss, 0.2f, 0.6f);
        Assert.InRange(PetCityLook.CapRim, 0.2f, 0.6f);
        foreach (var cap in new[] { "Cap", "CapRoof", "StoreCap" })
            Assert.True(PetCityLook.IsCap(cap) && PetCityLook.IsMushroom(cap), cap);
        foreach (var other in new[] { "Spots", "Stem", "LampGlow", "Roof", "StoreRoof", "Glow", "Bronze" })
            Assert.False(PetCityLook.IsCap(other), other);
        foreach (var other in new[] { "Roof", "StoreRoof", "Glow", "Cream", "Wall", "Bronze" })
            Assert.False(PetCityLook.IsMushroom(other), other);
        // The bulbs glowed 1.6 at night before, like the store's lamps still do.
        Assert.Equal(1.6f, PetCityLook.GlowNight);
        Assert.Equal(2f * 1.6f, PetCityLook.LampGlowNight);
    }

    /// <summary>The JSON chunk of Models/petcity.glb.</summary>
    private static JsonDocument ModelJson()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "modules", "PetCity", "Models", "petcity.glb")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        byte[] glb = File.ReadAllBytes(Path.Combine(dir!.FullName, "modules", "PetCity", "Models", "petcity.glb"));
        Assert.Equal("glTF", Encoding.ASCII.GetString(glb, 0, 4));
        int length = BitConverter.ToInt32(glb, 12);
        return JsonDocument.Parse(Encoding.UTF8.GetString(glb, 20, length));
    }

    /// <summary>Every material of Models/petcity.glb by name, with its base color (glTF's default white when it has none).</summary>
    private static Dictionary<string, Swatch> ModelColors()
    {
        using var json = ModelJson();
        var colors = new Dictionary<string, Swatch>();
        foreach (var m in json.RootElement.GetProperty("materials").EnumerateArray())
        {
            var color = new Swatch(1f, 1f, 1f);
            if (m.TryGetProperty("pbrMetallicRoughness", out var pbr) && pbr.TryGetProperty("baseColorFactor", out var f))
                color = new Swatch(f[0].GetSingle(), f[1].GetSingle(), f[2].GetSingle());
            colors[m.GetProperty("name").GetString()!] = color;
        }
        return colors;
    }

    /// <summary>The materials object <paramref name="name"/> of Models/petcity.glb is drawn with.</summary>
    private static HashSet<string> ModelMaterials(string name)
    {
        using var json = ModelJson();
        var root = json.RootElement;
        var materials = root.GetProperty("materials");
        var used = new HashSet<string>();
        foreach (var node in root.GetProperty("nodes").EnumerateArray())
        {
            if (!node.TryGetProperty("name", out var n) || n.GetString() != name)
                continue;
            var mesh = root.GetProperty("meshes")[node.GetProperty("mesh").GetInt32()];
            foreach (var primitive in mesh.GetProperty("primitives").EnumerateArray())
                used.Add(materials[primitive.GetProperty("material").GetInt32()].GetProperty("name").GetString()!);
        }
        Assert.NotEmpty(used);
        return used;
    }

    private static void AssertColor(Swatch expected, Swatch actual, string name)
    {
        Assert.True(MathF.Abs(expected.R - actual.R) < 1e-3f && MathF.Abs(expected.G - actual.G) < 1e-3f
            && MathF.Abs(expected.B - actual.B) < 1e-3f, $"{name}: expected {expected}, the model has {actual}");
    }

    [Fact]
    public void TheModelsMushroomsHaveTheVividColors()
    {
        var colors = ModelColors();
        AssertColor(PetCityLook.Cap, colors["Cap"], "Cap");
        AssertColor(PetCityLook.StoreCap, colors["StoreCap"], "StoreCap");
        AssertColor(PetCityLook.StyleCap(0), colors["CapRoof"], "CapRoof");
        AssertColor(PetCityLook.Spots, colors["Spots"], "Spots");
        AssertColor(PetCityLook.Stem, colors["Stem"], "Stem");
        AssertColor(PetCityLook.LampGlow, colors["LampGlow"], "LampGlow");
    }

    [Fact]
    public void OnlyMushroomsChangedColor()
    {
        // The materials the mushrooms shared before keep their colors on what is not a mushroom:
        // the terraces' parapets, the store's floor tiles, its ceiling lamps, the pets' muzzles.
        var colors = ModelColors();
        AssertColor(new Swatch(0.92f, 0.36f, 0.42f), colors["Roof"], "Roof");
        AssertColor(new Swatch(0.98f, 0.56f, 0.7f), colors["StoreRoof"], "StoreRoof");
        AssertColor(new Swatch(1f, 0.9f, 0.58f), colors["Glow"], "Glow");
        AssertColor(new Swatch(1f, 0.96f, 0.88f), colors["Cream"], "Cream");
        AssertColor(new Swatch(0.5f, 0.78f, 0.7f), colors["Bronze"], "Bronze");

        Assert.Contains("CapRoof", ModelMaterials("RoofCap"));
        Assert.DoesNotContain("Roof", ModelMaterials("RoofCap"));
        Assert.Contains("Roof", ModelMaterials("RoofTerrace"));
        var lamp = ModelMaterials("CityLamp");
        Assert.Contains("LampGlow", lamp);
        Assert.Contains("StoreCap", lamp);
        Assert.Contains("Spots", lamp);
        Assert.DoesNotContain("Glow", lamp);
        Assert.DoesNotContain("StoreRoof", lamp);
        var store = ModelMaterials("PetStore");
        Assert.Contains("StoreCap", store);
        Assert.Contains("StoreRoof", store);
        Assert.Contains("Glow", store);
        foreach (var pet in new[] { "Kitten", "Puppy" })
        {
            Assert.Contains("Cream", ModelMaterials(pet));
            Assert.Contains("Cap", ModelMaterials(pet));
        }
    }

    [Fact]
    public void MushroomsHaveNoEyes()
    {
        foreach (var name in new[] { "RoofCap", "RoofTerrace", "PetStore", "CityLamp", "Planter", "Statue" })
        {
            var used = ModelMaterials(name);
            Assert.DoesNotContain("Eye", used);
            Assert.DoesNotContain("EyeShine", used);
        }
    }
}
