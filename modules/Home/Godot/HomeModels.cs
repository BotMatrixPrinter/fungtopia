using System.Collections.Generic;
using Godot;

namespace Fungtopia.Home.View;

/// <summary>
/// Loads .glb models at run time (furniture.glb from this module, and the Pet module's pet models
/// to show the pets at home) and draws them toon-shaded with ink outlines.
/// </summary>
public static class HomeModels
{
    public const string Furniture = "res://modules/Home/Models/furniture.glb";
    public const string PetModels = "res://modules/Pet/Models/";
    private const string Shaders = "res://modules/Home/Godot/Shaders/";

    /// <summary>Surfaces drawn without an ink outline (small soft details).</summary>
    private static readonly HashSet<string> NoOutline = new() { "EyeShine", "Blush", "Mouth", "Whisker", "Tongue", "RugDots", "Page" };

    private static Shader? s_toon;
    private static ShaderMaterial? s_outline;
    private static readonly Dictionary<string, ShaderMaterial> s_materials = new();
    private static readonly Dictionary<string, Node3D?> s_scenes = new();

    /// <summary>A fresh copy of the object <paramref name="name"/> of a .glb (or null when missing), toon-shaded.</summary>
    public static Node3D? Instance(string path, string? name = null)
    {
        if (!s_scenes.TryGetValue(path, out var scene))
        {
            scene = Load(path);
            if (scene != null)
                ApplyMaterials(scene);
            s_scenes[path] = scene;
        }
        if (scene == null)
            return null;
        Node? source = name == null ? scene : scene.FindChild(name, true, false);
        if (source is not Node3D node)
        {
            GD.PushWarning($"Fungtopia: Home: {path} has no object {name}.");
            return null;
        }
        var copy = (Node3D)node.Duplicate();
        copy.Transform = Transform3D.Identity;
        return copy;
    }

    /// <summary>Frees the loaded models (the view calls it when it leaves the tree).</summary>
    public static void Release()
    {
        foreach (var scene in s_scenes.Values)
            scene?.Free();
        s_scenes.Clear();
    }

    /// <summary>A plain toon material (for shapes made in code).</summary>
    public static ShaderMaterial Toon(Color color, bool outline = true, float glow = 0f)
    {
        s_toon ??= GD.Load<Shader>(Shaders + "toon.gdshader");
        s_outline ??= new ShaderMaterial { Shader = GD.Load<Shader>(Shaders + "outline.gdshader") };
        var material = new ShaderMaterial { Shader = s_toon };
        material.SetShaderParameter("albedo", color);
        material.SetShaderParameter("self_light", glow);
        if (outline)
            material.NextPass = s_outline;
        return material;
    }

    /// <summary>
    /// Loads a .glb from its bytes, so the game runs without the editor having imported it first
    /// (a fresh checkout loads headless).
    /// </summary>
    private static Node3D? Load(string path)
    {
        byte[] bytes = FileAccess.GetFileAsBytes(path);
        if (bytes.Length == 0)
        {
            GD.PushWarning($"Fungtopia: Home: model {path} is missing.");
            return null;
        }
        var document = new GltfDocument();
        var state = new GltfState();
        var error = document.AppendFromBuffer(bytes, path.GetBaseDir(), state);
        if (error != Error.Ok)
        {
            GD.PushWarning($"Fungtopia: Home: model {path} could not be read ({error}).");
            return null;
        }
        return document.GenerateScene(state) as Node3D;
    }

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

    private static ShaderMaterial ToonMaterial(Material? source)
    {
        string name = source?.ResourceName ?? "";
        var color = source is BaseMaterial3D standard ? standard.AlbedoColor : Colors.White;
        string key = name + color.ToHtml();
        if (s_materials.TryGetValue(key, out var cached))
            return cached;
        // glTF stores linear colors; the shader's albedo is an sRGB color like Blender's swatch.
        float glow = name is "EyeShine" ? 0.6f : name is "Glow" ? 1.2f : 0f;
        var material = Toon(color.SrgbToLinear(), !NoOutline.Contains(name), glow);
        s_materials[key] = material;
        return material;
    }
}
