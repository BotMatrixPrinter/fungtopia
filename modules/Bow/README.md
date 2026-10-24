# Bow module

The player's bow: hold Fire to draw, release to shoot a poison mushroom-tipped arrow toward the
crosshair. Arrows fly under light gravity, poison the cute mushroom monsters they hit, and stick
in the ground and in giant mushroom trunks for a while.

**Who draws what.** The Player module draws the character holding the bow: the bow in its hands,
the string pulled back by `BowState.Draw` and the nocked arrow, with the bow hand at
`BowGeometry`'s hold point. The Bow module only simulates and draws the arrows once shot, and they
leave from exactly that hold point. The bow's model (`bow.glb`) is no longer drawn here.

- `Contract/` — `Fungtopia.Bow.Contract` (namespace `Fungtopia.Bow`), see below.
- `Core/` — `Fungtopia.Bow.Core`: `BowModule`, the module's one `IModule`; `Arrow` (one slot of
  the arrow pool, read by the view).
- `Godot/` — `Main.tscn` with `BowView`, which draws the arrows; `ToonModels`
  (loads the models toon-shaded with ink outlines); `Preview.tscn` with `BowPreview` (for
  screenshots); `Shaders/` (toon surface, ink outline).
- `Models/` — `bow.py`, the Blender script that builds and exports `bow.glb` and `arrow.glb`
  next to it.
- `Tests/` — `BowModuleTests.cs`: xUnit tests of `BowModule` with a fake `IMessageBus`.

Test it with `./test.sh Bow` from the repository root.

## Contract

| Message | Who publishes it | What it is |
| --- | --- | --- |
| `BowState` | Bow, once every tick | `Draw` (0..1, how far the bow is drawn), `ArrowsInFlight` (flying arrows; stuck ones do not count). |

`BowGeometry` (a static class, not a message) is where the character holds the bow, for the Player
module to put the bow hand there and for the Bow module to shoot from:

- `HoldRight` 0.15, `HoldUp` 1.15, `HoldForward` 0.45 (`HoldOffset` as a `Vector3`, X Right, Y Up,
  Z Forward): the hold point, an offset in meters from the player's feet in the player's facing
  frame (the chibi character's chest and hands). Forward is the facing (-sin Facing, 0,
  -cos Facing), Right is (cos Facing, 0, -sin Facing).
- `HoldPoint(feet, facing)`: that point in the world.
- `FullDrawPull` 0.26: how far the string (and the nocked arrow) comes back toward the character
  at a full draw, meters; a draw of `Draw` pulls `Draw` × this.

## Core

`BowModule` reads `Fungtopia.Input.PlayerInput` (`Fire`, `HasAim`, `AimDirection`),
`Fungtopia.Player.PlayerState` and `Fungtopia.Monsters.MonsterState`, and the forest's `Terrain`,
`Obstacles` and `ForestBounds` (Forest Contract). It publishes `Fungtopia.Monsters.HitMonster`.
Each tick:

- **Draw.** While `Fire` is held and the player is `Alive`, `Draw` rises by `dt / 0.6 s` up to 1
  (full after 0.6 s; holding longer keeps it full).
- **Shot.** Releasing `Fire` after drawing shoots one arrow and sets `Draw` back to 0. The arrow
  starts at the bow hand, `BowGeometry.HoldPoint(Position, Facing)` of the player (the hold
  offset turned by the player's facing, not by the aim), flying along `AimDirection` (the player's facing when there is no aim) at
  25 m/s + 20 m/s × draw (25..45 m/s). No drawing or shooting while the player is not `Alive`:
  the draw is lost.
- **Flight.** Gravity of 4 m/s² (light); each tick the arrow moves by its new velocity and points
  along it. Collisions are swept along the whole step, so fast arrows never tunnel.
- **Monster hits.** An arrow whose step enters a living monster's sphere (center `Radius` above
  its `Position`, radius `Radius`) publishes `HitMonster(MonsterId, Damage, 1 HP/s, 3 s, Source 0)`
  with `Damage` 1 + round(2 × draw) (1 for a quick tap, 2 from a half draw, 3 near a full one)
  and stops there: it is gone. Only monsters whose `MonsterState` arrived since the bow's previous
  tick can be hit; defeated ones are passed.
- **Sticking.** An arrow going into the ground (`Terrain.HeightAt`) or into a giant mushroom trunk
  (an `Obstacles` circle, up to 6 m above the ground, the shortest giant mushroom's stem; caps are
  above) sticks there for 5 s, then vanishes. The earliest of monster, ground and trunk along the
  step wins.
- **Lost arrows.** An arrow still flying after 6 s, or 10 m beyond `ForestBounds` (in the mushroom
  wall), vanishes.
- **Pool.** 24 arrow slots (`Arrows`). A shot takes a free slot, else reuses the slot shot longest
  ago, stuck arrows before flying ones. Each shot gets a new `Shot` number.

`BowModule` also exposes `Draw`, `AimDirection`, `Player`, `HasPlayer` and `Ticks`; the view
reads only `Arrows`. No allocations per tick.

## View

`BowView` draws only the arrows, interpolated between the last two ticks
(`Engine.GetPhysicsInterpolationFraction`): no bow and no nocked arrow (the Player module draws
those in the character's hands). Each arrow is drawn along its direction, with a faint lilac-to-green spore trail
(`CPUParticles3D`) while it flies; a hit on a monster leaves a little puff of poison spores; a
stuck arrow shrinks away at the end of its 5 s.

## Models

`blender -b --factory-startup --python modules/Bow/Models/bow.py` (from the repository root)
writes `bow.glb` and `arrow.glb`; commit both with the script. `bow.glb`: a chunky wooden bow with
a brown grip at the origin and little red, white-spotted mushroom caps on its tips (the game draws
the string). `arrow.glb`: tip at the origin pointing forward (-Z in Godot), a little purple
mushroom cap with glowing green spots on a cream stem, a wooden shaft and soft round green
fletching, 0.72 m long. The view loads them at run time with `GltfDocument` from the files'
bytes (an export must include `*.glb` as non-resource files). Only `arrow.glb` is drawn by this
module now; `bow.glb` is kept for the held bow the Player module draws.

## Screenshots

`./screenshot.sh modules/Bow/Godot/Preview.tscn` runs a real `BowModule` with scripted input on a
patch of forest ground, with the Player module's Girl model standing in for the player (for the
look only; it shows its own back bow, not a held one) and a few arrows already stuck in the
ground; an arrow has just left from the hold point with its spore trail.
