using System.Collections.Generic;
using Godot;

namespace Fungtopia.StartScreen.View;

/// <summary>
/// A little world of its own, rendered into a texture: a preview model (girl, boy, cat or dog)
/// on a turntable, or Mushroom Town (<see cref="Town"/>) seen from a slowly circling camera. The
/// models are this module's Models/*.glb, loaded at run time from the file's bytes (so a fresh
/// checkout runs without an editor import), toon-shaded with ink outlines.
/// </summary>
public partial class PreviewStage : SubViewport
{
    private const string Models = "res://modules/StartScreen/Models/";
    private const string Shaders = "res://modules/StartScreen/Godot/Shaders/";
    private static readonly HashSet<string> NoOutline = new() { "EyeShine", "Blush", "Mouth", "Spots", "Window", "Nose", "Tongue", "Tag", "GrassDark", "Path" };
    private static readonly HashSet<string> Glowing = new() { "EyeShine", "Window" };
    private static readonly Dictionary<string, ShaderMaterial> s_materials = new();
    private static Shader? s_toon;
    private static ShaderMaterial? s_outline;

    private Node3D? _model;
    private Camera3D _camera = null!;
    private float _time;
    private float _yaw;
    private float _spin;
    private const float SpinCycle = 5f;
    private const float SpinSeconds = 2f;

    /// <summary>The model's file name without ".glb": girl, boy, cat, dog or town.</summary>
    public string Model { get; set; } = "girl";

    public bool Town => Model == "town";

    /// <summary>The highlighted choice turns round on its turntable; the others sway gently.</summary>
    public bool Spinning { get; set; }

    public override void _Ready()
    {
        OwnWorld3D = true;
        TransparentBg = !Town;
        Msaa3D = Town ? Msaa.Disabled : Msaa.Msaa4X;
        RenderTargetUpdateMode = UpdateMode.Always;

        var environment = new Godot.Environment
        {
            BackgroundMode = Town ? Godot.Environment.BGMode.Sky : Godot.Environment.BGMode.ClearColor,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.66f, 0.58f, 0.74f),
            AmbientLightEnergy = 0.95f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            TonemapWhite = 6f,
            AdjustmentEnabled = true,
            AdjustmentSaturation = 1.2f,
        };
        if (Town)
        {
            environment.Sky = new Sky
            {
                SkyMaterial = new ProceduralSkyMaterial
                {
                    SkyTopColor = new Color(0.42f, 0.62f, 0.98f),
                    SkyHorizonColor = new Color(0.78f, 0.86f, 1f),
                    GroundHorizonColor = new Color(1f, 0.82f, 0.7f),
                    GroundBottomColor = new Color(0.55f, 0.7f, 0.45f),
                },
            };
            environment.FogEnabled = true;
            environment.FogLightColor = new Color(1f, 0.86f, 0.8f);
            environment.FogDensity = 0.006f;
        }
        AddChild(new WorldEnvironment { Environment = environment });
        AddChild(new DirectionalLight3D
        {
            LightColor = new Color(1f, 0.9f, 0.74f),
            LightEnergy = 1.25f,
            ShadowEnabled = Town,
            RotationDegrees = new Vector3(-42f, -35f, 0f),
        });

        _camera = new Camera3D { Current = true, Fov = Town ? 55f : 26f };
        AddChild(_camera);
        if (!Town)
        {
            // A little round stand: a pink mushroom cap cut flat.
            AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.55f, BottomRadius = 0.6f, Height = 0.08f, RadialSegments = 40 },
                Position = new Vector3(0f, -0.04f, 0f),
                MaterialOverride = Toon("Stand", new Color(1f, 0.82f, 0.82f)),
            });
        }

        _model = LoadGlb(Models + Model + ".glb");
        if (_model != null)
        {
            ApplyToon(_model);
            AddChild(_model);
        }
        Place(0f);
    }

    public override void _Process(double delta)
    {
        if (RenderTargetUpdateMode == UpdateMode.Disabled)
            return;
        float dt = (float)delta;
        _time += dt;
        Place(dt);
    }

    private void Place(float dt)
    {
        if (Town)
        {
            // Drift slowly from side to side in front of the town square, looking at the houses.
            float a = Mathf.Sin(_time * 0.05f);
            _camera.Position = new Vector3(a * 9f, 6f, 13f);
            _camera.LookAt(new Vector3(a * 3f, 2.5f, -10f), Vector3.Up);
            return;
        }
        float height = Model is "cat" or "dog" ? 0.5f : 0.75f;
        float distance = Model is "cat" or "dog" ? 3.1f : 4.2f;
        _camera.Position = new Vector3(0f, height + 0.35f, distance);
        _camera.LookAt(new Vector3(0f, height, 0f), Vector3.Up);
        if (_model == null)
            return;
        // Models face -Z: a half turn shows their faces to the camera at +Z.
        // The highlighted one hops, facing front for a while, then turns once round on its
        // turntable; the others sway and turn back to face front.
        _spin = Spinning ? _spin + dt : 0f;
        float cycle = _spin % SpinCycle;
        if (Spinning && cycle > SpinCycle - SpinSeconds)
            _yaw = Mathf.SmoothStep(0f, 1f, (cycle - (SpinCycle - SpinSeconds)) / SpinSeconds) * Mathf.Tau;
        else
            _yaw = Mathf.Wrap(_yaw, -Mathf.Pi, Mathf.Pi) * Mathf.Max(0f, 1f - dt * 4f);
        float sway = Mathf.Sin(_time * 1.3f) * (Spinning ? 0.15f : 0.3f);
        _model.Rotation = new Vector3(0f, Mathf.Pi + _yaw + sway, 0f);
        float hop = Spinning ? Mathf.Abs(Mathf.Sin(_spin * 5f)) * 0.07f : 0f;
        _model.Position = new Vector3(0f, hop, 0f);
    }

    private static Node3D? LoadGlb(string path)
    {
        byte[] bytes = FileAccess.GetFileAsBytes(path);
        if (bytes.Length == 0)
        {
            GD.PushWarning($"Fungtopia: StartScreen: model {path} is missing.");
            return null;
        }
        var document = new GltfDocument();
        var state = new GltfState();
        if (document.AppendFromBuffer(bytes, Models, state) != Error.Ok)
        {
            GD.PushWarning($"Fungtopia: StartScreen: model {path} could not be read.");
            return null;
        }
        return document.GenerateScene(state) as Node3D;
    }

    private void ApplyToon(Node node)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh != null)
        {
            mesh.CastShadow = Town ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off;
            for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
            {
                var source = mesh.Mesh.SurfaceGetMaterial(i);
                var color = source is BaseMaterial3D standard ? standard.AlbedoColor : Colors.White;
                mesh.SetSurfaceOverrideMaterial(i, Toon(source?.ResourceName ?? "", color));
            }
        }
        foreach (var child in node.GetChildren())
            ApplyToon(child);
    }

    /// <summary>The toon material for a color: an ink outline unless it is a small detail.</summary>
    private static ShaderMaterial Toon(string name, Color color)
    {
        string key = name + color.ToHtml();
        if (s_materials.TryGetValue(key, out var cached))
            return cached;
        s_toon ??= GD.Load<Shader>(Shaders + "toon.gdshader");
        s_outline ??= new ShaderMaterial { Shader = GD.Load<Shader>(Shaders + "outline.gdshader") };
        var material = new ShaderMaterial { Shader = s_toon };
        // glTF stores linear colors; the shader's albedo is an sRGB color like Blender's swatch.
        material.SetShaderParameter("albedo", color.SrgbToLinear());
        if (Glowing.Contains(name))
            material.SetShaderParameter("self_light", 0.6f);
        if (!NoOutline.Contains(name))
            material.NextPass = s_outline;
        s_materials[key] = material;
        return material;
    }
}
