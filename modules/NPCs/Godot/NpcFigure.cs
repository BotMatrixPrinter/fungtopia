using System;
using System.Collections.Generic;
using Godot;

namespace Fungtopia.NPCs.View;

/// <summary>
/// One NPC model (jeremy.glb, millie.glb or lucy.glb from modules/NPCs/Models), loaded at run
/// time from the file's bytes (so a fresh checkout runs without an editor import), drawn
/// toon-shaded with ink outlines and animated procedurally: breathing, a gentle sway and head
/// tilt, swinging arms, and a happy bounce with nodding while talking. Faces -Z at zero rotation.
/// </summary>
public partial class NpcFigure : Node3D
{
    private const string Models = "res://modules/NPCs/Models/";
    private const string Shaders = "res://modules/NPCs/Godot/Shaders/";

    /// <summary>The models, by NPC index (<see cref="NPCsModule.Jeremy"/>, Millie, Lucy).</summary>
    private static readonly string[] Files = { "jeremy.glb", "millie.glb", "lucy.glb" };

    /// <summary>Jeremy is a grown-up, a little taller than the kids.</summary>
    private static readonly float[] Scales = { 1.12f, 1.0f, 1.04f };

    /// <summary>How lively each one is: Millie bounces, Lucy barely moves.</summary>
    private static readonly float[] Liveliness = { 0.8f, 1.3f, 0.45f };

    /// <summary>Surfaces drawn without an ink outline (small soft details).</summary>
    private static readonly HashSet<string> NoOutline = new()
    {
        "EyeShine", "Blush", "Mouth", "Print", "Heart", "Button", "BadgeStripe", "Lid", "Spots",
    };

    private static readonly Dictionary<string, ShaderMaterial> s_materials = new();
    private static Shader? s_toon;
    private static ShaderMaterial? s_outline;

    private readonly Dictionary<string, (Node3D Node, Transform3D Home)> _parts = new();
    private int _npc;
    private float _time;
    private float _talk;

    /// <summary>Head height above the feet (meters), for the prompt.</summary>
    public float Height => 1.6f * Scales[_npc];

    /// <summary>True while this NPC is in a conversation (bounces and nods).</summary>
    public bool Talking { get; set; }

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
        float wave = _talk * (0.25f + MathF.Sin(_time * 6f) * 0.15f) * live;
        Pose("ArmL", new Vector3(0f, hop, 0f), new Vector3(arm, 0f, -arm * 0.5f), Vector3.One);
        Pose("ArmR", new Vector3(0f, hop, 0f), new Vector3(-arm - wave, 0f, arm * 0.5f + wave), Vector3.One);
        // The hands stay at the ends of the arms.
        FollowArm("ArmL", "HandL");
        FollowArm("ArmR", "HandR");
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
        if (name is "EyeShine" or "Metal")
            material.SetShaderParameter("self_light", 0.5f);
        if (!NoOutline.Contains(name))
            material.NextPass = s_outline;
        s_materials[key] = material;
        return material;
    }
}
