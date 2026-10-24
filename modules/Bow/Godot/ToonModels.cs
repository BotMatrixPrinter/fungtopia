using System.Collections.Generic;
using Godot;

namespace Fungtopia.Bow.View;

/// <summary>
/// Loads the bow's .glb models at run time (from the file's bytes, so a fresh checkout runs
/// without an editor import) and draws them toon-shaded with ink outlines.
/// </summary>
public static class ToonModels
{
    public const string Models = "res://modules/Bow/Models/";
    private const string Shaders = "res://modules/Bow/Godot/Shaders/";

    /// <summary>Surfaces drawn without an ink outline (small details).</summary>
    private static readonly HashSet<string> NoOutline = new() { "Spots", "PoisonSpots" };

    private static readonly Dictionary<string, ShaderMaterial> s_materials = new();
    private static readonly Dictionary<string, byte[]> s_files = new();
    private static Shader? s_toon;
    private static ShaderMaterial? s_outline;

    /// <summary>A new instance of the model at <paramref name="path"/>, or an empty node when it is missing.</summary>
    public static Node3D Load(string path)
    {
        if (!s_files.TryGetValue(path, out var bytes))
            s_files[path] = bytes = FileAccess.GetFileAsBytes(path);
        if (bytes.Length == 0)
        {
            GD.PushWarning($"Fungtopia: Bow: model {path} is missing.");
            return new Node3D();
        }
        var document = new GltfDocument();
        var state = new GltfState();
        if (document.AppendFromBuffer(bytes, path.GetBaseDir(), state) != Error.Ok
            || document.GenerateScene(state) is not Node3D scene)
        {
            GD.PushWarning($"Fungtopia: Bow: model {path} could not be read.");
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

    /// <summary>The toon material for a glTF material: its color, an ink outline unless it is a small detail.</summary>
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
        if (name == "PoisonSpots")
            material.SetShaderParameter("self_light", 0.5f);
        if (!NoOutline.Contains(name))
            material.NextPass = s_outline;
        s_materials[key] = material;
        return material;
    }
}
