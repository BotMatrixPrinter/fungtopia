# Town module

Draws Mushroom Town, exactly where the Map module lays it out (`Fungtopia.Map.Buildings`, the
ones in `Area.Town`): your house, Millie's house next door, Dr. Jeremy's clinic, the furniture
shop's stall, Abby's clothing shop, the "Mushroom Munchies" food stall, the fountain in the square and four more houses, plus the
Map's Town props (`Fungtopia.Map.Props`, the ones in `Area.Town`: street lamps, benches, garden
fences, planters, crates), and small flower clumps. The street lamps (and every window) light up
at night. Nothing in the town moves or is walked into here: the Map's `Ground.Obstacles` already
cover the buildings and the props; the Home module handles the house door and the shop, the
signpost is the Travel module's.

The rule (Map README, **Props**): anything taller than about 0.4 m or wider than about 0.6 m is
solid and drawn only from the Map's data; Town scatters only flower clumps (0.5 m across, 0.35 m
tall), which are walked over.

- `Contract/` — `Fungtopia.Town.Contract` (namespace `Fungtopia.Town`): `TownLights`.
- `Core/` — `Fungtopia.Town.Core`: `TownModule`, the module's one `IModule`; `TownLayout`, the
  placement list; `TownGround`, how each placement stands on the uneven ground; `TownLook`, the
  mushroom caps' gloss and rim and how bright Glow and windows are.
- `Godot/` — `Main.tscn` with `TownView` (draws the placements, lamps' lights), `TownModels`
  (loads `town.glb` at run time, toon materials); `Preview.tscn` with `TownPreview` (for
  screenshots); `Shaders/` (toon surface, ink outline).
- `Models/` — `town.py`, the Blender script that builds and exports `town.glb` next to it.
- `Tests/` — `TownTests.cs`: xUnit tests of `TownLayout` (its props are exactly the Map's Town props), of `town.glb` having an object for every placement (and the food stall's materials: wood, spotted cap, sign, food, steam, lanterns), of the vivid mushrooms (`town.glb`'s caps saturated and bright, spots pure white, stems light cream, every cap material glossy and rim-lit, Glow twice as bright, non-mushroom colors unchanged), of `TownGround` (bases never float over the ground nor sink deep, at many spots over the town, roads and the rough Deep Forest), and of `TownModule` with a fake `IMessageBus`.

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
`Width`, `Depth`, `Height`, `BuildingIndex`, `PropIndex`), the same every run:

- first each Town building of `Buildings.All`, once, in order, at its exact position, rotation
  and size, with its index in `Buildings.All` (`TryPieceFor(BuildingKind)`: PlayerHouse,
  MillieHouse, Clinic, FurnitureShop, Fountain, House, ClothingShop, FoodStall; the four plain houses get
  looks 0..3). A kind Town does not draw (PetCity's, or one the Map adds later) is skipped, never
  thrown;
- then each Town prop of `Props.All`, once, in order, with its index in `Props.All`
  (`TryPieceFor(PropKind)`: Lamp → StreetLamp, Bench, FenceSegment → Fence, Planter, Crate; a kind
  without a model is skipped). `ForProp` puts it at the prop's `X`, `Z`, `Rotation` and `Height`;
  a lamp or planter is 2 x `Radius` across, a bench or fence 2 x `Radius` long (along local X;
  `BenchDepth` 0.62 m, `FenceDepth` 0.2 m deep), a crate a square with its corners on its circle;
- then the free decoration (both indexes -1): flower clumps (`FlowersSize`, under
  `DecorationMaxHeight` 0.4 m and `DecorationMaxWidth` 0.6 m) along each building's front either
  side of the door and round the square. Each passes `FlowersFit`: walkable town ground, off every
  obstacle (`Ground.Blocks`), `PathClearance` (1.8 m) from the middle of the town's dirt paths
  (`PathDistance`: the street from the entrance to the square and the path from the square to each
  front, as the Map draws them), 1.2 m from the doors, NPC spots (`FoodSpot` included) and the signpost, and not on
  another clump.

## View

`TownView` puts one copy of a `town.glb` object at each placement, turned by `Yaw` and scaled to the
placement's size, standing where `TownGround.Fit` says (below). Big stems, foundations and basins
(houses, the clinic, Abby's shop, the fountain, street lamps, planters: `HasRootFlare`) get a
stone skirt from just above their highest ground widening down into the ground, so they look rooted. While `LampsLit` the lamp bulbs and windows glow (fading over 1.5 s; `TownLook`: Glow 0.24 by day
and 4.4 at night, twice what it was so glowing mushrooms clearly glow, windows 0.084 and 1.54) and each
street lamp has a warm `OmniLight3D` at its bulb (0.8 of its height), the food stall one among
its lanterns (0.63 of its height); by day they are off. The sky and sun are DayNight's.

### Vivid mushrooms

The game's rule for every mushroom (Tony, 2026-10-25): caps in saturated, bright colors, white
spots pure white, stems a clean light cream, a soft glossy band and a light rim on each cap, glowing
mushrooms twice as bright; every other color stays. `TownModels` draws the `TownLook.Caps`
materials (`Cap`, `CapPink`, `CapWhite`, `CapOrange`, `CapLilac`, `CapBlue`, `CapYellow`, `CapMint`)
with the toon shader's `gloss` (`CapGloss`, a small specular band) and `rim_light` (`CapRim`), both 0
on everything else. Cushions, the outfits in Abby's window and the berry jars have their own
materials (`FabricPink`, `FabricYellow`, `FabricBlue`, `FabricOrange`, `Jar`), so they keep their
colors.

## Ground

`TownGround.Fit(placement)` samples `Ground.HeightAt` at the footprint's center, 16 points on its
edge (an ellipse, or a rectangle for the two stalls, benches, fences and crates, corners included) and
8 halfway in (`FootprintPoint`). The base (`Base`) goes at the lowest of them minus `Sink` (0.15 m;
flower clumps 0.05 m), and the top (`Top`) stays the placement's `Height` above the highest, so the
stem reaches down by the difference (`DrawnHeight` = `Top` - `Base`; the view stretches the model
to it) and looks as tall as it is from the high side. Nothing floats, no cap sinks.

## Models

`blender -b --factory-startup --python modules/Town/Models/town.py` (from the repository root)
writes `town.glb`; commit it with the script. Chunky mushroom buildings on stone foundations,
toon-shaded with ink outlines, and no eyes or faces on anything:

Cap colors (`town.py`): `Cap` cherry red (0.97, 0.1, 0.1), `CapPink` (1.0, 0.36, 0.64), `CapOrange`
(1.0, 0.46, 0.06), `CapLilac` (0.68, 0.34, 1.0), `CapBlue` (0.18, 0.52, 1.0), `CapYellow` (1.0, 0.8,
0.08), `CapMint` (0.26, 0.96, 0.72), `CapWhite` and `Spots` pure white; `Wall` (the stems) light cream
(1.0, 0.95, 0.84).

- **PlayerHouse** — the biggest: a cherry-red cap with white spots, a round red door with a stone step,
  a welcome mat, round windows with flower boxes, a chimney, a
  little mushroom mailbox.
- **MillieHouse** — a hot-pink cap with two chimneys shaped like cat ears, a round blue door.
- **Clinic** — white walls and a white cap with pale spots, a red cross on the cap's front, one
  standing on top and one over the arched door, and a little sign with a red cross by the path.
- **FurnitureShop** — a wooden stall: counter, back shelves with cushions, a pink and cream
  striped awning with a scalloped edge, a sign board, two chairs and three mushroom lamps on display.
- **Fountain** — a round stone basin of water, a spotted mushroom in the middle with water
  streaming from its rim.
- **ClothingShop** — Abby's boutique: a bright mint cap with white spots, pale pink walls and a
  shop front with a pink and white striped, scalloped awning, a lilac arched door, a big window
  with three little outfits on stands (a dress, a shirt with shorts, a sun hat and scarf), a round
  window, flower boxes, two little lanterns by the door, and a big clothes-hanger sign on the cap
  reading "Abby's". Its windows and lanterns light up at night with the rest of the town.
- **FoodStall** — "Mushroom Munchies" (4.2 x 2.4 m, 3 m tall): a cozy wooden stall on a sunk stone
  footing, under a red mushroom cap with white spots; a cream sign board reading "Mushroom Munchies"
  under the cap's front; on the planked counter two baskets of berry muffins, a steaming pot of
  soup (static steam puffs) and two pies, more pies and berry jars on the back shelf; four little
  lanterns (Glow) that light up at night with the street lamps.
- **House0..House3** — bright orange, purple, blue and yellow caps, round or arched doors, flower boxes,
  two with chimneys.
- **StreetLamp** (modelled 3.2 m, a glowing bulb under a spotted cap), **Bench** (on
  mushroom-stem legs), **Fence** (a 3.5 m picket segment, 0.8 m tall, a post at each end),
  **Planter** (a round stone planter of flowers, 1.2 m across), **Crate** (0.71 m, planks with dark
  edges), all scaled to the Map's sizes; **Flowers** (a 0.5 m clump of three flowers, decoration).

## Screenshots

`./screenshot.sh modules/Town/Godot/Preview.tscn` shows the town by day;
`TOWN_PREVIEW=night` at 22:00 with the lamps lit. `TOWN_VIEW` = `home`, `clinic`, `fountain`,
`houses`, `shop` (Abby's) or `food` (the food stall) frames a closer view (default `square`, over the town from its entrance).
