using System;
using System.Collections.Generic;
using Fungtopia.Map;
using Fungtopia.Shell;
using Godot;
using Prop = Fungtopia.DeepForest.Prop;

namespace Fungtopia.DeepForest.View;

/// <summary>
/// Draws the Deep Forest from <see cref="DeepForestModule.Scenery"/>: the huge old giant mushrooms
/// on the Map's Deep Forest obstacles with their side stems, canopy caps, shelves, roots and webs;
/// glowing mushrooms, toadstools, ferns, drifting spores and low mist; FogVolumes (volumetric fog,
/// switched on by DayNight) and a few soft lights. Every kind is one MultiMesh, from the unit models
/// in Models/deep_forest.glb (built by Models/deep_forest.py). The canopy casts the sun's real
/// shadows, so the ground under it is dark by day. Sun, sky and environment are DayNight's; the
/// ground and its low roots are the Map's.
/// For screenshots, DEEPFOREST_PREVIEW=inside or entrance makes a preview camera current and hides
/// the screens over the world (start screen, HUD); without it the view never touches them.
/// </summary>
public partial class DeepForestView : Node3D, IModuleView
{
    private const string Shaders = "res://modules/DeepForest/Godot/Shaders/";
    private const string ModelFile = "res://modules/DeepForest/Models/deep_forest.glb";

    private DeepForestModule? _module;
    private Camera3D? _preview;

    public void Bind(IModule module) => _module = (DeepForestModule)module;

    public override void _Ready()
    {
        var scenery = _module?.Scenery ?? DeepForestScenery.Create();
        var meshes = LoadMeshes();
        BuildMushrooms(scenery, meshes);
        BuildProps(scenery, meshes);
        BuildAir(scenery);
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

    /// <summary>The unit meshes by object name, read at run time from the .glb's bytes (no editor import needed).</summary>
    private static Dictionary<string, Mesh> LoadMeshes()
    {
        var meshes = new Dictionary<string, Mesh>();
        var bytes = FileAccess.GetFileAsBytes(ModelFile);
        var document = new GltfDocument();
        var state = new GltfState();
        if (bytes.Length == 0 || document.AppendFromBuffer(bytes, ModelFile.GetBaseDir(), state) != Error.Ok
            || document.GenerateScene(state) is not Node scene)
        {
            GD.PushWarning($"Fungtopia: DeepForest: models {ModelFile} could not be read; drawing simple shapes.");
            return meshes;
        }
        Collect(scene, meshes);
        scene.Free();
        return meshes;

        static void Collect(Node node, Dictionary<string, Mesh> into)
        {
            if (node is MeshInstance3D instance && instance.Mesh != null)
                into[instance.Name] = instance.Mesh;
            foreach (var child in node.GetChildren())
                Collect(child, into);
        }
    }

    private static Mesh MeshOf(Dictionary<string, Mesh> meshes, string name) =>
        meshes.TryGetValue(name, out var mesh) ? mesh : new SphereMesh { Radius = 1f, Height = 1f };

    private static ShaderMaterial Material(string shader, bool outline = false)
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>(Shaders + shader) };
        if (outline)
            material.NextPass = new ShaderMaterial { Shader = GD.Load<Shader>(Shaders + "outline.gdshader") };
        return material;
    }

    private static Godot.Vector3 V(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);

    private static Color C(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);

    private static Transform3D Placed(in Prop p) =>
        new(new Basis(new Quaternion(p.Rotation.X, p.Rotation.Y, p.Rotation.Z, p.Rotation.W)) * Basis.FromScale(V(p.Scale)), V(p.Position));

    /// <summary>Instances of one mesh, drawn as a single MultiMesh.</summary>
    private sealed class Instances
    {
        private readonly List<Transform3D> _transforms = new();
        private readonly List<Color> _colors = new();
        private readonly List<Color> _custom = new();

        public void Add(Transform3D transform, Color color, Color custom = default)
        {
            _transforms.Add(transform);
            _colors.Add(color);
            _custom.Add(custom);
        }

        public MultiMeshInstance3D Build(string name, Mesh mesh, Material material, bool shadows = true)
        {
            var multi = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = true,
                UseCustomData = true,
                Mesh = mesh,
                InstanceCount = _transforms.Count,
            };
            for (int i = 0; i < _transforms.Count; i++)
            {
                multi.SetInstanceTransform(i, _transforms[i]);
                multi.SetInstanceColor(i, _colors[i]);
                multi.SetInstanceCustomData(i, _custom[i]);
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

    private void BuildMushrooms(DeepForestScenery scenery, Dictionary<string, Mesh> meshes)
    {
        var stems = new Instances();
        var caps = new Instances();
        foreach (var kind in new[] { PropKind.GiantStem, PropKind.BranchStem })
            foreach (var p in scenery.Of(kind))
                stems.Add(Placed(p), C(p.Color));
        foreach (var kind in new[] { PropKind.GiantCap, PropKind.BranchCap })
            foreach (var p in scenery.Of(kind))
                caps.Add(Placed(p), C(p.Color), new Color(p.Seed, p.Glow, 0f, 0f));
        // Toadstools: a pale little stem and a dark cap from the same meshes.
        foreach (var p in scenery.Of(PropKind.Toadstool))
        {
            var t = Placed(p);
            float s = p.Scale.X;
            var stemBasis = t.Basis.Orthonormalized() * Basis.FromScale(new Godot.Vector3(0.055f, 0.42f, 0.055f) * s);
            stems.Add(new Transform3D(stemBasis, t.Origin), new Color(0.62f, 0.58f, 0.56f));
            var capBasis = t.Basis.Orthonormalized() * Basis.FromScale(new Godot.Vector3(0.24f, 0.16f, 0.24f) * s);
            caps.Add(new Transform3D(capBasis, t.Origin + Godot.Vector3.Up * (0.42f * s - 0.03f * s)), C(p.Color), new Color(p.Seed, 0.8f, 0f, 0f));
        }
        AddChild(stems.Build("Stems", MeshOf(meshes, "Stem"), Material("toon_color.gdshader", outline: true)));
        AddChild(caps.Build("Caps", MeshOf(meshes, "Cap"), Material("cap.gdshader", outline: true)));
    }

    private void BuildProps(DeepForestScenery scenery, Dictionary<string, Mesh> meshes)
    {
        MultiMeshInstance3D Simple(PropKind kind, string name, string mesh, Material material, bool shadows = true)
        {
            var instances = new Instances();
            foreach (var p in scenery.Of(kind))
                instances.Add(Placed(p), C(p.Color), new Color(p.Seed, p.Glow, 0f, 0f));
            var node = instances.Build(name, MeshOf(meshes, mesh), material, shadows);
            AddChild(node);
            return node;
        }

        var shelfMaterial = Material("toon_color.gdshader", outline: true);
        shelfMaterial.SetShaderParameter("moss_height", 0f);
        Simple(PropKind.Shelf, "Shelves", "Shelf", shelfMaterial);
        Simple(PropKind.Root, "Roots", "Root", Material("toon_color.gdshader", outline: true));
        Simple(PropKind.Web, "Webs", "Web", Material("web.gdshader"), shadows: false);
        Simple(PropKind.Glow, "GlowingMushrooms", "Glow", Material("glow.gdshader", outline: true), shadows: false);
        Simple(PropKind.Fern, "Ferns", "Fern", Material("fern.gdshader"), shadows: false);

        var quad = new QuadMesh { Size = Godot.Vector2.One };
        var spores = Simple(PropKind.Spore, "Spores", "", Material("spore.gdshader"), shadows: false);
        spores.Multimesh.Mesh = quad;
        spores.ExtraCullMargin = 4f; // they drift in the shader
        var mist = Simple(PropKind.Mist, "Mist", "", Material("mist.gdshader"), shadows: false);
        mist.Multimesh.Mesh = quad;
        mist.ExtraCullMargin = 4f;
    }

    private void BuildAir(DeepForestScenery scenery)
    {
        // Soft, low fog, a little lavender, faintly lit from within so it is not black in the shade.
        int n = 0;
        foreach (var f in scenery.Fog)
        {
            var material = new FogMaterial
            {
                Density = f.Density,
                Albedo = new Color(0.62f, 0.6f, 0.74f),
                Emission = new Color(0.03f, 0.03f, 0.05f),
                HeightFalloff = f.Ellipsoid ? 0f : 0.25f,
                EdgeFade = f.Ellipsoid ? 1f : 0.35f,
            };
            AddChild(new FogVolume
            {
                Name = $"Fog{n++}",
                Shape = f.Ellipsoid ? RenderingServer.FogVolumeShape.Ellipsoid : RenderingServer.FogVolumeShape.Box,
                Size = V(f.Size),
                Position = V(f.Center),
                Material = material,
            });
        }
        n = 0;
        foreach (var l in scenery.Lights)
        {
            AddChild(new OmniLight3D
            {
                Name = $"GlowLight{n++}",
                Position = V(l.Position),
                LightColor = C(l.Color),
                LightEnergy = 0.9f,
                OmniRange = l.Range,
                OmniAttenuation = 1.4f,
                ShadowEnabled = false,
                DistanceFadeEnabled = true,
                DistanceFadeBegin = 45f,
                DistanceFadeLength = 15f,
            });
        }
    }

    private void BuildPreviewCamera()
    {
        string mode = OS.GetEnvironment("DEEPFOREST_PREVIEW").ToLowerInvariant();
        Godot.Vector3 eye, target;
        switch (mode)
        {
            case "inside":
                // In the clearing of a monster spawn spot deep in, looking further in.
                eye = OnGround(-180f, 30f) + Godot.Vector3.Up * 2.2f;
                target = OnGround(-215f, 20f) + Godot.Vector3.Up * 4f;
                break;
            case "entrance":
                // On the overgrown path at the forest's west gap, looking into the Deep Forest.
                eye = OnGround(-90f, 1.5f) + Godot.Vector3.Up * 2.2f;
                target = OnGround(-130f, -2f) + Godot.Vector3.Up * 3f;
                break;
            default:
                return;
        }
        _preview = new Camera3D { Name = "PreviewCamera", Fov = 70f, Far = 400f };
        AddChild(_preview);
        _preview.LookAtFromPosition(eye, target, Godot.Vector3.Up);
        _preview.MakeCurrent();
    }

    private static Godot.Vector3 OnGround(float x, float z) => new(x, Ground.HeightAt(x, z), z);
}
