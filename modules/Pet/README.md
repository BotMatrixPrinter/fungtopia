# Pet module

The player's pets: the starter, a Mushroom Cat or a Mushroom Dog chosen on the start screen, and
the Mushroom Crow adopted in Pet City. The active pet follows the player everywhere on the Map's walkable ground
(the forest, the roads, Mushroom Town, Pet City, into the house with them), runs off to bite cute
mushroom monsters near the player (the Crow flies about 2 m up beside the player and swoops down
to peck them), comes back, steers around giant mushroom trunks and buildings, follows the ground,
and sits by the player while they are defeated. Owned pets that are not active stay at home.

- `Contract/` — `Fungtopia.Pet.Contract` (namespace `Fungtopia.Pet`), see below.
- `Core/` — `Fungtopia.Pet.Core`: `PetModule`, the module's one `IModule`.
- `Godot/` — `Main.tscn` with `PetView`, which draws the active pet; `PetModel` (the model, toon
  materials and procedural animation); `Preview.tscn` (all three pets, for screenshots);
  `Shaders/` (toon surface, ink outline).
- `Models/` — `pets.py`, the Blender script that builds and exports `cat.glb`, `dog.glb` and
  `crow.glb` next to it.
- `Tests/` — `PetModuleTests.cs`: xUnit tests of `PetModule` with a fake `IMessageBus`.

Test it with `./test.sh Pet` from the repository root.

## Contract

Meters, seconds, Y up, System.Numerics. Yaw angles use Godot's convention: 0 faces -Z,
positive turns left; the direction of yaw `a` is `(-sin a, 0, -cos a)`.

| Type | Who publishes it | What it is |
| --- | --- | --- |
| `PetKind` | | `Cat`, `Dog` (the starter pets), `Crow` (3,000 M¢ in Pet City). |
| `PetState` | Pet, once every tick from `GameStart` on | The active pet: `Kind`, `Position` (feet: on the ground, in the air while the Crow flies), `Facing` (yaw), `Attacking` (has a monster target: going to it or biting/pecking it). |
| `OwnedPets` | Pet, once every tick from `GameStart` on | `Cat`, `Dog`, `Crow` (owned or not), `Active` (the pet out with the player); helpers `Owns(kind)`, `AtHome(kind)` (owned and not active: the Home module shows it). |
| `AdoptPet` | whoever gives pets (Lucy's Pet Store) | `Kind`: the player now owns it (no change if they already did) and it becomes active. |
| `SetActivePet` | anyone (the Home module, a menu) | `Kind`: makes an owned pet active; ignored for one not owned. |

## Core

`PetModule` reads `Fungtopia.Player.PlayerState` and `TeleportPlayer` (Player Contract),
`Fungtopia.Monsters.MonsterState`, `Fungtopia.StartScreen.GameStart` (StartScreen Contract),
`AdoptPet` and `SetActivePet`, publishes
`Fungtopia.Monsters.HitMonster` (Monsters Contract), and uses the Map's `Ground` (height,
obstacles) and `Areas` (walkable ground) (Map Contract). Until the first `PlayerState` it waits
beside `Places.HomeDoor`.

- **Starting.** Before `GameStart` the player owns no pet and none is out: the module publishes
  nothing (no `PetState`, `OwnedPets` or bites), ignores `AdoptPet` and `SetActivePet`, and only
  notes where `TeleportPlayer` puts the player. On the first `GameStart` the player owns only its `StarterPet`
  (`GameStart.Dog` is the Dog, anything else the Cat), which is active and appears beside the
  player (or beside the home door if no `PlayerState` came yet); later ones are ignored.
- **Owning.** `AdoptPet` adds a pet and makes it active; `SetActivePet` switches to an owned
  one. Only the active pet is out: it follows, fights and is in `PetState`; a newly active pet
  appears beside the player (the other one went home). `OwnedPets` is published every tick,
  after `PetState`.

Each tick, for the active pet:

- **Following.** It runs until 2 m from the player (horizontal) and waits there; it sets off
  again once the player is over 2.5 m away. Speed grows with the distance: up to 6 m/s near the
  player (who walks at 5), up to 9 m/s when far behind, slowing down as it arrives. The Crow
  instead flies to a spot 1.2 m to the player's right (setting off when 0.5 m from it) and
  hovers `HoverHeight` (2 m) above the ground there, facing the way the player faces.
- **Teleport.** Over 30 m from the player it appears beside them at once (1.5 m to their right,
  else left, behind or in front, on free walkable ground; the Crow 1.2 m, in the air), turned the
  way they face. On `TeleportPlayer` (fast travel, the house door, waking at the clinic) it does
  the same beside the new spot at once, even before the next `PlayerState`. So it never enters
  the home interior (a room not joined to the outside) unless the player does, and a pet left in
  there comes out to the player.
- **Target.** With no target it picks the alive monster nearest to the player within 8 m of the
  player. It keeps that target while it is alive, published and within 10 m of the player.
- **Biting.** It runs to the target at 7 m/s until their footprints are within 0.4 m, turns to
  it and bites with `HitMonster(MonsterId, Damage 1, no poison, Source 1)`: at once, then every
  1 s while in reach (the 1 s also counts while it is running). The Crow swoops down (6 m/s)
  once within 1.5 m of reach, pecks the same way only once at `PeckHeight` (0.4 m) or lower,
  every 0.8 s (`AttackInterval(kind)`), and climbs back up (3 m/s) when done.
- **Returning.** When the target is defeated, gone or left behind by the player, it picks the
  next one in range, else goes back to the player.
- **Obstacles.** Looking up to 3 m ahead, it steers past the edge of the nearest obstacle
  (`Ground.Obstacles`: trunks and buildings) in its way (padded by 0.3 m), staying on the same
  side of it, and is pushed out of any it touches (a 0.35 m circle); it stays on walkable ground
  (`Areas.Walkable` with its radius), sliding along the edge.
- **Ground.** Its feet are always on `Ground.HeightAt` (the Crow's `Altitude` above it).
- **Defeated player.** While `PlayerState.Alive` is false it drops its target, goes to 1.1 m
  from the player and sits there (`Sitting`; the Crow lands) until they come back.

Helpers: `Started` (`GameStart` came), `Kind` (the active pet), `Owned`,
`Owns(kind)`, `PlaceAt(feet, facing)` puts the pet somewhere (on the ground, the Crow hovering).
The view also reads `Velocity`, `Altitude`, `Sitting`, `SinceBite`, `Teleported` (also set on
the tick after a teleport or switch between ticks) and `Ticks`; tests also read `TargetId`. No
allocations per tick (it keeps up to 128 `MonsterState`s a tick in a fixed array).

## View

`PetView` draws nothing until the game starts (`Started`); then it draws the active pet, swapping the
model when it changes, placed between the last two ticks
(`Engine.GetPhysicsInterpolationFraction`), turned to `Facing`; a teleport pops instead of gliding.

`PetModel` loads `cat.glb` or `dog.glb` at run time with `GltfDocument` from the file's bytes
(so a fresh checkout runs without an editor import), replaces each material with the toon shader
in the glTF material's color plus an ink outline, and animates the parts: a trot on diagonal leg
pairs with a body bob, a pounce on each bite (a hop forward, head up, front paws reaching out),
sitting (tipping back around the shoulders, hind legs tucked, front legs straight), a head that
looks around while idle, and a tail that wags all the time, fastest while sitting or attacking.
The Crow flaps its wings and bobs while flying, leans forward into a swoop, dips to peck on each
hit, dangles its feet in the air, and folds its wings once landed.

## Models

`blender -b --factory-startup --python modules/Pet/Models/pets.py` (from the repository root)
writes `cat.glb`, `dog.glb` and `crow.glb`; commit them with the script. The Mushroom Cat: a round orange
body with soft stripes and a cream belly, a big round head with huge eyes with sparkles, a pink
nose, a little "w" mouth, rosy cheeks, whiskers, two red spotted mushroom caps on short stems
for ears, and a long curving tail with a cream tip. The Mushroom Dog: a fluffy brown body made of
soft clumps with a cream chest, a round head with fluffy cheeks, big eyes, a cream muzzle with
a black nose and a little tongue, pointy ears, three small red spotted mushrooms growing on its
back, a blue collar with a golden tag, and a curled fluffy tail. Separate objects with pivots
where they move: `Body`, `Head` (neck), `LegFL`/`LegFR`/`LegBL`/`LegBR` (shoulder or hip),
`Tail` (root). The Mushroom Crow: a round dark indigo body with a lilac belly and spots, a big
red spotted mushroom cap on its head with two little mushrooms beside it, huge sparkly eyes,
rosy cheeks, an orange beak, small wings with lilac feather tips, a little tail fan and orange
feet; parts `Body`, `Head` (neck), `WingL`/`WingR` (shoulder), `Tail`, `LegL`/`LegR` (hip).
Feet at the origin, facing -Z in Godot.

## Screenshots

`./screenshot.sh modules/Pet/Godot/Preview.tscn` shows all three pets (the Crow hovering above
the others); the environment variable `PET_PREVIEW` picks the pose: `front` (default), `side`,
`trot`, `sit` or `pounce`; `crow` shows the Crow alone close up, flying, and `crow-peck` pecking.
