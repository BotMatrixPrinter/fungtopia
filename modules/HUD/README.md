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
- **Fainting**: after `PlayerDefeated`, the screen fades softly (1.2 s) to a dusky veil at 75 %
  with "You fainted..."; it fades back out when `PlayerState.Alive` is true again (the doctor
  and the next-morning wake-up come with the town later).

- `Contract/` — `Fungtopia.HUD.Contract`: empty; the HUD publishes no messages.
- `Core/` — `Fungtopia.HUD.Core`: `HudModule`, the module's one `IModule`. It listens to
  `Fungtopia.Player.PlayerState` and `PlayerDefeated`, `Fungtopia.Wallet.WalletState` and
  `CoinsEarned`, and `Fungtopia.Bow.BowState`, runs the animations on the fixed ticks without
  allocating, and gives the view each value at a fraction between the last two ticks
  (`HeartsShake(f)`, `CoinsPopScale(f)`, `Floater(i).Rise(f)`, `DrawRing(f)`, `FaintFade(f)`…).
  `HudText` makes the texts ("M¢ 1,250", "+50", "You fainted...").
- `Godot/` — `Main.tscn` with `HudView` (a `CanvasLayer` on layer 10), which builds the HUD from
  `MushroomPanel`, `HeartRow`, `CoinIcon` and `Crosshair` (all drawn in code) and sets texts only
  when they change; `Preview.tscn` shows it over a backdrop with scripted messages, for
  screenshots: `./screenshot.sh modules/HUD/Godot/Preview.tscn`, or the faint with
  `HUD_PREVIEW=fainted`.
- `Tests/` — `HudModuleTests.cs`: xUnit tests of `HudModule` and `HudText` with a fake
  `IMessageBus`.

The font is the system's first of Nunito, Fredoka, Baloo 2, Varela Round, Comic Neue, Quicksand,
at weight 800, falling back to DejaVu Sans Bold.

Test it with `./test.sh HUD` from the repository root.
