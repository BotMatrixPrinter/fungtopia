using System;
using System.Collections.Generic;
using Godot;

namespace Fungtopia.Townsfolk.View;

/// <summary>
/// Loads the Townsfolk models (folk.glb, cat.glb, dog.glb, crow.glb, butterfly.glb from
/// modules/Townsfolk/Models) at run time from the files' bytes (so a fresh checkout runs without an
/// editor import), once per file, and makes copies drawn toon-shaded with ink outlines, tinted by
/// material name.
/// </summary>
public static class Figures
{
    private const string Models = "res://modules/Townsfolk/Models/";
    private const string Shaders = "res://modules/Townsfolk/Godot/Shaders/";

    /// <summary>Surfaces drawn without an ink outline (small soft details).</summary>
    private static readonly HashSet<string> NoOutline = new()
    {
        "EyeShine", "Blush", "Finger", "HatSpots", "Spots", "Gills", "Nose", "WingSpot", "Eye", "Leg",
    };

    private static readonly Dictionary<string, (GltfDocument Document, GltfState State)?> s_files = new();
    private static readonly Dictionary<string, ShaderMaterial> s_materials = new();
    private static Shader? s_toon;
    private static ShaderMaterial? s_outline;

    /// <summary>A new copy of the model in <paramref name="file"/>, its surfaces tinted: <paramref name="tints"/> maps a material name to an sRGB color.</summary>
    public static Node3D Make(string file, IReadOnlyDictionary<string, Color>? tints = null, bool outline = true)
    {
        if (!s_files.TryGetValue(file, out var loaded))
            s_files[file] = loaded = Read(Models + file);
        if (loaded is not { } gltf || gltf.Document.GenerateScene(gltf.State) is not Node3D scene)
            return new Node3D();
        Toonify(scene, tints, outline);
        return scene;
    }

    private static (GltfDocument, GltfState)? Read(string path)
    {
        var bytes = FileAccess.GetFileAsBytes(path);
        if (bytes.Length == 0)
        {
            GD.PushWarning($"Fungtopia: Townsfolk: model {path} is missing.");
            return null;
        }
        var document = new GltfDocument();
        var state = new GltfState();
        if (document.AppendFromBuffer(bytes, path.GetBaseDir(), state) != Error.Ok)
        {
            GD.PushWarning($"Fungtopia: Townsfolk: model {path} could not be read.");
            return null;
        }
        return (document, state);
    }

    private static void Toonify(Node node, IReadOnlyDictionary<string, Color>? tints, bool outline)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh != null)
        {
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
                mesh.SetSurfaceOverrideMaterial(i, Material(mesh.Mesh.SurfaceGetMaterial(i), tints, outline));
        }
        foreach (var child in node.GetChildren())
            Toonify(child, tints, outline);
    }

    private static ShaderMaterial Material(Material? source, IReadOnlyDictionary<string, Color>? tints, bool outline)
    {
        string name = source?.ResourceName ?? "";
        // glTF stores linear colors; the shader's albedo is an sRGB color like Blender's swatch.
        var color = tints != null && tints.TryGetValue(name, out var tint) ? tint
            : source is BaseMaterial3D standard ? standard.AlbedoColor.SrgbToLinear() : Colors.White;
        string key = name + color.ToHtml() + outline;
        if (s_materials.TryGetValue(key, out var cached))
            return cached;
        s_toon ??= GD.Load<Shader>(Shaders + "toon.gdshader");
        s_outline ??= new ShaderMaterial { Shader = GD.Load<Shader>(Shaders + "outline.gdshader") };
        var material = new ShaderMaterial { Shader = s_toon };
        material.SetShaderParameter("albedo", color);
        if (name is "EyeShine")
            material.SetShaderParameter("self_light", 0.5f);
        if (name is "Wing")
            material.SetShaderParameter("self_light", 0.15f);
        if (outline && !NoOutline.Contains(name))
            material.NextPass = s_outline;
        s_materials[key] = material;
        return material;
    }

    /// <summary>Forgets the loaded models and materials (when the view leaves the tree).</summary>
    public static void Release()
    {
        s_files.Clear();
        s_materials.Clear();
        s_toon = null;
        s_outline = null;
    }

    public static Color ToColor(System.Numerics.Vector3 c) => new(c.X, c.Y, c.Z);

    /// <summary>A part of a model with its rest transform, to pose it relative to that.</summary>
    public readonly struct Part
    {
        public readonly Node3D? Node;
        public readonly Transform3D Home;

        public Part(Node3D? node)
        {
            Node = node;
            Home = node?.Transform ?? Transform3D.Identity;
        }

        public void Pose(Vector3 offset, Vector3 euler, float scale = 1f)
        {
            if (Node == null)
                return;
            Node.Transform = new Transform3D(Home.Basis * Basis.FromEuler(euler).Scaled(Vector3.One * scale), Home.Origin + offset);
        }

        public void Pose(Vector3 offset, Vector3 euler, Vector3 scale)
        {
            if (Node == null)
                return;
            Node.Transform = new Transform3D(Home.Basis * Basis.FromEuler(euler).Scaled(scale), Home.Origin + offset);
        }
    }

    public static Part Find(Node root, string name) => new(root.FindChild(name, true, false) as Node3D);

    /// <summary>Keeps a floating hand at the end of its invisible arm as the arm turns.</summary>
    public static void FollowArm(in Part arm, in Part hand)
    {
        if (arm.Node == null || hand.Node == null)
            return;
        var local = arm.Home.AffineInverse() * hand.Home.Origin;
        hand.Node.Position = arm.Node.Transform * local;
    }

    public static float LerpAngle(float a, float b, float t)
    {
        float d = b - a;
        d -= MathF.Round(d / MathF.Tau) * MathF.Tau;
        return a + d * t;
    }
}
