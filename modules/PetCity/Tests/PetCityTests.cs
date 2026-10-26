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

    [Fact]
    public void OnlyTheFungiShaderReadsPerInstanceData()
    {
        // Real GPUs give ordinary meshes undefined INSTANCE_CUSTOM (Pet City drew black on Windows),
        // so only fungus.gdshader, on the fungi's MultiMeshes (custom data on), may read it.
        string shaders = ModuleFile("Godot", "Shaders");
        var files = Directory.GetFiles(shaders);
        Assert.Contains(Path.Combine(shaders, "toon.gdshader"), files);
        Assert.Contains(Path.Combine(shaders, "fungus.gdshader"), files);
        foreach (var file in files)
        {
            string code = ShaderCode(file);
            if (Path.GetFileName(file) == "fungus.gdshader")
                continue;
            Assert.DoesNotContain("INSTANCE_CUSTOM", code);
            Assert.DoesNotContain("instance_tint", code);
            Assert.DoesNotContain("INSTANCE_ID", code);
            Assert.DoesNotContain("varying", code);
        }
        // The fungi look like toon.gdshader (the same uniforms and light), their color times the
        // instance's tint, the varying set on every path.
        string toon = ShaderCode(Path.Combine(shaders, "toon.gdshader"));
        string fungus = ShaderCode(Path.Combine(shaders, "fungus.gdshader"));
        Assert.Contains("#include \"toon.gdshaderinc\"", toon);
        Assert.Contains("#include \"toon.gdshaderinc\"", fungus);
        Assert.Contains("ALBEDO = albedo;", toon);
        Assert.Contains("void vertex() {\n\ttint = INSTANCE_CUSTOM.rgb;\n}", fungus);
        Assert.Contains("ALBEDO = albedo * tint;", fungus);

        string models = File.ReadAllText(ModuleFile("Godot", "PetCityModels.cs"));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(models, "Shaders \\+ \"fungus\\.gdshader\""));
        Assert.DoesNotContain("instance_tint", models);
        Assert.Contains("UseCustomData = true", models);
    }

    /// <summary>A path under modules/PetCity/ of the repository.</summary>
    private static string ModuleFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "modules", "PetCity", "Godot")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var path = new List<string> { dir!.FullName, "modules", "PetCity" };
        path.AddRange(parts);
        return Path.Combine(path.ToArray());
    }

    /// <summary>A shader's code without its // comments, with \n line ends.</summary>
    private static string ShaderCode(string file)
    {
        var lines = File.ReadAllText(file).Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            int comment = lines[i].IndexOf("//", StringComparison.Ordinal);
            if (comment >= 0)
                lines[i] = lines[i][..comment].TrimEnd();
        }
        return string.Join("\n", lines);
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
    public void TheModelsMaterialsAreTheirMushroomsAndPaints()
    {
        var colors = ModelColors();
        AssertColor(PetCityLook.LampCaps[0], colors["LampCap"], "LampCap");
        // The little fungi's caps are white in the model: the view tints each one.
        foreach (var tinted in PetCityLook.TintedMaterials)
            AssertColor(new Swatch(1f, 1f, 1f), colors[tinted], tinted);
        foreach (var (name, paint) in PetCityLook.Paints)
            AssertColor(paint, colors[name], name);
        // Every material is a mushroom's, a paint, or one of the pets' own.
        var pets = new HashSet<string> { "Cream", "Eye", "EyeShine", "Blush", "Nose", "CatFur", "PupFur", "PupEar", "BunnyFur",
            "BunnyEar", "HamFur", "HamLight", "CrowBody", "CrowLight", "Beak", "Bone", "CushionIn" };
        foreach (var name in colors.Keys)
            Assert.True(PetCityLook.IsMushroom(name) || PetCityLook.Paints.ContainsKey(name) || pets.Contains(name), name);

        Assert.Contains("CapRoof", ModelMaterials("RoofCap"));
        Assert.DoesNotContain("Roof", ModelMaterials("RoofCap"));
        Assert.Contains("Roof", ModelMaterials("RoofTerrace"));
        var lamp = ModelMaterials("CityLamp");
        Assert.Contains("LampGlow", lamp);
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

    // The vibrant world: candy-bright paints, lush greens, flowers in many hues

    /// <summary>The city blocks' walls and roofs per style before (the view's).</summary>
    private static readonly Swatch[] OldWalls =
        { new(0.62f, 0.86f, 0.74f), new(0.78f, 0.7f, 0.96f), new(1f, 0.84f, 0.48f), new(0.62f, 0.8f, 0.98f) };
    private static readonly Swatch[] OldRoofs =
        { new(0.94f, 0.38f, 0.44f), new(0.98f, 0.62f, 0.36f), new(0.5f, 0.6f, 0.96f), new(0.96f, 0.5f, 0.72f) };

    [Fact]
    public void PaintsAreCandyBrightNeverWashedOutOrNeon()
    {
        foreach (var (name, c) in PetCityLook.Paints)
        {
            if (Array.IndexOf(PetCityLook.Neutrals, name) >= 0)
            {
                // Clean and bright, lightly tinted.
                Assert.True(c.Value >= 0.9f && c.Min >= 0.75f && c.Saturation <= 0.25f, $"{name} {c} is not a clean light neutral");
                continue;
            }
            Assert.True(c.Value >= 0.66f, $"{name} {c} is dull");
            Assert.True(c.Saturation is >= 0.3f and <= 0.92f, $"{name} {c} is washed out or neon");
            Assert.True(c.Min <= 0.75f, $"{name} {c} is washed out toward white");
        }
        // The buildings' paints: richer and at least as bright as before, still not neon.
        for (int i = 0; i < PetCityLayout.StyleCount; i++)
        {
            foreach (var (now, old, what) in new[] { (PetCityLook.StyleWalls[i], OldWalls[i], "wall"), (PetCityLook.StyleRoofs[i], OldRoofs[i], "roof") })
            {
                Assert.True(now.Saturation >= old.Saturation + 0.1f, $"style {i} {what} {now} is not richer than {old}");
                Assert.True(now.Value >= old.Value - 0.06f && now.Value >= 0.9f, $"style {i} {what} {now} is darker than {old}");
                Assert.True(now.Saturation <= 0.9f, $"style {i} {what} {now} is neon");
            }
        }
        // Clean bright stone under the buildings, and on the statue's pedestal.
        foreach (var stone in new[] { PetCityLook.PlinthStone, PetCityLook.StatueFlare, PetCityLook.Paints["Stone"] })
            Assert.True(stone.Value >= 0.85f && stone.Saturation <= 0.15f, $"{stone} is not clean bright stone");
    }

    [Fact]
    public void GreensAreLushWithYellowGreenHighlightsAndFlowersComeInManyHues()
    {
        var old = new Swatch(0.46f, 0.76f, 0.38f);
        foreach (var name in PetCityLook.Greens)
        {
            var g = PetCityLook.Paints[name];
            Assert.True(g.G == g.Max && g.Hue is > 80f and < 160f, $"{name} {g} is not green");
            Assert.True(g.Saturation >= 0.65f && g.Value >= 0.68f, $"{name} {g} is grey-olive, not lush");
        }
        Assert.True(PetCityLook.Paints["Leaf"].Saturation > old.Saturation + 0.15f && PetCityLook.Paints["Leaf"].Value > old.Value);
        // A yellow-green highlight and a deeper, bluer green for variation.
        var light = PetCityLook.Paints["LeafLight"];
        Assert.True(light.Hue < 95f && light.Value >= 0.9f, $"{light} is not a yellow-green highlight");
        Assert.True(PetCityLook.Paints["LeafDeep"].Hue > 130f);

        var hues = new List<float>();
        foreach (var name in PetCityLook.Flowers)
        {
            var f = PetCityLook.Paints[name];
            Assert.True(f.Saturation >= 0.6f && f.Value >= 0.95f, $"{name} {f} is not a bright flower");
            foreach (var h in hues)
            {
                float d = MathF.Abs(h - f.Hue);
                Assert.True(MathF.Min(d, 360f - d) >= 20f, $"{name} {f} is the same hue as another flower");
            }
            hues.Add(f.Hue);
        }
        Assert.True(hues.Count >= 5);
        // The windows' flower boxes and the planters carry every green and every flower.
        foreach (var model in new[] { "Tower3", "Planter" })
        {
            var used = ModelMaterials(model);
            foreach (var name in PetCityLook.Greens)
                Assert.Contains(name, used);
            foreach (var name in PetCityLook.Flowers)
                Assert.Contains(name, used);
        }
    }

    // The little fungi (variety)

    [Fact]
    public void ManyKindsOfFungiInManySizesProportionsAndTilts()
    {
        var all = PetCityFungi.All;
        Assert.Equal(Enum.GetValues<FungusKind>().Length, PetCityFungi.Looks.Length);
        Assert.True(PetCityFungi.Looks.Length >= 14);
        foreach (var kind in Enum.GetValues<FungusKind>())
        {
            var heights = new List<float>();
            var widths = new List<float>();
            var tilts = new List<float>();
            foreach (var f in all)
                if (f.Kind == kind)
                {
                    heights.Add(f.Height);
                    widths.Add(f.Width);
                    tilts.Add(f.Tilt);
                }
            Assert.True(heights.Count >= 3, $"{kind}: only {heights.Count}");
            heights.Sort();
            widths.Sort();
            tilts.Sort();
            Assert.True(heights[^1] >= heights[0] * 1.4f, $"{kind} comes in one size");
            Assert.True(widths[^1] >= widths[0] * 1.15f, $"{kind} comes in one proportion");
            Assert.True(tilts[^1] >= tilts[0] + 0.03f, $"{kind} comes in one tilt");
        }
        // Single mushrooms, pairs and clusters, and groups mixing kinds.
        int singles = 0, pairs = 0, clusters = 0, mixed = 0;
        foreach (var g in PetCityFungi.Groups)
        {
            if (g.Count == 1) singles++;
            else if (g.Count == 2) pairs++;
            else clusters++;
            for (int i = g.First + 1; i < g.First + g.Count; i++)
                if (all[i].Kind != all[g.First].Kind)
                {
                    mixed++;
                    break;
                }
        }
        Assert.True(singles >= 10 && pairs >= 10 && clusters >= 10 && mixed >= 10, $"{singles} singles, {pairs} pairs, {clusters} clusters, {mixed} mixed");
        // Caps of different shapes.
        var shapes = new HashSet<string>();
        foreach (var look in PetCityFungi.Looks)
            foreach (var word in look.Shape.Split(' '))
                shapes.Add(word);
        foreach (var shape in new[] { "dome", "flat", "funnel", "bell", "wavy", "cone", "cup", "ball", "shelf", "branching" })
            Assert.Contains(shape, shapes);
        // Planted on every kind of spot.
        foreach (var host in Enum.GetValues<FungusHost>())
            Assert.Contains(PetCityFungi.Groups.ToArray(), g => g.Host == host);
    }

    [Fact]
    public void NoTwoNeighborsAreIdentical()
    {
        var groups = PetCityFungi.Groups;
        var all = PetCityFungi.All;
        for (int i = 0; i < groups.Length; i++)
        {
            for (int j = i + 1; j < groups.Length; j++)
                if (System.Numerics.Vector3.Distance(groups[i].Center, groups[j].Center) < PetCityFungi.NeighborDistance)
                    Assert.True(groups[i].Main != groups[j].Main, $"neighboring groups {i} and {j} are both {groups[i].Main}");
            var g = groups[i];
            Assert.Equal(g.Main, all[g.First].Kind);
            for (int a = g.First; a < g.First + g.Count; a++)
            {
                Assert.Equal(i, all[a].Group);
                for (int b = a + 1; b < g.First + g.Count; b++)
                    Assert.False(all[a].Kind == all[b].Kind
                        && MathF.Abs(all[a].Height - all[b].Height) < PetCityFungi.AlikeHeight * MathF.Max(all[a].Height, all[b].Height),
                        $"group {i} has two alike {all[a].Kind}");
            }
        }
        // The lamps: neighbors never share a cap shape or a color; the cap roofs: neighbors never share a shape.
        var props = PetCityLayout.CityProps;
        var lampShapes = new HashSet<string>();
        for (int i = 0; i < props.Length; i++)
        {
            var cap = PetCityFungi.LampCapOf(i);
            Assert.Equal(props[i].Kind == PropKind.Lamp, cap.HasValue);
            if (cap == null)
                continue;
            lampShapes.Add(cap.Value.Model);
            for (int j = 0; j < i; j++)
                if (PetCityFungi.LampCapOf(j) is { } other && System.Numerics.Vector3.Distance(props[i].Position, props[j].Position) < PetCityFungi.LampNeighborDistance)
                {
                    Assert.NotEqual(other.Model, cap.Value.Model);
                    Assert.NotEqual(other.Color, cap.Value.Color);
                }
        }
        Assert.Equal(PetCityFungi.LampCapModels.Length, lampShapes.Count);
        var pieces = PetCityLayout.Pieces;
        var roofs = new HashSet<string>();
        for (int i = 0; i < pieces.Length; i++)
        {
            string? roof = PetCityFungi.RoofOf(i);
            Assert.Equal(pieces[i].Kind == PieceKind.CityBlock, roof != null);
            if (roof == null)
                continue;
            Assert.Equal(pieces[i].Style % 2 == 0, roof != "RoofTerrace");
            roofs.Add(roof);
            for (int j = 0; j < i; j++)
                if (roof != "RoofTerrace" && PetCityFungi.RoofOf(j) == roof)
                    Assert.True(System.Numerics.Vector3.Distance(pieces[i].Position, pieces[j].Position) >= PetCityFungi.RoofNeighborDistance, $"blocks {i} and {j} share {roof}");
        }
        foreach (var roof in PetCityFungi.RoofCapModels)
            Assert.Contains(roof, roofs);
    }

    [Fact]
    public void TheFungiAreTheSameEveryRun()
    {
        var (groups, fungi) = PetCityFungi.Build();
        Assert.Equal(PetCityFungi.Groups.ToArray(), groups);
        Assert.Equal(PetCityFungi.All.ToArray(), fungi);
        Assert.True(fungi.Length >= 300, $"{fungi.Length} fungi");
    }

    [Fact]
    public void FungiAreSmallDecorationAndLeaveEverySolidThingAsItWas()
    {
        var props = PetCityLayout.CityProps;
        foreach (var f in PetCityFungi.All)
        {
            Assert.True(f.Height is > 0.04f and <= PetCityFungi.MaxHeight && PetCityFungi.MaxHeight < 0.4f, $"{f.Kind} is {f.Height} m tall");
            Assert.True(f.Width is > 0.7f and < 1.4f && f.Tilt is >= 0f and <= 0.35f);
            float ground = Ground.HeightAt(f.Position.X, f.Position.Z);
            switch (f.Host)
            {
                case FungusHost.BuildingFoot:
                case FungusHost.LampFoot:
                    // On the ground, outside every building's plinth and every prop.
                    Assert.InRange(f.Position.Y, ground - 0.05f, ground + 0.01f);
                    foreach (var p in PetCityLayout.Pieces)
                        Assert.False(Buildings.All[p.BuildingIndex].Contains(f.Position.X, f.Position.Z, PetCityLayout.PlinthOverhang + 0.05f), $"{f.Kind} inside a building");
                    foreach (var p in props)
                        Assert.True(System.Numerics.Vector2.Distance(new(f.Position.X, f.Position.Z), new(p.Position.X, p.Position.Z)) > p.Radius, $"{f.Kind} inside prop {p.PropIndex}");
                    break;
                case FungusHost.Planter:
                case FungusHost.LampPost:
                    // On the prop, inside its footprint.
                    var host = Nearest(props, f.Position);
                    Assert.Equal(f.Host == FungusHost.Planter ? PropKind.Planter : PropKind.Lamp, host.Kind);
                    Assert.True(System.Numerics.Vector2.Distance(new(f.Position.X, f.Position.Z), new(host.Position.X, host.Position.Z)) < host.Radius);
                    Assert.True(f.Position.Y > host.Footing.High && f.Position.Y + f.Height < host.Footing.High + host.Height + PetCityFungi.MaxHeight);
                    break;
                case FungusHost.Terrace:
                    var on = Array.Find(PetCityLayout.Pieces.ToArray(), p => Buildings.All[p.BuildingIndex].Contains(f.Position.X, f.Position.Z));
                    Assert.Equal(PieceKind.CityBlock, on.Kind);
                    Assert.Equal("RoofTerrace", PetCityFungi.RoofOf(Array.IndexOf(PetCityLayout.Pieces.ToArray(), on)));
                    Assert.InRange(f.Position.Y, on.Footing.High + on.Size.Y - PetCityLayout.RoofHeight, on.Footing.High + on.Size.Y);
                    break;
            }
        }
        // Everything solid is still exactly the Map's (the layout's own tests check where and how large).
        Assert.Equal(PetCityLayout.CityProps.Length, Array.FindAll(Props.All.ToArray(), p => p.Area == Area.PetCity).Length);
    }

    private static CityProp Nearest(ReadOnlySpan<CityProp> props, System.Numerics.Vector3 at)
    {
        var best = props[0];
        foreach (var p in props)
            if (System.Numerics.Vector2.Distance(new(p.Position.X, p.Position.Z), new(at.X, at.Z))
                < System.Numerics.Vector2.Distance(new(best.Position.X, best.Position.Z), new(at.X, at.Z)))
                best = p;
        return best;
    }

    [Fact]
    public void FungiAreVividAndTheGlowingOnesGlowAtNight()
    {
        foreach (var look in PetCityFungi.Looks)
            foreach (var tint in new[] { look.TintA, look.TintB })
            {
                if (look.Pale)
                    Assert.True(tint.Value >= 0.94f && tint.Min >= 0.8f, $"{look.Model} {tint} is not a clean pale cap");
                else
                    Assert.True(tint.Value >= 0.85f && tint.Saturation >= 0.5f, $"{look.Model} {tint} is not vivid");
            }
        foreach (var f in PetCityFungi.All)
        {
            var look = PetCityFungi.Look(f.Kind);
            Assert.True(f.Tint.Value >= MathF.Min(look.TintA.Value, look.TintB.Value) - 0.03f, $"{f.Kind} {f.Tint} is duller than its kind");
            Assert.True(look.Pale || f.Tint.Saturation >= 0.5f, $"{f.Kind} {f.Tint} is not vivid");
        }
        var fly = PetCityFungi.Look(FungusKind.FlyAgaric).TintA;
        Assert.True(fly.R == 1f && fly.G <= 0.2f && fly.B <= 0.2f, "fly agarics are red");
        // Only the bioluminescent ones glow: blue and green, clearly at night.
        foreach (var kind in Enum.GetValues<FungusKind>())
        {
            var look = PetCityFungi.Look(kind);
            Assert.Equal(kind == FungusKind.GlowCap, look.Glows);
            Assert.Equal(look.Glows, ModelMaterials(look.Model).Contains("FungusGlow"));
        }
        var glow = PetCityFungi.Look(FungusKind.GlowCap);
        Assert.True(glow.TintA.Hue is > 170f and < 220f && glow.TintB.Hue is > 110f and < 160f, "glow caps are blue and green");
        Assert.True(PetCityLook.FungusGlowNight >= 0.7f * PetCityLook.LampGlowNight && PetCityLook.FungusGlowNight >= 5f * PetCityLook.FungusGlowDay);
        foreach (var lampCap in PetCityLook.LampCaps)
            Assert.True(lampCap.Value >= 0.98f && lampCap.Saturation >= 0.7f, $"lamp cap {lampCap}");
    }

    [Fact]
    public void TheModelHasEveryFungusLampCapAndRoof()
    {
        foreach (var look in PetCityFungi.Looks)
        {
            var used = ModelMaterials(look.Model);
            Assert.True(used.Contains("FungusCap") || used.Contains("FungusGlow"), look.Model);
            foreach (var m in used)
                Assert.True(PetCityLook.IsMushroom(m), $"{look.Model} uses {m}");
        }
        Assert.Contains("Spots", ModelMaterials("FungusFlyAgaric"));
        Assert.Contains("MorelPit", ModelMaterials("FungusMorel"));
        Assert.Contains("Ink", ModelMaterials("FungusInkCap"));
        foreach (var cap in PetCityFungi.LampCapModels)
            Assert.Contains("LampCap", ModelMaterials(cap));
        foreach (var roof in PetCityFungi.RoofCapModels)
            Assert.Contains("CapRoof", ModelMaterials(roof));
        // The planters and terraces leave their soil to the planted fungi.
        Assert.DoesNotContain("Cap", ModelMaterials("Planter"));
        Assert.Contains("Cap", ModelMaterials("RoofTerrace"));
    }

    [Fact]
    public void MushroomsHaveNoEyes()
    {
        var objects = new List<string> { "RoofCap", "RoofTerrace", "PetStore", "CityLamp", "Planter", "Statue" };
        objects.AddRange(PetCityFungi.LampCapModels);
        objects.AddRange(PetCityFungi.RoofCapModels);
        foreach (var look in PetCityFungi.Looks)
            objects.Add(look.Model);
        foreach (var name in objects)
        {
            var used = ModelMaterials(name);
            Assert.DoesNotContain("Eye", used);
            Assert.DoesNotContain("EyeShine", used);
        }
    }
}
