# Input module

Reads mouse, keyboard and gamepad and publishes one `PlayerInput` message per fixed tick.
Other modules (player, camera, bow) read it from the bus.

- `Contract/`: `Fungtopia.Input.Contract`, the `PlayerInput` struct (namespace `Fungtopia.Input`):
  - `Move`: x right, y forward, relative to the camera, length 0..1. Keyboard diagonals are
    normalized to length 1. The left stick is analog (it has a radial dead zone and is clamped to 1).
    The keyboard wins when both are used.
  - `Look`: camera turn this tick in radians, x yaw right, y pitch up.
  - `Jump`: true on the one tick after a jump press.
  - `Fire`: true while fire is held.
  - `Interact`: true on the one tick after an interact press (talk, signposts, doors).
  - `OpenMap`: true on the one tick after a map press.
  - `Eat`: true on the one tick after an eat press (eat the selected food).
  - `FoodSelect`: 0, 1 or 2 on the one tick after food key 1, 2 or 3 is pressed (the last one pressed
    wins), else -1 (also in `default(PlayerInput)`).
  - `FoodCycle`: -1 / +1 on the one tick after a previous / next food press, else 0 (presses between
    ticks add up and their sign is reported).
  - The six-argument constructor still works and leaves `Interact` and `OpenMap` false. The six-,
    eight- and nine-argument constructors leave `Eat` false, `FoodSelect` -1 and `FoodCycle` 0; the
    twelve-argument one sets them (`FoodSelect` outside 0..2 becomes -1, `FoodCycle` becomes its sign).
  - `HasAim` / `AimDirection`: the world-space unit ray from the current 3D camera through the
    screen center (where the HUD's crosshair sits). When there is no camera, `HasAim` is false and `AimDirection` is zero.
  - `AimOrigin`: the world-space start of that ray, the current 3D camera's position, published with
    `AimDirection` (zero when `HasAim` is false). The nine-argument constructor sets it; the older
    constructors leave it zero.
- `Contract/` also has `UiFocus` (`int Owner`, `bool Open`): a module publishes `Open` true while one
  of its menus is open and false when it closes (`Owner` is any number unique to that menu, for
  example a hash of its name). While any owner is open, `PlayerInput` has `Move` and `Look` zero and
  `Jump`, `Fire` and `Eat` false, `FoodSelect` -1 and `FoodCycle` 0; `Interact` and `OpenMap` are still reported, so menus and dialogue can use
  them. Owners are tracked one by one: an owner that opens twice needs one close, closing an owner
  that is not open changes nothing, and the controls come back when the last open owner closes.
- `Core/`: `Fungtopia.Input.Core`, `InputModule`. It keeps the latest device state its view
  reports. Mouse motion and jump, interact, map, eat and food presses (`PressEat`, `PressFoodSelect(slot)`,
  `PressFoodCycle(direction)`) that arrive between ticks are added up and handed out once (presses made while a menu is open are dropped, not saved for later). `MenuOpen` is true while
  any `UiFocus` owner is open. `SetAim(origin, direction)` takes the camera ray (a non-finite origin or a zero or
  non-finite direction means no aim). Settings: `StickDeadZone` (0.2), `StickLookRadiansPerSecond` (3),
  `MouseRadiansPerPixel` (0.003), `TriggerThreshold` (0.5).
- `Godot/`: `Main.tscn` with `InputView`, a full-screen `Control` that ignores the mouse. It
  reads the devices before each tick, casts the aim ray with
  `GetViewport().GetCamera3D()`: `ProjectRayOrigin(center)` (the camera's position) and
  `ProjectRayNormal(center)`. It draws nothing on screen: the
  crosshair (shown only while a weapon is in use) belongs to the HUD.
- `Tests/`: xUnit tests of `InputModule` with a fake `IMessageBus`.

## Controls

| Action | Mouse and keyboard | Gamepad (first pad) |
|---|---|---|
| Move | W A S D (physical keys) | Left stick |
| Look | Mouse (when captured) | Right stick |
| Jump | Space | A / Cross |
| Fire (hold) | Left mouse button | Right trigger |
| Interact | E (physical key) | X / Square |
| Open map | M (physical key) | Back / Select or Touchpad |
| Eat the selected food | F (physical key) | D-pad Up |
| Select food 1 / 2 / 3 | 1 / 2 / 3 (physical keys, top row) | |
| Previous / next food | | D-pad Left / Right |
| Free the mouse | Esc | |
| Capture the mouse again | Left click (this click does not fire) | |

The mouse is captured when the game starts.

Test it with `./test.sh Input` from the repository root.
