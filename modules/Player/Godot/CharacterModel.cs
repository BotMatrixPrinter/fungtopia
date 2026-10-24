using System;
using System.Collections.Generic;
using Godot;

namespace Fungtopia.Player.View;

/// <summary>
/// One player character (girl.glb or boy.glb from modules/Player/Models), drawn toon-shaded with
/// ink outlines and animated procedurally: walk bob, leg and arm swing, jump stretch and landing
/// squash, a white hit flash, and tipping over when defeated. Faces -Z at zero rotation.
/// </summary>
public partial class CharacterModel : Node3D
{
    private const string Models = "res://modules/Player/Models/";
    private const string Shaders = "res://modules/Player/Godot/Shaders/";

    /// <summary>Surfaces drawn without an ink outline (small soft details).</summary>
    private static readonly HashSet<string> NoOutline = new() { "EyeShine", "Blush", "Mouth", "String" };

    private static Shader? s_toon;
    private static ShaderMaterial? s_outline;

    private readonly Node3D _rig = new() { Name = "Rig" };
    private readonly List<GeometryInstance3D> _meshes = new();
    private Part _head, _body, _legL, _legR, _armL, _armR, _backBow;

    private float _walkPhase;
    private float _walkAmount;
    private float _squash;
    private float _stretch;
    private float _tipOver;
    private float _time;
    private float _shownFlash = -1f;
    private bool _wasGrounded = true;

    public CharacterKind Kind { get; private set; }

    /// <summary>A part of the model and its rest transform, so animation always starts from rest.</summary>
    private struct Part
    {
        public Node3D? Node;
        public Transform3D Rest;

        public Part(Node3D? node)
        {
            Node = node;
            Rest = node?.Transform ?? Transform3D.Identity;
        }

        public readonly void Pose(Vector3 rotation, Vector3 offset = default)
        {
            if (Node == null)
                return;
            var basis = Basis.FromEuler(rotation) * Rest.Basis;
            Node.Transform = new Transform3D(basis, Rest.Origin + offset);
        }
    }

    public static CharacterModel Create(CharacterKind kind)
    {
        var model = new CharacterModel { Name = kind.ToString(), Kind = kind };
        model.Build();
        return model;
    }

    private void Build()
    {
        AddChild(_rig);
        var scene = LoadGlb(Models + (Kind == CharacterKind.Boy ? "boy.glb" : "girl.glb"));
        if (scene == null)
            return;
        _rig.AddChild(scene);
        CollectMeshes(scene);
        _head = new Part(Find(scene, "Head"));
        _body = new Part(Find(scene, "Body"));
        _legL = new Part(Find(scene, "LegL"));
        _legR = new Part(Find(scene, "LegR"));
        _armL = new Part(Find(scene, "ArmL"));
        _armR = new Part(Find(scene, "ArmR"));
        _backBow = new Part(Find(scene, "BackBow"));
    }

    /// <summary>
    /// Loads a .glb at run time from its bytes, so the game runs without the editor having
    /// imported it first (a fresh checkout loads headless).
    /// </summary>
    private static Node3D? LoadGlb(string path)
    {
        byte[] bytes = FileAccess.GetFileAsBytes(path);
        if (bytes.Length == 0)
        {
            GD.PushWarning($"Fungtopia: Player: model {path} is missing.");
            return null;
        }
        var document = new GltfDocument();
        var state = new GltfState();
        var error = document.AppendFromBuffer(bytes, Models, state);
        if (error != Error.Ok)
        {
            GD.PushWarning($"Fungtopia: Player: model {path} could not be read ({error}).");
            return null;
        }
        return document.GenerateScene(state) as Node3D;
    }

    private static Node3D? Find(Node root, string name) => root.FindChild(name, true, false) as Node3D;

    private void CollectMeshes(Node node)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh != null)
        {
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
                mesh.SetSurfaceOverrideMaterial(i, ToonMaterial(mesh.Mesh.SurfaceGetMaterial(i)));
            _meshes.Add(mesh);
        }
        foreach (var child in node.GetChildren())
            CollectMeshes(child);
    }

    private static readonly Dictionary<string, ShaderMaterial> s_materials = new();

    /// <summary>The toon material for a glTF material: its color, an ink outline unless it is a small detail.</summary>
    private static ShaderMaterial ToonMaterial(Material? source)
    {
        string name = source?.ResourceName ?? "";
        var color = source is BaseMaterial3D standard ? standard.AlbedoColor : Colors.White;
        string key = name + color.ToHtml();
        if (s_materials.TryGetValue(key, out var cached))
            return cached;
        s_toon ??= GD.Load<Shader>(Shaders + "toon.gdshader");
        s_outline ??= new ShaderMaterial { Shader = GD.Load<Shader>(Shaders + "outline.gdshader") };
        var material = new ShaderMaterial { Shader = s_toon };
        // glTF stores linear colors; the shader's albedo is an sRGB color like Blender's swatch.
        material.SetShaderParameter("albedo", color.SrgbToLinear());
        if (name == "EyeShine")
            material.SetShaderParameter("self_light", 0.6f);
        if (!NoOutline.Contains(name))
            material.NextPass = s_outline;
        s_materials[key] = material;
        return material;
    }

    /// <summary>
    /// Advances the animation by <paramref name="delta"/> seconds of frame time.
    /// </summary>
    /// <param name="speed">Horizontal speed, meters per second.</param>
    /// <param name="grounded">Standing on the ground.</param>
    /// <param name="verticalSpeed">Meters per second, up positive.</param>
    /// <param name="flash">0..1 white hit flash.</param>
    /// <param name="alive">False tips the character over.</param>
    public void Animate(float delta, float speed, bool grounded, float verticalSpeed, float flash, bool alive)
    {
        _time += delta;
        float walkTarget = grounded && alive ? Math.Clamp(speed / 5f, 0f, 1f) : 0f;
        _walkAmount = Mathf.MoveToward(_walkAmount, walkTarget, delta * 6f);
        // About two steps per meter and a half: the feet keep up with the ground.
        _walkPhase = (_walkPhase + delta * speed * 2.6f) % Mathf.Tau;

        if (grounded && !_wasGrounded)
            _squash = 1f; // landing
        _wasGrounded = grounded;
        _squash = Mathf.MoveToward(_squash, 0f, delta * 5f);
        float stretchTarget = grounded ? 0f : Math.Clamp(verticalSpeed / 7f, -0.4f, 1f);
        _stretch = Mathf.Lerp(_stretch, stretchTarget, Math.Min(1f, delta * 14f));
        _tipOver = Mathf.MoveToward(_tipOver, alive ? 0f : 1f, delta * (alive ? 3f : 2.5f));

        float swing = MathF.Sin(_walkPhase) * 0.65f * _walkAmount;
        float bob = MathF.Abs(MathF.Sin(_walkPhase)) * 0.05f * _walkAmount;
        float breathe = MathF.Sin(_time * 2.4f) * 0.012f * (1f - _walkAmount);
        float airLegs = grounded ? 0f : 0.35f;

        // Squash and stretch scale the whole rig from the feet, keeping its volume about the same.
        float s = _squash * _squash;
        float up = 1f - 0.28f * s + 0.12f * _stretch + breathe;
        float wide = 1f + 0.18f * s - 0.05f * _stretch;
        float fall = Mathf.SmoothStep(0f, 1f, _tipOver);
        var lie = Basis.FromEuler(new Vector3(fall * Mathf.Pi * 0.5f, 0f, 0f));
        _rig.Transform = new Transform3D(lie * Basis.FromScale(new Vector3(wide, up, wide)), new Vector3(0f, bob, 0f));

        _legL.Pose(new Vector3(swing + airLegs * 0.6f, 0f, 0f));
        _legR.Pose(new Vector3(-swing - airLegs * 0.4f, 0f, 0f));
        float armsUp = grounded ? 0f : 0.9f;
        _armL.Pose(new Vector3(-swing * 0.9f, 0f, -armsUp));
        _armR.Pose(new Vector3(swing * 0.9f, 0f, armsUp));
        _body.Pose(new Vector3(-0.08f * _walkAmount, MathF.Sin(_walkPhase) * 0.08f * _walkAmount, 0f));
        _head.Pose(new Vector3(0.05f * _walkAmount - 0.06f * s, 0f, MathF.Sin(_walkPhase) * 0.05f * _walkAmount));
        _backBow.Pose(new Vector3(-0.08f * _walkAmount, MathF.Sin(_walkPhase) * 0.08f * _walkAmount, 0f),
            new Vector3(0f, bob * 0.3f, 0f));

        if (MathF.Abs(flash - _shownFlash) > 0.01f)
        {
            _shownFlash = flash;
            foreach (var mesh in _meshes)
                mesh.SetInstanceShaderParameter("flash", flash);
        }
    }
}
