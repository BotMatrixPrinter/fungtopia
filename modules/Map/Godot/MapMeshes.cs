using System;
using System.Collections.Generic;
using Godot;

namespace Fungtopia.Map.View;

/// <summary>
/// Builds the map's chunky meshes in code (the same lathed mushroom stems and caps as the
/// forest's, one cap mesh per <see cref="CapShape"/>, plus hedge blobs). Every mesh carries UV.y as
/// a part mask for its shader (1 the colored cap, 0 the pale gills or underside). Caps are about
/// radius 1 and height 1 with their base at y = 0.
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

    /// <summary>The cap mesh of <paramref name="shape"/>.</summary>
    public static ArrayMesh Cap(CapShape shape) => shape switch
    {
        CapShape.Dome => Cap(),
        CapShape.Flat => Flat(),
        CapShape.Funnel => Funnel(),
        CapShape.Bell => Bell(),
        CapShape.Wavy => Wavy(),
        CapShape.Cone => Cone(),
        CapShape.Ball => Ball(),
        CapShape.Cup => Cup(),
        CapShape.Coral => Coral(),
        CapShape.Jelly => Jelly(),
        CapShape.Shelf => Shelf(),
        _ => throw new ArgumentOutOfRangeException(nameof(shape)),
    };

    private static readonly Vector2[] Gills = { new(0.93f, 0.02f), new(0.6f, 0.1f), new(0.25f, 0.2f), new(0f, 0.24f) };

    /// <summary>Top profile point i of n on a curve from the crown (0, top) out to the rim.</summary>
    private static Vector2[] Profile(int n, Func<float, Vector2> at)
    {
        var points = new Vector2[n + 1];
        for (int i = 0; i <= n; i++)
            points[i] = at(i / (float)n);
        return points;
    }

    private static ArrayMesh Capped(Vector2[] top, Vector2[] under, Func<float, int, Vector2>? wobble = null)
    {
        var b = new Builder();
        b.Lathe(top, (_, _) => 1f, wobble);
        b.Lathe(under, (_, _) => 0f, wobble);
        return b.Commit();
    }

    /// <summary>A flat parasol cap with a little knob in the middle.</summary>
    private static ArrayMesh Flat() => Capped(
        Profile(12, t => new Vector2(t, 0.15f + 0.85f * (1f - MathF.Pow(t, 0.6f)) * (t < 0.2f ? 1f : 0.55f + 0.45f * MathF.Cos((t - 0.2f) * 2f)))),
        new[] { new Vector2(1f, 0.15f), new Vector2(0.92f, 0.04f), new Vector2(0.5f, 0.08f), new Vector2(0f, 0.12f) });

    /// <summary>A golden funnel: dipped in the middle, the rim turned up and out, ridged gills running down.</summary>
    private static ArrayMesh Funnel() => Capped(
        Profile(10, t => new Vector2(t, 0.62f + 0.38f * t * t - 0.06f * MathF.Sin(t * MathF.PI))),
        new[] { new Vector2(1f, 1f), new Vector2(0.96f, 0.86f), new Vector2(0.6f, 0.45f), new Vector2(0.28f, 0.12f), new Vector2(0f, 0f) });

    /// <summary>A tall bell (ink caps, glowing bells): rounded crown, straight-ish sides, the rim flaring a little.</summary>
    private static ArrayMesh Bell() => Capped(
        Profile(14, t =>
        {
            float a = MathF.Min(t * 2.2f, 1f) * MathF.PI * 0.5f;
            float r = MathF.Sin(a) * 0.82f + 0.18f * MathF.Pow(t, 4f);
            return new Vector2(r, 1f - t);
        }),
        new[] { new Vector2(1f, 0f), new Vector2(0.7f, 0.08f), new Vector2(0.3f, 0.3f), new Vector2(0f, 0.36f) });

    /// <summary>A dome opening wide, its rim rippling in soft waves.</summary>
    private static ArrayMesh Wavy() => Capped(
        Profile(12, t => new Vector2(t, 0.1f + 0.9f * MathF.Cos(t * MathF.PI * 0.5f))),
        Gills,
        (a, i) => new Vector2(1f + 0.06f * MathF.Sin(a * 7f) * (i / 12f), 0.1f * MathF.Sin(a * 7f) * MathF.Pow(MathF.Min(i, 12) / 12f, 2f)));

    /// <summary>A morel's pointed cone (the shader pits it like honeycomb).</summary>
    private static ArrayMesh Cone() => Capped(
        Profile(12, t => new Vector2(0.72f * MathF.Sin(MathF.Min(t * 1.4f, 1f) * MathF.PI * 0.5f) * MathF.Pow(t, 0.35f) + 0.28f * t, 1f - t * 0.96f)),
        new[] { new Vector2(1f, 0.04f), new Vector2(0.5f, 0.02f), new Vector2(0f, 0.02f) });

    /// <summary>A round puffball with a little pointed top, sitting on y = 0.</summary>
    private static ArrayMesh Ball()
    {
        var b = new Builder();
        b.Lathe(Profile(12, t =>
        {
            float a = t * MathF.PI;
            return new Vector2(MathF.Sin(a), 0.5f + 0.5f * MathF.Cos(a) + 0.08f * MathF.Pow(1f - t, 8f));
        }), (_, _) => 1f);
        return b.Commit();
    }

    /// <summary>A scarlet cup: pale outside (UV.y 0), the bright inside (1) open to the sky, a wavy rim.</summary>
    private static ArrayMesh Cup()
    {
        var b = new Builder();
        Vector2 Rim(float a, int i) => new(1f, i == 0 ? 0.05f * MathF.Sin(a * 5f) : 0f);
        // Outside from the rim down (outward faces), inside from the bottom up (faces toward the middle).
        b.Lathe(Profile(10, t => new Vector2(0.12f + 0.88f * MathF.Cos(t * MathF.PI * 0.5f), 1f - t)), (_, _) => 0f, Rim);
        b.Lathe(Profile(8, t => new Vector2(0.98f * MathF.Sin(t * MathF.PI * 0.5f), 0.3f + 0.7f * t * t)), (_, _) => 1f,
            (a, i) => new Vector2(1f, i == 8 ? 0.05f * MathF.Sin(a * 5f) : 0f));
        return b.Commit();
    }

    /// <summary>A tapering finger of radius 1 at its foot and height 1, rounded at the tip.</summary>
    private static readonly Vector2[] Finger =
        { new(0f, 1f), new(0.45f, 0.97f), new(0.62f, 0.85f), new(0.7f, 0.5f), new(0.85f, 0.15f), new(1f, 0f) };

    /// <summary>Branching coral fungus: a bunch of fingers fanning up from one foot, each forking.</summary>
    private static ArrayMesh Coral()
    {
        var b = new Builder();
        void Branch(Vector3 foot, Vector3 axis, float length, float radius)
        {
            var basis = new Basis(new Quaternion(Vector3.Up, axis.Normalized())) * Basis.FromScale(new Vector3(radius, length, radius));
            b.Lathe(Finger, (_, _) => 1f, null, new Transform3D(basis, foot));
        }
        for (int i = 0; i < 7; i++)
        {
            float a = i * MathF.Tau / 7f + 0.4f * MathF.Sin(i * 2.3f);
            float lean = i == 0 ? 0f : 0.35f + 0.12f * (i % 3);
            var dir = new Vector3(MathF.Sin(a) * lean, 1f, MathF.Cos(a) * lean).Normalized();
            float length = i == 0 ? 0.9f : 0.6f + 0.06f * (i % 4);
            Branch(Vector3.Zero, dir, length, 0.16f);
            // Two short forks out of each branch's upper half.
            var mid = dir * length * 0.55f;
            for (int f = -1; f <= 1; f += 2)
            {
                var side = new Vector3(MathF.Cos(a) * f, 0f, -MathF.Sin(a) * f);
                Branch(mid, (dir + side * 0.55f).Normalized(), length * 0.5f, 0.09f);
            }
        }
        return b.Commit();
    }

    /// <summary>A jelly fungus: a few glossy wobbly lobes like ears, bunched together.</summary>
    private static ArrayMesh Jelly()
    {
        var b = new Builder();
        var lobe = Profile(10, t =>
        {
            float a = t * MathF.PI;
            return new Vector2(MathF.Sin(a), 0.5f * (1f + MathF.Cos(a)));
        });
        for (int i = 0; i < 4; i++)
        {
            float a = i * MathF.Tau / 4f + 0.5f;
            var basis = new Basis(new Vector3(MathF.Cos(a), 0f, -MathF.Sin(a)), 0.5f) * Basis.FromScale(new Vector3(0.55f, 0.75f - 0.08f * i, 0.32f));
            b.Lathe(lobe, (_, _) => 1f, (an, _) => new Vector2(1f + 0.12f * MathF.Sin(an * 3f + i), 0f),
                new Transform3D(basis, new Vector3(MathF.Sin(a) * 0.38f, 0.02f, MathF.Cos(a) * 0.38f)));
        }
        return b.Commit();
    }

    /// <summary>A shelf fungus: a flat lens, half of it buried in the stem it grows on.</summary>
    private static ArrayMesh Shelf() => Capped(
        Profile(8, t => new Vector2(t, 0.5f + 0.5f * MathF.Cos(t * MathF.PI * 0.5f))),
        new[] { new Vector2(1f, 0.5f), new Vector2(0.95f, 0.2f), new Vector2(0.5f, 0.02f), new Vector2(0f, 0f) },
        (a, _) => new Vector2(1f + 0.05f * MathF.Sin(a * 6f), 0f));

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
        public void Lathe(Vector2[] profile, Func<int, int, float> mask, Func<float, int, Vector2>? wobble = null, Transform3D? place = null)
        {
            var xf = place ?? Transform3D.Identity;
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
                    // wobble(angle, i): a radius factor and a height offset (wavy rims, lobes).
                    var wob = wobble?.Invoke(a, i) ?? new Vector2(1f, 0f);
                    Add(xf * new Vector3(profile[i].X * wob.X * c, profile[i].Y + wob.Y, profile[i].X * wob.X * sn),
                        (xf.Basis * new Vector3(normal2.X * c, normal2.Y, normal2.X * sn)).Normalized(),
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
