# DeepForest module

The look of the **Deep Forest**, the Map's dark area west of Mushroom Forest where the hard
monsters live (`Fungtopia.Map.DeepForestLayout`, x -280..-110, z -90..90, and its overgrown path
x -112..-96 through the forest's west gap). Huge, old giant mushrooms on clean light cream stems
stand on every one of the Map's Deep Forest obstacles, its medium mushrooms on every one of the
Map's Deep Forest `MediumMushroom` props, and small fungi of every kind dot the ground: many
recognizable kinds, cartoon-simplified and cute (fly agarics, boletes, chanterelles, parasols,
shaggy ink caps, morels, enoki clusters, amethyst caps, puffballs, bracket shelves, scarlet cups,
coral, jelly and bioluminescent glowing ones), as singles, pairs and clusters of different
heights, proportions and tilts, no two neighbors alike, in deep jewel colors that pop in the gloom
(purples, magentas, teals, cherry reds, golds; crisp pure white spots on some, a soft glossy
highlight and a light rim). The giants' caps and side caps close
into a dense canopy that throws real shadows, so it is clearly darker than Mushroom Forest by day.
The air is slightly foggy (a deep violet), glowing fungi (on the ground, some medium ones and some
giant caps; clearly glowing at night) and drifting spores give light, roots
arch from the stems, webs hang under some caps (the tarantulas live here), and the path in is
overgrown. Walking in is gradual: canopy, undergrowth, mist and fog thicken from the path to well
past the entrance. Mushrooms, plants and props never have eyes or faces.

- `Contract/` — `Fungtopia.DeepForest.Contract` (namespace `Fungtopia.DeepForest`): `DeepForestGloom`.
- `Core/` — `Fungtopia.DeepForest.Core`: `DeepForestModule`, the module's one `IModule` (builds
  the scenery once at start; nothing per tick, no messages), and `DeepForestScenery`, where every
  prop, fog volume and light goes (System.Numerics, deterministic).
- `Godot/` — `Main.tscn` with `DeepForestView`, which draws the scenery; `Shaders/` (toon surface
  with ink outlines, caps of every kind, ferns, webs, spores, mist).
- `Models/` — `deep_forest.py`, the Blender script that builds and exports `deep_forest.glb`
  (run `blender -b --factory-startup --python modules/DeepForest/Models/deep_forest.py` from the
  repository root; commit both).
- `Tests/` — `DeepForestTests.cs`: xUnit tests of the Contract, the scenery and the module with a
  fake `IMessageBus`.

Test it with `./test.sh DeepForest` from the repository root.

## Contract

| Member | What it is |
| --- | --- |
| `DeepForestGloom.InArea(x, z, margin = 0)` | Whether (x, z) is in the Deep Forest's rectangle or on its overgrown path (`Map.Areas.DeepForestPath`), `margin` meters from their edge. |
| `DeepForestGloom.At(x, z)` | How deep in the gloom (x, z) is, 0..1: 0 outside the area and at the forest end of the path (`StartX`, -96), rising smoothly westward to 1 at `FullX` (-135). For other modules that want to dim their light with it (DayNight, Camera). Pure, allocation-free. |

## Scenery

`DeepForestScenery.Create()` (called once by `DeepForestModule.Start`) gives `Of(PropKind)`, the
props of each kind (`Prop`: `Position`, `Rotation`, `Scale` for the unit models, `Color`, `Seed`,
`Glow`, `Owner`, the index in `Map.Ground.Obstacles` of the giant mushroom it grows on, or of the
medium mushroom's circle, or -1; for fungi `Species` (`Fungus`), and for caps `Shape` (`CapShape`,
the cap mesh), `Spots` and `Pattern` (`CapPattern`: spots, a parasol's scales, a morel's honeycomb,
an ink cap's ink)), `GroundSpots` (the small fungi's spots: center and `Look`, species and shape),
`Fog` (`FogPatch`: box or ellipsoid, center, size, density) and `Lights` (`GlowLight`).

- `GiantStem`, `GiantCap`, `Skirt`: one per Deep Forest obstacle (the last `Ground.DeepForestTrunkCount`
  circles), in order, the stem exactly the circle (radius, center), 12-30 m tall over the high side
  of its foot, the cap 2.5-4.4 times the stem's radius plus 2 m; the skirt is its root flare, from
  the stem's base to a little over the high side, widening into the ground.
- Kinds (`Fungus`) and looks (`Look`: a kind with its cap shape), chosen by a fixed seed per spot
  and then changed where needed so a spot never shares its look with its nearest neighbor (or the
  spot it is nearest to): the giants' caps (`Wavy`, `Dome`, `Flat`, `Funnel` or `Bell`) lean to
  amethyst, glowing and ink caps, each side cap differs from its giant's; the medium and small
  ones take every kind. A spot is one mushroom, a pair or three (enoki: 5-9 thin stems splaying
  out; puffballs, cups, coral and jelly: 1-4 bodies) of different heights, each with its own
  proportions (`Proportions` per kind) and a small tilt (the stem shortened so the tilted cap
  stays under the height). Colors per kind (`ColorsOf`): fly agarics red with white spots,
  boletes chestnut, chanterelles gold, parasols peach or plum with scales, ink caps pearly lilac
  with an inky rim, morels honey, enoki pink, peach or mint, amethysts purple or magenta,
  puffballs pearly, scarlet cups red, coral pink, orange or magenta, jelly orange, gold or violet,
  glowing ones cyan, blue, green or violet; brightest channel 0.4-1 and clearly saturated (ink
  caps and puffballs pale but bright). Bracket shelves (`Fungus.Bracket`) orange, teal, magenta or
  gold. Stems `StemCream` (the giant and side stems at most `GiantStemShade` darker, skirts a
  touch more). Glowing small and medium fungi glow `GlowStrength` to twice it (1.2-2.4), glowing
  giant caps softly (up to `GiantGlow`, 0.7), their stems faintly. Ferns are deep jewel greens
  (teal to emerald), roots a plum brown, mist a violet lavender; webs and spores keep theirs.
- `MediumStem`, `MediumCap`: one spot on each of the Map's Deep Forest `MediumMushroom` props
  (`Owner` its circle in `Ground.Obstacles`), exactly there: every stem's foot (`StalkFoot` stem
  radii) and every stemless body inside the prop's radius, the tallest 0.8-1 times the prop's
  height over the high ground under its circle, the biggest cap covering the circle.
- `SmallStem`, `SmallCap`: small fungi, scattered freely, so under `SmallMaxHeight` (0.38 m) over
  the high ground of their spot (`SmallSpotRadius`, 0.3 m); ferns stay under it too (`FernHeight`).
- Planting: everything standing on the uneven ground has its base at `BaseHeight(x, z, radius,
  sink)`: the lowest `Ground.HeightAt` under its round footprint (`GroundUnder`: the center and
  rings out to the rim, samples at most `FootprintSpacing` 0.5 m apart) minus a sink (`BaseSink`
  0.15 m for giant stems, skirts and roots, `SmallSink` 0.1 m for the medium
  and small fungi's stems and stemless bodies and ferns), so no base floats where the ground drops away. Giant stems' footprints are `FootRadius`
  (1.7) stem radii (their flared foot); stems and stemless fungi are extended down by
  the drop (`Scale.Y`) so their height over the high side stays the same, and caps, side stems,
  shelves and webs hang off the high side. Roots (`PlantRoot`) are pitched to follow the ground
  from start to tip and lowered until their whole length is in it.
- `BranchStem`, `BranchCap`: one to four side stems leaning out from high on each giant stem, each
  with a cap: the lower canopy. Deep inside at least 85% of the ground is under a cap; at the
  entrance much less.
- `Shelf`, `Root`, `Web`: bracket shelves on the stems, big arching roots from their feet (the
  Map draws the low ones), and webs on about a third of the giant mushrooms (under the cap, and
  sometimes a low one between the roots); never on a monster spawn spot.
- `SmallStem`, `SmallCap`, `Fern`: on the open ground only (`OpenGround`: in the area, at least
  `StemClearance` 0.3 m off every obstacle, and off the path's middle, `PathClearHalfWidth` 1.4 m
  each side), planted on `Ground.HeightAt` (footprints `StalkFoot` stem radii, a stemless body's
  radius, `FernFoot` times the fern's scale). Glowing fungi grow in clusters of spots at about half
  the stem feet, on the ground and on the path's edges; ferns are thickest along the path and at the
  entrance.
- `Spore`, `Mist`: billboards 0.3-6.5 m above the ground; spores gather near glowing clusters,
  mist puffs in hollows (`Hollowness`) and deeper in.
- `Fog`: a soft, low box (to `FogTop`, 6 m above the highest ground) over the area from 12 m past
  the entrance (density 0.035), a thinner one at the entrance (0.02), a thinner still over the
  path (0.008), and thicker ellipsoids in the hollows (0.05). None reaches past the area.
- `Lights`: up to `MaxLights` (16) soft omni lights over the biggest glowing clusters, 25 m apart.

## View

`DeepForestView` loads `Models/deep_forest.glb` at run time with `GltfDocument` from the file's
bytes (so a fresh checkout runs without an editor import) and draws each kind as one
`MultiMeshInstance3D` from its unit mesh (`Stem`, `Skirt`, `Shelf`, `Web`, `Root`, `Fern`; the
medium and small fungi's stems from `Stalk`; spores and mist are quads); caps are one MultiMesh per
cap mesh (`Cap` for `Wavy`, `Dome`, `Flat`, `Funnel`, `Bell`, `Cone`, `Ball`, `Cup`, `Coral`,
`Jelly`), for the big ones (giant, side, medium; with shadows) and the small ones (without), all
with `cap.gdshader` and per-instance custom data (seed, spots, glow, pattern). Stems, stalks,
skirts, caps, shelves and roots have ink outlines; stems, the medium stalks, caps, shelves and
roots cast shadows. Caps have their pattern (crisp pure white spots, scales, honeycomb or ink), a
soft glossy specular band and a light rim (`TOON_GLOSS` in `toon.gdshaderinc`, masked by
`ROUGHNESS`), and glowing ones emit their color, pulsing slowly; the stalks of glowing fungi glow
faintly (`toon_color.gdshader`). The soft lights over the glowing clusters have energy 2. Its
own props take less ambient light the deeper in the gloom (`DeepForestGloom`'s ramp in the
shaders). The fog is `FogVolume` nodes with a violet-lavender `FogMaterial`: DayNight turns volumetric
fog on with no density of its own, so it shows only here; it needs the Forward+ renderer (the
OpenGL renderer of `./screenshot.sh` ignores it, while the mist puffs show in both). The ground
and its low roots are the Map's; sun, sky and environment are DayNight's.

## Screenshots

`DEEPFOREST_PREVIEW=inside` (deep in, at a spawn clearing), `DEEPFOREST_PREVIEW=entrance` (on
the path at the forest's west gap, looking in) or `DEEPFOREST_PREVIEW=foot` (close to the foot of the
giant stem on the most sloped ground, from its low side) makes a preview camera current and hides the
screens drawn over the world, for example
`DEEPFOREST_PREVIEW=inside DAYNIGHT_HOUR=12 ./screenshot.sh "" deepforest_inside.png`.
