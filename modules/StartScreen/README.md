# StartScreen module

The cute title screen shown when the game starts: a big "Fungtopia" logo with mushroom-cap
letters over a soft blurred view of Mushroom Town, then "Choose your character" (the Girl and
the Boy side by side on little turntables), then "Choose your starter pet" (the Mushroom Cat and
the Mushroom Dog), then a "Start!" button. It publishes the choices once when Start is pressed.

- `Contract/` — `Fungtopia.StartScreen.Contract` (namespace `Fungtopia.StartScreen`), see below.
- `Core/` — `Fungtopia.StartScreen.Core`: `StartScreenModule`, the module's one `IModule`, and
  the `StartStep` and `StartButton` enums.
- `Godot/` — `Main.tscn` with `StartScreenView` (a `CanvasLayer` on layer 50, over the HUD),
  built in code from `MushroomLogo`, `ChoiceCard`, `CapButton` and `PreviewStage`;
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

On the tick Start is pressed, `GameStart` comes first, then `StartScreenState` with `Open`
false. Other modules (Player, Pet) do not read these yet: until they do, the game runs behind
the start screen and starts as the Girl with the Cat whatever is chosen.

## Core

`StartScreenModule` goes through the steps `Title`, `Character`, `Pet`, `Ready`, then
`Started` (closed for good). The view reports presses between ticks, and each tick applies them
in order (up to 32 a tick; more are dropped):

- `Press(StartButton.Confirm)` goes to the next step; on `Ready` it presses "Start!".
- `Press(StartButton.Back)` goes one step back (not before `Title`); the choices are kept.
- `Press(StartButton.Left)` / `Right` highlight the left (Girl, Cat) or right (Boy, Dog) choice
  on the `Character` and `Pet` steps; elsewhere they do nothing.
- `Point(option)` (0 or 1) highlights that choice (the mouse is over its card); a click is
  `Point` then `Confirm`.

The Girl and the Cat are highlighted at first. Once started, presses are ignored and
`GameStart` is never published again. The view also reads `Step`, `Character`, `StarterPet`,
`Open`, `Ticks` and `StepTicks` (ticks since the step began). No allocations per tick.

## View

| Action | Mouse | Keyboard | Gamepad |
| --- | --- | --- | --- |
| Highlight a choice | Hover its card | Left / Right arrows | Left stick or D-pad left / right |
| OK, next, "Start!" | Click (a card, "Start!", anywhere on the title) | Enter or E | A |
| Back | The "Back" button | Esc or Backspace | B |

While open, `StartScreenView` takes every input event (so a press there does not also jump or
talk in the game) and keeps the mouse visible; after "Start!" it fades out (0.6 s), stops
rendering its previews and captures the mouse again. Each step slides up and fades in. The
highlighted card grows, gets a gold border and a bobbing arrow, and its model hops and now and
then turns once round on its turntable.

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
highlighted), `pet`, `dog` (the Dog highlighted) or `ready` (the Boy with the Dog). The main
scene (`./screenshot.sh`) shows the title over the game.
