using Fungtopia.Shell;
using Godot;

namespace Fungtopia.Music.View;

/// <summary>
/// Plays the songs: one <see cref="AudioStreamPlayer"/> per song, each at its level from
/// <see cref="MusicModule.LevelOf"/>; a song at level 0 is paused and resumes where it stopped.
/// The files are loaded at run time from their bytes, so a fresh checkout plays them without the
/// editor having imported them first.
/// </summary>
public partial class MusicView : Node, IModuleView
{
    public const string Folder = "res://modules/Music/Audio/";

    /// <summary>The volume at level 1: medium, under the other sounds.</summary>
    public const float FullVolumeDb = -8f;

    private MusicModule? _music;
    private readonly AudioStreamPlayer?[] _players = new AudioStreamPlayer?[SongFiles.All.Length];
    private readonly bool[] _started = new bool[SongFiles.All.Length];

    public void Bind(IModule module) => _music = (MusicModule)module;

    public override void _Ready()
    {
        for (int i = 0; i < SongFiles.All.Length; i++)
        {
            Song song = SongFiles.All[i];
            AudioStream? stream = Load(song);
            if (stream == null)
                continue;
            _players[i] = new AudioStreamPlayer { Name = song.ToString(), Stream = stream, VolumeDb = FullVolumeDb };
            AddChild(_players[i]);
        }
    }

    public override void _Process(double delta)
    {
        for (int i = 0; i < _players.Length; i++)
        {
            var player = _players[i];
            if (player == null)
                continue;
            float level = _music?.LevelOf(SongFiles.All[i]) ?? 0f;
            if (level <= 0f)
            {
                player.StreamPaused = true;
                continue;
            }
            player.VolumeDb = FullVolumeDb + Mathf.LinearToDb(level);
            if (!_started[i])
            {
                _started[i] = true;
                player.Play();
            }
            player.StreamPaused = false;
        }
    }

    /// <summary>The song's file as a seamlessly looping stream, or null (after one warning) when it is missing or unreadable.</summary>
    private static AudioStream? Load(Song song)
    {
        string path = Folder + SongFiles.FileName(song);
        byte[] bytes = FileAccess.GetFileAsBytes(path);
        if (bytes.Length == 0)
        {
            GD.PushWarning($"Fungtopia: Music: {path} is missing; {song} stays silent.");
            return null;
        }
        // Godot logs an ERROR for bytes that are not what it is told, so check them first.
        if (SongFiles.IsWav(song))
        {
            if (!SongFiles.LooksLikeWav(bytes))
            {
                GD.PushWarning($"Fungtopia: Music: {path} is not a WAV file; {song} stays silent.");
                return null;
            }
            var options = new Godot.Collections.Dictionary
            {
                // ResourceImporterWav's options: loop mode 2 is Forward, the whole file (end -1), no trimming.
                ["edit/loop_mode"] = 2,
                ["edit/loop_begin"] = 0,
                ["edit/loop_end"] = -1,
                ["edit/trim"] = false,
            };
            var wav = AudioStreamWav.LoadFromBuffer(bytes, options);
            if (wav == null)
                GD.PushWarning($"Fungtopia: Music: {path} could not be read; {song} stays silent.");
            return wav;
        }
        if (!SongFiles.LooksLikeMp3(bytes))
        {
            GD.PushWarning($"Fungtopia: Music: {path} is not an MP3; {song} stays silent.");
            return null;
        }
        return new AudioStreamMP3 { Data = bytes, Loop = true };
    }
}
