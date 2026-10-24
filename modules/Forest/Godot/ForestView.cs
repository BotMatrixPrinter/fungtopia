using System;
using System.Collections.Generic;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.Forest.View;

/// <summary>
/// Draws the Mushroom Forest from its Contract: the ground mesh from <see cref="Terrain.HeightAt"/>,
/// a giant mushroom on every <see cref="Obstacles"/> circle, the wall of giant mushrooms around
/// <see cref="ForestBounds"/>, and instanced small props (mushroom clusters, ferns, grass, logs,
/// fireflies). Also a warm late-afternoon light, light fog, and a preview camera at the player's
/// start that any other current camera replaces.
/// </summary>
public partial class ForestView : Node3D, IModuleView
{
    private const string Shaders = "res://modules/Forest/Godot/Shaders/";

    /// <summary>The ground mesh reaches this far past the walkable square, under the mushroom wall.</summary>
    private const float GroundBorder = 40f;

    private const float EyeHeight = 1.6f;

    private static readonly Color[] CapColors =
    {
        new(0.88f, 0.24f, 0.22f), new(0.93f, 0.36f, 0.26f), new(0.8f, 0.18f, 0.24f),
        new(0.62f, 0.4f, 0.24f), new(0.72f, 0.5f, 0.3f), new(0.55f, 0.33f, 0.22f),
        new(0.6f, 0.36f, 0.72f), new(0.72f, 0.42f, 0.78f),
    };

    private static readonly Color[] GlowColors =
    {
        new(0.62f, 0.45f, 0.95f), new(0.95f, 0.5f, 0.85f), new(0.4f, 0.8f, 0.9f),
    };

    private ForestModule? _forest;

    public void Bind(IModule module) => _forest = (ForestModule)module;

    public override void _Ready()
    {
        BuildEnvironment();
        BuildGround();
        BuildMushrooms();
        BuildFoliage();
        BuildLogs();
        BuildFireflies();
        BuildPreviewCamera();
    }

    /// <summary>A fixed pseudo-random value in [0, 1) for (i, k): the same scene every run.</summary>
    private static float Rand(int i, int k)
    {
        uint h = unchecked((uint)i * 2654435761u ^ (uint)k * 2246822519u ^ 0x5bd1e995u);
        h = (h ^ (h >> 15)) * 2246822519u;
        h = (h ^ (h >> 13)) * 3266489917u;
        h ^= h >> 16;
        return (h >> 8) * (1f / 16777216f);
    }

    private static Vector3 Ground(float x, float z) => new(x, Terrain.HeightAt(x, z), z);

    /// <summary>Whether a prop of <paramref name="radius"/> at (x, z) stays off paths and trunks.</summary>
    private static bool Open(float x, float z, float radius) =>
        ForestBounds.Contains(x, z, radius) && !Obstacles.Blocks(x, z, radius + 0.3f)
        && ForestLayout.PathDistance(x, z) > ForestLayout.PathHalfWidth + radius;

    private static ShaderMaterial Material(string shader, bool outline = false)
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>(Shaders + shader) };
        if (outline)
            material.NextPass = new ShaderMaterial { Shader = GD.Load<Shader>(Shaders + "outline.gdshader") };
        return material;
    }

    private void BuildEnvironment()
    {
        var sky = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color(0.5f, 0.62f, 0.9f),
            SkyHorizonColor = new Color(1.0f, 0.78f, 0.58f),
            GroundBottomColor = new Color(0.3f, 0.32f, 0.25f),
            GroundHorizonColor = new Color(0.95f, 0.78f, 0.62f),
            SunAngleMax = 30f,
        };
        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = sky },
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.62f, 0.55f, 0.72f),
            AmbientLightEnergy = 0.9f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            TonemapWhite = 6f,
            GlowEnabled = true,
            GlowIntensity = 0.6f,
            GlowBloom = 0.05f,
            FogEnabled = true,
            FogLightColor = new Color(0.86f, 0.74f, 0.7f),
            FogDensity = 0.009f,
            AdjustmentEnabled = true,
            AdjustmentSaturation = 1.2f,
            FogSkyAffect = 0.25f,
        };
        AddChild(new WorldEnvironment { Name = "WorldEnvironment", Environment = environment });

        // A low, warm late-afternoon sun from the west; the caps' shadows dapple the ground.
        var sun = new DirectionalLight3D
        {
            Name = "Sun",
            LightColor = new Color(1.0f, 0.84f, 0.62f),
            LightEnergy = 1.35f,
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 90f,
            RotationDegrees = new Vector3(-34f, -112f, 0f),
        };
        AddChild(sun);
    }

    private static Color GroundColor(float x, float z)
    {
        var grass = new Color(0.5f, 0.74f, 0.33f);
        var moss = new Color(0.3f, 0.56f, 0.32f);
        var meadow = new Color(0.66f, 0.8f, 0.38f);
        var dirt = new Color(0.7f, 0.52f, 0.36f);
        float mottle = 0.5f + 0.25f * MathF.Sin(x * 0.19f + MathF.Sin(z * 0.13f) * 2f) + 0.25f * MathF.Sin(z * 0.23f - x * 0.07f);
        var color = grass.Lerp(moss, Mathf.SmoothStep(0.35f, 0.85f, mottle));
        color = color.Lerp(meadow, Mathf.SmoothStep(-2f, 5f, ForestLayout.ClearingDepth(x, z)) * 0.8f);
        // Moss gathers outside the square and around trunk feet.
        float outside = MathF.Max(MathF.Abs(x), MathF.Abs(z)) - ForestBounds.HalfSize;
        color = color.Lerp(moss * 0.85f, Mathf.SmoothStep(-4f, 6f, outside));
        float path = 1f - Mathf.SmoothStep(ForestLayout.PathHalfWidth - 0.5f, ForestLayout.PathHalfWidth + 0.7f, ForestLayout.PathDistance(x, z));
        return color.Lerp(dirt, path);
    }

    private void BuildGround()
    {
        const float step = 1f;
        float extent = ForestBounds.HalfSize + GroundBorder;
        int n = (int)(2 * extent / step) + 1;
        var vertices = new Vector3[n * n];
        var normals = new Vector3[n * n];
        var colors = new Color[n * n];
        for (int i = 0; i < n; i++)
        for (int j = 0; j < n; j++)
        {
            float x = -extent + i * step, z = -extent + j * step;
            int k = i * n + j;
            vertices[k] = Ground(x, z);
            const float e = 0.25f;
            float dx = Terrain.HeightAt(x + e, z) - Terrain.HeightAt(x - e, z);
            float dz = Terrain.HeightAt(x, z + e) - Terrain.HeightAt(x, z - e);
            normals[k] = new Vector3(-dx, 2f * e, -dz).Normalized();
            colors[k] = GroundColor(x, z);
        }
        var indices = new int[(n - 1) * (n - 1) * 6];
        int t = 0;
        for (int i = 0; i + 1 < n; i++)
        for (int j = 0; j + 1 < n; j++)
        {
            int a = i * n + j, b = a + n;
            // Clockwise seen from above: Godot's front faces.
            indices[t++] = a; indices[t++] = b; indices[t++] = a + 1;
            indices[t++] = a + 1; indices[t++] = b; indices[t++] = b + 1;
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, Material("ground.gdshader"));
        AddChild(new MeshInstance3D { Name = "Ground", Mesh = mesh });
    }

    /// <summary>Instances of one mesh, drawn as a single MultiMesh.</summary>
    private sealed class Instances
    {
        public readonly List<Transform3D> Transforms = new();
        public readonly List<Color> Colors = new();
        public readonly List<Color> Custom = new();

        public void Add(Transform3D transform, Color color, Color custom = default)
        {
            Transforms.Add(transform);
            Colors.Add(color);
            Custom.Add(custom);
        }

        public MultiMeshInstance3D Build(string name, Mesh mesh, Material material, bool shadows = true)
        {
            var multi = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = true,
                UseCustomData = true,
                Mesh = mesh,
                InstanceCount = Transforms.Count,
            };
            for (int i = 0; i < Transforms.Count; i++)
            {
                multi.SetInstanceTransform(i, Transforms[i]);
                multi.SetInstanceColor(i, Colors[i]);
                multi.SetInstanceCustomData(i, Custom[i]);
            }
            return new MultiMeshInstance3D
            {
                Name = name,
                Multimesh = multi,
                MaterialOverride = material,
                CastShadow = shadows ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
            };
        }
    }

    private static Transform3D Placed(Vector3 position, float yaw, Vector3 scale) =>
        new(new Basis(Vector3.Up, yaw) * Basis.FromScale(scale), position);

    /// <summary>The yaw (angle from +X toward +Z) facing the nearest path from (x, z), so walkers see the eyes.</summary>
    private static float LookTowardPath(float x, float z)
    {
        const float e = 0.5f;
        float dx = ForestLayout.PathDistance(x + e, z) - ForestLayout.PathDistance(x - e, z);
        float dz = ForestLayout.PathDistance(x, z + e) - ForestLayout.PathDistance(x, z - e);
        return MathF.Atan2(-dz, -dx);
    }

    private void BuildMushrooms()
    {
        var stems = new Instances();
        var caps = new Instances();
        var eyes = new Instances();
        int seed = 0;

        void Mushroom(float x, float z, float stemRadius, float height, float capRadius, bool withEyes)
        {
            int id = seed++;
            float yaw = Rand(id, 1) * MathF.Tau;
            var foot = Ground(x, z) + Vector3.Down * MathF.Min(0.4f, height * 0.08f);
            var stemColor = new Color(0.97f, 0.91f, 0.8f).Lerp(new Color(0.93f, 0.82f, 0.7f), Rand(id, 2));
            stems.Add(Placed(foot, yaw, new Vector3(stemRadius, height, stemRadius)), stemColor);

            bool glow = Rand(id, 3) < 0.16f;
            var color = glow ? GlowColors[(int)(Rand(id, 4) * GlowColors.Length)] : CapColors[(int)(Rand(id, 4) * CapColors.Length)];
            color = color.Lightened(0.08f * (Rand(id, 5) - 0.5f));
            float capHeight = capRadius * (0.5f + 0.3f * Rand(id, 6));
            var tilt = new Basis(new Vector3(MathF.Cos(yaw), 0f, MathF.Sin(yaw)), 0.08f * (Rand(id, 7) - 0.5f));
            var capBasis = tilt * new Basis(Vector3.Up, yaw) * Basis.FromScale(new Vector3(capRadius, capHeight, capRadius));
            var capCustom = new Color(Rand(id, 8), glow ? 0.9f + 0.6f * Rand(id, 9) : 0f, 0f, 0f);
            caps.Add(new Transform3D(capBasis, foot + Vector3.Up * (height - capHeight * 0.18f)), color, capCustom);

            if (!withEyes)
                return;
            // A pair of big eyes on the stem, a little above the player's eye height.
            float eyeY = MathF.Min(height * 0.42f, 2.6f + stemRadius * 0.3f);
            float t = eyeY / height;
            float surface = stemRadius * (1f + 0.4f * MathF.Pow(1f - t, 4f) - 0.14f * t);
            float eyeSize = 0.3f + 0.16f * stemRadius;
            float spread = 0.62f * eyeSize / surface + 0.1f;
            float look = LookTowardPath(x, z);
            for (int side = -1; side <= 1; side += 2)
            {
                float a = look + side * spread;
                var outward = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                var basis = new Basis(new Quaternion(Vector3.Up, outward)) * Basis.FromScale(new Vector3(eyeSize, eyeSize * 0.8f, eyeSize));
                eyes.Add(new Transform3D(basis, foot + Vector3.Up * eyeY + outward * (surface - 0.05f)), Colors.White);
            }
        }

        // A giant mushroom on every obstacle, its stem exactly the obstacle's circle.
        foreach (var o in Obstacles.All)
        {
            int id = seed;
            float height = 6f + o.Radius * 2.6f + 4f * Rand(id, 20);
            Mushroom(o.X, o.Z, o.Radius, height, o.Radius * (2.6f + 1.2f * Rand(id, 21)), Rand(id, 22) < 0.45f);
        }

        // The wall: dense giant mushrooms in a band just outside the square.
        const float cell = 5.5f, depth = 26f;
        float outer = ForestBounds.HalfSize + depth;
        for (float gx = -outer; gx < outer; gx += cell)
        for (float gz = -outer; gz < outer; gz += cell)
        {
            int id = seed;
            float x = gx + cell * (0.15f + 0.7f * Rand(id, 30)), z = gz + cell * (0.15f + 0.7f * Rand(id, 31));
            float r = 1.1f + 1.5f * Rand(id, 32);
            if (ForestBounds.Contains(x, z, -(r + 0.5f)))
                continue; // inside the square (or touching it): walkers stay inside instead
            float height = 7f + 9f * Rand(id, 33);
            Mushroom(x, z, r, height, r * (2.4f + 1.4f * Rand(id, 34)), Rand(id, 35) < 0.12f);
        }

        // Small mushroom clusters in the open (walkable: not obstacles).
        for (int c = 0; c < 320; c++)
        {
            float cx = (Rand(c, 40) * 2f - 1f) * ForestBounds.HalfSize, cz = (Rand(c, 41) * 2f - 1f) * ForestBounds.HalfSize;
            if (!Open(cx, cz, 0.8f) && Rand(c, 47) > 0.2f)
                continue;
            int count = 3 + (int)(Rand(c, 42) * 5f);
            for (int k = 0; k < count; k++)
            {
                float a = k * 2.4f + Rand(c, 43) * 6f, d = 0.15f + 0.6f * Rand(c * 8 + k, 44);
                float x = cx + MathF.Cos(a) * d, z = cz + MathF.Sin(a) * d;
                if (!Open(x, z, 0.15f))
                    continue;
                float size = 0.12f + 0.3f * Rand(c * 8 + k, 45) * (k == 0 ? 1.6f : 1f);
                Mushroom(x, z, size * 0.35f, size * (1.2f + Rand(c * 8 + k, 46)), size, k == 0 && size > 0.3f);
            }
        }

        AddChild(stems.Build("MushroomStems", ForestMeshes.Stem(), Material("toon_color.gdshader", outline: true)));
        AddChild(caps.Build("MushroomCaps", ForestMeshes.Cap(), Material("mushroom_cap.gdshader", outline: true)));
        AddChild(eyes.Build("MushroomEyes", ForestMeshes.Eye(), Material("eye.gdshader", outline: true), shadows: false));
    }

    private void BuildFoliage()
    {
        var ferns = new Instances();
        // Ferns gather at trunk feet and along the paths' edges.
        int f = 0;
        foreach (var o in Obstacles.All)
        {
            for (int k = 0; k < 3; k++, f++)
            {
                float a = Rand(f, 50) * MathF.Tau, d = o.Radius + 0.6f + 1.4f * Rand(f, 51);
                float x = o.X + MathF.Cos(a) * d, z = o.Z + MathF.Sin(a) * d;
                if (Open(x, z, 0.5f))
                    ferns.Add(Placed(Ground(x, z), Rand(f, 52) * MathF.Tau, Vector3.One * (0.8f + 0.9f * Rand(f, 53))),
                        new Color(0.36f, 0.66f, 0.3f).Lerp(new Color(0.5f, 0.72f, 0.28f), Rand(f, 54)));
            }
        }
        for (int k = 0; k < 900; k++, f++)
        {
            float x = (Rand(f, 55) * 2f - 1f) * ForestBounds.HalfSize, z = (Rand(f, 56) * 2f - 1f) * ForestBounds.HalfSize;
            if (Open(x, z, 0.5f) && ForestLayout.ClearingDepth(x, z) < 1f)
                ferns.Add(Placed(Ground(x, z), Rand(f, 57) * MathF.Tau, Vector3.One * (0.7f + 0.8f * Rand(f, 58))),
                    new Color(0.34f, 0.62f, 0.3f).Lerp(new Color(0.5f, 0.72f, 0.28f), Rand(f, 59)));
        }
        var fernMaterial = Material("foliage.gdshader");
        AddChild(ferns.Build("Ferns", ForestMeshes.Fern(), fernMaterial));

        var grass = new Instances();
        for (int k = 0; k < 40000; k++)
        {
            float x = (Rand(k, 60) * 2f - 1f) * (ForestBounds.HalfSize + 6f), z = (Rand(k, 61) * 2f - 1f) * (ForestBounds.HalfSize + 6f);
            if (ForestLayout.PathDistance(x, z) < ForestLayout.PathHalfWidth + 0.2f || Obstacles.Blocks(x, z, 0.1f))
                continue;
            grass.Add(Placed(Ground(x, z), Rand(k, 62) * MathF.Tau, Vector3.One * (0.8f + 0.8f * Rand(k, 63))),
                GroundColor(x, z).Lightened(0.08f));
        }
        var grassMaterial = Material("foliage.gdshader");
        grassMaterial.SetShaderParameter("sway", 0.04f);
        AddChild(grass.Build("Grass", ForestMeshes.GrassTuft(), grassMaterial, shadows: false));
    }

    private void BuildLogs()
    {
        var logs = new Instances();
        for (int k = 0, placed = 0; k < 400 && placed < 30; k++)
        {
            float x = (Rand(k, 70) * 2f - 1f) * ForestBounds.HalfSize, z = (Rand(k, 71) * 2f - 1f) * ForestBounds.HalfSize;
            float length = 2.5f + 2.5f * Rand(k, 72), radius = 0.3f + 0.2f * Rand(k, 73);
            float yaw = Rand(k, 74) * MathF.Tau;
            var along = new Vector3(MathF.Cos(yaw), 0f, MathF.Sin(yaw));
            var a = new Vector3(x, 0f, z) - along * (length * 0.5f);
            var b = new Vector3(x, 0f, z) + along * (length * 0.5f);
            if (!Open(x, z, length * 0.5f + radius) || ForestLayout.ClearingDepth(x, z) > -1f)
                continue;
            var center = Ground(x, z) + Vector3.Up * (radius * 0.75f);
            center.Y = MathF.Max(Terrain.HeightAt(a.X, a.Z), Terrain.HeightAt(b.X, b.Z)) * 0.5f + center.Y * 0.5f;
            // The log's local Y (its length) lies along the ground.
            var basis = new Basis(new Quaternion(Vector3.Up, along)) * Basis.FromScale(new Vector3(radius, length, radius));
            logs.Add(new Transform3D(basis, center), Colors.White);
            placed++;
        }
        AddChild(logs.Build("Logs", ForestMeshes.Log(), Material("log.gdshader", outline: true)));
    }

    private void BuildFireflies()
    {
        var flies = new Instances();
        int k = 0;
        foreach (var clearing in ForestLayout.Clearings)
        {
            for (int i = 0; i < 40; i++, k++)
            {
                float a = Rand(k, 80) * MathF.Tau, d = clearing.Radius * (0.3f + 0.9f * Rand(k, 81));
                float x = clearing.X + MathF.Cos(a) * d, z = clearing.Z + MathF.Sin(a) * d;
                flies.Add(new Transform3D(Basis.Identity, Ground(x, z) + Vector3.Up * (0.6f + 2.4f * Rand(k, 82))),
                    Colors.White, new Color(Rand(k, 83), 0f, 0f, 0f));
            }
        }
        for (int i = 0; i < 260; i++, k++)
        {
            float x = (Rand(k, 84) * 2f - 1f) * ForestBounds.HalfSize, z = (Rand(k, 85) * 2f - 1f) * ForestBounds.HalfSize;
            flies.Add(new Transform3D(Basis.Identity, Ground(x, z) + Vector3.Up * (0.5f + 3f * Rand(k, 86))),
                Colors.White, new Color(Rand(k, 87), 0f, 0f, 0f));
        }
        var node = flies.Build("Fireflies", ForestMeshes.Quad(), Material("firefly.gdshader"), shadows: false);
        // The billboards drift in the shader; keep them from being culled near the edges of view.
        node.ExtraCullMargin = 3f;
        AddChild(node);
    }

    private void BuildPreviewCamera()
    {
        // Looks north up the path from the player's start at eye height. Any camera another
        // module makes current (the player's) replaces it.
        var start = ForestBounds.PlayerStart;
        var eye = new Vector3(start.X, start.Y + EyeHeight, start.Z);
        var camera = new Camera3D { Name = "PreviewCamera", Fov = 70f, Far = 400f };
        AddChild(camera);
        camera.LookAtFromPosition(eye, Ground(4f, 50f) + Vector3.Up * 3f, Vector3.Up);
    }
}
