using System;
using Godot;

namespace Fungtopia.Input.View;

/// <summary>
/// A sheet of every prompt icon: the table of <see cref="PromptAction"/> by <see cref="DeviceKind"/>
/// drawn with <see cref="Prompts.Rich"/>, then every file in modules/Input/Glyphs/. For screenshots:
/// ./screenshot.sh modules/Input/Godot/GlyphSheet.tscn glyphs.png
/// </summary>
public partial class GlyphSheet : Control
{
    private static readonly DeviceKind[] Devices = { DeviceKind.KeyboardMouse, DeviceKind.Xbox, DeviceKind.PlayStation, DeviceKind.Nintendo };

    public override void _Ready()
    {
        GlyphTextures.Register();
        SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(new ColorRect { Color = new Color("f6efe2"), AnchorRight = 1, AnchorBottom = 1 });

        var table = new GridContainer { Columns = Devices.Length + 1, Position = new Vector2(30, 20) };
        table.AddThemeConstantOverride("h_separation", 14);
        table.AddThemeConstantOverride("v_separation", 2);
        AddChild(table);
        table.AddChild(Label(""));
        foreach (var device in Devices)
            table.AddChild(Label(device.ToString()));
        foreach (PromptAction action in Enum.GetValues<PromptAction>())
        {
            table.AddChild(Label(action.ToString()));
            foreach (var device in Devices)
                table.AddChild(Rich($"{Prompts.Rich(action, device, 40)} {Prompts.Text(action, device)}", 260));
        }

        var all = new GridContainer { Columns = 7, Position = new Vector2(1230, 20) };
        all.AddThemeConstantOverride("h_separation", 6);
        all.AddThemeConstantOverride("v_separation", 4);
        AddChild(all);
        foreach (var file in DirAccess.GetFilesAt(Prompts.GlyphDirectory))
        {
            if (!file.EndsWith(".svg"))
                continue;
            string name = file[..^4];
            all.AddChild(Rich($"[center][img=64]{Prompts.GlyphDirectory}{file}[/img]\n[font_size=11]{name}[/font_size][/center]", 92));
        }
    }

    private static Label Label(string text)
    {
        var label = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeColorOverride("font_color", new Color("4a3b32"));
        label.AddThemeFontSizeOverride("font_size", 18);
        return label;
    }

    private static RichTextLabel Rich(string bbcode, float width)
    {
        var rich = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, CustomMinimumSize = new Vector2(width, 0), Text = bbcode };
        rich.AddThemeColorOverride("default_color", new Color("4a3b32"));
        rich.AddThemeFontSizeOverride("normal_font_size", 18);
        return rich;
    }
}
