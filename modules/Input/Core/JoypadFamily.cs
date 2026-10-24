using System;

namespace Fungtopia.Input;

/// <summary>Tells a gamepad's family from the name and GUID Godot reports for it (Input.GetJoyName / GetJoyGuid).</summary>
public static class JoypadFamily
{
    private static readonly string[] s_playStation = { "playstation", "dualshock", "dualsense", "sony", "ps3", "ps4", "ps5" };
    private static readonly string[] s_nintendo = { "nintendo", "switch", "joy-con", "joycon", "pro controller" };
    private static readonly string[] s_xbox = { "xbox", "xinput", "x-box", "microsoft" };

    /// <summary>
    /// The family named in <paramref name="name"/> (PlayStation, Nintendo, Xbox, in that order), else
    /// the one of the USB vendor in an SDL <paramref name="guid"/> (Sony 054c, Nintendo 057e,
    /// Microsoft 045e), else Generic.
    /// </summary>
    public static DeviceKind Detect(string? name, string? guid)
    {
        if (!string.IsNullOrEmpty(name))
        {
            if (ContainsAny(name, s_playStation))
                return DeviceKind.PlayStation;
            if (ContainsAny(name, s_nintendo))
                return DeviceKind.Nintendo;
            if (ContainsAny(name, s_xbox))
                return DeviceKind.Xbox;
        }
        // SDL GUIDs are 32 hex digits: bus (4), CRC (4), vendor (4, little-endian), ...
        if (guid != null && guid.Length >= 12)
        {
            var vendor = guid.AsSpan(8, 4);
            if (vendor.Equals("4c05", StringComparison.OrdinalIgnoreCase))
                return DeviceKind.PlayStation;
            if (vendor.Equals("7e05", StringComparison.OrdinalIgnoreCase))
                return DeviceKind.Nintendo;
            if (vendor.Equals("5e04", StringComparison.OrdinalIgnoreCase))
                return DeviceKind.Xbox;
        }
        return DeviceKind.Generic;
    }

    private static bool ContainsAny(string text, string[] words)
    {
        foreach (var word in words)
            if (text.Contains(word, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
