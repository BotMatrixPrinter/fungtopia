# Pet module

The player's starter pet: a Mushroom Cat (default) or a Mushroom Dog that follows the player
around the Mushroom Forest, runs off to bite cute mushroom monsters near the player, comes back,
steers around giant mushroom trunks, follows the ground, and sits by the player while they are
defeated.

- `Contract/` — `Fungtopia.Pet.Contract` (namespace `Fungtopia.Pet`), see below.
- `Core/` — `Fungtopia.Pet.Core`: `PetModule`, the module's one `IModule`.
- `Godot/` — `Main.tscn` with `PetView`, which draws the pet; `PetModel` (the model, toon
  materials and procedural animation); `Preview.tscn` (both pets side by side, for screenshots);
  `Shaders/` (toon surface, ink outline).
- `Models/` — `pets.py`, the Blender script that builds and exports `cat.glb` and `dog.glb`
  next to it.
- `Tests/` — `PetModuleTests.cs`: xUnit tests of `PetModule` with a fake `IMessageBus`.

Test it with `./test.sh Pet` from the repository root.

## Contract

Meters, seconds, Y up, System.Numerics. Yaw angles use Godot's convention: 0 faces -Z,
positive turns left; the direction of yaw `a` is `(-sin a, 0, -cos a)`.

| Type | Who publishes it | What it is |
| --- | --- | --- |
| `PetKind` | | `Cat`, `Dog` (the starter pets), `Crow` (later: 3,000 M¢ in Pet City). |
| `PetState` | Pet, once every tick | `Kind`, `Position` (feet), `Facing` (yaw), `Attacking` (has a monster target: running to it or biting it). |

## Core

`PetModule` reads `Fungtopia.Player.PlayerState` (Player Contract) and
`Fungtopia.Monsters.MonsterState`, publishes `Fungtopia.Monsters.HitMonster` (Monsters
Contract), and uses the forest's `Terrain`, `ForestBounds` and `Obstacles` (Forest Contract).
Until the first `PlayerState` it waits beside `ForestBounds.PlayerStart`. Each tick:

- **Following.** It runs until 2 m from the player (horizontal) and waits there; it sets off
  again once the player is over 2.5 m away. Speed grows with the distance: up to 6 m/s near the
  player (who walks at 5), up to 9 m/s when far behind, slowing down as it arrives.
- **Teleport.** Over 30 m from the player it appears beside them at once (1.5 m to their right,
  else left, behind or in front, on free ground), turned the way they face.
- **Target.** With no target it picks the alive monster nearest to the player within 8 m of the
  player. It keeps that target while it is alive, published and within 10 m of the player.
- **Biting.** It runs to the target at 7 m/s until their footprints are within 0.4 m, turns to
  it and bites with `HitMonster(MonsterId, Damage 1, no poison, Source 1)`: at once, then every
  1 s while in reach (the 1 s also counts while it is running).
- **Returning.** When the target is defeated, gone or left behind by the player, it picks the
  next one in range, else runs back to the player.
- **Obstacles.** Looking up to 3 m ahead, it steers past the edge of the nearest trunk in its
  way (padded by 0.3 m), staying on the same side of it, and is pushed out of any trunk it
  touches (a 0.35 m circle); it stays inside `ForestBounds`.
- **Ground.** Its feet are always on `Terrain.HeightAt`.
- **Defeated player.** While `PlayerState.Alive` is false it drops its target, runs to 1.1 m
  from the player and sits there (`Sitting`) until they come back.

Settings and helpers: `Kind` (the pet setting, Cat by default), `PlaceAt(feet, facing)` puts the
pet on the ground somewhere. The view also reads `Velocity`, `Sitting`, `SinceBite`,
`Teleported` and `Ticks`; tests also read `TargetId`. No allocations per tick (it keeps up to
128 `MonsterState`s a tick in a fixed array).

## View

`PetView` sets the Core's `Kind` from its exported `Pet` property (`"Cat"` in `Main.tscn`),
which the user argument `--pet=dog` overrides (for example `godot --path . -- --pet=dog`). It
places the model between the last two ticks (`Engine.GetPhysicsInterpolationFraction`), turned
to `Facing`; a teleport pops instead of gliding.

`PetModel` loads `cat.glb` or `dog.glb` at run time with `GltfDocument` from the file's bytes
(so a fresh checkout runs without an editor import), replaces each material with the toon shader
in the glTF material's color plus an ink outline, and animates the parts: a trot on diagonal leg
pairs with a body bob, a pounce on each bite (a hop forward, head up, front paws reaching out),
sitting (tipping back around the shoulders, hind legs tucked, front legs straight), a head that
looks around while idle, and a tail that wags all the time, fastest while sitting or attacking.
The Crow has no model yet and is drawn as the cat.

## Models

`blender -b --factory-startup --python modules/Pet/Models/pets.py` (from the repository root)
writes `cat.glb` and `dog.glb`; commit them with the script. The Mushroom Cat: a round orange
body with soft stripes and a cream belly, a big round head with huge eyes with sparkles, a pink
nose, a little "w" mouth, rosy cheeks, whiskers, two red spotted mushroom caps on short stems
for ears, and a long curving tail with a cream tip. The Mushroom Dog: a fluffy brown body made of
soft clumps with a cream chest, a round head with fluffy cheeks, big eyes, a cream muzzle with
a black nose and a little tongue, pointy ears, three small red spotted mushrooms growing on its
back, a blue collar with a golden tag, and a curled fluffy tail. Separate objects with pivots
where they move: `Body`, `Head` (neck), `LegFL`/`LegFR`/`LegBL`/`LegBR` (shoulder or hip),
`Tail` (root). Feet at the origin, facing -Z in Godot.

## Screenshots

`./screenshot.sh modules/Pet/Godot/Preview.tscn` shows both pets; the environment variable
`PET_PREVIEW` picks the pose: `front` (default), `side`, `trot`, `sit` or `pounce`.
