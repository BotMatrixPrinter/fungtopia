using System;
using System.Text;
using Fungtopia.Input;

namespace Fungtopia.Wardrobe;

/// <summary>
/// The controls hints of the wardrobe and shop screens as RichTextLabel BBCode: the button icons
/// of one device only (<see cref="Prompts.Rich"/>), each followed by what it does, for example
/// "[A] Choose" on Xbox, "[Cross] Choose" on PlayStation, "[Enter] Choose" on keyboard. The tab
/// buttons have no <see cref="PromptAction"/>: LT / RT (L2, ZL ...) come from the Input module's
/// icons, Page Up / Page Down from this module's own (<see cref="GlyphDirectory"/>).
/// </summary>
public static class WardrobePrompts
{
    /// <summary>The icons' size in the hints, in pixels (the hint text is 18 px).</summary>
    public const int IconSize = 32;

    /// <summary>This module's icons (key_pgup, key_pgdn; 64x64 SVG like the Input module's).</summary>
    public const string GlyphDirectory = "res://modules/Wardrobe/Godot/Glyphs/";

    // Between an icon and its words: no break, so a group wraps as a whole; between groups: a break.
    private const string Tight = "\u00A0";
    private const string Gap = "     ";

    /// <summary>The wardrobe screen's hint: turn, tabs, choose, done.</summary>
    public static string Wardrobe(DeviceKind device, int size = IconSize)
    {
        var text = new StringBuilder();
        Turn(text, device, size);
        Group(text, Icons(Rich(TabIcon(-1, device), size), Rich(TabIcon(1, device), size)), "Tabs");
        Group(text, Prompts.Rich(PromptAction.Confirm, device, size), "Choose");
        Group(text, Prompts.Rich(PromptAction.Back, device, size), "Done");
        return text.ToString();
    }

    /// <summary>Abby's shop's hint: turn, pick then Buy, done.</summary>
    public static string Shop(DeviceKind device, int size = IconSize)
    {
        var text = new StringBuilder();
        Turn(text, device, size);
        Group(text, Prompts.Rich(PromptAction.Confirm, device, size), "Pick, then Buy");
        Group(text, Prompts.Rich(PromptAction.Back, device, size), "Done");
        return text.ToString();
    }

    /// <summary>
    /// The res:// icon of the previous (<paramref name="direction"/> &lt; 0) or next tab button:
    /// Page Up / Page Down on keyboard, the left / right trigger on gamepads.
    /// </summary>
    public static string TabIcon(int direction, DeviceKind device)
    {
        bool next = direction > 0;
        return device switch
        {
            DeviceKind.KeyboardMouse => GlyphDirectory + (next ? "key_pgdn.svg" : "key_pgup.svg"),
            DeviceKind.PlayStation => Prompts.GlyphDirectory + (next ? "ps_r2.svg" : "ps_l2.svg"),
            DeviceKind.Nintendo => Prompts.GlyphDirectory + (next ? "ns_zr.svg" : "ns_zl.svg"),
            DeviceKind.Xbox or DeviceKind.Generic => Prompts.GlyphDirectory + (next ? "xbox_rt.svg" : "xbox_lt.svg"),
            _ => throw new ArgumentOutOfRangeException(nameof(device), device, null),
        };
    }

    /// <summary>
    /// A gamepad's family from the name and GUID Godot reports (as the Input module tells it: the
    /// name, else the USB vendor in the SDL GUID), for input the view takes before the Input module.
    /// </summary>
    public static DeviceKind PadFamily(string? name, string? guid)
    {
        string n = (name ?? "").ToLowerInvariant();
        if (ContainsAny(n, "playstation", "dualshock", "dualsense", "sony", "ps3", "ps4", "ps5"))
            return DeviceKind.PlayStation;
        if (ContainsAny(n, "nintendo", "switch", "joy-con", "joycon", "pro controller"))
            return DeviceKind.Nintendo;
        if (ContainsAny(n, "xbox", "xinput", "x-box", "microsoft"))
            return DeviceKind.Xbox;
        string vendor = guid != null && guid.Length >= 12 ? guid.Substring(8, 4).ToLowerInvariant() : "";
        return vendor switch
        {
            "4c05" => DeviceKind.PlayStation,
            "7e05" => DeviceKind.Nintendo,
            "5e04" => DeviceKind.Xbox,
            _ => DeviceKind.Generic,
        };
    }

    private static void Turn(StringBuilder text, DeviceKind device, int size)
    {
        string buttons = Icons(Prompts.Rich(PromptAction.TurnLeft, device, size), Prompts.Rich(PromptAction.TurnRight, device, size));
        if (device == DeviceKind.KeyboardMouse)
        {
            Group(text, buttons, "Turn");
            Group(text, Prompts.Rich(PromptAction.Fire, device, size), "Drag to turn");
        }
        else
            Group(text, Icons(buttons, Prompts.Rich(PromptAction.Look, device, size)), "Turn");
    }

    private static string Rich(string icon, int size) => $"[img={size}]{icon}[/img]";

    private static string Icons(string a, string b) => a + Tight + b;

    private static void Group(StringBuilder text, string icons, string words)
    {
        if (text.Length > 0)
            text.Append(Gap);
        text.Append(icons).Append(Tight).Append(words.Replace(" ", Tight));
    }

    private static bool ContainsAny(string text, params string[] words)
    {
        foreach (var word in words)
            if (text.Contains(word, StringComparison.Ordinal))
                return true;
        return false;
    }
}
