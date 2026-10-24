using System.Collections.Generic;
using Godot;

namespace Fungtopia.Town.View;

/// <summary>
/// Loads town.glb at run time and draws it toon-shaded with ink outlines. The Glow (lamp bulbs)
/// and Window surfaces share one material each, so the view lights them all up at night at once.
/// </summary>
public static class TownModels
{
    public const string Town = "res://modules/Town/Models/town.glb";
    private const string Shaders = "res://modules/Town/Godot/Shaders/";

    /// <summary>Surfaces drawn without an ink outline (small soft details).</summary>
    private static readonly HashSet<string> NoOutline = new() { "Spots", "SpotsPale", "MatStripe", "Water", "Soot", "Glow", "SignText" };

    private static Shader? s_toon;
    private static ShaderMaterial? s_outline;
    private static readonly Dictionary<string, ShaderMaterial> s_materials = new();
    private static Node3D? s_scene;
    private static bool s_loaded;

    /// <summary>The lamp bulbs' material (null until the model is loaded).</summary>
    public static ShaderMaterial? Glow => s_materials.GetValueOrDefault("Glow");

    /// <summary>The windows' material (null until the model is loaded).</summary>
    public static ShaderMaterial? Window => s_materials.GetValueOrDefault("Window");

    /// <summary>A fresh copy of the object <paramref name="name"/> of town.glb (or null when missing), toon-shaded.</summary>
    public static Node3D? Instance(string name)
    {
        if (!s_loaded)
        {
            s_loaded = true;
            s_scene = Load(Town);
            if (s_scene != null)
                ApplyMaterials(s_scene);
        }
        if (s_scene?.FindChild(name, true, false) is not Node3D node)
        {
            GD.PushWarning($"Fungtopia: Town: {Town} has no object {name}.");
            return null;
        }
        var copy = (Node3D)node.Duplicate();
        copy.Transform = Transform3D.Identity;
        return copy;
    }

    /// <summary>Frees the loaded model (the view calls it when it leaves the tree).</summary>
    public static void Release()
    {
        s_scene?.Free();
        s_scene = null;
        s_loaded = false;
        s_materials.Clear();
    }

    private static ShaderMaterial Toon(Color color, bool outline, float glow)
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
            GD.PushWarning($"Fungtopia: Town: model {path} is missing.");
            return null;
        }
        var document = new GltfDocument();
        var state = new GltfState();
        var error = document.AppendFromBuffer(bytes, path.GetBaseDir(), state);
        if (error != Error.Ok)
        {
            GD.PushWarning($"Fungtopia: Town: model {path} could not be read ({error}).");
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
        if (s_materials.TryGetValue(name, out var cached))
            return cached;
        // glTF stores linear colors; the shader's albedo is an sRGB color like Blender's swatch.
        var color = source is BaseMaterial3D standard ? standard.AlbedoColor : Colors.White;
        float glow = name is "Water" ? 0.25f : name is "Glow" or "Window" ? TownView.DayGlow : 0f;
        var material = Toon(color.SrgbToLinear(), !NoOutline.Contains(name), glow);
        s_materials[name] = material;
        return material;
    }
}
