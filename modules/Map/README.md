# Map module

The one source of truth for the world's ground and layout. Three places joined by walkable dirt
roads: **Mushroom Forest** in the middle (the 200 m square on the origin, unchanged from the Forest
module), **Mushroom Town** to the south, **Pet City** to the north, the darker **Deep Forest** to
the west (home of the hard monsters), and the player's **home interior**, a room far away under
the world. Only these are walkable; cliffs, hedges and giant
mushrooms close off everything else. All of it is fixed data in the Contract; the Core does
nothing per tick.

- `Contract/` — `Fungtopia.Map.Contract` (namespace `Fungtopia.Map`), see below.
- `Core/` — `Fungtopia.Map.Core`: `MapModule`, the module's one `IModule` (no work per tick).
- `Godot/` — `Main.tscn` with `MapView`, which draws the ground from the Contract.
- `Tests/` — `MapTests.cs`: xUnit tests of the Contract and of `MapModule` with a fake `IMessageBus`.

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
                                          shop south of it (0, 246) facing it, four more houses

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
| `Ground.Obstacles` | `ReadOnlySpan<Circle>` (`X`, `Z`, `Radius`, `Center`): every circle walkers must not enter. First the forest's trunks (`Ground.ForestTrunkCount`, 235, same order as Forest's `Obstacles.All`), then circles covering each building of `Buildings.All` in order (a grid of circles over each footprint rectangle, one circle for the fountain), then the Deep Forest's giant mushroom trunks (the last `Ground.DeepForestTrunkCount`, 311: radius 1.3..3.5 m, 2.5 m apart, 2 m from its edge, none in the entrance or within 6 m of a spawn spot). |
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
| `Place` | `Position` (`Vector3` on the ground: `Y == Ground.HeightAt(X, Z)`), `Yaw`, `Facing` (unit horizontal vector). |
| `Places.HomeDoor`, `ClinicDoor`, `MillieSpot`, `FurnitureShopSpot`, `LucySpot`, `AbbySpot` | 1.6 m (`Places.FrontGap`) in front of the building's front, facing out from it. Lucy's counter is the pet store's open front; Abby stands at her clothing shop's door, facing the square. |
| `Places.HomeInterior`, `HomeExit` | Inside the home room: where you appear on entering (facing into the room), and the spot by its door that leaves the house (facing the door). |
| `Places.SignpostTown`, `SignpostForestSouth`, `SignpostForestNorth`, `SignpostPetCity` | The signposts, beside the road at each entrance, facing into their own area. |
| `Places.SignpostDeepForest` | The Deep Forest's entrance signpost, just inside the west gap beside the path, facing west into it. |
| `Places.Signpost(Area)` | Town, Forest (its south signpost), PetCity or DeepForest; throws for Road and Home. |
| `enum BuildingKind` | `PlayerHouse`, `MillieHouse`, `Clinic`, `FurnitureShop`, `House`, `Fountain`, `PetStore`, `CityBlock`, `ClothingShop` (Abby's boutique mushroom house, added last). |
| `Building` | `Kind`, `Area`, `X`, `Z`, `Rotation` (yaw; at 0 the front faces -Z), `Width` (local X), `Depth` (local Z), `Height` (suggested drawing height), `Center`, `Front`, `ToWorld(localX, localZ)`, `Contains(x, z, margin)`. |
| `Buildings.All`, `Count`, `First(kind)`, `TownSquare` (0, 215), `CityPlaza` (0, -215) | Every building and the fountain, in a fixed order (Abby's clothing shop last): Town draws the Town ones, PetCity the PetCity ones, exactly there. |

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
  round eyes looking at the walkable ground), and little glowing mushroom lamps along both roads
  and the town's street — each kind one MultiMesh;
- the home interior: wood plank floor, cream walls with wooden skirting, four round windows, a
  door with a knob in the south wall, a ceiling seen only from inside, and a warm light.

Buildings, signposts, NPCs, sky and sun are other modules' (Town, PetCity, Forest, ...).
