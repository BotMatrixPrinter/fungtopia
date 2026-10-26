using System.Collections.Generic;
using Godot;

namespace Fungtopia.Town.View;

/// <summary>
/// Loads town.glb at run time and draws it toon-shaded with ink outlines. The glowing surfaces
/// (<see cref="TownLook.Glows"/>: lamp bulbs, glowing mushrooms) and Window share one material each, so the view lights them all up at night at once.
/// Mushroom caps (<see cref="TownLook.Caps"/>) get a soft glossy band and a light rim.
/// </summary>
public static class TownModels
{
    public const string Town = "res://modules/Town/Models/town.glb";
    private const string Shaders = "res://modules/Town/Godot/Shaders/";

    /// <summary>Surfaces drawn without an ink outline (small soft details).</summary>
    private static readonly HashSet<string> NoOutline = new() { "Spots", "SpotsPale", "MatStripe", "Water", "Soot", "Glow", "SignText", "Steam", "Scales", "MorelPit", "CupInner" };

    private static Shader? s_toon;
    private static ShaderMaterial? s_outline;
    private static readonly Dictionary<string, ShaderMaterial> s_materials = new();
    private static Node3D? s_scene;
    private static bool s_loaded;

    /// <summary>The materials that glow at night (<see cref="TownLook.Glows"/>: lamp bulbs, glowing mushroom caps) that the model has.</summary>
    public static IEnumerable<ShaderMaterial> Glowing()
    {
        foreach (var name in TownLook.Glows)
            if (s_materials.TryGetValue(name, out var material))
                yield return material;
    }

    /// <summary>The windows' material (null until the model is loaded).</summary>
    public static ShaderMaterial? Window => s_materials.GetValueOrDefault("Window");

    /// <summary>The toon material of surface <paramref name="name"/> of town.glb (null until the model is loaded, or when it has none).</summary>
    public static ShaderMaterial? Surface(string name) => s_materials.GetValueOrDefault(name);

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

    private static ShaderMaterial Toon(Color color, bool outline, float glow, bool cap)
    {
        s_toon ??= GD.Load<Shader>(Shaders + "toon.gdshader");
        s_outline ??= new ShaderMaterial { Shader = GD.Load<Shader>(Shaders + "outline.gdshader") };
        var material = new ShaderMaterial { Shader = s_toon };
        material.SetShaderParameter("albedo", color);
        material.SetShaderParameter("self_light", glow);
        material.SetShaderParameter("vivid", TownLook.Vividness);
        material.SetShaderParameter("gloss", cap ? TownLook.CapGloss : 0f);
        material.SetShaderParameter("rim_light", cap ? TownLook.CapRim : 0f);
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
        float glow = name is "Water" or "Steam" ? 0.25f : TownLook.Glowing(name) ? TownLook.DayGlow : name is "Window" ? TownLook.WindowDayGlow : 0f;
        var material = Toon(color.SrgbToLinear(), !NoOutline.Contains(name), glow, TownLook.IsCap(name));
        s_materials[name] = material;
        return material;
    }
}
