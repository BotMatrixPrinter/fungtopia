using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Fungtopia.Map;
using Fungtopia.Player;
using Fungtopia.Shell;
using Fungtopia.StartScreen;
using Xunit;

namespace Fungtopia.Music.Tests;

public class MusicModuleTests
{
    /// <summary>Fake of the shell's bus: delivers messages to subscribers like the real one.</summary>
    private sealed class FakeBus : IMessageBus
    {
        private readonly Dictionary<Type, List<object>> _handlers = new();

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
                _handlers[typeof(T)] = list = new List<object>();
            list.Add(handler);
        }

        public void Publish<T>(in T message) where T : struct
        {
            if (_handlers.TryGetValue(typeof(T), out var list))
                foreach (var handler in list)
                    ((IMessageHandler<T>)handler).Handle(message);
        }
    }

    private static readonly TickContext Step = new(0, 1.0 / 60);

    private static PlayerState In(Area area) =>
        new(Vector3.Zero, 0f, Vector3.Zero, true, 10, 10, true, CharacterKind.Girl, area);

    private static (FakeBus, MusicModule) Started()
    {
        var bus = new FakeBus();
        var music = new MusicModule();
        music.Start(bus);
        return (bus, music);
    }

    /// <summary>One tick of the game after the start screen: the start screen closed, the player in <paramref name="area"/>.</summary>
    private static void Tick(FakeBus bus, MusicModule music, Area area)
    {
        bus.Publish(new StartScreenState(false));
        bus.Publish(In(area));
        music.Tick(Step);
    }

    private static void Walk(FakeBus bus, MusicModule music, Area area, int ticks)
    {
        for (int i = 0; i < ticks; i++)
            Tick(bus, music, area);
    }

    // ---- Which song ----

    [Fact]
    public void NothingPlaysBeforeAnyState()
    {
        var (_, music) = Started();
        for (int i = 0; i < 300; i++)
            music.Tick(Step);
        Assert.Equal(Song.None, music.Current);
        Assert.Equal(Song.None, music.Previous);
        Assert.Equal(0f, music.CurrentLevel);
        foreach (var song in SongFiles.All)
            Assert.Equal(0f, music.LevelOf(song));
    }

    [Fact]
    public void TheStartScreenPlaysSeaSpongeRisingOverTwoSeconds()
    {
        var (bus, music) = Started();
        Assert.Equal(120, MusicModule.FadeTicks);
        var levels = new float[240];
        for (int i = 0; i < levels.Length; i++)
        {
            bus.Publish(new StartScreenState(true));
            music.Tick(Step);
            Assert.Equal(Song.SeaSponge, music.Current);
            Assert.Equal(Song.None, music.Previous);
            levels[i] = music.CurrentLevel;
        }
        Assert.Equal(1f / 120, levels[0], 5);
        Assert.Equal(0.5f, levels[59], 5);
        Assert.True(levels[118] < 1f);
        Assert.Equal(1f, levels[119]);
        Assert.Equal(1f, levels[239]);
        Assert.Equal(1f, music.LevelOf(Song.SeaSponge));
    }

    [Fact]
    public void WhileTheStartScreenIsOpenItsSongWinsOverThePlayersArea()
    {
        var (bus, music) = Started();
        for (int i = 0; i < 200; i++)
        {
            bus.Publish(In(Area.Forest));
            bus.Publish(new StartScreenState(true));
            music.Tick(Step);
        }
        Assert.Equal(Song.SeaSponge, music.Current);
        Assert.Equal(1f, music.CurrentLevel);
        Assert.Equal(0f, music.LevelOf(Song.PopcornParty));
    }

    [Theory]
    [InlineData(Area.Town, Song.SnowmanSnuggles)]
    [InlineData(Area.Forest, Song.PopcornParty)]
    [InlineData(Area.DeepForest, Song.EightBitSnack)]
    [InlineData(Area.PetCity, Song.Darnt)]
    public void EachPlaceHasItsOwnSongAtFullLevel(Area area, Song song)
    {
        var (bus, music) = Started();
        Walk(bus, music, area, 200);
        Assert.Equal(song, music.Current);
        Assert.Equal(1f, music.CurrentLevel);
        Assert.Equal(Song.None, music.Previous);
        foreach (var other in SongFiles.All)
            Assert.Equal(other == song ? 1f : 0f, music.LevelOf(other));
    }

    [Theory]
    [InlineData(Area.Town, Song.SnowmanSnuggles)]
    [InlineData(Area.Forest, Song.PopcornParty)]
    [InlineData(Area.DeepForest, Song.EightBitSnack)]
    [InlineData(Area.PetCity, Song.Darnt)]
    public void TheRoadKeepsTheLastPlacesSongFadingNothing(Area area, Song song)
    {
        var (bus, music) = Started();
        Walk(bus, music, area, 200);
        for (int i = 0; i < 600; i++)
        {
            Tick(bus, music, Area.Road);
            Assert.Equal(song, music.Current);
            Assert.Equal(1f, music.CurrentLevel);
            Assert.Equal(Song.None, music.Previous);
        }
    }

    [Fact]
    public void FromTheRoadIntoTheNextPlaceItsSongCrossfadesIn()
    {
        var (bus, music) = Started();
        Walk(bus, music, Area.Town, 200);
        Walk(bus, music, Area.Road, 300);
        Walk(bus, music, Area.Forest, 60);
        Assert.Equal(Song.PopcornParty, music.Current);
        Assert.Equal(Song.SnowmanSnuggles, music.Previous);
        Assert.Equal(0.5f, music.CurrentLevel, 5);
        Assert.Equal(0.5f, music.PreviousLevel, 5);
        // Back onto the road: the forest's song now, still rising.
        Walk(bus, music, Area.Road, 60);
        Assert.Equal(Song.PopcornParty, music.Current);
        Assert.Equal(1f, music.CurrentLevel);
        Assert.Equal(0f, music.LevelOf(Song.SnowmanSnuggles));
    }

    [Fact]
    public void OnTheRoadStraightFromTheStartScreenMushroomTownsSongPlays()
    {
        var (bus, music) = Started();
        for (int i = 0; i < 200; i++)
        {
            bus.Publish(new StartScreenState(true));
            music.Tick(Step);
        }
        Walk(bus, music, Area.Road, 200);
        Assert.Equal(Song.SnowmanSnuggles, music.Current);
        Assert.Equal(1f, music.CurrentLevel);
        Assert.Equal(0f, music.LevelOf(Song.SeaSponge));
    }

    [Fact]
    public void InTheHouseMushroomTownsSongKeepsPlayingAtSeventyPercent()
    {
        var (bus, music) = Started();
        Assert.Equal(0.7f, MusicModule.HomeLevel, 5);
        Walk(bus, music, Area.Town, 200);

        var levels = new float[120];
        for (int i = 0; i < levels.Length; i++)
        {
            Tick(bus, music, Area.Home);
            // The same song all along: nothing crossfades, it only gets quieter.
            Assert.Equal(Song.SnowmanSnuggles, music.Current);
            Assert.Equal(Song.None, music.Previous);
            levels[i] = music.CurrentLevel;
        }
        Assert.Equal(119f / 120, levels[0], 5);
        for (int i = 1; i < 36; i++)
            Assert.True(levels[i] < levels[i - 1], $"tick {i}");
        Assert.Equal(0.7f, levels[35], 5);
        for (int i = 36; i < levels.Length; i++)
            Assert.Equal(0.7f, levels[i], 5);

        // Out of the door: back up to full, still the same song.
        Walk(bus, music, Area.Town, 36);
        Assert.Equal(Song.SnowmanSnuggles, music.Current);
        Assert.Equal(1f, music.CurrentLevel);
        Assert.Equal(Song.None, music.Previous);
    }

    [Fact]
    public void LoadedIntoTheHouseMushroomTownsSongRisesToSeventyPercent()
    {
        var (bus, music) = Started();
        Walk(bus, music, Area.Home, 200);
        Assert.Equal(Song.SnowmanSnuggles, music.Current);
        Assert.Equal(0.7f, music.CurrentLevel, 5);
    }

    // ---- The crossfade ----

    [Fact]
    public void ChangingSongCrossfadesOverTwoSecondsTheOldFallingAsTheNewRises()
    {
        var (bus, music) = Started();
        for (int i = 0; i < 200; i++)
        {
            bus.Publish(new StartScreenState(true));
            music.Tick(Step);
        }

        var rising = new float[121];
        var falling = new float[121];
        for (int i = 0; i < rising.Length; i++)
        {
            Tick(bus, music, Area.Town);
            Assert.Equal(Song.SnowmanSnuggles, music.Current);
            rising[i] = music.LevelOf(Song.SnowmanSnuggles);
            falling[i] = music.LevelOf(Song.SeaSponge);
            if (i < 119)
            {
                Assert.Equal(Song.SeaSponge, music.Previous);
                Assert.Equal(music.PreviousLevel, falling[i]);
            }
        }
        for (int i = 1; i < 120; i++)
        {
            Assert.True(rising[i] > rising[i - 1], $"tick {i}: {rising[i]} not above {rising[i - 1]}");
            Assert.True(falling[i] < falling[i - 1], $"tick {i}: {falling[i]} not below {falling[i - 1]}");
        }
        Assert.Equal(1f / 120, rising[0], 5);
        Assert.Equal(119f / 120, falling[0], 5);
        Assert.Equal(0.5f, rising[59], 5); // 1 s: halfway
        Assert.Equal(0.5f, falling[59], 5);
        Assert.True(rising[118] < 1f);
        Assert.True(falling[118] > 0f);
        Assert.Equal(1f, rising[119]); // 2 s
        Assert.Equal(0f, falling[119]);
        Assert.Equal(Song.None, music.Previous);
        Assert.Equal(1f, rising[120]);
        Assert.Equal(0f, falling[120]);
    }

    [Fact]
    public void TurningBackMidCrossfadeGoesBackFromWhereItWas()
    {
        var (bus, music) = Started();
        Walk(bus, music, Area.Forest, 200);
        Walk(bus, music, Area.DeepForest, 30); // 0.25 in, 0.75 out
        Assert.Equal(Song.EightBitSnack, music.Current);
        Assert.Equal(0.25f, music.CurrentLevel, 5);
        Assert.Equal(0.75f, music.PreviousLevel, 5);

        Tick(bus, music, Area.Forest);
        Assert.Equal(Song.PopcornParty, music.Current);
        Assert.Equal(Song.EightBitSnack, music.Previous);
        Assert.Equal(91f / 120, music.CurrentLevel, 5);
        Assert.Equal(29f / 120, music.PreviousLevel, 5);
        Walk(bus, music, Area.Forest, 28);
        Assert.Equal(119f / 120, music.CurrentLevel, 5);
        Assert.Equal(1f / 120, music.PreviousLevel, 5);
        Tick(bus, music, Area.Forest);
        Assert.Equal(1f, music.CurrentLevel);
        Assert.Equal(Song.None, music.Previous);
        Assert.Equal(0f, music.LevelOf(Song.EightBitSnack));
    }

    [Fact]
    public void AThirdSongMidCrossfadeFadesInUnderTheLouderOldOne()
    {
        var (bus, music) = Started();
        Walk(bus, music, Area.Town, 200);
        Walk(bus, music, Area.Forest, 30); // forest 0.25, town 0.75
        Walk(bus, music, Area.PetCity, 1);
        Assert.Equal(Song.Darnt, music.Current);
        Assert.Equal(Song.SnowmanSnuggles, music.Previous);
        Assert.Equal(1f / 120, music.CurrentLevel, 5);
        Assert.Equal(89f / 120, music.PreviousLevel, 5);
        Assert.Equal(0f, music.LevelOf(Song.PopcornParty));
        Walk(bus, music, Area.PetCity, 119);
        Assert.Equal(1f, music.CurrentLevel);
        Assert.Equal(Song.None, music.Previous);
    }

    [Fact]
    public void AtMostTwoSongsAreEverAudible()
    {
        var (bus, music) = Started();
        var areas = new[] { Area.Town, Area.Road, Area.Forest, Area.DeepForest, Area.Home, Area.PetCity };
        var rng = new Random(933);
        for (int i = 0; i < 5000; i++)
        {
            Tick(bus, music, areas[rng.Next(areas.Length)]);
            int audible = 0;
            foreach (var song in SongFiles.All)
            {
                float level = music.LevelOf(song);
                Assert.InRange(level, 0f, 1f);
                if (level > 0f)
                    audible++;
            }
            Assert.True(audible <= 2, $"tick {i}: {audible} songs audible");
        }
    }

    // ---- The files ----

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    [Fact]
    public void EverySongHasItsFileInTheAudioFolder()
    {
        string folder = Path.Combine(RepoRoot(), "modules", "Music", "Audio");
        Assert.Equal(
            new[] { "darnt.mp3", "eight_bit_snack.wav", "popcorn_party.mp3", "sea_sponge.mp3", "snowman_snuggles.mp3" },
            Array.ConvertAll(Directory.GetFiles(folder), f => Path.GetFileName(f)!).AsSortedCopy());
        Assert.False(File.Exists(Path.Combine(folder, ".gdignore")));
        foreach (var song in SongFiles.All)
        {
            string path = Path.Combine(folder, SongFiles.FileName(song)!);
            Assert.True(File.Exists(path), path);
            if (!SongFiles.IsWav(song))
                Assert.True(SongFiles.LooksLikeMp3(File.ReadAllBytes(path)), path);
        }
        Assert.True(SongFiles.IsWav(Song.EightBitSnack));
        Assert.Null(SongFiles.FileName(Song.None));
    }

    [Fact]
    public void BrokenBytesAreRejected()
    {
        // A bare MPEG-1 Layer III frame header.
        Assert.True(SongFiles.LooksLikeMp3(new byte[] { 0xFF, 0xFB, 0x90, 0x64 }));
        Assert.False(SongFiles.LooksLikeMp3(Array.Empty<byte>()));
        Assert.False(SongFiles.LooksLikeMp3(new byte[] { (byte)'I', (byte)'D', (byte)'3' }));
        Assert.False(SongFiles.LooksLikeMp3(Encoding.ASCII.GetBytes("<html>not found</html>")));
        Assert.False(SongFiles.LooksLikeMp3(Encoding.ASCII.GetBytes("version https://git-lfs")));
        Assert.False(SongFiles.LooksLikeMp3(new byte[] { 0xFF, 0xF1, 0x50, 0x80 })); // AAC (ADTS), layer 0

        var wav = new byte[44];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);
        Encoding.ASCII.GetBytes("WAVEfmt ").CopyTo(wav, 8);
        Assert.True(SongFiles.LooksLikeWav(wav));
        Assert.False(SongFiles.LooksLikeWav(Array.Empty<byte>()));
        Assert.False(SongFiles.LooksLikeWav(Encoding.ASCII.GetBytes("RIFF")));
        Assert.False(SongFiles.LooksLikeWav(Encoding.ASCII.GetBytes("RIFF\0\0\0\0AVI LIST")));
        // A QuickTime/M4A file renamed .wav.
        Assert.False(SongFiles.LooksLikeWav(new byte[] { 0, 0, 0, 0x14, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'q', (byte)'t', 0x20, 0x20, 0 }));
    }
}

internal static class SortExtensions
{
    public static string[] AsSortedCopy(this string[] items)
    {
        var copy = (string[])items.Clone();
        Array.Sort(copy, StringComparer.Ordinal);
        return copy;
    }
}
