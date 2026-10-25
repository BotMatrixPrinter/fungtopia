using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Xunit;

namespace Fungtopia.Townsfolk.Tests;

/// <summary>The models (modules/Townsfolk/Models/*.glb, built by townsfolk.py), read back.</summary>
public class TownsfolkModelsTests
{
    /// <summary>
    /// One of the models read back: each node's name, parent, mesh and world transform (glTF's
    /// frame, Godot's: X right, Y up, -Z forward), and its mesh's vertices by material.
    /// </summary>
    private sealed class Glb
    {
        public readonly Dictionary<string, int> Index = new();
        public readonly List<(string Name, int Parent, int Mesh, Matrix4x4 World)> Nodes = new();
        private readonly JsonElement _json;
        private readonly byte[] _bin;

        public Glb(string file)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "modules", "Townsfolk", "Models")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            var bytes = File.ReadAllBytes(Path.Combine(dir!.FullName, "modules", "Townsfolk", "Models", file + ".glb"));
            int jsonLength = BitConverter.ToInt32(bytes, 12);
            _json = JsonDocument.Parse(new ReadOnlyMemory<byte>(bytes, 20, jsonLength)).RootElement;
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
                Index[name] = i;
                Nodes.Add((name, parents[i], nodes[i].TryGetProperty("mesh", out var mesh) ? mesh.GetInt32() : -1, World(i)));
            }
        }

        private static Matrix4x4 Local(JsonElement node)
        {
            float[] Floats(string key, params float[] fallback) =>
                node.TryGetProperty(key, out var v) ? v.EnumerateArray().Select(x => x.GetSingle()).ToArray() : fallback;
            var t = Floats("translation", 0, 0, 0);
            var r = Floats("rotation", 0, 0, 0, 1);
            var s = Floats("scale", 1, 1, 1);
            return Matrix4x4.CreateScale(s[0], s[1], s[2]) * Matrix4x4.CreateFromQuaternion(new Quaternion(r[0], r[1], r[2], r[3])) *
                Matrix4x4.CreateTranslation(t[0], t[1], t[2]);
        }

        public bool Has(string name) => Index.ContainsKey(name);

        public (string Name, int Parent, int Mesh, Matrix4x4 World) Node(string name)
        {
            Assert.True(Index.ContainsKey(name), $"no node {name}");
            return Nodes[Index[name]];
        }

        public Vector3 Origin(string name) => Node(name).World.Translation;

        public string ParentOf(string name) => Node(name).Parent < 0 ? "" : Nodes[Node(name).Parent].Name;

        /// <summary>The materials of the node's own mesh.</summary>
        public IEnumerable<string> Materials(string name)
        {
            int mesh = Node(name).Mesh;
            if (mesh < 0)
                yield break;
            foreach (var primitive in _json.GetProperty("meshes")[mesh].GetProperty("primitives").EnumerateArray())
                yield return _json.GetProperty("materials")[primitive.GetProperty("material").GetInt32()].GetProperty("name").GetString()!;
        }

        /// <summary>The world positions of the node's own mesh's vertices (only those in <paramref name="materials"/> when given).</summary>
        public List<Vector3> Vertices(string name, params string[] materials)
        {
            var result = new List<Vector3>();
            var node = Node(name);
            if (node.Mesh < 0)
                return result;
            foreach (var primitive in _json.GetProperty("meshes")[node.Mesh].GetProperty("primitives").EnumerateArray())
            {
                string mat = _json.GetProperty("materials")[primitive.GetProperty("material").GetInt32()].GetProperty("name").GetString()!;
                if (materials.Length > 0 && !materials.Contains(mat))
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
                if (Nodes[i].Parent == Index[name])
                    result.AddRange(VerticesUnder(Nodes[i].Name));
            return result;
        }
    }


    private static readonly string[] Hairs = { "HairShort", "HairBob", "HairBuns", "HairSpiky", "HairLong" };
    private static readonly string[] Hats = { "HatDome", "HatFlat", "HatTall" };

    private static float Radius(Vector3 v) => MathF.Sqrt(v.X * v.X + v.Z * v.Z);

    [Theory]
    [InlineData("folk", new[] { "Head", "Body", "Trousers", "Collar", "ArmL", "ArmR", "HandL", "HandR", "LegL", "LegR", "FootL", "FootR",
        "HairShort", "HairBob", "HairBuns", "HairSpiky", "HairLong", "HatDome", "HatFlat", "HatTall" })]
    [InlineData("cat", new[] { "Body", "Head", "LegFL", "LegFR", "LegBL", "LegBR", "Tail" })]
    [InlineData("dog", new[] { "Body", "Head", "LegFL", "LegFR", "LegBL", "LegBR", "Tail" })]
    [InlineData("crow", new[] { "Body", "Head", "WingL", "WingR", "Tail", "LegL", "LegR" })]
    [InlineData("butterfly", new[] { "Body", "WingL", "WingR" })]
    public void EachModelHasThePartsTheViewAnimates(string file, string[] parts)
    {
        var glb = new Glb(file);
        foreach (var part in parts)
            Assert.True(glb.Has(part), $"{file}: {part}");
    }

    [Fact]
    public void TheFolkHaveTheNpcsRig()
    {
        var glb = new Glb("folk");
        Assert.True(Vector3.Distance(new Vector3(0f, 0.72f, 0f), glb.Origin("Head")) < 0.001f);
        Assert.True(Vector3.Distance(new Vector3(0f, 0.3f, 0f), glb.Origin("Body")) < 0.001f);
        foreach (var (arm, hand, leg, foot, side) in new[] { ("ArmL", "HandL", "LegL", "FootL", -1f), ("ArmR", "HandR", "LegR", "FootR", 1f) })
        {
            Assert.True(Vector3.Distance(new Vector3(side * 0.23f, 0.65f, 0f), glb.Origin(arm)) < 0.001f, arm);
            Assert.Equal(0.42f, Vector3.Distance(glb.Origin(arm), glb.Origin(hand)), 2);
            // No arms or legs are drawn: only empty pivots.
            Assert.Equal(-1, glb.Node(arm).Mesh);
            Assert.Equal(-1, glb.Node(leg).Mesh);
            Assert.Equal(leg, glb.ParentOf(foot));
            Assert.Equal(new[] { "Shoe" }, glb.Materials(foot));
            Assert.Equal(new[] { "Skin" }, glb.Materials(hand));
            Assert.True(glb.Vertices(foot).Min(v => v.Y) > -0.01f && glb.Vertices(foot).Max(v => v.Y) < 0.15f, foot);
        }
        Assert.Equal("Body", glb.ParentOf("Trousers"));
        Assert.Equal("Body", glb.ParentOf("Collar"));
        Assert.Equal(new[] { "Trousers" }, glb.Materials("Trousers"));
        Assert.Contains("Outfit", glb.Materials("Body"));
        Assert.Contains("Trim", glb.Materials("Body"));
    }

    [Fact]
    public void TheFolkHeadFloatsOverATriangleBodyWithRoundHandsBesideIt()
    {
        var glb = new Glb("folk");
        float bodyTop = glb.VerticesUnder("Body").Max(v => v.Y);
        float headBottom = glb.Vertices("Head").Min(v => v.Y);
        Assert.True(headBottom > bodyTop + 0.02f, $"head bottom {headBottom} over body top {bodyTop}");
        Assert.True(headBottom - bodyTop < 0.15f);
        // A cone: wide at the bottom, narrow at the top.
        var body = glb.Vertices("Body", "Outfit");
        float low = body.Where(v => v.Y < 0.2f).Max(Radius), high = body.Where(v => v.Y > 0.55f).Max(Radius);
        Assert.True(low > 0.25f && high < 0.15f, $"{low} {high}");
        // Big cute eyes on the front of the head.
        var eyes = glb.Vertices("Head", "Eye");
        Assert.NotEmpty(eyes);
        Assert.True(eyes.All(v => v.Z < -0.2f));
        Assert.True(eyes.Max(v => v.Y) - eyes.Min(v => v.Y) > 0.18f);
        foreach (var hand in new[] { "HandL", "HandR" })
        {
            var v = glb.Vertices(hand);
            var size = v.Aggregate(Vector3.Max) - v.Aggregate(Vector3.Min);
            Assert.True(size.X > 0.14f && size.X < 0.2f && MathF.Abs(size.X - size.Y) < 0.03f, hand);
            // Floating beside the body, not touching it.
            var c = glb.Origin(hand);
            float coneR = 0.3f + (0.05f - 0.3f) * (c.Y - 0.13f) / (0.61f - 0.13f);
            Assert.True(Radius(c) - 0.09f > coneR, hand);
        }
    }

    [Fact]
    public void HairAndHatsAreChildrenOfTheHeadWithoutFaces()
    {
        var glb = new Glb("folk");
        float eyesTop = glb.Vertices("Head", "Eye").Max(v => v.Y);
        float headTop = glb.Vertices("Head", "Skin").Max(v => v.Y);
        foreach (var hair in Hairs)
        {
            Assert.Equal("Head", glb.ParentOf(hair));
            Assert.Equal(new[] { "Hair" }, glb.Materials(hair));
        }
        foreach (var hat in Hats)
        {
            Assert.Equal("Head", glb.ParentOf(hat));
            Assert.Equal(new[] { "Gills", "Hat", "HatSpots" }, glb.Materials(hat).Distinct().OrderBy(m => m, StringComparer.Ordinal));
            var v = glb.Vertices(hat);
            Assert.True(v.Min(p => p.Y) > eyesTop, hat);
            Assert.True(v.Max(p => p.Y) > headTop + 0.05f, hat);
        }
    }

    [Theory]
    [InlineData("folk")]
    [InlineData("cat")]
    [InlineData("dog")]
    [InlineData("crow")]
    [InlineData("butterfly")]
    public void OnlyHeadsHaveEyes(string file)
    {
        var glb = new Glb(file);
        foreach (var node in glb.Nodes)
            if (node.Name != "Head")
                Assert.DoesNotContain("Eye", glb.Materials(node.Name));
        if (file != "butterfly")
            Assert.Contains("Eye", glb.Materials("Head"));
    }

    [Theory]
    [InlineData("cat", "Fur", 0.35f, 0.6f)]
    [InlineData("dog", "Fur", 0.4f, 0.65f)]
    [InlineData("crow", "Feather", 0.25f, 0.45f)]
    public void ThePetsAreSmallAndTintable(string file, string tinted, float minHeight, float maxHeight)
    {
        var glb = new Glb(file);
        var all = glb.Nodes.SelectMany(n => glb.Vertices(n.Name)).ToList();
        float top = all.Max(v => v.Y), bottom = all.Min(v => v.Y);
        Assert.True(bottom > -0.02f && bottom < 0.03f, $"{file} stands on the ground: {bottom}");
        Assert.True(top > minHeight && top < maxHeight, $"{file} height {top}");
        Assert.Contains(tinted, glb.Materials("Body"));
        Assert.Contains("Cap", glb.Nodes.SelectMany(n => glb.Materials(n.Name)));
        // Facing -Z: the eyes are in front.
        Assert.True(glb.Vertices("Head", "Eye").Average(v => v.Z) < glb.Vertices("Body").Average(v => v.Z));
    }

    [Fact]
    public void TheButterflysWingsLieFlatBesideItsBody()
    {
        var glb = new Glb("butterfly");
        Assert.Equal(new[] { "BodyDark" }, glb.Materials("Body"));
        foreach (var (wing, side) in new[] { ("WingL", -1f), ("WingR", 1f) })
        {
            Assert.Contains("Wing", glb.Materials(wing));
            var v = glb.Vertices(wing);
            Assert.True(v.All(p => p.X * side > 0f), wing);
            Assert.True(v.Max(p => p.Y) - v.Min(p => p.Y) < 0.02f, wing);
            Assert.True(MathF.Abs(glb.Origin(wing).X) < 0.001f, wing);
        }
        var all = glb.Vertices("WingL").Concat(glb.Vertices("WingR")).ToList();
        float span = all.Max(p => p.X) - all.Min(p => p.X);
        Assert.True(span > 0.1f && span < 0.2f, $"span {span}");
    }
}
