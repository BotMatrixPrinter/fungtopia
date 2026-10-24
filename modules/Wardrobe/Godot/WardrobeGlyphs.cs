using System.Collections.Generic;
using Godot;

namespace Fungtopia.Wardrobe.View;

/// <summary>
/// Makes this module's prompt icons (<see cref="WardrobePrompts.GlyphDirectory"/>) loadable by
/// their res:// paths even when Godot has not imported them (a fresh checkout run without the
/// editor), the way the Input module does for its own: each unimported SVG is rasterized and put
/// in the resource cache under its path.
/// </summary>
public static class WardrobeGlyphs
{
    // The resource cache holds textures weakly; these keep them alive.
    private static readonly List<Texture2D> s_kept = new();
    private static readonly HashSet<string> s_registered = new();

    /// <summary>Registers every SVG in <paramref name="directory"/> once (default: this module's icons).</summary>
    public static void Register(string directory = WardrobePrompts.GlyphDirectory)
    {
        if (!s_registered.Add(directory))
            return;
        foreach (var file in DirAccess.GetFilesAt(directory))
        {
            if (!file.EndsWith(".svg"))
                continue;
            string path = directory + file;
            if (ResourceLoader.Exists(path))
                continue;
            var image = new Image();
            if (image.LoadSvgFromString(FileAccess.GetFileAsString(path), 2f) != Error.Ok)
            {
                GD.PushWarning($"Fungtopia: Wardrobe: cannot read the icon {path}.");
                continue;
            }
            var texture = ImageTexture.CreateFromImage(image);
            texture.TakeOverPath(path);
            s_kept.Add(texture);
        }
    }
}
