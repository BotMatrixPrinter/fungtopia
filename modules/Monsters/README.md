# Monsters module

The cute mushroom monsters: 12 Mushroom Caterpillars and 12 Mushroom Ants (50 M¢ each) wander
Mushroom Forest, and the hard ones, 8 Mushroom Tarantulas and 8 Mushroom Ladybugs (200 M¢ each), the
darker Deep Forest. They chase the player when near and bite (the tarantula pounces, the ladybug rolls into a
charge), get hit and poisoned by mushroom-tipped arrows (and the pet), pop into a puff of spores
when defeated, and come back 8 s later (the hard ones 15 s).

- `Contract/` — `Fungtopia.Monsters.Contract` (namespace `Fungtopia.Monsters`), see below.
- `Core/` — `Fungtopia.Monsters.Core`: `MonstersModule`, the module's one `IModule`, and
  `Monster` (one monster slot, read by the view).
- `Core/MonsterLook.cs` — `MonsterLook`: how the view shades each model material (which are
  mushroom parts, the caps' gloss and rim, what glows).
- `Godot/` — `Main.tscn` with `MonstersView`, which draws every monster; `MonsterModel` (a
  model, toon materials, procedural animation, poison bubbles, defeat poof, health bar);
  `Preview.tscn` (the monsters side by side, for screenshots); `Shaders/` (toon surface with hit
  flash, poison tint and the caps' glossy band and rim, ink outline).
- `Models/` — `monsters.py`, the Blender script that builds and exports `caterpillar.glb`,
  `ant.glb`, `tarantula.glb` and `ladybug.glb` next to it.
- `Tests/` — `MonstersModuleTests.cs`: xUnit tests of `MonstersModule` with a fake `IMessageBus`.

Test it with `./test.sh Monsters` from the repository root.

## Contract

Meters, seconds, Y up, System.Numerics.

| Type | Who publishes it | What it is |
| --- | --- | --- |
| `MonsterKind` | | `Caterpillar`, `Ant` (normal, Mushroom Forest), `Tarantula`, `Ladybug` (hard, the Deep Forest). |
| `MonsterState` | Monsters, every tick for every monster | `MonsterId`, `Kind`, `Position` (feet), `Radius`, `Health`, `MaxHealth`, `Alive`, `Poisoned`. A defeated monster keeps being published (`Alive` false) until its slot respawns with a new `MonsterId`. |
| `HitMonster` | the bow, the pet | `MonsterId`, `Damage` (at once), `PoisonDamagePerSecond` and `PoisonSeconds`, `Source` (0 player, 1 pet). |
| `MonsterKilled` | Monsters, once per kill | `MonsterId`, `Kind`, `Reward` (M¢), `Position`, `Source` (who dealt the last hit or poison). |
| `MonsterRewards` | | `For(kind)`: normal monsters 50 M¢, hard monsters 200 M¢. |
| `KnockBackPlayer` | Monsters, when a ladybug's charge hits (right after its `DamagePlayer`) | `Direction` (unit, horizontal, away from the monster), `Distance` (3 m), `From`. For the Player module to apply; it does not yet. |

## Core

`MonstersModule` reads `Fungtopia.Player.PlayerState` and `Fungtopia.Pet.PetState` (where the
attacker is, for the ladybug's shell) and publishes `Fungtopia.Player.DamagePlayer` (Player
Contract); it uses the world's `Ground.HeightAt`, `Ground.Obstacles`/`Blocks`, `Areas` and
`DeepForestLayout.SpawnPoints` (Map Contract) for every monster.

| | Caterpillar | Ant | Tarantula | Ladybug |
| --- | --- | --- | --- | --- |
| Lives in | Mushroom Forest | Mushroom Forest | Deep Forest | Deep Forest |
| Count | 12 | 12 | 8 | 8 |
| Respawns after | 8 s | 8 s | 15 s | 15 s |
| Health | 8 | 5 | 18 | 25 |
| Attack | bite 1 | bite 2 | pounce 3 | charge 2 + knockback |
| Wander speed | 0.5 m/s | 0.9 m/s | 1.2 m/s | 0.4 m/s |
| Chase speed | 1.8 m/s | 2.8 m/s | 3.6 m/s | 1.2 m/s (rolls at 7 m/s) |
| Chases within | 10 m | 10 m | 12 m | 12 m |
| Radius | 0.55 m | 0.45 m | 0.8 m | 0.75 m |
| Reward | 50 M¢ | 50 M¢ | 200 M¢ | 200 M¢ |

- **Spawning.** Each kind keeps to its area (`Areas.Forest` or `Areas.DeepForest`, 3 m from its
  edge). At start the caterpillars and ants (kinds alternating) get random free spots in the
  forest: walkable, off every obstacle, at least 30 m from each of the forest's three entrances
  (south, north, west gaps) and 18 m (`SpawnSpacing`) from other monsters (relaxed to 9 m, then
  none, only if 300 random tries find no spot). The tarantulas and ladybugs (alternating) take the
  ten `DeepForestLayout.SpawnPoints` first, tried from a random one, 18 m from other monsters; when
  none is free (sixteen are more than ten spots) they get random free spots of the Deep Forest the
  same way, at least `SpawnMinEntranceDistance` (40 m) from its entrance, a taken spawn spot being
  the last resort. A respawn also keeps 25 m (`SpawnAwayFromPlayer`) from the player.
- **Wandering.** Caterpillars and ants take a random heading (or a rest) every 2–5 s at wander
  speed. Tarantulas and ladybugs roam the whole Deep Forest: each walks toward a goal, a free spot
  20–60 m away (`RoamMinDistance`, `RoamMaxDistance`), re-aiming at it (a little off) every 3–6 s
  with a short rest now and then, and picks a new goal when it gets there or is stuck behind
  trunks. Near the edge of its area a monster heads back to the middle. They slide around
  obstacles, never stop inside one (wedged between two, a monster stays put that tick) and never
  leave their area.
- **Chasing.** Within their chase range (horizontal) of the alive player they head for it at chase
  speed. Caterpillars and ants stop just inside reach; within 1.5 m they bite with `DamagePlayer`
  (`From` = the monster's position) at once and then every 1.2 s while in reach.
- **Tarantula: stalk and pounce.** Within 4.5 m and rested it crouches still for 0.5 s, then leaps
  for 0.4 s along a line fixed at take-off to just in front of where the player was (`Action`
  `Crouch`, then `Leap`); on landing it deals 3 if the player is within 1.8 m. Then it rests 2 s,
  stalking up to about 2.7 m. So the first pounce lands 54 ticks after it starts to crouch; the
  player can dodge by stepping aside during the leap.
- **Ladybug: roll and charge.** Within 7 m and rested it curls up still for 0.6 s, then rolls at
  7 m/s along a line fixed at the start (`Curl`, then `Charge`) for up to 1.2 s, stopping early at
  a trunk or the edge. If it comes within 1.5 m of the player it deals 2, publishes
  `KnockBackPlayer` (3 m away from it) and stops. Then it rests 3 s, plodding closer.
- **Ladybug shell.** A hit whose attacker (the player for `Source` 0, the pet for 1, where they
  were on the latest tick) is within 60° of where the ladybug faces does half damage; halves add
  up over hits (two 1-point hits make 1). Poison is not halved.
- **Hits.** `HitMonster` takes `Damage` at once. Poison does `PoisonDamagePerSecond` over
  `PoisonSeconds` in whole points as it adds up, for every kind. A new poison while poisoned
  restarts the duration with the new `PoisonSeconds` but keeps the rate it had.
- **Defeat.** At 0 health: one `MonsterKilled` with `MonsterRewards.For(kind)`, right away for a
  hit, on the tick for poison. Further hits are ignored. `NormalRespawnDelay` (8 s) later for a
  caterpillar or an ant, `HardRespawnDelay` (15 s) for a tarantula or a ladybug
  (`RespawnDelayOf(kind)`), a new monster of the same kind (new `MonsterId`, full health) appears
  at a free spot of its area away from the player and the other monsters, as at the start.
- Overlapping monsters push each other apart. All randomness comes from `Seed` (set before
  `Start`), so a run is repeatable. No allocations per tick.

The view reads `Monsters` (each `Monster`: `Facing`, `Velocity`, `HurtSeconds`, `SinceBite`,
`DeadSeconds`, `Action`, `ActionSeconds`, …; slots are caterpillars, ants, tarantulas, ladybugs)
and `Ticks`; tests use `Place(index, feet, facing)` and `Find(id)`.

## View

`MonstersView` places each model between the last two ticks
(`Engine.GetPhysicsInterpolationFraction`), turned to its facing; a respawn jumps. `MonsterModel`
loads the .glb at run time with `GltfDocument` from the file's bytes (so a fresh checkout runs
without an editor import), draws it toon-shaded with ink outlines and animates it: the
caterpillar crawls in a wave from tail to head, the ant scurries on two alternating tripods of
legs with a bobbing striped abdomen, the tarantula skitters on two alternating sets of four legs,
sinks and wiggles as it crouches and flies up in an arc (0.9 m high) as it leaps, the ladybug
waddles with a rocking shell, tucks in its head and legs as it curls and rolls over and over as
it charges; a head lunge on each bite, a white flash when hit, a green
tint and rising bubbles while poisoned, a soft rim glow on the Deep Forest's monsters (and a
gently glowing red shell and brightly glowing caps on the ladybug, `self_light` 0.4, twice what it was) so they read under the canopy and in the fog, a hop, squash and puff of spores on defeat, and a small
health bar (turned to the camera) above a hurt monster.

## Models

`blender -b --factory-startup --python modules/Monsters/Models/monsters.py` (from the repository
root) writes `caterpillar.glb`, `ant.glb`, `tarantula.glb` and `ladybug.glb`; commit them with the
script. The Mushroom
Caterpillar: four round green segments with a pale belly and stubby feet, a big round head with
big eyes, rosy cheeks, a smile and a red spotted mushroom cap. The Mushroom Ant: a big round head
with big eyes, worried brows and a small frown, antennae tipped with little red spotted
mushrooms, a small thorax, a big striped abdomen and six bent legs. The Mushroom Tarantula: a
furry round purple body covered in soft tufts, big dark eyes with sparkles (and two little ones
above), blush, a smile with two tiny fangs, three red spotted mushrooms on its back, and eight
long hairy legs bent high at the knee. The Mushroom Ladybug: a round red shell with black spots
and a seam, covered in little mushrooms (orange, hot pink, golden and red caps), a peachy round face with big
eyes, blush, a smile and two antennae, and six stubby legs. 
The mushrooms are vivid, the game's rule for every mushroom: caps a clearly saturated, bright
color (`Cap` a pure cherry red (0.96, 0.06, 0.08); the ladybug's `CapTan` orange (0.96, 0.48,
0.1), `CapPink` hot pink (1, 0.14, 0.55), `CapGold` golden yellow (1, 0.8, 0.12)), `Spots` pure
white, `Stem` a clean light cream (1, 0.95, 0.84). In the game every cap also gets a small glossy
highlight band and a light rim along its lit edge (`MonsterLook.CapGloss`, `CapRim`, drawn by the
toon shader's `gloss` and `rim_light`), and the spots a faint self-light (`SpotLight`) so they
stay crisp white in shade. Every other color (bodies, faces, eyes, the caterpillar's
pale `BackDot`s) is as it was. Separate objects with pivots where
they move (`Head`, `Seg1`..`Seg4`; `Head`, `Thorax`, `Abdomen`, `LegL1`..`LegR3`; `Body`,
`LegL1`..`LegR4`; `Shell`, `Head`, `LegL1`..`LegR3`). Feet at
the origin, facing -Z in Godot.

## Screenshots

`./screenshot.sh modules/Monsters/Godot/Preview.tscn` shows a crawling caterpillar, a scurrying
ant, a poisoned caterpillar and a hurt ant with health bars, and an ant's defeat poof;
`MONSTERS_PREVIEW=close` shows a caterpillar and an ant up close; `MONSTERS_PREVIEW=hard` the Deep
Forest's monsters in gloom (a tarantula skittering, one mid-pounce, a ladybug waddling, one rolling
in a charge, a hurt poisoned ladybug); `MONSTERS_PREVIEW=hardclose` a tarantula and a ladybug up
close. `MONSTERS_FOLLOW=<slot>` (0..39; 24..31 tarantulas, 32..39 ladybugs) in the game makes a
camera follow that monster and hides the screens over the world, for example
`MONSTERS_FOLLOW=32 DAYNIGHT_HOUR=12 ./screenshot.sh "" ladybug.png`.
