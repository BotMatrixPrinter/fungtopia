using System;
using System.Collections.Generic;
using Fungtopia.Wardrobe;
using Godot;

namespace Fungtopia.Player.View;

/// <summary>
/// One player character (girl.glb or boy.glb from modules/Player/Models, the modular rig), dressed
/// by a <see cref="CharacterLook"/> (<see cref="Apply"/>: skin, eye and hair tints, the hair style
/// and each worn item's model on the rig's parts, the built-in outfit where nothing is worn),
/// drawn toon-shaded with ink outlines and animated procedurally: walk bob, the floating hands and the feet swinging,
/// jump stretch and landing squash, a white hit flash, and tipping over when defeated; and the archery pose
/// (<see cref="ArcheryPose"/>): the bow from the back over the right shoulder into the left hand
/// at the hold point on the right, the right hand drawing the string with a nocked arrow to the
/// right cheek, upper body and head toward the aim.
/// Faces -Z at zero rotation.
/// </summary>
public partial class CharacterModel : Node3D
{
    private const string Models = "res://modules/Player/Models/";
    private const string Shaders = "res://modules/Player/Godot/Shaders/";

    /// <summary>Surfaces drawn without an ink outline (small soft details).</summary>
    private static readonly HashSet<string> NoOutline = new() { "EyeShine", "Blush", "Mouth", "String", Mushrooms.PoisonSpots };

    /// <summary>Materials each character tints itself (so two characters can look different).</summary>
    private static readonly HashSet<string> Tinted = new() { "Skin", "Eye", "Hair" };

    private static readonly BuiltinPieces[] s_pieces =
        { BuiltinPieces.Hair, BuiltinPieces.Top, BuiltinPieces.Bottom, BuiltinPieces.Shoes, BuiltinPieces.Head };

    private static readonly Dictionary<string, byte[]> s_bytes = new();
    private static Shader? s_toon;
    private static ShaderMaterial? s_outline;
    private static CylinderMesh? s_stringMesh;

    /// <summary>The held bow is this much bigger than the slung one (characters.py's HAND_BOW_SCALE); it grows as it swings into the hand.</summary>
    private const float HandBowScale = 1.3f;

    private readonly Node3D _rig = new() { Name = "Rig" };
    private readonly List<GeometryInstance3D> _meshes = new();
    private readonly List<(string Name, ShaderMaterial Material, Color Color)> _tints = new();
    private readonly List<Node3D> _worn = new();
    private Part _head, _body, _legL, _legR, _armL, _armR, _handL, _handR, _backBow, _handBow, _limbTop, _limbBottom;
    private Node3D? _scene;
    private Node3D? _arrow;
    private MeshInstance3D? _stringTop, _stringBottom;
    private ShaderMaterial? _stringMaterial;

    /// <summary>How far the body cone's bottom is below its pivot (the hips; characters.py's BODY_BOTTOM is 0.12 m): squash keeps it there.</summary>
    private const float BodyBelowPivot = 0.17f;

    /// <summary>How far the (invisible) arms turn out from their rest while standing and walking, radians.</summary>
    private const float HandsOut = 0.12f;

    /// <summary>How far the slung bow sits out behind a worn backpack (parts' frame, +Z is behind).</summary>
    private static readonly Vector3 BackpackPush = new(0f, 0.02f, 0.13f);

    private Vector3 _bowPush;
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
        CollectMeshes(scene, shared: false, null);
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
        _limbTop = new Part(Find(scene, "HandBowLimbTop"));
        _limbBottom = new Part(Find(scene, "HandBowLimbBottom"));
        _scene = scene;
        BuildHandBow(Find(scene, "NockArrow"));
    }

    /// <summary>
    /// Dresses the character: tints the skin parts, eyes and hair, hides the built-in pieces the
    /// look does not show, and attaches the hair style's and each worn item's model to the rig.
    /// Each of their objects goes on the rig part of its name (<see cref="Rig.IsAttachPoint"/>) with
    /// no offset, so it moves with it in every animation and the archery pose. Replaces what the
    /// last call attached; call it only when the look changes.
    /// </summary>
    public void Apply(in CharacterLook look)
    {
        if (_scene == null)
            return;
        foreach (var node in _worn)
        {
            RemoveMeshes(node);
            node.GetParent()?.RemoveChild(node);
            node.QueueFree();
        }
        _worn.Clear();
        foreach (var (name, material, color) in _tints)
        {
            var tint = !look.Custom ? color : name switch
            {
                "Skin" => ToColor(look.SkinColor),
                "Eye" => ToColor(look.EyeColor),
                _ => ToColor(look.HairColor),
            };
            material.SetShaderParameter("albedo", tint);
        }
        foreach (var piece in s_pieces)
            ShowPiece(_scene, Rig.NodePrefix(piece), look.Shows(piece));
        Wear(look.HairModel, "Hair", look.HairColor);
        foreach (Slot slot in Enum.GetValues<Slot>())
            Wear(look.Model(slot), slot.ToString(), look.HairColor);
        _bowPush = look.Back.Length > 0 ? BackpackPush : Vector3.Zero;
        _shownFlash = -1f; // the new meshes take the flash on the next frame
    }

    private static Color ToColor(System.Numerics.Vector3 c) => new(c.X, c.Y, c.Z);

    private static void ShowPiece(Node node, string prefix, bool visible)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Node3D piece && piece.Name.ToString().StartsWith(prefix, StringComparison.Ordinal))
                piece.Visible = visible;
            ShowPiece(child, prefix, visible);
        }
    }

    /// <summary>Attaches each object of the model at <paramref name="path"/> to the rig part of the same name.</summary>
    private void Wear(string path, string slot, System.Numerics.Vector3 hairColor)
    {
        if (string.IsNullOrEmpty(path) || _scene == null)
            return;
        var model = LoadGlb(path);
        if (model == null)
            return;
        foreach (var child in model.GetChildren())
        {
            if (child is not Node3D piece || !Rig.IsAttachPoint(piece.Name) || Find(_scene, piece.Name) is not Node3D part)
                continue;
            model.RemoveChild(piece);
            piece.Owner = null;
            piece.Name = "Worn" + slot + piece.Name;
            piece.Transform = Transform3D.Identity;
            part.AddChild(piece);
            CollectMeshes(piece, shared: true, ToColor(hairColor));
            _worn.Add(piece);
        }
        model.QueueFree();
    }

    private void RemoveMeshes(Node node)
    {
        if (node is GeometryInstance3D mesh)
            _meshes.Remove(mesh);
        foreach (var child in node.GetChildren())
            RemoveMeshes(child);
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
        if (!s_bytes.TryGetValue(path, out var bytes))
            s_bytes[path] = bytes = FileAccess.GetFileAsBytes(path);
        if (bytes.Length == 0)
        {
            GD.PushWarning($"Fungtopia: Player: model {path} is missing.");
            return null;
        }
        var document = new GltfDocument();
        var state = new GltfState();
        var error = document.AppendFromBuffer(bytes, path.GetBaseDir(), state);
        if (error != Error.Ok)
        {
            GD.PushWarning($"Fungtopia: Player: model {path} could not be read ({error}).");
            return null;
        }
        return document.GenerateScene(state) as Node3D;
    }

    private static Node3D? Find(Node root, string name) => root.FindChild(name, true, false) as Node3D;

    /// <summary>
    /// Gives every mesh under <paramref name="node"/> toon materials. On the rig (not
    /// <paramref name="shared"/>) the skin, eye and hair materials are this character's own, for
    /// <see cref="Apply"/> to tint; on a worn model a hair style's "Hair" takes <paramref name="hair"/>.
    /// </summary>
    private void CollectMeshes(Node node, bool shared, Color? hair)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh != null)
        {
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
            {
                var source = mesh.Mesh.SurfaceGetMaterial(i);
                string name = source?.ResourceName ?? "";
                ShaderMaterial toon;
                if (hair is Color hairColor && name == "Hair")
                {
                    toon = ToonMaterial(source, cache: false);
                    toon.SetShaderParameter("albedo", hairColor);
                }
                else if (!shared && Tinted.Contains(name))
                {
                    toon = ToonMaterial(source, cache: false);
                    _tints.Add((name, toon, (Color)toon.GetShaderParameter("albedo")));
                }
                else
                    toon = ToonMaterial(source, cache: true);
                mesh.SetSurfaceOverrideMaterial(i, toon);
                if (name == "String")
                    _stringMaterial = toon;
            }
            _meshes.Add(mesh);
        }
        foreach (var child in node.GetChildren())
            CollectMeshes(child, shared, hair);
    }

    private static readonly Dictionary<string, ShaderMaterial> s_materials = new();

    /// <summary>The toon material for a glTF material: its color, an ink outline unless it is a small detail; mushroom caps
    /// glossy and rim lit, glow spots glowing (<see cref="Mushrooms"/>).</summary>
    private static ShaderMaterial ToonMaterial(Material? source, bool cache)
    {
        string name = source?.ResourceName ?? "";
        var color = source is BaseMaterial3D standard ? standard.AlbedoColor : Colors.White;
        string key = name + color.ToHtml();
        if (cache && s_materials.TryGetValue(key, out var cached))
            return cached;
        s_toon ??= GD.Load<Shader>(Shaders + "toon.gdshader");
        s_outline ??= new ShaderMaterial { Shader = GD.Load<Shader>(Shaders + "outline.gdshader") };
        var material = new ShaderMaterial { Shader = s_toon };
        // glTF stores linear colors; the shader's albedo is an sRGB color like Blender's swatch.
        material.SetShaderParameter("albedo", color.SrgbToLinear());
        if (name == "EyeShine")
            material.SetShaderParameter("self_light", 0.6f);
        if (Mushrooms.Glows(name))
            material.SetShaderParameter("self_light", Mushrooms.GlowLight);
        if (Mushrooms.IsCap(name))
        {
            material.SetShaderParameter("gloss", Mushrooms.Gloss);
            material.SetShaderParameter("rim_light", Mushrooms.RimLight);
        }
        if (!NoOutline.Contains(name))
            material.NextPass = s_outline;
        if (cache)
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

        // The model has no arms or legs: ArmL/ArmR and LegL/LegR are invisible pivots, so these swing
        // the floating hands (at the arms' ends) and the feet (hanging from the hips) back and forth.
        float swing = MathF.Sin(_walkPhase) * 0.65f * _walkAmount;
        float bob = MathF.Abs(MathF.Sin(_walkPhase)) * 0.05f * _walkAmount;
        float breathe = MathF.Sin(_time * 2.4f) * 0.012f * (1f - _walkAmount);
        float airLegs = grounded ? 0f : 0.35f;

        // Squash and stretch scale the body and head only (keeping their volume about the same), the
        // body's bottom staying put; hands and feet are never scaled, the hands only ride up and down
        // with the shoulders.
        float aim = ArcheryPose.Eased(archery.Weight);
        float s = _squash * _squash + 0.25f * ArcheryPose.FullDrawSquash(archery) * aim;
        float up = 1f - 0.28f * s + 0.12f * _stretch + breathe;
        float wide = 1f + 0.18f * s - 0.05f * _stretch;
        float fall = Mathf.SmoothStep(0f, 1f, _tipOver);
        var lie = Basis.FromEuler(new Vector3(fall * Mathf.Pi * 0.5f, 0f, 0f));
        _rig.Transform = new Transform3D(lie, new Vector3(0f, bob, 0f));

        _legL.Pose(new Vector3(swing + airLegs * 0.6f, 0f, 0f));
        _legR.Pose(new Vector3(-swing - airLegs * 0.4f, 0f, 0f));
        // The hands float a little out and up from the arms' rest, beside the middle of the body.
        float armsUp = grounded ? HandsOut : 0.9f;
        var walkBody = Basis.FromEuler(new Vector3(-0.08f * _walkAmount, MathF.Sin(_walkPhase) * 0.08f * _walkAmount, 0f));
        var walkHead = Basis.FromEuler(new Vector3(0.05f * _walkAmount - 0.06f * s, 0f, MathF.Sin(_walkPhase) * 0.05f * _walkAmount));
        var backRotation = new Vector3(-0.08f * _walkAmount, MathF.Sin(_walkPhase) * 0.08f * _walkAmount, 0f);
        var backOffset = new Vector3(0f, bob * 0.3f, 0f) + _bowPush;
        _backBow.Pose(backRotation, backOffset);
        PoseArchery(archery, aimYaw, aimPitch, walkBody, walkHead, new Vector3(-swing * 0.9f, 0f, -armsUp),
            new Vector3(swing * 0.9f, 0f, armsUp), new Vector3(wide, up, wide), backRotation, backOffset);

        if (MathF.Abs(flash - _shownFlash) > 0.01f)
        {
            _shownFlash = flash;
            foreach (var mesh in _meshes)
                mesh.SetInstanceShaderParameter("flash", flash);
        }
    }

    private static Vector3 G(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);

    private static Quaternion G(System.Numerics.Quaternion q) => new(q.X, q.Y, q.Z, q.W);

    /// <summary>
    /// The body, head, arms, hands and the bow: walking (the bow on the back) blended by the
    /// archery weight into <see cref="ArcheryPose.Solve"/>'s pose, in the model frame (feet,
    /// facing). The body turns and leans about the hips, carrying the neck and the shoulders;
    /// the arms only turn at the shoulders, so the hands stay at their ends: on the bow's grip at
    /// the hold point and on the string in the full pose. Only the body and head are squashed.
    /// </summary>
    private void PoseArchery(in ArcheryState archery, float aimYaw, float aimPitch, Basis walkBody, Basis walkHead,
        Vector3 armLWalk, Vector3 armRWalk, Vector3 squash, Vector3 backRotation, Vector3 backOffset)
    {
        float weight = Math.Clamp(archery.Weight, 0f, 1f);
        float w = ArcheryPose.Eased(weight);
        bool inHand = ArcheryPose.BowInHand(archery) && _handBow.Node != null;
        var fromParts = _rig.Transform * (_scene?.Transform ?? Transform3D.Identity); // parts' parent -> model
        var toParts = fromParts.AffineInverse();
        var frame = ArcheryPose.Solve(archery, aimYaw, aimPitch);
        Quaternion ToPartsRotation(Quaternion model) => (toParts.Basis * new Basis(model) * fromParts.Basis).GetRotationQuaternion();

        // The body about the hips (slid a little toward the bow), the head on its neck.
        var pivot = G(ArcheryPose.BodyPivot);
        var plant = new Vector3(0f, -(1f - squash.Y) * BodyBelowPivot, 0f);
        var slide = toParts.Basis * (G(frame.Hips - ArcheryPose.BodyPivot) * w + plant);
        var body = walkBody.GetRotationQuaternion().Slerp(ToPartsRotation(G(frame.Body)), w);
        if (_body.Node != null)
            _body.Node.Transform = new Transform3D(new Basis(body) * Basis.FromScale(squash) * _body.Rest.Basis, _body.Rest.Origin + slide);
        Vector3 OnBody(System.Numerics.Vector3 rest) => pivot + slide + new Basis(body) * ((G(rest) - pivot) * new Vector3(1f, squash.Y, 1f));
        if (_head.Node != null)
        {
            var head = walkHead.GetRotationQuaternion().Slerp(ToPartsRotation(G(frame.Head)), w);
            _head.Node.Transform = new Transform3D(new Basis(head) * Basis.FromScale(squash) * _head.Rest.Basis, OnBody(ArcheryPose.NeckRest));
        }

        // The bow: from its place on the back (swaying like BackBow) to the hold point, along the arrow.
        var back = new Transform3D(Basis.FromEuler(backRotation) * _handBow.Rest.Basis, _handBow.Rest.Origin + backOffset);
        var backModel = fromParts * back.Origin;
        var gripLocal = ArcheryPose.BowGrip(weight, new System.Numerics.Vector3(backModel.X, backModel.Y, backModel.Z));
        var bowRotation = back.Basis.GetRotationQuaternion().Slerp(ToPartsRotation(G(frame.Bow)), w);
        float bowScale = Mathf.Lerp(1f / HandBowScale, 1f, w);
        if (_handBow.Node != null)
        {
            _handBow.Node.Visible = inHand;
            if (inHand)
                _handBow.Node.Transform = new Transform3D(new Basis(bowRotation).Scaled(Vector3.One * bowScale), toParts * G(gripLocal));
        }
        if (_backBow.Node != null)
            _backBow.Node.Visible = !inHand;
        if (inHand)
        {
            // The limbs bend back about the grip with the draw; the string runs from their tips to the nock.
            var top = new Basis(Vector3.Right, frame.Flex);
            var bottom = new Basis(Vector3.Right, -frame.Flex);
            if (_limbTop.Node != null)
                _limbTop.Node.Transform = new Transform3D(top * _limbTop.Rest.Basis, _limbTop.Rest.Origin);
            if (_limbBottom.Node != null)
                _limbBottom.Node.Transform = new Transform3D(bottom * _limbBottom.Rest.Basis, _limbBottom.Rest.Origin);
            var nock = new Vector3(0f, 0f, ArcheryPose.StringRest + frame.Pull);
            PlaceString(_stringTop, top * new Vector3(0f, ArcheryPose.TipHalfSpan, ArcheryPose.StringRest), nock);
            PlaceString(_stringBottom, bottom * new Vector3(0f, -ArcheryPose.TipHalfSpan, ArcheryPose.StringRest), nock);
            if (_arrow != null)
            {
                _arrow.Visible = ArcheryPose.ArrowVisible(archery);
                _arrow.Transform = new Transform3D(Basis.Identity, nock);
            }
        }

        // The arms turn at the shoulders (which ride on the body) toward the grip and the string.
        PoseArm(_armL, _handL, OnBody(ArcheryPose.ShoulderLRest), Basis.FromEuler(armLWalk).GetRotationQuaternion(),
            ToPartsRotation(G(frame.ArmL)), w);
        PoseArm(_armR, _handR, OnBody(new System.Numerics.Vector3(-ArcheryPose.ShoulderLRest.X, ArcheryPose.ShoulderLRest.Y, ArcheryPose.ShoulderLRest.Z)),
            Basis.FromEuler(armRWalk).GetRotationQuaternion(), ToPartsRotation(G(frame.ArmR)), w);
    }

    /// <summary>
    /// An arm at <paramref name="shoulder"/> (parts' frame), turned from its walking rotation
    /// toward the pose's by <paramref name="w"/>; never scaled: the hand stays at its end.
    /// </summary>
    private static void PoseArm(Part arm, Part hand, Vector3 shoulder, Quaternion walk, Quaternion pose, float w)
    {
        if (arm.Node == null)
            return;
        var rotation = walk.Slerp(pose, w);
        var reach = hand.Node != null ? hand.Rest.Origin - arm.Rest.Origin : new Vector3(0f, -ArcheryPose.ArmLength, 0f);
        arm.Node.Transform = new Transform3D(new Basis(rotation) * arm.Rest.Basis, shoulder);
        if (hand.Node != null)
            hand.Node.Transform = new Transform3D(hand.Rest.Basis, shoulder + new Basis(rotation) * reach);
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
