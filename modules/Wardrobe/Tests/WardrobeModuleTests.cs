using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Linq;
using System.Text.RegularExpressions;
using Fungtopia.Input;
using Fungtopia.Map;
using Fungtopia.Monsters;
using Fungtopia.Player;
using Fungtopia.Save;
using Fungtopia.Shell;
using Fungtopia.Wallet;
using Xunit;

namespace Fungtopia.Wardrobe.Tests;

public class CatalogTests
{
    /// <summary>The repository root, found from the test assembly's folder.</summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static string FileOf(string resPath)
    {
        Assert.StartsWith("res://", resPath);
        return Path.Combine(RepoRoot(), resPath.Substring("res://".Length));
    }

    [Fact]
    public void IdsAreUniqueAndItemsWellFormed()
    {
        var ids = new HashSet<string>();
        foreach (var item in Catalog.Items)
        {
            Assert.Matches("^[a-z][a-z0-9_]*$", item.Id);
            Assert.True(ids.Add(item.Id), $"duplicate id {item.Id}");
            Assert.False(string.IsNullOrWhiteSpace(item.Name));
            Assert.True(Enum.IsDefined(item.Slot), $"{item.Id}: slot {item.Slot}");
            Assert.InRange(item.Fit, Fits.Both, Fits.Boy);
            Assert.Contains(item.Source, new[] { Sources.Starter, Sources.Shop, Sources.Reward });
            if (item.Source == Sources.Shop)
                Assert.InRange(item.Price, 100, 600);
            else
                Assert.Equal(0, item.Price);
            if (item.Dress)
                Assert.Equal(Slot.Top, item.Slot);
            Assert.Equal(item.Id, Catalog.Items[Catalog.IndexOf(item.Id)].Id);
        }
        Assert.Equal(Catalog.Count, ids.Count);
        Assert.Equal(-1, Catalog.IndexOf("no_such_item"));
        Assert.Equal(-1, Catalog.IndexOf(""));
    }

    [Fact]
    public void EveryModelFileAndItsScriptExist()
    {
        foreach (var item in Catalog.Items)
        {
            string glb = FileOf(item.ModelPath);
            Assert.EndsWith(item.Id + ".glb", glb);
            Assert.True(File.Exists(glb), $"missing model {glb}");
            Assert.True(new FileInfo(glb).Length > 1000, $"empty model {glb}");
            Assert.True(File.Exists(Path.ChangeExtension(glb, ".py")), $"missing model script for {item.Id}");
        }
        foreach (var hair in Looks.HairStyles)
        {
            string glb = FileOf(hair.ModelPath);
            Assert.True(File.Exists(glb), $"missing hair model {glb}");
            Assert.True(File.Exists(Path.ChangeExtension(glb, ".py")), $"missing hair script for {hair.Id}");
        }
    }

    [Fact]
    public void TheStarterCatalogHasEveryKindOfPiece()
    {
        int Count(Slot slot, bool dress = false)
        {
            int n = 0;
            foreach (var item in Catalog.Items)
                if (item.Slot == slot && item.Dress == dress && item.Source != Sources.Reward)
                    n++;
            return n;
        }
        Assert.Equal(4, Count(Slot.Top));
        Assert.Equal(3, Count(Slot.Top, dress: true));
        Assert.Equal(4, Count(Slot.Bottom));
        Assert.Equal(3, Count(Slot.Shoes));
        Assert.Equal(4, Count(Slot.Head));
        Assert.Equal(2, Count(Slot.Face));
        Assert.Equal(2, Count(Slot.Back));
        foreach (var item in Catalog.Items)
            if (item.Dress)
                Assert.NotEqual(Fits.Boy, item.Fit);
        Assert.True(Catalog.TryGet(Catalog.SpiderSilkScarf, out var scarf));
        Assert.Equal(Sources.Reward, scarf.Source);
        Assert.True(Catalog.TryGet(Catalog.LadybugBackpack, out var backpack));
        Assert.Equal(Sources.Reward, backpack.Source);
        Assert.Equal(Slot.Back, backpack.Slot);
    }

    [Fact]
    public void StarterOutfitsAreThreeOutfitsOfStarterItemsInTheirSlots()
    {
        Assert.Equal(3, Catalog.StarterOutfits.Length);
        foreach (var outfit in Catalog.StarterOutfits)
        {
            void Check(string id, Slot slot, bool required)
            {
                if (string.IsNullOrEmpty(id))
                {
                    Assert.False(required, $"{outfit.Name} has no {slot}");
                    return;
                }
                Assert.True(Catalog.TryGet(id, out var item), id);
                Assert.Equal(slot, item.Slot);
                Assert.Equal(Sources.Starter, item.Source);
            }
            Check(outfit.Top, Slot.Top, true);
            Check(outfit.Bottom, Slot.Bottom, !Catalog.Items[Catalog.IndexOf(outfit.Top)].Dress);
            Check(outfit.Shoes, Slot.Shoes, true);
            Check(outfit.Head, Slot.Head, false);
        }
        // Both characters have an outfit to start in.
        Assert.True(Fits.Matches(Catalog.StarterOutfits[Catalog.DefaultOutfit(0)].Fit, 0));
        Assert.True(Fits.Matches(Catalog.StarterOutfits[Catalog.DefaultOutfit(1)].Fit, 1));
    }

    [Fact]
    public void LooksHaveTheDesignedChoices()
    {
        Assert.Equal(6, Looks.SkinTones.Length);
        Assert.Equal(6, Looks.HairStyles.Length);
        Assert.Equal(8, Looks.HairColors.Length);
        Assert.Equal(6, Looks.EyeColors.Length);
        Assert.Equal(Looks.HairColors.Length, Looks.HairColorNames.Length);
        Assert.Equal(Looks.EyeColors.Length, Looks.EyeColorNames.Length);
        Assert.Contains("Pink", Looks.HairColorNames.ToArray());
        Assert.Contains("Mint", Looks.HairColorNames.ToArray());
        var styles = new List<string>();
        foreach (var style in Looks.HairStyles)
            styles.Add(style.Id);
        Assert.Equal(new[] { "short", "bob", "pigtails", "ponytail", "top_bun", "curly" }, styles);
        // Light to deep.
        for (int i = 1; i < Looks.SkinTones.Length; i++)
        {
            var a = Looks.SkinTones[i - 1];
            var b = Looks.SkinTones[i];
            Assert.True(a.X + a.Y + a.Z > b.X + b.Y + b.Z, $"skin tone {i} is not deeper");
        }
        foreach (var span in new[] { Looks.SkinTones.ToArray(), Looks.HairColors.ToArray(), Looks.EyeColors.ToArray() })
            foreach (var c in span)
                Assert.True(c.X is >= 0 and <= 1 && c.Y is >= 0 and <= 1 && c.Z is >= 0 and <= 1);
    }
}

/// <summary>
/// The clothes and hair styles fit the Player's simple-shapes rig (modules/Player/Models/boy.glb
/// and girl.glb, read back): a round head floating over a smooth cone body, round hands floating
/// beside it, little round feet under it, no arms or legs. Godot's frame: Y up, -Z the front.
/// </summary>
public class ModelFitTests
{
    private static readonly string[] Parts = { Attach.Head, Attach.Body, Attach.LegL, Attach.LegR };

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static Glb Model(string resPath) => new(Path.Combine(RepoRoot(), resPath.Substring("res://".Length)));

    private static readonly Glb s_rig = new(Path.Combine(RepoRoot(), "modules", "Player", "Models", "boy.glb"));

    /// <summary>The body cone's skin: its radius at each height (rounding included), bottom and top.</summary>
    private static class Cone
    {
        public static readonly (float Y, float R)[] Rings = s_rig.Vertices("Body", "Skin")
            .GroupBy(v => MathF.Round(v.Y, 4)).Select(g => (g.Key, g.Max(Radius))).OrderBy(r => r.Key).ToArray();
        public static readonly float Bottom = Rings[0].Y;
        public static readonly float Top = Rings[^1].Y;
        public static readonly float HeadBottom = s_rig.VerticesUnder("Head").Min(v => v.Y);

        public static float R(float y)
        {
            if (y <= Bottom)
                return Rings[0].R;
            for (int i = 1; i < Rings.Length; i++)
                if (y <= Rings[i].Y)
                {
                    var (y0, r0) = Rings[i - 1];
                    var (y1, r1) = Rings[i];
                    return r0 + (r1 - r0) * (y - y0) / (y1 - y0);
                }
            return 0f;
        }

        /// <summary>How far the point is from the body's outline (side, rim and top) in its half-plane through the axis.</summary>
        public static float Distance(Vector3 v)
        {
            var p = new Vector2(Radius(v), v.Y);
            float best = float.MaxValue;
            for (int i = 0; i < Rings.Length; i++)
            {
                var a = new Vector2(Rings[i].R, Rings[i].Y);
                var b = i + 1 < Rings.Length ? new Vector2(Rings[i + 1].R, Rings[i + 1].Y) : new Vector2(0f, Top);
                var ab = b - a;
                float t = ab.LengthSquared() < 1e-12f ? 0f : Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0f, 1f);
                best = MathF.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        /// <summary>How far the point is out from the body's side (negative: inside the body).</summary>
        public static float Gap(Vector3 v) => v.Y < Bottom || v.Y > Top ? float.PositiveInfinity : Radius(v) - R(v.Y);
    }

    private static float Radius(Vector3 v) => MathF.Sqrt(v.X * v.X + v.Z * v.Z);

    private static IEnumerable<ClothingItem> On(params Slot[] slots) => Catalog.Items.ToArray().Where(i => slots.Contains(i.Slot));

    /// <summary>Whether vertices close round the body (within <paramref name="near"/> of its side) cover every heading between the heights.</summary>
    private static bool AllRound(List<Vector3> v, float y0, float y1, float near = 0.04f)
    {
        var headings = new HashSet<int>();
        foreach (var p in v)
            if (p.Y >= y0 && p.Y <= y1 && Radius(p) - Cone.R(p.Y) < near)
                headings.Add((int)MathF.Floor((MathF.Atan2(p.X, -p.Z) + MathF.PI) / (2 * MathF.PI) * 24) % 24);
        return headings.Count == 24;
    }

    [Fact]
    public void TheRigStillHasTheConeBodyThePartsAndThePivotsAttachNames()
    {
        Assert.InRange(Cone.Bottom, 0.1f, 0.16f);
        Assert.InRange(Cone.Top, 0.58f, 0.64f);
        Assert.True(Cone.R(Cone.Bottom + 0.05f) > 2.5f * Cone.R(Cone.Top - 0.1f), "a cone, wide at the bottom");
        foreach (var file in new[] { "boy.glb", "girl.glb" })
        {
            var rig = new Glb(Path.Combine(RepoRoot(), "modules", "Player", "Models", file));
            Assert.True(Vector3.Distance(Attach.HeadPivot, rig.Origin(Attach.Head)) < 0.001f, file);
            Assert.True(Vector3.Distance(Attach.BodyPivot, rig.Origin(Attach.Body)) < 0.001f, file);
            Assert.True(Vector3.Distance(Attach.LegLPivot, rig.Origin(Attach.LegL)) < 0.001f, file);
            Assert.True(Vector3.Distance(Attach.LegRPivot, rig.Origin(Attach.LegR)) < 0.001f, file);
        }
    }

    [Fact]
    public void EveryModelIsMadeOfRigPartsWithTheirOriginsAtThePartsPivots()
    {
        var pivots = new Dictionary<string, Vector3>
        {
            [Attach.Head] = Attach.HeadPivot, [Attach.Body] = Attach.BodyPivot,
            [Attach.LegL] = Attach.LegLPivot, [Attach.LegR] = Attach.LegRPivot,
        };
        string[] Expected(ClothingItem item) => item.Slot switch
        {
            Slot.Top or Slot.Bottom or Slot.Back => new[] { Attach.Body },
            Slot.Shoes => new[] { Attach.LegL, Attach.LegR },
            Slot.Head => new[] { Attach.Head },
            _ => item.Id == Catalog.SpiderSilkScarf ? new[] { Attach.Body } : new[] { Attach.Head },
        };
        var models = Catalog.Items.ToArray().Select(i => (i.ModelPath, Expected(i)))
            .Concat(Looks.HairStyles.ToArray().Select(h => (h.ModelPath, new[] { Attach.Head })));
        foreach (var (path, parts) in models)
        {
            var glb = Model(path);
            Assert.Equal(parts.OrderBy(p => p, StringComparer.Ordinal),
                glb.Nodes.Select(n => n.Name).OrderBy(p => p, StringComparer.Ordinal));
            foreach (var part in parts)
            {
                Assert.True(Vector3.Distance(pivots[part], glb.Origin(part)) < 0.001f, $"{path}: {part}");
                Assert.True(glb.Vertices(part).Count > 50, $"{path}: {part} is empty");
            }
        }
    }

    [Fact]
    public void NothingOnTheBodyPokesThroughItReachesTheHeadOrTheHandsOrFloatsLoose()
    {
        var hands = new[] { "HandL", "HandR" }.Select(h =>
        {
            var c = s_rig.Origin(h);
            return (Center: c, Radius: s_rig.Vertices(h).Max(v => Vector3.Distance(v, c)));
        }).ToList();
        foreach (var item in Catalog.Items)
        {
            var glb = Model(item.ModelPath);
            if (!glb.Has(Attach.Body))
                continue;
            var v = glb.Vertices(Attach.Body);
            var inside = v.Where(p => Cone.Gap(p) < -0.002f).ToList();
            Assert.True(inside.Count == 0, $"{item.Id} pokes into the body at {(inside.Count > 0 ? inside[0] : default)}");
            Assert.True(v.Max(p => p.Y) < Cone.HeadBottom - 0.03f, $"{item.Id} reaches up to the head");
            foreach (var (center, radius) in hands)
                Assert.True(v.Min(p => Vector3.Distance(p, center)) > radius + 0.02f, $"{item.Id} reaches the floating hands");
            // It lies on the body: its inside touches the side.
            Assert.True(v.Min(Cone.Gap) < 0.025f, $"{item.Id} floats off the body");
        }
    }

    [Fact]
    public void TopsAndDressesCoverTheTriangleWithoutSleeves()
    {
        float h = Cone.Top - Cone.Bottom;
        foreach (var item in On(Slot.Top))
        {
            var v = Model(item.ModelPath).Vertices(Attach.Body);
            Assert.True(v.Max(p => p.Y) >= Cone.Top, $"{item.Id} leaves the top of the triangle bare");
            Assert.True(AllRound(v, Cone.Bottom + h * 0.45f, Cone.Bottom + h * 0.7f), $"{item.Id} does not go all round");
            // No sleeves (no arms): nothing sticks out sideways from the cone.
            Assert.True(v.All(p => Cone.Distance(p) < 0.09f), $"{item.Id} sticks out from the body (sleeves?)");
            if (item.Dress)
            {
                Assert.True(v.Min(p => p.Y) <= Cone.Bottom, $"{item.Id}: a dress goes down to the bottom edge");
                Assert.True(AllRound(v, Cone.Bottom + 0.01f, Cone.Bottom + h * 0.15f), $"{item.Id} does not go all round at the bottom");
            }
            else
            {
                // Down into the lower band, under every bottom's top edge, so no skin shows between them.
                float hem = v.Min(p => p.Y);
                Assert.True(hem > Cone.Bottom + h * 0.1f, $"{item.Id} is as long as a dress");
                foreach (var bottom in On(Slot.Bottom))
                    Assert.True(Model(bottom.ModelPath).Vertices(Attach.Body).Max(p => p.Y) > hem + 0.02f, $"{item.Id} over {bottom.Id}");
            }
        }
    }

    [Fact]
    public void BottomsAreTheLowerBandOfTheTriangleAndShortsAreShorter()
    {
        float h = Cone.Top - Cone.Bottom;
        foreach (var item in On(Slot.Bottom))
        {
            var v = Model(item.ModelPath).Vertices(Attach.Body);
            float top = v.Max(p => p.Y), low = v.Min(p => p.Y);
            Assert.InRange(top, Cone.Bottom + h * 0.2f, Cone.Bottom + h * 0.4f);
            Assert.True(AllRound(v, top - 0.06f, top - 0.02f, 0.05f), $"{item.Id} does not go all round");
            Assert.True(v.All(p => Cone.Distance(p) < 0.1f), $"{item.Id} sticks out from the body (trouser legs?)");
            if (item.Id == "shorts")
                Assert.True(low > Cone.Bottom + 0.04f, "shorts are shorter: they end above the bottom edge");
            else
                Assert.True(low <= Cone.Bottom, $"{item.Id} goes down to the bottom edge");
        }
    }

    [Fact]
    public void ShoesFitTheRoundFeetUnderTheBody()
    {
        foreach (var item in On(Slot.Shoes))
        {
            var glb = Model(item.ModelPath);
            foreach (var leg in new[] { Attach.LegL, Attach.LegR })
            {
                var v = glb.Vertices(leg);
                var foot = s_rig.Vertices("DefaultShoes" + leg);
                Vector3 low = v.Aggregate(Vector3.Min), high = v.Aggregate(Vector3.Max);
                Vector3 footLow = foot.Aggregate(Vector3.Min), footHigh = foot.Aggregate(Vector3.Max);
                Assert.InRange(low.Y, -0.01f, 0.01f);
                Assert.True(high.Y < Cone.Bottom, $"{item.Id} reaches into the body");
                Assert.True(high.Y >= footHigh.Y - 0.005f, $"{item.Id} is lower than the foot");
                // Over the foot: as wide and long as it (a little more), and not much more.
                Assert.True(low.X <= footLow.X + 0.002f && high.X >= footHigh.X - 0.002f, $"{item.Id}: {leg} across");
                Assert.True(low.Z <= footLow.Z + 0.002f && high.Z >= footHigh.Z - 0.002f, $"{item.Id}: {leg} along");
                Assert.True(high.X - low.X < (footHigh.X - footLow.X) + 0.06f, $"{item.Id}: {leg} too wide");
                Assert.True(high.Z - low.Z < (footHigh.Z - footLow.Z) + 0.08f, $"{item.Id}: {leg} too long");
            }
        }
    }

    [Fact]
    public void HatsGlassesClipsAndHairSitOnTheHeadAboveTheBody()
    {
        var head = s_rig.Vertices(Attach.Head, "Skin");
        var center = (head.Aggregate(Vector3.Min) + head.Aggregate(Vector3.Max)) / 2;
        var models = Catalog.Items.ToArray().Select(i => i.ModelPath).Concat(Looks.HairStyles.ToArray().Select(h => h.ModelPath));
        foreach (var path in models)
        {
            var glb = Model(path);
            if (!glb.Has(Attach.Head))
                continue;
            var v = glb.Vertices(Attach.Head);
            Assert.True(v.Min(p => p.Y) > Cone.Top + 0.03f, $"{path} hangs down onto the body");
            Assert.True(v.Max(p => Vector3.Distance(p, center)) < 0.85f, $"{path} floats away from the head");
            Assert.True(v.Min(p => Vector3.Distance(p, center)) < 0.45f, $"{path} does not touch the head");
        }
    }

    [Fact]
    public void BackpacksSitOnTheTrianglesBackAndTheScarfRoundItsTop()
    {
        foreach (var item in On(Slot.Back))
        {
            var v = Model(item.ModelPath).Vertices(Attach.Body);
            // The bag behind the body (Godot's +Z), most of it below the top, straps over the top.
            var bag = v.Where(p => p.Z > Cone.R(p.Y) + 0.03f).ToList();
            Assert.True(bag.Count > v.Count / 3, $"{item.Id}: the bag is not on the back");
            Assert.True(bag.Average(p => p.Y) > Cone.Bottom + 0.08f && bag.Average(p => p.Y) < Cone.Top - 0.1f, $"{item.Id}: the bag is not on the back");
            Assert.True(bag.Max(p => p.Z) < Cone.R(Cone.Bottom) + 0.25f, $"{item.Id} sticks out far behind");
            Assert.Contains(v, p => p.Z < -0.05f && Cone.Gap(p) < 0.05f);
            Assert.Contains(v, p => p.Y > Cone.Top - 0.1f);
        }
        var scarf = Model(Catalog.Items[Catalog.IndexOf(Catalog.SpiderSilkScarf)].ModelPath).Vertices(Attach.Body);
        Assert.True(AllRound(scarf, Cone.Top - 0.15f, Cone.Top - 0.03f, 0.1f), "the scarf goes round the top");
        Assert.True(scarf.Min(p => p.Y) > Cone.Bottom + (Cone.Top - Cone.Bottom) * 0.35f, "the scarf's tails hang down the front a little");
    }

    /// <summary>
    /// A .glb read back: each node's name, parent, mesh and world transform (glTF's frame, Godot's),
    /// and its mesh's vertices by material.
    /// </summary>
    private sealed class Glb
    {
        public readonly List<(string Name, int Parent, int Mesh, Matrix4x4 World)> Nodes = new();
        private readonly Dictionary<string, int> _index = new();
        private readonly System.Text.Json.JsonElement _json;
        private readonly byte[] _bin;

        public Glb(string file)
        {
            Assert.True(File.Exists(file), file);
            var bytes = File.ReadAllBytes(file);
            int jsonLength = BitConverter.ToInt32(bytes, 12);
            _json = System.Text.Json.JsonDocument.Parse(new ReadOnlyMemory<byte>(bytes, 20, jsonLength)).RootElement;
            int binStart = 20 + jsonLength;
            _bin = bytes[(binStart + 8)..(binStart + 8 + BitConverter.ToInt32(bytes, binStart))];
            var nodes = _json.GetProperty("nodes");
            var parents = Enumerable.Repeat(-1, nodes.GetArrayLength()).ToArray();
            for (int i = 0; i < parents.Length; i++)
                if (nodes[i].TryGetProperty("children", out var children))
                    foreach (var child in children.EnumerateArray())
                        parents[child.GetInt32()] = i;
            var world = new Matrix4x4?[parents.Length];
            Matrix4x4 World(int i) => world[i] ??= Local(nodes[i]) * (parents[i] < 0 ? Matrix4x4.Identity : World(parents[i]));
            for (int i = 0; i < parents.Length; i++)
            {
                string name = nodes[i].GetProperty("name").GetString()!;
                _index[name] = i;
                Nodes.Add((name, parents[i], nodes[i].TryGetProperty("mesh", out var mesh) ? mesh.GetInt32() : -1, World(i)));
            }
        }

        private static Matrix4x4 Local(System.Text.Json.JsonElement node)
        {
            float[] Floats(string key, params float[] fallback) =>
                node.TryGetProperty(key, out var v) ? v.EnumerateArray().Select(x => x.GetSingle()).ToArray() : fallback;
            var t = Floats("translation", 0, 0, 0);
            var r = Floats("rotation", 0, 0, 0, 1);
            var s = Floats("scale", 1, 1, 1);
            return Matrix4x4.CreateScale(s[0], s[1], s[2]) * Matrix4x4.CreateFromQuaternion(new Quaternion(r[0], r[1], r[2], r[3])) *
                Matrix4x4.CreateTranslation(t[0], t[1], t[2]);
        }

        public bool Has(string name) => _index.ContainsKey(name);

        public Vector3 Origin(string name)
        {
            Assert.True(Has(name), $"no node {name}");
            return Nodes[_index[name]].World.Translation;
        }

        /// <summary>The world positions of the node's own mesh's vertices (only those in <paramref name="material"/> when given).</summary>
        public List<Vector3> Vertices(string name, string? material = null)
        {
            Assert.True(Has(name), $"no node {name}");
            var result = new List<Vector3>();
            var node = Nodes[_index[name]];
            if (node.Mesh < 0)
                return result;
            foreach (var primitive in _json.GetProperty("meshes")[node.Mesh].GetProperty("primitives").EnumerateArray())
            {
                string mat = _json.GetProperty("materials")[primitive.GetProperty("material").GetInt32()].GetProperty("name").GetString()!;
                if (material != null && mat != material)
                    continue;
                var accessor = _json.GetProperty("accessors")[primitive.GetProperty("attributes").GetProperty("POSITION").GetInt32()];
                var view = _json.GetProperty("bufferViews")[accessor.GetProperty("bufferView").GetInt32()];
                int start = (view.TryGetProperty("byteOffset", out var vo) ? vo.GetInt32() : 0) +
                    (accessor.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0);
                int stride = view.TryGetProperty("byteStride", out var st) ? st.GetInt32() : 12;
                for (int i = 0; i < accessor.GetProperty("count").GetInt32(); i++)
                {
                    int at = start + i * stride;
                    var local = new Vector3(BitConverter.ToSingle(_bin, at), BitConverter.ToSingle(_bin, at + 4), BitConverter.ToSingle(_bin, at + 8));
                    result.Add(Vector3.Transform(local, node.World));
                }
            }
            return result;
        }

        /// <summary>The vertices of the node and every node under it.</summary>
        public List<Vector3> VerticesUnder(string name)
        {
            var result = Vertices(name);
            for (int i = 0; i < Nodes.Count; i++)
                if (Nodes[i].Parent == _index[name])
                    result.AddRange(VerticesUnder(Nodes[i].Name));
            return result;
        }
    }
}

public class WardrobeModuleTests
{
    /// <summary>
    /// Fake of the shell's bus: delivers messages like the real one and records what the
    /// wardrobe publishes. With <see cref="Coins"/> set it also plays the wallet, answering every
    /// SpendCoins with a SpendResult right away.
    /// </summary>
    private sealed class FakeBus : IMessageBus
    {
        private readonly Dictionary<Type, List<object>> _handlers = new();
        public readonly List<AppearanceState> Appearances = new();
        public readonly List<WardrobeState> States = new();
        public readonly List<SpendCoins> Spends = new();
        public readonly List<ItemReceived> Received = new();
        public readonly List<PreviewYaw> Yaws = new();
        public readonly List<SavePart> Parts = new();
        public readonly List<object> All = new();
        public int? Coins;

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
                _handlers[typeof(T)] = list = new List<object>();
            list.Add(handler);
        }

        public void Publish<T>(in T message) where T : struct
        {
            All.Add(message);
            if (message is SavePart part)
                Parts.Add(part);
            if (message is AppearanceState appearance)
                Appearances.Add(appearance);
            if (message is WardrobeState state)
                States.Add(state);
            if (message is PreviewYaw yaw)
                Yaws.Add(yaw);
            if (message is ItemReceived received)
                Received.Add(received);
            if (message is SpendCoins spend)
            {
                Spends.Add(spend);
                if (Coins is int coins)
                {
                    bool ok = spend.Amount >= 0 && spend.Amount <= coins;
                    if (ok)
                        Coins = coins - spend.Amount;
                    Publish(new SpendResult(spend.RequestId, ok, Coins.Value));
                }
            }
            if (_handlers.TryGetValue(typeof(T), out var list))
                foreach (var handler in list)
                    ((IMessageHandler<T>)handler).Handle(message);
        }
    }

    private static readonly TickContext Step = new(0, 1.0 / 60);

    private static WardrobeModule Started(FakeBus bus, CharacterKind? character = null)
    {
        var wardrobe = new WardrobeModule();
        wardrobe.Start(bus);
        if (character is CharacterKind kind)
            bus.Publish(Player(kind));
        return wardrobe;
    }

    [Fact]
    public void DeviceFollowsActiveDeviceAndTheViewsNotes()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus);
        Assert.Equal(DeviceKind.KeyboardMouse, wardrobe.Device);
        bus.Publish(new ActiveDevice(DeviceKind.PlayStation));
        Assert.Equal(DeviceKind.PlayStation, wardrobe.Device);

        // While a screen is open the view takes the input: its note wins over the Input module's
        // unchanged device, published again every tick.
        bus.Publish(new OpenWardrobe());
        wardrobe.NoteDevice(DeviceKind.KeyboardMouse);
        bus.Publish(new ActiveDevice(DeviceKind.PlayStation));
        wardrobe.Tick(Step);
        Assert.Equal(DeviceKind.KeyboardMouse, wardrobe.Device);
        wardrobe.NoteDevice(DeviceKind.Xbox);
        Assert.Equal(DeviceKind.Xbox, wardrobe.Device);

        // A new device from the Input module is followed at once.
        bus.Publish(new ActiveDevice(DeviceKind.Nintendo));
        Assert.Equal(DeviceKind.Nintendo, wardrobe.Device);

        // Opening a screen shows the Input module's device (the press that opened it went through it).
        wardrobe.Close();
        wardrobe.NoteDevice(DeviceKind.KeyboardMouse);
        bus.Publish(new OpenShop());
        Assert.Equal(DeviceKind.Nintendo, wardrobe.Device);
    }

    private static PlayerState Player(CharacterKind kind) =>
        new(Vector3.Zero, 0f, Vector3.Zero, true, 10, 10, true, kind, Area.Home);

    private static MonsterKilled Kill(int id, MonsterKind kind) =>
        new(id, kind, MonsterRewards.For(kind), Vector3.Zero, 0);

    [Fact]
    public void StartersAreOwnedAndNothingElse()
    {
        var wardrobe = Started(new FakeBus());
        for (int i = 0; i < Catalog.Count; i++)
            Assert.Equal(Catalog.Items[i].Source == Sources.Starter, wardrobe.Owns(i));
        // Every starter outfit can be put on.
        foreach (var outfit in Catalog.StarterOutfits)
            foreach (var id in new[] { outfit.Top, outfit.Bottom, outfit.Shoes, outfit.Head })
                if (id != "")
                    Assert.True(wardrobe.Owns(id), id);
    }

    [Fact]
    public void PublishesAppearanceAndWardrobeStateEveryTick()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Boy);
        for (int i = 0; i < 3; i++)
            wardrobe.Tick(Step);
        Assert.Equal(3, bus.Appearances.Count);
        Assert.Equal(3, bus.States.Count);
        Assert.All(bus.States, s => Assert.False(s.Open));
        // The boy starts in the first starter outfit.
        var look = bus.Appearances[^1];
        Assert.Equal("striped_tee", look.Top);
        Assert.Equal("shorts", look.Bottom);
        Assert.Equal("sneakers", look.Shoes);
        Assert.Equal("", look.Face);
        Assert.Equal("", look.Back);
    }

    [Fact]
    public void TheGirlStartsInTheMushroomDressWithNoBottomShown()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Girl);
        wardrobe.Tick(Step);
        var look = bus.Appearances[^1];
        Assert.Equal("spotted_dress", look.Top);
        Assert.Equal("", look.Bottom);
        Assert.Equal("mary_janes", look.Shoes);
        Assert.Equal("big_bow", look.Head);
    }

    [Fact]
    public void EquipAndUnequip()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Boy);
        bus.Publish(new Equip("rain_boots"));
        bus.Publish(new Equip("beanie"));
        wardrobe.Tick(Step);
        Assert.Equal("rain_boots", bus.Appearances[^1].Shoes);
        Assert.Equal("beanie", bus.Appearances[^1].Head);

        bus.Publish(Equip.Unequip(Slot.Head));
        bus.Publish(new Equip("", Slot.Shoes));
        wardrobe.Tick(Step);
        Assert.Equal("", bus.Appearances[^1].Head);
        Assert.Equal("", bus.Appearances[^1].Shoes);
        Assert.Equal("striped_tee", bus.Appearances[^1].Top);
        Assert.Equal(-1, wardrobe.Worn(Slot.Head));
    }

    [Fact]
    public void ItemsNotOwnedOrNotFittingOrUnknownCannotBePutOn()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Boy);
        bus.Publish(new Equip("mushroom_hat"));      // shop item, not owned
        bus.Publish(new Equip("spotted_dress"));     // owned starter, but a girl's dress
        bus.Publish(new Equip("no_such_item"));
        wardrobe.Tick(Step);
        Assert.Equal("", bus.Appearances[^1].Head);
        Assert.Equal("striped_tee", bus.Appearances[^1].Top);
    }

    [Fact]
    public void ADressHidesTheBottomAndTakingItOffShowsItAgain()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Girl);
        bus.Publish(new Equip("striped_tee"));
        bus.Publish(new Equip("overalls"));
        wardrobe.Tick(Step);
        Assert.Equal("overalls", bus.Appearances[^1].Bottom);

        bus.Publish(new Equip("spotted_dress"));
        wardrobe.Tick(Step);
        Assert.Equal("spotted_dress", bus.Appearances[^1].Top);
        Assert.Equal("", bus.Appearances[^1].Bottom);
        Assert.True(wardrobe.BottomHidden);

        // A bottom put on under the dress stays hidden until the dress comes off.
        bus.Publish(new Equip("shorts"));
        wardrobe.Tick(Step);
        Assert.Equal("", bus.Appearances[^1].Bottom);

        bus.Publish(new Equip("overalls_top"));
        wardrobe.Tick(Step);
        Assert.Equal("overalls_top", bus.Appearances[^1].Top);
        Assert.Equal("shorts", bus.Appearances[^1].Bottom);
        Assert.False(wardrobe.BottomHidden);
    }

    [Fact]
    public void SetLookChangesTheLookOnlyWhenInRange()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Girl);
        bus.Publish(new SetLook(5, 4, 6, 2));
        wardrobe.Tick(Step);
        var look = bus.Appearances[^1];
        Assert.Equal((5, 4, 6, 2), (look.Skin, look.HairStyle, look.HairColor, look.EyeColor));

        foreach (var bad in new[]
                 {
                     new SetLook(6, 0, 0, 0), new SetLook(0, 6, 0, 0), new SetLook(0, 0, 8, 0),
                     new SetLook(0, 0, 0, 6), new SetLook(-1, 0, 0, 0),
                 })
            bus.Publish(bad);
        wardrobe.Tick(Step);
        look = bus.Appearances[^1];
        Assert.Equal((5, 4, 6, 2), (look.Skin, look.HairStyle, look.HairColor, look.EyeColor));
        Assert.True(Looks.Valid(look.Skin, look.HairStyle, look.HairColor, look.EyeColor));
    }

    [Fact]
    public void OpenWardrobeAndShopAndClose()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus);
        bus.Publish(new OpenWardrobe());
        wardrobe.Tick(Step);
        Assert.Equal(new WardrobeState(true, false), bus.States[^1]);
        bus.Publish(new OpenShop());
        wardrobe.Tick(Step);
        Assert.Equal(new WardrobeState(true, true), bus.States[^1]);
        wardrobe.Close();
        wardrobe.Tick(Step);
        Assert.Equal(new WardrobeState(false, false), bus.States[^1]);
    }

    [Fact]
    public void PreviewYawIsPublishedOnlyWhileAScreenIsOpenAndStartsFacingTheCamera()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus);
        wardrobe.SetTurnButtons(false, true);
        wardrobe.Tick(Step);
        Assert.Empty(bus.Yaws);                    // closed: nothing, and no turning

        bus.Publish(new OpenWardrobe());
        wardrobe.SetTurnButtons(false, false);
        wardrobe.Tick(Step);
        wardrobe.Tick(Step);
        Assert.Equal(new[] { 0f, 0f }, bus.Yaws.ConvertAll(y => y.Yaw));
        wardrobe.Close();
        wardrobe.Tick(Step);
        Assert.Equal(2, bus.Yaws.Count);

        bus.Publish(new OpenShop());
        wardrobe.Tick(Step);
        Assert.Equal(3, bus.Yaws.Count);           // the shop turns the character too
        Assert.Equal(0f, bus.Yaws[^1].Yaw);
    }

    [Fact]
    public void TurnButtonsTurnAtTheTurnSpeed()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus);
        bus.Publish(new OpenWardrobe());
        float step = WardrobeModule.TurnRadiansPerSecond / 60f;

        wardrobe.SetTurnButtons(false, true);      // E / RB: the front turns to the right
        for (int i = 0; i < 10; i++)
            wardrobe.Tick(Step);
        Assert.Equal(10 * step, bus.Yaws[^1].Yaw, 4);
        for (int i = 1; i < 10; i++)
            Assert.Equal(step, bus.Yaws[i].Yaw - bus.Yaws[i - 1].Yaw, 4);

        wardrobe.SetTurnButtons(true, false);      // Q / LB: back to the left
        for (int i = 0; i < 15; i++)
            wardrobe.Tick(Step);
        Assert.Equal(-5 * step, bus.Yaws[^1].Yaw, 4);

        wardrobe.SetTurnButtons(true, true);       // both: no turn
        wardrobe.Tick(Step);
        Assert.Equal(-5 * step, bus.Yaws[^1].Yaw, 4);
        Assert.Equal(wardrobe.Yaw, bus.Yaws[^1].Yaw);
    }

    [Fact]
    public void TheRightStickTurnsPastItsDeadZoneAndTheButtonsWin()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus);
        bus.Publish(new OpenShop());
        float full = WardrobeModule.TurnRadiansPerSecond / 60f;

        wardrobe.SetTurnStick(0.15f);              // inside the dead zone
        wardrobe.Tick(Step);
        Assert.Equal(0f, bus.Yaws[^1].Yaw);

        wardrobe.SetTurnStick(1f);
        wardrobe.Tick(Step);
        Assert.Equal(full, bus.Yaws[^1].Yaw, 4);

        wardrobe.SetTurnStick(-0.6f);              // half way between the dead zone and full
        wardrobe.Tick(Step);
        Assert.Equal(full - 0.5f * full, bus.Yaws[^1].Yaw, 4);

        wardrobe.SetTurnStick(-1f);
        wardrobe.SetTurnButtons(false, true);      // E held with the stick left: E wins
        wardrobe.Tick(Step);
        Assert.Equal(1.5f * full, bus.Yaws[^1].Yaw, 4);
    }

    [Fact]
    public void MouseDragTurnsAtTheNextTickAndYawWraps()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus);
        wardrobe.Drag(500f);                       // closed: ignored
        bus.Publish(new OpenWardrobe());
        wardrobe.Tick(Step);
        Assert.Equal(0f, bus.Yaws[^1].Yaw);

        wardrobe.Drag(30f);
        wardrobe.Drag(20f);
        Assert.Equal(0f, wardrobe.Yaw);            // applied on the tick
        wardrobe.Tick(Step);
        Assert.Equal(50f * WardrobeModule.DragRadiansPerPixel, bus.Yaws[^1].Yaw, 4);
        wardrobe.Tick(Step);
        Assert.Equal(0.5f, bus.Yaws[^1].Yaw, 4);   // a drag is used once

        wardrobe.Drag(-100f);
        wardrobe.Tick(Step);
        Assert.Equal(-0.5f, bus.Yaws[^1].Yaw, 4);

        // Past half a turn it wraps round to the other side, staying in [-π, π).
        wardrobe.Drag(-300f);
        wardrobe.Tick(Step);
        Assert.Equal(-3.5f + MathF.Tau, bus.Yaws[^1].Yaw, 4);
        wardrobe.Drag(float.NaN);
        wardrobe.Tick(Step);
        Assert.InRange(bus.Yaws[^1].Yaw, -MathF.PI, MathF.PI);

        // Reopening faces the camera again.
        wardrobe.Close();
        bus.Publish(new OpenWardrobe());
        wardrobe.Tick(Step);
        Assert.Equal(0f, bus.Yaws[^1].Yaw);
    }

    [Fact]
    public void TryingOnShowsTheItemAndRevertsWhenNotBought()
    {
        var bus = new FakeBus { Coins = 100 };
        var wardrobe = Started(bus, CharacterKind.Girl);   // the Mushroom Dress, Mary-Janes, Big Bow
        bus.Publish(new Equip("shorts"));                  // kept under the dress
        wardrobe.TryOn("mushroom_hat");                    // shop not open: nothing
        wardrobe.Tick(Step);
        Assert.Equal("big_bow", bus.Appearances[^1].Head);

        bus.Publish(new OpenShop());
        wardrobe.TryOn("mushroom_hat");
        wardrobe.Tick(Step);
        Assert.Equal("mushroom_hat", bus.Appearances[^1].Head);
        Assert.Equal("spotted_dress", bus.Appearances[^1].Top);
        Assert.Equal(Catalog.IndexOf("big_bow"), wardrobe.Worn(Slot.Head));   // still worn underneath

        // Another highlighted item: the hat goes back to the bow.
        wardrobe.TryOn("jeans");
        wardrobe.Tick(Step);
        Assert.Equal("big_bow", bus.Appearances[^1].Head);
        Assert.Equal("", bus.Appearances[^1].Bottom);      // under the dress
        wardrobe.TryOn("sweater_vest");                    // a top instead of the dress shows the bottom
        wardrobe.Tick(Step);
        Assert.Equal(("sweater_vest", "shorts"), (bus.Appearances[^1].Top, bus.Appearances[^1].Bottom));

        // Not enough M¢ for it: still only tried on.
        wardrobe.TryOn("party_dress");
        wardrobe.Buy("party_dress");
        wardrobe.Tick(Step);
        Assert.Equal(BuyResult.NotEnough, wardrobe.LastBuy);
        Assert.Equal("party_dress", bus.Appearances[^1].Top);

        // Nothing highlighted, or a non-shop item: what is worn.
        wardrobe.TryOn("");
        wardrobe.Tick(Step);
        Assert.Equal("spotted_dress", bus.Appearances[^1].Top);
        wardrobe.TryOn("striped_tee");
        wardrobe.Tick(Step);
        Assert.Equal("spotted_dress", bus.Appearances[^1].Top);
        Assert.Equal(-1, wardrobe.TryingOn);

        // Closing the shop takes off what was not bought.
        wardrobe.TryOn("round_glasses");
        wardrobe.Tick(Step);
        Assert.Equal("round_glasses", bus.Appearances[^1].Face);
        wardrobe.Close();
        wardrobe.Tick(Step);
        Assert.Equal("", bus.Appearances[^1].Face);
        Assert.Equal(-1, wardrobe.TryingOn);
        wardrobe.TryOn("round_glasses");                   // closed: ignored
        wardrobe.Tick(Step);
        Assert.Equal("", bus.Appearances[^1].Face);
    }

    [Fact]
    public void TryingOnOnlyItemsThatFit()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Boy);
        bus.Publish(new OpenShop());
        wardrobe.TryOn("flower_dress");                    // girl only
        wardrobe.Tick(Step);
        Assert.Equal("striped_tee", bus.Appearances[^1].Top);
        wardrobe.TryOn("no_such_item");
        Assert.Equal(-1, wardrobe.TryingOn);
    }

    [Fact]
    public void AnItemBoughtWhileTriedOnStaysOn()
    {
        var bus = new FakeBus { Coins = 1000 };
        var wardrobe = Started(bus, CharacterKind.Girl);
        bus.Publish(new OpenShop());
        wardrobe.TryOn("star_sunglasses");
        wardrobe.Buy("star_sunglasses");
        wardrobe.Tick(Step);
        Assert.Equal(BuyResult.Bought, wardrobe.LastBuy);
        wardrobe.Close();
        wardrobe.Tick(Step);
        Assert.Equal("star_sunglasses", bus.Appearances[^1].Face);
        Assert.Equal(Catalog.IndexOf("star_sunglasses"), wardrobe.Worn(Slot.Face));
    }

    [Fact]
    public void BuyingWithEnoughCoinsAddsTheItem()
    {
        var bus = new FakeBus { Coins = 1000 };
        var wardrobe = Started(bus, CharacterKind.Girl);
        bus.Publish(new WalletState(1000));
        bus.Publish(new OpenShop());
        wardrobe.Buy("mushroom_hat");
        Assert.Empty(bus.Spends);                 // sent on the next tick
        wardrobe.Tick(Step);

        var spend = Assert.Single(bus.Spends);
        Assert.Equal(450, spend.Amount);
        Assert.Equal("Red Mushroom-Cap Hat", spend.What);
        Assert.True(wardrobe.Owns("mushroom_hat"));
        Assert.Equal(BuyResult.Bought, wardrobe.LastBuy);
        Assert.Equal(550, wardrobe.Coins);
        var received = Assert.Single(bus.Received);
        Assert.Equal("mushroom_hat", received.ItemId);
        Assert.Equal(WardrobeModule.ShopReason, received.Why);

        // Bought items can be worn, and are not sold twice.
        bus.Publish(new Equip("mushroom_hat"));
        wardrobe.Tick(Step);
        Assert.Equal("mushroom_hat", bus.Appearances[^1].Head);
        Assert.False(wardrobe.CanBuy(Catalog.IndexOf("mushroom_hat")));
        wardrobe.Buy("mushroom_hat");
        wardrobe.Tick(Step);
        Assert.Single(bus.Spends);
    }

    [Fact]
    public void BuyingWithTooFewCoinsAddsNothing()
    {
        var bus = new FakeBus { Coins = 100 };
        var wardrobe = Started(bus, CharacterKind.Girl);
        bus.Publish(new OpenShop());
        wardrobe.Buy("party_dress");
        wardrobe.Tick(Step);
        Assert.Single(bus.Spends);
        Assert.False(wardrobe.Owns("party_dress"));
        Assert.Equal(BuyResult.NotEnough, wardrobe.LastBuy);
        Assert.Empty(bus.Received);
        Assert.Equal(100, bus.Coins);
    }

    [Fact]
    public void OnlyTheOpenShopSellsAndOnlyShopItemsThatFit()
    {
        var bus = new FakeBus { Coins = 5000 };
        var wardrobe = Started(bus, CharacterKind.Boy);
        wardrobe.Buy("jeans");                     // shop not open
        bus.Publish(new OpenWardrobe());
        wardrobe.Buy("jeans");                     // the wardrobe is not the shop
        bus.Publish(new OpenShop());
        wardrobe.Buy(Catalog.SpiderSilkScarf);     // rewards are not for sale
        wardrobe.Buy("flower_dress");              // does not fit the boy
        wardrobe.Buy("sneakers");                  // already owned
        wardrobe.Tick(Step);
        Assert.Empty(bus.Spends);
        Assert.False(wardrobe.Owns("jeans"));
    }

    [Fact]
    public void ASpendResultForSomeoneElseIsIgnored()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Girl);
        bus.Publish(new SpendResult(7, true, 0));
        bus.Publish(new SpendResult(WardrobeModule.RequestIdBase + 1, true, 0));
        wardrobe.Tick(Step);
        Assert.Empty(bus.Received);
    }

    [Fact]
    public void FirstTarantulaAndFirstLadybugGiveTheirRewardOnce()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Boy);
        bus.Publish(Kill(1, MonsterKind.Caterpillar));
        bus.Publish(Kill(2, MonsterKind.Ant));
        Assert.Empty(bus.Received);

        bus.Publish(Kill(3, MonsterKind.Tarantula));
        bus.Publish(Kill(4, MonsterKind.Tarantula));
        Assert.True(wardrobe.Owns(Catalog.SpiderSilkScarf));
        Assert.False(wardrobe.Owns(Catalog.LadybugBackpack));
        var scarf = Assert.Single(bus.Received);
        Assert.Equal(Catalog.SpiderSilkScarf, scarf.ItemId);
        Assert.Equal(WardrobeModule.TarantulaReason, scarf.Why);

        bus.Publish(Kill(5, MonsterKind.Ladybug));
        bus.Publish(Kill(6, MonsterKind.Ladybug));
        Assert.Equal(2, bus.Received.Count);
        Assert.Equal(Catalog.LadybugBackpack, bus.Received[1].ItemId);
        Assert.True(wardrobe.TarantulaRewarded && wardrobe.LadybugRewarded);

        bus.Publish(new Equip(Catalog.LadybugBackpack));
        wardrobe.Tick(Step);
        Assert.Equal(Catalog.LadybugBackpack, bus.Appearances[^1].Back);
    }

    [Fact]
    public void GrantItemGivesOnceAndIgnoresUnknownItems()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Girl);
        bus.Publish(new GrantItem("round_glasses", "Millie's quest"));
        bus.Publish(new GrantItem("round_glasses", "Millie's quest"));
        bus.Publish(new GrantItem("sneakers", "already owned"));
        bus.Publish(new GrantItem("no_such_item", "?"));
        var received = Assert.Single(bus.Received);
        Assert.Equal("round_glasses", received.ItemId);
        Assert.Equal("Millie's quest", received.Why);
        Assert.True(wardrobe.Owns("round_glasses"));
    }

    [Fact]
    public void SwitchingCharacterTakesOffWhatDoesNotFit()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Girl);
        bus.Publish(new Equip("shorts"));
        bus.Publish(new Equip("spotted_dress"));
        bus.Publish(Player(CharacterKind.Boy));
        wardrobe.Tick(Step);
        Assert.Equal("", bus.Appearances[^1].Top);
        Assert.Equal("shorts", bus.Appearances[^1].Bottom);
    }

    [Fact]
    public void WearOutfitPutsOnAStarterOutfit()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Boy);
        wardrobe.WearOutfit(2);
        wardrobe.Tick(Step);
        var look = bus.Appearances[^1];
        Assert.Equal(("overalls_top", "overalls", "rain_boots", "beanie"), (look.Top, look.Bottom, look.Shoes, look.Head));
        wardrobe.WearOutfit(1); // the dress outfit does not fit the boy
        wardrobe.Tick(Step);
        Assert.Equal("overalls_top", bus.Appearances[^1].Top);
    }

    /// <summary>Everything a save keeps: the look, what is owned and what is put on in each slot.</summary>
    private static (int, int, int, int, bool[], int[]) StateOf(WardrobeModule w)
    {
        var owned = new bool[Catalog.Count];
        for (int i = 0; i < owned.Length; i++)
            owned[i] = w.Owns(i);
        var worn = new int[6];
        for (int s = 0; s < worn.Length; s++)
            worn[s] = w.Worn((Slot)s);
        return (w.Skin, w.HairStyle, w.HairColor, w.EyeColor, owned, worn);
    }

    private static void AssertSameState(WardrobeModule expected, WardrobeModule actual)
    {
        var (a, b) = (StateOf(expected), StateOf(actual));
        Assert.Equal((a.Item1, a.Item2, a.Item3, a.Item4), (b.Item1, b.Item2, b.Item3, b.Item4));
        Assert.Equal(a.Item5, b.Item5);
        Assert.Equal(a.Item6, b.Item6);
        Assert.Equal(expected.Appearance, actual.Appearance);
    }

    [Fact]
    public void SaveAndRestoreRoundTripsTheLookAndOwnedAndWornItemsWithoutSideEffects()
    {
        // A girl who bought the flower clip, got the spider silk scarf, changed her look, wears the
        // flower clip, the scarf and a dress over hidden shorts, and took her shoes off.
        var bus = new FakeBus { Coins = 1000 };
        var original = Started(bus, CharacterKind.Girl);
        bus.Publish(new SetLook(3, 4, 5, 2));
        bus.Publish(new OpenShop());
        original.Buy("flower_clip");
        original.Tick(Step);
        original.Close();
        bus.Publish(Kill(1, MonsterKind.Tarantula));
        bus.Publish(new Equip("flower_clip"));
        bus.Publish(new Equip(Catalog.SpiderSilkScarf));
        bus.Publish(new Equip("shorts"));
        bus.Publish(new Equip("spotted_dress"));
        bus.Publish(Equip.Unequip(Slot.Shoes));
        Assert.True(original.Owns("flower_clip"));
        Assert.True(original.BottomHidden);

        bus.Publish(new CollectSave(42));
        var part = Assert.Single(bus.Parts);
        Assert.Equal(42, part.RequestId);
        Assert.Equal(SaveModules.Wardrobe, part.Module);
        Assert.Contains("\"v\":1", part.Json);

        // A fresh game, with a screen open, restores it.
        var loadBus = new FakeBus { Coins = 1000 };
        var restored = Started(loadBus, CharacterKind.Girl);
        loadBus.Publish(new OpenWardrobe());
        loadBus.All.Clear();
        loadBus.Publish(new RestorePart(SaveModules.Wallet, "{\"v\":1}"));
        loadBus.Publish(new RestorePart(SaveModules.Wardrobe, part.Json));
        AssertSameState(original, restored);
        Assert.True(restored.BottomHidden);
        Assert.Equal(-1, restored.Worn(Slot.Shoes));
        Assert.False(restored.Open);
        // Restoring published nothing: no ItemReceived, no SpendCoins.
        Assert.Equal(2, loadBus.All.Count);

        // The state stays restored: ticks, the player's state and the first tarantula give nothing new.
        restored.Tick(Step);
        loadBus.Publish(Player(CharacterKind.Girl));
        loadBus.Publish(Kill(2, MonsterKind.Tarantula));
        restored.Tick(Step);
        Assert.Empty(loadBus.Received);
        Assert.Empty(loadBus.Spends);
        AssertSameState(original, restored);
        Assert.Equal(original.Appearance, loadBus.Appearances[^1]);

        // Saving the restored game writes the same part.
        loadBus.Publish(new CollectSave(43));
        Assert.Equal(part.Json, Assert.Single(loadBus.Parts).Json);
    }

    [Fact]
    public void ABoysSaveRoundTripsIntoAFreshGame()
    {
        var bus = new FakeBus();
        var original = Started(bus, CharacterKind.Boy);
        original.WearOutfit(2);
        bus.Publish(new GrantItem(Catalog.LadybugBackpack, "quest"));
        bus.Publish(new Equip(Catalog.LadybugBackpack));
        bus.Publish(new SetLook(5, 0, 7, 5));
        bus.Publish(new CollectSave(1));

        var loadBus = new FakeBus();
        var restored = Started(loadBus);
        loadBus.Publish(new RestorePart(SaveModules.Wardrobe, bus.Parts[^1].Json));
        loadBus.Publish(Player(CharacterKind.Boy));
        AssertSameState(original, restored);
        Assert.True(restored.LadybugRewarded);
        Assert.Empty(loadBus.Received);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"v\":1,\"look\":[0,0,0")]
    [InlineData("[1,2,3]")]
    [InlineData("null")]
    [InlineData("{\"v\":2,\"look\":[1,1,1,1],\"owned\":[\"mushroom_hat\"],\"worn\":[\"\",\"\",\"\",\"mushroom_hat\",\"\",\"\"]}")]
    [InlineData("{\"v\":1,\"look\":[99,1,1,1],\"owned\":[\"mushroom_hat\"],\"worn\":[\"\",\"\",\"\",\"mushroom_hat\",\"\",\"\"]}")]
    [InlineData("{\"v\":1,\"look\":[1,1,1,1],\"owned\":[\"mushroom_hat\"],\"worn\":[\"mushroom_hat\"]}")]
    [InlineData("{\"v\":1,\"look\":\"pink\",\"owned\":[],\"worn\":[]}")]
    public void BadOrUnknownSaveDataKeepsTheDefaultsAndLogsOnce(string? json)
    {
        var defaults = Started(new FakeBus(), CharacterKind.Girl);
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Girl);
        var log = new List<string>();
        wardrobe.Log = log.Add;
        bus.All.Clear();
        bus.Publish(new RestorePart(SaveModules.Wardrobe, json!));
        bus.Publish(new RestorePart(SaveModules.Wardrobe, json!));
        AssertSameState(defaults, wardrobe);
        Assert.Single(log);
        Assert.Contains("Wardrobe", log[0]);
        Assert.Equal(2, bus.All.Count);
        Assert.False(wardrobe.Restore(json));
    }

    [Fact]
    public void RestoreSkipsItemsNoLongerInTheCatalog()
    {
        var wardrobe = Started(new FakeBus(), CharacterKind.Girl);
        Assert.True(wardrobe.Restore("{\"v\":1,\"look\":[1,2,3,4],\"owned\":[\"gone_hat\",\"skirt\"]," +
            "\"worn\":[\"striped_tee\",\"skirt\",\"\",\"gone_hat\",\"\",\"\"]}"));
        Assert.True(wardrobe.Owns("skirt"));
        Assert.Equal(Catalog.IndexOf("skirt"), wardrobe.Worn(Slot.Bottom));
        Assert.Equal(-1, wardrobe.Worn(Slot.Head));
        Assert.Equal((1, 2, 3, 4), (wardrobe.Skin, wardrobe.HairStyle, wardrobe.HairColor, wardrobe.EyeColor));
    }

    /// <summary>
    /// Plays the Input module's <see cref="MenuNav"/> as its contract says: a step on the tick a
    /// direction is first pushed, then one per 0.2 s (12 ticks) while held, never two closer than that.
    /// </summary>
    private sealed class FakeMenuInput
    {
        private const int RepeatTicks = 12;
        private long _tick;
        private long _lastStep = -RepeatTicks;

        public MenuNav Next(int vertical)
        {
            int step = vertical != 0 && _tick - _lastStep >= RepeatTicks ? vertical : 0;
            if (step != 0)
                _lastStep = _tick;
            _tick++;
            return new MenuNav(step, 0);
        }
    }

    /// <summary>
    /// Runs <paramref name="ticks"/> ticks with the screen open: each tick the Input module publishes
    /// its MenuNav (down held while <paramref name="held"/> says so) and the view runs two frames (a
    /// 120 Hz screen), moving the focus by each step it takes. Returns the ticks the focus moved on.
    /// </summary>
    private static List<int> FocusMoves(WardrobeModule wardrobe, FakeBus bus, int ticks, Func<int, bool> held)
    {
        var input = new FakeMenuInput();
        var moves = new List<int>();
        for (int t = 0; t < ticks; t++)
        {
            bus.Publish(input.Next(held(t) ? 1 : 0));
            wardrobe.Tick(Step);
            for (int frame = 0; frame < 2; frame++)
                if (wardrobe.TakeMenuStep(out int vertical, out int horizontal))
                {
                    Assert.Equal((1, 0), (vertical, horizontal));
                    moves.Add(t);
                }
        }
        return moves;
    }

    [Fact]
    public void HeldDirectionMovesTheFocusOneItemPerFifthOfASecond()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Girl);
        bus.Publish(new OpenShop());
        // Down held for one second: the first item at once, then one per 0.2 s (12 ticks).
        Assert.Equal(new[] { 0, 12, 24, 36, 48 }, FocusMoves(wardrobe, bus, 60, _ => true));
    }

    [Fact]
    public void ShortFlickMovesTheFocusExactlyOneItem()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Girl);
        bus.Publish(new OpenWardrobe());
        // The stick pushed for two ticks (33 ms), then let go: one item, however long the screen stays open.
        Assert.Equal(new[] { 0 }, FocusMoves(wardrobe, bus, 60, t => t < 2));
    }

    [Fact]
    public void MenuStepsAreTakenOnceAndOnlyWhileAScreenIsOpen()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Girl);
        // Closed: steps (walking with W A S D or the stick) are not kept for later.
        bus.Publish(new MenuNav(1, 0));
        bus.Publish(new OpenWardrobe());
        Assert.False(wardrobe.TakeMenuStep(out _, out _));

        // The ticks without a step do not drop one the view has not taken yet.
        bus.Publish(new MenuNav(0, -1));
        bus.Publish(new MenuNav(0, 0));
        Assert.True(wardrobe.TakeMenuStep(out int vertical, out int horizontal));
        Assert.Equal((0, -1), (vertical, horizontal));
        Assert.False(wardrobe.TakeMenuStep(out _, out _));

        // Steps never pile up: only the latest untaken one moves the focus.
        bus.Publish(new MenuNav(-1, 0));
        bus.Publish(new MenuNav(1, 0));
        Assert.True(wardrobe.TakeMenuStep(out vertical, out _));
        Assert.Equal(1, vertical);
        Assert.False(wardrobe.TakeMenuStep(out _, out _));

        // Closing forgets an untaken step.
        bus.Publish(new MenuNav(1, 0));
        wardrobe.Close();
        bus.Publish(new OpenShop());
        Assert.False(wardrobe.TakeMenuStep(out _, out _));
    }
}

public class WardrobePromptsTests
{
    public static readonly TheoryData<DeviceKind> Devices = new()
    {
        DeviceKind.KeyboardMouse, DeviceKind.Xbox, DeviceKind.PlayStation, DeviceKind.Nintendo, DeviceKind.Generic,
    };

    /// <summary>The icon file-name prefixes of a device's buttons (Generic pads show the Xbox ones).</summary>
    private static string[] Families(DeviceKind device) => device switch
    {
        DeviceKind.KeyboardMouse => new[] { "key_", "mouse_" },
        DeviceKind.PlayStation => new[] { "ps_" },
        DeviceKind.Nintendo => new[] { "ns_" },
        _ => new[] { "xbox_" },
    };

    private static readonly string[] AllFamilies = { "key_", "mouse_", "xbox_", "ps_", "ns_" };

    private static string[] IconsIn(string bbcode) =>
        Regex.Matches(bbcode, @"\[img=\d+\](.*?)\[/img\]").Select(m => m.Groups[1].Value).ToArray();

    private static string Glyph(string path) => Path.GetFileNameWithoutExtension(path);

    private static string Words(string bbcode) => Regex.Replace(bbcode, @"\[img=\d+\].*?\[/img\]", "").Replace('\u00A0', ' ');

    private static string RepoFile(string resPath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, resPath.Substring("res://".Length));
    }

    private static void OnlyThatDevice(string bbcode, DeviceKind device)
    {
        var icons = IconsIn(bbcode);
        Assert.NotEmpty(icons);
        var own = Families(device);
        foreach (var icon in icons)
        {
            string glyph = Glyph(icon);
            Assert.True(own.Any(glyph.StartsWith), $"{device} hint shows {glyph}");
            Assert.True(File.Exists(RepoFile(icon)), $"missing icon {icon}");
        }
        foreach (var other in AllFamilies.Except(own))
            Assert.DoesNotContain("/" + other, bbcode);
        // No button names in words, as in the old "Enter / A" and "Esc / B" hints.
        string words = Words(bbcode);
        Assert.DoesNotContain("/", words);
        foreach (var name in new[] { "Esc", "Enter", "Page", "LB", "RB", "LT", "RT", "Cross", "Circle", "stick", " A ", " B ", " Q ", " E " })
            Assert.DoesNotContain(name, " " + words + " ");
    }

    [Theory]
    [MemberData(nameof(Devices))]
    public void WardrobeHintShowsOnlyThatDevicesIcons(DeviceKind device)
    {
        string hint = WardrobePrompts.Wardrobe(device);
        OnlyThatDevice(hint, device);
        Assert.Contains(Prompts.Rich(PromptAction.Confirm, device, WardrobePrompts.IconSize) + "\u00A0Choose", hint);
        Assert.Contains(Prompts.Rich(PromptAction.Back, device, WardrobePrompts.IconSize) + "\u00A0Done", hint);
        Assert.Contains(Prompts.Icon(PromptAction.TurnLeft, device), hint);
        Assert.Contains(Prompts.Icon(PromptAction.TurnRight, device), hint);
        Assert.Contains(WardrobePrompts.TabIcon(-1, device), hint);
        Assert.Contains(WardrobePrompts.TabIcon(1, device), hint);
        Assert.Contains("Tabs", hint);
    }

    [Theory]
    [MemberData(nameof(Devices))]
    public void ShopHintShowsOnlyThatDevicesIcons(DeviceKind device)
    {
        string hint = WardrobePrompts.Shop(device);
        OnlyThatDevice(hint, device);
        Assert.Contains(Prompts.Rich(PromptAction.Confirm, device, WardrobePrompts.IconSize) + "\u00A0Pick,\u00A0then\u00A0Buy", hint);
        Assert.Contains(Prompts.Rich(PromptAction.Back, device, WardrobePrompts.IconSize) + "\u00A0Done", hint);
        Assert.Contains(Prompts.Icon(PromptAction.TurnLeft, device), hint);
        Assert.DoesNotContain("Tabs", hint);
    }

    [Fact]
    public void EachDeviceShowsItsOwnButtons()
    {
        Assert.Contains("[img=32]res://modules/Input/Glyphs/key_enter.svg[/img]\u00A0Choose", WardrobePrompts.Wardrobe(DeviceKind.KeyboardMouse));
        Assert.Contains("[img=32]res://modules/Input/Glyphs/key_esc.svg[/img]\u00A0Done", WardrobePrompts.Wardrobe(DeviceKind.KeyboardMouse));
        Assert.Contains("res://modules/Wardrobe/Godot/Glyphs/key_pgup.svg", WardrobePrompts.Wardrobe(DeviceKind.KeyboardMouse));
        Assert.Contains("mouse_left.svg[/img]\u00A0Drag", WardrobePrompts.Wardrobe(DeviceKind.KeyboardMouse));
        Assert.Contains("xbox_a.svg[/img]\u00A0Choose", WardrobePrompts.Wardrobe(DeviceKind.Xbox));
        Assert.Contains("xbox_b.svg[/img]\u00A0Done", WardrobePrompts.Shop(DeviceKind.Xbox));
        Assert.Contains("xbox_lt.svg", WardrobePrompts.Wardrobe(DeviceKind.Xbox));
        Assert.Contains("xbox_rs.svg", WardrobePrompts.Wardrobe(DeviceKind.Xbox));
        Assert.Contains("ps_cross.svg[/img]\u00A0Choose", WardrobePrompts.Wardrobe(DeviceKind.PlayStation));
        Assert.Contains("ps_circle.svg[/img]\u00A0Done", WardrobePrompts.Shop(DeviceKind.PlayStation));
        Assert.Contains("ps_l2.svg", WardrobePrompts.Wardrobe(DeviceKind.PlayStation));
        Assert.Contains("ns_zr.svg", WardrobePrompts.Wardrobe(DeviceKind.Nintendo));
        Assert.Equal(WardrobePrompts.Wardrobe(DeviceKind.Xbox), WardrobePrompts.Wardrobe(DeviceKind.Generic));
    }

    [Fact]
    public void IconSizeIsUsedForEveryIcon()
    {
        string hint = WardrobePrompts.Wardrobe(DeviceKind.PlayStation, 48);
        Assert.Equal(IconsIn(hint).Length, Regex.Matches(hint, @"\[img=48\]").Count);
        Assert.DoesNotContain("[img=32]", hint);
    }

    [Theory]
    [InlineData("PS5 Controller", "", DeviceKind.PlayStation)]
    [InlineData("Sony Interactive Entertainment Wireless Controller", "", DeviceKind.PlayStation)]
    [InlineData("Nintendo Switch Pro Controller", "", DeviceKind.Nintendo)]
    [InlineData("Xbox Series X Controller", "", DeviceKind.Xbox)]
    [InlineData("Wireless Controller", "030000004c050000cc09000011810000", DeviceKind.PlayStation)]
    [InlineData("Some Pad", "030000005e0400008e02000010010000", DeviceKind.Xbox)]
    [InlineData("Some Pad", "03000000790000000600000010010000", DeviceKind.Generic)]
    [InlineData(null, null, DeviceKind.Generic)]
    public void PadFamilyFromNameOrVendor(string? name, string? guid, DeviceKind expected) =>
        Assert.Equal(expected, WardrobePrompts.PadFamily(name, guid));
}
