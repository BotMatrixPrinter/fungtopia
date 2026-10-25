using System;
using System.Collections.Generic;
using Godot;

namespace Fungtopia.Pet.View;

/// <summary>
/// One pet (cat.glb, dog.glb or crow.glb from modules/Pet/Models), drawn toon-shaded with ink
/// outlines and animated procedurally: for the Cat and the Dog a trot on diagonal leg pairs, a
/// fast low gallop while dashing to an aimed monster (ears back, tail streaming), the leap onto it
/// (crouch, spring with the front paws reaching, a squash on landing), a hop on each bite, sitting
/// down, and a wagging tail; for the Crow flapping wings, a hovering bob, a forward lean while it
/// swoops (hard into a dash), a peck on each hit, and folded wings once it has landed.
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
    private Part _head, _body, _legFL, _legFR, _legBL, _legBR, _tail, _earL, _earR;
    private Part _wingL, _wingR, _legL, _legR;

    private float _trotPhase;
    private float _trotAmount;
    private float _sit;
    private float _time;
    private float _wagPhase;
    private float _flapPhase;
    private float _flight;
    private float _dash;
    private float _gallopPhase;
    private bool _wasPouncing;
    private float _sinceLanding = 100f;

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
        _earL = new Part(Find(scene, "EarL"));
        _earR = new Part(Find(scene, "EarR"));
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
        // Mushroom caps (and their spots) are glossy, with a light rim light.
        if (name is "Cap" or "Spots")
        {
            material.SetShaderParameter("gloss", name == "Cap" ? 0.55f : 0.3f);
            material.SetShaderParameter("rim_light", name == "Cap" ? 0.35f : 0.2f);
        }
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
    /// <param name="dashing">Dashing to an aimed monster: a low gallop (the Crow leans hard into its swoop).</param>
    /// <param name="pouncing">In the leap onto an aimed monster (the Cat and the Dog).</param>
    /// <param name="pounceProgress">How far through the leap, 0 to 1.</param>
    public void Animate(float delta, float speed, bool sitting, float sinceBite, bool attacking, float altitude = 0f,
        bool dashing = false, bool pouncing = false, float pounceProgress = 0f)
    {
        _time += delta;
        _dash = Mathf.MoveToward(_dash, dashing ? 1f : 0f, delta * 7f);
        if (Kind == PetKind.Crow)
        {
            AnimateCrow(delta, speed, sinceBite, attacking, altitude);
            return;
        }
        if (_wasPouncing && !pouncing)
            _sinceLanding = 0f;
        _wasPouncing = pouncing;
        _sinceLanding += delta;
        if (pouncing)
        {
            AnimateLeap(Math.Clamp(pounceProgress, 0f, 1f));
            return;
        }
        float trotTarget = sitting ? 0f : Math.Clamp(speed / 4f, 0f, 1f);
        _trotAmount = Mathf.MoveToward(_trotAmount, trotTarget, delta * 6f);
        // Short legs take quick steps: about three strides per meter.
        _trotPhase = (_trotPhase + delta * speed * 3.2f) % Mathf.Tau;
        _sit = Mathf.MoveToward(_sit, sitting ? 1f : 0f, delta * 3f);
        float sit = Mathf.SmoothStep(0f, 1f, _sit);

        // The pounce: a hop forward with the head up and the front paws reaching out (not right
        // after the leap, whose landing is its own squash).
        float pounce = sinceBite < PounceSeconds && _sinceLanding > PounceSeconds
            ? MathF.Sin(Mathf.Pi * sinceBite / PounceSeconds) : 0f;
        // Landing from the leap: a quick squash and back.
        const float SquashSeconds = 0.22f;
        float squash = _sinceLanding < SquashSeconds ? MathF.Sin(Mathf.Pi * _sinceLanding / SquashSeconds) : 0f;

        float dash = Mathf.SmoothStep(0f, 1f, _dash);
        // The gallop: big strides, front pair then hind pair, about three a second at full speed.
        _gallopPhase = (_gallopPhase + delta * MathF.Max(speed, 6f) * 1.35f * (dash > 0f ? 1f : 0f)) % Mathf.Tau;
        float gallopFront = MathF.Sin(_gallopPhase) * 1.05f;
        float gallopHind = MathF.Sin(_gallopPhase + 2.4f) * 1.0f;

        float swing = MathF.Sin(_trotPhase) * 0.7f * _trotAmount * (1f - dash);
        float bob = MathF.Abs(MathF.Cos(_trotPhase)) * 0.035f * _trotAmount * (1f - dash)
            + MathF.Abs(MathF.Sin(_gallopPhase + 1.2f)) * 0.045f * dash;
        float breathe = MathF.Sin(_time * 2.6f) * 0.01f * (1f - _trotAmount);

        // Sitting tips the body back around the shoulders until the haunches rest on the ground;
        // the gallop leans it forward, low and stretched long, rocking with each stride.
        float tipBack = 0.55f * sit;
        float lift = 0.35f * pounce;
        float lean = (-0.2f + 0.1f * MathF.Sin(_gallopPhase + 0.6f)) * dash;
        var stretch = new Vector3(1f + 0.08f * squash, 1f + breathe - 0.1f * dash - 0.2f * squash, 1f + 0.18f * dash + 0.1f * squash);
        var basis = Basis.FromEuler(new Vector3(tipBack + lift + lean, 0f, 0f)) * Basis.FromScale(stretch);
        var origin = Shoulders - basis * Shoulders
            + new Vector3(0f, bob + 0.12f * pounce - 0.07f * dash - 0.04f * squash, -0.18f * pounce);
        _rig.Transform = new Transform3D(basis, origin);

        // Trot on diagonal pairs: front-left with back-right, front-right with back-left; the
        // gallop moves the front legs together, then the hind ones (each side a little apart).
        float frontReach = 1.1f * pounce + 0.35f * squash;
        _legFL.Pose(new Vector3(swing - tipBack + frontReach + dash * (gallopFront + 0.15f), 0f, 0f));
        _legFR.Pose(new Vector3(-swing - tipBack + frontReach + dash * (MathF.Sin(_gallopPhase - 0.35f) * 1.05f + 0.15f), 0f, 0f));
        float tuck = 1.05f * sit;
        _legBL.Pose(new Vector3(-swing + tuck - 0.5f * pounce + dash * (gallopHind - 0.2f), 0f, -0.15f * sit));
        _legBR.Pose(new Vector3(swing + tuck - 0.5f * pounce + dash * (MathF.Sin(_gallopPhase + 2.05f) - 0.2f), 0f, 0.15f * sit));
        _body.Pose(new Vector3(0f, 0f, MathF.Sin(_trotPhase) * 0.05f * _trotAmount * (1f - dash)));
        float look = MathF.Sin(_time * 0.7f) * 0.25f * (1f - _trotAmount) * (attacking ? 0f : 1f);
        // Running flat out the head stays level, reaching forward.
        _head.Pose(new Vector3(-0.8f * tipBack + 0.04f * MathF.Cos(_trotPhase * 2f) * _trotAmount - 0.25f * pounce
            + (0.12f - 0.05f * MathF.Sin(_gallopPhase + 0.6f)) * dash - 0.15f * squash,
            look, MathF.Sin(_time * 1.3f) * 0.08f * sit));
        PoseEars(1.0f * dash + 0.3f * squash);

        // The tail wags fastest while sitting happily or going for a monster; it streams out
        // behind in the gallop, fluttering.
        float wagSpeed = sitting ? 11f : attacking ? 13f : 7f + 3f * _trotAmount;
        _wagPhase = (_wagPhase + delta * (wagSpeed + 10f * dash)) % Mathf.Tau;
        float wag = MathF.Sin(_wagPhase) * (0.45f + 0.2f * sit) * (1f - 0.75f * dash);
        float tailUp = (attacking ? 0.25f : 0f) - 0.6f * sit + 0.95f * dash;
        _tail.Pose(new Vector3(tailUp + 0.08f * MathF.Sin(_wagPhase * 1.3f) * dash, wag, 0f));
    }

    /// <summary>Lays the ears back (0 up, 1 flat back).</summary>
    private void PoseEars(float back)
    {
        _earL.Pose(new Vector3(0.85f * back, 0f, -0.15f * back));
        _earR.Pose(new Vector3(0.85f * back, 0f, 0.15f * back));
    }

    /// <summary>
    /// The leap onto an aimed monster (the Core moves it along the arc): a quick crouch, then the
    /// spring with the body stretched long and the front paws reaching forward, nose up on the
    /// way up and down toward the landing.
    /// </summary>
    private void AnimateLeap(float p)
    {
        // The crouch: haunches down, gathered, in the first sixth of the leap.
        float crouch = p < 0.18f ? MathF.Sin(Mathf.Pi * Math.Clamp(p / 0.18f, 0f, 1f) * 0.5f + Mathf.Pi * 0.5f) : 0f;
        // The spring: stretched out from the crouch to just before landing.
        float spring = Mathf.SmoothStep(0.08f, 0.3f, p) * (1f - Mathf.SmoothStep(0.8f, 1f, p));
        float pitch = 0.3f * crouch + (0.3f - 0.6f * p) * spring;
        var basis = Basis.FromEuler(new Vector3(pitch, 0f, 0f))
            * Basis.FromScale(new Vector3(1f + 0.06f * crouch - 0.04f * spring, 1f - 0.18f * crouch - 0.1f * spring,
                1f - 0.05f * crouch + 0.28f * spring));
        var origin = Shoulders - basis * Shoulders + new Vector3(0f, -0.06f * crouch, -0.08f * spring);
        _rig.Transform = new Transform3D(basis, origin);
        // Front paws reach far forward; hind legs kick out behind; in the crouch all fold under.
        _legFL.Pose(new Vector3(-0.5f * crouch + 1.35f * spring, 0f, 0f));
        _legFR.Pose(new Vector3(-0.5f * crouch + 1.25f * spring, 0f, 0f));
        _legBL.Pose(new Vector3(0.7f * crouch - 1.0f * spring, 0f, 0f));
        _legBR.Pose(new Vector3(0.7f * crouch - 1.05f * spring, 0f, 0f));
        _body.Pose(Vector3.Zero);
        // Eyes on the monster: the head counters the body's pitch.
        _head.Pose(new Vector3(-0.6f * pitch - 0.1f * crouch, 0f, 0f));
        PoseEars(0.4f * crouch + 0.9f * spring);
        _tail.Pose(new Vector3(-0.2f * crouch + 1.0f * spring, 0f, 0f));
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
        float dash = Mathf.SmoothStep(0f, 1f, _dash);
        float lean = -Math.Clamp(speed / 8f, 0f, 1f) * (attacking ? 0.45f : 0.25f) * flight * (1f - dash)
            - 0.95f * dash - 0.5f * peck;
        var basis = Basis.FromEuler(new Vector3(lean, 0f, MathF.Sin(_time * 1.7f) * 0.06f * flight))
            * Basis.FromScale(new Vector3(1f, 1f + breathe, 1f));
        var center = new Vector3(0f, 0.3f, 0f);
        var origin = center - basis * center + new Vector3(0f, bob, -0.1f * peck);
        _rig.Transform = new Transform3D(basis, origin);

        // Wings beat up and down around the shoulders; folded flat against the body on the ground.
        // Swooping on an aimed monster: wings swept back, beating short and fast.
        float folded = 1f - flight;
        flap *= 1f - 0.6f * dash;
        _wingL.Pose(new Vector3(0f, 0.2f * folded - 0.7f * dash, -flap - 0.15f * flight + 0.35f * dash));
        _wingR.Pose(new Vector3(0f, -0.2f * folded + 0.7f * dash, flap + 0.15f * flight - 0.35f * dash));
        float look = MathF.Sin(_time * 0.8f) * 0.3f * (attacking ? 0f : 1f);
        _head.Pose(new Vector3(-0.6f * peck + 0.05f * MathF.Sin(_flapPhase) * flight, look, 0f));
        _tail.Pose(new Vector3(0.25f * flight + 0.1f * MathF.Sin(_flapPhase + 1f) * flight + 0.4f * dash, 0f, 0f));
        // Feet dangle back in flight and stand straight once landed.
        _legL.Pose(new Vector3(0.5f * flight, 0f, 0f));
        _legR.Pose(new Vector3(0.5f * flight, 0f, 0f));
    }
}
