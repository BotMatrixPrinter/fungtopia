# Player module

The player character: a chibi Girl or Boy who walks around the Mushroom Forest in third person,
relative to the camera, turns smoothly, jumps, follows the ground, slides along giant mushroom
trunks and the forest's edge, takes damage from monsters and comes back at the start after
defeat.

- `Contract/` — `Fungtopia.Player.Contract` (namespace `Fungtopia.Player`), see below.
- `Core/` — `Fungtopia.Player.Core`: `PlayerModule`, the module's one `IModule`.
- `Godot/` — `Main.tscn` with `PlayerView`, which draws the character; `CharacterModel` (the
  model, toon materials and procedural animation); `Preview.tscn` (both characters side by side,
  for screenshots); `Shaders/` (toon surface with hit flash, ink outline).
- `Models/` — `characters.py`, the Blender script that builds and exports `girl.glb` and
  `boy.glb` next to it.
- `Tests/` — `PlayerModuleTests.cs`: xUnit tests of `PlayerModule` with a fake `IMessageBus`.

Test it with `./test.sh Player` from the repository root.

## Contract

Meters, seconds, Y up, System.Numerics. Yaw angles use Godot's convention: 0 faces -Z,
positive turns left; the direction of yaw `a` is `(-sin a, 0, -cos a)`.

| Message | Who publishes it | What it is |
| --- | --- | --- |
| `PlayerState` | Player, once every tick | `Position` (feet), `Facing` (yaw), `Velocity`, `Grounded`, `Health`, `MaxHealth`, `Alive`, `Character` (`CharacterKind.Girl` or `Boy`). |
| `DamagePlayer` | monsters (anyone) | `Amount` of health to take, `From` where the hit came from. |
| `PlayerDefeated` | Player, once per defeat | `Position` where the player fell, on its next tick after health reached 0. |

## Core

`PlayerModule` reads `Fungtopia.Input.PlayerInput` and the forest's `Terrain`, `ForestBounds`
and `Obstacles` (Forest Contract). Each tick:

- **Camera yaw.** When the input has an aim (`HasAim`), the camera's yaw is the aim direction's
  horizontal part; otherwise `Look.X` (yaw right) is added up.
- **Walking.** `Move` (x right, y forward, clamped to length 1) is turned by the camera yaw:
  5 m/s at full deflection.
- **Facing.** Turns at up to 10 rad/s the short way round: toward the aim while `Fire` is held,
  else toward the move direction; it keeps its facing when standing still.
- **Jump.** On `Jump` while grounded: 1.2 m high under 20 m/s² gravity (about 0.7 s in the air).
- **Ground.** While grounded the feet stay on `Terrain.HeightAt` (up and down hills).
- **Collisions.** A 0.4 m circle, pushed out of every trunk along its normal (so it slides around
  them) and kept inside `ForestBounds` (slides along the edge).
- **Health.** 10 of 10. `DamagePlayer` with `Amount > 0` lowers it (never below 0), then 0.7 s of
  invulnerability ignores more hits. At 0: `Alive` false, one `PlayerDefeated`, no moving.
  3 s later the player comes back at `ForestBounds.PlayerStart` with full health (the doctor in
  town comes later).

Settings and helpers: `Character` (the character setting, Girl by default), `PlaceAt(feet,
facing)` puts the player on the ground somewhere. The view also reads `CameraYaw`,
`InvulnerableSeconds`, `DefeatedSeconds` and `Ticks`. No allocations per tick.

## View

`PlayerView` sets the Core's `Character` from its exported `Character` property (`"Girl"` in
`Main.tscn`), which the user argument `--character=boy` overrides (for example
`godot --path . -- --character=boy`). It places the model between the last two ticks
(`Engine.GetPhysicsInterpolationFraction`), turned to `Facing`; a comeback jumps instead of
gliding. The view has no camera: the camera module follows `PlayerState`.

`CharacterModel` loads `girl.glb` or `boy.glb` at run time with `GltfDocument` from the file's
bytes (so a fresh checkout runs without an editor import; an export must include `*.glb` as
non-resource files), replaces each material with the toon shader in the glTF material's color
plus an ink outline, and animates the parts: walk bob, leg and arm swing, body sway, arms up and
legs tucked in the air, stretch rising and squash on landing, a blinking white flash while
invulnerable, and tipping over on its back when defeated.

## Models

`blender -b --factory-startup --python modules/Player/Models/characters.py` (from the
repository root) writes `girl.glb` and `boy.glb`; commit both with the script. Chibi kids from
the drawing: a big round head with large dark oval eyes with sparkles, rosy cheeks, a small
smile and hair; a simple rounded body; short arms with little round hands; stubby legs with round
shoes; a wooden bow slung on the back with mushroom-red tips. The Girl has a ribbon bow on her
head, pigtail puffs and a pink triangle dress with white spots; the Boy a red bow tie, a
rounded blue shirt and trousers. Separate objects with pivots where they move: `Body`, `Head`
(neck), `LegL`/`LegR` (hip), `ArmL`/`ArmR` (shoulder), `BackBow`. Feet at the origin, facing
-Z in Godot.

## Screenshots

`./screenshot.sh modules/Player/Godot/Preview.tscn` shows both characters; the environment
variable `PLAYER_PREVIEW` picks the pose: `front` (default), `back`, `walk`, `jump` or `hit`.
