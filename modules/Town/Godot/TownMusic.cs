using Godot;

namespace Fungtopia.Town.View;

/// <summary>
/// Mushroom Town's song, loaded at run time from the file's bytes (like town.glb), so a fresh
/// checkout plays it without the editor having imported it first.
/// </summary>
public static class TownMusic
{
    public const string Song = "res://modules/Town/Audio/mushroom_town.mp3";

    /// <summary>The volume at <see cref="TownModule.MusicLevel"/> 1: medium, under the other sounds.</summary>
    public const float FullVolumeDb = -8f;

    /// <summary>The song, looping seamlessly, or null (after one warning) when it is missing or unreadable.</summary>
    public static AudioStreamMP3? Load()
    {
        byte[] bytes = FileAccess.GetFileAsBytes(Song);
        if (bytes.Length == 0)
        {
            GD.PushWarning($"Fungtopia: Town: music {Song} is missing; the town stays silent.");
            return null;
        }
        // Godot logs an ERROR for bytes that are no MP3, so check them first.
        if (!TownModule.LooksLikeMp3(bytes))
        {
            GD.PushWarning($"Fungtopia: Town: music {Song} is not an MP3; the town stays silent.");
            return null;
        }
        return new AudioStreamMP3 { Data = bytes, Loop = true };
    }
}
