# DayNight module

The game's clock and its light. One in-game day lasts 20 real minutes, starting Day 1 at 9:00;
night is 20:00-6:00. Fainting in a fight costs no coins: you wake up at Dr. Jeremy's clinic the
next morning. The view owns the one sun, sky and WorldEnvironment of the whole game.

- `Contract/` — `Fungtopia.DayNight.Contract` (namespace `Fungtopia.DayNight`), see below.
- `Core/` — `Fungtopia.DayNight.Core`: `DayNightModule`, the module's one `IModule`; `VolumetricFog`, the
  global volumetric fog settings; `Lighting` (with `Mood` and `Rgb`), the palette of the sun, sky and
  environment.
- `Godot/` — `Main.tscn` with `DayNightView` (light, sky, environment); `Shaders/sky.gdshader`.
- `Tests/` — `DayNightModuleTests.cs`: xUnit tests of `DayNightModule` (clock, faint and wake spot, save/restore) with a fake `IMessageBus`, of
  `VolumetricFog`, and of `Lighting`'s palette (hues, saturation and fog checked at every quarter hour).

Test it with `./test.sh DayNight` from the repository root.

## Contract

| Member | What it is |
| --- | --- |
| `TimeOfDay` | Published by DayNight once every tick: `Day` (from 1), `Hour` (float, 0 up to but never 24), `IsNight` (20:00-6:00). Constants `RealSecondsPerDay` (1200), `NightStartHour` (20), `NightEndHour` (6); `IsNightAt(hour)`. |
| `WokeUp` | Published once when the player wakes at the clinic after fainting: `Day`, the day woken on. |

## Core

`DayNightModule` reads `Fungtopia.Player.PlayerDefeated` and `PlayerState` (only its `Area`, for
the view) and `Fungtopia.Save.CollectSave` and `RestorePart`, and uses `Fungtopia.Map`'s `Places.ClinicDoor`, `Ground.Blocks` and `Areas.Walkable`. Each tick it adds the step to the clock
(50 real seconds per hour; the day counts up at midnight) and publishes `TimeOfDay`.

On `PlayerDefeated` it starts a faint (more defeats while fainting are ignored). After
`FaintSeconds` (3 s, while the screen fades) it skips to the next morning at `WakeHour` (7:00):
the next day, or the same day when the faint began before 7:00 (in the small hours the next
morning is that day's), then publishes, in this order, one `TeleportPlayer` to
`DayNightModule.WakeSpot` (position and yaw) and one `WokeUp`, and that tick's `TimeOfDay` already
shows 7:00. The Player module restores health on the teleport.

`WakeSpot` (found once) is beside Dr. Jeremy, who stands exactly at `Places.ClinicDoor`, not
inside him: `WakeOut` (2.5 m) further out from the clinic than ClinicDoor (along its facing) and
`WakeSide` (1.2 m) to its right, facing the clinic (ClinicDoor's yaw + pi), so the camera, which
snaps behind the player after a teleport, lands out on the street looking at the player, Jeremy
and the clinic. If that spot is not free (`Areas.Walkable` with `WakeClearance` 0.5 m, and not
`Ground.Blocks` for a 0.5 m walker), it tries the left side, then both sides again 0.5 m further
out each time (up to 3 m more); every candidate is at least 1.2 m from Jeremy.

Saving (`Fungtopia.Save`): on `CollectSave` it answers at once with one `SavePart` (Module
`"DayNight"`) whose Json is compact, e.g. `{"v":1,"day":3,"hour":14.25}` (`SaveVersion` 1; the hour
as a double, so a round trip is exact). On `RestorePart` for `"DayNight"` it sets exactly that day
and hour and drops a faint in progress, publishing nothing (no `WokeUp`, no teleport); the next
tick's `TimeOfDay` shows the restored time. Bad or empty Json, another version, day < 1 or an hour
outside 0..24 reset the clock to its defaults (Day 1, 9:00) and write one line to `Log` (the
console by default). `RestorePart`s for other modules are ignored.

For the view and tests: `Day`, `Hour` (double), `HourF` (as published), `IsNight`, `Fainting`,
`FaintProgress` (0..1 over the faint), `PlayerArea`, `Ticks`, `SetTime(day, hour)` and `Log`. No
allocations per tick.

## View

`DayNightView` builds a `WorldEnvironment` (sky shader, colored ambient, filmic tonemap, glow,
fog, and a color adjustment: saturation 1.25, contrast 1.05, brightness 1.02) and one shadowed
`DirectionalLight3D` named `Sun` in code, and each frame applies `Lighting.At(hour)`.

`Lighting` is the look of a cheerful cartoon: rich, saturated, never grey or washed out. Its
`Moods` are keyed by hour: deep blue-purple night (dim violet ambient, strong glow so glowing
mushrooms stand out, all stars), pink dawn (6:00), warm morning with a peach horizon (8:00), a
lilac step (9:30), bright day (11:00-16:00: a clear saturated blue sky, warm bright sunlight
at energy 1.2, soft periwinkle ambient, very thin blue haze), lilac again (17:18), golden evening
(18:36), pink-purple dusk (20:00), night again (21:24). Neighbours blend with a smoothstep and
`Rgb.VividLerp`, which keeps a blend as saturated as its ends, so no transition passes through
grey; the lilac steps make the horizon turn from peach to blue by way of lilac, not mint.

The sun rises in the east at 5:30, crosses the south (60° up at 13:00) and sets in the west at 20:30; then the light becomes the
moon, lavender-blue and dim, rising east, never more than 30° up, crossing the north sky (so it is in view looking north up the
roads) and setting at 5:30. The sky shader draws a toon gradient, a crisp sun disk with halo, a
big cream moon with soft mushroom-cap spots and a blue halo, and twinkling stars at night.

While the player is in the forest (`PlayerArea == Forest`, blended over 2 s) the fog thickens
into a yellow-green haze by day and a teal one by night, the ambient turns a little greener and
drops, and the glow rises: cap shadows dapple the ground more and glowing mushrooms stand out.

Volumetric fog is on in the environment (settings in `VolumetricFog`) with density 0, so open
areas look as without it and other modules can add local `FogVolume` nodes (the Deep Forest's
fog). It is kept cheap: 40 m long, a 48x48x32 froxel grid (set with
`RenderingServer.EnvironmentSetVolumetricFogVolumeSize`), no GI inject, ambient inject 0.5 so
shaded fog is not black. Volumetric fog needs the Forward+ renderer; the OpenGL fallback of
`./screenshot.sh` ignores it.

## Screenshots

`DAYNIGHT_HOUR=12 ./screenshot.sh` (any hour 0..24) starts the clock at that hour. The main scene
opens on the start screen, whose blurred town is its own world and does not show this module's
light, so look at the world through the Map's preview camera: for example
`MAP_PREVIEW=road DAYNIGHT_HOUR=12 ./screenshot.sh "" day.png` and
`MAP_PREVIEW=road DAYNIGHT_HOUR=22 ./screenshot.sh "" night.png` (18.6 for the golden evening,
20 for dusk).
