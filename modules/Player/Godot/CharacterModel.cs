using System;
using System.Collections.Generic;
using Godot;

namespace Fungtopia.Player.View;

/// <summary>
/// One player character (girl.glb or boy.glb from modules/Player/Models), drawn toon-shaded with
/// ink outlines and animated procedurally: walk bob, leg and arm swing, jump stretch and landing
/// squash, a white hit flash, and tipping over when defeated; and the archery pose
/// (<see cref="ArcheryPose"/>): the bow from the back into the left hand at the hold point, the
/// right hand drawing the string with a nocked arrow, upper body and head toward the aim.
/// Faces -Z at zero rotation.
/// </summary>
public partial class CharacterModel : Node3D
{
    private const string Models = "res://modules/Player/Models/";
    private const string Shaders = "res://modules/Player/Godot/Shaders/";

    /// <summary>Surfaces drawn without an ink outline (small soft details).</summary>
    private static readonly HashSet<string> NoOutline = new() { "EyeShine", "Blush", "Mouth", "String", "PoisonSpots" };

    private static Shader? s_toon;
    private static ShaderMaterial? s_outline;
    private static CylinderMesh? s_stringMesh;

    /// <summary>The held bow leans a little (radians about its forward axis), like a cute archer's.</summary>
    private const float BowCant = 0.22f;

    private readonly Node3D _rig = new() { Name = "Rig" };
    private readonly List<GeometryInstance3D> _meshes = new();
    private Part _head, _body, _legL, _legR, _armL, _armR, _handL, _handR, _backBow, _handBow;
    private Node3D? _scene;
    private Node3D? _arrow;
    private MeshInstance3D? _stringTop, _stringBottom;
    private ShaderMaterial? _stringMaterial;

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
        _handL = new Part(Find(scene, "HandL"));
        _handR = new Part(Find(scene, "HandR"));
        _backBow = new Part(Find(scene, "BackBow"));
        _handBow = new Part(Find(scene, "HandBow"));
        _scene = scene;
        BuildHandBow(Find(scene, "NockArrow"));
    }

    /// <summary>
    /// The held bow: HandBow with a string drawn in two straight halves to the nock, and the
    /// nocked arrow on it (both children of HandBow, in its grip frame). Hidden until drawn.
    /// </summary>
    private void BuildHandBow(Node3D? arrow)
    {
        if (_handBow.Node == null)
            return;
        _handBow.Node.Visible = false;
        s_stringMesh ??= new CylinderMesh { TopRadius = 0.009f, BottomRadius = 0.009f, Height = 1f, RadialSegments = 6, Rings = 1 };
        _stringTop = new MeshInstance3D { Name = "StringTop", Mesh = s_stringMesh, MaterialOverride = _stringMaterial };
        _stringBottom = new MeshInstance3D { Name = "StringBottom", Mesh = s_stringMesh, MaterialOverride = _stringMaterial };
        _handBow.Node.AddChild(_stringTop);
        _handBow.Node.AddChild(_stringBottom);
        _meshes.Add(_stringTop);
        _meshes.Add(_stringBottom);
        if (arrow != null)
        {
            arrow.Owner = null;
            arrow.GetParent()?.RemoveChild(arrow);
            _handBow.Node.AddChild(arrow);
            arrow.Visible = false;
            _arrow = arrow;
        }
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
            {
                var source = mesh.Mesh.SurfaceGetMaterial(i);
                var toon = ToonMaterial(source);
                mesh.SetSurfaceOverrideMaterial(i, toon);
                if (source?.ResourceName == "String")
                    _stringMaterial = toon;
            }
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
        if (name == "EyeShine" || name == "PoisonSpots")
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
    public void Animate(float delta, float speed, bool grounded, float verticalSpeed, float flash, bool alive) =>
        Animate(delta, speed, grounded, verticalSpeed, flash, alive, ArcheryState.Relaxed, 0f, 0f);

    /// <inheritdoc cref="Animate(float, float, bool, float, float, bool)"/>
    /// <param name="archery">The archery pose (<see cref="PlayerModule.Archery"/>).</param>
    /// <param name="aimYaw">Aim yaw from the facing, from <see cref="ArcheryPose.AimAngles"/>.</param>
    /// <param name="aimPitch">Aim pitch, from <see cref="ArcheryPose.AimAngles"/>.</param>
    public void Animate(float delta, float speed, bool grounded, float verticalSpeed, float flash, bool alive,
        in ArcheryState archery, float aimYaw, float aimPitch)
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
        float aim = ArcheryPose.Eased(archery.Weight);
        float s = _squash * _squash + 0.25f * ArcheryPose.FullDrawSquash(archery) * aim;
        float up = 1f - 0.28f * s + 0.12f * _stretch + breathe;
        float wide = 1f + 0.18f * s - 0.05f * _stretch;
        float fall = Mathf.SmoothStep(0f, 1f, _tipOver);
        var lie = Basis.FromEuler(new Vector3(fall * Mathf.Pi * 0.5f, 0f, 0f));
        _rig.Transform = new Transform3D(lie * Basis.FromScale(new Vector3(wide, up, wide)), new Vector3(0f, bob, 0f));

        _legL.Pose(new Vector3(swing + airLegs * 0.6f, 0f, 0f));
        _legR.Pose(new Vector3(-swing - airLegs * 0.4f, 0f, 0f));
        float armsUp = grounded ? 0f : 0.9f;
        // Drawing: the upper body turns side-on (left shoulder toward the aim) and both turn and
        // pitch with the aim; the legs keep walking.
        _body.Pose(new Vector3(-0.08f * _walkAmount + aim * aimPitch * 0.3f,
            MathF.Sin(_walkPhase) * 0.08f * _walkAmount * (1f - aim) + aim * (aimYaw * 0.5f - 0.25f), 0f));
        // The head also tilts away to the left, making room for the string hand at the cheek.
        _head.Pose(new Vector3(0.05f * _walkAmount - 0.06f * s + aim * aimPitch * 0.7f, aim * aimYaw,
            MathF.Sin(_walkPhase) * 0.05f * _walkAmount * (1f - aim) + aim * 0.45f));
        var backRotation = new Vector3(-0.08f * _walkAmount, MathF.Sin(_walkPhase) * 0.08f * _walkAmount, 0f);
        var backOffset = new Vector3(0f, bob * 0.3f, 0f);
        _backBow.Pose(backRotation, backOffset);
        PoseArchery(archery, aimYaw, aimPitch, new Vector3(-swing * 0.9f, 0f, -armsUp), new Vector3(swing * 0.9f, 0f, armsUp),
            backRotation, backOffset);

        if (MathF.Abs(flash - _shownFlash) > 0.01f)
        {
            _shownFlash = flash;
            foreach (var mesh in _meshes)
                mesh.SetInstanceShaderParameter("flash", flash);
        }
    }

    /// <summary>
    /// The arms, hands and the bow: walking arms and the bow on the back blended by the archery
    /// weight into the archery pose, computed in the model frame (feet, facing) so the bow's grip
    /// lands exactly on <see cref="ArcheryPose.HoldLocal"/> whatever the rig's squash.
    /// </summary>
    private void PoseArchery(in ArcheryState archery, float aimYaw, float aimPitch, Vector3 armLWalk, Vector3 armRWalk,
        Vector3 backRotation, Vector3 backOffset)
    {
        float weight = Math.Clamp(archery.Weight, 0f, 1f);
        float w = ArcheryPose.Eased(weight);
        bool inHand = ArcheryPose.BowInHand(archery) && _handBow.Node != null;
        var fromParts = _rig.Transform * (_scene?.Transform ?? Transform3D.Identity); // parts' parent -> model
        var toParts = fromParts.AffineInverse();

        // The bow: from its place on the back (swaying like BackBow) to the hold point, aimed.
        var back = new Transform3D(Basis.FromEuler(backRotation) * _handBow.Rest.Basis, _handBow.Rest.Origin + backOffset);
        var forwardLocal = ArcheryPose.BowForward(ArcheryPose.AimLocal(aimYaw, aimPitch));
        var z = -new Vector3(forwardLocal.X, forwardLocal.Y, forwardLocal.Z);
        var x = Vector3.Up.Cross(z).Normalized();
        var holdBasis = new Basis(x, z.Cross(x), z) * new Basis(Vector3.Back, BowCant);
        var backModel = fromParts * back.Origin;
        var gripLocal = ArcheryPose.BowGrip(weight, new System.Numerics.Vector3(backModel.X, backModel.Y, backModel.Z));
        var grip = new Vector3(gripLocal.X, gripLocal.Y, gripLocal.Z);
        var backRotationParts = back.Basis.GetRotationQuaternion();
        var holdRotationParts = (toParts.Basis * holdBasis).Orthonormalized().GetRotationQuaternion();
        var bowRotation = backRotationParts.Slerp(holdRotationParts, w);
        float pull = ArcheryPose.StringPull(archery);
        if (_handBow.Node != null)
        {
            _handBow.Node.Visible = inHand;
            if (inHand)
                _handBow.Node.Transform = new Transform3D(new Basis(bowRotation), toParts * grip);
        }
        if (_backBow.Node != null)
            _backBow.Node.Visible = !inHand;
        if (inHand)
        {
            var nock = new Vector3(0f, 0f, ArcheryPose.StringRest + pull);
            PlaceString(_stringTop, new Vector3(0f, ArcheryPose.TipHalfSpan, ArcheryPose.StringRest), nock);
            PlaceString(_stringBottom, new Vector3(0f, -ArcheryPose.TipHalfSpan, ArcheryPose.StringRest), nock);
            if (_arrow != null)
            {
                _arrow.Visible = ArcheryPose.ArrowVisible(archery);
                _arrow.Transform = new Transform3D(Basis.Identity, nock);
            }
        }

        // The left hand holds the grip; the right hand is on the string (or following through).
        var rightLocal = ArcheryPose.RightHand(archery, gripLocal, forwardLocal);
        PoseArm(_armL, _handL, armLWalk, toParts * grip, w);
        PoseArm(_armR, _handR, armRWalk, toParts * new Vector3(rightLocal.X, rightLocal.Y, rightLocal.Z), w);
    }

    /// <summary>
    /// An arm from its walking rotation toward reaching <paramref name="target"/> (parts' parent
    /// frame) with its hand: turned toward it and stretched to reach it (a rubbery chibi arm),
    /// by <paramref name="w"/>. The hand stays round at the arm's end.
    /// </summary>
    private static void PoseArm(Part arm, Part hand, Vector3 walkRotation, Vector3 target, float w)
    {
        if (arm.Node == null)
            return;
        var reach = hand.Node != null ? hand.Rest.Origin - arm.Rest.Origin : new Vector3(0f, -0.22f, 0f);
        var rotation = Basis.FromEuler(walkRotation).GetRotationQuaternion();
        float stretch = 1f;
        var toTarget = target - arm.Rest.Origin;
        if (w > 0f && toTarget.LengthSquared() > 1e-6f)
        {
            var reachRotation = new Quaternion(reach.Normalized(), toTarget.Normalized());
            rotation = rotation.Slerp(reachRotation, w);
            stretch = Mathf.Lerp(1f, toTarget.Length() / reach.Length(), w);
        }
        // Scale by `stretch` along the arm's rest axis only.
        var a = reach.Normalized();
        float k = stretch - 1f;
        var along = new Basis(new Vector3(1f, 0f, 0f) + a * (k * a.X), new Vector3(0f, 1f, 0f) + a * (k * a.Y),
            new Vector3(0f, 0f, 1f) + a * (k * a.Z));
        var basis = new Basis(rotation);
        arm.Node.Transform = new Transform3D(basis * along * arm.Rest.Basis, arm.Rest.Origin);
        if (hand.Node != null)
            hand.Node.Transform = new Transform3D(hand.Rest.Basis, arm.Rest.Origin + basis * (reach * stretch));
    }

    /// <summary>A straight piece of the string from <paramref name="from"/> to <paramref name="to"/> in the bow's frame.</summary>
    private static void PlaceString(MeshInstance3D? piece, Vector3 from, Vector3 to)
    {
        if (piece == null)
            return;
        var along = to - from;
        var side = Vector3.Right.Cross(along).Normalized();
        piece.Transform = new Transform3D(new Basis(Vector3.Right, along, side), (from + to) * 0.5f);
    }
}
