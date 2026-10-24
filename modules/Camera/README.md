# Camera module

The third-person camera of the Mushroom Forest. It orbits the player, turned by the look input,
and publishes one `CameraPose` per fixed tick.

- `Contract/`: `Fungtopia.Camera.Contract`, the `CameraPose` struct (namespace `Fungtopia.Camera`):
  - `Position`: world position, meters (Y up).
  - `Orientation`: world rotation; like a Godot `Camera3D` it looks along its local -Z.
  - `Yaw`: radians, Godot's convention like `PlayerState.Facing`: 0 looks toward -Z, positive
    turns left; the horizontal view direction is (-sin Yaw, 0, -cos Yaw).
- `Core/`: `Fungtopia.Camera.Core`, `CameraModule`. It reads `Fungtopia.Player.PlayerState`
  (the feet) and `Fungtopia.Input.PlayerInput.Look` (x turns the yaw right, y the pitch up).
  - The orbit point is `PivotHeight` (1.6 m) above the feet, moved `ShoulderOffset` (0.5 m) to
    the right; the camera sits `Distance` (5 m) behind it along the view direction.
  - Pitch is the view's elevation, clamped to `MinPitch`..`MaxPitch` (-35..+60 degrees); it starts
    at -12 degrees (looking slightly down). Yaw starts at 0 (looking north, -Z) and wraps.
  - The orbit point follows the player smoothly (`FollowSharpness`, 12 per second) and snaps
    when the player moves more than `SnapDistance` (4 m) in one tick (a comeback).
  - When the ground (`Fungtopia.Forest.Terrain`) or a giant mushroom trunk
    (`Fungtopia.Forest.Obstacles`, treated as tall cylinders) is in the way, the camera pulls in
    at once to keep `Clearance` (0.3 m) from it, and eases back out (`ReturnSharpness`, 4 per
    second) once the view clears.
  - **Home.** In the home room (`Fungtopia.Map.Areas.Home`, floor `Areas.HomeFloorY`,
    `RoomHeight` 4 m) the walls, floor and ceiling take the place of the ground and trunks: the
    camera keeps `Clearance` inside them (`RoomFreeLength`).
  - **Front view.** While `Fungtopia.Wardrobe.WardrobeState.Open` (the wardrobe or Abby's shop)
    or `Fungtopia.StartScreen.StartScreenState.Open`, the camera swings round to the player's
    front (`FrontView`), like the character creation screen: `FrontDistance` (2.75 m) from the
    chest, level at `ChestHeight` (0.6 m above the feet), turned right by `FrameAngle` so the
    whole player (head to feet) stands in the middle of the screen's left half (`FrameScreenX`
    -0.5 at the 70 degree `FieldOfView`, 16:9), clear of a panel on the right. The side it looks
    from (`FrontYaw`, the arm's yaw) is fixed when the view begins (again after a teleport),
    from the player's facing (`Facing` + π), and the camera then holds still: no orbit, and
    turning the character (`PreviewYaw`) does not move it. At home (`ChooseFrontYaw`), when the
    walls leave less than `MinRoomDistance` (2.2 m) in front, it takes the nearest side (in 10
    degree steps) that has that room; with less than 2.75 m it pulls in and rises
    `RaisePerMeter` (0.5 m per meter pulled in), looking down a little. The swing in and back
    out takes `TransitionSeconds` (1 s), eased (`Framing` 0..1); it blends the arm's yaw, pitch,
    length and center, not positions, so it goes round the player, never through them (and
    stays inside the room at home). Look input is ignored while open, so it comes back to the
    orbit it left. The player should turn to face the camera's side (`FrontYaw` - π) plus
    `PreviewYaw` and stand still meanwhile: that is the Player module's.
  - Modules tick in name order, so the camera uses the player's state from the previous tick.
- `Godot/`: `Main.tscn` with `CameraView`, a `Camera3D` (70 degree FOV) made current when it
  loads, so the Input module aims through it. Each frame it is placed between the last two
  poses (a jump of more than 3 m is a cut).
- `Tests/`: xUnit tests of `CameraModule` with a fake `IMessageBus`: orbit limits, smooth
  following, ground, trunk and wall pull-in, the front view's framing (left half, inside the home
  room), holding still and transitions, no allocations per tick, determinism.

Test it with `./test.sh Camera` from the repository root.
