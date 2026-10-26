using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Numerics;
using Fungtopia.DayNight;
using Fungtopia.Map;
using Fungtopia.Player;
using Fungtopia.Shell;
using Xunit;

namespace Fungtopia.Town.Tests;

public class TownTests
{
    /// <summary>Fake of the shell's bus: delivers messages like the real one and records what Town publishes.</summary>
    private sealed class FakeBus : IMessageBus
    {
        private readonly Dictionary<Type, List<object>> _handlers = new();
        public readonly List<TownLights> Lights = new();
        public int Published;

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
                _handlers[typeof(T)] = list = new List<object>();
            list.Add(handler);
        }

        public void Publish<T>(in T message) where T : struct
        {
            if (message is TownLights lights)
            {
                Lights.Add(lights);
                Published++;
            }
            if (_handlers.TryGetValue(typeof(T), out var list))
                foreach (var handler in list)
                    ((IMessageHandler<T>)handler).Handle(message);
        }
    }

    private static readonly TickContext Step = new(0, 1.0 / 60);

    private static PlayerState In(Area area) =>
        new(Vector3.Zero, 0f, Vector3.Zero, true, 10, 10, true, CharacterKind.Girl, area);

    private static TimeOfDay At(int day, float hour) => new(day, hour, TimeOfDay.IsNightAt(hour));

    // ---- The placement list ----

    [Fact]
    public void EveryTownBuildingOfTheMapIsDrawnExactlyOnceWhereTheMapPutsIt()
    {
        var buildings = Buildings.All;
        int town = 0;
        for (int i = 0; i < buildings.Length; i++)
        {
            var b = buildings[i];
            int drawn = 0;
            foreach (var p in TownLayout.All)
            {
                if (p.BuildingIndex != i)
                    continue;
                drawn++;
                Assert.True(TownLayout.TryPieceFor(b.Kind, out var piece));
                Assert.Equal(piece, p.Piece);
                Assert.Equal(b.X, p.X);
                Assert.Equal(b.Z, p.Z);
                Assert.Equal(b.Rotation, p.Yaw);
                Assert.Equal(b.Width, p.Width);
                Assert.Equal(b.Depth, p.Depth);
                Assert.Equal(b.Height, p.Height);
            }
            bool drawable = b.Area == Area.Town && TownLayout.TryPieceFor(b.Kind, out _);
            Assert.Equal(drawable ? 1 : 0, drawn);
            if (drawable)
                town++;
        }
        Assert.Equal(11, town);
    }

    [Fact]
    public void EachNamedBuildingAndFourPlainHousesInDifferentLooks()
    {
        int Count(TownPiece piece)
        {
            int n = 0;
            foreach (var p in TownLayout.All)
                if (p.Piece == piece)
                    n++;
            return n;
        }
        Assert.Equal(1, Count(TownPiece.PlayerHouse));
        Assert.Equal(1, Count(TownPiece.MillieHouse));
        Assert.Equal(1, Count(TownPiece.Clinic));
        Assert.Equal(1, Count(TownPiece.FurnitureShop));
        Assert.Equal(1, Count(TownPiece.Fountain));
        Assert.Equal(1, Count(TownPiece.ClothingShop));
        Assert.Equal(1, Count(TownPiece.FoodStall));
        Assert.Equal(4, Count(TownPiece.House));
        var looks = new HashSet<int>();
        foreach (var p in TownLayout.All)
            if (p.Piece == TownPiece.House)
                looks.Add(p.Variant);
        Assert.Equal(new HashSet<int> { 0, 1, 2, 3 }, looks);

        // Your house is the biggest.
        var home = Buildings.First(BuildingKind.PlayerHouse);
        foreach (var b in Buildings.All)
            if (b.Area == Area.Town && b.Kind != BuildingKind.PlayerHouse)
                Assert.True(b.Width * b.Depth * b.Height < home.Width * home.Depth * home.Height, b.Kind.ToString());
    }

    [Theory]
    [InlineData(BuildingKind.ClothingShop, TownPiece.ClothingShop)]
    [InlineData(BuildingKind.FoodStall, TownPiece.FoodStall)]
    public void AbbysShopAndTheFoodStallAreDrawnOnceAtTheMapsSpot(BuildingKind kind, TownPiece expected)
    {
        Assert.True(TownLayout.TryPieceFor(kind, out var piece));
        Assert.Equal(expected, piece);
        var buildings = Buildings.All;
        int index = -1;
        for (int i = 0; i < buildings.Length; i++)
            if (buildings[i].Kind == kind)
                index = i;
        Assert.True(index >= 0);
        var shop = buildings[index];

        int drawn = 0;
        foreach (var p in TownLayout.All)
        {
            if (p.Piece != expected)
                continue;
            drawn++;
            Assert.Equal(index, p.BuildingIndex);
            Assert.Equal(0, p.Variant);
            Assert.Equal(shop.X, p.X);
            Assert.Equal(shop.Z, p.Z);
            Assert.Equal(shop.Rotation, p.Yaw);
            Assert.Equal(shop.Width, p.Width);
            Assert.Equal(shop.Depth, p.Depth);
            Assert.Equal(shop.Height, p.Height);
        }
        Assert.Equal(1, drawn);
    }

    /// <summary>The repository's root (the folder holding modules/Town/Models/town.glb).</summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "modules", "Town", "Models", "town.glb")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    /// <summary>The JSON chunk of Models/town.glb.</summary>
    private static JsonDocument ModelJson()
    {
        byte[] glb = File.ReadAllBytes(Path.Combine(RepoRoot(), "modules", "Town", "Models", "town.glb"));
        Assert.Equal("glTF", Encoding.ASCII.GetString(glb, 0, 4));
        int length = BitConverter.ToInt32(glb, 12);
        return JsonDocument.Parse(Encoding.UTF8.GetString(glb, 20, length));
    }

    /// <summary>The names of the objects in Models/town.glb.</summary>
    private static HashSet<string> ModelObjects()
    {
        using var json = ModelJson();
        var names = new HashSet<string>();
        foreach (var node in json.RootElement.GetProperty("nodes").EnumerateArray())
            if (node.TryGetProperty("name", out var name))
                names.Add(name.GetString()!);
        return names;
    }

    /// <summary>The town.glb object a placement draws (as TownView.ModelName): a House or flower clump by its look, a mushroom by its kind, anything else by its piece.</summary>
    private static string ModelName(in TownPlacement p) => p.Piece switch
    {
        TownPiece.House => $"House{p.Variant}",
        TownPiece.Flowers => $"Flowers{p.Variant}",
        TownPiece.Mushroom => TownMushrooms.ModelName((MushroomKind)p.Variant),
        _ => p.Piece.ToString(),
    };

    [Fact]
    public void TheModelHasAnObjectForEveryPlacementIncludingAbbysShop()
    {
        var names = ModelObjects();
        Assert.Contains("ClothingShop", names);
        Assert.Contains("FoodStall", names);
        foreach (var p in TownLayout.All)
        {
            Assert.Contains(ModelName(p), names);
        }
    }

    /// <summary>The materials object <paramref name="name"/> of Models/town.glb is drawn with.</summary>
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
        return used;
    }

    [Fact]
    public void TheFoodStallIsAWoodenStallUnderASpottedCapWithLanternsThatLightAtNight()
    {
        var used = ModelMaterials("FoodStall");
        // Wood, a red cap with white spots, the "Mushroom Munchies" sign, the food on the counter,
        // and Glow (the lanterns), which TownView lights at night with the street lamps.
        foreach (var mat in new[] { "Wood", "WoodDark", "Cap", "Spots", "SignText", "Muffin", "Soup", "Pie", "Steam", "Glow" })
            Assert.Contains(mat, used);
    }

    /// <summary>The base colors (r, g, b, as town.py sets them) of Models/town.glb's materials, by name.</summary>
    private static Dictionary<string, Vector3> ModelColors()
    {
        using var json = ModelJson();
        var colors = new Dictionary<string, Vector3>();
        foreach (var material in json.RootElement.GetProperty("materials").EnumerateArray())
        {
            // glTF leaves the factor out when it is pure white (its default).
            var color = Vector3.One;
            if (material.GetProperty("pbrMetallicRoughness").TryGetProperty("baseColorFactor", out var c))
                color = new Vector3(c[0].GetSingle(), c[1].GetSingle(), c[2].GetSingle());
            colors[material.GetProperty("name").GetString()!] = color;
        }
        return colors;
    }

    private static float Saturation(Vector3 c)
    {
        float max = MathF.Max(c.X, MathF.Max(c.Y, c.Z)), min = MathF.Min(c.X, MathF.Min(c.Y, c.Z));
        return max <= 0f ? 0f : (max - min) / max;
    }

    [Fact]
    public void MushroomCapsAreVividWithPureWhiteSpotsAndCleanCreamStems()
    {
        var colors = ModelColors();
        // Every colored cap clearly saturated and bright (the dull ones were 0.25 to 0.4 saturated).
        foreach (var cap in TownLook.Caps)
        {
            Assert.True(colors.ContainsKey(cap), cap);
            if (cap == "CapWhite")
                continue;
            var c = colors[cap];
            Assert.True(Saturation(c) >= 0.6f, $"{cap} {c} is not saturated enough");
            Assert.True(MathF.Max(c.X, MathF.Max(c.Y, c.Z)) >= 0.95f, $"{cap} {c} is not bright enough");
        }
        // The red caps a pure cherry/tomato red, not brownish or pinkish.
        var red = colors["Cap"];
        Assert.True(red.X >= 0.9f && red.Y <= 0.15f && red.Z <= 0.15f, $"Cap {red}");
        // The clinic's cap and the white spots pure white.
        foreach (var white in new[] { "CapWhite", "Spots" })
            Assert.Equal(new Vector3(1f, 1f, 1f), colors[white]);
        // Stems (the houses' walls, the fountain's, lamps' and bench legs) a clean light cream.
        var stem = colors["Wall"];
        Assert.True(stem.X >= 0.99f && stem.Y >= 0.94f && stem.Z is >= 0.8f and <= 0.9f, $"Wall {stem}");
    }

    [Fact]
    public void EveryCapMaterialIsGlossyAndRimLitAndGlowIsTwiceAsBright()
    {
        // Each Cap* material of the model is in TownLook.Caps, which TownModels shades with the
        // glossy band and rim (CapPinkInner is the inside of Millie's cat-ear chimneys, not a cap).
        foreach (var name in ModelColors().Keys)
            if (name.StartsWith("Cap") && name != "CapPinkInner")
                Assert.True(TownLook.IsCap(name), name);
        Assert.False(TownLook.IsCap("Wall"));
        Assert.False(TownLook.IsCap("Spots"));
        Assert.True(TownLook.CapGloss > 0f);
        Assert.True(TownLook.CapRim > 0f);
        // Glowing mushrooms (Glow: lamp bulbs, the street lamps' glowing spots, the lanterns) twice
        // as bright as before (0.12 by day, 2.2 at night); the windows stay as they were.
        Assert.Equal(2f * 0.12f, TownLook.GlowAt(0f), 4);
        Assert.Equal(2f * 2.2f, TownLook.GlowAt(1f), 4);
        Assert.Equal(0.12f * 0.7f, TownLook.WindowGlowAt(0f), 4);
        Assert.Equal(2.2f * 0.7f, TownLook.WindowGlowAt(1f), 4);
        // The bioluminescent mushrooms' caps glow with the lamps (clearly at night), and are caps.
        foreach (var name in new[] { "Glow", "CapGlowBlue", "CapGlowGreen" })
            Assert.True(TownLook.Glowing(name), name);
        Assert.True(TownLook.IsCap("CapGlowBlue") && TownLook.IsCap("CapGlowGreen"));
        Assert.False(TownLook.Glowing("Cap"));
        Assert.False(TownLook.Glowing("Window"));
        Assert.True(TownLook.GlowAt(1f) > 10f * TownLook.GlowAt(0f));
        foreach (var shape in TownMushrooms.Shapes)
        {
            var used = ModelMaterials(TownMushrooms.ModelName(shape.Kind));
            bool glows = false;
            foreach (var mat in used)
                glows |= TownLook.Glowing(mat);
            Assert.Equal(shape.Glows, glows);
        }
        Assert.Contains("CapGlowBlue", ModelMaterials("MushroomGlowBlue"));
        Assert.Contains("CapGlowGreen", ModelMaterials("MushroomGlowGreen"));
    }

    /// <summary>Hue in degrees (0..360) of a color.</summary>
    private static float Hue(Vector3 c)
    {
        float max = MathF.Max(c.X, MathF.Max(c.Y, c.Z)), min = MathF.Min(c.X, MathF.Min(c.Y, c.Z)), d = max - min;
        if (d <= 0f)
            return 0f;
        float h = max == c.X ? (c.Y - c.Z) / d : max == c.Y ? 2f + (c.Z - c.X) / d : 4f + (c.X - c.Y) / d;
        return (h * 60f + 360f) % 360f;
    }

    private static float Luminance(Vector3 c) => 0.2126f * c.X + 0.7152f * c.Y + 0.0722f * c.Z;

    [Fact]
    public void TheTownIsVibrantCheerfulAndStillReadable()
    {
        var colors = ModelColors();
        // Every surface drawn a little richer than its material, mildly (never neon).
        Assert.InRange(TownLook.Vividness, 1.05f, 1.3f);
        // Lush bright greens (no grey olive: the old leaf was 0.51 saturated), with yellow-green highlights.
        foreach (var leaf in new[] { "Leaf", "LeafLight" })
        {
            var c = colors[leaf];
            Assert.True(c.Y >= c.X && c.Y >= c.Z && Saturation(c) >= 0.7f && c.Y >= 0.8f, $"{leaf} {c}");
        }
        Assert.InRange(Hue(colors["Leaf"]), 100f, 140f);
        Assert.InRange(Hue(colors["LeafLight"]), 70f, 95f);
        // Warm golden wood and soil, clean bright stone, darker trims for contrast.
        foreach (var warm in new[] { "Wood", "Soil", "Basket", "Hanger" })
        {
            var c = colors[warm];
            Assert.True(c.X > c.Y && c.Y > c.Z && Saturation(c) >= 0.65f, $"{warm} {c}");
        }
        Assert.True(colors["Wood"].X >= 0.85f);
        var stone = colors["Stone"];
        Assert.True(MathF.Min(stone.X, MathF.Min(stone.Y, stone.Z)) >= 0.74f && Saturation(stone) <= 0.12f, $"Stone {stone}");
        Assert.True(Luminance(colors["StoneDark"]) < Luminance(stone) * 0.8f);
        Assert.True(Luminance(colors["WoodDark"]) < Luminance(colors["Wood"]) * 0.6f);
        // Flowers in many hues: white and at least six clearly different bright hues.
        var hues = new List<float>();
        foreach (var (name, c) in colors)
        {
            if (!name.StartsWith("Petal") || name == "PetalWhite")
                continue;
            Assert.True(Saturation(c) >= 0.55f && MathF.Max(c.X, MathF.Max(c.Y, c.Z)) >= 0.95f, $"{name} {c}");
            hues.Add(Hue(c));
        }
        Assert.True(colors.ContainsKey("PetalWhite") && hues.Count >= 6, $"{hues.Count} petal colors");
        int distinct = 0;
        for (int i = 0; i < hues.Count; i++)
        {
            bool alike = false;
            for (int k = 0; k < i; k++)
                alike |= MathF.Min(MathF.Abs(hues[i] - hues[k]), 360f - MathF.Abs(hues[i] - hues[k])) < 15f;
            if (!alike)
                distinct++;
        }
        Assert.True(distinct >= 6, $"{distinct} petal hues");
        // Candy-bright paints on doors, awnings, mats, cushions and fabrics; never neon (some depth
        // left in every paint) nor washed out to white.
        foreach (var paint in new[] { "DoorRed", "DoorBlue", "DoorGreen", "DoorLilac", "Awning", "Mat", "Cushion", "Knob", "FabricPink", "FabricYellow", "FabricBlue", "FabricOrange", "Water", "Cross" })
        {
            var c = colors[paint];
            Assert.True(Saturation(c) is >= 0.55f and <= 0.9f, $"{paint} {c}");
            Assert.True(MathF.Max(c.X, MathF.Max(c.Y, c.Z)) >= 0.95f, $"{paint} {c}");
        }
        foreach (var (name, c) in colors)
            Assert.False(Saturation(c) > 0.97f && !TownLook.IsCap(name), $"{name} {c} is neon");
        // The lamp posts dark enough to stand out against the bright town; windows still warm.
        Assert.True(MathF.Max(colors["Metal"].X, MathF.Max(colors["Metal"].Y, colors["Metal"].Z)) <= 0.5f);
        Assert.InRange(Hue(colors["Window"]), 35f, 55f);
        // Cushions, the outfits in Abby's window and the berry jars keep materials of their own.
        Assert.Contains("FabricPink", ModelMaterials("FurnitureShop"));
        Assert.Contains("FabricBlue", ModelMaterials("ClothingShop"));
        Assert.Contains("Jar", ModelMaterials("FoodStall"));
        // The gardens: planters and flower clumps with bright and yellow-green leaves.
        Assert.Contains("LeafLight", ModelMaterials("Planter"));
        for (int v = 0; v < TownLayout.FlowerVariants; v++)
        {
            var used = ModelMaterials($"Flowers{v}");
            Assert.Contains("Leaf", used);
            Assert.Contains("LeafLight", used);
        }
    }

    // ---- Garden mushrooms ----

    /// <summary>The bounds (min, max; glTF axes, Y up) of object <paramref name="name"/>'s mesh in Models/town.glb.</summary>
    private static (Vector3 Min, Vector3 Max) ModelBounds(string name)
    {
        using var json = ModelJson();
        var root = json.RootElement;
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        bool found = false;
        foreach (var node in root.GetProperty("nodes").EnumerateArray())
        {
            if (!node.TryGetProperty("name", out var n) || n.GetString() != name)
                continue;
            found = true;
            var mesh = root.GetProperty("meshes")[node.GetProperty("mesh").GetInt32()];
            foreach (var primitive in mesh.GetProperty("primitives").EnumerateArray())
            {
                var accessor = root.GetProperty("accessors")[primitive.GetProperty("attributes").GetProperty("POSITION").GetInt32()];
                var lo = accessor.GetProperty("min");
                var hi = accessor.GetProperty("max");
                min = Vector3.Min(min, new Vector3(lo[0].GetSingle(), lo[1].GetSingle(), lo[2].GetSingle()));
                max = Vector3.Max(max, new Vector3(hi[0].GetSingle(), hi[1].GetSingle(), hi[2].GetSingle()));
            }
        }
        Assert.True(found, name);
        return (min, max);
    }

    [Fact]
    public void EveryMushroomKindHasAModelOfTheSizeTheGameScalesFrom()
    {
        Assert.Equal(Enum.GetValues<MushroomKind>().Length, TownMushrooms.KindCount);
        for (int k = 0; k < TownMushrooms.KindCount; k++)
        {
            var shape = TownMushrooms.Shapes[k];
            Assert.Equal((MushroomKind)k, shape.Kind);
            var (min, max) = ModelBounds(TownMushrooms.ModelName(shape.Kind));
            string what = shape.Kind.ToString();
            float across = 2f * MathF.Max(MathF.Max(-min.X, max.X), MathF.Max(-min.Z, max.Z));
            Assert.True(MathF.Abs(across - shape.ModelWidth) < 0.005f, $"{what} {across} across");
            Assert.True(MathF.Abs(max.Y - shape.ModelHeight) < 0.005f, $"{what} {max.Y} tall");
            Assert.True(min.Y > -0.03f, $"{what} reaches {min.Y} below its base");
            // Even the largest stays small ground decoration.
            Assert.True(shape.ModelHeight * shape.MaxScale <= TownLayout.DecorationMaxHeight - 0.02f, what);
            Assert.True(shape.ModelWidth * shape.MaxScale * 1.2f <= TownLayout.DecorationMaxWidth - 0.02f, what);
        }
        // Clean cream stems and vivid caps (and the fly agaric's crisp white spots).
        var fly = ModelMaterials("MushroomFlyAgaric");
        foreach (var mat in new[] { "Cap", "Spots", "Wall", "Gills" })
            Assert.Contains(mat, fly);
        var expected = new Dictionary<string, string>
        {
            ["MushroomPorcini"] = "CapBrown", ["MushroomChanterelle"] = "CapGold", ["MushroomParasol"] = "CapTan",
            ["MushroomInkCap"] = "Ink", ["MushroomMorel"] = "MorelPit", ["MushroomEnoki"] = "CapWhite",
            ["MushroomAmethyst"] = "CapAmethyst", ["MushroomPuffball"] = "CapWhite", ["MushroomCupFungus"] = "CapScarlet",
            ["MushroomCoral"] = "CapCoral", ["MushroomJelly"] = "CapJelly",
        };
        foreach (var (name, mat) in expected)
            Assert.Contains(mat, ModelMaterials(name));
    }

    /// <summary>The garden mushroom placements.</summary>
    private static List<TownPlacement> Mushrooms()
    {
        var list = new List<TownPlacement>();
        foreach (var p in TownLayout.All)
            if (p.Piece == TownPiece.Mushroom)
                list.Add(p);
        return list;
    }

    [Fact]
    public void GardenMushroomsAreEveryKindInSeveralSizesShapesAndTilts()
    {
        var all = Mushrooms();
        Assert.InRange(all.Count, 80, 400);
        var shapes = new HashSet<CapShape>();
        for (int k = 0; k < TownMushrooms.KindCount; k++)
        {
            var heights = new HashSet<int>();
            var widths = new HashSet<int>();
            foreach (var p in all)
                if (p.Variant == k)
                {
                    heights.Add((int)MathF.Round(p.Height * 100f));
                    widths.Add((int)MathF.Round(p.Width * 100f));
                }
            string what = ((MushroomKind)k).ToString();
            Assert.True(heights.Count >= 3, $"{what}: only {heights.Count} heights");
            Assert.True(widths.Count >= 3, $"{what}: only {widths.Count} widths");
            shapes.Add(TownMushrooms.Shapes[k].Cap);
        }
        foreach (var cap in new[] { CapShape.Dome, CapShape.Flat, CapShape.Funnel, CapShape.Bell, CapShape.Wavy, CapShape.Conical })
            Assert.Contains(cap, shapes);
        // Proportions vary, not only sizes: the height to width ratio of one kind differs.
        var ratios = new HashSet<int>();
        foreach (var p in all)
            if (p.Variant == (int)MushroomKind.FlyAgaric)
                ratios.Add((int)MathF.Round(p.Height / p.Width * 20f));
        Assert.True(ratios.Count >= 3, $"{ratios.Count} fly agaric proportions");
        // Tilts vary, never more than MaxTilt.
        var tilts = new HashSet<int>();
        foreach (var p in all)
        {
            Assert.True(MathF.Abs(p.Tilt) <= TownMushrooms.MaxTilt + 1e-4f);
            tilts.Add((int)MathF.Round(p.Tilt * 100f));
        }
        Assert.True(tilts.Count >= 10, $"{tilts.Count} tilts");
        // Glowing ones among them.
        Assert.Contains(all, p => TownMushrooms.Shapes[p.Variant].Glows);
        // Nothing else leans.
        foreach (var p in TownLayout.All)
            if (p.Piece != TownPiece.Mushroom)
                Assert.Equal(0f, p.Tilt);
    }

    [Fact]
    public void MushroomsGrowSingleInPairsAndInClustersOfDifferentHeights()
    {
        var groups = new Dictionary<int, List<TownPlacement>>();
        foreach (var p in Mushrooms())
        {
            Assert.True(p.Group >= 0);
            if (!groups.TryGetValue(p.Group, out var list))
                groups[p.Group] = list = new List<TownPlacement>();
            list.Add(p);
        }
        int singles = 0, pairs = 0, clusters = 0;
        foreach (var (_, group) in groups)
        {
            if (group.Count == 1) singles++;
            else if (group.Count == 2) pairs++;
            else clusters++;
            Assert.InRange(group.Count, 1, 5);
            var first = group[0];
            for (int i = 0; i < group.Count; i++)
            {
                Assert.Equal(first.Variant, group[i].Variant);
                Assert.True(Vector2.Distance(new Vector2(first.X, first.Z), new Vector2(group[i].X, group[i].Z)) <= TownMushrooms.GroupRadius + 1e-4f);
                for (int k = 0; k < i; k++)
                    Assert.True(MathF.Abs(group[i].Height - group[k].Height) >= 0.01f, $"group at ({first.X}, {first.Z}): two of one height");
                if (i > 0)
                    Assert.True(group[i].Height < first.Height, "the first of a group is its biggest");
            }
        }
        Assert.True(singles >= 10 && pairs >= 10 && clusters >= 10, $"{singles} singles, {pairs} pairs, {clusters} clusters");
    }

    [Fact]
    public void NoTwoNeighborsAreAlike()
    {
        var all = Mushrooms();
        for (int i = 0; i < all.Count; i++)
            for (int k = 0; k < i; k++)
            {
                float d = Vector2.Distance(new Vector2(all[i].X, all[i].Z), new Vector2(all[k].X, all[k].Z));
                // Neighboring groups are different kinds; within a group each is a different size.
                if (all[i].Group != all[k].Group && d < TownMushrooms.NeighborDistance)
                    Assert.NotEqual(all[i].Variant, all[k].Variant);
                Assert.False(all[i].Variant == all[k].Variant && all[i].Height == all[k].Height && all[i].Tilt == all[k].Tilt && d < 6f,
                    $"two identical mushrooms at ({all[i].X}, {all[i].Z}) and ({all[k].X}, {all[k].Z})");
            }
        // Flower clumps side by side differ in palette, and every palette is used.
        var palettes = new HashSet<int>();
        var flowers = new List<TownPlacement>();
        foreach (var p in TownLayout.All)
            if (p.Piece == TownPiece.Flowers)
            {
                Assert.InRange(p.Variant, 0, TownLayout.FlowerVariants - 1);
                foreach (var q in flowers)
                    if (Vector2.Distance(new Vector2(p.X, p.Z), new Vector2(q.X, q.Z)) < 1.6f)
                        Assert.NotEqual(q.Variant, p.Variant);
                flowers.Add(p);
                palettes.Add(p.Variant);
            }
        Assert.Equal(TownLayout.FlowerVariants, palettes.Count);
    }

    [Fact]
    public void EachSpotsMushroomsComeFromItsFixedSeed()
    {
        // The seed is a pure function of the spot, different for spots a few centimeters apart.
        Assert.Equal(TownMushrooms.Seed(12.5f, 201.25f), TownMushrooms.Seed(12.5f, 201.25f));
        Assert.NotEqual(TownMushrooms.Seed(12.5f, 201.25f), TownMushrooms.Seed(12.55f, 201.25f));
        Assert.NotEqual(TownMushrooms.Seed(12.5f, 201.25f), TownMushrooms.Seed(12.5f, 201.3f));
        Assert.Equal(TownMushrooms.Unit(77u, 3), TownMushrooms.Unit(77u, 3));
        Assert.InRange(TownMushrooms.Unit(77u, 3), 0f, 1f);
        // Every group is exactly what its root spot's seed grows: its kind the seed's (or, when a
        // neighbor already has that kind, the next free one KindStep on), its size, members,
        // offsets, sizes and tilts from TownMushrooms.Member.
        var groups = new Dictionary<int, List<TownPlacement>>();
        foreach (var p in Mushrooms())
        {
            if (!groups.TryGetValue(p.Group, out var list))
                groups[p.Group] = list = new List<TownPlacement>();
            list.Add(p);
        }
        int asSeeded = 0;
        foreach (var (_, group) in groups)
        {
            var root = group[0];
            uint seed = TownMushrooms.Seed(root.X, root.Z);
            int kind = (int)(seed % (uint)TownMushrooms.KindCount);
            if (kind == root.Variant)
                asSeeded++;
            bool reachable = false;
            for (int t = 0; t < TownMushrooms.KindCount; t++)
                reachable |= (kind + t * TownMushrooms.KindStep) % TownMushrooms.KindCount == root.Variant;
            Assert.True(reachable);
            var k = (MushroomKind)root.Variant;
            Assert.Equal(TownMushrooms.GroupSize(k, seed), group.Count);
            for (int m = 0; m < group.Count; m++)
            {
                var (x, z, yaw, tilt, width, height) = TownMushrooms.Member(k, seed, m, group.Count, root.X, root.Z);
                Assert.Equal(x, group[m].X, 4);
                Assert.Equal(z, group[m].Z, 4);
                Assert.Equal(yaw, group[m].Yaw, 4);
                Assert.Equal(tilt, group[m].Tilt, 4);
                Assert.Equal(width, group[m].Width, 4);
                Assert.Equal(height, group[m].Height, 4);
            }
        }
        Assert.True(asSeeded * 2 > groups.Count, $"{asSeeded} of {groups.Count} groups have their seed's own kind");
    }

    [Fact]
    public void TheBuildingsAndPlantersGrowMushroomsToo()
    {
        // Shelf fungi on every house's stem, and small garden mushrooms of several kinds on its foundation.
        foreach (var house in new[] { "PlayerHouse", "MillieHouse", "Clinic", "ClothingShop", "House0", "House1", "House2", "House3" })
        {
            var used = ModelMaterials(house);
            Assert.Contains("CapBracket", used);
            int kinds = 0;
            foreach (var cap in new[] { "CapBrown", "CapGold", "CapTan", "CapMorel", "CapAmethyst", "CapScarlet", "CapCoral", "CapJelly", "CapGlowBlue", "CapGlowGreen", "Ink" })
                if (used.Contains(cap))
                    kinds++;
            Assert.True(kinds >= 3, $"{house}: {kinds} kinds of small mushrooms");
        }
        var planter = ModelMaterials("Planter");
        Assert.Contains("Cap", planter);
        Assert.Contains("CapAmethyst", planter);
    }

    [Fact]
    public void KindsTownDoesNotDrawAreSkippedNotThrown()
    {
        Assert.False(TownLayout.TryPieceFor(BuildingKind.PetStore, out _));
        Assert.False(TownLayout.TryPieceFor(BuildingKind.CityBlock, out _));
        Assert.False(TownLayout.TryPieceFor((BuildingKind)999, out _));

        // PetCity's buildings get no placement, and the layout still builds.
        var buildings = Buildings.All;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (buildings[i].Area == Area.Town)
                continue;
            foreach (var p in TownLayout.All)
                Assert.NotEqual(i, p.BuildingIndex);
        }
        Assert.True(TownLayout.Count > 10);
    }

    [Fact]
    public void BuildingsComeFirstThenTheMapsPropsThenDecoration()
    {
        var all = TownLayout.All;
        int stage = 0;
        foreach (var p in all)
        {
            int s = p.BuildingIndex >= 0 ? 0 : p.PropIndex >= 0 ? 1 : 2;
            Assert.False(p.BuildingIndex >= 0 && p.PropIndex >= 0);
            Assert.True(s >= stage);
            stage = s;
        }
        Assert.Equal(11, CountWhere(p => p.BuildingIndex >= 0));
    }

    private static int CountWhere(Func<TownPlacement, bool> match)
    {
        int n = 0;
        foreach (var p in TownLayout.All)
            if (match(p))
                n++;
        return n;
    }

    [Fact]
    public void TheDrawnPropsAreExactlyTheMapsTownProps()
    {
        // Each Town prop of the Map, in order, drawn once, where, turned and as large as the Map says.
        var props = Props.All;
        var drawn = new List<TownPlacement>();
        foreach (var p in TownLayout.All)
            if (p.PropIndex >= 0)
                drawn.Add(p);
        var expected = new List<int>();
        for (int i = 0; i < props.Length; i++)
            if (props[i].Area == Area.Town)
                expected.Add(i);
        Assert.True(expected.Count > 40);
        Assert.Equal(expected, drawn.ConvertAll(p => p.PropIndex));

        foreach (var p in drawn)
        {
            var m = props[p.PropIndex];
            string what = $"{m.Kind} at ({m.X}, {m.Z})";
            Assert.True(TownLayout.TryPieceFor(m.Kind, out var piece), what);
            Assert.Equal(piece, p.Piece);
            Assert.Equal(m.X, p.X);
            Assert.Equal(m.Z, p.Z);
            Assert.Equal(m.Rotation, p.Yaw);
            Assert.Equal(m.Height, p.Height);
            switch (m.Kind)
            {
                case PropKind.Bench:
                case PropKind.FenceSegment:
                    // As long as the Map's segment (along local X), thinner than its circles.
                    Assert.Equal(m.Radius * 2f, p.Width, 4);
                    Assert.True(p.Depth <= Math.Max(m.Radius, Props.FencePostRadius) * 2f, what);
                    break;
                case PropKind.Crate:
                    // A square whose corners touch its circle.
                    Assert.Equal(p.Width, p.Depth);
                    Assert.Equal(m.Radius, MathF.Sqrt(p.Width * p.Width + p.Depth * p.Depth) * 0.5f, 4);
                    break;
                default:
                    Assert.Equal(m.Radius * 2f, p.Width, 4);
                    Assert.Equal(m.Radius * 2f, p.Depth, 4);
                    break;
            }
        }
    }

    [Fact]
    public void EveryTownPropKindOfTheMapHasAPieceAndOthersAreSkipped()
    {
        foreach (var m in Props.All)
            if (m.Area == Area.Town)
                Assert.True(TownLayout.TryPieceFor(m.Kind, out _), m.Kind.ToString());
        Assert.False(TownLayout.TryPieceFor(PropKind.MediumMushroom, out _));
        Assert.False(TownLayout.TryPieceFor((PropKind)999, out _));
        // Other areas' props are not Town's to draw.
        foreach (var p in TownLayout.All)
            if (p.PropIndex >= 0)
                Assert.Equal(Area.Town, Props.All[p.PropIndex].Area);
    }

    [Fact]
    public void TheTownHasTheMapsLampsBenchesFencesPlantersAndCrates()
    {
        Assert.True(CountWhere(p => p.Piece == TownPiece.StreetLamp) >= 10);
        Assert.True(CountWhere(p => p.Piece == TownPiece.Bench) >= 3);
        Assert.True(CountWhere(p => p.Piece == TownPiece.Fence) >= 14);
        Assert.True(CountWhere(p => p.Piece == TownPiece.Planter) >= 3);
        Assert.Equal(2, CountWhere(p => p.Piece == TownPiece.Crate));
        foreach (var p in TownLayout.All)
            if (p.Piece is TownPiece.StreetLamp or TownPiece.Bench or TownPiece.Fence or TownPiece.Planter or TownPiece.Crate)
                Assert.True(p.PropIndex >= 0, $"{p.Piece} at ({p.X}, {p.Z}) is not one of the Map's props");
    }

    [Fact]
    public void OnlySmallFlowerClumpsAndMushroomsArePlacedFreely()
    {
        int flowers = 0, mushrooms = 0;
        foreach (var p in TownLayout.All)
        {
            if (!p.IsDecoration)
            {
                Assert.NotEqual(TownPiece.Mushroom, p.Piece);
                Assert.NotEqual(TownPiece.Flowers, p.Piece);
                continue;
            }
            if (p.Piece == TownPiece.Flowers)
                flowers++;
            else
            {
                Assert.Equal(TownPiece.Mushroom, p.Piece);
                mushrooms++;
            }
            Assert.True(p.Height <= TownLayout.DecorationMaxHeight, $"{p.Piece} {p.Height} m tall");
            Assert.True(p.Width <= TownLayout.DecorationMaxWidth && p.Depth <= TownLayout.DecorationMaxWidth);
            Assert.True(TownLayout.DecorationMaxHeight <= 0.4f && TownLayout.DecorationMaxWidth <= 0.6f);
        }
        Assert.InRange(flowers, 10, 200);
        // Cheap to draw: a few hundred small instances of fourteen meshes at most.
        Assert.InRange(mushrooms, 80, 400);
    }

    [Fact]
    public void FlowerClumpsAndMushroomsStayOffObstaclesPathsAndSpots()
    {
        var all = TownLayout.All;
        var spots = new[] { Places.HomeDoor, Places.ClinicDoor, Places.MillieSpot, Places.FurnitureShopSpot, Places.AbbySpot, Places.FoodSpot, Places.SignpostTown };
        for (int i = 0; i < all.Length; i++)
        {
            var p = all[i];
            if (!p.IsDecoration)
                continue;
            string what = $"{p.Piece} at ({p.X}, {p.Z})";
            float r = p.Radius;
            Assert.Equal(Area.Town, Areas.At(p.X, p.Z));
            Assert.True(Areas.Walkable(p.X, p.Z, r), what);
            Assert.False(Ground.Blocks(p.X, p.Z, r), what);
            Assert.True(TownLayout.PathDistance(p.X, p.Z) >= r + 1.5f, $"{what} on a path");
            foreach (var spot in spots)
                Assert.True(Vector2.Distance(new Vector2(p.X, p.Z), new Vector2(spot.Position.X, spot.Position.Z)) >= r + 1f, $"{what} by a spot");
            for (int k = i + 1; k < all.Length; k++)
                // Mushrooms of one group nestle together; nothing else overlaps.
                if (all[k].IsDecoration && (p.Group < 0 || all[k].Group != p.Group))
                    Assert.True(Vector2.Distance(new Vector2(p.X, p.Z), new Vector2(all[k].X, all[k].Z)) >= r + all[k].Radius - 0.02f, $"{what} overlaps another");
        }
    }

    [Fact]
    public void PathDistanceFollowsTheStreetAndTheDoorPaths()
    {
        Assert.Equal(0f, TownLayout.PathDistance(0f, 180f), 3);
        Assert.Equal(4f, TownLayout.PathDistance(4f, 180f), 3);
        var home = Buildings.First(BuildingKind.PlayerHouse);
        var front = home.Center + home.Front * (home.Depth * 0.5f + 1f);
        Assert.Equal(0f, TownLayout.PathDistance(front.X, front.Y), 3);
    }

    [Fact]
    public void TheLayoutIsTheSameEveryTime()
    {
        var a = TownLayout.All.ToArray();
        var b = TownLayout.All.ToArray();
        Assert.Equal(a, b);
        Assert.Equal(TownLayout.Count, a.Length);
    }

    // ---- Standing on the ground ----

    /// <summary>Ground under the footprint, densely: <paramref name="rings"/> rings of 64 points out to its edge, and the center.</summary>
    private static IEnumerable<Vector2> DenseFootprint(TownPiece piece, float x, float z, float yaw, float width, float depth, int rings = 4)
    {
        yield return new Vector2(x, z);
        float cy = MathF.Cos(yaw), sy = MathF.Sin(yaw);
        bool boxy = TownGround.IsBoxy(piece);
        for (int r = 1; r <= rings; r++)
            for (int i = 0; i < 64; i++)
            {
                float a = i * MathF.Tau / 64f, c = MathF.Cos(a), s = MathF.Sin(a);
                if (boxy)
                {
                    float m = MathF.Max(MathF.Abs(c), MathF.Abs(s));
                    c /= m;
                    s /= m;
                }
                float lx = c * width * 0.5f * r / rings, lz = s * depth * 0.5f * r / rings;
                yield return new Vector2(x + lx * cy + lz * sy, z - lx * sy + lz * cy);
            }
    }

    /// <summary>Points over the town, the roads and the rough Deep Forest, every few meters, walkable ones only.</summary>
    private static IEnumerable<Vector2> SamplePositions()
    {
        foreach (var area in new[] { Areas.Town, Areas.DeepForest, Areas.Forest })
            for (float x = area.MinX + 1.3f; x < area.MaxX; x += 3.7f)
                for (float z = area.MinZ + 0.9f; z < area.MaxZ; z += 3.7f)
                    if (Areas.Walkable(x, z, 0.5f))
                        yield return new Vector2(x, z);
        for (int i = 0; i < Areas.RoadPoints.Length; i++)
            if (!float.IsNaN(Areas.RoadPoints[i].X))
                yield return Areas.RoadPoints[i];
    }

    [Fact]
    public void TheFootprintIsTheCenterAndAtLeastEightPointsOnItsEdge()
    {
        Assert.True(TownGround.EdgePoints >= 8);
        var center = TownGround.FootprintPoint(TownPiece.StreetLamp, 10f, 200f, 0.7f, 0.8f, 0.8f, 0);
        Assert.Equal(new Vector2(10f, 200f), center);
        var angles = new HashSet<int>();
        for (int k = 1; k <= TownGround.EdgePoints; k++)
        {
            var w = TownGround.FootprintPoint(TownPiece.StreetLamp, 10f, 200f, 0.7f, 0.8f, 0.8f, k);
            Assert.Equal(0.4f, Vector2.Distance(w, center), 3);
            angles.Add((int)MathF.Round(MathF.Atan2(w.Y - 200f, w.X - 10f) * 100f));
        }
        Assert.Equal(TownGround.EdgePoints, angles.Count);
        // A box's edge points reach its corners.
        var corner = TownGround.FootprintPoint(TownPiece.Crate, 0f, 0f, 0f, 2f, 1f, 1 + TownGround.EdgePoints / 8);
        Assert.Equal(1f, MathF.Abs(corner.X), 3);
        Assert.Equal(0.5f, MathF.Abs(corner.Y), 3);
        Assert.Throws<ArgumentOutOfRangeException>(() => TownGround.FootprintPoint(TownPiece.Crate, 0f, 0f, 0f, 1f, 1f, TownGround.FootprintPoints));
    }

    [Fact]
    public void StemsAnywhereSitInTheGroundNeverFloatingNorSunkDeep()
    {
        // Street lamps, planters and mushroom stems of the sizes Town draws, all over the walkable world.
        var stems = new (TownPiece Piece, float Width, float Height)[]
        {
            (TownPiece.StreetLamp, 0.8f, 3.2f), (TownPiece.Planter, 1.2f, 0.7f), (TownPiece.StreetLamp, 0.6f, 2.5f),
            (TownPiece.Crate, 0.71f, 0.8f), (TownPiece.Flowers, 0.5f, 0.35f),
        };
        int checkedCount = 0, sloped = 0;
        foreach (var at in SamplePositions())
            foreach (var (piece, width, height) in stems)
            {
                float yaw = at.X * 0.37f + at.Y * 0.11f;
                var fit = TownGround.Fit(piece, at.X, at.Y, yaw, width, width, height);
                string what = $"{piece} at ({at.X}, {at.Y})";
                float high = float.MinValue, low = float.MaxValue;
                foreach (var w in DenseFootprint(piece, at.X, at.Y, yaw, width, width))
                {
                    float h = Ground.HeightAt(w.X, w.Y);
                    Assert.True(fit.Base <= h, $"{what}: base {fit.Base} floats over ground {h} at ({w.X}, {w.Y})");
                    high = MathF.Max(high, h);
                    low = MathF.Min(low, h);
                }
                // A stem (up to 0.8 m across) at most 0.3 m under its highest ground; wider pieces sink only by the drop under them.
                if (width <= 0.8f)
                    Assert.True(fit.Base >= high - 0.3f, $"{what}: base {fit.Base} sunk more than 0.3 m under the highest ground {high}");
                Assert.True(fit.Base >= low - 0.2f, $"{what}: base {fit.Base} sunk more than 0.2 m under the lowest ground {low}");
                Assert.Equal(height, fit.Top - fit.High, 3);
                Assert.Equal(fit.Top - fit.Base, fit.DrawnHeight, 3);
                checkedCount++;
                if (fit.High - fit.Low > 0.02f)
                    sloped++;
            }
        Assert.True(checkedCount > 2000, $"{checkedCount}");
        Assert.True(sloped > 500, $"only {sloped} sample spots on sloped ground");
    }

    [Fact]
    public void EveryPlacementStandsInTheGroundAndLooksAsTallAsItIs()
    {
        foreach (var p in TownLayout.All)
        {
            var fit = TownGround.Fit(p);
            string what = $"{p.Piece} at ({p.X}, {p.Z})";
            float low = float.MaxValue;
            foreach (var w in DenseFootprint(p.Piece, p.X, p.Z, p.Yaw, p.Width, p.Depth, rings: 8))
            {
                float h = Ground.HeightAt(w.X, w.Y);
                Assert.True(fit.Base <= h, $"{what}: base {fit.Base} floats over ground {h} at ({w.X}, {w.Y})");
                low = MathF.Min(low, h);
            }
            for (int k = 0; k < TownGround.FootprintPoints; k++)
            {
                var w = TownGround.FootprintPoint(p, k);
                Assert.True(fit.Base <= Ground.HeightAt(w.X, w.Y) - TownGround.SinkOf(p.Piece) + 1e-4f, what);
            }
            Assert.InRange(TownGround.SinkOf(p.Piece), 0.01f, 0.2f);
            Assert.True(fit.Base >= low - 0.2f, $"{what}: sunk {low - fit.Base} m");
            // Its stem reaches down; from the high side it is exactly as tall as the Map says.
            Assert.Equal(p.Height, fit.Top - fit.High, 3);
            Assert.True(fit.DrawnHeight >= p.Height + TownGround.SinkOf(p.Piece) - 1e-4f, what);
            if (p.BuildingIndex < 0)
                Assert.True(fit.Extra <= 0.3f, $"{what}: stem reaches {fit.Extra} m down");
        }
    }

    [Fact]
    public void BigStemsAndFoundationsFlareIntoTheGround()
    {
        foreach (var piece in new[] { TownPiece.PlayerHouse, TownPiece.MillieHouse, TownPiece.Clinic, TownPiece.House, TownPiece.ClothingShop, TownPiece.Fountain, TownPiece.StreetLamp, TownPiece.Planter })
            Assert.True(TownGround.HasRootFlare(piece), piece.ToString());
        foreach (var piece in new[] { TownPiece.Flowers, TownPiece.Fence, TownPiece.Bench, TownPiece.Crate, TownPiece.FoodStall })
            Assert.False(TownGround.HasRootFlare(piece), piece.ToString());
        // The stalls stand on a rectangular wooden base instead, sampled to its corners.
        Assert.True(TownGround.IsBoxy(TownPiece.FoodStall));
        Assert.True(TownGround.IsBoxy(TownPiece.FurnitureShop));
    }

    // ---- The module: lamps follow night ----

    [Fact]
    public void LampsAreOffByDayAndLightUpAtNightfall()
    {
        var bus = new FakeBus();
        var town = new TownModule();
        town.Start(bus);
        Assert.False(town.LampsLit);

        bus.Publish(At(1, 9f));
        town.Tick(Step);
        Assert.False(town.LampsLit);
        Assert.Empty(bus.Lights);

        bus.Publish(At(1, 19.99f));
        Assert.False(town.LampsLit);
        bus.Publish(At(1, 20f));
        Assert.True(town.LampsLit);
        Assert.Equal(new[] { true }, bus.Lights.ConvertAll(l => l.On));

        bus.Publish(At(2, 3f));
        Assert.True(town.LampsLit);
        bus.Publish(At(2, 6f));
        Assert.False(town.LampsLit);
        Assert.Equal(new[] { true, false }, bus.Lights.ConvertAll(l => l.On));
    }

    [Fact]
    public void LampsFollowIsNightAsPublished()
    {
        var bus = new FakeBus();
        var town = new TownModule();
        town.Start(bus);
        bus.Publish(new TimeOfDay(1, 12f, true));
        Assert.True(town.LampsLit);
        bus.Publish(new TimeOfDay(1, 23f, false));
        Assert.False(town.LampsLit);
    }

    [Fact]
    public void LightsArePublishedOnlyWhenTheySwitch()
    {
        var bus = new FakeBus();
        var town = new TownModule();
        town.Start(bus);
        for (int i = 0; i < 600; i++)
        {
            bus.Publish(At(1, 19.5f + i * 0.0008f));
            town.Tick(Step);
        }
        Assert.Equal(0, bus.Published);
        for (int i = 0; i < 600; i++)
        {
            bus.Publish(At(1, 20f + i * 0.001f));
            town.Tick(Step);
        }
        Assert.Equal(1, bus.Published);
    }

    [Fact]
    public void WokeUpAtTheClinicInTheMorningTheLampsAreOff()
    {
        var bus = new FakeBus();
        var town = new TownModule();
        town.Start(bus);
        bus.Publish(At(1, 22f));
        Assert.True(town.LampsLit);
        // Fainting skips the clock straight to 7:00 the next morning.
        bus.Publish(new WokeUp(2));
        bus.Publish(At(2, 7f));
        Assert.False(town.LampsLit);
    }

    // ---- The module: the town's music ----

    /// <summary>Publishes the player in <paramref name="area"/> and ticks, once per tick like the Player module; returns the level after each tick.</summary>
    private static float[] Walk(FakeBus bus, TownModule town, Area area, int ticks)
    {
        var levels = new float[ticks];
        for (int i = 0; i < ticks; i++)
        {
            bus.Publish(In(area));
            town.Tick(Step);
            levels[i] = town.MusicLevel;
        }
        return levels;
    }

    [Fact]
    public void TheMusicIsSilentBeforeThePlayerExists()
    {
        var bus = new FakeBus();
        var town = new TownModule();
        town.Start(bus);
        Assert.Equal(0f, town.MusicLevel);
        for (int i = 0; i < 600; i++)
            town.Tick(Step);
        Assert.Equal(0f, town.MusicLevel);
    }

    [Fact]
    public void InTownTheMusicRisesToFullOverTwoSeconds()
    {
        var bus = new FakeBus();
        var town = new TownModule();
        town.Start(bus);
        Assert.Equal(120, TownModule.MusicFadeTicks);

        var levels = Walk(bus, town, Area.Town, 240);
        for (int i = 1; i < 120; i++)
            Assert.True(levels[i] > levels[i - 1], $"tick {i}: {levels[i]} not above {levels[i - 1]}");
        Assert.Equal(1f / 120, levels[0], 5);
        Assert.Equal(0.25f, levels[29], 5); // 0.5 s
        Assert.Equal(0.5f, levels[59], 5); // 1 s
        Assert.True(levels[118] < 1f);
        Assert.Equal(1f, levels[119]); // 2 s
        for (int i = 120; i < 240; i++)
            Assert.Equal(1f, levels[i]);
    }

    [Theory]
    [InlineData(Area.Road)]
    [InlineData(Area.Forest)]
    [InlineData(Area.PetCity)]
    [InlineData(Area.DeepForest)]
    [InlineData(Area.Home)]
    public void OutsideTownTheMusicFallsToSilenceOverTwoSeconds(Area area)
    {
        var bus = new FakeBus();
        var town = new TownModule();
        town.Start(bus);
        Walk(bus, town, Area.Town, 120);
        Assert.Equal(1f, town.MusicLevel);

        var levels = Walk(bus, town, area, 240);
        for (int i = 1; i < 120; i++)
            Assert.True(levels[i] < levels[i - 1], $"tick {i}: {levels[i]} not below {levels[i - 1]}");
        Assert.Equal(119f / 120, levels[0], 5);
        Assert.Equal(0.5f, levels[59], 5); // 1 s
        Assert.True(levels[118] > 0f);
        Assert.Equal(0f, levels[119]); // 2 s
        for (int i = 120; i < 240; i++)
            Assert.Equal(0f, levels[i]);
    }

    [Fact]
    public void StartingOutsideTownTheMusicStaysSilent()
    {
        var bus = new FakeBus();
        var town = new TownModule();
        town.Start(bus);
        Assert.All(Walk(bus, town, Area.Forest, 300), level => Assert.Equal(0f, level));
    }

    [Fact]
    public void LeavingMidFadeTurnsTheMusicBackFromWhereItWas()
    {
        var bus = new FakeBus();
        var town = new TownModule();
        town.Start(bus);
        Walk(bus, town, Area.Town, 90); // 1.5 s in: 0.75
        Assert.Equal(0.75f, town.MusicLevel, 5);
        Walk(bus, town, Area.Road, 30); // 0.5 s out: 0.5
        Assert.Equal(0.5f, town.MusicLevel, 5);
        Walk(bus, town, Area.Town, 60); // back in: full after 1 s more
        Assert.Equal(1f, town.MusicLevel);
        // Into the house: falls again.
        Walk(bus, town, Area.Home, 60);
        Assert.Equal(0.5f, town.MusicLevel, 5);
    }

    [Fact]
    public void TheTownSongIsCommittedAsAnMp3AndBrokenBytesAreRejected()
    {
        string path = Path.Combine(RepoRoot(), "modules", "Town", "Audio", "mushroom_town.mp3");
        Assert.True(File.Exists(path), path);
        Assert.True(TownModule.LooksLikeMp3(File.ReadAllBytes(path)));
        // A bare MPEG-1 Layer III frame header.
        Assert.True(TownModule.LooksLikeMp3(new byte[] { 0xFF, 0xFB, 0x90, 0x64 }));

        Assert.False(TownModule.LooksLikeMp3(Array.Empty<byte>()));
        Assert.False(TownModule.LooksLikeMp3(new byte[] { (byte)'I', (byte)'D', (byte)'3' }));
        Assert.False(TownModule.LooksLikeMp3(Encoding.ASCII.GetBytes("<html>not found</html>")));
        Assert.False(TownModule.LooksLikeMp3(Encoding.ASCII.GetBytes("version https://git-lfs")));
        Assert.False(TownModule.LooksLikeMp3(new byte[] { 0xFF, 0xF1, 0x50, 0x80 })); // AAC (ADTS), layer 0
    }

    // ---- Windows: no near-coincident surfaces (they z-fight on a real GPU) ----

    /// <summary>The clear step (m) between any two nearly parallel faces of different parts on and round the windows.</summary>
    private const float WindowClearance = 0.02f;

    private readonly record struct Tri(Vector3 A, Vector3 B, Vector3 C, string Material, int Part)
    {
        public Vector3 Normal => Vector3.Cross(B - A, C - A);
        public Vector3 Center => (A + B + C) / 3f;
    }

    /// <summary>
    /// The triangles of object <paramref name="name"/> of Models/town.glb (glTF axes, Y up), each with
    /// its material and its part: the connected piece of mesh it belongs to (a pane, a frame, a wall...).
    /// </summary>
    private static List<Tri> ModelTriangles(string name)
    {
        string path = Path.Combine(RepoRoot(), "modules", "Town", "Models", "town.glb");
        byte[] glb = File.ReadAllBytes(path);
        int jsonLength = BitConverter.ToInt32(glb, 12);
        int bin = 20 + jsonLength + 8;
        using var json = ModelJson();
        var root = json.RootElement;
        var accessors = root.GetProperty("accessors");
        var views = root.GetProperty("bufferViews");
        var materials = root.GetProperty("materials");

        int Start(JsonElement accessor)
        {
            var view = views[accessor.GetProperty("bufferView").GetInt32()];
            int offset = view.TryGetProperty("byteOffset", out var o) ? o.GetInt32() : 0;
            return bin + offset + (accessor.TryGetProperty("byteOffset", out var a) ? a.GetInt32() : 0);
        }

        var raw = new List<(Vector3, Vector3, Vector3, string)>();
        foreach (var node in root.GetProperty("nodes").EnumerateArray())
        {
            if (!node.TryGetProperty("name", out var n) || n.GetString() != name)
                continue;
            Assert.False(node.TryGetProperty("translation", out _) || node.TryGetProperty("rotation", out _) || node.TryGetProperty("scale", out _), name);
            var mesh = root.GetProperty("meshes")[node.GetProperty("mesh").GetInt32()];
            foreach (var primitive in mesh.GetProperty("primitives").EnumerateArray())
            {
                string material = materials[primitive.GetProperty("material").GetInt32()].GetProperty("name").GetString()!;
                var pos = accessors[primitive.GetProperty("attributes").GetProperty("POSITION").GetInt32()];
                int p0 = Start(pos);
                Vector3 Vertex(int i) => new(BitConverter.ToSingle(glb, p0 + 12 * i), BitConverter.ToSingle(glb, p0 + 12 * i + 4), BitConverter.ToSingle(glb, p0 + 12 * i + 8));
                var idx = accessors[primitive.GetProperty("indices").GetInt32()];
                int i0 = Start(idx), count = idx.GetProperty("count").GetInt32(), type = idx.GetProperty("componentType").GetInt32();
                int Index(int k) => type switch
                {
                    5121 => glb[i0 + k],
                    5123 => BitConverter.ToUInt16(glb, i0 + 2 * k),
                    _ => (int)BitConverter.ToUInt32(glb, i0 + 4 * k),
                };
                for (int k = 0; k + 2 < count; k += 3)
                    raw.Add((Vertex(Index(k)), Vertex(Index(k + 1)), Vertex(Index(k + 2)), material));
            }
        }
        Assert.NotEmpty(raw);

        // Parts: triangles joined by shared corners (positions to 0.1 mm, across materials and seams).
        var keys = new Dictionary<(int, int, int), int>();
        var parent = new List<int>();
        int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
        int Key(Vector3 v)
        {
            var k = ((int)MathF.Round(v.X * 1e4f), (int)MathF.Round(v.Y * 1e4f), (int)MathF.Round(v.Z * 1e4f));
            if (!keys.TryGetValue(k, out int id))
            {
                keys[k] = id = parent.Count;
                parent.Add(id);
            }
            return id;
        }
        var corners = new List<(int, int, int)>();
        foreach (var (a, b, c, _) in raw)
        {
            int ka = Key(a), kb = Key(b), kc = Key(c);
            parent[Find(kb)] = Find(ka);
            parent[Find(kc)] = Find(ka);
            corners.Add((ka, kb, kc));
        }
        var tris = new List<Tri>(raw.Count);
        for (int i = 0; i < raw.Count; i++)
            tris.Add(new Tri(raw[i].Item1, raw[i].Item2, raw[i].Item3, raw[i].Item4, Find(corners[i].Item1)));
        return tris;
    }

    private static Vector3 ClosestOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        // Ericson, Real-Time Collision Detection, 5.1.5.
        Vector3 ab = b - a, ac = c - a, ap = p - a;
        float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0f && d2 <= 0f) return a;
        Vector3 bp = p - b;
        float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0f && d4 <= d3) return b;
        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + d1 / (d1 - d3) * ab;
        Vector3 cp = p - c;
        float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0f && d5 <= d6) return c;
        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + d2 / (d2 - d6) * ac;
        float va = d3 * d6 - d5 * d4;
        if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f) return b + (d4 - d3) / (d4 - d3 + d5 - d6) * (c - b);
        float denom = 1f / (va + vb + vc);
        return a + ab * (vb * denom) + ac * (vc * denom);
    }

    private static float SegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
    {
        // Ericson 5.1.9.
        Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
        float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
        float s, t;
        if (a <= 1e-12f && e <= 1e-12f) return Vector3.Distance(p1, p2);
        if (a <= 1e-12f) { s = 0f; t = Math.Clamp(f / e, 0f, 1f); }
        else
        {
            float c = Vector3.Dot(d1, r);
            if (e <= 1e-12f) { t = 0f; s = Math.Clamp(-c / a, 0f, 1f); }
            else
            {
                float b = Vector3.Dot(d1, d2), denom = a * e - b * b;
                s = denom > 1e-12f ? Math.Clamp((b * f - c * e) / denom, 0f, 1f) : 0f;
                t = (b * s + f) / e;
                if (t < 0f) { t = 0f; s = Math.Clamp(-c / a, 0f, 1f); }
                else if (t > 1f) { t = 1f; s = Math.Clamp((b - c) / a, 0f, 1f); }
            }
        }
        return Vector3.Distance(p1 + d1 * s, p2 + d2 * t);
    }

    /// <summary>Where segment p..q crosses triangle t, as a share of the way from p to q (-1 when it does not).</summary>
    private static float SegmentHit(Vector3 p, Vector3 q, in Tri t)
    {
        Vector3 d = q - p, e1 = t.B - t.A, e2 = t.C - t.A, h = Vector3.Cross(d, e2);
        float det = Vector3.Dot(e1, h);
        if (MathF.Abs(det) < 1e-12f) return -1f;
        Vector3 s = p - t.A;
        float u = Vector3.Dot(s, h) / det;
        if (u < 0f || u > 1f) return -1f;
        Vector3 qv = Vector3.Cross(s, e1);
        float v = Vector3.Dot(d, qv) / det;
        if (v < 0f || u + v > 1f) return -1f;
        float k = Vector3.Dot(e2, qv) / det;
        return k >= 0f && k <= 1f ? k : -1f;
    }

    private static bool SegmentHitsTriangle(Vector3 p, Vector3 q, in Tri t) => SegmentHit(p, q, t) >= 0f;

    /// <summary>The distance between two triangles (0 when they cross).</summary>
    private static float TriangleDistance(in Tri x, in Tri y)
    {
        Vector3[] xs = { x.A, x.B, x.C }, ys = { y.A, y.B, y.C };
        for (int i = 0; i < 3; i++)
            if (SegmentHitsTriangle(xs[i], xs[(i + 1) % 3], y) || SegmentHitsTriangle(ys[i], ys[(i + 1) % 3], x))
                return 0f;
        float best = float.MaxValue;
        for (int i = 0; i < 3; i++)
        {
            best = MathF.Min(best, Vector3.Distance(xs[i], ClosestOnTriangle(xs[i], y.A, y.B, y.C)));
            best = MathF.Min(best, Vector3.Distance(ys[i], ClosestOnTriangle(ys[i], x.A, x.B, x.C)));
            for (int j = 0; j < 3; j++)
                best = MathF.Min(best, SegmentDistance(xs[i], xs[(i + 1) % 3], ys[j], ys[(j + 1) % 3]));
        }
        return best;
    }

    /// <summary>A window pane of a building and its local axes: outward normal, sideways and up.</summary>
    private readonly record struct Pane(int Part, Vector3 Center, Vector3 Normal, Vector3 Side, float HalfWidth, float HalfHeight, float Back, float Front);

    /// <summary>The window panes (the parts of material Window) of object <paramref name="name"/>.</summary>
    private static List<Pane> Panes(List<Tri> tris)
    {
        var panes = new List<Pane>();
        foreach (var part in tris.Where(t => t.Material == "Window").Select(t => t.Part).Distinct())
        {
            var own = tris.Where(t => t.Part == part).ToList();
            Assert.All(own, t => Assert.Equal("Window", t.Material));
            var points = own.SelectMany(t => new[] { t.A, t.B, t.C }).ToList();
            var center = points.Aggregate(Vector3.Zero, (s, p) => s + p) / points.Count;
            var outward = new Vector3(center.X, 0f, center.Z);
            // The pane faces where its biggest flat face faces, away from the building's middle.
            var normal = own.MaxBy(t => t.Normal.LengthSquared()).Normal;
            normal = Vector3.Normalize(Vector3.Dot(normal, outward) < 0f ? -normal : normal);
            var side = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, normal));
            float Along(Vector3 p, Vector3 axis) => Vector3.Dot(p - center, axis);
            panes.Add(new Pane(part, center, normal, side,
                points.Max(p => MathF.Abs(Along(p, side))), points.Max(p => MathF.Abs(p.Y - center.Y)),
                points.Min(p => Along(p, normal)), points.Max(p => Along(p, normal))));
        }
        return panes;
    }

    private static readonly string[] WindowedBuildings = { "PlayerHouse", "MillieHouse", "Clinic", "House0", "House1", "House2", "House3", "ClothingShop" };

    [Fact]
    public void EveryWindowPaneIsAThinPaneClearlyInFrontOfItsWallWithFrameAndBarsAStepInFrontOfIt()
    {
        int round = 0;
        foreach (string name in WindowedBuildings)
        {
            var tris = ModelTriangles(name);
            var panes = Panes(tris);
            Assert.NotEmpty(panes);
            foreach (var pane in panes)
            {
                string where = $"{name} pane at {pane.Center}";
                // A thin pane (a disc, or the shop's big display pane).
                Assert.True(pane.Front - pane.Back <= 0.05f, $"{where} is {pane.Front - pane.Back} m thick");
                var others = tris.Where(t => t.Part != pane.Part).ToList();

                // Behind it, the wall (or the shop's back wall): nothing of it within the clearance
                // of the pane's back, and the pane wholly outside it (the wall is not crossed in front).
                float behind = float.MaxValue;
                int wall = -1;
                var from = pane.Center + pane.Normal * pane.Back;
                foreach (var t in others)
                {
                    float k = SegmentHit(from, from - pane.Normal, t);
                    if (k >= 0f && k < behind)
                    {
                        behind = k;
                        wall = t.Part;
                    }
                }
                Assert.True(wall >= 0, $"{where} has no wall behind it");
                Assert.True(behind >= WindowClearance, $"{where}: its wall is {behind} m behind its back");
                foreach (var t in tris.Where(t => t.Part == wall))
                    Assert.False(SegmentHitsTriangle(pane.Center + pane.Normal * pane.Back, pane.Center + pane.Normal * 1f, t), $"{where} sinks into its wall");

                // The frame and the crossing bars (the parts centered on the pane's axis, in front of
                // it): each wholly a clear step in front of the pane.
                var onAxis = others.GroupBy(t => t.Part).Where(g =>
                {
                    var pts = g.SelectMany(t => new[] { t.A, t.B, t.C }).ToList();
                    var c = (pts.Aggregate(Vector3.Zero, (s, p) => s + p) / pts.Count) - pane.Center;
                    return MathF.Abs(Vector3.Dot(c, pane.Side)) < 0.05f && MathF.Abs(c.Y) < 0.05f
                        && Vector3.Dot(c, pane.Normal) > pane.Front && Vector3.Dot(c, pane.Normal) < 0.8f;
                }).ToList();
                Assert.NotEmpty(onAxis);
                foreach (var g in onAxis)
                {
                    float back = g.SelectMany(t => new[] { t.A, t.B, t.C }).Min(p => Vector3.Dot(p - pane.Center, pane.Normal));
                    Assert.True(back >= pane.Front + WindowClearance - 0.001f,
                        $"{where}: its {g.First().Material} is only {back - pane.Front} m in front of the pane");
                }
                bool isRound = MathF.Abs(pane.HalfWidth - pane.HalfHeight) < 0.02f;
                if (isRound)
                {
                    round++;
                    // Round windows: the bars (WindowBar, one crossing piece) and the painted frame.
                    Assert.Equal(2, onAxis.Count);
                    Assert.Contains(onAxis, g => g.All(t => t.Material == "WindowBar"));
                    Assert.Contains(onAxis, g => g.All(t => t.Material != "WindowBar" && t.Material != "Window"));
                }
            }
        }
        // Every house's round windows and Abby's round window (the big display pane is not round).
        Assert.Equal(6 + 5 + 6 + 4 * 5 + 1, round);
    }

    [Fact]
    public void NoTwoNearlyParallelFacesOnAndRoundTheWindowsAreCloserThanTheClearance()
    {
        // The parts of each window (those wholly round it: the pane, its bars and frame, the window
        // box and its flowers below, the shop window's outfits) against each other and everything
        // else there (the wall behind, the shop front with its shelf and back wall, fungi on the
        // stem nearby): faces of different parts that are nearly parallel (within 25°) never come
        // closer than the clearance (crossing faces at a steep angle do not fight).
        float parallel = MathF.Cos(25f * MathF.PI / 180f);
        var problems = new List<string>();
        foreach (string name in WindowedBuildings)
        {
            var tris = ModelTriangles(name);
            foreach (var pane in Panes(tris))
            {
                bool Inside(Vector3 p)
                {
                    var d = p - pane.Center;
                    float side = Vector3.Dot(d, pane.Side), depth = Vector3.Dot(d, pane.Normal);
                    return MathF.Abs(side) <= pane.HalfWidth + 0.35f && d.Y <= pane.HalfHeight + 0.35f
                        && d.Y >= -pane.HalfHeight - 0.7f && depth >= -0.35f && depth <= 0.7f;
                }
                var near = tris.Where(t => Inside(t.A) || Inside(t.B) || Inside(t.C)).ToList();
                var outside = tris.Where(t => !(Inside(t.A) && Inside(t.B) && Inside(t.C))).Select(t => t.Part).ToHashSet();
                var bounds = near.Select(t => (Min: Vector3.Min(t.A, Vector3.Min(t.B, t.C)), Max: Vector3.Max(t.A, Vector3.Max(t.B, t.C)))).ToList();
                var units = near.Select(t => Vector3.Normalize(t.Normal)).ToList();
                for (int i = 0; i < near.Count; i++)
                {
                    if (near[i].Normal.LengthSquared() < 1e-14f) continue;
                    for (int j = i + 1; j < near.Count; j++)
                    {
                        if (near[i].Part == near[j].Part || near[j].Normal.LengthSquared() < 1e-14f) continue;
                        if (outside.Contains(near[i].Part) && outside.Contains(near[j].Part)) continue;
                        if (MathF.Abs(Vector3.Dot(units[i], units[j])) < parallel) continue;
                        var gap = Vector3.Max(bounds[i].Min - bounds[j].Max, bounds[j].Min - bounds[i].Max);
                        if (gap.X >= WindowClearance || gap.Y >= WindowClearance || gap.Z >= WindowClearance) continue;
                        float d = TriangleDistance(near[i], near[j]);
                        if (d < WindowClearance)
                            problems.Add($"{name}: {near[i].Material} and {near[j].Material} {d:0.000} m apart at {near[i].Center}");
                    }
                }
            }
        }
        Assert.True(problems.Count == 0, $"{problems.Count} close faces, e.g.\n" + string.Join("\n", problems.Distinct().Take(20)));
    }

    [Fact]
    public void ThePanesAndBarsHaveNoInkOutlineButTheFramesKeepTheirs()
    {
        // Their outline (an inverted hull) would overlap the frame's own right where they meet it.
        Assert.False(TownLook.Outlined("Window"));
        Assert.False(TownLook.Outlined("WindowBar"));
        foreach (string frame in new[] { "DoorRed", "DoorBlue", "DoorGreen", "Wood" })
            Assert.True(TownLook.Outlined(frame), frame);
        // The bars are painted like the old wooden bars.
        var colors = ModelColors();
        Assert.Equal(colors["Wood"], colors["WindowBar"]);
    }

    [Fact]
    public void TickBeforeStartThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new TownModule().Tick(Step));
        Assert.Throws<ArgumentNullException>(() => new TownModule().Start(null!));
    }
}
