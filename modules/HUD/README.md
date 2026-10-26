# HUD module

The cute heads-up display drawn over the game: rounded mushroom-cap panels (a soft pink dome with
cream spots and an ink outline), soft colors and a bold friendly font.

- **Hearts** (top left): one heart per 2 health of `PlayerState.MaxHealth` (5 for 10), filled
  from the left in halves from `Health`; full hearts have big round eyes, empty ones sleep. When
  `Health` drops, the hearts panel shakes for 0.4 s (a fading 14 Hz wobble of up to 9 px).
- **Mushroom Coins** (top right): `WalletState.Coins` as "M¢ 1,250" next to a gold mushroom
  coin (a little spotted mushroom on it, with no face: only creatures have eyes). Each `CoinsEarned` pops the counter (a 0.35 s swell to 1.35×) and floats a gold "+50"
  that rises 70 px into it and fades out over 1.2 s (the latest 4 at a time).
- **Minimap** (top right, just below the Mushroom Coins panel, right-aligned with it): a cream
  rounded square about 220 px across (its longer side; the map inside is 204 px) with an ink
  outline, a soft shadow and a little pink spotted "N" cap on its top edge. It shows the whole
  current zone (`PlayerState.Area`: Town, Forest, PetCity, DeepForest), north (-Z) up, the zone's
  corners at the map's corners (so a 160 x 140 m town makes it a little wider than tall): dark moss
  off the walkable ground with a lighter hedge rim, the ground in a soft color (green grass,
  lighter forest clearings, lilac Pet City paving, the Deep Forest's darker moss under a dusky violet
  background), the roads, forest and town paths, Pet City's avenue and the overgrown Deep Forest path
  in golden dirt, the buildings as little filled footprints with an ink edge (your house pink with a
  cream heart, Millie's lilac with a flower, the clinic white with a red cross, the shops gold, the
  Pet Store mint with a paw, other houses rosy brown, city blocks blue, the fountain a blue disc), the
  signposts as little posts with golden boards and the NPC spots and doors as cream dots; and the player as
  a chubby pink arrow pointing their `Facing`, always on top. On the Road it shows a 120 m square
  around the player instead (scrolling the picture of the strip from Pet City to Mushroom Town: the
  road, the zone edges, signposts and buildings in it). Hidden in the Home interior, on the start
  screen and before any `PlayerState`; it fades out with the screen after fainting (hidden once black)
  and back in with it at the clinic.
- **Crosshair and bow draw ring** (center): a dot with a ring around it filling clockwise from the
  top with `BowState.Draw` (clamped to 0..1), pink turning gold, with a gold halo at full draw.
  It shows only while the bow is in use: it fades in (0.1 s) once `Draw` rises above 0, stays
  while drawing and for 0.5 s after `Draw` is back to 0 (so you see where the shot went), then
  fades out (0.1 s); hidden otherwise (walking, talking, menus).
- **Clock** (under the hearts): a cream pill with "Day 3  2:05 PM" from `TimeOfDay` (`Day`, the
  minute of `Hour`) and a badge: a smiling sun on sky blue by day, a sleepy crescent moon on navy
  while `IsNight`.
- **Quest tracker** (under the clock): Millie's active quest from `QuestState` as "Defeat 5
  Mushroom Caterpillars 3/5" (progress clamped to the goal) next to a little mushroom cap, which
  turns into a green check when `Complete`; it swells a little on progress and hides while no quest
  is `Active`.
- **Place-name banner** (top center): when the living player's `PlayerState.Area` becomes a new
  named place, a mushroom-cap panel with "Mushroom Town", "Mushroom Forest", "Pet City", "Home" or
  "Deep Forest" slides down (0.45 s), stays, and slides back up after 3 s. The Deep Forest's
  banner is spookier: a dusky violet cap with faint greenish spots, a dark band and misty lilac
  text. The roads have no name
  and no banner (Town → Road → Town shows nothing again); nothing slides in while fainted.
- **Fainting**: after `PlayerDefeated`, the screen fades (1.2 s) to black with "You fainted...".
  On DayNight's `WokeUp` that text gives way to "Day 4 - you wake up at Dr. Jeremy's" (in 0.5 s,
  shown 4.5 s in all, out over the last 1 s), and when `PlayerState.Alive` is true again (the
  clinic teleport) the screen comes back over 2 s under it.
- **Start screen**: the whole HUD stays hidden while `Fungtopia.StartScreen.StartScreenState.Open`
  is true, and shows once it is false (it shows when no `StartScreenState` has come).

- `Contract/` — `Fungtopia.HUD.Contract`: empty; the HUD publishes no messages of its own (only a
  `TeleportPlayer` for screenshots, see `HUD_MINIMAP_AT`).
- `Core/` — `Fungtopia.HUD.Core`: `HudModule`, the module's one `IModule`. It listens to
  `Fungtopia.Player.PlayerState` (`Area` too) and `PlayerDefeated`, `Fungtopia.Wallet.WalletState`
  and `CoinsEarned`, `Fungtopia.Bow.BowState`, `Fungtopia.DayNight.TimeOfDay` and `WokeUp`,
  `Fungtopia.NPCs.QuestState` (using `Fungtopia.Map`'s `Area`, `Areas`, `Buildings`, `Places` and
  `Props`, and `Fungtopia.Forest.ForestLayout` for the minimap) and `Fungtopia.StartScreen.StartScreenState`, runs the animations on the fixed ticks
  without allocating, and gives the view each value at a fraction between the last two ticks
  (`HeartsShake(f)`, `CoinsPopScale(f)`, `Floater(i).Rise(f)`, `DrawRing(f)`, `CrosshairAlpha(f)`, `FaintFade(f)`,
  `WakeTextAlpha(f)`, `BannerSlide(f)`, `QuestPopScale(f)`…), plus `Hidden`, `CrosshairVisible`, `BannerArea`/`BannerCount`/`BannerSpooky`,
  `Day`/`ClockMinute`/`IsNight`, `WakeDay` and the quest (`QuestActive`, `QuestTitle`,
  `QuestProgress`, `QuestGoal`, `QuestComplete`, `QuestVersion` counting its changes).
  The minimap: `Minimap` (a `MinimapPicture`, null at Home), `MinimapArea`, `MinimapVisible`,
  `MinimapAlpha(f)`, `MinimapWindow(f)` (the zone's bounds, or on the Road a `RoadWindowMeters`
  (120) square around the player), `MinimapPlayer(f)` (where the arrow sits, 0..1 each way in the
  window, (0, 0) its north-west corner) and `MinimapArrow(f)` (the arrow's rotation, radians
  clockwise from up: `MinimapMath.ArrowAngle(Facing)` = -Facing, wrapped); the player's position
  and facing glide between ticks and jump on a teleport (over `MinimapSnapMeters`, 8 m).
  `MinimapPicture.For(area)` builds a zone's picture once (cached) when it is first shown, as plain
  data: 1 m `MinimapCell`s (`Outside`, `Ground`, `Paving`, `Moss`, `Clearing`, `Path`) sampled from
  `Areas.Walkable`, `RoadDistance`, `TownPathDistance`, `Props.CityAvenueDistance`, `DeepForestPath`
  and the Forest Contract's `ForestLayout` paths and clearings (the Forest's thin overgrown trail is not
  in a Contract, so it is not drawn), row 0 the northmost; the `Buildings.All` footprints
  (`MinimapBuilding`, four world corners) and the signposts and NPC spots of `Places`
  (`MinimapMark`) in it; `ToPicture(x, z)` maps a world point to 0..1. Per tick only the
  position and facing are stored; nothing is allocated.
  For screenshots only, `TeleportPlayerOnce` publishes one `TeleportPlayer` and
  `ShowUnderStartScreen` keeps the HUD showing while the start screen is open.
  `HudText` says which places are spooky (`IsSpooky`) and makes the texts ("M¢ 1,250", "+50", "You fainted...", "Mushroom Town", "Day 3",
  "2:05 PM", "Day 4 - you wake up at Dr. Jeremy's", "Defeat 5 Mushroom Caterpillars 3/5").
- `Godot/` — `Main.tscn` with `HudView` (a `CanvasLayer` on layer 10), which builds the HUD from
  `MushroomPanel`, `HeartRow`, `CoinIcon`, `SkyIcon`, `QuestIcon`, `Crosshair` and `MinimapView`
  (all drawn in code) and sets texts only when they change. `MinimapView` draws the frame and clips
  the map to its rounded square (`MinimapMask`); `MinimapLayer` turns a picture into a texture and draws
  its buildings and markers once per zone; per frame only the layer (scrolling on the Road) and the
  `MinimapArrow` move; `Preview.tscn` shows it over a backdrop with
  scripted messages, for screenshots: `./screenshot.sh modules/HUD/Godot/Preview.tscn` (day, a
  banner, the quest at 3/5), or with `HUD_PREVIEW=night` (moon, quest complete), `fainted`,
  `woke` (the wake-up text as the screen comes back), `deep` (entering the Deep Forest), or `town`,
  `city` or `road` (the player, and the minimap, there). In the game, `HUD_MINIMAP_AT` = `town`,
  `forest`, `city`, `road` or `deep` (screenshots only) teleports the player there once and hides
  the start screen and the other screens over the world, for example
  `HUD_MINIMAP_AT=road DAYNIGHT_HOUR=12 ./screenshot.sh "" minimap_road.png`.
- `Tests/` — `HudModuleTests.cs`: xUnit tests of `HudModule` and `HudText` with a fake
  `IMessageBus`: hearts, coins, the draw ring and when the crosshair shows, the faint and wake-up, the banner (and the spooky
  Deep Forest one), the clock, the quest tracker, hiding under the start screen, determinism, and
  the minimap (the zone and road window it shows, where the player and the zone's corners land, the
  arrow's angle, the buildings, paths, signposts and spots of each picture, hiding indoors, on the
  start screen and after fainting).

The font is the system's first of Nunito, Fredoka, Baloo 2, Varela Round, Comic Neue, Quicksand,
at weight 800, falling back to DejaVu Sans Bold.

Test it with `./test.sh HUD` from the repository root.
