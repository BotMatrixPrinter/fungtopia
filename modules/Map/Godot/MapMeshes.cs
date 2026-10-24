using System;
using System.Collections.Generic;
using Godot;

namespace Fungtopia.Map.View;

/// <summary>
/// Builds the map's chunky meshes in code (the same lathed mushroom stems, caps and eyes as the
/// forest's, plus hedge blobs). Every mesh carries UV.y as a part mask for its shader.
/// </summary>
internal static class MapMeshes
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

    /// <summary>A round hedge blob: a slightly squashed ball of radius 1 sitting on y = 0.</summary>
    public static ArrayMesh Blob()
    {
        var profile = new List<Vector2>();
        for (int i = 0; i <= 10; i++)
        {
            float a = i / 10f * MathF.PI;
            profile.Add(new Vector2(MathF.Sin(a), 0.9f * (1f - MathF.Cos(a))));
        }
        profile.Reverse();
        var b = new Builder();
        b.Lathe(profile.ToArray(), (i, n) => 1f - i / (float)(n - 1));
        return b.Commit();
    }

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
