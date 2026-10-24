# StartScreen module

The cute title screen shown when the game starts: a big "Fungtopia" logo with mushroom-cap
letters over a soft blurred view of Mushroom Town, then "Choose your character" (the Girl and
the Boy side by side on little turntables), then "Make your look" (skin tone, hair style, hair
color, eye color and a starter outfit, each change sent at once to the Wardrobe module), then
"Choose your starter pet" (the Mushroom Cat and the Mushroom Dog), then a "Start!" button. It
publishes the character and pet once when Start is pressed.

- `Contract/` — `Fungtopia.StartScreen.Contract` (namespace `Fungtopia.StartScreen`), see below.
- `Core/` — `Fungtopia.StartScreen.Core`: `StartScreenModule`, the module's one `IModule`, and
  the `StartStep`, `LookRow` and `StartButton` enums. It references the Wardrobe Contract
  (`Fungtopia.Wardrobe`: `Looks`, `Catalog`, `SetLook`, `Equip`, `PreviewYaw`).
- `Godot/` — `Main.tscn` with `StartScreenView` (a `CanvasLayer` on layer 50, over the HUD),
  built in code from `MushroomLogo`, `ChoiceCard`, `CapButton`, `LookPanel` and `PreviewStage`;
  `Preview.tscn` (one step, for screenshots); `Shaders/` (toon surface, ink outline, blur).
- `Models/` — `previews.py`, the Blender script that builds and exports `girl.glb`, `boy.glb`,
  `cat.glb`, `dog.glb` and `town.glb` next to it.
- `Tests/` — `StartScreenModuleTests.cs`: xUnit tests of `StartScreenModule` with a fake
  `IMessageBus`.

Test it with `./test.sh StartScreen` from the repository root.

## Contract

| Message | Who publishes it | What it is |
| --- | --- | --- |
| `GameStart` | StartScreen, once, on the tick "Start!" is pressed | `Character` (`GameStart.Girl` = 0, `GameStart.Boy` = 1), `StarterPet` (`GameStart.Cat` = 0, `GameStart.Dog` = 1). |
| `StartScreenState` | StartScreen, once every tick | `Open`: true from the first tick until "Start!" is pressed, false after. |

It also sends the Wardrobe module's messages (see the Wardrobe README) from the look step:

| Message | When | What |
| --- | --- | --- |
| `Fungtopia.Wardrobe.SetLook` | On the tick the look step is reached from the character step, and on each tick skin tone, hair style, hair color or eye color changes | All four indices (into `Looks.SkinTones`, `HairStyles`, `HairColors`, `EyeColors`). |
| `Fungtopia.Wardrobe.Equip` | On the same tick the look step is reached, and on each tick the outfit changes | Four messages for the chosen `Catalog.StarterOutfits` entry, in the order Top, Bottom, Shoes, Head: `new Equip(id)`, or `Equip.Unequip(slot)` for a slot it leaves empty. |
| `Fungtopia.Wardrobe.PreviewYaw` | Every tick the look step is showing (from the tick it is reached; not on the tick it is left) | `Yaw`: how the player turned the character, as the wardrobe sends it: radians in [-π, π), 0 faces the camera, positive turns its front to the screen's right; 0 each time the look step is reached. |

In a tick they come in the order `SetLook`, `Equip`s, `PreviewYaw`, `GameStart`, `StartScreenState`; a few
changes in one tick send one `SetLook` (the latest look). On the tick Start is pressed,
`GameStart` comes first, then `StartScreenState` with `Open` false.

Other modules (Player, Pet) do not read `GameStart` yet, the Player does not draw the
Wardrobe's `AppearanceState` yet, and it turns the character by `PreviewYaw` (and stands still)
only if its own job makes it do so during the start screen too: until they do, the game runs behind
the start screen and starts as the Girl with the Cat whatever is chosen.

## Core

`StartScreenModule` goes through the steps `Title`, `Character`, `Look`, `Pet`, `Ready`, then
`Started` (closed for good). The view reports presses between ticks, and each tick applies them
in order (up to 32 a tick; more are dropped):

- `Press(StartButton.Confirm)` goes to the next step; on `Ready` it presses "Start!".
- `Press(StartButton.Back)` goes one step back (not before `Title`); the choices are kept.
- `Press(StartButton.Left)` / `Right` highlight the left (Girl, Cat) or right (Boy, Dog) choice
  on the `Character` and `Pet` steps; on the `Look` step they change the highlighted row's value
  to the previous / next one, wrapping round (outfits that do not fit the character, the
  Mushroom Dress for the Boy, are skipped); elsewhere they do nothing.
- `Press(StartButton.Up)` / `Down` highlight the row above / below on the `Look` step (`Skin`,
  `HairStyle`, `HairColor`, `EyeColor`, `Outfit`; they stop at the ends); elsewhere nothing.
- `Pick(row, value)` (a click on a swatch or pill) highlights that row and gives it that value,
  on the `Look` step only; values out of range and outfits that do not fit are ignored.
- `Point(option)` (0 or 1) highlights that choice (the mouse is over its card); a click is
  `Point` then `Confirm`.

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
`Open`, `Row`, `Yaw`, `Skin`, `HairStyle`, `HairColor`, `EyeColor`, `Outfit`, `LookValue(row)`,
`LookCount(row)`, `OutfitFits(outfit)`, `Ticks` and `StepTicks` (ticks since the step began). No allocations per tick.

## View

| Action | Mouse | Keyboard | Gamepad |
| --- | --- | --- | --- |
| Highlight a choice | Hover its card | Left / Right arrows | Left stick or D-pad left / right |
| Look: pick a row | Click a value in it | Up / Down arrows | Left stick or D-pad up / down |
| Look: change a value | Click a swatch or pill | Left / Right arrows | Left stick or D-pad left / right |
| Look: turn the character | Drag on the left half | Q / E (held) | Right stick, LB / RB (held) |
| OK, next, "Start!" | Click (a card, "Next", "Start!", anywhere on the title) | Enter, or E (not on the look step, where E turns) | A |
| Back | The "Back" button | Esc or Backspace | B |

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
LB / RB and the right stick, polled every frame on the look step only; the hint under it says so.

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
variable `START_PREVIEW` picks it: `title` (default), `character`, `boy` (the Boy
highlighted), `look` (the Girl's look step), `boylook` (the Boy's, with skin tone and hair color
changed), `pet`, `dog` (the Dog highlighted) or `ready` (the Boy with the Dog). The preview has no
game behind it, so the left of the look step is empty there. The main
scene (`./screenshot.sh`) shows the title over the game.
