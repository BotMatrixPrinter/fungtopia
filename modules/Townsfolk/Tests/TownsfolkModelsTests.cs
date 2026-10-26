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

        /// <summary>The base color of the material named <paramref name="material"/> (linear).</summary>
        public Vector3 Color(string material)
        {
            foreach (var m in _json.GetProperty("materials").EnumerateArray())
                if (m.GetProperty("name").GetString() == material)
                {
                    // glTF leaves out the default, white.
                    if (!m.GetProperty("pbrMetallicRoughness").TryGetProperty("baseColorFactor", out var c))
                        return Vector3.One;
                    return new Vector3(c[0].GetSingle(), c[1].GetSingle(), c[2].GetSingle());
                }
            Assert.Fail($"no material {material}");
            return default;
        }

        /// <summary>Every material name of the model.</summary>
        public IEnumerable<string> AllMaterials => _json.GetProperty("materials").EnumerateArray().Select(m => m.GetProperty("name").GetString()!);

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


    private static readonly string[] Hats = { "HatDome", "HatFlat", "HatTall" };

    private static float Radius(Vector3 v) => MathF.Sqrt(v.X * v.X + v.Z * v.Z);

    private static Vector3 Size(List<Vector3> v) => v.Aggregate(Vector3.Max) - v.Aggregate(Vector3.Min);

    private static Vector3 Center(List<Vector3> v) => (v.Aggregate(Vector3.Max) + v.Aggregate(Vector3.Min)) / 2f;

    /// <summary>The vertices split into separate pieces (single linkage: a gap over <paramref name="gap"/> splits).</summary>
    private static List<List<Vector3>> Pieces(List<Vector3> vertices, float gap)
    {
        var pieces = new List<List<Vector3>>();
        var left = new List<Vector3>(vertices);
        while (left.Count > 0)
        {
            var piece = new List<Vector3> { left[^1] };
            left.RemoveAt(left.Count - 1);
            for (int i = 0; i < piece.Count; i++)
                for (int j = left.Count - 1; j >= 0; j--)
                    if (Vector3.Distance(piece[i], left[j]) < gap)
                    {
                        piece.Add(left[j]);
                        left.RemoveAt(j);
                    }
            pieces.Add(piece);
        }
        return pieces;
    }

    [Theory]
    [InlineData("folk", new[] { "Head", "Body", "Dress", "Capsule", "BowTie", "Bow", "ArmL", "ArmR", "HandL", "HandR",
        "LegL", "LegR", "ShinL", "ShinR", "HatDome", "HatFlat", "HatTall" })]
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
    public void TheFolkRigIsTheOneTheViewPoses()
    {
        var glb = new Glb("folk");
        Assert.Equal(1.47f, Look.ModelHeight);
        Assert.True(Vector3.Distance(new Vector3(0f, Stride.HipHeight, 0f), glb.Origin("Body")) < 0.001f);
        Assert.Equal(-1, glb.Node("Body").Mesh);
        foreach (var shape in new[] { "Dress", "Capsule", "BowTie" })
            Assert.Equal("Body", glb.ParentOf(shape));
        Assert.Equal(new[] { "Outfit" }, glb.Materials("Dress"));
        Assert.Equal(new[] { "Outfit" }, glb.Materials("Capsule"));
        Assert.Equal(new[] { "Bow" }, glb.Materials("BowTie"));
        foreach (var (arm, hand, leg, shin, side) in new[] { ("ArmL", "HandL", "LegL", "ShinL", -1f), ("ArmR", "HandR", "LegR", "ShinR", 1f) })
        {
            // No arms are drawn: only empty pivots holding the floating hands.
            Assert.Equal(-1, glb.Node(arm).Mesh);
            Assert.True(glb.Origin(arm).X * side > 0.1f, arm);
            Assert.Equal(new[] { "Finger", "Skin" }, glb.Materials(hand).OrderBy(m => m, StringComparer.Ordinal));
            // The thigh turns at the hip, the shin (with the foot) at the knee: Stride's heights.
            Assert.Equal(Stride.HipHeight, glb.Origin(leg).Y, 3);
            Assert.Equal(leg, glb.ParentOf(shin));
            Assert.Equal(Stride.KneeHeight, glb.Origin(shin).Y, 3);
            Assert.Equal(glb.Origin(leg).X, glb.Origin(shin).X, 3);
            Assert.True(glb.Origin(leg).X * side > 0.05f, leg);
            Assert.Equal(new[] { "Legs" }, glb.Materials(leg));
            Assert.Equal(new[] { "Legs" }, glb.Materials(shin));
        }
        var dress = glb.Vertices("Dress");
        Assert.Equal(Stride.BodyBottom, dress.Min(v => v.Y), 2);
        Assert.Equal(Stride.BodyBottom, glb.Vertices("Capsule").Min(v => v.Y), 2);
    }

    [Fact]
    public void TheFolkFollowTheDrawingsProportions()
    {
        var glb = new Glb("folk");
        const float height = 1.47f;
        var head = glb.Vertices("Head", "Skin");
        var headSize = Size(head);
        // A big plain round ball about 0.38 m across, its top at 1.47 m: about a quarter of the height.
        Assert.Equal(height, head.Max(v => v.Y), 2);
        Assert.InRange(headSize.X, 0.36f, 0.4f);
        Assert.InRange(headSize.Y, 0.36f, 0.4f);
        Assert.InRange(headSize.Z, 0.36f, 0.4f);
        float headBottom = head.Min(v => v.Y);
        foreach (var shape in new[] { "Dress", "Capsule" })
        {
            // Floating a few cm above the body: no neck.
            float shapeTop = glb.Vertices(shape).Max(v => v.Y);
            Assert.InRange(headBottom - shapeTop, 0.02f, 0.08f);
        }
        foreach (var shape in new[] { "Dress", "Capsule" })
        {
            var body = glb.Vertices(shape);
            // The body about half the height, the legs under it about a quarter.
            Assert.InRange((body.Max(v => v.Y) - body.Min(v => v.Y)) / height, 0.42f, 0.52f);
            Assert.InRange(body.Min(v => v.Y) / height, 0.22f, 0.28f);
        }

        // The dress: a clean tall cone, narrow and rounded at the top, about as wide as the head at the bottom.
        var dress = glb.Vertices("Dress");
        float Width(List<Vector3> v, float y0, float y1) => 2f * v.Where(p => p.Y >= y0 && p.Y <= y1).Max(Radius);
        float bottom = dress.Min(v => v.Y), top = dress.Max(v => v.Y);
        float hem = Width(dress, bottom, bottom + 0.06f);
        Assert.InRange(hem, 0.36f, 0.44f);
        Assert.True(Width(dress, top - 0.1f, top) < hem * 0.5f);
        float mid = Width(dress, (bottom + top) / 2f - 0.02f, (bottom + top) / 2f + 0.02f);
        Assert.InRange(mid, 0.18f, 0.28f);
        // The capsule: a tall rounded box about 0.32 m wide with straight sides.
        var capsule = glb.Vertices("Capsule");
        var capsuleSize = Size(capsule);
        Assert.InRange(capsuleSize.X, 0.3f, 0.34f);
        Assert.True(capsuleSize.Z < capsuleSize.X && capsuleSize.Z > 0.2f);
        float low = capsule.Min(v => v.Y) + capsuleSize.Y * 0.3f, high = capsule.Min(v => v.Y) + capsuleSize.Y * 0.7f;
        Assert.Equal(Width(capsule, low - 0.02f, low + 0.02f), Width(capsule, high - 0.02f, high + 0.02f), 2);
        // A big bow tie, about as wide as the capsule, at its top, on its front (-Z).
        var tie = glb.Vertices("BowTie");
        Assert.InRange(Size(tie).X, capsuleSize.X * 0.8f, capsuleSize.X * 1.1f);
        Assert.True(Center(tie).Y > capsule.Max(v => v.Y) - 0.2f && tie.Max(v => v.Y) < capsule.Max(v => v.Y));
        Assert.True(tie.Min(v => v.Z) < capsule.Min(v => v.Z));
        Assert.True(Size(tie).Y < Size(tie).X * 0.6f, "a bow tie is wider than tall");

        float bodyMid = (bottom + top) / 2f;
        foreach (var hand in new[] { "HandL", "HandR" })
        {
            // Big ovals (0.18 m tall, 0.14 m wide, about half the head's width) ...
            var skin = glb.Vertices(hand, "Skin");
            var size = Size(skin);
            Assert.InRange(size.Y, 0.16f, 0.2f);
            Assert.InRange(size.X, 0.12f, 0.16f);
            Assert.InRange(size.X / headSize.X, 0.3f, 0.5f);
            // ... floating well out beside the body at about its middle height ...
            var c = Center(skin);
            Assert.InRange(c.Y, bodyMid - 0.08f, bodyMid + 0.08f);
            float inner = skin.Min(v => MathF.Abs(v.X));
            Assert.True(inner > capsuleSize.X / 2f + 0.03f, $"{hand} inner edge {inner}");
            Assert.True(inner > Width(dress, c.Y - 0.1f, c.Y + 0.1f) / 2f + 0.03f, $"{hand} inner edge {inner}");
            // ... each with two or three short finger lines on it.
            var fingers = Pieces(glb.Vertices(hand, "Finger"), 0.012f);
            Assert.InRange(fingers.Count, 2, 3);
            foreach (var finger in fingers)
            {
                Assert.InRange(Size(finger).Y, 0.02f, 0.08f);
                Assert.True(Size(finger).X < 0.025f);
                Assert.True(finger.All(v => Vector3.Distance(v, c) < 0.1f));
            }
        }

        foreach (var (leg, shin) in new[] { ("LegL", "ShinL"), ("LegR", "ShinR") })
        {
            // Plain straight tubes (0.06 m radius) from under the body ...
            var thigh = glb.Vertices(leg);
            Assert.InRange(Size(thigh).X, 0.11f, 0.13f);
            Assert.InRange(Size(thigh).Z, 0.11f, 0.13f);
            Assert.InRange(thigh.Max(v => v.Y), bottom, bottom + 0.2f);
            Assert.True(thigh.All(v => MathF.Abs(v.X) < 0.2f), "under the dress");
            // ... down to rounded feet a little wider than the legs, on the ground.
            var lower = glb.Vertices(shin);
            Assert.InRange(lower.Min(v => v.Y), -0.01f, 0.01f);
            float tube = Size(lower.Where(v => v.Y > 0.15f).ToList()).X, foot = Size(lower.Where(v => v.Y < 0.1f).ToList()).X;
            Assert.InRange(tube, 0.11f, 0.13f);
            Assert.InRange(foot, tube + 0.01f, tube * 1.5f);
        }
    }

    [Fact]
    public void TheFolkHeadHasOnlyTwoBigDarkEyes()
    {
        var glb = new Glb("folk");
        // No mouth, nose, ears or hair: the head is skin, two eyes with sparkles and small cheeks.
        Assert.Equal(new[] { "Blush", "Eye", "EyeShine", "Skin" }, glb.Materials("Head").Distinct().OrderBy(m => m, StringComparer.Ordinal));
        foreach (var gone in new[] { "Mouth", "Hair", "Nose" })
            Assert.DoesNotContain(gone, glb.AllMaterials);
        Assert.DoesNotContain(glb.Nodes, n => n.Name.StartsWith("Hair", StringComparison.Ordinal) || n.Name.StartsWith("Foot", StringComparison.Ordinal));
        var head = glb.Vertices("Head", "Skin");
        var headCenter = Center(head);
        float headR = Size(head).X / 2f;
        Assert.True(glb.Color("Eye").Length() < 0.15f, "dark eyes");
        Assert.True(glb.Color("EyeShine").X > 0.9f && glb.Color("EyeShine").Y > 0.9f && glb.Color("EyeShine").Z > 0.9f);
        var eyes = Pieces(glb.Vertices("Head", "Eye"), 0.01f);
        Assert.Equal(2, eyes.Count);
        foreach (var eye in eyes)
        {
            // Big ovals, taller than wide, on the front, set low-middle and wide apart.
            var size = Size(eye);
            var c = Center(eye);
            Assert.InRange(size.Y, 0.08f, 0.13f);
            Assert.True(size.Y > size.X * 1.2f);
            Assert.True(c.Z < headCenter.Z - headR * 0.7f, "on the front");
            Assert.InRange(c.Y, headCenter.Y - headR * 0.5f, headCenter.Y);
            Assert.InRange(MathF.Abs(c.X), headR * 0.3f, headR * 0.6f);
            // A white sparkle on each.
            Assert.Contains(glb.Vertices("Head", "EyeShine"), v => Vector3.Distance(v, c) < size.Y / 2f);
        }
        Assert.True(eyes[0].Average(v => v.X) * eyes[1].Average(v => v.X) < 0f, "one each side");
        // Small soft cheeks.
        foreach (var cheek in Pieces(glb.Vertices("Head", "Blush"), 0.01f))
            Assert.True(Size(cheek).X < 0.08f && Size(cheek).Y < 0.05f);
    }

    [Fact]
    public void HatsAndTheBowSitOnTheHeadWithoutFaces()
    {
        var glb = new Glb("folk");
        float eyesTop = glb.Vertices("Head", "Eye").Max(v => v.Y);
        float headTop = glb.Vertices("Head", "Skin").Max(v => v.Y);
        foreach (var hat in Hats)
        {
            Assert.Equal("Head", glb.ParentOf(hat));
            Assert.Equal(new[] { "Gills", "Hat", "HatSpots" }, glb.Materials(hat).Distinct().OrderBy(m => m, StringComparer.Ordinal));
            var v = glb.Vertices(hat);
            Assert.True(v.Min(p => p.Y) > eyesTop, hat);
            Assert.True(v.Max(p => p.Y) > headTop + 0.03f, hat);
        }
        Assert.Equal("Head", glb.ParentOf("Bow"));
        Assert.Equal(new[] { "Bow" }, glb.Materials("Bow"));
        Assert.True(glb.Vertices("Bow").Min(p => p.Y) > eyesTop);
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
