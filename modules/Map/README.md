# Map module

The one source of truth for the world's ground and layout. Three places joined by walkable dirt
roads: **Mushroom Forest** in the middle (the 200 m square on the origin, unchanged from the Forest
module), **Mushroom Town** to the south, **Pet City** to the north, the darker **Deep Forest** to
the west (home of the hard monsters), and the player's **home interior**, a room far away under
the world. Only these are walkable; cliffs, hedges and giant
mushrooms close off everything else, and solid props (medium mushrooms, lamps, benches, fences,
...) stand in them. All of it is fixed data in the Contract; the Core does
nothing per tick.

- `Contract/` — `Fungtopia.Map.Contract` (namespace `Fungtopia.Map`), see below.
- `Core/` — `Fungtopia.Map.Core`: `MapModule`, the module's one `IModule` (no work per tick).
- `Godot/` — `Main.tscn` with `MapView`, which draws the ground from the Contract.
- `Tests/` — `MapTests.cs`: xUnit tests of the Contract (including `Props`) and of `MapModule` with a fake `IMessageBus`.

Test it with `./test.sh Map` from the repository root.

## Layout

Meters, Y up, System.Numerics; north is -Z (Godot's forward), south is +Z. Rotations and facings
are yaws in radians about +Y (Godot's `rotation.y`); yaw 0 faces -Z.

```
  z = -290 ┌──────── Pet City ────────┐   x -80..80, z -290..-150; Lucy's Pet Store at the north
           │  blocks   plaza   blocks │   end of the plaza (0, -215), tall city blocks both sides
  z = -150 └───────────┬┬─────────────┘   SignpostPetCity at its south entrance
                       ││  road (6 m wide, gently curving)
  z = -100 ┌───────────┴┴─────────────┐   gap ~10 m wide in the forest's mushroom ring at (0, -100)
           │      Mushroom Forest     │   x, z -100..100: exactly the Forest module's ground,
           │                          │   paths, clearings and 235 giant mushroom trunks
  z = +100 └───────────┬┬─────────────┘   gap at (0, +100); SignpostForestSouth/North inside the gaps
                       ││  road
  z = +150 ┌───────────┴┴─────────────┐   SignpostTown at its north entrance
           │       Mushroom Town      │   x -80..80, z 150..290; fountain in the square (0, 215);
  z = +290 └──────────────────────────┘   your house west of it, Millie's next door, the clinic
                                          and the furniture stall east of it, Abby's clothing
                                          shop south of it (0, 246) facing it, the Mushroom
                                          Munchies food stall on the square's north-west side
                                          (-5.2, 205.3) facing the fountain, four more houses

  West of the forest, beyond a 10 m strip of cliff:
  x = -280          -110  -100
  z = -90 ┌───────────┐ │  Deep Forest: x -280..-110, z -90..90; rougher ground (roots, lumps,
          │   Deep    │ │  small dips), 311 denser, bigger giant mushrooms, 10 monster spawn spots
          │  Forest   ══╡  overgrown path 8 m wide (z -4..4) from x -96 to -112 through the gap
          │           │ │  in the middle of the forest's west edge (-100, 0); SignpostDeepForest
  z = +90 └───────────┘ │  just inside it at (-112, 5.5)
  Home interior: a 12 m x 10 m room at x 594..606, z 595..605, floor at y = -50; door in its
  south wall. Reached only through the house door (HomeDoor <-> HomeInterior / HomeExit).
```

## Contract

Everything is pure, deterministic (no random seed, no clock) and allocation-free per call.

| Member | What it is |
| --- | --- |
| `Ground.HeightAt(x, z)` | Ground height. Inside the forest square exactly the forest's rolling formula (`Fungtopia.Forest.Terrain.HeightAt`); beyond it the rolling flattens (towns stay within ±0.6 m, roads gentle), and past the walkable edge it rises into cliffs (`CliffHeight` 6.5 m, starting `HedgeBand` 1.5 m past the edge, over `CliffRise` 12 m). In the Deep Forest rougher (lumps, root ridges, small dips; within about ±0.6 m, still gentle enough to walk everywhere), fading in over the 8 m past its edge. Flat `Areas.HomeFloorY` (-50) around the home room. Continuous everywhere else. |
| `Ground.Obstacles` | `ReadOnlySpan<Circle>` (`X`, `Z`, `Radius`, `Center`): every circle walkers must not enter. First the forest's trunks (`Ground.ForestTrunkCount`, 235, same order as Forest's `Obstacles.All`), then circles covering each building of `Buildings.All` in order (a grid of circles over each footprint rectangle, one circle for the fountain), then the circles of every prop of `Props.All`, in order (`Ground.PropCircleStart`, `Ground.PropCircleCount`), then the Deep Forest's giant mushroom trunks (the last `Ground.DeepForestTrunkCount`, 311: radius 1.3..3.5 m, 2.5 m apart, 2 m from its edge, none in the entrance or within 6 m of a spawn spot). |
| `Ground.Blocks(x, z, radius = 0)` | Whether a walker of `radius` at (x, z) overlaps an obstacle (binned, so it checks only nearby circles). |
| `enum Area { Town, Road, Forest, PetCity, Home, DeepForest }` | The places. |
| `Areas.At(x, z)` | The area of (x, z): Home near the home room, Forest/Town/PetCity/DeepForest inside their rectangles (the overgrown path west of the forest square is DeepForest), Road on a road between them; for a point that is not walkable, the nearest area. |
| `Areas.Walkable(x, z, margin = 0)` | Whether (x, z) is on walkable ground with `margin` meters to spare. Obstacles are separate (`Ground.Blocks`): a walker is free where `Walkable(x, z, r) && !Blocks(x, z, r)`. |
| `Areas.Depth(x, z)` | Signed meters inside the walkable ground (negative outside). |
| `Areas.Forest`, `Town`, `PetCity`, `Home`, `DeepForest`, `Bounds(Area)` | `AreaBounds` rectangles (`MinX`, `MinZ`, `MaxX`, `MaxZ`, `Center`, `Depth`, `Contains`). `Bounds(Area.Road)` throws. |
| `Areas.RoadPoints`, `RoadHalfWidth` (3), `RoadDistance(x, z)` | The two roads' center lines as (x, z) points (a NaN point separates them); each starts 6 m inside the forest and ends 6 m inside its town. |
| `Areas.ForestHalfSize` (100), `ForestGapSouth` (0, 100), `ForestGapNorth` (0, -100), `ForestGapHalfWidth` (5) | The forest square and the two gaps its ring of dense mushrooms must leave open for the roads. |
| `Areas.ForestGapWest` (-100, 0), `DeepForestGapHalfWidth` (4), `DeepForestPath` | The forest's west gap and the overgrown path through it: the `AreaBounds` x -112..-96, z -4..4, walkable, part of the Deep Forest. Elsewhere the strip between the two forests is cliff. |
| `DeepForestLayout.Bounds`, `Entrance` (-110, 0) | The Deep Forest's rectangle and where the path comes out into it. |
| `DeepForestLayout.SpawnPoints`, `SpawnMinEntranceDistance` (40), `ClearingRadius` (6) | Ten monster spawn spots (x, z) spread over the Deep Forest, each walkable and clear of obstacles with 2 m to spare, at least 40 m from the entrance and 20 m from each other. |
| `Areas.HomeFloorY` | -50: the home room's floor height. |
| `Areas.TownPathDistance(x, z)`, `TownPathHalfWidth` (1.5) | Distance to Mushroom Town's dirt paths: the street from the entrance (0, 150) to the square, and one from the square to 1 m in front of each building. |
| `Place` | `Position` (`Vector3` on the ground: `Y == Ground.HeightAt(X, Z)`), `Yaw`, `Facing` (unit horizontal vector). |
| `Places.HomeDoor`, `ClinicDoor`, `MillieSpot`, `FurnitureShopSpot`, `LucySpot`, `AbbySpot`, `FoodSpot` | 1.6 m (`Places.FrontGap`) in front of the building's front, facing out from it. Lucy's counter is the pet store's open front; Abby stands at her clothing shop's door, facing the square; `FoodSpot` is in front of the food stall's counter, on the square, facing the fountain. |
| `Places.HomeInterior`, `HomeExit` | Inside the home room: where you appear on entering (facing into the room), and the spot by its door that leaves the house (facing the door). |
| `Places.SignpostTown`, `SignpostForestSouth`, `SignpostForestNorth`, `SignpostPetCity` | The signposts, beside the road at each entrance, facing into their own area. |
| `Places.SignpostDeepForest` | The Deep Forest's entrance signpost, just inside the west gap beside the path, facing west into it. |
| `Places.Signpost(Area)` | Town, Forest (its south signpost), PetCity or DeepForest; throws for Road and Home. |
| `enum BuildingKind` | `PlayerHouse`, `MillieHouse`, `Clinic`, `FurnitureShop`, `House`, `Fountain`, `PetStore`, `CityBlock`, `ClothingShop` (Abby's boutique mushroom house), `FoodStall` ("Mushroom Munchies", a small open stall 4.2 m x 2.4 m, 3 m tall, on the town square between the street and the path to the north-west house; added last). |
| `Building` | `Kind`, `Area`, `X`, `Z`, `Rotation` (yaw; at 0 the front faces -Z), `Width` (local X), `Depth` (local Z), `Height` (suggested drawing height), `Center`, `Front`, `ToWorld(localX, localZ)`, `Contains(x, z, margin)`. |
| `Buildings.All`, `Count`, `First(kind)`, `TownSquare` (0, 215), `CityPlaza` (0, -215) | Every building and the fountain, in a fixed order (Abby's clothing shop, then the food stall last): Town draws the Town ones, PetCity the PetCity ones, exactly there. |
| `enum PropKind` | `MediumMushroom`, `Lamp`, `Bench`, `FenceSegment`, `Planter`, `Crate`, `Statue`; new kinds are added at the end. |
| `Prop` | `Kind`, `Area`, `X`, `Z`, `Rotation` (yaw; at 0 its front faces -Z), `Radius`, `Height`, `Center`, `Front`, `Along` (its local X), `CircleCount`, `Circle(k)`: its obstacle circles, one of `Radius` for most kinds; a `FenceSegment` runs along local X, `Radius` is half its length, and is a row of `Props.FencePostRadius` (0.2 m) circles at most `Props.FenceCircleSpacing` (0.3 m) apart. A bench's seat is along local X too (`Radius` half its length). |
| `Props.All`, `Count` | Every solid prop, in a fixed order (Mushroom Forest, Deep Forest, Town, Pet City, roads), see **Props** below. |
| `Props.Clearance` (1.5), `GladeRadius` (12), `CityAvenueDistance(x, z)`, `CityAvenueHalfWidth` (3) | The free ground every prop keeps (see below); the glade around a lone forest trunk; Pet City's avenue, x = 0 from its south entrance to Lucy's counter. |

## Props

The rule: anything you can see standing taller than about 0.4 m (or wider than about 0.6 m) is
solid and comes from the Map's obstacle data, drawn exactly where and as large as `Props.All`
says (the module drawing its `Area` draws it: Forest, DeepForest, Town, PetCity; the Map draws
the road lamps). Only small ground decoration under that size (grass tufts, tiny mushrooms,
flowers, pebbles) may be scattered freely and walked over. Every prop's circles are in
`Ground.Obstacles`, so `Ground.Blocks` is true anywhere on a prop.

- **Mushroom Forest**: about 200 clusters of medium mushrooms (`MediumMushroom`, 0.8..2.5 m tall,
  radius 0.5..0.93 m), one per 9.5 m cell, the look and density the Forest view scattered; none on the
  paths (nor the overgrown trail from the west gap), in the clearings, the road gaps (and 12 m in from them) or the west gap, and none within
  `GladeRadius` (12 m) of a lone giant mushroom (one with no other trunk that close), so those stand
  in open glades.
- **Deep Forest**: about 330 medium mushrooms, denser (one per 6 m cell, squeezed between the
  trunks) and bigger (1.1..2.5 m); none on the overgrown path, in the entrance or in a spawn
  spot's clearing (`ClearingRadius`).
- **Mushroom Town**: street lamps (`Lamp`) along both sides of the street and in a ring around the
  square, benches around the square facing the fountain, planters, a fenced garden (seven
  3.5 m `FenceSegment`s, a gate in the back half, a planter in the middle) behind each house (yours,
  Millie's and the four others; a garden that would not fit whole is left out), and two crates by the furniture stall.
- **Pet City**: lamps and planters along both sides of the avenue, lamps around the plaza, benches
  facing the avenue and around the plaza, one statue on the plaza's west side.
- **Roads**: lamps beside the two roads (12 m apart, alternating sides), outside the forest square.

Each prop keeps `Props.Clearance` (1.5 m) of free ground from the roads, the forest's and town's
paths, Pet City's avenue, the buildings, the doors and every named place (2 m from those), and
from every other obstacle (a fence's own circles meet); the Deep Forest's spawn clearings and
entrance and the town square's ring around the fountain and Pet City's plaza middle stay open.

Every named place is walkable with 0.5 m to spare and clear of obstacles for a 0.5 m walker, and
everything outdoors is reachable on foot from your house's door (the tests flood-fill it); the
Deep Forest only through the west gap.

## View

`MapView` builds everything in code at startup (no imported assets, so a fresh checkout loads
headless), using its own copies of the forest's toon shaders (`Godot/Shaders/`):

- one ground mesh from `Ground.HeightAt` for everything except the forest square: a 1 m grid
  next to the square (the Forest's own grid and heights, so the seam matches) and a 2 m grid out
  to the cliffs; vertex-colored grass and moss (the forest's mottle at the seam), dirt roads, dirt
  paths from the town square to each door, a stone town square, Pet City's lilac paving with a
  pink tiled plaza, the Deep Forest's dark soil and deep moss streaked with roots (and its
  trodden overgrown path), rock on cliff faces;
- low roots spreading from the foot of each Deep Forest mushroom and a few loose ones between
  (one MultiMesh); the DeepForest module draws those mushrooms themselves and the gloom and fog;
- hedges of round blobs just past every walkable edge (darker around the Deep Forest; not along the forest's own edge, where
  the Forest module's mushroom ring stands), giant spotted mushrooms on the cliffs (some with big
  round eyes looking at the walkable ground), and little glowing mushroom lamps beside both roads
  (the `Area.Road` lamps of `Props.All`, exactly there and that tall) — each kind one MultiMesh;
- the home interior: wood plank floor, cream walls with wooden skirting, four round windows, a
  door with a knob in the south wall, a ceiling seen only from inside, and a warm light.

Buildings, signposts, NPCs, sky and sun are other modules' (Town, PetCity, Forest, ...).
