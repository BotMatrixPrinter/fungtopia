# StartScreen module

The cute title screen shown when the game starts: a big "Fungtopia" logo with mushroom-cap
letters over a soft blurred view of Mushroom Town, with **Continue** (the newest save of any
slot, manual or auto; hidden when there is none), **Load** (the three save slots with their
character, day, coins, area and saved time; then the slot's manual save or its autosave) and
**New Game** (pick slot 1-3; a slot in use asks "Overwrite?"). Continue and Load load the save
through the Save module and close the screen. A new game goes on to "Choose your character" (the Girl and
the Boy side by side on little turntables), then "Make your look" (skin tone, hair style, hair
color, eye color and a starter outfit, each change sent at once to the Wardrobe module), then
"Choose your starter pet" (the Mushroom Cat and the Mushroom Dog), then a "Start!" button. It
publishes the character and pet once when Start is pressed.

- `Contract/` — `Fungtopia.StartScreen.Contract` (namespace `Fungtopia.StartScreen`), see below.
- `Core/` — `Fungtopia.StartScreen.Core`: `StartScreenModule`, the module's one `IModule`, and
  the `StartStep`, `TitleItem`, `LookRow` and `StartButton` enums, and `StartPrompts` (the control
  hints). It references the Wardrobe
  Contract (`Fungtopia.Wardrobe`: `Looks`, `Catalog`, `SetLook`, `Equip`, `PreviewYaw`), the Save
  Contract (`Fungtopia.Save`: `SaveSlots`, `LoadGame`, `NewGameInSlot`; it brings the Map and
  Player Contracts) and the Input Contract (`Fungtopia.Input.UiFocus`, `ActiveDevice`, `MenuNav`, `Prompts`).
- `Godot/` — `Main.tscn` with `StartScreenView` (a `CanvasLayer` on layer 50, over the HUD),
  built in code from `MushroomLogo`, `ChoiceCard`, `SlotCard`, `CapButton`, `LookPanel` and
  `PreviewStage`;
  `Preview.tscn` (one step, for screenshots); `Shaders/` (toon surface, ink outline, blur).
- `Models/` — `previews.py`, the Blender script that builds and exports `girl.glb`, `boy.glb`,
  `cat.glb`, `dog.glb` and `town.glb` next to it.
- `Tests/` — `StartScreenModuleTests.cs`: xUnit tests of `StartScreenModule` with a fake
  `IMessageBus`.

Test it with `./test.sh StartScreen` from the repository root.

## Contract

| Message | Who publishes it | What it is |
| --- | --- | --- |
| `GameStart` | StartScreen, once, on the tick "Start!" is pressed, or a save is chosen (Continue, Load) | `Character` (`GameStart.Girl` = 0, `GameStart.Boy` = 1), `StarterPet` (`GameStart.Cat` = 0, `GameStart.Dog` = 1). For a loaded save: that save's character (from `SaveSlots`) and the Cat, so the modules that wait for `GameStart` start; the Save module's `RestorePart`s (on its next tick) then set the real state. |
| `StartScreenState` | StartScreen, once every tick | `Open`: true from the first tick until "Start!" is pressed or a save is loaded, false after. |

It reads the Save module's `Fungtopia.Save.SaveSlots` and the Input module's
`Fungtopia.Input.ActiveDevice` (each kept, the latest one) and the Input module's
`Fungtopia.Input.MenuNav` (each step one direction press, see Core) and sends:

| Message | When | What |
| --- | --- | --- |
| `Fungtopia.Save.NewGameInSlot` | On the tick New Game's slot is picked (an empty slot, or "Yes" to "Overwrite?"); again if the player goes back and picks one again | `Slot` 1..3. Nothing is deleted: the Save module overwrites the slot's files at its first save. |
| `Fungtopia.Save.LoadGame` | Once, on the tick Continue, or a slot's manual save or autosave, is chosen | `Slot`, `Auto`. Then `GameStart`, and the screen closes without the creation steps. |
| `Fungtopia.Input.UiFocus` | Every tick while open, then once on the tick it closes | `Owner` `StartScreenModule.FocusOwner` (0x53545254, "STRT"), `Open`. |

It also sends the Wardrobe module's messages (see the Wardrobe README) from the look step:

| Message | When | What |
| --- | --- | --- |
| `Fungtopia.Wardrobe.SetLook` | On the tick the look step is reached from the character step, and on each tick skin tone, hair style, hair color or eye color changes | All four indices (into `Looks.SkinTones`, `HairStyles`, `HairColors`, `EyeColors`). |
| `Fungtopia.Wardrobe.Equip` | On the same tick the look step is reached, and on each tick the outfit changes | Four messages for the chosen `Catalog.StarterOutfits` entry, in the order Top, Bottom, Shoes, Head: `new Equip(id)`, or `Equip.Unequip(slot)` for a slot it leaves empty. |
| `Fungtopia.Wardrobe.PreviewYaw` | Every tick the look step is showing (from the tick it is reached; not on the tick it is left) | `Yaw`: how the player turned the character, as the wardrobe sends it: radians in [-π, π), 0 faces the camera, positive turns its front to the screen's right; 0 each time the look step is reached. |

In a tick they come in the order `SetLook`, `Equip`s, `PreviewYaw`, `NewGameInSlot`, `LoadGame`,
`GameStart`, `UiFocus`, `StartScreenState`; a few changes in one tick send one `SetLook` (the
latest look). On the tick Start is pressed (or a save is chosen), `LoadGame` (a load only) and
`GameStart` come first, then `UiFocus` and `StartScreenState` with `Open` false.


## Core

`StartScreenModule` starts on `Title`. A new game goes `Title`, `NewSlot` (then `Overwrite` for a
slot in use), `Character`, `Look`, `Pet`, `Ready`, then `Started` (closed for good); a load goes
`Title`, `Load`, `LoadSave`, `Started`, or straight from `Title` to `Started` with Continue. The
view reports presses between ticks, and each tick applies them in order (up to 32 a tick; more
are dropped).

**Directions come only from `MenuNav`.** Every highlight, choice, row and value below moves by
the Input module's paced `MenuNav` steps (one step when a direction is first pushed, then at most
one per 0.2 s while it is held, from the left stick, D-pad, arrow keys and W A S D alike): a
`Vertical` step of -1 / +1 is one Up / Down press, a `Horizontal` step one Left / Right, applied
on the next tick like the other presses; a `MenuNav` with no step does nothing. So a held
direction moves one item per 0.2 s and a short flick exactly one. `Press(StartButton)` takes only
`Confirm` and `Back`; the directions given to it are ignored.

**Menus.** `Title`, `Load`, `LoadSave`, `NewSlot` and `Overwrite` are menus (`ItemCount(step)`):
`Highlight` is the highlighted item and `ItemEnabled(item)` says whether it can be chosen.

| Step | Items (`Highlight`) | Can be chosen |
| --- | --- | --- |
| `Title` | `TitleItem.Continue` (0), `Load` (1), `NewGame` (2) | Continue and Load only when `CanContinue` (some slot has a readable save, `SaveSlots.TryGetNewest`) |
| `Load` | slot 1..3 (0..2) | a slot with a readable manual save or autosave |
| `LoadSave` | the `GameSlot`'s manual save (0), its autosave (1) | a readable one |
| `NewSlot` | slot 1..3 (0..2) | all |
| `Overwrite` | Yes (0), No (1) | both |

- Up / Left and Down / Right highlight the previous / next item that can be chosen (they stop at
  the ends); `Point(item)` (the mouse over it, 0..`MaxOptions` − 1) highlights it if it can be
  chosen; `Confirm` chooses the highlighted item (nothing when it cannot be chosen).
- The title highlights New Game at first, then Continue as soon as `SaveSlots` shows a save
  (unless the player has moved the highlight), and New Game whenever Continue cannot be chosen.
- **Continue** loads the newest readable save of any slot, manual or auto. **Load** goes to
  `Load` (the first slot that can be loaded highlighted); a slot goes to `LoadSave` (its newer
  save highlighted); choosing a save loads it. Loading sets `GameSlot`, `Loaded`, `LoadedAuto`,
  `Character` (the save's) and `StarterPet` (the Cat), and closes the screen: `LoadGame`, then
  `GameStart`, on that tick.
- **New Game** goes to `NewSlot` (slot 1 highlighted). An empty slot sets `GameSlot` and goes to
  `Character`, publishing `NewGameInSlot` on that tick; a slot that exists (even unreadable) goes
  to `Overwrite` with No highlighted: Yes does the same as an empty slot, No goes back to
  `NewSlot`.
- `Back`: `Load` and `NewSlot` go to `Title`, `LoadSave` to `Load`, `Overwrite` and `Character`
  to `NewSlot` (the slot kept highlighted); nothing on `Title`.

**New game.**

- `Press(StartButton.Confirm)` goes to the next step; on `Ready` it presses "Start!".
- `Press(StartButton.Back)` goes one step back (from `Character` to `NewSlot`); the choices are kept.
- Left / Right (`MenuNav.Horizontal`) highlight the left (Girl, Cat) or right (Boy, Dog) choice
  on the `Character` and `Pet` steps; on the `Look` step they change the highlighted row's value
  to the previous / next one, wrapping round (outfits that do not fit the character, the
  Mushroom Dress for the Boy, are skipped); on menus they move the highlight; on `Ready`
  they do nothing.
- Up / Down (`MenuNav.Vertical`) highlight the row above / below on the `Look` step (`Skin`,
  `HairStyle`, `HairColor`, `EyeColor`, `Outfit`; they stop at the ends); on menus they move
  the highlight; elsewhere nothing.
- The hair row starts on "No hair" (`HairStyle` = `Looks.NoHair`, the style appended last to
  `Looks.HairStyles`, with no model): the characters' round heads are bald, as Saria's drawing has
  them, so a character made here has no hair unless the player picks a style (Right from "No
  hair" wraps round to the first style, Left goes to the last real one). The look step's first
  `SetLook` already carries it.
- `Pick(row, value)` (a click on a swatch or pill) highlights that row and gives it that value,
  on the `Look` step only; values out of range and outfits that do not fit are ignored.
- `Point(option)` (0 or 1) highlights that choice (the mouse is over its card), on menus that
  item; other values are ignored; a click is `Point` then `Confirm`.

**Turning** (the same as the wardrobe's): on each tick of the `Look` step `Yaw` turns and is
published as `PreviewYaw`: `SetTurnButtons(left, right)` (Q / LB, E / RB, held) turn at
`TurnRadiansPerSecond` (3), else `SetTurnStick(x)` (the right stick) past `StickDeadZone` (0.2)
up to that speed, plus `Drag(pixels)` (a mouse drag, added up between ticks,
`DragRadiansPerPixel` 0.01; ignored on other steps, dropped when the step is left). Reaching the
`Look` step (from `Character` or back from `Pet`) sets `Yaw` to 0.

The Girl and the Cat are highlighted at first; the look starts at index 0 everywhere. Each time
the `Look` step is reached from `Character`, a character other than last time (or the first time)
gets `Catalog.DefaultOutfit` (the Mushroom Dress for the Girl, Sunny Stripes for the Boy) and the
`Skin` row highlighted; the look and outfit are then sent whole. Coming back from `Pet` sends
nothing. Once started, presses are ignored and
`GameStart` is never published again. The view also reads `Step`, `Character`, `StarterPet`,
`Open`, `Row`, `Yaw`, `Highlight`, `GameSlot`, `Slots`, `CanContinue`, `Skin`, `HairStyle`, `HairColor`, `EyeColor`, `Outfit`, `LookValue(row)`,
`LookCount(row)`, `OutfitFits(outfit)`, `Ticks` and `StepTicks` (ticks since the step began). No allocations per tick.

**Prompts.** `Device` is the latest `ActiveDevice`'s `Kind` (keyboard and mouse until one comes).
`StartPrompts.Hint(step, device, iconSize)` (and `Hint(iconSize)` for the current step and
device) is the control hint under a step as RichTextLabel BBCode: the buttons of that device only,
as `Prompts.Rich` icons, each group followed by what it does; no button is named in words.

| Step | Hint |
| --- | --- |
| `Title` | [Left][Right] Choose, [Confirm] OK |
| menus, `Character`, `Pet` | [Left][Right] Choose, [Confirm] OK, [Back] Back |
| `Look` | [TurnLeft][TurnRight] or [Look] Turn ("Drag to turn" on keyboard and mouse); then [Up][Down] Row, [Left][Right] Change, [Confirm] Next, [Back] Back |
| `Ready` | [Confirm] Start!, [Back] Back |

So on keyboard it shows the arrow, Enter and Esc keys (Q, E and the mouse on the look step), on
Xbox (and other pads) the D-pad, A and B (LB, RB, right stick), on PlayStation the D-pad, Cross
and Circle (L1, R1, right stick), on Nintendo the D-pad, B and A (L, R, right stick).

## View

| Action | Mouse | Keyboard | Gamepad |
| --- | --- | --- | --- |
| Highlight a choice | Hover its card | Left / Right arrows, A / D | Left stick or D-pad left / right |
| Look: pick a row | Click a value in it | Up / Down arrows, W / S | Left stick or D-pad up / down |
| Look: change a value | Click a swatch or pill | Left / Right arrows, A / D | Left stick or D-pad left / right |
| Look: turn the character | Drag on the left half | Q / E (held) | Right stick, LB / RB (held) |
| Highlight a menu item (title button, slot, save, Yes / No) | Hover it | Arrows, W A S D | Left stick or D-pad |
| OK, next, "Start!" | Click (a card, a menu item, "Next", "Start!") | Enter, or E (not on the look step, where E turns) | A |
| Back | The "Back" button | Esc or Backspace | B |

The view does not read the arrows, W A S D, the D-pad or the left stick itself: those reach the
Core only as the Input module's `MenuNav` steps (held: one step per 0.2 s). It reports Enter / E /
A, Esc / Backspace / B and the mouse.

The hint at the bottom (`StartPrompts`, in a `RichTextLabel`) shows only the active device's
buttons as icons and changes as soon as the player switches device; it is rebuilt only when the
step, the device or the size changes. Its icons are 1.45 times the text size, which scales with the
window, and it grows upwards from the bottom margin, so it stays tidy at any icon size.

The title shows the logo with a row of cap buttons under it: Continue (green, only when there is
a save; the line under the row says what it loads: slot, character, day, area), Load (greyed when
there is nothing to load) and New Game. `Load` and `NewSlot` show three `SlotCard`s ("Slot N",
then the newest save's character, "Day D   C coins", area, "Saved …" or "Autosaved …" in local
time; "Empty", or "Damaged save"); `LoadSave` shows two (Manual save, Autosave; "No save yet"
when missing); `Overwrite` shows the slot's card with Yes and No under it. Cards and buttons that
cannot be chosen are greyed; the highlighted one grows and gets a gold border (cards a bobbing
arrow too). The slot info is redrawn every half second (the Save module sends it every second).

While open, `StartScreenView` takes every input event (so a press there does not also jump or
talk in the game) and keeps the mouse visible; after "Start!" it fades out (0.6 s), stops
rendering its previews and captures the mouse again. Each step slides up and fades in. The
highlighted card grows, gets a gold border and a bobbing arrow, and its model hops and now and
then turns once round on its turntable.

On the `Look` step the blurred town is hidden, so the game shows through on the left: the
character there is the Player module's own, framed by the Camera, and it changes as the Wardrobe
applies the messages. `LookPanel` on the right draws the five rows (round swatches for the
colors, pills for hair styles and outfits; the outfits that do not fit are left out), the
highlighted row in gold with little arrows, each row's value with a gold ring, and the value under
the mouse a little bigger; the green "Next" button is below it. The left half stays free for
the character: pressing the mouse there (off the Back button) and dragging turns it, as do Q / E,
LB / RB and the right stick, polled every frame on the look step only; the hint under it shows
how, with the active device's icons.

`PreviewStage` is a `SubViewport` with its own world: a model on a turntable, or Mushroom Town
seen from a slowly drifting camera (rendered at 480x270 and drawn over the screen through a
gaussian blur). It loads the module's `.glb` files at run time with `GltfDocument` from the
files' bytes and draws them toon-shaded with ink outlines.

The font is the system's first of Nunito, Fredoka, Baloo 2, Varela Round, Comic Neue,
Quicksand at weight 800, falling back to DejaVu Sans Bold.

## Models

`blender -b --factory-startup --python modules/StartScreen/Models/previews.py` (from the
repository root) writes the five `.glb` files; commit them with the script. Static preview
figures in the game's chibi style (the Girl waving, with her ribbon bow, pigtail puffs and
spotted pink dress; the Boy waving, with his bow tie and blue shirt; the Mushroom Cat with
mushroom-cap ears and whiskers; the fluffy Mushroom Dog with little mushrooms on its back and a
blue collar), and Mushroom Town: grass, a sandy square, mushroom houses with spotted caps, doors
and lit windows, giant mushroom trees and little mushrooms.

## Screenshots

`./screenshot.sh modules/StartScreen/Godot/Preview.tscn` shows one step; the environment
variable `START_PREVIEW` picks it: `title` (default, no saves), `menu` (the title with
Continue), `load`, `loadsave` (slot 1's two saves), `newslot`, `overwrite` (these five with made-up
saves: slot 1 both saves, slot 2 damaged, slot 3 empty), `character`, `boy` (the Boy
highlighted), `look` (the Girl's look step), `boylook` (the Boy's, with skin tone and hair color
changed), `pet`, `dog` (the Dog highlighted) or `ready` (the Boy with the Dog). `START_DEVICE`
picks the device the hint is shown for: `keyboard` (default), `xbox`, `playstation`, `nintendo`
or `generic` (the preview makes the Input module's icons loadable itself, as its Input view does
in the game). The preview has no
game behind it, so the left of the look step is empty there. The main
scene (`./screenshot.sh`) shows the title over the game.
