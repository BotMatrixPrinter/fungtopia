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
    [InlineData("folk", new[] { "Head", "Body", "Trousers", "Skirt", "Collar", "ArmL", "ArmR", "HandL", "HandR", "LegL", "LegR",
        "ShinL", "ShinR", "FootL", "FootR",
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

    private static Vector3 Size(List<Vector3> v) => v.Aggregate(Vector3.Max) - v.Aggregate(Vector3.Min);

    [Fact]
    public void TheFolkHaveTheGrownUpRigOfTheCore()
    {
        var glb = new Glb("folk");
        Assert.True(Vector3.Distance(new Vector3(0f, Body.NeckY, 0f), glb.Origin("Head")) < 0.001f);
        Assert.True(Vector3.Distance(new Vector3(0f, Body.HipY, 0f), glb.Origin("Body")) < 0.001f);
        Assert.Equal(1.48f, Body.NeckY);
        Assert.Equal(0.9f, Body.HipY);
        foreach (var (arm, hand, leg, shin, foot, side) in new[] { ("ArmL", "HandL", "LegL", "ShinL", "FootL", -1f), ("ArmR", "HandR", "LegR", "ShinR", "FootR", 1f) })
        {
            Assert.True(Vector3.Distance(new Vector3(side * 0.22f, 1.4f, 0f), glb.Origin(arm)) < 0.001f, arm);
            Assert.True(Vector3.Distance(new Vector3(side * Body.ShoulderX, Body.ShoulderY, 0f), glb.Origin(arm)) < 0.001f, arm);
            Assert.Equal(0.66f, Vector3.Distance(glb.Origin(arm), glb.Origin(hand)), 2);
            Assert.Equal(Body.ArmLength, Vector3.Distance(glb.Origin(arm), glb.Origin(hand)), 2);
            Assert.True(Vector3.Distance(new Vector3(side * 0.1f, 0.9f, 0f), glb.Origin(leg)) < 0.001f, leg);
            Assert.True(Vector3.Distance(new Vector3(side * Body.HipX, Body.KneeY, 0f), glb.Origin(shin)) < 0.001f, shin);
            // Invisible arms, but real legs: a thigh on the hip, a shin on the knee, a shoe on the shin.
            Assert.Equal(-1, glb.Node(arm).Mesh);
            Assert.Equal(new[] { "Legs" }, glb.Materials(leg));
            Assert.Equal(new[] { "Legs" }, glb.Materials(shin));
            Assert.Equal(leg, glb.ParentOf(shin));
            Assert.Equal(shin, glb.ParentOf(foot));
            Assert.Equal(new[] { "Shoe" }, glb.Materials(foot));
            Assert.Equal(new[] { "Skin" }, glb.Materials(hand));
        }
        Assert.Equal("Body", glb.ParentOf("Trousers"));
        Assert.Equal("Body", glb.ParentOf("Skirt"));
        Assert.Equal("Body", glb.ParentOf("Collar"));
        Assert.Equal(new[] { "Trousers" }, glb.Materials("Trousers"));
        Assert.Equal(new[] { "Outfit", "Trim" }, glb.Materials("Skirt").Distinct().OrderBy(m => m, StringComparer.Ordinal));
        Assert.Contains("Outfit", glb.Materials("Body"));
        Assert.Contains("Trim", glb.Materials("Body"));
    }

    [Fact]
    public void TheFolkAre1_80TallWithNormalGrownUpProportions()
    {
        var glb = new Glb("folk");
        // The head: round, 0.27 m wide and 0.30 m tall round 1.64 m, its top at about 1.80.
        var head = glb.Vertices("Head", "Skin");
        var headSize = Size(head);
        Assert.InRange(headSize.X, 0.26f, 0.32f);
        Assert.InRange(head.Max(v => v.Y) - head.Min(v => v.Y), 0.28f, 0.32f);
        Assert.InRange((head.Max(v => v.Y) + head.Min(v => v.Y)) / 2f, 1.62f, 1.66f);
        Assert.InRange(head.Max(v => v.Y), 1.77f, 1.81f);
        // The torso: from 0.88 to 1.44 m, shoulders about 0.40 wide, waist 0.30, hips 0.34, 0.22 deep.
        var torso = glb.Vertices("Body", "Outfit");
        Assert.InRange(torso.Min(v => v.Y), 0.86f, 0.9f);
        Assert.InRange(torso.Max(v => v.Y), 1.42f, 1.46f);
        float Width(float y0, float y1) => torso.Where(v => v.Y >= y0 && v.Y <= y1).Max(v => MathF.Abs(v.X)) * 2f;
        Assert.InRange(Width(1.3f, 1.42f), 0.37f, 0.43f);
        Assert.InRange(Width(1.08f, 1.12f), 0.28f, 0.32f);
        Assert.InRange(Width(0.9f, 0.96f), 0.32f, 0.36f);
        Assert.InRange(torso.Max(v => MathF.Abs(v.Z)) * 2f, 0.2f, 0.24f);
        // The head floats about 5 cm over the torso and its collar.
        float bodyTop = glb.VerticesUnder("Body").Max(v => v.Y);
        float headBottom = glb.Vertices("Head").Min(v => v.Y);
        Assert.InRange(headBottom - bodyTop, 0.02f, 0.08f);
        Assert.InRange(headBottom - torso.Max(v => v.Y), 0.04f, 0.07f);
        // The legs: rounded tubes about 0.065 m round, from the hips down to the shoes.
        foreach (var (leg, shin, foot) in new[] { ("LegL", "ShinL", "FootL"), ("LegR", "ShinR", "FootR") })
        {
            var thigh = glb.Vertices(leg);
            Assert.InRange(Size(thigh).X, 0.12f, 0.14f);
            Assert.InRange(Size(thigh).Z, 0.12f, 0.14f);
            Assert.InRange(thigh.Max(v => v.Y), 0.9f, 0.98f);
            Assert.InRange(thigh.Min(v => v.Y), 0.4f, 0.48f);
            var lower = glb.Vertices(shin);
            Assert.InRange(Size(lower).X, 0.11f, 0.14f);
            Assert.True(lower.Min(v => v.Y) < 0.1f && lower.Max(v => v.Y) > 0.5f, shin);
            // A rounded shoe about 0.12 m tall and 0.24 m long, on the ground, its toe forward.
            var shoe = glb.Vertices(foot);
            Assert.InRange(shoe.Min(v => v.Y), -0.005f, 0.01f);
            Assert.InRange(shoe.Max(v => v.Y), 0.11f, 0.13f);
            Assert.InRange(Size(shoe).Z, 0.22f, 0.26f);
            Assert.True(shoe.Average(v => v.Z) < -0.02f, foot);
        }
        // Big cute eyes with sparkles, rosy cheeks and a smile on the front of the head.
        var eyes = glb.Vertices("Head", "Eye");
        Assert.True(eyes.All(v => v.Z < -0.08f));
        Assert.InRange(eyes.Max(v => v.Y) - eyes.Min(v => v.Y), 0.07f, 0.12f);
        Assert.True(eyes.Average(v => v.Y) < 1.64f && eyes.Average(v => v.Y) > 1.56f);
        foreach (var detail in new[] { "EyeShine", "Blush", "Mouth" })
            Assert.Contains(detail, glb.Materials("Head"));
        // Round hands about 0.055 m round, floating at the ends of the arms, clear of the torso, skirt and legs.
        var near = glb.VerticesUnder("Body").Concat(glb.Vertices("LegL")).Concat(glb.Vertices("LegR")).ToList();
        foreach (var hand in new[] { "HandL", "HandR" })
        {
            var size = Size(glb.Vertices(hand));
            Assert.InRange(size.X, 0.1f, 0.12f);
            Assert.True(MathF.Abs(size.X - size.Y) < 0.01f, hand);
            var c = glb.Origin(hand);
            Assert.InRange(c.Y, 0.7f, 0.8f);
            Assert.True(near.Min(v => Vector3.Distance(v, c)) > 0.07f, hand);
        }
        // A skirt flares down to about 0.66 m.
        var skirt = glb.Vertices("Skirt");
        Assert.InRange(skirt.Min(v => v.Y), 0.62f, 0.7f);
        Assert.True(skirt.Where(v => v.Y < 0.72f).Max(v => MathF.Abs(v.X)) > Width(0.9f, 0.96f) / 2f + 0.04f);
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
            // Hair fits the small grown-up head.
            Assert.True(Size(glb.Vertices(hair)).X < 0.4f, hair);
        }
        foreach (var hat in Hats)
        {
            Assert.Equal("Head", glb.ParentOf(hat));
            Assert.Equal(new[] { "Gills", "Hat", "HatSpots" }, glb.Materials(hat).Distinct().OrderBy(m => m, StringComparer.Ordinal));
            var v = glb.Vertices(hat);
            Assert.True(v.Min(p => p.Y) > eyesTop, hat);
            Assert.True(v.Max(p => p.Y) > headTop + 0.02f, hat);
            Assert.True(Size(v).X < 0.5f, hat);
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
