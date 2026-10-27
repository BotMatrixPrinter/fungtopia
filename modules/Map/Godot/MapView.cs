using System;
using System.Collections.Generic;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.Map.View;

/// <summary>
/// Draws the world's ground from the Map Contract, everywhere except the forest square (the
/// Forest module draws its own floor there; this mesh meets it on the same 1 m grid and heights):
/// grass, dirt roads with little glowing mushroom lamps, the town square and Pet City's paving,
/// the Deep Forest's dark soil and moss with roots spreading from its giant mushrooms (the
/// DeepForest module draws the mushrooms themselves and the gloom), hedges along every walkable
/// edge, cliffs beyond them topped with giant mushrooms, and the home
/// interior (wood floor, walls with round windows, a door) at its far-away spot. Buildings are
/// drawn by the Town and PetCity modules; nothing here moves, so it costs only a few draws.
/// </summary>
public partial class MapView : Node3D, IModuleView
{
    private const string Shaders = "res://modules/Map/Godot/Shaders/";

    private static Color ToColor(System.Numerics.Vector3 c) => new(c.X, c.Y, c.Z);
    private static Vector3 ToGodot(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);

    private MapModule? _map;
    private Camera3D? _preview;

    public void Bind(IModule module) => _map = (MapModule)module;

    public override void _Ready()
    {
        BuildGround();
        BuildHedges();
        BuildMushrooms();
        BuildRoots();
        BuildHome();
        BuildPreviewCamera();
    }

    public override void _Process(double delta)
    {
        if (_preview == null)
            return;
        // Another module's camera (the player's) may have become current since: take it back.
        if (!_preview.Current)
            _preview.MakeCurrent();
        // Screenshots only: hide the screens and labels drawn over the world (start screen, HUD, prompts).
        foreach (var node in GetParent().GetChildren())
        {
            if (node is CanvasLayer layer && layer.Visible)
                layer.Visible = false;
            else if (node is Control control && control.Visible)
                control.Visible = false;
        }
    }

    /// <summary>For screenshots: MAP_PREVIEW=road, cliffs, home or homewindow makes a preview camera current.</summary>
    private void BuildPreviewCamera()
    {
        Vector3 from, target;
        switch (OS.GetEnvironment("MAP_PREVIEW").ToLowerInvariant())
        {
            case "road":
                // On the south road, looking along its glowing lamps toward Mushroom Town.
                from = OnGround(0f, 114f) + Vector3.Up * 2f;
                target = OnGround(1f, 132f) + Vector3.Up * 1f;
                break;
            case "cliffs":
                // From the south road, west up at the giant mushrooms on the cliffs beside it.
                from = OnGround(2f, 124f) + Vector3.Up * 1.8f;
                target = OnGround(-30f, 128f) + Vector3.Up * 9f;
                break;
            case "home":
                // Inside the home room, from by the door toward its north-west corner and windows.
                from = new Vector3(603.5f, Areas.HomeFloorY + 1.7f, 604f);
                target = new Vector3(597f, Areas.HomeFloorY + 1.9f, 596f);
                break;
            case "homewindow":
                // Close to the north wall, along it at a grazing angle onto a round window.
                from = new Vector3(600.5f, Areas.HomeFloorY + 2.3f, 596.2f);
                target = new Vector3(597f, Areas.HomeFloorY + 2.1f, 595f);
                break;
            default:
                return;
        }
        _preview = new Camera3D { Name = "PreviewCamera", Fov = 70f, Far = 400f };
        AddChild(_preview);
        _preview.LookAtFromPosition(from, target, Vector3.Up);
        _preview.MakeCurrent();
    }

    /// <summary>A fixed pseudo-random value in [0, 1) for (i, k): the same scene every run.</summary>
    private static float Rand(int i, int k) => Fungi.Hash(i, k);

    private static Vector3 OnGround(float x, float z) => new(x, Map.Ground.HeightAt(x, z), z);

    private static ShaderMaterial Material(string shader, bool outline = false)
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>(Shaders + shader) };
        if (outline)
            material.NextPass = new ShaderMaterial { Shader = GD.Load<Shader>(Shaders + "outline.gdshader") };
        return material;
    }

    private static bool InForestSquare(float x, float z) =>
        MathF.Abs(x) < Areas.ForestHalfSize && MathF.Abs(z) < Areas.ForestHalfSize;

    /// <summary>Meters inside the Deep Forest or its overgrown path (negative outside).</summary>
    private static float DeepForestDepth(float x, float z) =>
        MathF.Max(Areas.DeepForest.Depth(x, z), Areas.DeepForestPath.Depth(x, z));

    // Ground

    private static Vector3 GroundNormal(float x, float z)
    {
        const float e = 0.25f;
        float dx = Map.Ground.HeightAt(x + e, z) - Map.Ground.HeightAt(x - e, z);
        float dz = Map.Ground.HeightAt(x, z + e) - Map.Ground.HeightAt(x, z - e);
        return new Vector3(-dx, 2f * e, -dz).Normalized();
    }

    private static Color GroundColor(float x, float z, Vector3 normal)
    {
        // The Forest module's own grass and moss right at the seam, so its edge does not show;
        // from there out the vibrant world's (WorldPalette).
        var forestGrass = new Color(0.5f, 0.74f, 0.33f);
        var forestMoss = new Color(0.3f, 0.56f, 0.32f);
        var grass = ToColor(WorldPalette.Grass);
        var moss = ToColor(WorldPalette.Moss);
        var meadow = ToColor(WorldPalette.Meadow);
        var dirt = ToColor(WorldPalette.Dirt);
        var rock = ToColor(WorldPalette.Rock);
        var stone = ToColor(WorldPalette.Stone);
        var paving = ToColor(WorldPalette.Paving);
        var tiles = ToColor(WorldPalette.Tiles);

        float mottle = 0.5f + 0.25f * MathF.Sin(x * 0.19f + MathF.Sin(z * 0.13f) * 2f) + 0.25f * MathF.Sin(z * 0.23f - x * 0.07f);
        float outside = MathF.Max(MathF.Abs(x), MathF.Abs(z)) - Areas.ForestHalfSize;
        float vibrant = Mathf.SmoothStep(0f, 6f, outside);
        var color = forestGrass.Lerp(grass, vibrant).Lerp(forestMoss.Lerp(moss, vibrant), Mathf.SmoothStep(0.35f, 0.85f, mottle));
        color = color.Lerp(moss.Lerp(grass, 0.3f), Mathf.SmoothStep(-4f, 6f, outside) * 0.6f);

        // Brighter meadow in the towns and along the roads.
        float depth = Areas.Depth(x, z);
        color = color.Lerp(meadow.Lerp(grass, 0.4f * mottle), Mathf.SmoothStep(-1f, 4f, depth) * Mathf.SmoothStep(6f, 16f, outside));

        var town = Areas.Town;
        var city = Areas.PetCity;
        if (town.Depth(x, z) > -2f)
        {
            float path = 1f - Mathf.SmoothStep(1.3f, 2f, Areas.TownPathDistance(x, z));
            color = color.Lerp(dirt, path);
            float square = new System.Numerics.Vector2(x - Buildings.TownSquare.X, z - Buildings.TownSquare.Y).Length();
            color = color.Lerp(stone, 1f - Mathf.SmoothStep(12f, 13f, square));
            if (square < 12f && square > 6f && MathF.Sin(MathF.Atan2(z - Buildings.TownSquare.Y, x) * 12f) > 0.6f)
                color = color.Lerp(stone * 0.88f, 0.6f);
        }
        else if (city.Depth(x, z) > -2f)
        {
            // Pet City is paved, with a pink tiled plaza ring.
            bool tile = ((int)MathF.Floor(x / 4f) + (int)MathF.Floor(z / 4f)) % 2 == 0;
            color = color.Lerp(tile ? paving : paving.Lightened(0.12f), Mathf.SmoothStep(-1f, 2f, city.Depth(x, z)));
            float plaza = new System.Numerics.Vector2(x - Buildings.CityPlaza.X, z - Buildings.CityPlaza.Y).Length();
            float ring = 1f - Mathf.SmoothStep(0.4f, 0.8f, MathF.Abs(MathF.Abs(plaza - 11f) - 2.5f) - 1.5f);
            color = color.Lerp(tiles, Mathf.Clamp(ring, 0f, 1f) * (plaza < 15f ? 1f : 0f));
            if (plaza < 7f)
                color = color.Lerp(tiles.Lightened(0.3f), 0.8f);
        }

        float road = 1f - Mathf.SmoothStep(Areas.RoadHalfWidth - 0.6f, Areas.RoadHalfWidth + 0.4f, Areas.RoadDistance(x, z));
        color = color.Lerp(dirt, road);

        // The Deep Forest: dark soil under patches of deep moss, streaked with old mushroom roots.
        float deep = Mathf.SmoothStep(-6f, 3f, DeepForestDepth(x, z));
        if (deep > 0f)
        {
            var soil = new Color(0.3f, 0.22f, 0.17f);
            var deepMoss = new Color(0.14f, 0.36f, 0.2f);
            var root = new Color(0.46f, 0.34f, 0.25f);
            var floor = soil.Lerp(deepMoss, Mathf.SmoothStep(0.3f, 0.75f, mottle + 0.15f * MathF.Sin(x * 0.41f + z * 0.37f)));
            float roots = MathF.Abs(MathF.Sin(0.5f * x + 0.8f * MathF.Sin(0.21f * z)));
            floor = floor.Lerp(root, Mathf.SmoothStep(0.9f, 0.98f, roots) * 0.7f);
            // The overgrown path: trodden soil, mossy at its edges.
            float path = 1f - Mathf.SmoothStep(1.5f, Areas.DeepForestGapHalfWidth, MathF.Abs(z));
            if (x > Areas.DeepForest.MaxX - 4f)
                floor = floor.Lerp(soil.Lightened(0.15f), path * 0.7f);
            color = color.Lerp(floor, deep);
        }

        // Cliff faces are rock; their tops are mossy.
        float cliff = Mathf.SmoothStep(0.92f, 0.75f, normal.Y);
        color = color.Lerp(rock, cliff);
        // Alpha: how much of it is meadow grass, for the ground shader's sunny patches and flowers
        // (none on the roads, paths, paving, the Deep Forest's floor or rock, nor at the forest's seam).
        float paths = town.Depth(x, z) > -2f ? Mathf.SmoothStep(1.5f, 2.5f, Areas.TownPathDistance(x, z)) : 1f;
        float fromSquare = new System.Numerics.Vector2(x - Buildings.TownSquare.X, z - Buildings.TownSquare.Y).Length();
        color.A = vibrant * (1f - road) * (1f - deep) * (1f - cliff) * paths * Mathf.SmoothStep(13f, 14f, fromSquare)
            * (1f - Mathf.SmoothStep(-2f, 0f, Areas.PetCity.Depth(x, z)));
        return color;
    }

    /// <summary>Collects one ground mesh from several square-grid patches.</summary>
    private sealed class GroundMesh
    {
        public readonly List<Vector3> Vertices = new();
        public readonly List<Vector3> Normals = new();
        public readonly List<Color> Colors = new();
        public readonly List<int> Indices = new();

        /// <summary>A patch from (x0, z0) to (x1, z1) on a <paramref name="step"/> grid, leaving out cells <paramref name="skip"/> says.</summary>
        public void Patch(float x0, float z0, float x1, float z1, float step, Func<float, float, bool> skip)
        {
            int nx = (int)MathF.Round((x1 - x0) / step) + 1, nz = (int)MathF.Round((z1 - z0) / step) + 1;
            var index = new int[nx * nz];
            Array.Fill(index, -1);
            int Vertex(int i, int j)
            {
                ref int k = ref index[i * nz + j];
                if (k < 0)
                {
                    float x = x0 + i * step, z = z0 + j * step;
                    k = Vertices.Count;
                    Vertices.Add(OnGround(x, z));
                    var normal = GroundNormal(x, z);
                    Normals.Add(normal);
                    Colors.Add(GroundColor(x, z, normal));
                }
                return k;
            }
            for (int i = 0; i + 1 < nx; i++)
            for (int j = 0; j + 1 < nz; j++)
            {
                if (skip(x0 + (i + 0.5f) * step, z0 + (j + 0.5f) * step))
                    continue;
                int a = Vertex(i, j), b = Vertex(i + 1, j), c = Vertex(i, j + 1), d = Vertex(i + 1, j + 1);
                // Clockwise seen from above: Godot's front faces.
                Indices.Add(a); Indices.Add(b); Indices.Add(c);
                Indices.Add(c); Indices.Add(b); Indices.Add(d);
            }
        }

        public ArrayMesh Commit()
        {
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = Vertices.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = Normals.ToArray();
            arrays[(int)Mesh.ArrayType.Color] = Colors.ToArray();
            arrays[(int)Mesh.ArrayType.Index] = Indices.ToArray();
            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            return mesh;
        }
    }

    private void BuildGround()
    {
        const float near = 112f;
        var ground = new GroundMesh();
        // A 1 m grid around the forest square (the Forest's own grid, so the seam matches), then
        // a coarser 2 m grid out to the cliffs beyond both towns.
        ground.Patch(-near, -near, near, near, 1f, InForestSquare);
        ground.Patch(-160f, -336f, 160f, 336f, 2f, (x, z) => MathF.Abs(x) < near && MathF.Abs(z) < near);
        // And west around the Deep Forest.
        ground.Patch(-336f, -144f, -160f, 144f, 2f, (_, _) => false);
        var mesh = ground.Commit();
        var material = Material("ground.gdshader");
        material.SetShaderParameter("highlight", ToColor(WorldPalette.GrassHighlight));
        material.SetShaderParameter("flowers", Array.ConvertAll(WorldPalette.Flowers, c =>
        {
            var linear = ToColor(c).SrgbToLinear();
            return new Vector3(linear.R, linear.G, linear.B);
        }));
        mesh.SurfaceSetMaterial(0, material);
        AddChild(new MeshInstance3D { Name = "Ground", Mesh = mesh });
    }

    // Instanced props

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

    private void BuildHedges()
    {
        // A band of round hedge blobs just past every walkable edge, except along the forest's
        // own edge, where the Forest module's ring of giant mushrooms stands.
        var hedges = new Instances();
        const float cell = 1.3f;
        int id = 0;
        for (float gx = -290f; gx < 112f; gx += cell)
        for (float gz = -300f; gz < 300f; gz += cell, id++)
        {
            float x = gx + cell * (Rand(id, 1) - 0.5f) * 0.6f, z = gz + cell * (Rand(id, 2) - 0.5f) * 0.6f;
            float beyond = -Areas.Depth(x, z);
            if (beyond < 0.5f || beyond > 0.5f + cell)
                continue;
            if (Areas.Forest.Depth(x, z) > -2f && Areas.RoadDistance(x, z) > Areas.RoadHalfWidth + 2.5f)
                continue;
            float size = 0.85f + 0.4f * Rand(id, 3);
            var color = ToColor(WorldPalette.HedgeDark).Lerp(ToColor(WorldPalette.HedgeLight), Rand(id, 4));
            if (DeepForestDepth(x, z) > -4f && x < -Areas.ForestHalfSize)
                color = color.Darkened(0.4f); // the Deep Forest's hedges are darker
            hedges.Add(Placed(OnGround(x, z) + Vector3.Down * 0.2f, Rand(id, 5) * MathF.Tau, new Vector3(size, size * 0.85f, size)), color);
        }
        AddChild(hedges.Build("Hedges", MapMeshes.Blob(), Material("toon_color.gdshader", outline: true)));
    }

    private void BuildMushrooms()
    {
        // One MultiMesh for every stem and one per cap shape (Fungi picks each mushroom's kind,
        // shape, size, tilt and colors from a fixed seed per spot).
        var stems = new Instances();
        var caps = new Instances[Enum.GetValues<CapShape>().Length];
        for (int i = 0; i < caps.Length; i++)
            caps[i] = new Instances();
        int seed = 0;
        Color stemLight = ToColor(MushroomPalette.StemLight), stemDark = ToColor(MushroomPalette.StemDark);

        // The foot of a mushroom standing at spot + (f.X, f.Z), sunk a little into the ground.
        static Vector3 Foot(Vector3 spot, in Fungus f) => new(spot.X + f.X,
            Map.Ground.HeightAt(spot.X + f.X, spot.Z + f.Z) - MathF.Min(0.4f, MathF.Max(f.StemHeight, f.CapHeight) * 0.08f), spot.Z + f.Z);

        // A bracket (Lift > 0) grows on its group's main stem, from mainFoot along its lean.
        void Draw(Vector3 spot, in Fungus f, Vector3 mainFoot = default, in Fungus main = default)
        {
            int id = seed++;
            var foot = f.Lift == 0f ? Foot(spot, f) : mainFoot + ToGodot(Fungi.OnStem(main, f));
            // Lean toward TiltYaw: rotate about the horizontal axis across it.
            var lean = new Basis(new Vector3(MathF.Cos(f.TiltYaw), 0f, -MathF.Sin(f.TiltYaw)), f.Tilt);
            var top = foot;
            if (f.StemHeight > 0f)
            {
                var stemColor = stemLight.Lerp(stemDark, Rand(id, 2));
                stems.Add(new Transform3D(lean * new Basis(Vector3.Up, f.Yaw) * Basis.FromScale(new Vector3(f.StemRadius, f.StemHeight, f.StemRadius)), foot), stemColor);
                top = foot + lean * Vector3.Up * (f.StemHeight - f.CapHeight * 0.18f);
            }
            var basis = lean * new Basis(Vector3.Up, f.Yaw) * Basis.FromScale(new Vector3(f.CapRadius, f.CapHeight, f.CapRadius));
            if (f.Shape == CapShape.Shelf)
                basis = new Basis(Vector3.Up, f.Yaw) * Basis.FromScale(new Vector3(f.CapRadius, f.CapHeight, f.CapRadius)) * new Basis(Vector3.Right, -0.15f);
            caps[(int)f.Shape].Add(new Transform3D(basis, top), ToColor(f.Color), new Color(Rand(id, 8), f.Glow, f.Spots, f.Pattern));
        }

        // Giant mushrooms and fungi on the cliffs (not in the forest's ring, which the Forest module
        // fills): one group per 8 m cell, no two neighboring cells of the same kind.
        const float cell = 8f, x0 = -336f, x1 = 160f, z0 = -336f, z1 = 336f;
        int rows = (int)((z1 - z0) / cell);
        var kinds = Fungi.CliffKinds((int)((x1 - x0) / cell), rows);
        Span<Fungus> group = stackalloc Fungus[Fungi.MaxGroup];
        int k = 0;
        for (float gx = x0; gx < x1; gx += cell)
        for (float gz = z0; gz < z1; gz += cell, k++)
        {
            if (gx < -160f && MathF.Abs(gz) > 144f)
                continue; // past the ground mesh
            float x = gx + cell * (0.15f + 0.7f * Rand(k, 10)), z = gz + cell * (0.15f + 0.7f * Rand(k, 11));
            if (MathF.Abs(x) < 128f && MathF.Abs(z) < 128f && Areas.RoadDistance(x, z) > 14f)
                continue;
            float size = 0.9f + 1.4f * Rand(k, 12);
            float beyond = -Areas.Depth(x, z);
            if (beyond < 3.5f + size || beyond > 45f || Rand(k, 13) < 0.25f)
                continue;
            int n = Fungi.CliffGroup(kinds[k], k, size, group);
            var spot = new Vector3(x, 0f, z);
            var mainFoot = Foot(spot, group[0]);
            for (int i = 0; i < n; i++)
                Draw(spot, group[i], mainFoot, group[0]);
        }

        // Little glowing mushroom lamps beside the roads: the Map's road props, solid, exactly where
        // and as tall as Props says (the towns' and forests' props are their modules' to draw);
        // their caps vary in shape and color (MushroomPalette.LampColors), never wider than the prop.
        int lamp = 0;
        foreach (var p in Props.All)
        {
            if (p.Area != Area.Road || p.Kind != PropKind.Lamp)
                continue;
            var f = Fungi.Lamp(lamp++, p.Height, p.Radius);
            Draw(new Vector3(p.X, 0f, p.Z), f with { Glow = MushroomPalette.LampGlow });
        }

        AddChild(stems.Build("MushroomStems", MapMeshes.Stem(), Material("toon_color.gdshader", outline: true)));
        var capMaterial = Material("mushroom_cap.gdshader", outline: true);
        capMaterial.SetShaderParameter("spot_color", ToColor(MushroomPalette.SpotColor));
        foreach (var shape in Enum.GetValues<CapShape>())
            if (caps[(int)shape].Transforms.Count > 0)
                AddChild(caps[(int)shape].Build("MushroomCaps" + shape, MapMeshes.Cap(shape), capMaterial));
    }

    private void BuildRoots()
    {
        // Low roots spreading from the foot of each Deep Forest giant mushroom over the soil, and
        // a few loose ones between: knee-high at most, so walkers step over them.
        var roots = new Instances();
        var all = Map.Ground.Obstacles;
        int first = all.Length - Map.Ground.DeepForestTrunkCount, id = 0;
        void Root(float x, float z, float yaw, float length, float thickness)
        {
            var color = new Color(0.5f, 0.37f, 0.27f).Lerp(new Color(0.62f, 0.5f, 0.38f), Rand(id, 31));
            roots.Add(Placed(OnGround(x, z) + Vector3.Down * thickness * 0.35f, yaw, new Vector3(thickness, thickness * 0.8f, length)), color);
            id++;
        }
        for (int i = first; i < all.Length; i++)
        {
            var o = all[i];
            int count = 3 + (int)(Rand(i, 32) * 3f);
            for (int n = 0; n < count; n++)
            {
                float a = (n + 0.6f * Rand(i, 33 + n)) * MathF.Tau / count;
                float length = o.Radius * (0.7f + 0.6f * Rand(i, 40 + n));
                float reach = o.Radius + length * 0.55f;
                // A yaw of a turns local +Z toward (sin a, cos a).
                Root(o.X + MathF.Sin(a) * reach, o.Z + MathF.Cos(a) * reach, a, length, 0.22f + 0.12f * o.Radius / 3f);
            }
        }
        var b = Areas.DeepForest;
        const float cell = 5f;
        int k = 0;
        for (float gx = b.MinX; gx < b.MaxX; gx += cell)
        for (float gz = b.MinZ; gz < b.MaxZ; gz += cell, k++)
        {
            float x = gx + cell * Rand(k, 50), z = gz + cell * Rand(k, 51);
            if (Rand(k, 52) < 0.55f || b.Depth(x, z) < 1f || Map.Ground.Blocks(x, z, 1.5f))
                continue;
            Root(x, z, Rand(k, 53) * MathF.Tau, 0.8f + 1.2f * Rand(k, 54), 0.14f + 0.08f * Rand(k, 55));
        }
        AddChild(roots.Build("DeepForestRoots", MapMeshes.Blob(), Material("toon_color.gdshader", outline: true)));
    }

    // Home interior

    private static ShaderMaterial Flat(Color color, float glow = 0f, bool outline = false)
    {
        var material = Material("toon_flat.gdshader", outline);
        material.SetShaderParameter("albedo", color);
        material.SetShaderParameter("glow", glow);
        return material;
    }

    private void Box(Node3D parent, string name, Vector3 center, Vector3 size, Material material, bool shadows = true)
    {
        parent.AddChild(new MeshInstance3D
        {
            Name = name,
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = material,
            Position = center,
            CastShadow = shadows ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    private void Window(Node3D parent, HomeWindow window, Material glass, Material frame)
    {
        // A round window: a glowing pane in a chunky ring, standing out from the wall facing inward
        // (glass, then frame, each clear of what is behind it so nothing z-fights; HomeLayout).
        var inward = ToGodot(window.Inward);
        var basis = new Basis(new Quaternion(Vector3.Up, inward));
        parent.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = HomeLayout.GlassRadius, BottomRadius = HomeLayout.GlassRadius, Height = HomeLayout.GlassThickness, RadialSegments = 24 },
            MaterialOverride = glass,
            Transform = new Transform3D(basis, ToGodot(window.At(window.GlassCenter))),
        });
        parent.AddChild(new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = HomeLayout.FrameInnerRadius, OuterRadius = HomeLayout.FrameOuterRadius, Rings = 24, RingSegments = 8 },
            MaterialOverride = frame,
            Transform = new Transform3D(basis * Basis.FromScale(new Vector3(1f, HomeLayout.FrameDepthScale, 1f)), ToGodot(window.At(window.FrameCenter))),
        });
    }

    private void BuildHome()
    {
        var room = Areas.Home;
        float y = Areas.HomeFloorY, cx = room.Center.X, cz = room.Center.Y;
        float w = room.MaxX - room.MinX, d = room.MaxZ - room.MinZ;
        const float wall = HomeLayout.Wall, height = HomeLayout.Height;
        var home = new Node3D { Name = "HomeInterior" };
        AddChild(home);

        // Floor, walls (the south one with the door in its middle, at Places.HomeExit), door and
        // wooden skirting all around, laid out by HomeLayout so no two faces are within 2 cm.
        var walls = Flat(new Color(0.98f, 0.9f, 0.78f));
        var trim = Flat(new Color(0.62f, 0.4f, 0.26f));
        foreach (var box in HomeLayout.Boxes)
        {
            Material material = box.Name switch
            {
                "Floor" => Material("wood_floor.gdshader"),
                "Door" => Flat(new Color(0.7f, 0.42f, 0.26f), outline: true),
                _ when box.Name.StartsWith("Skirting", StringComparison.Ordinal) => trim,
                _ => walls,
            };
            Box(home, box.Name, ToGodot(box.Center), ToGodot(box.Size), material);
        }
        home.AddChild(new MeshInstance3D
        {
            Name = "DoorKnob",
            Mesh = new SphereMesh { Radius = HomeLayout.KnobRadius, Height = 2f * HomeLayout.KnobRadius },
            MaterialOverride = Flat(new Color(1f, 0.82f, 0.36f), 0.3f),
            Position = ToGodot(HomeLayout.KnobCenter),
        });

        var glass = Flat(new Color(0.66f, 0.86f, 1f), 0.6f);
        var frame = Flat(new Color(0.62f, 0.4f, 0.26f), outline: true);
        foreach (var window in HomeLayout.Windows)
            Window(home, window, glass, frame);

        // A ceiling seen only from inside (one-sided, facing down), casting no shadow.
        home.AddChild(new MeshInstance3D
        {
            Name = "Ceiling",
            Mesh = new PlaneMesh { Size = new Vector2(w + 2f * wall, d + 2f * wall) },
            MaterialOverride = Flat(new Color(0.9f, 0.78f, 0.64f)),
            Transform = new Transform3D(new Basis(Vector3.Right, MathF.PI), new Vector3(cx, y + height, cz)),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        home.AddChild(new OmniLight3D
        {
            Name = "HomeLight",
            LightColor = new Color(1f, 0.86f, 0.66f),
            LightEnergy = 1.6f,
            OmniRange = 11f,
            Position = new Vector3(cx, y + height - 0.8f, cz),
        });
    }
}
