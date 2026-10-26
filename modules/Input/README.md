# Input module

Reads mouse, keyboard and gamepad and publishes one `PlayerInput`, one `ActiveDevice` and one `MenuNav`
message per fixed tick. Other modules (player, camera, bow) read them from the bus; modules that show button
prompts use `ActiveDevice` with `Prompts` to show only the buttons of the device in use, as icons.

- `Contract/`: `Fungtopia.Input.Contract`, the `PlayerInput` struct (namespace `Fungtopia.Input`):
  - `Move`: x right, y forward, relative to the camera, length 0..1. Keyboard diagonals are
    normalized to length 1. The left stick is analog (it has a radial dead zone and is clamped to 1).
    The keyboard wins when both are used.
  - `Look`: camera turn this tick in radians, x yaw right, y pitch up.
  - `Jump`: true on the one tick after a jump press (Space; the gamepad's bottom button only when
    nothing can be interacted with, see `InteractAvailable`).
  - `Fire`: true while fire is held.
  - `Run`: true while run is held: the gamepad's left trigger at or past `TriggerThreshold` (the
    same threshold the right trigger uses for `Fire`), or Shift on the keyboard.
  - `Interact`: true on the one tick after an interact press (talk, signposts, doors): E, X / Square,
    or the gamepad's bottom button while something can be interacted with or a menu is open.
  - `OpenMap`: true on the one tick after a map press.
  - `Eat`: true on the one tick after an eat press (eat the selected food).
  - `FoodSelect`: 0, 1 or 2 on the one tick after food key 1, 2 or 3 is pressed (the last one pressed
    wins), else -1 (also in `default(PlayerInput)`).
  - `FoodCycle`: -1 / +1 on the one tick after a previous / next food press, else 0 (presses between
    ticks add up and their sign is reported).
  - The six-argument constructor still works and leaves `Interact` and `OpenMap` false. The six-,
    eight- and nine-argument constructors leave `Eat` false, `FoodSelect` -1 and `FoodCycle` 0; the
    twelve-argument one sets them (`FoodSelect` outside 0..2 becomes -1, `FoodCycle` becomes its sign).
    Every constructor but the full thirteen-argument one (which adds `run` last) leaves `Run` false.
  - `HasAim` / `AimDirection`: the world-space unit ray from the current 3D camera through the
    screen center (where the HUD's crosshair sits). When there is no camera, `HasAim` is false and `AimDirection` is zero.
  - `AimOrigin`: the world-space start of that ray, the current 3D camera's position, published with
    `AimDirection` (zero when `HasAim` is false). The nine-argument constructor sets it; the older
    constructors leave it zero.
- `Contract/` also has `UiFocus` (`int Owner`, `bool Open`): a module publishes `Open` true while one
  of its menus is open and false when it closes (`Owner` is any number unique to that menu, for
  example a hash of its name). While any owner is open, `PlayerInput` has `Move` and `Look` zero and
  `Jump`, `Fire`, `Run` and `Eat` false, `FoodSelect` -1 and `FoodCycle` 0; `Interact` and `OpenMap` are still reported, so menus and dialogue can use
  them. Owners are tracked one by one: an owner that opens twice needs one close, closing an owner
  that is not open changes nothing, and the controls come back when the last open owner closes.
- `Contract/` also has `InteractAvailable` (`int Owner`, `bool Available`), tracked per owner like
  `UiFocus`: a module publishes `Available` true while it offers an interaction (its Interact prompt
  shows, or a dialogue is open) and false when that ends (NPCs, Travel, Home, Food and Dialogue do).
  This is "smart A": a press of the gamepad's bottom face button (Xbox A, PlayStation Cross,
  Nintendo's bottom button, B) gives `Interact` and no `Jump` while any `InteractAvailable` owner is
  available or any `UiFocus` owner is open, and `Jump` otherwise (as in Zelda and Animal Crossing).
  X / Square (Nintendo Y) always gives `Interact`; the keyboard is unchanged (E interacts, Space jumps).
- `Contract/` also has `MenuNav` (`int Vertical`: -1 up, +1 down, else 0; `int Horizontal`: -1 left,
  +1 right, else 0; `bool Any`), published every tick (also while a menu is open), so every menu moves
  its selection at the same pace instead of reading the stick and D-pad itself. A step fires on the tick
  a direction is first pushed (left stick past `MenuStickThreshold`, 0.5, on its larger axis; D-pad;
  arrow keys; W A S D), then once per `MenuRepeatSeconds` (0.2 s) while it stays held. Steps are never
  closer than 0.2 s, however often the stick is flicked; a push 0.2 s or more after the last step fires
  at once. Stick wobbles inside the threshold never fire. At most one field is non-zero: the stick
  uses its larger axis, held buttons win over the stick, and vertical wins over horizontal.
- `Contract/` also has the prompt helpers (see [Prompts](#prompts)): `ActiveDevice` (`DeviceKind Kind`,
  `bool IsGamepad`), `enum DeviceKind { KeyboardMouse, Xbox, PlayStation, Nintendo, Generic }`,
  `enum PromptAction` and `static class Prompts`.
- `Core/`: `Fungtopia.Input.Core`, `InputModule`. It keeps the latest device state its view
  reports. Mouse motion and jump, interact, map, eat and food presses (`PressEat`, `PressFoodSelect(slot)`,
  `PressFoodCycle(direction)`) that arrive between ticks are added up and handed out once (presses made while a menu is open are dropped, not saved for later);
  `PressGamepadA()` (the bottom face button) becomes Interact or Jump at the tick, by `InteractOffered`
  (any `InteractAvailable` owner available) and `MenuOpen`. `MenuOpen` is true while
  any `UiFocus` owner is open. `SetAim(origin, direction)` takes the camera ray (a non-finite origin or a zero or
  non-finite direction means no aim). `Device` is the device used last (keyboard and mouse at start),
published as `ActiveDevice` every tick (also while a menu is open): `NoteKeyboardMouse()` (a key,
mouse button or wheel press), `NoteMouseMotion(pixels)` (switches from `MouseSwitchPixels`, 2, in one
event), `NoteGamepadButton(family)` and `NoteGamepadAxis(family, value)` (switches only past
`StickDeadZone`). `SetFireButton` / `SetFireTrigger` and `SetRunKey` / `SetRunTrigger` take the held
fire and run buttons and triggers. `SetMenuButtons(vertical, horizontal)` takes the held D-pad / arrow / WASD menu
directions; the menu stick is the left stick from `SetMoveStick`, and `LatestMenuNav` is the last step.
`JoypadFamily.Detect(name, guid)` tells the family from Godot's joypad name
(PlayStation / DualShock / DualSense / Sony / PS3-5, Nintendo / Switch / Joy-Con / Pro Controller,
Xbox / XInput / Microsoft) or else the USB vendor in its SDL GUID (054c Sony, 057e Nintendo,
045e Microsoft); any other pad is Generic. Settings: `StickDeadZone` (0.2), `StickLookRadiansPerSecond` (3),
  `MouseRadiansPerPixel` (0.003), `TriggerThreshold` (0.5), `MenuStickThreshold` (0.5), `MenuRepeatSeconds` (0.2).
- `Godot/`: `Main.tscn` with `InputView`, a full-screen `Control` that ignores the mouse. It
  reads the devices before each tick, reports which device each real input came from (the family of
  each joypad is detected once per connection), casts the aim ray with
  `GetViewport().GetCamera3D()`: `ProjectRayOrigin(center)` (the camera's position) and
  `ProjectRayNormal(center)`. It draws nothing on screen: the
  crosshair (shown only while a weapon is in use) belongs to the HUD. `GlyphTextures.Register()` (called
  in its `_Ready`) makes the icons loadable by their res:// paths even when Godot has not imported
  them (a fresh checkout run without the editor): each unimported SVG is rasterized and put in the
  resource cache under its own path. `GlyphSheet.tscn` shows every icon:
  `./screenshot.sh modules/Input/Godot/GlyphSheet.tscn glyphs.png`.
- `Glyphs/`: the icons, 64x64 SVG files made by `make_glyphs.py` (run it with `python3` to remake them;
  letters are drawn as strokes because Godot's SVG loader does not draw `<text>`). `key_*` (E Q F M R
  W A S D 1 2 3, Esc, Enter, Space, Shift, arrows, the `wasd` and `arrows` clusters), `mouse_*` (left,
  right, move, wheel up / down), `xbox_*` (a b x y, lb rb lt rt, ls rs, dpad_up/down/left/right, view,
  menu), `ps_*` (cross circle square triangle, l1 r1 l2 r2, ls rs, dpad_*, share, options), `ns_*`
  (a b x y, l r zl zr, ls rs, dpad_*, minus, plus).
- `Tests/`: xUnit tests of `InputModule` with a fake `IMessageBus`.

## Controls

| Action | Mouse and keyboard | Gamepad (first pad) |
|---|---|---|
| Move | W A S D (physical keys) | Left stick |
| Look | Mouse (when captured) | Right stick |
| Jump | Space | A / Cross (when nothing can be interacted with) |
| Fire (hold) | Left mouse button | Right trigger |
| Run (hold, while moving) | Shift | Left trigger |
| Interact | E (physical key) | A / Cross while something can be interacted with or a menu is open; X / Square always |
| Open map | M (physical key) | Back / Select or Touchpad |
| Eat the selected food | F (physical key) | D-pad Up |
| Select food 1 / 2 / 3 | 1 / 2 / 3 (physical keys, top row) | |
| Previous / next food | Mouse wheel up / down (when captured) | D-pad Left / Right |
| Free the mouse | Esc | |
| Capture the mouse again | Left click (this click does not fire) | |

The mouse is captured when the game starts.

## Prompts

`Prompts.Icon(action, device)` is the res:// path of the button's icon, `Prompts.Rich(action, device,
size = 28)` RichTextLabel BBCode for it (`[img=28]res://modules/Input/Glyphs/key_e.svg[/img]`, set
`BbcodeEnabled`), `Prompts.Text(action, device)` a plain name ("E", "A", "Cross"), and
`Prompts.Glyph(action, device)` the icon's file name ("key_e"). Show prompts for the `ActiveDevice`
the bus delivers (it arrives from the first tick on, after every view is ready, so the icons are
loadable by then). Generic pads show the Xbox buttons. Godot's gamepad buttons are positional, so a
Nintendo pad's bottom button is B and its right one A.

| PromptAction | Keyboard / mouse | Xbox (and Generic) | PlayStation | Nintendo | Used by |
|---|---|---|---|---|---|
| Interact | E | A | Cross | B | this module (X / Square / Y interacts too) |
| Jump | Space | A | Cross | B | this module |
| Fire | Left click | RT | R2 | ZR | this module |
| Back | Esc | B | Circle | A | menus: backs out one page, and closes the menu from its top one (Home and Food also take Q) |
| Confirm | Enter | A | Cross | B | menus (the start screen also takes E) |
| OpenMap | M | View | Share | - | this module |
| Eat | F | D-pad Up | D-pad Up | D-pad Up | this module |
| FoodPrev | Wheel up | D-pad Left | D-pad Left | D-pad Left | this module (keys 1-3 pick a slot) |
| FoodNext | Wheel down | D-pad Right | D-pad Right | D-pad Right | this module |
| Pause | Esc | Menu | Options | + | pause menu |
| MenuUp / Down / Left / Right | Arrow keys | D-pad | D-pad | D-pad | menus, via `MenuNav` (also WASD and the left stick) |
| TurnLeft | Q | LB | L1 | L | wardrobe |
| TurnRight | E | RB | R1 | R | wardrobe |
| Move | W A S D | Left stick | Left stick | Left stick | this module |
| Look | Mouse | Right stick | Right stick | Right stick | this module |
| Run | Shift | LT | L2 | ZL | this module |

Test it with `./test.sh Input` from the repository root.
