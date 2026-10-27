# Music module

Plays the game's music: each place has its own song (Tony, 2026-10-27), and only this module
plays songs, so two never play over each other.

| Where | Song | File (`Audio/`) |
| --- | --- | --- |
| Start screen | "Sea Sponge" | `sea_sponge.mp3` |
| Mushroom Town (and the house, at 70%) | "Snowman Snuggles" | `snowman_snuggles.mp3` |
| Mushroom Forest | "Popcorn Party" | `popcorn_party.mp3` |
| Deep Forest (and a boss fight, later; there is no boss yet) | "8 Bit Snack" | `eight_bit_snack.wav` |
| Pet City | "Darnt" | `darnt.mp3` |

- `Contract/` — `Fungtopia.Music.Contract` (namespace `Fungtopia.Music`): `Song` (`None`,
  `SeaSponge`, `SnowmanSnuggles`, `PopcornParty`, `EightBitSnack`, `Darnt`). Music publishes no
  messages.
- `Core/` — `Fungtopia.Music.Core`: `MusicModule`, the module's one `IModule` (which song, how
  loud); `SongFiles`, each song's file name and checks that bytes look like an MP3 or a WAV.
- `Godot/` — `Main.tscn` with `MusicView`, which plays the songs.
- `Audio/` — the five songs above. No `.gdignore` here: they are game files (but loaded from their
  bytes, see View).
- `Tests/` — `MusicModuleTests.cs`: xUnit tests of `MusicModule` with a fake `IMessageBus` (nothing
  before any state; Sea Sponge while the start screen is open, even with a player; each area's
  song; the road keeping the last place's song, or Mushroom Town's straight from the start screen;
  the house keeping Mushroom Town's song at 0.7; the 2 s crossfade tick by tick, old falling as new
  rises; turning back mid-crossfade; a third song mid-crossfade; never more than two songs audible)
  and of the files (each song's file in `Audio/`, the MP3s being MP3s, broken bytes refused).

Test it with `./test.sh Music` from the repository root.

## Core

`MusicModule` reads `Fungtopia.StartScreen.StartScreenState` and `Fungtopia.Player.PlayerState`
(the latest of each) and each tick picks a song and a target level:

- while `StartScreenState.Open`: `SeaSponge`, at 1, whatever the player's area;
- else, after the first `PlayerState`, by its `Area`: `Town` `SnowmanSnuggles`, `Forest`
  `PopcornParty`, `DeepForest` `EightBitSnack`, `PetCity` `Darnt`, each at 1; `Home` (the house's
  inside) `SnowmanSnuggles` at `HomeLevel` (`HomeTicks` 84 of 120, 0.7); `Road` (and any area added
  later) keeps the last of those places' song at its level, so nothing fades (Mushroom Town's at 1
  when no place came before, straight out of the start screen);
- else (no state yet, or between the start screen closing and the player's first state) whatever
  plays goes on.

Levels are whole ticks of `FadeTicks` (120, 2 s at 60 ticks a second), so they are exact. `Current`
is the song playing and `CurrentLevel` its level, which moves one tick toward its target each tick
(so the house's 0.7 is 36 ticks from full, the same song, no crossfade). A new song becomes
`Current` at 0 and rises by 1/120 a tick while the old one becomes `Previous` and `PreviousLevel`
falls by 1/120 a tick: a 2 s crossfade, after which `Previous` is `None`. Turned back mid-crossfade,
the two swap and go on from where they were. A third song mid-crossfade fades in under the louder
of the other two; the quieter stops. `LevelOf(song)` is a song's level (0 unless current or
previous). No allocations per tick.

## View

`MusicView` has one `AudioStreamPlayer` per song, each loaded in `_Ready` from the file's bytes
(`FileAccess.GetFileAsBytes`), so a fresh checkout plays them without an editor import: an MP3
into an `AudioStreamMP3` (`Data`, `Loop` on), the WAV through `AudioStreamWav.LoadFromBuffer`
(loop mode Forward over the whole file, no trimming); both restart seamlessly. Each frame a song
plays at `FullVolumeDb` (-8 dB, medium, as the town's song did) plus `LinearToDb(LevelOf(song))`;
at level 0 it is paused, and fading back in it resumes where it was. A missing file, or bytes that
are no MP3 (`LooksLikeMp3`) or no WAV (`LooksLikeWav`: "RIFF", size, "WAVE"), log one `WARNING`
and that song stays silent.

**Note (2026-10-27):** the committed `eight_bit_snack.wav` is not a WAV: it is AAC audio in a
QuickTime (`.mov`/`.m4a`) container, which Godot cannot play, so the Deep Forest stays silent
with a warning until it is replaced by a real WAV (PCM) or an MP3 export of the song under the same
name (an MP3 also needs `SongFiles.FileName` changed).
