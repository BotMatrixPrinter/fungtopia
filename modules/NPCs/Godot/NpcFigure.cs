using System;
using System.Collections.Generic;
using Godot;

namespace Fungtopia.NPCs.View;

/// <summary>
/// One NPC model (jeremy.glb, millie.glb, lucy.glb, abby.glb or pip.glb from modules/NPCs/Models), loaded
/// at run time from the file's bytes (so a fresh checkout runs without an editor import), drawn
/// toon-shaded with ink outlines and animated procedurally: breathing, a gentle sway and head
/// tilt, swinging hands, and a happy bounce with nodding while talking. Pip stirs the soup pot with
/// the wooden spoon instead, and waves with the other hand when the player comes near (and while
/// talking). Like the player characters they have no arms or legs to draw: <c>ArmL</c>/<c>ArmR</c>
/// and <c>LegL</c>/<c>LegR</c> are empty pivots; turning an arm moves its floating hand (see
/// <see cref="FollowArm"/>), and the feet hang from the legs. Faces -Z at zero rotation.
/// </summary>
public partial class NpcFigure : Node3D
{
    private const string Models = "res://modules/NPCs/Models/";
    private const string Shaders = "res://modules/NPCs/Godot/Shaders/";

    /// <summary>The models, by NPC index (<see cref="NPCsModule.Jeremy"/>, Millie, Lucy, Abby, Pip).</summary>
    private static readonly string[] Files = { "jeremy.glb", "millie.glb", "lucy.glb", "abby.glb", "pip.glb" };

    /// <summary>Jeremy is a grown-up, a little taller than the kids.</summary>
    private static readonly float[] Scales = { 1.12f, 1.0f, 1.04f, 1.02f, 1.0f };

    /// <summary>How lively each one is: Millie bounces, Abby and Pip nearly as much, Lucy barely moves.</summary>
    private static readonly float[] Liveliness = { 0.8f, 1.3f, 0.45f, 1.15f, 1.1f };

    /// <summary>The top of each one's head (meters above the feet, before scaling): Pip's chef's hat on the step stool is taller.</summary>
    private static readonly float[] Heights = { 1.6f, 1.6f, 1.6f, 1.6f, 2.07f };

    /// <summary>How long Pip waves when the player comes near (seconds).</summary>
    private const float WaveSeconds = 2.5f;

    /// <summary>Surfaces drawn without an ink outline (small soft details).</summary>
    private static readonly HashSet<string> NoOutline = new()
    {
        "EyeShine", "Blush", "Mouth", "Print", "Heart", "Button", "BadgeStripe", "Lid", "Spots",
        "Freckle", "Cup", "MuffinTop", "Berry", "Cherry", "HatSpots", "Gills",
    };

    private static readonly Dictionary<string, ShaderMaterial> s_materials = new();
    private static Shader? s_toon;
    private static ShaderMaterial? s_outline;

    private readonly Dictionary<string, (Node3D Node, Transform3D Home)> _parts = new();
    private int _npc;
    private float _time;
    private float _talk;
    private float _wave;
    private float _waveLeft;
    private float _stir;
    private bool _noticing;

    /// <summary>Head height above the feet (meters), for the prompt.</summary>
    public float Height => Heights[_npc] * Scales[_npc];

    /// <summary>True while this NPC is in a conversation (bounces and nods).</summary>
    public bool Talking { get; set; }

    /// <summary>True while the player is near (the Core's <see cref="NPCsModule.Noticing"/>); Pip waves when it turns true.</summary>
    public bool Noticing
    {
        get => _noticing;
        set
        {
            if (value && !_noticing)
                _waveLeft = WaveSeconds;
            _noticing = value;
        }
    }

    public static NpcFigure Create(int npc)
    {
        var figure = new NpcFigure { Name = NPCsModule.Name(npc).Replace(".", "").Replace(" ", "") };
        figure._npc = npc;
        // Each one breathes at its own beat.
        figure._time = npc * 1.7f;
        var model = Load(Models + Files[npc]);
        model.Scale = Vector3.One * Scales[npc];
        figure.AddChild(model);
        foreach (var name in new[] { "Body", "Head", "ArmL", "ArmR", "HandL", "HandR", "LegL", "LegR" })
            if (model.FindChild(name, true, false) is Node3D part)
                figure._parts[name] = (part, part.Transform);
        return figure;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        _talk = Mathf.MoveToward(_talk, Talking ? 1f : 0f, dt * 4f);
        float live = Liveliness[_npc];
        float breath = MathF.Sin(_time * 2.2f);
        float sway = MathF.Sin(_time * 1.1f) * 0.05f * live;
        float nod = MathF.Sin(_time * 9f) * 0.08f * _talk;
        float hop = MathF.Max(0f, MathF.Sin(_time * 7f)) * 0.04f * _talk * live;

        Pose("Body", new Vector3(0f, hop, 0f), new Vector3(0f, 0f, sway * 0.5f),
            new Vector3(1f + breath * 0.012f, 1f - breath * 0.018f + 0.02f, 1f + breath * 0.012f));
        Pose("Head", new Vector3(0f, hop + breath * 0.006f, 0f),
            new Vector3(-nod + MathF.Sin(_time * 0.7f) * 0.04f * live, MathF.Sin(_time * 0.45f) * 0.12f * live,
                sway + MathF.Sin(_time * 0.9f) * 0.06f * live), Vector3.One);
        float arm = MathF.Sin(_time * 2.2f + 0.6f) * 0.07f * live;
        if (_npc == NPCsModule.Pip)
            PoseCook(dt, hop, arm);
        else
        {
            float wave = _talk * (0.25f + MathF.Sin(_time * 6f) * 0.15f) * live;
            Pose("ArmL", new Vector3(0f, hop, 0f), new Vector3(arm, 0f, -arm * 0.5f), Vector3.One);
            Pose("ArmR", new Vector3(0f, hop, 0f), new Vector3(-arm - wave, 0f, arm * 0.5f + wave), Vector3.One);
        }
        // The floating hands stay at the ends of the invisible arms.
        FollowArm("ArmL", "HandL");
        FollowArm("ArmR", "HandR");
    }

    /// <summary>
    /// Pip's arms: the right one reaches up over the rim of the soup pot on the counter (in front,
    /// to Pip's right) and stirs it in little circles with the spoon tipped forward into it; the
    /// left one waves high for a moment when the player comes near and while talking (stirring
    /// slows meanwhile). It tips the spoon hand itself (<see cref="FollowArm"/> only moves it).
    /// </summary>
    private void PoseCook(float dt, float hop, float arm)
    {
        _waveLeft = MathF.Max(0f, _waveLeft - dt);
        _wave = Mathf.MoveToward(_wave, _waveLeft > 0f || Talking ? 1f : 0f, dt * 4f);
        float stir = 1f - _wave * 0.5f;
        _stir += dt * 3.4f * stir;
        float turn = _stir;
        // Rotation about X lifts the arm forward (past level, up over the rim), circling.
        Pose("ArmR", new Vector3(0f, hop, 0f),
            new Vector3(stir * (2.0f + MathF.Sin(turn) * 0.12f), 0f, stir * (MathF.Cos(turn) * 0.12f)), Vector3.One);
        // The spoon hangs down from the hand: tip it forward into the pot.
        if (_parts.TryGetValue("HandR", out var hand))
            hand.Node.Basis = hand.Home.Basis * Basis.FromEuler(new Vector3(0.8f * stir, 0f, 0f));
        // The wave: out to the side and a little forward, beside the face, flapping.
        float flap = MathF.Sin(_time * 10f) * 0.4f;
        Pose("ArmL", new Vector3(0f, hop, 0f),
            new Vector3(arm * (1f - _wave) + _wave * 0.9f, 0f, -arm * 0.5f * (1f - _wave) - _wave * (1.9f + flap)), Vector3.One);
    }

    private void Pose(string name, Vector3 offset, Vector3 euler, Vector3 scale)
    {
        if (!_parts.TryGetValue(name, out var part))
            return;
        var basis = part.Home.Basis * Basis.FromEuler(euler).Scaled(scale);
        part.Node.Transform = new Transform3D(basis, part.Home.Origin + offset);
    }

    private void FollowArm(string armName, string handName)
    {
        if (!_parts.TryGetValue(armName, out var arm) || !_parts.TryGetValue(handName, out var hand))
            return;
        // The hand's rest position relative to the shoulder, turned with the arm.
        var local = arm.Home.AffineInverse() * hand.Home.Origin;
        hand.Node.Position = arm.Node.Transform * local;
    }

    private static Node3D Load(string path)
    {
        var bytes = FileAccess.GetFileAsBytes(path);
        if (bytes.Length == 0)
        {
            GD.PushWarning($"Fungtopia: NPCs: model {path} is missing.");
            return new Node3D();
        }
        var document = new GltfDocument();
        var state = new GltfState();
        if (document.AppendFromBuffer(bytes, path.GetBaseDir(), state) != Error.Ok
            || document.GenerateScene(state) is not Node3D scene)
        {
            GD.PushWarning($"Fungtopia: NPCs: model {path} could not be read.");
            return new Node3D();
        }
        Toonify(scene);
        return scene;
    }

    private static void Toonify(Node node)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh != null)
        {
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
                mesh.SetSurfaceOverrideMaterial(i, Material(mesh.Mesh.SurfaceGetMaterial(i)));
        }
        foreach (var child in node.GetChildren())
            Toonify(child);
    }

    private static ShaderMaterial Material(Material? source)
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
        // Mushroom caps get a glossy highlight and a rim light along their edge.
        material.SetShaderParameter("gloss", MushroomLook.Gloss(name));
        material.SetShaderParameter("rim", MushroomLook.Rim(name));
        if (name is "EyeShine" or "Metal")
            material.SetShaderParameter("self_light", 0.5f);
        if (!NoOutline.Contains(name))
            material.NextPass = s_outline;
        s_materials[key] = material;
        return material;
    }
}
