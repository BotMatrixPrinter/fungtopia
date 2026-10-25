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

    private static readonly Color[] CapColors = Array.ConvertAll(MushroomPalette.CapColors, ToColor);
    private static readonly Color[] LampColors = Array.ConvertAll(MushroomPalette.LampColors, ToColor);

    private static Color ToColor(System.Numerics.Vector3 c) => new(c.X, c.Y, c.Z);

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

    /// <summary>For screenshots: MAP_PREVIEW=road or cliffs makes a preview camera current.</summary>
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
            default:
                return;
        }
        _preview = new Camera3D { Name = "PreviewCamera", Fov = 70f, Far = 400f };
        AddChild(_preview);
        _preview.LookAtFromPosition(from, target, Vector3.Up);
        _preview.MakeCurrent();
    }

    /// <summary>A fixed pseudo-random value in [0, 1) for (i, k): the same scene every run.</summary>
    private static float Rand(int i, int k)
    {
        uint h = unchecked((uint)i * 2654435761u ^ (uint)k * 2246822519u ^ 0x7f4a7c15u);
        h = (h ^ (h >> 15)) * 2246822519u;
        h = (h ^ (h >> 13)) * 3266489917u;
        h ^= h >> 16;
        return (h >> 8) * (1f / 16777216f);
    }

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
        var grass = new Color(0.5f, 0.74f, 0.33f);
        var moss = new Color(0.3f, 0.56f, 0.32f);
        var meadow = new Color(0.62f, 0.82f, 0.38f);
        var dirt = new Color(0.7f, 0.52f, 0.36f);
        var rock = new Color(0.66f, 0.55f, 0.5f);
        var stone = new Color(0.7f, 0.64f, 0.56f);
        var paving = new Color(0.5f, 0.46f, 0.58f);
        var tiles = new Color(0.8f, 0.42f, 0.5f);

        // The forest's own grass and moss mottle, so the seam at its edge does not show.
        float mottle = 0.5f + 0.25f * MathF.Sin(x * 0.19f + MathF.Sin(z * 0.13f) * 2f) + 0.25f * MathF.Sin(z * 0.23f - x * 0.07f);
        var color = grass.Lerp(moss, Mathf.SmoothStep(0.35f, 0.85f, mottle));
        float outside = MathF.Max(MathF.Abs(x), MathF.Abs(z)) - Areas.ForestHalfSize;
        color = color.Lerp(moss * 0.85f, Mathf.SmoothStep(-4f, 6f, outside));

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
            var deepMoss = new Color(0.17f, 0.32f, 0.2f);
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
        color = color.Lerp(rock, Mathf.SmoothStep(0.92f, 0.75f, normal.Y));
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
        mesh.SurfaceSetMaterial(0, Material("ground.gdshader"));
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
            var color = new Color(0.3f, 0.6f, 0.3f).Lerp(new Color(0.42f, 0.7f, 0.32f), Rand(id, 4));
            if (DeepForestDepth(x, z) > -4f && x < -Areas.ForestHalfSize)
                color = color.Darkened(0.4f); // the Deep Forest's hedges are darker
            hedges.Add(Placed(OnGround(x, z) + Vector3.Down * 0.2f, Rand(id, 5) * MathF.Tau, new Vector3(size, size * 0.85f, size)), color);
        }
        AddChild(hedges.Build("Hedges", MapMeshes.Blob(), Material("toon_color.gdshader", outline: true)));
    }

    private void BuildMushrooms()
    {
        var stems = new Instances();
        var caps = new Instances();
        int seed = 0;
        Color stemLight = ToColor(MushroomPalette.StemLight), stemDark = ToColor(MushroomPalette.StemDark);

        void Mushroom(float x, float z, float stemRadius, float height, float capRadius, Color color, float glow)
        {
            int id = seed++;
            float yaw = Rand(id, 1) * MathF.Tau;
            var foot = OnGround(x, z) + Vector3.Down * MathF.Min(0.4f, height * 0.08f);
            var stemColor = stemLight.Lerp(stemDark, Rand(id, 2));
            stems.Add(Placed(foot, yaw, new Vector3(stemRadius, height, stemRadius)), stemColor);
            float capHeight = capRadius * (0.5f + 0.3f * Rand(id, 6));
            caps.Add(Placed(foot + Vector3.Up * (height - capHeight * 0.18f), yaw, new Vector3(capRadius, capHeight, capRadius)),
                color, new Color(Rand(id, 8), glow, 0f, 0f));
        }

        // Giant mushrooms on the cliffs (not in the forest's ring, which the Forest module fills).
        const float cell = 8f;
        int k = 0;
        for (float gx = -336f; gx < 160f; gx += cell)
        for (float gz = -336f; gz < 336f; gz += cell, k++)
        {
            if (gx < -160f && MathF.Abs(gz) > 144f)
                continue; // past the ground mesh
            float x = gx + cell * (0.15f + 0.7f * Rand(k, 10)), z = gz + cell * (0.15f + 0.7f * Rand(k, 11));
            if (MathF.Abs(x) < 128f && MathF.Abs(z) < 128f && Areas.RoadDistance(x, z) > 14f)
                continue;
            float r = 0.9f + 1.4f * Rand(k, 12);
            float beyond = -Areas.Depth(x, z);
            if (beyond < 3.5f + r || beyond > 45f || Rand(k, 13) < 0.25f)
                continue;
            var color = CapColors[(int)(Rand(k, 15) * CapColors.Length)];
            Mushroom(x, z, r, 6f + r * 2.6f + 5f * Rand(k, 16), r * (2.4f + 1.2f * Rand(k, 17)), color, Rand(k, 18) < 0.12f ? MushroomPalette.GiantGlow : 0f);
        }

        // Little glowing mushroom lamps beside the roads: the Map's road props, solid, exactly where
        // and as tall as Props says (the towns' and forests' props are their modules' to draw).
        int lamp = 0;
        foreach (var p in Props.All)
        {
            if (p.Area != Area.Road || p.Kind != PropKind.Lamp)
                continue;
            Mushroom(p.X, p.Z, 0.1f, p.Height, p.Radius, LampColors[lamp++ % LampColors.Length], MushroomPalette.LampGlow);
        }

        AddChild(stems.Build("MushroomStems", MapMeshes.Stem(), Material("toon_color.gdshader", outline: true)));
        var capMaterial = Material("mushroom_cap.gdshader", outline: true);
        capMaterial.SetShaderParameter("spot_color", ToColor(MushroomPalette.SpotColor));
        AddChild(caps.Build("MushroomCaps", MapMeshes.Cap(), capMaterial));
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

    private void Window(Node3D parent, Vector3 center, Vector3 inward, Material glass, Material frame)
    {
        // A round window: a glowing pane in a chunky ring, set into the wall facing inward.
        var basis = new Basis(new Quaternion(Vector3.Up, inward));
        parent.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.62f, BottomRadius = 0.62f, Height = 0.04f, RadialSegments = 24 },
            MaterialOverride = glass,
            Transform = new Transform3D(basis, center),
        });
        parent.AddChild(new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.6f, OuterRadius = 0.78f, Rings = 24, RingSegments = 8 },
            MaterialOverride = frame,
            Transform = new Transform3D(basis, center + inward * 0.02f),
        });
    }

    private void BuildHome()
    {
        var room = Areas.Home;
        float y = Areas.HomeFloorY, cx = room.Center.X, cz = room.Center.Y;
        float w = room.MaxX - room.MinX, d = room.MaxZ - room.MinZ;
        const float wall = 0.3f, height = 4f, doorWidth = 1.6f, doorHeight = 2.4f;
        var home = new Node3D { Name = "HomeInterior" };
        AddChild(home);

        var floor = Material("wood_floor.gdshader");
        Box(home, "Floor", new Vector3(cx, y - 0.1f, cz), new Vector3(w + 2f * wall, 0.2f, d + 2f * wall), floor);

        var walls = Flat(new Color(0.98f, 0.9f, 0.78f));
        var trim = Flat(new Color(0.62f, 0.4f, 0.26f));
        float wy = y + height * 0.5f;
        Box(home, "WallNorth", new Vector3(cx, wy, room.MinZ - wall * 0.5f), new Vector3(w + 2f * wall, height, wall), walls);
        Box(home, "WallWest", new Vector3(room.MinX - wall * 0.5f, wy, cz), new Vector3(wall, height, d), walls);
        Box(home, "WallEast", new Vector3(room.MaxX + wall * 0.5f, wy, cz), new Vector3(wall, height, d), walls);
        // The south wall has the door in its middle, at Places.HomeExit.
        float doorX = Places.HomeExit.Position.X, side = (w - doorWidth) * 0.5f;
        float sz = room.MaxZ + wall * 0.5f;
        Box(home, "WallSouthWest", new Vector3(room.MinX + side * 0.5f - wall * 0.5f, wy, sz), new Vector3(side + wall, height, wall), walls);
        Box(home, "WallSouthEast", new Vector3(room.MaxX - side * 0.5f + wall * 0.5f, wy, sz), new Vector3(side + wall, height, wall), walls);
        Box(home, "WallSouthLintel", new Vector3(doorX, y + (height + doorHeight) * 0.5f, sz), new Vector3(doorWidth, height - doorHeight, wall), walls);
        Box(home, "Door", new Vector3(doorX, y + doorHeight * 0.5f, sz), new Vector3(doorWidth - 0.1f, doorHeight - 0.05f, 0.12f), Flat(new Color(0.7f, 0.42f, 0.26f), outline: true));
        home.AddChild(new MeshInstance3D
        {
            Name = "DoorKnob",
            Mesh = new SphereMesh { Radius = 0.08f, Height = 0.16f },
            MaterialOverride = Flat(new Color(1f, 0.82f, 0.36f), 0.3f),
            Position = new Vector3(doorX + 0.5f, y + 1.1f, sz - 0.1f),
        });
        // Wooden skirting all around.
        Box(home, "SkirtingNorth", new Vector3(cx, y + 0.1f, room.MinZ + 0.03f), new Vector3(w, 0.2f, 0.06f), trim);
        Box(home, "SkirtingWest", new Vector3(room.MinX + 0.03f, y + 0.1f, cz), new Vector3(0.06f, 0.2f, d), trim);
        Box(home, "SkirtingEast", new Vector3(room.MaxX - 0.03f, y + 0.1f, cz), new Vector3(0.06f, 0.2f, d), trim);

        var glass = Flat(new Color(0.66f, 0.86f, 1f), 0.6f);
        var frame = Flat(new Color(0.62f, 0.4f, 0.26f), outline: true);
        float windowY = y + 2.1f;
        Window(home, new Vector3(cx - 3f, windowY, room.MinZ - 0.02f), Vector3.Back, glass, frame);
        Window(home, new Vector3(cx + 3f, windowY, room.MinZ - 0.02f), Vector3.Back, glass, frame);
        Window(home, new Vector3(room.MinX - 0.02f, windowY, cz), Vector3.Right, glass, frame);
        Window(home, new Vector3(room.MaxX + 0.02f, windowY, cz), Vector3.Left, glass, frame);

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
