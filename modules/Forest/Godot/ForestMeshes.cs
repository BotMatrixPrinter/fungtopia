using System;
using System.Collections.Generic;
using Godot;

namespace Fungtopia.Forest.View;

/// <summary>
/// Builds the forest's chunky meshes in code: lathed mushroom stems and caps, and
/// ferns, flowers and grass tufts. Every mesh carries UV.y as a part mask or root-to-tip ramp for its shader.
/// </summary>
internal static class ForestMeshes
{
    private const int Segments = 28;

    /// <summary>A fat stem of height 1 bulging to <see cref="ForestScenery.StemFootRadius"/> at the foot (<see cref="ForestScenery.StemProfile"/>): never wider than its circle.</summary>
    public static ArrayMesh Stem()
    {
        var profile = new List<Vector2> { new(0f, 1f) };
        for (int i = 10; i >= 0; i--)
        {
            float t = i / 10f;
            profile.Add(new Vector2(ForestScenery.StemProfile(t), t));
        }
        var b = new Builder();
        b.Lathe(profile.ToArray(), (i, n) => profile[i].Y);
        return b.Commit();
    }

    /// <summary>A root flare of height 1 widening to radius 1 at its base (<see cref="ForestScenery.SkirtProfile"/>), sunk into the ground round a stem's foot.</summary>
    public static ArrayMesh Skirt()
    {
        var profile = new List<Vector2>();
        for (int i = 12; i >= 0; i--)
        {
            float t = i / 12f;
            profile.Add(new Vector2(ForestScenery.SkirtProfile(t), t));
        }
        var b = new Builder();
        b.Lathe(profile.ToArray(), (i, n) => profile[i].Y * 0.3f);
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

    /// <summary>The cap (or fungus body) mesh for <paramref name="shape"/>: radius about 1, height 1, its origin at the bottom middle.</summary>
    public static ArrayMesh Cap(CapShape shape) => shape switch
    {
        CapShape.Flat => Capped(new[] { new Vector2(0f, 1f), new(0.12f, 0.97f), new(0.22f, 0.78f), new(0.45f, 0.6f), new(0.75f, 0.42f), new(0.95f, 0.24f), new(1f, 0.12f), new(0.97f, 0.04f) },
            new[] { new Vector2(0.97f, 0.04f), new(0.6f, 0.1f), new(0.2f, 0.16f), new(0f, 0.18f) }),
        // A funnel: a hollow middle, the rim up and wavy, ridges running down under it to the stem.
        CapShape.Funnel => Capped(new[] { new Vector2(0f, 0.6f), new(0.3f, 0.66f), new(0.6f, 0.8f), new(0.86f, 0.97f), new(1f, 1f), new(1.03f, 0.9f) },
            new[] { new Vector2(1.03f, 0.9f), new(0.75f, 0.6f), new(0.45f, 0.3f), new(0.2f, 0.08f), new(0f, 0f) },
            (a, t) => 1f + 0.07f * MathF.Sin(a * 5f) * t),
        CapShape.Bell => Capped(new[] { new Vector2(0f, 1f), new(0.3f, 0.98f), new(0.55f, 0.9f), new(0.72f, 0.72f), new(0.8f, 0.48f), new(0.85f, 0.22f), new(0.92f, 0.04f), new(0.88f, 0f) },
            new[] { new Vector2(0.88f, 0f), new(0.5f, 0.08f), new(0f, 0.12f) }),
        CapShape.Cone => Capped(new[] { new Vector2(0f, 1f), new(0.18f, 0.95f), new(0.42f, 0.82f), new(0.62f, 0.62f), new(0.78f, 0.38f), new(0.86f, 0.15f), new(0.8f, 0f) },
            new[] { new Vector2(0.8f, 0f), new(0f, 0.06f) }),
        // A low dome with a wavy, ruffled rim (amethysts; jelly blobs).
        CapShape.Wavy => Capped(Dome(), new[] { new Vector2(0.93f, 0.02f), new(0.6f, 0.1f), new(0.25f, 0.2f), new(0f, 0.24f) },
            (a, t) => 1f + 0.1f * MathF.Sin(a * 7f) * t * t),
        // A puffball: a ball on the ground, a little pear-shaped, all top.
        CapShape.Ball => Capped(new[] { new Vector2(0f, 1f), new(0.45f, 0.94f), new(0.8f, 0.76f), new(0.98f, 0.5f), new(0.92f, 0.26f), new(0.65f, 0.08f), new(0.35f, 0f), new(0f, 0f) },
            Array.Empty<Vector2>()),
        CapShape.Cup => CupMesh(),
        CapShape.Coral => CoralMesh(),
        _ => Cap(),
    };

    /// <summary>The domed cap's top profile: a squashed dome whose rim curls under a little.</summary>
    private static Vector2[] Dome()
    {
        var top = new List<Vector2> { new(0f, 1f) };
        for (int i = 0; i <= 12; i++)
        {
            float a = i / 12f * MathF.PI * 0.5f;
            top.Add(new Vector2(MathF.Sin(a) * (1f - 0.04f * MathF.Pow(i / 12f, 6f)), 0.12f + 0.88f * MathF.Cos(a)));
        }
        top.Add(new Vector2(0.93f, 0.02f));
        return top.ToArray();
    }

    /// <summary>A lathed cap: its top (UV.y 1) and underside (0) profiles, top to bottom, the radius waved by wave(angle, part of the way down the top).</summary>
    private static ArrayMesh Capped(Vector2[] top, Vector2[] under, Func<float, float, float>? wave = null)
    {
        var b = new Builder();
        b.Lathe(top, (_, _) => 1f, wave == null ? null : (a, i) => wave(a, i / (float)(top.Length - 1)));
        if (under.Length > 0)
            b.Lathe(under, (_, _) => 0f, wave == null ? null : (a, i) => wave(a, 1f - i / (float)(under.Length - 1)));
        return b.Commit();
    }

    /// <summary>A scarlet elf cup: open to the sky, bright inside (UV.y 1), paler outside (0).</summary>
    private static ArrayMesh CupMesh()
    {
        var b = new Builder();
        Func<float, int, float> wave = (a, _) => 1f + 0.06f * MathF.Sin(a * 6f);
        b.Lathe(new[] { new Vector2(1f, 1f), new(0.95f, 0.8f), new(0.75f, 0.45f), new(0.45f, 0.15f), new(0.2f, 0.02f), new(0f, 0f) }, (_, _) => 0f, wave);
        // The inside runs from the middle up to the rim, so its faces look up and in.
        b.Lathe(new[] { new Vector2(0f, 0.3f), new(0.4f, 0.42f), new(0.7f, 0.65f), new(0.9f, 0.92f), new(1f, 1f) }, (_, _) => 1f, wave);
        return b.Commit();
    }

    /// <summary>A coral fungus: a bush of tapering branches forking up from one foot, about 1 across in radius and 1 tall.</summary>
    private static ArrayMesh CoralMesh()
    {
        var b = new Builder();
        void Branch(Vector3 from, Vector3 dir, float length, float radius, int depth, int seed)
        {
            var to = from + dir * length;
            b.Tube(from, to, radius, radius * 0.7f);
            if (depth == 0)
            {
                b.Knob(to, radius * 0.9f);
                return;
            }
            int forks = depth == 3 ? 4 : 2 + seed % 2;
            for (int k = 0; k < forks; k++)
            {
                float a = k * MathF.Tau / forks + seed * 1.3f;
                var side = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                var next = (dir + side * (depth == 3 ? 0.9f : 0.55f)).Normalized();
                Branch(to, next, length * 0.78f, radius * 0.7f, depth - 1, seed * 3 + k + 1);
            }
        }
        Branch(Vector3.Zero, Vector3.Up, 0.33f, 0.2f, 3, 1);
        return b.Commit();
    }

    /// <summary>A shelf fungus: a half disc reaching out along +X from its back edge (x = 0, z -1..1), domed on top (UV.y 1), flatter below (0), y 0..1.</summary>
    public static ArrayMesh Shelf()
    {
        var b = new Builder();
        const int rings = 6, steps = 14;
        foreach (bool top in new[] { true, false })
        {
            int start = b.Count;
            for (int i = 0; i <= rings; i++)
            for (int j = 0; j <= steps; j++)
            {
                float rho = i / (float)rings, phi = (j / (float)steps - 0.5f) * MathF.PI;
                float x = rho * MathF.Cos(phi), z = rho * MathF.Sin(phi);
                float bulge = 1f - rho * rho;
                var v = new Vector3(x, top ? 0.45f + 0.55f * bulge : 0.45f - 0.3f * bulge, z);
                var n = top ? new Vector3(x * 1.1f, 1f, z * 1.1f).Normalized() : new Vector3(x * 0.6f, -1f, z * 0.6f).Normalized();
                b.Add(v, n, new Vector2(j / (float)steps, top ? 1f : 0f));
            }
            for (int i = 0; i < rings; i++)
            for (int j = 0; j < steps; j++)
            {
                int a = start + i * (steps + 1) + j, outward = a + steps + 1, along = a + 1;
                // Counter-clockwise seen from above for the top, from below for the underside.
                if (top)
                {
                    b.Tri(a, along, outward);
                    b.Tri(along, outward + 1, outward);
                }
                else
                {
                    b.Tri(a, outward, along);
                    b.Tri(along, outward, outward + 1);
                }
            }
        }
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

    /// <summary>A little flower 1 tall: a stem blade (UV.y 0..0.5) and five flat petals around its top (UV.y 1), 0.6 across in radius.</summary>
    public static ArrayMesh Flower()
    {
        var b = new Builder();
        var top = Vector3.Up * 0.92f;
        b.Add(new Vector3(-0.04f, 0f, 0f), Vector3.Up, new Vector2(0f, 0f));
        b.Add(new Vector3(0.04f, 0f, 0f), Vector3.Up, new Vector2(1f, 0f));
        b.Add(top, Vector3.Up, new Vector2(0.5f, 0.5f));
        b.Tri(0, 2, 1);
        const int petals = 5;
        for (int k = 0; k < petals; k++)
        {
            float yaw = k * MathF.Tau / petals;
            var dir = new Vector3(MathF.Cos(yaw), 0.12f, MathF.Sin(yaw));
            var side = new Vector3(-MathF.Sin(yaw), 0f, MathF.Cos(yaw)) * 0.16f;
            int i = b.Count;
            b.Add(top, Vector3.Up, new Vector2(0.5f, 1f));
            b.Add(top + dir * 0.3f - side, Vector3.Up, new Vector2(0f, 1f));
            b.Add(top + dir * 0.55f, Vector3.Up, new Vector2(0.5f, 1f));
            b.Add(top + dir * 0.3f + side, Vector3.Up, new Vector2(1f, 1f));
            b.Tri(i, i + 2, i + 1);
            b.Tri(i, i + 3, i + 2);
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
        public void Lathe(Vector2[] profile, Func<int, int, float> mask, Func<float, int, float>? wave = null)
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
                    float w = wave?.Invoke(a, i) ?? 1f;
                    Add(new Vector3(profile[i].X * w * c, profile[i].Y, profile[i].X * w * sn),
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

        /// <summary>A tapering tube from <paramref name="from"/> (radius r0) to <paramref name="to"/> (r1), faces outward, UV.y 1.</summary>
        public void Tube(Vector3 from, Vector3 to, float r0, float r1)
        {
            const int sides = 7;
            var d = (to - from).Normalized();
            var u = (Mathf.Abs(d.Y) < 0.9f ? Vector3.Up.Cross(d) : Vector3.Right.Cross(d)).Normalized();
            // (u, d, v) turn like (X, Y, Z), as the lathe's rings do.
            var v = u.Cross(d);
            int start = Count;
            foreach (var (center, r) in new[] { (to, r1), (from, r0) })
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * MathF.Tau;
                    var n = u * MathF.Cos(a) + v * MathF.Sin(a);
                    Add(center + n * r, n, new Vector2(s / (float)sides, 1f));
                }
            for (int s = 0; s < sides; s++)
            {
                int a = start + s, bb = a + sides + 1;
                Tri(a, a + 1, bb);
                Tri(a + 1, bb + 1, bb);
            }
        }

        /// <summary>A small round knob (a branch tip) of radius <paramref name="r"/> at <paramref name="center"/>.</summary>
        public void Knob(Vector3 center, float r)
        {
            var profile = new Vector2[7];
            for (int i = 0; i < profile.Length; i++)
            {
                float a = i / (float)(profile.Length - 1) * MathF.PI;
                profile[i] = new Vector2(MathF.Sin(a) * r, MathF.Cos(a) * r);
            }
            int start = Count;
            Lathe(profile, (_, _) => 1f);
            for (int i = start; i < Count; i++)
                _vertices[i] += center;
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
