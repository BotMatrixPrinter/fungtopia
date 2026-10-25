using System.Collections.Generic;
using Godot;

namespace Fungtopia.PetCity.View;

/// <summary>
/// Loads petcity.glb at run time and draws its objects toon-shaded with ink outlines. Materials are
/// shared by name, so the city's windows, sign and lamps light up together at night; a city block's
/// "Wall" and "Roof" are recolored per style, and its mushroom cap roof ("CapRoof") per style from
/// <see cref="PetCityLook.StyleCaps"/>. Mushroom caps get <see cref="PetCityLook"/>'s gloss and rim.
/// </summary>
public static class PetCityModels
{
    public const string Path = "res://modules/PetCity/Models/petcity.glb";
    private const string Shaders = "res://modules/PetCity/Godot/Shaders/";

    /// <summary>Surfaces drawn without an ink outline (glass, water and small soft details).</summary>
    private static readonly HashSet<string> NoOutline = new() { "EyeShine", "Blush", "Window", "Water", "Glow", "LampGlow" };

    /// <summary>Surfaces that glow at night: windows, lamps, the sign's letters and hearts.</summary>
    private static readonly HashSet<string> NightGlow = new() { "Window", "Glow", "LampGlow", "SignText", "Heart" };

    private static readonly Color WindowDay = new(0.56f, 0.78f, 0.96f);
    private static readonly Color WindowNight = new(1f, 0.84f, 0.5f);

    private static Shader? s_toon;
    private static ShaderMaterial? s_outline;
    private static Node3D? s_scene;
    private static readonly Dictionary<string, ShaderMaterial> s_materials = new();
    private static readonly Dictionary<string, Color> s_baseColors = new();

    /// <summary>A fresh copy of the object <paramref name="name"/> (or null when missing), keeping its own transform.</summary>
    public static Node3D? Instance(string name, Color? wall = null, Color? roof = null, Color? cap = null)
    {
        if (s_scene == null)
        {
            s_scene = Load();
            if (s_scene == null)
                return null;
        }
        if (s_scene.FindChild(name, true, false) is not MeshInstance3D source || source.Mesh == null)
        {
            GD.PushWarning($"Fungtopia: PetCity: {Path} has no object {name}.");
            return null;
        }
        var copy = new MeshInstance3D
        {
            Name = name,
            Mesh = source.Mesh,
            Transform = source.Transform,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
        };
        for (int i = 0; i < source.Mesh.GetSurfaceCount(); i++)
        {
            var original = source.Mesh.SurfaceGetMaterial(i);
            string material = original?.ResourceName ?? "";
            var color = material == "Wall" ? wall : material == "Roof" ? roof : material == "CapRoof" ? cap : null;
            copy.SetSurfaceOverrideMaterial(i, ToonMaterial(original, color));
        }
        return copy;
    }

    /// <summary>Lights the windows, lamps and sign (night) or turns them back to daylight glass.</summary>
    public static void SetNight(bool night)
    {
        foreach (var (key, material) in s_materials)
        {
            string name = key.Split('#')[0];
            if (!NightGlow.Contains(name))
                continue;
            if (name == "Window")
            {
                material.SetShaderParameter("albedo", night ? WindowNight : WindowDay);
                material.SetShaderParameter("self_light", night ? 1.3f : 0.12f);
            }
            else
            {
                float glow = name switch
                {
                    "LampGlow" => night ? PetCityLook.LampGlowNight : PetCityLook.GlowDay,
                    "Glow" => night ? PetCityLook.GlowNight : PetCityLook.GlowDay,
                    _ => night ? 0.7f : 0f,
                };
                material.SetShaderParameter("self_light", glow);
            }
        }
    }

    /// <summary>A Godot color from a <see cref="Swatch"/>.</summary>
    public static Color ColorOf(Swatch swatch) => new(swatch.R, swatch.G, swatch.B);

    /// <summary>A plain toon material.</summary>
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

    /// <summary>Frees the loaded model (the view calls it when it leaves the tree).</summary>
    public static void Release()
    {
        s_scene?.Free();
        s_scene = null;
        s_materials.Clear();
    }

    /// <summary>
    /// Loads the .glb from its bytes, so the game runs without the editor having imported it first
    /// (a fresh checkout loads headless).
    /// </summary>
    private static Node3D? Load()
    {
        byte[] bytes = FileAccess.GetFileAsBytes(Path);
        if (bytes.Length == 0)
        {
            GD.PushWarning($"Fungtopia: PetCity: model {Path} is missing.");
            return null;
        }
        var document = new GltfDocument();
        var state = new GltfState();
        var error = document.AppendFromBuffer(bytes, Path.GetBaseDir(), state);
        if (error != Error.Ok)
        {
            GD.PushWarning($"Fungtopia: PetCity: model {Path} could not be read ({error}).");
            return null;
        }
        return document.GenerateScene(state) as Node3D;
    }

    private static ShaderMaterial ToonMaterial(Material? source, Color? recolor)
    {
        string name = source?.ResourceName ?? "";
        // Darkened like the other modules' glTF colors, so the city's swatches match theirs.
        var color = (recolor ?? (source is BaseMaterial3D standard ? standard.AlbedoColor : Colors.White)).SrgbToLinear();
        string key = name + "#" + color.ToHtml();
        if (s_materials.TryGetValue(key, out var cached))
            return cached;
        float glow = name is "EyeShine" ? 0.6f : name is "Glow" or "LampGlow" ? PetCityLook.GlowDay : name is "Window" ? 0.12f : name is "Water" ? 0.25f : 0f;
        var material = Toon(color, !NoOutline.Contains(name), glow);
        if (PetCityLook.IsCap(name))
        {
            material.SetShaderParameter("gloss", PetCityLook.CapGloss);
            material.SetShaderParameter("rim_light", PetCityLook.CapRim);
        }
        s_materials[key] = material;
        return material;
    }
}
