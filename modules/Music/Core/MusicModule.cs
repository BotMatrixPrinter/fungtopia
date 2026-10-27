using System;
using Fungtopia.Map;
using Fungtopia.Player;
using Fungtopia.Shell;
using Fungtopia.StartScreen;

namespace Fungtopia.Music;

/// <summary>
/// Picks the song for where the player is and crossfades between songs. While
/// <see cref="StartScreenState.Open"/> it is <see cref="Song.SeaSponge"/>; after that it follows the
/// last <see cref="PlayerState.Area"/>: each place its own song, the Road keeping the last place's,
/// the house (<see cref="Area.Home"/>) Mushroom Town's at <see cref="HomeLevel"/>. A new song rises
/// from 0 while the old one falls to 0, both over <see cref="FadeTicks"/>. Levels are counted in
/// whole ticks, so they are exact; nothing allocates per tick.
/// </summary>
public sealed class MusicModule : IModule, IMessageHandler<StartScreenState>, IMessageHandler<PlayerState>
{
    /// <summary>Ticks a song takes to fade fully in or out: 2 s at 60 ticks a second.</summary>
    public const int FadeTicks = 120;

    /// <summary>Mushroom Town's song in the house: 84 of <see cref="FadeTicks"/>, 70% of full.</summary>
    public const int HomeTicks = 84;

    /// <summary>The level of Mushroom Town's song in the house (0.7).</summary>
    public const float HomeLevel = HomeTicks / (float)FadeTicks;

    private IMessageBus? _bus;
    private bool _startOpen;
    private bool _hasPlayer;
    private Area _area;
    private Song _placeSong;
    private int _placeTarget;

    private int _currentTicks;
    private int _targetTicks;
    private int _previousTicks;

    /// <summary>The song playing (or fading in); <see cref="Song.None"/> before the first one.</summary>
    public Song Current { get; private set; }

    /// <summary>The song fading out under <see cref="Current"/>; <see cref="Song.None"/> once it is silent.</summary>
    public Song Previous { get; private set; }

    /// <summary>How loud <see cref="Current"/> plays, 0..1.</summary>
    public float CurrentLevel => _currentTicks / (float)FadeTicks;

    /// <summary>How loud <see cref="Previous"/> plays, 0..1.</summary>
    public float PreviousLevel => _previousTicks / (float)FadeTicks;

    /// <summary>How loud <paramref name="song"/> plays, 0..1 (0 for any song neither current nor previous).</summary>
    public float LevelOf(Song song)
    {
        if (song == Song.None)
            return 0f;
        if (song == Current)
            return CurrentLevel;
        return song == Previous ? PreviousLevel : 0f;
    }

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        bus.Subscribe<StartScreenState>(this);
        bus.Subscribe<PlayerState>(this);
    }

    public void Handle(in StartScreenState message) => _startOpen = message.Open;

    public void Handle(in PlayerState message)
    {
        _hasPlayer = true;
        _area = message.Area;
    }

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("MusicModule.Tick before Start.");
        if (_startOpen)
            Play(Song.SeaSponge, FadeTicks);
        else if (_hasPlayer)
            PlayFor(_area);
        // Neither (before the start screen's first state, or between it closing and the player's
        // first state): whatever plays goes on.

        if (_currentTicks < _targetTicks)
            _currentTicks++;
        else if (_currentTicks > _targetTicks)
            _currentTicks--;
        if (_previousTicks > 0)
            _previousTicks--;
        if (_previousTicks == 0)
            Previous = Song.None;
    }

    private void PlayFor(Area area)
    {
        switch (area)
        {
            case Area.Town:
                Place(Song.SnowmanSnuggles, FadeTicks);
                break;
            case Area.Home:
                Place(Song.SnowmanSnuggles, HomeTicks);
                break;
            case Area.Forest:
                Place(Song.PopcornParty, FadeTicks);
                break;
            case Area.DeepForest:
                Place(Song.EightBitSnack, FadeTicks);
                break;
            case Area.PetCity:
                Place(Song.Darnt, FadeTicks);
                break;
            default:
                // The Road (or an area added later): the last place's song, as loud as it was there.
                // On the road before any place (just out of the start screen), Mushroom Town's.
                if (_placeSong == Song.None)
                    Place(Song.SnowmanSnuggles, FadeTicks);
                else
                    Play(_placeSong, _placeTarget);
                break;
        }
    }

    private void Place(Song song, int target)
    {
        _placeSong = song;
        _placeTarget = target;
        Play(song, target);
    }

    /// <summary>Makes <paramref name="song"/> the current song, heading for <paramref name="target"/> ticks of level.</summary>
    private void Play(Song song, int target)
    {
        _targetTicks = target;
        if (song == Current)
            return;
        if (song == Previous)
        {
            // Turned back mid-crossfade: the two swap and go on from where they were.
            (_currentTicks, _previousTicks) = (_previousTicks, _currentTicks);
            Previous = Current;
            Current = song;
            return;
        }
        // A new song. The louder of the two playing fades out under it; the other one (already
        // mostly faded) stops.
        if (Current != Song.None && _currentTicks >= _previousTicks)
        {
            Previous = Current;
            _previousTicks = _currentTicks;
        }
        Current = song;
        _currentTicks = 0;
    }
}
