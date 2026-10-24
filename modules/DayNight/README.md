# DayNight module

The game's clock and its light. One in-game day lasts 20 real minutes, starting Day 1 at 9:00;
night is 20:00-6:00. Fainting in a fight costs no coins: you wake up at Dr. Jeremy's clinic the
next morning. The view owns the one sun, sky and WorldEnvironment of the whole game.

- `Contract/` — `Fungtopia.DayNight.Contract` (namespace `Fungtopia.DayNight`), see below.
- `Core/` — `Fungtopia.DayNight.Core`: `DayNightModule`, the module's one `IModule`; `VolumetricFog`, the
  global volumetric fog settings.
- `Godot/` — `Main.tscn` with `DayNightView` (light, sky, environment); `Shaders/sky.gdshader`.
- `Tests/` — `DayNightModuleTests.cs`: xUnit tests of `DayNightModule` with a fake `IMessageBus`, and of
  `VolumetricFog`.

Test it with `./test.sh DayNight` from the repository root.

## Contract

| Member | What it is |
| --- | --- |
| `TimeOfDay` | Published by DayNight once every tick: `Day` (from 1), `Hour` (float, 0 up to but never 24), `IsNight` (20:00-6:00). Constants `RealSecondsPerDay` (1200), `NightStartHour` (20), `NightEndHour` (6); `IsNightAt(hour)`. |
| `WokeUp` | Published once when the player wakes at the clinic after fainting: `Day`, the day woken on. |

## Core

`DayNightModule` reads `Fungtopia.Player.PlayerDefeated` and `PlayerState` (only its `Area`, for
the view) and uses `Fungtopia.Map.Places.ClinicDoor`. Each tick it adds the step to the clock
(50 real seconds per hour; the day counts up at midnight) and publishes `TimeOfDay`.

On `PlayerDefeated` it starts a faint (more defeats while fainting are ignored). After
`FaintSeconds` (3 s, while the screen fades) it skips to the next morning at `WakeHour` (7:00):
the next day, or the same day when the faint began before 7:00 (in the small hours the next
morning is that day's), then publishes, in this order, one `TeleportPlayer` to
`Places.ClinicDoor` (position and yaw) and one `WokeUp`, and that tick's `TimeOfDay` already
shows 7:00. The Player module restores health on the teleport.

For the view and tests: `Day`, `Hour` (double), `HourF` (as published), `IsNight`, `Fainting`,
`FaintProgress` (0..1 over the faint), `PlayerArea`, `Ticks`, and `SetTime(day, hour)`. No
allocations per tick.

## View

`DayNightView` builds a `WorldEnvironment` (sky shader, colored ambient, filmic tonemap, glow,
fog) and one shadowed `DirectionalLight3D` named `Sun` in code, and each frame blends between
moods keyed by hour: blue night, pink dawn (6:00), warm morning (8:00), bright day (11:00-16:00),
golden evening (18:36), purple dusk (20:00), night again (21:24). The sun rises in the east at
5:30, crosses the south (60° up at 13:00) and sets in the west at 20:30; then the light becomes the
moon, blue and dim, rising east, never more than 30° up, crossing the north sky (so it is in view looking north up the
roads) and setting at 5:30. The sky shader draws a toon gradient, a crisp sun disk with halo, a
big cream moon with soft mushroom-cap spots and a blue halo, and twinkling stars at night.

While the player is in the forest (`PlayerArea == Forest`, blended over 2 s) the fog thickens
into a green-gold haze by day and a teal one by night, the ambient drops and the glow rises:
cap shadows dapple the ground more and glowing mushrooms stand out.

Volumetric fog is on in the environment (settings in `VolumetricFog`) with density 0, so open
areas look as without it and other modules can add local `FogVolume` nodes (the Deep Forest's
fog). It is kept cheap: 40 m long, a 48x48x32 froxel grid (set with
`RenderingServer.EnvironmentSetVolumetricFogVolumeSize`), no GI inject, ambient inject 0.5 so
shaded fog is not black. Volumetric fog needs the Forward+ renderer; the OpenGL renderer that
`./screenshot.sh` uses ignores it.

## Screenshots

`DAYNIGHT_HOUR=12 ./screenshot.sh` (any hour 0..24) starts the clock at that hour, for example
`DAYNIGHT_HOUR=23 ./screenshot.sh "" night.png`.
