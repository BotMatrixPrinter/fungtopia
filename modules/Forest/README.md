# Forest module

The Mushroom Forest, the first place: a 200 m x 200 m square of gentle rolling ground with
winding dirt paths and clearings, giant mushrooms as trees, and a wall of giant mushrooms around
it. The Map module (`modules/Map`) now owns the world's ground and layout (the forest square,
its roads to Mushroom Town and Pet City, the Deep Forest), and the DayNight module owns the sun,
sky and environment. The Forest Contract forwards to the Map, so walkers in other modules that
use it keep working; new code should use `Fungtopia.Map` directly. The Core only remembers
whether it is night, for the glowing mushrooms.

- `Contract/` — `Fungtopia.Forest.Contract` (namespace `Fungtopia.Forest`), see below.
- `Core/` — `Fungtopia.Forest.Core`: `ForestModule`, the module's one `IModule` (no work per tick;
  reads `Fungtopia.DayNight.TimeOfDay` into `IsNight`), `ForestRing`, where the view's ring of
  giant mushrooms may stand and where it stands (`Mushrooms`), `ForestProps`, where props may
  stand, the west gap's overgrown trail and tiny dark mushrooms, `ForestScenery`, everything the view
  draws standing in the forest and how big (giant, medium and ring mushrooms, small decoration),
  `ForestFooting`, how each of them is planted on the uneven ground (see **Planting** below),
  `ForestSpecies`, the kinds of mushrooms and fungi and how each looks (see **Kinds** below),
  `ForestPalette`, the colors of the mushrooms and of everything else the forest draws, and the caps' shading (see **Colors** below), and `ForestRandom`, the fixed hash the layout uses.
- `Godot/` — `Main.tscn` with `ForestView`, which draws the forest from the Contract.
- `Tests/` — `ForestTests.cs`: xUnit tests of the Contract (its forwarding values equal the Map's; `Obstacles` are exactly the Map's obstacles in the forest area; at least 80, radii up to 3 m, inside the square or on its wall, the same on every call, off paths, clearings, road gaps and the start, with no fixed count, so they hold once the Map adds solid props),
  of `ForestRing` (it matches the Map's obstacles and walkable ground; it opens at the west gap just
  as wide as the Map's path and is closed along the rest of the strip), of `ForestProps` (no prop
  blocks the west gap or the trail), of `ForestScenery` (one medium cluster on exactly each of the
  Map's forest `MediumMushroom` props, as tall as it, every stem (a stemless fungus' whole body) inside its circle and filling most of it, a mix of singles, pairs, clusters and enoki tufts of different heights; many kinds everywhere (at least 8 kinds of giants, 10 of medium mushrooms, every kind among the tiny ones, none more than a quarter of the giants), domed, flat, funnel, bell, conical and wavy giant caps, every shape among the tiny ones; no tall mushroom and no patch of tiny fungi of the same kind as any of its 3 nearest; giants of varied heights, cap sizes and depths, slim and fat stems that keep their full foot, gentle leans; shelf fungi on 15-60 % of the giants, on the stem's surface between ground and cap; at least 300 tiny fungi along the paths and 100 in the clearings; giant stems are exactly the trunk circles and the stem mesh is
  never wider than its circle; ring mushrooms are drawn on the ring's circles; every drawn mushroom,
  and stems at 1500 sample positions over hills, the Deep Forest's rough ground and cliffs, have their base at or
  below the ground everywhere under the foot and at most 0.3 m below its lowest point, caps clear of the ground;
  big stems flare into the ground, sticking out of the stem where it meets the ground and never wider than their circle; all decoration is under 0.4 m tall and 0.6 m across; everything
  taller inside the square stands within an obstacle), of `ForestSpecies` (each kind's shapes, pattern and proportions), of `ForestPalette` (every kind's caps saturated and bright, glowing blue and green among them; lush saturated greens, more than before, yellow-green sunny patches, golden paths clearly lighter than grass and meadow, flowers in at least 7 hues; caps strongly saturated and bright, each more saturated than the color it replaced and no darker; reds pure cherry/tomato; red, orange/brown and purple caps, violet, pink and cyan glowing ones, dark but rich Deep Forest caps; pure white spots; light cream stems and gills; a gloss, a rim stronger than the toon light's, glowing caps twice as bright as before at night and no brighter by day; the view and shaders use them, the cap shader paints every pattern, stems and their outlines bend as `ForestScenery` says) and of `ForestModule` with a fake `IMessageBus`.

Test it with `./test.sh Forest` from the repository root.

## Contract

Meters, Y up, System.Numerics; north is -Z (Godot's forward), south is +Z. Everything is fixed:
the same on every run, computed from math only (no random seed, no clock). The Contract
references `Fungtopia.Map.Contract`; members marked *forwarding* just pass on the Map's values.

| Member | What it is |
| --- | --- |
| `Terrain.HeightAt(float x, float z)` | *Forwarding* to `Fungtopia.Map.Ground.HeightAt`. Pure, deterministic, allocation-free; defined everywhere. Inside the square within ±`Terrain.MaxAbsHeight` (1.9 m) and never steeper than about 1 in 6; beyond it the Map's roads, towns and cliffs. |
| `ForestBounds.HalfSize` | *Forwarding*: `Map.Areas.ForestHalfSize`, `100f`: the forest is the square \|x\| ≤ 100, \|z\| ≤ 100 (`Map.Areas.Forest`). Beyond it, walkable ground is the Map's (`Map.Areas.Walkable`). |
| `ForestBounds.PlayerStart` | `Vector3` on the ground (`Y == Map.Ground.HeightAt(X, Z)`) in the start clearing near the south edge, (0, h, 84). At least 8 m from any trunk. Unchanged in value. |
| `ForestBounds.Contains(x, z, margin = 0)` | *Forwarding* to `Map.Areas.Forest.Contains`: whether (x, z) is inside the square, `margin` meters from its edge. |
| `Obstacles.All` | *Forwarding*: every circle of `Map.Ground.Obstacles` whose center is inside `Map.Areas.Forest`, whatever it is, in the same order, as `ReadOnlySpan<Obstacle>` (`X`, `Z`, `Radius`, `Center`). Today exactly the forest trunks (the first `Map.Ground.ForestTrunkCount`): all inside the bounds, off paths and clearings, more than 1 m apart; any solid prop the Map adds in the forest joins them. |
| `Obstacles.Count` | Number of them (today 235, `Map.Ground.ForestTrunkCount`). |
| `Obstacles.Blocks(x, z, radius = 0)` | Whether a walker of `radius` at (x, z) overlaps one of `Obstacles.All` (`Map.Ground.Blocks` also checks buildings and the Deep Forest). Allocation-free. |
| `ForestLayout.Clearings` | `ReadOnlySpan<Clearing>` (`X`, `Z`, `Radius`); the first holds the start. |
| `ForestLayout.PathPoints`, `PathHalfWidth`, `PathDistance(x, z)`, `OnPath(x, z)` | The dirt paths: polylines of (x, z) points (NaN separates paths), 1.8 m half-width. |
| `ForestLayout.ClearingDepth(x, z)` | Meters inside the nearest clearing (negative outside all). |

## View

`ForestView` builds everything in code at startup (no imported assets, so a fresh checkout loads
headless): the forest square's floor from `Map.Ground.HeightAt` on a 1 m grid from -100 to 100
(the Map draws the ground around it on the same grid, so the seam matches; vertex-colored grass,
moss, meadow clearings and dirt paths), a giant mushroom on every forest trunk
(`ForestScenery.Giants`, the first `Map.Ground.ForestTrunkCount` of `Obstacles`; stem radius
= the circle's radius, the stem mesh bulging to exactly that at its foot and, by its kind, slimmer
above (`Slender`, `StemRadiusAt`) and leaning a little (`Lean`, at most `MaxGiantLean`), the cap
riding the leaning top; clean light cream stems, caps of the spot's kind (see **Kinds**), varied in
height, size and depth; shelf fungi (`ForestScenery.Shelves`) stacked up one side of about two in
five stems; no eyes or faces: only creatures have eyes), the ring of dense giant
mushrooms outside the square (`ForestRing.Mushrooms`: open in the Map's road gaps,
`Areas.ForestGapSouth` and `ForestGapNorth`, `ForestGapHalfWidth` 5 m to each side, and in its
west gap to the Deep Forest, `Areas.ForestGapWest` and `DeepForestPath`; off every road and
walkable ground by `WalkableClearance` 1.5 m and clear of all `Map.Ground.Obstacles`; not beside
the roads where the Map draws its own cliff mushrooms; a row of stems closes the narrow strip
between the square and the Deep Forest, its first stems right at the west gap's edges; within
`DarkReach` 16 m of the west gap their caps are dark but rich Deep Forest purples, blues and dusky reds; they stand
off all walkable ground; also of varied kinds), a medium cluster on every Map prop of kind
`MediumMushroom` in `Area.Forest` (`ForestScenery.MediumClusters`, their mushrooms
`MediumMushrooms`: one kind per prop, alone, a pair, three to five or an enoki tuft of 7-11 thin
leaning stems, the tallest `Height` tall (a stemless puffball or coral no taller than its circle
allows), the others lower, every stem inside the prop's circle and the group filling most of it;
caps may overhang), small walkable decoration (`ForestScenery.Decoration`: patches of tiny fungi of
every kind (`SmallPatches`, through the forest, every few meters along both edges of the paths and
in the clearings), small ferns, flowers, grass tufts, each under `DecorationMaxHeight` 0.4 m tall and
`DecorationMaxRadius` 0.3 m in radius) and fireflies.

The west gap: the Map's 8 m Deep Forest path (x -112..-96, z -4..4) runs through the ring
unblocked, and inside the square an overgrown little trail (`ForestProps.TrailPoints`, 1.1 m
half-width, patchy dark soil under moss) winds from the gap between the trunks to the forest's west
path at (-80, 26). A few tiny dark mushrooms (`ForestProps.DarkMushrooms`, under 0.4 m) stand beside it at the
gap's mouth. Small props
and all mushrooms are MultiMesh instances: one stem mesh (slimmed and leaned per instance in
`toon_color.gdshader` and the outline, `stem_bend.gdshaderinc`), one root flare, one mesh per cap
shape (`ForestMeshes.Cap(CapShape)`: dome, flat, funnel, bell, cone, wavy, ball, cup, coral) and one
shelf, all caps with one material. Shaders in `Godot/Shaders/`: soft two-band toon light with ink
outlines; mushroom caps add a soft glossy specular band and a light rim and paint each kind's
pattern (`mushroom_cap.gdshader`: white spots, parasol scales, morel honeycomb, shaggy ink-cap
scales running into ink at the rim, shelf bands, glossy jelly; INSTANCE_CUSTOM.z = pattern / 8,
.w how much the cap's color tints its underside). The glowing caps shine 4.8 times as bright at night as by day
(`ForestModule.IsNight`, fading over about two seconds; see **Colors**). The forest has no light, sky or
environment of its own: DayNight's global sun and `WorldEnvironment` light it.

Rule: anything drawn taller than 0.4 m (or wider than 0.6 m) inside the square is a mushroom on
one of the Map's obstacles, drawn exactly where and as large as the Map says; only the small
decoration is scattered freely, and walkers cross it. Tiny mushrooms, ferns and flowers stand only
where `ForestProps.Open` allows: inside the square, off obstacles, paths, the trail and the Deep
Forest path. Only ankle-high grass tufts grow on the trail and in the gap (the
overgrowth); off the square they keep off the Map's walkable ground.

## Kinds

`ForestSpecies` (Tony, 2026-10-26: "a variety of many different types of mushrooms and fungi"),
cartoon-simple and cute: fly agarics (red domes or flat caps, white spots), fat brown boletes,
golden funnel chanterelles, tall thin parasols with wide flat scaly caps, shaggy ink caps (tall
white bells inky at the rim), honeycomb morels, enoki tufts, purple amethysts (wavy, domed or
funnelled), puffballs, scarlet elf cups, pink and orange coral fungi, glossy jelly fungi, and
bioluminescent ones (blue, green, cyan, violet, pink); shelf fungi grow on giant stems. Each kind
has its shapes, pattern, proportions (`GiantCap`, `CapPerHeight`, `CapDepth`, `Slender`,
`StemOfCap`, `GiantHeight`), habit (single or pair, any, tuft) and sizes (`Tier`: giants, medium,
small; the stemless puffballs, cups, corals and jellies are never giants). `ForestScenery` gives
every tall spot (giants, medium props, the ring) and every patch of tiny fungi a kind from a fixed
hash, never the kind of any of its `Neighbors` (3) nearest, then varies size, depth, slimness and
lean per mushroom by the hash: the same forest every run.

## Colors

Every mushroom the forest draws takes its colors from `ForestPalette` (Core), the "vivid" rule
(Tony, 2026-10-25, the same for every mushroom in the game): caps clearly saturated and bright,
so they pop against the greens and browns: three cherry/tomato reds (about (0.94, 0.06, 0.1)),
three oranges and warm browns, two purples (`Caps`; fly agarics take the reds, amethysts the
purples) and each kind's own (`BoleteCaps`, `ChanterelleCaps` ... `ShelfCaps`, all saturated and
bright); glowing caps violet, pink, cyan, blue and green; by the west gap
dark but rich purples, blues and a dusky red. Spots pure white (1, 1, 1), stems a clean light cream
((1, 0.97, 0.88) to (0.98, 0.93, 0.8), the root flare a shade darker), gills pale cream. The cap
shader adds a soft white specular band on the lit side of the top (`CapGloss` 0.32) and a light
rim along the edge (`CapRim` 0.42, the other forest shaders' rim is 0.18); glowing caps' emission
is scaled by `GlowEmission` 2 times `DayGlow` 0.5 by day (as before, so they do not wash out in the
sun) and times `NightGlow` 2.4 at night (twice as bright as before), fading over about two seconds. No
mushroom has eyes or a face.

The rest of what the forest draws takes `ForestPalette`'s vibrant colors too (Tony, 2026-10-26:
"make the whole world more vibrant and colorful"): lush saturated grass (`Grass`) mottled with moss
and sunny yellow-green patches (`GrassLight`), bright meadows in the clearings, warm golden dirt
paths (`Dirt`, clearly lighter than grass and meadow so they read), the trail's soil, saturated
fern greens, flowers in nine hues on bright green stems. The forest draws no buildings or stone.

## Planting

Nothing the view stands on the ground floats where the ground drops away under its edge.
`ForestFooting.Lowest` samples `Map.Ground.HeightAt` at the footprint's center, 16 points on its
radius and 8 at half of it; the base goes at the lowest of them, `Sink` (0.15 m) deeper (15 % of
the height for things under 1 m: tiny mushrooms, ferns, flowers, grass, whose footprint is their root clump, 0.4 of their radius).
`ForestFooting.Plant` plants a mushroom: its stem runs from that base up to its cap, and its cap's
top stays `Height` above the ground at its center (so the stem above the high side looks as it
did), lifted only where the ground under the cap's rim (on the cliffs past the ring) would reach
it. Stems of `FlareMinRadius` (0.6 m) or more get a root flare (`ForestScenery.SkirtProfile`, one
more MultiMesh) sunk round its foot: the stem mesh's foot is 0.94 of its radius (`StemFootRadius`),
the flare's the full radius (never wider than its circle) from the base up to the high side of the
foot's ground, which `Plant` puts at most halfway up the flare, so the flare sticks out of the stem
all round where it meets the ground; above that it narrows smoothly to 0.55 of the radius, inside
the stem, at least 0.4 of the radius above the high side (at most 0.8 of the stem).

A `PreviewCamera` stands at `PlayerStart` at eye height (1.6 m) looking north up the path; without
`FOREST_PREVIEW` it is current only until another module makes its own camera current, and nothing
else changes. For screenshots, `FOREST_PREVIEW` picks and keeps it: `start` leaves it at the
player's start looking north up the path; `close` puts it near the giant mushroom closest to the
start, on the start's side, looking up at its cap. With either value the preview camera stays
current, the screens over the world (the main scene's `CanvasLayer`s and `Control`s: start screen,
HUD) are hidden, and the glowing caps jump to their day or night level without the fade.

## Screenshots

The forest is the main scene's start. From the repository root,
`FOREST_PREVIEW=start DAYNIGHT_HOUR=12 ./screenshot.sh "" forest_day.png` and
`FOREST_PREVIEW=start DAYNIGHT_HOUR=23 ./screenshot.sh "" forest_night.png` render the view up the
path by day and at night; `FOREST_PREVIEW=close DAYNIGHT_HOUR=12 ./screenshot.sh "" forest_close_day.png`
and `FOREST_PREVIEW=close DAYNIGHT_HOUR=23 ./screenshot.sh "" forest_close_night.png` the close-up of a
giant mushroom (`modules/Forest/Godot/Main.tscn` alone has no sun or sky).
