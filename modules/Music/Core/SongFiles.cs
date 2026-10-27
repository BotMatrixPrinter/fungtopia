using System;

namespace Fungtopia.Music;

/// <summary>Where each song's file is and what its bytes must look like, so the view can refuse a broken file.</summary>
public static class SongFiles
{
    /// <summary>The songs that have a file, in <see cref="Song"/> order.</summary>
    public static readonly Song[] All =
    {
        Song.SeaSponge, Song.SnowmanSnuggles, Song.PopcornParty, Song.EightBitSnack, Song.Darnt,
    };

    /// <summary>The song's file name in <c>modules/Music/Audio/</c>, or null for <see cref="Song.None"/>.</summary>
    public static string? FileName(Song song) => song switch
    {
        Song.SeaSponge => "sea_sponge.mp3",
        Song.SnowmanSnuggles => "snowman_snuggles.mp3",
        Song.PopcornParty => "popcorn_party.mp3",
        Song.EightBitSnack => "eight_bit_snack.wav",
        Song.Darnt => "darnt.mp3",
        _ => null,
    };

    /// <summary>Whether the song's file is a WAV (else an MP3).</summary>
    public static bool IsWav(Song song) => FileName(song)?.EndsWith(".wav", StringComparison.Ordinal) == true;

    /// <summary>
    /// Whether <paramref name="bytes"/> start like an MP3 file (an ID3v2 tag or an MPEG audio frame
    /// header), so the view can warn and stay silent instead of handing Godot a broken file.
    /// </summary>
    public static bool LooksLikeMp3(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4)
            return false;
        if (bytes[0] == (byte)'I' && bytes[1] == (byte)'D' && bytes[2] == (byte)'3')
            return bytes.Length > 10;
        // Frame sync (11 bits set), an MPEG version and layer that exist, a bitrate that is not "bad".
        return bytes[0] == 0xFF && (bytes[1] & 0xE0) == 0xE0 && (bytes[1] & 0x18) != 0x08
            && (bytes[1] & 0x06) != 0 && (bytes[2] & 0xF0) != 0xF0;
    }

    /// <summary>
    /// Whether <paramref name="bytes"/> start like a WAV file ("RIFF", a size, "WAVE"), so the view
    /// can warn and stay silent instead of handing Godot something else (a QuickTime/M4A file renamed .wav).
    /// </summary>
    public static bool LooksLikeWav(ReadOnlySpan<byte> bytes) =>
        bytes.Length > 12
        && bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'
        && bytes[8] == (byte)'W' && bytes[9] == (byte)'A' && bytes[10] == (byte)'V' && bytes[11] == (byte)'E';
}
