# HUD module

The cute heads-up display drawn over the game: rounded mushroom-cap panels (a soft pink dome with
cream spots and an ink outline), soft colors and a bold friendly font.

- **Hearts** (top left): one heart per 2 health of `PlayerState.MaxHealth` (5 for 10), filled
  from the left in halves from `Health`; full hearts have big round eyes, empty ones sleep. When
  `Health` drops, the hearts panel shakes for 0.4 s (a fading 14 Hz wobble of up to 9 px).
- **Mushroom Coins** (top right): `WalletState.Coins` as "M¢ 1,250" next to a gold mushroom
  coin. Each `CoinsEarned` pops the counter (a 0.35 s swell to 1.35×) and floats a gold "+50"
  that rises 70 px into it and fades out over 1.2 s (the latest 4 at a time).
- **Bow draw ring** (center): a ring around the crosshair dot filling clockwise from the top with
  `BowState.Draw` (clamped to 0..1), pink turning gold, with a gold halo at full draw; only the
  dot shows while the bow is relaxed.
- **Clock** (under the hearts): a cream pill with "Day 3  2:05 PM" from `TimeOfDay` (`Day`, the
  minute of `Hour`) and a badge: a smiling sun on sky blue by day, a sleepy crescent moon on navy
  while `IsNight`.
- **Quest tracker** (under the clock): Millie's active quest from `QuestState` as "Defeat 5
  Mushroom Caterpillars 3/5" (progress clamped to the goal) next to a little mushroom cap, which
  turns into a green check when `Complete`; it swells a little on progress and hides while no quest
  is `Active`.
- **Place-name banner** (top center): when the living player's `PlayerState.Area` becomes a new
  named place, a mushroom-cap panel with "Mushroom Town", "Mushroom Forest", "Pet City", "Home" or
  "Deep Forest" slides down (0.45 s), stays, and slides back up after 3 s. The roads have no name
  and no banner (Town → Road → Town shows nothing again); nothing slides in while fainted.
- **Fainting**: after `PlayerDefeated`, the screen fades (1.2 s) to black with "You fainted...".
  On DayNight's `WokeUp` that text gives way to "Day 4 - you wake up at Dr. Jeremy's" (in 0.5 s,
  shown 4.5 s in all, out over the last 1 s), and when `PlayerState.Alive` is true again (the
  clinic teleport) the screen comes back over 2 s under it.

- `Contract/` — `Fungtopia.HUD.Contract`: empty; the HUD publishes no messages.
- `Core/` — `Fungtopia.HUD.Core`: `HudModule`, the module's one `IModule`. It listens to
  `Fungtopia.Player.PlayerState` (`Area` too) and `PlayerDefeated`, `Fungtopia.Wallet.WalletState`
  and `CoinsEarned`, `Fungtopia.Bow.BowState`, `Fungtopia.DayNight.TimeOfDay` and `WokeUp`, and
  `Fungtopia.NPCs.QuestState` (using `Fungtopia.Map.Area`), runs the animations on the fixed ticks
  without allocating, and gives the view each value at a fraction between the last two ticks
  (`HeartsShake(f)`, `CoinsPopScale(f)`, `Floater(i).Rise(f)`, `DrawRing(f)`, `FaintFade(f)`,
  `WakeTextAlpha(f)`, `BannerSlide(f)`, `QuestPopScale(f)`…), plus `BannerArea`/`BannerCount`,
  `Day`/`ClockMinute`/`IsNight`, `WakeDay` and the quest (`QuestActive`, `QuestTitle`,
  `QuestProgress`, `QuestGoal`, `QuestComplete`, `QuestVersion` counting its changes).
  `HudText` makes the texts ("M¢ 1,250", "+50", "You fainted...", "Mushroom Town", "Day 3",
  "2:05 PM", "Day 4 - you wake up at Dr. Jeremy's", "Defeat 5 Mushroom Caterpillars 3/5").
- `Godot/` — `Main.tscn` with `HudView` (a `CanvasLayer` on layer 10), which builds the HUD from
  `MushroomPanel`, `HeartRow`, `CoinIcon`, `SkyIcon`, `QuestIcon` and `Crosshair` (all drawn in
  code) and sets texts only when they change; `Preview.tscn` shows it over a backdrop with
  scripted messages, for screenshots: `./screenshot.sh modules/HUD/Godot/Preview.tscn` (day, a
  banner, the quest at 3/5), or with `HUD_PREVIEW=night` (moon, quest complete), `fainted` or
  `woke` (the wake-up text as the screen comes back).
- `Tests/` — `HudModuleTests.cs`: xUnit tests of `HudModule` and `HudText` with a fake
  `IMessageBus`: hearts, coins, the draw ring, the faint and wake-up, the banner, the clock, the
  quest tracker, determinism and no allocations per tick.

The font is the system's first of Nunito, Fredoka, Baloo 2, Varela Round, Comic Neue, Quicksand,
at weight 800, falling back to DejaVu Sans Bold.

Test it with `./test.sh HUD` from the repository root.
