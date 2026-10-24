using System;
using System.Collections.Generic;
using Fungtopia.Map;
using Fungtopia.Shell;
using Godot;
using MapGround = Fungtopia.Map.Ground;

namespace Fungtopia.Forest.View;

/// <summary>
/// Draws the Mushroom Forest: the forest square's floor from the Map's <see cref="MapGround.HeightAt"/>
/// (the Map draws the ground around it on the same 1 m grid), the giant and medium mushrooms of
/// <see cref="ForestScenery"/> (on the trunks and the Map's forest props), the ring of giant mushrooms around <see cref="ForestBounds"/>
/// with the Map's gaps left open (<see cref="ForestRing"/>), darker toward the west gap with its
/// overgrown trail (<see cref="ForestProps"/>), and instanced small walkable decoration under
/// 0.4 m (tiny mushrooms, ferns, flowers, grass) and fireflies. Mushrooms have plain spotted caps and no faces. The glowing caps glow brighter at night
/// (<see cref="ForestModule.IsNight"/>). Sun, sky and environment are the DayNight module's. Also
/// a preview camera at the player's start that any other current camera replaces.
/// </summary>
public partial class ForestView : Node3D, IModuleView
{
    private const string Shaders = "res://modules/Forest/Godot/Shaders/";

    /// <summary>How much brighter the glowing caps shine at night.</summary>
    private const float NightGlow = 2.4f;

    /// <summary>How fast (per second) the glow follows day and night.</summary>
    private const float GlowRate = 0.5f;

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

    /// <summary>The Deep Forest's dark caps: deep purples, blues and dusky reds.</summary>
    private static readonly Color[] DarkCapColors =
    {
        new(0.32f, 0.2f, 0.42f), new(0.24f, 0.22f, 0.4f), new(0.42f, 0.18f, 0.28f), new(0.2f, 0.28f, 0.36f),
    };

    private ForestModule? _forest;
    private ShaderMaterial? _capMaterial;
    private float _glow = 1f;

    public void Bind(IModule module) => _forest = (ForestModule)module;

    public override void _Ready()
    {
        BuildGround();
        BuildMushrooms();
        BuildFoliage();
        BuildFireflies();
        BuildPreviewCamera();
    }

    /// <summary>A fixed pseudo-random value in [0, 1) for (i, k): the same scene every run.</summary>
    private static float Rand(int i, int k) => ForestRandom.Value(i, k);

    public override void _Process(double delta)
    {
        if (_capMaterial == null)
            return;
        float target = _forest != null && _forest.IsNight ? NightGlow : 1f;
        if (_glow == target)
            return;
        float step = GlowRate * (NightGlow - 1f) * (float)delta;
        _glow = _glow < target ? MathF.Min(_glow + step, target) : MathF.Max(_glow - step, target);
        _capMaterial.SetShaderParameter("night_glow", _glow);
    }

    private static Vector3 Ground(float x, float z) => new(x, MapGround.HeightAt(x, z), z);

    private static ShaderMaterial Material(string shader, bool outline = false)
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>(Shaders + shader) };
        if (outline)
            material.NextPass = new ShaderMaterial { Shader = GD.Load<Shader>(Shaders + "outline.gdshader") };
        return material;
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
        color = color.Lerp(dirt, path);
        // The overgrown trail to the west gap: patchy dark soil half lost under moss, darker
        // toward the Deep Forest.
        float trail = 1f - Mathf.SmoothStep(ForestProps.TrailHalfWidth - 0.6f, ForestProps.TrailHalfWidth + 0.5f, ForestProps.TrailDistance(x, z));
        trail = MathF.Max(trail, Mathf.SmoothStep(-1f, 1.5f, Areas.DeepForestPath.Depth(x, z)));
        float patches = Mathf.SmoothStep(0.25f, 0.75f, 0.5f + 0.5f * MathF.Sin(x * 0.9f + MathF.Sin(z * 1.3f) * 1.5f) * MathF.Sin(z * 0.7f - x * 0.4f));
        var soil = new Color(0.5f, 0.4f, 0.3f).Lerp(new Color(0.34f, 0.3f, 0.26f), Mathf.SmoothStep(-90f, -100f, x));
        color = color.Lerp(soil, trail * (0.35f + 0.5f * patches));
        return color.Lerp(moss * 0.8f, Mathf.SmoothStep(-88f, -100f, x) * Mathf.SmoothStep(14f, 4f, MathF.Abs(z)) * 0.35f);
    }

    private void BuildGround()
    {
        // Just the forest square, on the 1 m grid the Map's ground continues outside it.
        const float step = 1f;
        float extent = ForestBounds.HalfSize;
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
            float dx = MapGround.HeightAt(x + e, z) - MapGround.HeightAt(x - e, z);
            float dz = MapGround.HeightAt(x, z + e) - MapGround.HeightAt(x, z - e);
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

    private void BuildMushrooms()
    {
        var stems = new Instances();
        var caps = new Instances();

        // Height is the cap's top above the ground; the stem's foot sinks a little into it.
        void Mushroom(DrawnMushroom m, Color color, bool glow, float glowStrength)
        {
            int id = m.Id;
            float sink = MathF.Min(0.4f, m.Height * 0.08f);
            var foot = Ground(m.X, m.Z) + Vector3.Down * sink;
            float stemHeight = m.Height + sink - m.CapHeight * 0.82f;
            var stemColor = new Color(0.97f, 0.91f, 0.8f).Lerp(new Color(0.93f, 0.82f, 0.7f), Rand(id, 2));
            stems.Add(Placed(foot, m.Yaw, new Vector3(m.StemRadius, stemHeight, m.StemRadius)), stemColor);
            var tilt = new Basis(new Vector3(MathF.Cos(m.Yaw), 0f, MathF.Sin(m.Yaw)), 0.08f * (Rand(id, 7) - 0.5f));
            var capBasis = tilt * new Basis(Vector3.Up, m.Yaw) * Basis.FromScale(new Vector3(m.CapRadius, m.CapHeight, m.CapRadius));
            var capCustom = new Color(Rand(id, 8), glow ? glowStrength : 0f, 0f, 0f);
            caps.Add(new Transform3D(capBasis, foot + Vector3.Up * (stemHeight - m.CapHeight * 0.18f)), color, capCustom);
        }

        void Colored(DrawnMushroom m, bool dark = false)
        {
            int id = m.Id;
            bool glow = Rand(id, 3) < 0.16f;
            var color = glow ? GlowColors[(int)(Rand(id, 4) * GlowColors.Length)]
                : dark ? DarkCapColors[(int)(Rand(id, 4) * DarkCapColors.Length)]
                : CapColors[(int)(Rand(id, 4) * CapColors.Length)];
            Mushroom(m, color.Lightened(0.08f * (Rand(id, 5) - 0.5f)), glow, 0.9f + 0.6f * Rand(id, 9));
        }

        // A giant mushroom on every forest trunk, its stem exactly the trunk's circle.
        foreach (var m in ForestScenery.Giants)
            Colored(m);

        // A medium mushroom on every Map prop of the forest, exactly where and as large as the Map says.
        foreach (var m in ForestScenery.MediumMushrooms)
            Colored(m);

        // The ring: dense giant mushrooms in a band just outside the square (off all walkable
        // ground), open where the Map's roads and the Deep Forest path pass through, dark-capped by the west gap.
        foreach (var r in ForestRing.Mushrooms)
        {
            float capRadius = r.Radius * (2.4f + 1.4f * Rand(r.Id, 34));
            float capHeight = capRadius * (0.5f + 0.3f * Rand(r.Id, 6));
            Colored(new DrawnMushroom(DecorKind.Mushroom, r.X, r.Z, Rand(r.Id, 1) * MathF.Tau, r.Radius,
                7f + 9f * Rand(r.Id, 33) + capHeight * 0.82f, capRadius, capHeight, r.Id), r.Dark);
        }

        // Tiny walkable mushrooms (decoration under the height limit), a few dark ones by the west gap.
        foreach (var d in ForestScenery.Decoration)
        {
            if (d.Kind != DecorKind.Mushroom && d.Kind != DecorKind.DarkMushroom)
                continue;
            Colored(new DrawnMushroom(d.Kind, d.X, d.Z, d.Yaw, d.Radius * 0.35f, d.Height, d.Radius, d.CapHeight, d.Id + 7),
                d.Kind == DecorKind.DarkMushroom);
        }

        AddChild(stems.Build("MushroomStems", ForestMeshes.Stem(), Material("toon_color.gdshader", outline: true)));
        _capMaterial = Material("mushroom_cap.gdshader", outline: true);
        AddChild(caps.Build("MushroomCaps", ForestMeshes.Cap(), _capMaterial));
    }

    private static readonly Color[] FlowerColors =
    {
        new(1f, 0.96f, 0.9f), new(1f, 0.85f, 0.3f), new(0.95f, 0.55f, 0.75f), new(0.65f, 0.6f, 1f), new(1f, 0.6f, 0.45f),
    };

    private void BuildFoliage()
    {
        // Small ground decoration from ForestScenery, each scaled from its mesh to its height.
        var ferns = new Instances();
        var flowers = new Instances();
        var grass = new Instances();
        foreach (var d in ForestScenery.Decoration)
        {
            int id = d.Id;
            switch (d.Kind)
            {
                case DecorKind.Fern:
                    ferns.Add(Placed(Ground(d.X, d.Z), d.Yaw, Vector3.One * (d.Height / ForestScenery.FernMeshHeight)),
                        new Color(0.34f, 0.64f, 0.3f).Lerp(new Color(0.5f, 0.72f, 0.28f), Rand(id, 59)));
                    break;
                case DecorKind.Flower:
                    flowers.Add(Placed(Ground(d.X, d.Z), d.Yaw, Vector3.One * (d.Height / ForestScenery.FlowerMeshHeight)),
                        FlowerColors[(int)(Rand(id, 70) * FlowerColors.Length)]);
                    break;
                case DecorKind.Grass:
                    grass.Add(Placed(Ground(d.X, d.Z), d.Yaw, Vector3.One * (d.Height / ForestScenery.GrassMeshHeight)),
                        GroundColor(d.X, d.Z).Lightened(0.08f));
                    break;
            }
        }
        AddChild(ferns.Build("Ferns", ForestMeshes.Fern(), Material("foliage.gdshader")));
        AddChild(flowers.Build("Flowers", ForestMeshes.Flower(), Material("flower.gdshader"), shadows: false));
        var grassMaterial = Material("foliage.gdshader");
        grassMaterial.SetShaderParameter("sway", 0.04f);
        AddChild(grass.Build("Grass", ForestMeshes.GrassTuft(), grassMaterial, shadows: false));
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
