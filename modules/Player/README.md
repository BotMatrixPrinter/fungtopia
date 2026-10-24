# Player module

The player character: a chibi Girl or Boy who walks the whole world (Mushroom Town, the roads,
Mushroom Forest, Pet City, the home interior) in third person, relative to the camera, turns
smoothly, jumps, follows the ground, slides along giant mushroom trunks, buildings and the edge
of the walkable ground, stands still while talking, takes damage from monsters and stays down
after defeat until it is teleported (to the clinic the next morning).

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
| `PlayerState` | Player, once every tick | `Position` (feet), `Facing` (yaw), `Velocity`, `Grounded`, `Health`, `MaxHealth`, `Alive`, `Character` (`CharacterKind.Girl` or `Boy`), `Area` (`Fungtopia.Map.Area` at `Position`; the old constructor without it computes it with `Areas.At`). |
| `TeleportPlayer` | anyone (fast travel, the house door, DayNight at the clinic) | `Position`, `Facing`: the player is put on the ground there at once with zero velocity; after a defeat it also comes back `Alive` with full health. |
| `DamagePlayer` | monsters (anyone) | `Amount` of health to take, `From` where the hit came from. |
| `PlayerDefeated` | Player, once per defeat | `Position` where the player fell, on its next tick after health reached 0. |

## Core

`PlayerModule` reads `Fungtopia.Input.PlayerInput`, `Fungtopia.Dialogue.DialogueState`,
`TeleportPlayer` and `DamagePlayer`, and the world from the Map Contract (`Ground`, `Areas`,
`Places`). The Player Contract references the Map Contract (for `Area`). The game starts at
`Places.HomeDoor`, facing out. Each tick:

- **Camera yaw.** When the input has an aim (`HasAim`), the camera's yaw is the aim direction's
  horizontal part; otherwise `Look.X` (yaw right) is added up.
- **Walking.** `Move` (x right, y forward, clamped to length 1) is turned by the camera yaw:
  5 m/s at full deflection. While `DialogueState.Open` is true, `Move` and `Jump` are ignored.
- **Facing.** Turns at up to 10 rad/s the short way round: toward the aim while `Fire` is held,
  else toward the move direction; it keeps its facing when standing still.
- **Jump.** On `Jump` while grounded: 1.2 m high under 20 m/s² gravity (about 0.7 s in the air).
- **Ground.** While grounded the feet stay on `Ground.HeightAt` (up and down hills).
- **Collisions.** A 0.4 m circle. When `Ground.Blocks` says it overlaps an obstacle it is pushed
  out of each overlapping `Ground.Obstacles` circle along its normal (so it slides around trunks
  and buildings); when `Areas.Walkable(x, z, 0.4)` is false it is pushed back along the gradient
  of `Areas.Depth` (slides along the edges of roads, towns and the forest). If that does not
  settle (a tight corner) it stays where it was. `CanStand(x, z)` tells whether a spot is free.
- **Health.** 10 of 10. `DamagePlayer` with `Amount > 0` lowers it (never below 0), then 0.7 s of
  invulnerability ignores more hits. At 0: `Alive` false, one `PlayerDefeated`, no moving, for
  as long as it takes: the next `TeleportPlayer` brings it back with full health (DayNight sends
  it the next morning at Dr. Jeremy's clinic).

Settings and helpers: `Character` (the character setting, Girl by default), `PlaceAt(feet,
facing)` puts the player on the ground somewhere. The view also reads `CameraYaw`,
`InvulnerableSeconds`, `DefeatedSeconds` and `Ticks`. No allocations per tick.

## View

`PlayerView` sets the Core's `Character` from its exported `Character` property (`"Girl"` in
`Main.tscn`), which the user argument `--character=boy` overrides (for example
`godot --path . -- --character=boy`). It places the model between the last two ticks
(`Engine.GetPhysicsInterpolationFraction`), turned to `Facing`; a teleport jumps instead of
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
