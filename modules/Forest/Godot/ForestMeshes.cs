using System;
using System.Collections.Generic;
using Godot;

namespace Fungtopia.Forest.View;

/// <summary>
/// Builds the forest's chunky meshes in code: lathed mushroom stems, caps, eyes and logs, and
/// fern and grass tufts. Every mesh carries UV.y as a part mask or root-to-tip ramp for its shader.
/// </summary>
internal static class ForestMeshes
{
    private const int Segments = 28;

    /// <summary>A fat stem of radius about 1 and height 1, bulging at the foot.</summary>
    public static ArrayMesh Stem()
    {
        var profile = new List<Vector2> { new(0f, 1f) };
        for (int i = 10; i >= 0; i--)
        {
            float t = i / 10f;
            float r = 1f + 0.4f * MathF.Pow(1f - t, 4f) - 0.14f * t;
            profile.Add(new Vector2(r, t));
        }
        var b = new Builder();
        b.Lathe(profile.ToArray(), (i, n) => profile[i].Y);
        return b.Commit();
    }

    /// <summary>A domed cap of radius 1 and height about 1: spotted top (UV.y = 1) and gills below (0).</summary>
    public static ArrayMesh Cap()
    {
        var top = new List<Vector2>();
        for (int i = 0; i <= 12; i++)
        {
            float a = i / 12f * MathF.PI * 0.5f;
            // A squashed dome whose rim curls under a little.
            top.Add(new Vector2(MathF.Sin(a) * (1f - 0.04f * MathF.Pow(i / 12f, 6f)), 0.12f + 0.88f * MathF.Cos(a)));
        }
        top.Insert(0, new Vector2(0f, 1f));
        top.Add(new Vector2(0.93f, 0.02f));
        var under = new[] { new Vector2(0.93f, 0.02f), new Vector2(0.6f, 0.1f), new Vector2(0.25f, 0.2f), new Vector2(0f, 0.24f) };
        var b = new Builder();
        b.Lathe(top.ToArray(), (_, _) => 1f);
        b.Lathe(under, (_, _) => 0f);
        return b.Commit();
    }

    /// <summary>A shallow eye dome of radius 1 around +Y.</summary>
    public static ArrayMesh Eye()
    {
        var profile = new List<Vector2>();
        for (int i = 0; i <= 6; i++)
        {
            float a = i / 6f * MathF.PI * 0.5f;
            profile.Add(new Vector2(MathF.Sin(a), 0.35f * MathF.Cos(a)));
        }
        profile.Insert(0, new Vector2(0f, 0.35f));
        var b = new Builder();
        b.Lathe(profile.ToArray(), (_, _) => 1f);
        return b.Commit();
    }

    /// <summary>A log: a cylinder of radius 1 and length 1 along Y, centered, with cut ends (UV.y = 0).</summary>
    public static ArrayMesh Log()
    {
        var b = new Builder();
        b.Lathe(new[] { new Vector2(0f, 0.5f), new Vector2(1f, 0.5f) }, (_, _) => 0f);
        b.Lathe(new[] { new Vector2(1f, 0.5f), new Vector2(1.03f, 0f), new Vector2(1f, -0.5f) }, (_, _) => 1f);
        b.Lathe(new[] { new Vector2(1f, -0.5f), new Vector2(0f, -0.5f) }, (_, _) => 0f);
        return b.Commit();
    }

    /// <summary>A fern: fronds arching out from the root, about 1 m wide.</summary>
    public static ArrayMesh Fern()
    {
        var b = new Builder();
        const int fronds = 7, steps = 6;
        for (int f = 0; f < fronds; f++)
        {
            float yaw = f * MathF.Tau / fronds + 0.3f * MathF.Sin(f * 2.1f);
            var along = new Vector3(MathF.Cos(yaw), 0f, MathF.Sin(yaw));
            var side = new Vector3(-along.Z, 0f, along.X);
            int start = b.Count;
            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                // An arch: up and out, drooping at the tip; leafy width peaks mid-frond.
                var spine = along * (0.55f * t) + Vector3.Up * (0.5f * MathF.Sin(t * MathF.PI * 0.8f));
                float width = 0.16f * MathF.Sin(MathF.PI * MathF.Min(t * 1.1f, 1f));
                var normal = (Vector3.Up - along * 0.3f).Normalized();
                b.Add(spine - side * width, normal, new Vector2(0f, t));
                b.Add(spine + side * width, normal, new Vector2(1f, t));
            }
            for (int s = 0; s < steps; s++)
            {
                int i = start + s * 2;
                b.Tri(i, i + 2, i + 1);
                b.Tri(i + 1, i + 2, i + 3);
            }
        }
        return b.Commit();
    }

    /// <summary>A grass tuft: a few soft blades about 0.3 m tall.</summary>
    public static ArrayMesh GrassTuft()
    {
        var b = new Builder();
        const int blades = 6;
        for (int k = 0; k < blades; k++)
        {
            float yaw = k * 2.4f;
            var dir = new Vector3(MathF.Cos(yaw), 0f, MathF.Sin(yaw));
            var side = new Vector3(-dir.Z, 0f, dir.X) * 0.035f;
            var foot = dir * 0.05f;
            float h = 0.22f + 0.1f * ((k * 37) % 5) / 4f;
            var tip = dir * 0.12f + Vector3.Up * h;
            int i = b.Count;
            // Up-facing normals light the tuft like the ground it grows from.
            b.Add(foot - side, Vector3.Up, new Vector2(0f, 0f));
            b.Add(foot + side, Vector3.Up, new Vector2(1f, 0f));
            b.Add(tip, Vector3.Up, new Vector2(0.5f, 1f));
            b.Tri(i, i + 2, i + 1);
        }
        return b.Commit();
    }

    /// <summary>A unit quad facing +Z (for billboards).</summary>
    public static ArrayMesh Quad()
    {
        var b = new Builder();
        b.Add(new Vector3(-1, -1, 0), Vector3.Back, new Vector2(0, 1));
        b.Add(new Vector3(1, -1, 0), Vector3.Back, new Vector2(1, 1));
        b.Add(new Vector3(1, 1, 0), Vector3.Back, new Vector2(1, 0));
        b.Add(new Vector3(-1, 1, 0), Vector3.Back, new Vector2(0, 0));
        b.Tri(0, 1, 2);
        b.Tri(0, 2, 3);
        return b.Commit();
    }

    /// <summary>Collects vertices and triangles into one surface.</summary>
    private sealed class Builder
    {
        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector3> _normals = new();
        private readonly List<Vector2> _uvs = new();
        private readonly List<int> _indices = new();

        public int Count => _vertices.Count;

        public void Add(Vector3 v, Vector3 n, Vector2 uv)
        {
            _vertices.Add(v);
            _normals.Add(n);
            _uvs.Add(uv);
        }

        /// <summary>A triangle wound counter-clockwise seen from its front (Godot's front faces).</summary>
        public void Tri(int a, int b, int c)
        {
            _indices.Add(a);
            _indices.Add(c);
            _indices.Add(b);
        }

        /// <summary>
        /// Revolves a (radius, height) profile around Y, top to bottom for outward faces.
        /// mask(i, n) gives UV.y for profile point i.
        /// </summary>
        public void Lathe(Vector2[] profile, Func<int, int, float> mask)
        {
            int n = profile.Length;
            int start = Count;
            for (int i = 0; i < n; i++)
            {
                // Profile normal from the neighbouring points' tangent, pointing outward.
                var prev = profile[Math.Max(i - 1, 0)];
                var next = profile[Math.Min(i + 1, n - 1)];
                var tangent = next - prev;
                var normal2 = new Vector2(-tangent.Y, tangent.X).Normalized();
                for (int s = 0; s <= Segments; s++)
                {
                    float a = s / (float)Segments * MathF.Tau;
                    float c = MathF.Cos(a), sn = MathF.Sin(a);
                    Add(new Vector3(profile[i].X * c, profile[i].Y, profile[i].X * sn),
                        new Vector3(normal2.X * c, normal2.Y, normal2.X * sn),
                        new Vector2(s / (float)Segments, mask(i, n)));
                }
            }
            int row = Segments + 1;
            for (int i = 0; i + 1 < n; i++)
            for (int s = 0; s < Segments; s++)
            {
                int a = start + i * row + s, bb = a + row;
                Tri(a, a + 1, bb);
                Tri(a + 1, bb + 1, bb);
            }
        }

        public ArrayMesh Commit()
        {
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = _vertices.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = _normals.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = _uvs.ToArray();
            arrays[(int)Mesh.ArrayType.Index] = _indices.ToArray();
            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            return mesh;
        }
    }
}
