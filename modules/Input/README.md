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
  - The six-argument constructor still works and leaves `Interact` and `OpenMap` false.
  - `HasAim` / `AimDirection`: the world-space unit ray from the current 3D camera through the
    screen center (where the HUD's crosshair sits). When there is no camera, `HasAim` is false and `AimDirection` is zero.
  - `AimOrigin`: the world-space start of that ray, the current 3D camera's position, published with
    `AimDirection` (zero when `HasAim` is false). The nine-argument constructor sets it; the older
    constructors leave it zero.
- `Core/`: `Fungtopia.Input.Core`, `InputModule`. It keeps the latest device state its view
  reports. Mouse motion and jump, interact and map presses that arrive between ticks are added up and handed out
  once. `SetAim(origin, direction)` takes the camera ray (a non-finite origin or a zero or
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
| Free the mouse | Esc | |
| Capture the mouse again | Left click (this click does not fire) | |

The mouse is captured when the game starts.

Test it with `./test.sh Input` from the repository root.
