using System.Collections.Generic;
using Godot;

namespace Fungtopia.Travel.View;

/// <summary>
/// Loads signpost.glb at run time (from the file's bytes, so a fresh checkout runs without an
/// editor import) and draws it toon-shaded with ink outlines.
/// </summary>
public static class SignpostModel
{
    public const string Path = "res://modules/Travel/Models/signpost.glb";
    private const string Shaders = "res://modules/Travel/Godot/Shaders/";

    /// <summary>Surfaces drawn without an ink outline (small details).</summary>
    private static readonly HashSet<string> NoOutline = new() { "Spots", "Nails", "Pupil", "Sparkle" };

    private static readonly Dictionary<string, ShaderMaterial> s_materials = new();
    private static byte[]? s_bytes;
    private static Shader? s_toon;
    private static ShaderMaterial? s_outline;

    /// <summary>A new signpost (objects Post, Board0..2), or an empty node when the model is missing.</summary>
    public static Node3D Load()
    {
        s_bytes ??= FileAccess.GetFileAsBytes(Path);
        if (s_bytes.Length == 0)
        {
            GD.PushWarning($"Fungtopia: Travel: model {Path} is missing.");
            return new Node3D();
        }
        var document = new GltfDocument();
        var state = new GltfState();
        if (document.AppendFromBuffer(s_bytes, Path.GetBaseDir(), state) != Error.Ok
            || document.GenerateScene(state) is not Node3D scene)
        {
            GD.PushWarning($"Fungtopia: Travel: model {Path} could not be read.");
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
        if (!NoOutline.Contains(name))
            material.NextPass = s_outline;
        s_materials[key] = material;
        return material;
    }
}
