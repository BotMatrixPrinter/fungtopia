# Town module

Draws Mushroom Town, exactly where the Map module lays it out (`Fungtopia.Map.Buildings`, the
ones in `Area.Town`): your house, Millie's house next door, Dr. Jeremy's clinic, the furniture
shop's stall, the fountain in the square and four more houses, plus street lamps, benches, picket
fences and flower beds. The street lamps (and every window) light up at night. Nothing in the
town moves or is walked into here: the Map's `Ground.Obstacles` already cover the buildings; the
Home module handles the house door and the shop, the signpost is the Travel module's.

- `Contract/` — `Fungtopia.Town.Contract` (namespace `Fungtopia.Town`): `TownLights`.
- `Core/` — `Fungtopia.Town.Core`: `TownModule`, the module's one `IModule`; `TownLayout`, the
  placement list.
- `Godot/` — `Main.tscn` with `TownView` (draws the placements, lamps' lights), `TownModels`
  (loads `town.glb` at run time, toon materials); `Preview.tscn` with `TownPreview` (for
  screenshots); `Shaders/` (toon surface, ink outline).
- `Models/` — `town.py`, the Blender script that builds and exports `town.glb` next to it.
- `Tests/` — `TownTests.cs`: xUnit tests of `TownLayout` and of `TownModule` with a fake `IMessageBus`.

Test it with `./test.sh Town` from the repository root.

## Contract

| Message | Who publishes it | What it is |
| --- | --- | --- |
| `TownLights` | Town, only when the lamps switch | `On`: the street lamps just lit (nightfall) or went out (dawn). |

## Core

`TownModule` reads `Fungtopia.DayNight.TimeOfDay`: `LampsLit` follows its `IsNight` (off until
the first night), and each change publishes one `TownLights`. Its Tick does nothing; no
allocations per tick.

`TownLayout.All` is a plain list of `TownPlacement`s (`Piece`, `Variant`, `X`, `Z`, `Yaw`,
`Width`, `Depth`, `Height`, `BuildingIndex`), the same every run:

- first each Town building of `Buildings.All`, once, in order, at its exact position, rotation
  and size, with its index in `Buildings.All` (`TryPieceFor(kind)`: PlayerHouse, MillieHouse,
  Clinic, FurnitureShop, Fountain, House; the four plain houses get looks 0..3). A kind Town
  does not draw (yet), such as ClothingShop or one the Map adds later, is skipped, never thrown;
  props still keep clear of its footprint;
- then the props (`BuildingIndex` -1): street lamps in a ring round the square and at both front
  corners of every building, benches on the square facing the fountain, flower beds either side
  of each front door and round the square, picket fences round the back and sides of the four
  houses' yards, behind your and Millie's shared back garden, and either side of the street
  where it comes into town.

Every prop passes `PropFits`: in the Town area on walkable ground, clear of every building's
footprint (`Clearance` 0.6 m), its center not in an obstacle, `PathClearance` (1.8 m) from the
middle of the town's dirt paths (`PathDistance`: the street from the entrance to the square and
the path from the square to each front, as the Map draws them), 1.2 m from the doors, NPC spots
and the signpost, and not overlapping another prop.

## View

`TownView` puts one copy of a `town.glb` object at each placement (on the lowest ground under a
building's footprint, its foundation reaching into the rest), turned by `Yaw` and scaled to the
placement's size. While `LampsLit` the lamp bulbs and windows glow (fading over 1.5 s) and each
street lamp has a warm `OmniLight3D`; by day they are off. The sky and sun are DayNight's.

## Models

`blender -b --factory-startup --python modules/Town/Models/town.py` (from the repository root)
writes `town.glb`; commit it with the script. Chunky mushroom buildings on stone foundations,
toon-shaded with ink outlines, and no eyes or faces on anything:

- **PlayerHouse** — the biggest: a red cap with cream spots, a round red door with a stone step,
  a welcome mat, round windows with flower boxes, a chimney, a
  little mushroom mailbox.
- **MillieHouse** — a pink cap with two chimneys shaped like cat ears, a round blue door.
- **Clinic** — white walls and a white cap with pale spots, a red cross on the cap's front, one
  standing on top and one over the arched door, and a little sign with a red cross by the path.
- **FurnitureShop** — a wooden stall: counter, back shelves with cushions, a pink and cream
  striped awning with a scalloped edge, a sign board, two chairs and three mushroom lamps on display.
- **Fountain** — a round stone basin of water, a spotted mushroom in the middle with water
  streaming from its rim.
- **House0..House3** — orange, lilac, blue and yellow caps, round or arched doors, flower boxes,
  two with chimneys.
- **StreetLamp** (3.2 m, a glowing bulb under a spotted cap), **Bench** (on mushroom-stem legs),
  **Fence** (a 2 m picket segment), **FlowerBed** (2 x 0.8 m, round flowers).

## Screenshots

`./screenshot.sh modules/Town/Godot/Preview.tscn` shows the town by day;
`TOWN_PREVIEW=night` at 22:00 with the lamps lit. `TOWN_VIEW` = `home`, `clinic`, `fountain` or
`houses` frames a closer view (default `square`, over the town from its entrance).
