using System.Collections.Generic;
using Godot;

namespace Fungtopia.Wardrobe.View;

/// <summary>
/// Loads the Wardrobe's .glb models at run time from the files' bytes (so a fresh checkout runs
/// without an editor import), drawn toon-shaded with ink outlines like the characters.
/// </summary>
public static class ToonGlb
{
    private const string Shaders = "res://modules/Wardrobe/Godot/Shaders/";

    private static readonly Dictionary<string, byte[]> s_bytes = new();
    private static Shader? s_toon;
    private static ShaderMaterial? s_outline;

    /// <summary>The model at <paramref name="path"/> (rig coordinates: feet at the origin), or null with a warning.</summary>
    public static Node3D? Load(string path)
    {
        if (!s_bytes.TryGetValue(path, out var bytes))
            s_bytes[path] = bytes = FileAccess.GetFileAsBytes(path);
        if (bytes.Length == 0)
        {
            GD.PushWarning($"Fungtopia: Wardrobe: model {path} is missing.");
            return null;
        }
        var document = new GltfDocument();
        var state = new GltfState();
        if (document.AppendFromBuffer(bytes, path.GetBaseDir(), state) != Error.Ok)
        {
            GD.PushWarning($"Fungtopia: Wardrobe: model {path} could not be read.");
            return null;
        }
        var scene = document.GenerateScene(state) as Node3D;
        if (scene != null)
            Toon(scene);
        return scene;
    }

    /// <summary>Replaces every material under <paramref name="node"/> with the toon shader in its color, with an outline.</summary>
    public static void Toon(Node node)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh != null)
        {
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
            {
                var source = mesh.Mesh.SurfaceGetMaterial(i);
                // glTF stores linear colors; the shader's albedo is an sRGB color like Blender's swatch.
                var color = source is BaseMaterial3D b ? b.AlbedoColor.SrgbToLinear() : Colors.White;
                var toon = Material(color);
                toon.SetMeta("name", source?.ResourceName ?? "");
                mesh.SetSurfaceOverrideMaterial(i, toon);
            }
        }
        foreach (var child in node.GetChildren())
            Toon(child);
    }

    public static ShaderMaterial Material(Color color)
    {
        s_toon ??= GD.Load<Shader>(Shaders + "toon.gdshader");
        s_outline ??= new ShaderMaterial { Shader = GD.Load<Shader>(Shaders + "outline.gdshader") };
        var material = new ShaderMaterial { Shader = s_toon, NextPass = s_outline };
        material.SetShaderParameter("albedo", color);
        return material;
    }

    /// <summary>Sets the color of every surface under <paramref name="node"/> whose glTF material is named <paramref name="name"/>.</summary>
    public static void Tint(Node node, string name, Color color)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh != null)
            for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
                if (mesh.GetSurfaceOverrideMaterial(i) is ShaderMaterial m && (string)m.GetMeta("name", "") == name)
                    m.SetShaderParameter("albedo", color);
        foreach (var child in node.GetChildren())
            Tint(child, name, color);
    }

    /// <summary>The bounding box of every mesh under <paramref name="root"/>, in its coordinates.</summary>
    public static Aabb Bounds(Node3D root)
    {
        var box = new Aabb();
        bool any = false;
        void Walk(Node node, Transform3D parent)
        {
            var xf = node is Node3D n3 && node != root ? parent * n3.Transform : parent;
            if (node is MeshInstance3D mesh && mesh.Mesh != null)
            {
                var b = xf * mesh.Mesh.GetAabb();
                box = any ? box.Merge(b) : b;
                any = true;
            }
            foreach (var child in node.GetChildren())
                Walk(child, xf);
        }
        Walk(root, Transform3D.Identity);
        return box;
    }
}
