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
/// a preview camera at the player's start that any other current camera replaces. For screenshots,
/// FOREST_PREVIEW=start or close keeps a preview camera current and hides the screens over the
/// world (start screen, HUD); without it the view never touches them.
/// </summary>
public partial class ForestView : Node3D, IModuleView
{
    private const string Shaders = "res://modules/Forest/Godot/Shaders/";

    /// <summary>How much brighter the glowing caps shine at night.</summary>
    private const float NightGlow = ForestPalette.NightGlow;

    /// <summary>How fast (per second) the glow follows day and night.</summary>
    private const float GlowRate = 0.5f;

    private const float EyeHeight = 1.6f;

    private static Color ToColor(System.Numerics.Vector3 c) => new(c.X, c.Y, c.Z);

    private static Color[] ToColors(System.Numerics.Vector3[] colors) => Array.ConvertAll(colors, ToColor);

    private ForestModule? _forest;
    private ShaderMaterial? _capMaterial;
    private float _glow = ForestPalette.DayGlow;
    private Camera3D? _preview;

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
        if (_preview != null)
            KeepPreview();
        if (_capMaterial == null)
            return;
        float target = _forest != null && _forest.IsNight ? NightGlow : ForestPalette.DayGlow;
        if (_glow == target)
            return;
        // Screenshots show the glow at once, without the fade.
        if (_preview != null)
            _glow = target;
        float step = GlowRate * (NightGlow - ForestPalette.DayGlow) * (float)delta;
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
        // ForestPalette's lush greens, sunny yellow-green patches and golden paths.
        var grass = ToColor(ForestPalette.Grass);
        var moss = ToColor(ForestPalette.Moss);
        var meadow = ToColor(ForestPalette.Meadow);
        var dirt = ToColor(ForestPalette.Dirt);
        float mottle = 0.5f + 0.25f * MathF.Sin(x * 0.19f + MathF.Sin(z * 0.13f) * 2f) + 0.25f * MathF.Sin(z * 0.23f - x * 0.07f);
        var color = grass.Lerp(moss, Mathf.SmoothStep(0.35f, 0.85f, mottle) * 0.8f);
        float sunny = 0.5f + 0.5f * MathF.Sin(x * 0.11f - MathF.Sin(z * 0.17f) * 2.3f) * MathF.Sin(z * 0.09f + x * 0.05f);
        color = color.Lerp(ToColor(ForestPalette.GrassLight), Mathf.SmoothStep(0.55f, 0.95f, sunny) * 0.6f);
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
        var soil = ToColor(ForestPalette.Soil).Lerp(ToColor(ForestPalette.SoilDark), Mathf.SmoothStep(-90f, -100f, x));
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
        var skirts = new Instances();
        var shelves = new Instances();
        // One MultiMesh per cap shape, all with the one cap material.
        var caps = new Instances[Enum.GetValues<CapShape>().Length];
        for (int i = 0; i < caps.Length; i++)
            caps[i] = new Instances();

        // Planted by ForestFooting: the stem's foot under the lowest ground around it, the cap
        // Height above the ground at its center, big stems flaring into the ground. Stems slim and
        // lean in the stem shader (custom: slender, lean); the cap sits on the leaning top, tilted with it.
        void Mushroom(DrawnMushroom m, Color color, bool glow, float glowStrength)
        {
            int id = m.Id;
            var look = ForestSpecies.Of(m.Species);
            var planted = ForestFooting.Plant(m);
            var foot = new Vector3(m.X, planted.BaseY, m.Z);
            var pattern = new Color(Rand(id, 8), glow ? glowStrength : 0f, (int)look.Pattern / 8f, look.Underside);
            if (look.Stemless)
            {
                // One body from below the ground up to its top.
                float top = planted.CapY + m.CapHeight;
                caps[(int)m.Shape].Add(Placed(foot, m.Yaw, new Vector3(m.CapRadius, top - planted.BaseY, m.CapRadius)), color, pattern);
                return;
            }
            var stemColor = ToColor(ForestPalette.StemLight).Lerp(ToColor(ForestPalette.StemShade), Rand(id, 2));
            stems.Add(Placed(foot, m.Yaw, new Vector3(m.StemRadius, planted.StemHeight, m.StemRadius)), stemColor,
                new Color(m.Slender, m.Lean * planted.StemHeight / m.StemRadius, 0f, 0f));
            if (planted.SkirtHeight > 0f)
                skirts.Add(Placed(foot, m.Yaw, new Vector3(m.StemRadius, planted.SkirtHeight, m.StemRadius)), stemColor.Darkened(0.04f), new Color(1f, 0f, 0f, 0f));
            var lean = ForestScenery.LeanOffset(m, planted.StemHeight, 1f);
            float c = MathF.Cos(m.Yaw), sn = MathF.Sin(m.Yaw);
            // Tilted toward the lean (up x the lean's direction), a touch of wobble on top.
            var tilt = new Basis(new Vector3(-sn, 0f, -c), MathF.Atan(2f * m.Lean) * 0.8f + 0.06f * (Rand(id, 7) - 0.5f));
            var capBasis = tilt * new Basis(Vector3.Up, m.Yaw) * Basis.FromScale(new Vector3(m.CapRadius, m.CapHeight, m.CapRadius));
            caps[(int)m.Shape].Add(new Transform3D(capBasis, new Vector3(m.X + lean.X, planted.CapY, m.Z + lean.Y)), color, pattern);
        }

        // Each kind takes its own colors (ForestSpecies.Colors, from ForestPalette); by the west
        // gap the Deep Forest's dark caps (ForestPalette.DarkCaps), but glowing ones still glow.
        void Colored(DrawnMushroom m, bool dark = false)
        {
            int id = m.Id;
            bool glow = ForestSpecies.Of(m.Species).Glows;
            var colors = dark && !glow ? ForestPalette.DarkCaps : ForestSpecies.Colors(m.Species);
            var color = ToColor(colors[(int)(Rand(id, 4) * colors.Length)]);
            Mushroom(m, color.Lightened(0.08f * (Rand(id, 5) - 0.5f)), glow, 0.9f + 0.6f * Rand(id, 9));
        }

        // A giant mushroom on every forest trunk, its stem exactly the trunk's circle.
        foreach (var m in ForestScenery.Giants)
            Colored(m);

        // Shelf fungi stacked up the side of some giant stems.
        foreach (var shelf in ForestScenery.Shelves)
        {
            var g = ForestScenery.Giants[shelf.Giant];
            var at = ForestScenery.StemPoint(g, ForestFooting.Plant(g), shelf.Along, shelf.Angle);
            var into = new Vector3(MathF.Cos(shelf.Angle), 0f, MathF.Sin(shelf.Angle)) * (shelf.Radius * 0.15f);
            var colors = ForestPalette.ShelfCaps;
            shelves.Add(Placed(new Vector3(at.X, at.Y - shelf.Thickness * 0.5f, at.Z) - into, -shelf.Angle,
                    new Vector3(shelf.Radius, shelf.Thickness, shelf.Radius * 1.25f)),
                ToColor(colors[(int)(Rand(shelf.Id, 4) * colors.Length)]),
                new Color(Rand(shelf.Id, 8), 0f, (int)CapPattern.Bands / 8f, 0.5f));
        }

        // The medium clusters on every Map prop of the forest, exactly where the Map says, each stem inside its circle.
        foreach (var m in ForestScenery.MediumMushrooms)
            Colored(m);

        // The ring: dense giant mushrooms in a band just outside the square (off all walkable
        // ground), open where the Map's roads and the Deep Forest path pass through, dark-capped by the west gap.
        for (int i = 0; i < ForestScenery.RingMushrooms.Length; i++)
            Colored(ForestScenery.RingMushrooms[i], ForestRing.Mushrooms[i].Dark);

        // Tiny walkable fungi of every kind (decoration under the height limit), a few dark ones by the west gap.
        foreach (var d in ForestScenery.Decoration)
        {
            if (d.Kind != DecorKind.Mushroom && d.Kind != DecorKind.DarkMushroom)
                continue;
            Colored(ForestScenery.AsMushroom(d), d.Kind == DecorKind.DarkMushroom);
        }

        var stemMaterial = Material("toon_color.gdshader", outline: true);
        ((ShaderMaterial)stemMaterial.NextPass).SetShaderParameter("bend", true);
        AddChild(stems.Build("MushroomStems", ForestMeshes.Stem(), stemMaterial));
        AddChild(skirts.Build("MushroomSkirts", ForestMeshes.Skirt(), stemMaterial));
        _capMaterial = Material("mushroom_cap.gdshader", outline: true);
        _capMaterial.SetShaderParameter("spot_color", ToColor(ForestPalette.Spot));
        _capMaterial.SetShaderParameter("gill_color", ToColor(ForestPalette.Gills));
        _capMaterial.SetShaderParameter("gloss", ForestPalette.CapGloss);
        _capMaterial.SetShaderParameter("rim", ForestPalette.CapRim);
        _capMaterial.SetShaderParameter("glow_emission", ForestPalette.GlowEmission);
        _capMaterial.SetShaderParameter("night_glow", _glow);
        foreach (var shape in Enum.GetValues<CapShape>())
            if (caps[(int)shape].Transforms.Count > 0)
                AddChild(caps[(int)shape].Build("MushroomCaps" + shape, ForestMeshes.Cap(shape), _capMaterial));
        AddChild(shelves.Build("ShelfFungi", ForestMeshes.Shelf(), _capMaterial));
    }

    private static readonly Color[] FlowerColors = ToColors(ForestPalette.Flowers);

    /// <summary>Where a piece of foliage stands: its root clump (0.4 of its radius) at or under the ground all around it.</summary>
    private static Vector3 Planted(in Decoration d) => new(d.X, ForestFooting.BaseY(d.X, d.Z, d.Radius * 0.4f, d.Height), d.Z);

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
                    ferns.Add(Placed(Planted(d), d.Yaw, Vector3.One * (d.Height / ForestScenery.FernMeshHeight)),
                        ToColor(ForestPalette.FernDark).Lerp(ToColor(ForestPalette.FernLight), Rand(id, 59)));
                    break;
                case DecorKind.Flower:
                    flowers.Add(Placed(Planted(d), d.Yaw, Vector3.One * (d.Height / ForestScenery.FlowerMeshHeight)),
                        FlowerColors[(int)(Rand(id, 70) * FlowerColors.Length)]);
                    break;
                case DecorKind.Grass:
                    grass.Add(Placed(Planted(d), d.Yaw, Vector3.One * (d.Height / ForestScenery.GrassMeshHeight)),
                        GroundColor(d.X, d.Z).Lerp(ToColor(ForestPalette.GrassLight), 0.25f + 0.25f * Rand(id, 64)));
                    break;
            }
        }
        AddChild(ferns.Build("Ferns", ForestMeshes.Fern(), Material("foliage.gdshader")));
        var flowerMaterial = Material("flower.gdshader");
        flowerMaterial.SetShaderParameter("stem_color", ToColor(ForestPalette.FlowerStem));
        AddChild(flowers.Build("Flowers", ForestMeshes.Flower(), flowerMaterial, shadows: false));
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

    /// <summary>Screenshots only: the preview camera stays current and the screens and labels over the world are hidden.</summary>
    private void KeepPreview()
    {
        if (!_preview!.Current)
            _preview.MakeCurrent();
        foreach (var node in GetParent().GetChildren())
        {
            if (node is CanvasLayer layer && layer.Visible)
                layer.Visible = false;
            else if (node is Control control && control.Visible)
                control.Visible = false;
        }
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
        switch (OS.GetEnvironment("FOREST_PREVIEW").ToLowerInvariant())
        {
            case "start":
                break;
            case "close":
                // Close to the giant mushroom nearest the start, from the start's side, looking up at its cap.
                var near = ForestScenery.Giants[0];
                foreach (var m in ForestScenery.Giants)
                    if (MathF.Pow(m.X - start.X, 2) + MathF.Pow(m.Z - start.Z, 2) < MathF.Pow(near.X - start.X, 2) + MathF.Pow(near.Z - start.Z, 2))
                        near = m;
                var away = new Vector3(start.X - near.X, 0f, start.Z - near.Z).Normalized();
                var at = Ground(near.X, near.Z);
                var from = at + away * (near.CapRadius * 2.6f);
                camera.LookAtFromPosition(Ground(from.X, from.Z) + Vector3.Up * EyeHeight, at + Vector3.Up * near.Height * 0.75f, Vector3.Up);
                break;
            default:
                return;
        }
        _preview = camera;
        camera.MakeCurrent();
    }
}
