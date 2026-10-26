# Town module

Draws Mushroom Town, exactly where the Map module lays it out (`Fungtopia.Map.Buildings`, the
ones in `Area.Town`): your house, Millie's house next door, Dr. Jeremy's clinic, the furniture
shop's stall, Abby's clothing shop, the "Mushroom Munchies" food stall, the fountain in the square and four more houses, plus the
Map's Town props (`Fungtopia.Map.Props`, the ones in `Area.Town`: street lamps, benches, garden
fences, planters, crates), and small flower clumps and garden mushrooms of fourteen kinds. The
street lamps, the glowing mushrooms and every window light up at night. Nothing in the town moves or is walked into here: the Map's `Ground.Obstacles` already
cover the buildings and the props; the Home module handles the house door and the shop, the
signpost is the Travel module's. In town its song, `Audio/mushroom_town.mp3`, plays on loop,
fading in as you arrive and out as you leave.

The rule (Map README, **Props**): anything taller than about 0.4 m or wider than about 0.6 m is
solid and drawn only from the Map's data, exactly where and as large as it says; Town scatters
only flower clumps (0.5 m across, 0.35 m tall) and garden mushrooms (at most 0.38 m tall, 0.58 m
across), which are walked over.

- `Contract/` — `Fungtopia.Town.Contract` (namespace `Fungtopia.Town`): `TownLights`.
- `Core/` — `Fungtopia.Town.Core`: `TownModule`, the module's one `IModule` (lamps and the
  music's level); `TownLayout`, the
  placement list; `TownGround`, how each placement stands on the uneven ground; `TownLook`, the
  mushroom caps' gloss and rim and how bright the glowing bits and windows are; `TownMushrooms`,
  the garden mushrooms' kinds, sizes, spot seeds and groups.
- `Godot/` — `Main.tscn` with `TownView` (draws the placements, lamps' lights), `TownModels`
  (loads `town.glb` at run time, toon materials), `TownMusic` (loads the song at run time); `Preview.tscn` with `TownPreview` (for
  screenshots); `Shaders/` (toon surface with `vivid`, ink outline).
- `Audio/` — `mushroom_town.mp3`, Mushroom Town's music.
- `Models/` — `town.py`, the Blender script that builds and exports `town.glb` next to it.
- `Tests/` — `TownTests.cs`: xUnit tests of `TownLayout` (its props are exactly the Map's Town props), of `town.glb` having an object for every placement (and the food stall's materials: wood, spotted cap, sign, food, steam, lanterns), of the vivid mushrooms (`town.glb`'s caps saturated and bright, spots pure white, stems light cream, every cap material glossy and rim-lit, Glow and the glowing caps twice as bright), of the vibrant palette (lush and yellow-green leaves, golden wood, clean bright stone, six and more petal hues, candy paints, nothing neon, dark trims and lamp posts for contrast), of the garden mushrooms (every kind modelled at the size the game scales from; every kind in three or more sizes; domes, flat, funnel, bell, wavy and conical caps; singles, pairs and clusters of different heights; varied tilts and proportions; neighbors never alike; each group exactly what its spot's seed grows; bracket fungi and small mushrooms on every house, mushrooms in the planters), of `TownGround` (bases never float over the ground nor sink deep, at many spots over the town, roads and the rough Deep Forest), and of `TownModule` with a fake `IMessageBus` (lamps; the music level silent before the player
exists, rising to 1 over 120 ticks in town, falling to 0 over 120 ticks on the road, in the
forests, Pet City and the house; the song being an MP3 and broken bytes being refused).

Test it with `./test.sh Town` from the repository root.

## Contract

| Message | Who publishes it | What it is |
| --- | --- | --- |
| `TownLights` | Town, only when the lamps switch | `On`: the street lamps just lit (nightfall) or went out (dawn). |

## Core

`TownModule` reads `Fungtopia.DayNight.TimeOfDay`: `LampsLit` follows its `IsNight` (off until
the first night), and each change publishes one `TownLights`.

It also reads `Fungtopia.Player.PlayerState` and keeps `MusicLevel` (0..1) for the view: each
tick it rises by 1/`MusicFadeTicks` (120 ticks, 2 s) while the last `PlayerState.Area` is
`Area.Town` and falls as fast anywhere else (Road, Forest, PetCity, DeepForest and Home, the
house's inside). Before the first `PlayerState` (title/start screen) it stays 0. A fade turned
round midway goes back from where it was. `LooksLikeMp3(bytes)` says whether bytes start like an
MP3 (an ID3v2 tag or an MPEG audio frame header). No allocations per tick.

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
  another clump. Each clump's palette (`Variant`, 0..`FlowerVariants` - 1) comes from its spot's
  seed, unlike any clump within 1.6 m;
- then the garden mushrooms (`Piece` Mushroom, `Variant` its `MushroomKind`, `Group` its group):
  groups rooted round each building just outside its footprint, one each side of every fence
  segment, two by each lamp and planter, and thirty round the square, 18 m from its middle, each
  passing the same `FlowersFit` and touching no other decoration (a group's own members may nestle). See
  **Garden mushrooms** below.

### Garden mushrooms

`TownMushrooms.Shapes` lists fourteen kinds, cartoon-simple and cute, each a `town.glb` object
`Mushroom<Kind>` modelled `ModelWidth` across and `ModelHeight` tall: `FlyAgaric` (round red dome,
white spots, a skirt), `Porcini` (fat brown dome on a bulging stem), `Chanterelle` (golden wavy
funnel), `Parasol` (tall thin stem, wide flat tan cap with brown scales), `InkCap` (tall shaggy white
bell with an inky rim), `Morel` (honeycomb cone), `Enoki` (a cluster of thin stems with tiny caps),
`Amethyst` (wavy purple cap), `Puffball` (warty white ball), `CupFungus` (scarlet cups on moss),
`Coral` (branching pink with orange tips), `Jelly` (wavy golden blobs on a twig), `GlowBlue`
(conical blue caps) and `GlowGreen` (wavy green caps), the last two glowing (their caps are
`TownLook.Glows`, lit with the lamps: 0.24 by day, 4.4 at night). Stems are the clean cream `Wall`;
every cap material is in `TownLook.Caps` (glossy band, light rim).

Each group's spot (x, z) has a fixed seed (`Seed`, a hash of its position to the centimeter): its
kind is `Seed % KindCount`, or, when a group of that kind is within `NeighborDistance` (3 m), the
next free one `KindStep` (5) kinds on, so no two neighbors are alike. `GroupSize` gives a single, a
pair or a cluster of 3..5 (the already-clustered kinds: single or pair); `Member` gives each its
offset (within `GroupRadius`, 0.36 m), yaw, tilt (at most `MaxTilt`, 0.26 rad, the smaller ones
leaning out), size (from the kind's `MinScale`..`MaxScale`, each member clearly shorter than the one
before) and proportions (0.8..1.2 times as wide). The same every run. They stand like flower clumps
(`FlowersSink`, 0.05 m).

The houses' stems grow bracket fungi (three stacks of shelves: either side of the front and at
the back) and, just past their stone foundations (inside their footprints), seven small mushrooms
of a mix of kinds (each house its own); the planters a
fly agaric and an amethyst among their flowers. These are part of their solid models, so nothing
changes where you can walk.

## View

`TownView` puts one copy of a `town.glb` object at each placement (sharing its meshes), turned by
`Yaw`, leaned by `Tilt` and scaled to the placement's size (a mushroom from its kind's model size), standing where `TownGround.Fit` says (below). Big stems, foundations and basins
(houses, the clinic, Abby's shop, the fountain, street lamps, planters: `HasRootFlare`) get a
stone skirt from just above their highest ground widening down into the ground, so they look rooted. While `LampsLit` the lamp bulbs, glowing mushrooms and windows glow (fading over 1.5 s; `TownLook`: Glow 0.24 by day
and 4.4 at night, twice what it was so glowing mushrooms clearly glow, windows 0.084 and 1.54) and each
street lamp has a warm `OmniLight3D` at its bulb (0.8 of its height), the food stall one among
its lanterns (0.63 of its height); by day they are off. The sky and sun are DayNight's.

### Music

`TownMusic.Load` reads `Audio/mushroom_town.mp3`'s bytes (`FileAccess.GetFileAsBytes`) into an
`AudioStreamMP3` with `Loop` on (seamless restart), the way `TownModels` loads `town.glb`, so a
fresh checkout plays it without an editor import. A missing file, or bytes that are no MP3
(`LooksLikeMp3`), log one `WARNING` and the town stays silent. `TownView` plays it through an
`AudioStreamPlayer` at `FullVolumeDb` (-8 dB, medium) plus `LinearToDb(MusicLevel)`; at level 0
the stream is paused and resumes where it stopped.

### Vivid mushrooms

The game's rule for every mushroom (Tony, 2026-10-25): caps in saturated, bright colors, white
spots pure white, stems a clean light cream, a soft glossy band and a light rim on each cap, glowing
mushrooms twice as bright. `TownModels` draws the `TownLook.Caps`
materials (`Cap`, `CapPink`, `CapWhite`, `CapOrange`, `CapLilac`, `CapBlue`, `CapYellow`, `CapMint`,
and the garden mushrooms' `CapBrown`, `CapGold`, `CapTan`, `CapMorel`, `CapAmethyst`, `CapScarlet`,
`CapCoral`, `CapJelly`, `CapBracket`, `CapGlowBlue`, `CapGlowGreen`)
with the toon shader's `gloss` (`CapGloss`, a small specular band) and `rim_light` (`CapRim`), both 0
on everything else. Cushions, the outfits in Abby's window and the berry jars have their own
materials (`FabricPink`, `FabricYellow`, `FabricBlue`, `FabricOrange`, `Jar`).

### Vibrant colors

Tony, 2026-10-26: everything Town draws is richer, more saturated and brighter, like a cheerful
cartoon, and still readable: lush bright greens (`Leaf` (0.26, 0.8, 0.2)) with yellow-green
highlights (`LeafLight` (0.66, 0.95, 0.16)) in the flower clumps, planters and leaves; warm golden
`Wood` (0.88, 0.54, 0.24) and `Soil`; clean bright `Stone` (0.84, 0.81, 0.76); candy-bright doors,
awnings, mats, cushions and fabrics (saturation 0.55..0.9, never neon); flowers in white and six
hues (`PetalPink`, `PetalYellow`, `PetalBlue`, `PetalPurple`, `PetalOrange`, `PetalRed`), the
clumps in four palettes; the houses' round window frames painted in their door's color. Every
surface is drawn `TownLook.Vividness` (1.15) times as far from grey as its material (the toon
shader's `vivid`; whites and greys unchanged), so the filmic tonemapping does not wash it out. Dark trims (`WoodDark`, `StoneDark`) and dark teal lamp posts (`Metal`)
keep the contrast; the toon shading and ink outlines are unchanged. (The ground and its dirt paths
are the Map module's.)

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
  edges), all scaled to the Map's sizes; **Flowers0..3** (a 0.5 m clump of three flowers in grass
  and leaves, four palettes, decoration); **Mushroom<Kind>** (the fourteen garden mushrooms, see
  **Garden mushrooms**). Every mushroom house has bracket fungi on its stem and small mushrooms on
  its foundation.

## Screenshots

`./screenshot.sh modules/Town/Godot/Preview.tscn` shows the town by day;
`TOWN_PREVIEW=night` at 22:00 with the lamps lit. `TOWN_VIEW` = `home`, `clinic`, `fountain`,
`houses`, `shop` (Abby's) or `food` (the food stall) frames a closer view (default `square`, over the town from its entrance).
