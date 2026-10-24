# Monsters module

The cute mushroom monsters of the Mushroom Forest: 6 Mushroom Caterpillars and 6 Mushroom Ants
wander the forest, chase the player when near, bite, get hit and poisoned by mushroom-tipped
arrows (and the pet), pop into a puff of spores when defeated, and come back 20 s later.

- `Contract/` — `Fungtopia.Monsters.Contract` (namespace `Fungtopia.Monsters`), see below.
- `Core/` — `Fungtopia.Monsters.Core`: `MonstersModule`, the module's one `IModule`, and
  `Monster` (one monster slot, read by the view).
- `Godot/` — `Main.tscn` with `MonstersView`, which draws every monster; `MonsterModel` (a
  model, toon materials, procedural animation, poison bubbles, defeat poof, health bar);
  `Preview.tscn` (the monsters side by side, for screenshots); `Shaders/` (toon surface with hit
  flash and poison tint, ink outline).
- `Models/` — `monsters.py`, the Blender script that builds and exports `caterpillar.glb` and
  `ant.glb` next to it.
- `Tests/` — `MonstersModuleTests.cs`: xUnit tests of `MonstersModule` with a fake `IMessageBus`.

Test it with `./test.sh Monsters` from the repository root.

## Contract

Meters, seconds, Y up, System.Numerics.

| Type | Who publishes it | What it is |
| --- | --- | --- |
| `MonsterKind` | | `Caterpillar`, `Ant` (normal; the only ones that spawn now), `Tarantula`, `Ladybug` (hard, later). |
| `MonsterState` | Monsters, every tick for every monster | `MonsterId`, `Kind`, `Position` (feet), `Radius`, `Health`, `MaxHealth`, `Alive`, `Poisoned`. A defeated monster keeps being published (`Alive` false) until its slot respawns with a new `MonsterId`. |
| `HitMonster` | the bow, the pet | `MonsterId`, `Damage` (at once), `PoisonDamagePerSecond` and `PoisonSeconds`, `Source` (0 player, 1 pet). |
| `MonsterKilled` | Monsters, once per kill | `MonsterId`, `Kind`, `Reward` (M¢), `Position`, `Source` (who dealt the last hit or poison). |
| `MonsterRewards` | | `For(kind)`: normal monsters 50 M¢, hard monsters 200 M¢. |

## Core

`MonstersModule` reads `Fungtopia.Player.PlayerState` and publishes `Fungtopia.Player.DamagePlayer`
(Player Contract); it uses the forest's `Terrain`, `ForestBounds` and `Obstacles` (Forest
Contract).

| | Caterpillar | Ant |
| --- | --- | --- |
| Health | 8 | 5 |
| Bite | 1 | 2 |
| Wander speed | 0.5 m/s | 0.9 m/s |
| Chase speed | 1.8 m/s | 2.8 m/s |
| Radius | 0.55 m | 0.45 m |

- **Spawning.** At start the 12 monsters (kinds alternating) get random free spots: inside the
  forest (3 m from its edge), off every trunk, at least 30 m from `ForestBounds.PlayerStart`,
  and 18 m apart (relaxed only if no spot is found). A respawn also keeps 25 m from the player.
- **Wandering.** A random heading (or a rest) every 2–5 s at wander speed; near the edge they
  head back to the middle. They slide around trunks like the player and never leave the forest.
- **Chasing.** Within 10 m (horizontal) of the alive player they head for it at chase speed and
  stop just inside reach. Within 1.5 m they bite with `DamagePlayer` (`From` = the monster's
  position) at once and then every 1.2 s while in reach.
- **Hits.** `HitMonster` takes `Damage` at once. Poison does `PoisonDamagePerSecond` over
  `PoisonSeconds` in whole points as it adds up. A new poison while poisoned restarts the
  duration with the new `PoisonSeconds` but keeps the rate it had.
- **Defeat.** At 0 health: one `MonsterKilled` with `MonsterRewards.For(kind)`, right away for a
  hit, on the tick for poison. Further hits are ignored. 20 s later a new monster of the same
  kind (new `MonsterId`, full health) appears at a free spot away from the player.
- Overlapping monsters push each other apart. All randomness comes from `Seed` (set before
  `Start`), so a run is repeatable. No allocations per tick.

The view reads `Monsters` (each `Monster`: `Facing`, `Velocity`, `HurtSeconds`, `SinceBite`,
`DeadSeconds`, …) and `Ticks`; tests use `Place(index, feet)` and `Find(id)`.

## View

`MonstersView` places each model between the last two ticks
(`Engine.GetPhysicsInterpolationFraction`), turned to its facing; a respawn jumps. `MonsterModel`
loads the .glb at run time with `GltfDocument` from the file's bytes (so a fresh checkout runs
without an editor import), draws it toon-shaded with ink outlines and animates it: the
caterpillar crawls in a wave from tail to head, the ant scurries on two alternating tripods of
legs with a bobbing striped abdomen; a head lunge on each bite, a white flash when hit, a green
tint and rising bubbles while poisoned, a hop, squash and puff of spores on defeat, and a small
health bar (turned to the camera) above a hurt monster.

## Models

`blender -b --factory-startup --python modules/Monsters/Models/monsters.py` (from the repository
root) writes `caterpillar.glb` and `ant.glb`; commit them with the script. The Mushroom
Caterpillar: four round green segments with a pale belly and stubby feet, a big round head with
big eyes, rosy cheeks, a smile and a red spotted mushroom cap. The Mushroom Ant: a big round head
with big eyes, worried brows and a small frown, antennae tipped with little red spotted
mushrooms, a small thorax, a big striped abdomen and six bent legs. Separate objects with pivots
where they move (`Head`, `Seg1`..`Seg4`; `Head`, `Thorax`, `Abdomen`, `LegL1`..`LegR3`). Feet at
the origin, facing -Z in Godot.

## Screenshots

`./screenshot.sh modules/Monsters/Godot/Preview.tscn` shows a crawling caterpillar, a scurrying
ant, a poisoned caterpillar and a hurt ant with health bars, and an ant's defeat poof;
`MONSTERS_PREVIEW=close` shows a caterpillar and an ant up close.
