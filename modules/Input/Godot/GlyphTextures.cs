using System.Collections.Generic;
using Godot;

namespace Fungtopia.Input.View;

/// <summary>
/// Makes the prompt icons in modules/Input/Glyphs/ loadable by their res:// paths (as
/// <see cref="Prompts.Rich"/> and <see cref="Prompts.Icon"/> give them) even when Godot has not
/// imported them, as in a fresh checkout run without the editor: each SVG not known to the
/// ResourceLoader is rasterized and put in the resource cache under its own path.
/// </summary>
public static class GlyphTextures
{
    /// <summary>Pixels per SVG unit: the 64-unit icons become 128-pixel textures, sharp at any prompt size.</summary>
    private const float Scale = 2f;

    // The resource cache holds textures weakly; these keep them alive.
    private static readonly List<Texture2D> s_kept = new();
    private static bool s_registered;

    /// <summary>Registers every icon once; later calls do nothing.</summary>
    public static void Register()
    {
        if (s_registered)
            return;
        s_registered = true;
        foreach (var file in DirAccess.GetFilesAt(Prompts.GlyphDirectory))
        {
            if (!file.EndsWith(".svg"))
                continue;
            string path = Prompts.GlyphDirectory + file;
            if (ResourceLoader.Exists(path))
                continue;
            var image = new Image();
            if (image.LoadSvgFromString(FileAccess.GetFileAsString(path), Scale) != Error.Ok)
            {
                GD.PushWarning($"Fungtopia: Input: cannot read the icon {path}.");
                continue;
            }
            var texture = ImageTexture.CreateFromImage(image);
            texture.TakeOverPath(path);
            s_kept.Add(texture);
        }
    }
}
