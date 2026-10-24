using System;

namespace Fungtopia.Input;

/// <summary>
/// Button prompts for the active device (see <see cref="ActiveDevice"/>): the icon of each
/// <see cref="PromptAction"/>'s binding on keyboard and mouse or on each gamepad family. Generic
/// gamepads use the Xbox icons. The table is in modules/Input/README.md.
/// </summary>
public static class Prompts
{
    /// <summary>The folder of the icons (64x64 SVG files named <c>&lt;glyph&gt;.svg</c>).</summary>
    public const string GlyphDirectory = "res://modules/Input/Glyphs/";

    private const int Families = 4; // keyboard/mouse, Xbox, PlayStation, Nintendo

    // Per action: keyboard/mouse, Xbox, PlayStation, Nintendo (Godot's buttons are positional:
    // its "A" is the bottom button, Nintendo's B; its "B" the right one, Nintendo's A; its "X" the left one, Nintendo's Y).
    private static readonly string[,] s_glyphs =
    {
        /* Interact  */ { "key_e", "xbox_x", "ps_square", "ns_y" },
        /* Jump      */ { "key_space", "xbox_a", "ps_cross", "ns_b" },
        /* Fire      */ { "mouse_left", "xbox_rt", "ps_r2", "ns_zr" },
        /* Back      */ { "key_esc", "xbox_b", "ps_circle", "ns_a" },
        /* Confirm   */ { "key_enter", "xbox_a", "ps_cross", "ns_b" },
        /* OpenMap   */ { "key_m", "xbox_view", "ps_share", "ns_minus" },
        /* Eat       */ { "key_f", "xbox_dpad_up", "ps_dpad_up", "ns_dpad_up" },
        /* FoodPrev  */ { "mouse_wheel_up", "xbox_dpad_left", "ps_dpad_left", "ns_dpad_left" },
        /* FoodNext  */ { "mouse_wheel_down", "xbox_dpad_right", "ps_dpad_right", "ns_dpad_right" },
        /* Pause     */ { "key_esc", "xbox_menu", "ps_options", "ns_plus" },
        /* MenuUp    */ { "key_up", "xbox_dpad_up", "ps_dpad_up", "ns_dpad_up" },
        /* MenuDown  */ { "key_down", "xbox_dpad_down", "ps_dpad_down", "ns_dpad_down" },
        /* MenuLeft  */ { "key_left", "xbox_dpad_left", "ps_dpad_left", "ns_dpad_left" },
        /* MenuRight */ { "key_right", "xbox_dpad_right", "ps_dpad_right", "ns_dpad_right" },
        /* TurnLeft  */ { "key_q", "xbox_lb", "ps_l1", "ns_l" },
        /* TurnRight */ { "key_e", "xbox_rb", "ps_r1", "ns_r" },
        /* Move      */ { "key_wasd", "xbox_ls", "ps_ls", "ns_ls" },
        /* Look      */ { "mouse_move", "xbox_rs", "ps_rs", "ns_rs" },
    };

    private static readonly string[,] s_texts =
    {
        /* Interact  */ { "E", "X", "Square", "Y" },
        /* Jump      */ { "Space", "A", "Cross", "B" },
        /* Fire      */ { "Left Click", "RT", "R2", "ZR" },
        /* Back      */ { "Esc", "B", "Circle", "A" },
        /* Confirm   */ { "Enter", "A", "Cross", "B" },
        /* OpenMap   */ { "M", "View", "Share", "-" },
        /* Eat       */ { "F", "D-pad Up", "D-pad Up", "D-pad Up" },
        /* FoodPrev  */ { "Wheel Up", "D-pad Left", "D-pad Left", "D-pad Left" },
        /* FoodNext  */ { "Wheel Down", "D-pad Right", "D-pad Right", "D-pad Right" },
        /* Pause     */ { "Esc", "Menu", "Options", "+" },
        /* MenuUp    */ { "Up", "D-pad Up", "D-pad Up", "D-pad Up" },
        /* MenuDown  */ { "Down", "D-pad Down", "D-pad Down", "D-pad Down" },
        /* MenuLeft  */ { "Left", "D-pad Left", "D-pad Left", "D-pad Left" },
        /* MenuRight */ { "Right", "D-pad Right", "D-pad Right", "D-pad Right" },
        /* TurnLeft  */ { "Q", "LB", "L1", "L" },
        /* TurnRight */ { "E", "RB", "R1", "R" },
        /* Move      */ { "WASD", "Left Stick", "Left Stick", "Left Stick" },
        /* Look      */ { "Mouse", "Right Stick", "Right Stick", "Right Stick" },
    };

    private static readonly string[,] s_icons = Paths();

    /// <summary>The icon's file name without folder and extension, for example "key_e" or "ps_cross".</summary>
    public static string Glyph(PromptAction action, DeviceKind device) => s_glyphs[Row(action), Column(device)];

    /// <summary>The res:// path of the icon of <paramref name="action"/>'s button on <paramref name="device"/>.</summary>
    public static string Icon(PromptAction action, DeviceKind device) => s_icons[Row(action), Column(device)];

    /// <summary>RichTextLabel BBCode showing the icon <paramref name="size"/> pixels wide: "[img=28]res://...[/img]".</summary>
    public static string Rich(PromptAction action, DeviceKind device, int size = 28) =>
        $"[img={size}]{Icon(action, device)}[/img]";

    /// <summary>A plain-text name of the button, for where an icon cannot show ("E", "A", "Cross").</summary>
    public static string Text(PromptAction action, DeviceKind device) => s_texts[Row(action), Column(device)];

    private static int Row(PromptAction action)
    {
        int row = (int)action;
        if ((uint)row >= (uint)s_glyphs.GetLength(0))
            throw new ArgumentOutOfRangeException(nameof(action), action, null);
        return row;
    }

    private static int Column(DeviceKind device) => device switch
    {
        DeviceKind.KeyboardMouse => 0,
        DeviceKind.Xbox or DeviceKind.Generic => 1,
        DeviceKind.PlayStation => 2,
        DeviceKind.Nintendo => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(device), device, null),
    };

    private static string[,] Paths()
    {
        var paths = new string[s_glyphs.GetLength(0), Families];
        for (int row = 0; row < paths.GetLength(0); row++)
            for (int column = 0; column < Families; column++)
                paths[row, column] = GlyphDirectory + s_glyphs[row, column] + ".svg";
        return paths;
    }
}
