using System;
using System.Collections.Generic;
using Godot;

namespace Fungtopia.Pet.View;

/// <summary>
/// One pet (cat.glb, dog.glb or crow.glb from modules/Pet/Models), drawn toon-shaded with ink
/// outlines and animated procedurally: for the Cat and the Dog a trot on diagonal leg pairs, a
/// pounce on each bite, sitting down, and a wagging tail; for the Crow flapping wings, a hovering
/// bob, a forward lean while it swoops, a peck on each hit, and folded wings once it has landed.
/// Faces -Z at zero rotation.
/// </summary>
public partial class PetModel : Node3D
{
    private const string Models = "res://modules/Pet/Models/";
    private const string Shaders = "res://modules/Pet/Godot/Shaders/";

    /// <summary>Surfaces drawn without an ink outline (small soft details).</summary>
    private static readonly HashSet<string> NoOutline = new() { "EyeShine", "Blush", "Mouth", "Whisker", "Tongue" };

    /// <summary>How long a pounce lasts, seconds.</summary>
    private const float PounceSeconds = 0.4f;

    /// <summary>The front shoulders (Godot coordinates): the pet sits by tipping back around them.</summary>
    private static readonly Vector3 Shoulders = new(0f, 0.28f, -0.15f);

    private static Shader? s_toon;
    private static ShaderMaterial? s_outline;
    private static readonly Dictionary<string, ShaderMaterial> s_materials = new();

    private readonly Node3D _rig = new() { Name = "Rig" };
    private Part _head, _body, _legFL, _legFR, _legBL, _legBR, _tail;
    private Part _wingL, _wingR, _legL, _legR;

    private float _trotPhase;
    private float _trotAmount;
    private float _sit;
    private float _time;
    private float _wagPhase;
    private float _flapPhase;
    private float _flight;

    public PetKind Kind { get; private set; }

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

    public static PetModel Create(PetKind kind)
    {
        var model = new PetModel { Name = kind.ToString(), Kind = kind };
        model.Build();
        return model;
    }

    private void Build()
    {
        AddChild(_rig);
        var scene = LoadGlb(Models + Kind switch { PetKind.Dog => "dog.glb", PetKind.Crow => "crow.glb", _ => "cat.glb" });
        if (scene == null)
            return;
        _rig.AddChild(scene);
        ApplyMaterials(scene);
        _head = new Part(Find(scene, "Head"));
        _body = new Part(Find(scene, "Body"));
        _legFL = new Part(Find(scene, "LegFL"));
        _legFR = new Part(Find(scene, "LegFR"));
        _legBL = new Part(Find(scene, "LegBL"));
        _legBR = new Part(Find(scene, "LegBR"));
        _tail = new Part(Find(scene, "Tail"));
        _wingL = new Part(Find(scene, "WingL"));
        _wingR = new Part(Find(scene, "WingR"));
        _legL = new Part(Find(scene, "LegL"));
        _legR = new Part(Find(scene, "LegR"));
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
            GD.PushWarning($"Fungtopia: Pet: model {path} is missing.");
            return null;
        }
        var document = new GltfDocument();
        var state = new GltfState();
        var error = document.AppendFromBuffer(bytes, Models, state);
        if (error != Error.Ok)
        {
            GD.PushWarning($"Fungtopia: Pet: model {path} could not be read ({error}).");
            return null;
        }
        return document.GenerateScene(state) as Node3D;
    }

    private static Node3D? Find(Node root, string name) => root.FindChild(name, true, false) as Node3D;

    private static void ApplyMaterials(Node node)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh != null)
        {
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
                mesh.SetSurfaceOverrideMaterial(i, ToonMaterial(mesh.Mesh.SurfaceGetMaterial(i)));
        }
        foreach (var child in node.GetChildren())
            ApplyMaterials(child);
    }

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

    /// <summary>Advances the animation by <paramref name="delta"/> seconds of frame time.</summary>
    /// <param name="speed">Horizontal speed, meters per second.</param>
    /// <param name="sitting">Sitting by the defeated player.</param>
    /// <param name="sinceBite">Seconds since the last bite: a pounce plays right after one.</param>
    /// <param name="attacking">Going for a monster (the tail stands up, the wag gets eager).</param>
    /// <param name="altitude">Height above the ground, meters (the Crow flies while it is above 0).</param>
    public void Animate(float delta, float speed, bool sitting, float sinceBite, bool attacking, float altitude = 0f)
    {
        _time += delta;
        if (Kind == PetKind.Crow)
        {
            AnimateCrow(delta, speed, sinceBite, attacking, altitude);
            return;
        }
        float trotTarget = sitting ? 0f : Math.Clamp(speed / 4f, 0f, 1f);
        _trotAmount = Mathf.MoveToward(_trotAmount, trotTarget, delta * 6f);
        // Short legs take quick steps: about three strides per meter.
        _trotPhase = (_trotPhase + delta * speed * 3.2f) % Mathf.Tau;
        _sit = Mathf.MoveToward(_sit, sitting ? 1f : 0f, delta * 3f);
        float sit = Mathf.SmoothStep(0f, 1f, _sit);

        // The pounce: a hop forward with the head up and the front paws reaching out.
        float pounce = sinceBite < PounceSeconds ? MathF.Sin(Mathf.Pi * sinceBite / PounceSeconds) : 0f;

        float swing = MathF.Sin(_trotPhase) * 0.7f * _trotAmount;
        float bob = MathF.Abs(MathF.Cos(_trotPhase)) * 0.035f * _trotAmount;
        float breathe = MathF.Sin(_time * 2.6f) * 0.01f * (1f - _trotAmount);

        // Sitting tips the body back around the shoulders until the haunches rest on the ground.
        float tipBack = 0.55f * sit;
        float lift = 0.35f * pounce;
        var basis = Basis.FromEuler(new Vector3(tipBack + lift, 0f, 0f)) * Basis.FromScale(new Vector3(1f, 1f + breathe, 1f));
        var origin = Shoulders - basis * Shoulders + new Vector3(0f, bob + 0.12f * pounce, -0.18f * pounce);
        _rig.Transform = new Transform3D(basis, origin);

        // Trot on diagonal pairs: front-left with back-right, front-right with back-left.
        float frontReach = 1.1f * pounce;
        _legFL.Pose(new Vector3(swing - tipBack + frontReach, 0f, 0f));
        _legFR.Pose(new Vector3(-swing - tipBack + frontReach, 0f, 0f));
        float tuck = 1.05f * sit;
        _legBL.Pose(new Vector3(-swing + tuck - 0.5f * pounce, 0f, -0.15f * sit));
        _legBR.Pose(new Vector3(swing + tuck - 0.5f * pounce, 0f, 0.15f * sit));
        _body.Pose(new Vector3(0f, 0f, MathF.Sin(_trotPhase) * 0.05f * _trotAmount));
        float look = MathF.Sin(_time * 0.7f) * 0.25f * (1f - _trotAmount) * (attacking ? 0f : 1f);
        _head.Pose(new Vector3(-0.8f * tipBack + 0.04f * MathF.Cos(_trotPhase * 2f) * _trotAmount - 0.25f * pounce,
            look, MathF.Sin(_time * 1.3f) * 0.08f * sit));

        // The tail wags fastest while sitting happily or going for a monster.
        float wagSpeed = sitting ? 11f : attacking ? 13f : 7f + 3f * _trotAmount;
        _wagPhase = (_wagPhase + delta * wagSpeed) % Mathf.Tau;
        float wag = MathF.Sin(_wagPhase) * (0.45f + 0.2f * sit);
        float tailUp = (attacking ? 0.25f : 0f) - 0.6f * sit;
        _tail.Pose(new Vector3(tailUp, wag, 0f));
    }

    /// <summary>The Crow: flaps and bobs while it flies, leans into a swoop, pecks, and folds its wings on the ground.</summary>
    private void AnimateCrow(float delta, float speed, float sinceBite, bool attacking, float altitude)
    {
        _flight = Mathf.MoveToward(_flight, altitude > 0.05f ? 1f : 0f, delta * 4f);
        float flight = Mathf.SmoothStep(0f, 1f, _flight);
        // Quick little wing beats, faster while it hurries.
        _flapPhase = (_flapPhase + delta * (14f + 1.5f * speed)) % Mathf.Tau;
        float flap = MathF.Sin(_flapPhase) * 0.85f * flight;
        float bob = MathF.Sin(_flapPhase) * 0.03f * flight + MathF.Sin(_time * 2.2f) * 0.04f * flight;
        float breathe = MathF.Sin(_time * 2.6f) * 0.012f * (1f - flight);

        // The peck: a quick dip of the whole ball and the head toward the monster.
        const float PeckSeconds = 0.3f;
        float peck = sinceBite < PeckSeconds ? MathF.Sin(Mathf.Pi * sinceBite / PeckSeconds) : 0f;
        // Leaning into the flight (more while swooping on a monster), nose down to peck.
        float lean = -Math.Clamp(speed / 8f, 0f, 1f) * (attacking ? 0.45f : 0.25f) * flight - 0.5f * peck;
        var basis = Basis.FromEuler(new Vector3(lean, 0f, MathF.Sin(_time * 1.7f) * 0.06f * flight))
            * Basis.FromScale(new Vector3(1f, 1f + breathe, 1f));
        var center = new Vector3(0f, 0.3f, 0f);
        var origin = center - basis * center + new Vector3(0f, bob, -0.1f * peck);
        _rig.Transform = new Transform3D(basis, origin);

        // Wings beat up and down around the shoulders; folded flat against the body on the ground.
        float folded = 1f - flight;
        _wingL.Pose(new Vector3(0f, 0.2f * folded, -flap - 0.15f * flight));
        _wingR.Pose(new Vector3(0f, -0.2f * folded, flap + 0.15f * flight));
        float look = MathF.Sin(_time * 0.8f) * 0.3f * (attacking ? 0f : 1f);
        _head.Pose(new Vector3(-0.6f * peck + 0.05f * MathF.Sin(_flapPhase) * flight, look, 0f));
        _tail.Pose(new Vector3(0.25f * flight + 0.1f * MathF.Sin(_flapPhase + 1f) * flight, 0f, 0f));
        // Feet dangle back in flight and stand straight once landed.
        _legL.Pose(new Vector3(0.5f * flight, 0f, 0f));
        _legR.Pose(new Vector3(0.5f * flight, 0f, 0f));
    }
}
