# Save module

Save games: three slots (one per player), each with its own manual save and its own separate
autosave, kept as JSON files in Godot's `user://saves/`. It gathers every module's saved state
over the bus, writes it atomically, reads it back and hands each module its part. It also owns
the in-game pause menu (Esc / Start): Resume, Save, Load, Quit to Title.

- `Contract/` — `Fungtopia.Save.Contract` (namespace `Fungtopia.Save`), see below. It references
  the Map and Player Contracts (for `Area` and `CharacterKind`).
- `Core/` — `Fungtopia.Save.Core`: `SaveModule`, the module's one `IModule`; `SaveFiles` (the
  files of one folder: paths, atomic write, read, delete, the JSON format) and `SaveData` (one
  file). It reads the Input (`UiFocus`), StartScreen (`GameStart`, `StartScreenState`), Player
  (`PlayerState`), Wallet (`WalletState`) and DayNight (`TimeOfDay`) Contracts.
- `Godot/` — `Main.tscn` with `SaveView` (a `CanvasLayer` on layer 60, over the HUD and the
  start screen), drawn by `PauseMenu` and `SaveToast`; `Preview.tscn` (`SavePreview`, for
  screenshots).
- `Tests/` — `SaveModuleTests.cs`: xUnit tests of `SaveModule` and `SaveFiles` with a fake
  synchronous `IMessageBus` and fake saving modules, on a temp folder.

Test it with `./test.sh Save` from the repository root.

## Contract

| Message | Who publishes it | What it is |
| --- | --- | --- |
| `CollectSave` | Save, when it saves | `RequestId`. Every module with saved state answers at once (the bus is synchronous) with one `SavePart`. |
| `SavePart` | each module with saved state, in answer to `CollectSave` | `RequestId` (the one answered), `Module` (a `SaveModules` name), `Json` (its whole state). Parts with another `RequestId` are ignored; a module answering twice keeps its latest. |
| `RestorePart` | Save, once per saved part when loading | `Module`, `Json`: that module restores its state from it (missing or unreadable Json: keep defaults). Modules that had no part get none. |
| `GameLoaded` | Save, after the `RestorePart`s of a load | `Slot` (1..3), `Auto` (the autosave was loaded). |
| `SaveGame` | anyone (the pause menu, a title screen) | `Slot` (1..3, or 0 for the current slot), `Auto`: save there. |
| `GameSaved` | Save, once per save it tried | `Slot`, `Auto`, `Ok` (false: the file could not be written, the old one is kept). |
| `LoadGame` | anyone | `Slot`, `Auto`: load that slot's manual save or autosave. A missing or unreadable file loads nothing (no `RestorePart`, no `GameLoaded`). |
| `DeleteSlot` | anyone | `Slot`: deletes its manual save and its autosave. |
| `NewGameInSlot` | the title screen's New Game | `Slot`: it becomes the current slot; nothing is deleted until the first save overwrites the files. |
| `SaveSlots` | Save, every second (from its first tick) and at once after a save, load or delete | `Slot1`..`Slot3` (`Get(slot)`), each a `SlotInfo`: `Manual` and `Auto` (`SaveInfo`: `State` None / Ok / Unreadable, `SavedAt` UTC, `CharacterName`, `Character`, `Day`, `Coins`, `Area`), `Exists`, `ManualSavedAt`, `AutoSavedAt`, `Newest` / `NewestIsAuto`. `TryGetNewest(out slot, out auto)` is the newest readable save of any slot, manual or auto (Continue). |
| `CurrentSlot` | Save, every tick | `Slot` being played, 0 for none. |

`SaveModules` names the parts: `Player`, `Wardrobe`, `Wallet`, `Pet`, `Home`, `NPCs`,
`DayNight`. What each one saves is that module's business (Player: position — area, place,
facing — and health; Wardrobe: appearance, owned and equipped clothes; Wallet: coins; Pet: owned
and active pets; Home: owned and placed furniture; NPCs: quests; DayNight: day and time). No
module answers `CollectSave` or reads `RestorePart` yet: each needs its own job.

## Files

`user://saves/slot<N>.json` (manual) and `slot<N>-auto.json` (autosave), N = 1..3. One UTF-8
JSON object per save:

```json
{
  "version": 1, "slot": 1, "auto": false, "savedAt": "2026-10-24T12:00:00.0000000Z",
  "characterName": "Girl", "character": "Girl", "day": 3, "coins": 120, "area": "Town",
  "parts": { "Player": { ... }, "Wallet": { ... } }
}
```

A part whose `Json` is valid JSON is embedded as JSON; any other text is stored as a JSON string
and handed back exactly. The header comes from the latest `PlayerState` (character, area),
`WalletState` (coins) and `TimeOfDay` (day); the character name is the kind ("Girl" / "Boy")
until characters get names. A write goes to `<file>.tmp` first, then is renamed over the old
file, so a crash or a failed write never leaves half a save (a stale `.tmp` is overwritten by the
next write). A file that is not JSON, has no `parts` object, or has no or a newer `version` is
listed as `Unreadable`, cannot be loaded, and never crashes the game; saving over it works.

## Core

Requests (`SaveGame`, `LoadGame`, `DeleteSlot`, `NewGameInSlot`, and the view's
`RequestSave` / `RequestLoad` / `RequestDelete` / `RequestNewGame`) are queued (up to 16 a tick)
and carried out in order on the module's next tick, so a save never runs in the middle of
another module's tick.

- **Saving.** Publishes `CollectSave` with a new id, gathers the `SavePart`s, writes the file
  (`Now`, UTC, is the saved time), publishes `GameSaved` and re-reads the slot list.
- **Loading.** Reads the file, publishes `RestorePart` for each part in the saved order, makes
  that slot current, counts the game as started, closes the pause menu, then publishes
  `GameLoaded`.
- **Current slot.** `NewGameInSlot` sets it. A `GameStart` with no current slot plays in slot 1
  (until the title screen sends `NewGameInSlot`).
- **Playing** (`Playing`): after `GameStart` or a load, while `StartScreenState.Open` is false and
  no menu is open (no `UiFocus` owner other than the pause menu's, and the pause menu closed).
- **Autosave.** Every `AutosaveSeconds` (60) of playing, counted in fixed steps and paused
  otherwise, to the current slot's autosave; and on the tick after `PlayerState.Area` becomes
  `Home` from another area (after the game started, not on the start screen). For
  `AreaGraceTicks` (60) after a load or a new game, entering Home does not autosave (the load
  puts the player back where they were). Loads and new games restart the 60 s.
- **Slot list.** `Slots` is read from disk when the folder is set and after each save, load or
  delete (not every second, so ticks do not allocate); it is published every `SlotsEveryTicks`
  (60).
- **Pause menu.** `OpenMenu()` opens it only when `CanOpenMenu` (= `Playing`); `CloseMenu()`
  closes it. On the next tick the Core publishes `UiFocus(SaveModule.MenuOwner, true)` (false
  when it closes): the Input module then stops moving the player. `UiFocus` from `MenuOwner`
  does not count as another menu.
- The view also reads `Current`, `Started`, `MenuOpen`, `SaveCount` / `LastSaveOk` /
  `LastSaveAuto`, `LoadCount` / `LastLoadOk`, `PlaySeconds` and `Files`. `SaveFolder` is null
  until the view sets it (nothing is saved, every slot is empty). No allocations per tick except
  when saving or loading.

## View

`SaveView` sets the Core's `SaveFolder` to `user://saves` (globalized). While playing, Esc
(keyboard) or Start (gamepad) opens the pause menu: a cream panel with a spotted red mushroom
cap on a dimmed screen, popping in. It is not opened on the start screen or while another
module's menu is open (the Core says so).

| Page | Items |
| --- | --- |
| Paused | Resume, Save (the current slot's manual save; a "Saved!" toast, or "Could not save"), Load, Quit to Title |
| Load a Game | three slot cards (character, day, coins, area, manual and auto times; "Empty" or "Unreadable"), Back |
| Slot N | Manual save, Autosave (each with its day, coins and time; grey when it cannot be loaded), Back |

Up / Down (or Left / Right, the D-pad, the left stick) move the highlight; Enter, Space or A
choose; Backspace or B go back; Esc or Start resume (or go back from the Load pages). The mouse
is visible while it is open: hovering highlights, clicking chooses (the click is kept from the
game). Loading closes the menu ("Loaded!"); autosaves show a small "Autosaved" toast.
Quit to Title loads the whole game scene again (`ReloadCurrentScene`), which starts at the start
screen; unsaved play is lost.

The game is not paused: other modules keep ticking behind the menu (DayNight's clock runs,
monsters move); only the player stands still, because the Input module zeroes movement while
the menu's `UiFocus` is open. A real pause needs a shell or per-module change.

## Screenshots

`./screenshot.sh modules/Save/Godot/Preview.tscn` shows the menu over a plain backdrop with
made-up saves in a temp folder (slot 1 with both saves, slot 2 damaged, slot 3 empty); the
environment variable `SAVE_PREVIEW` picks the page: `pause` (default), `saved` (with the
"Saved!" toast), `slots` (the slot list) or `choice` (slot 1's manual save and autosave).
