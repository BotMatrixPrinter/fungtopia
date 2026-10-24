# DeepForest module

The look of the **Deep Forest**, the Map's dark area west of Mushroom Forest where the hard
monsters live (`Fungtopia.Map.DeepForestLayout`, x -280..-110, z -90..90, and its overgrown path
x -112..-96 through the forest's west gap). Huge, old giant mushrooms in dark purples, deep reds
and browns stand on every one of the Map's Deep Forest obstacles; their caps and side caps close
into a dense canopy that throws real shadows, so it is clearly darker than Mushroom Forest by day.
The air is slightly foggy, faint glowing mushrooms and drifting spores give a little light, roots
arch from the stems, webs hang under some caps (the tarantulas live here), and the path in is
overgrown. Walking in is gradual: canopy, undergrowth, mist and fog thicken from the path to well
past the entrance. Mushrooms, plants and props never have eyes or faces.

- `Contract/` — `Fungtopia.DeepForest.Contract` (namespace `Fungtopia.DeepForest`): `DeepForestGloom`.
- `Core/` — `Fungtopia.DeepForest.Core`: `DeepForestModule`, the module's one `IModule` (builds
  the scenery once at start; nothing per tick, no messages), and `DeepForestScenery`, where every
  prop, fog volume and light goes (System.Numerics, deterministic).
- `Godot/` — `Main.tscn` with `DeepForestView`, which draws the scenery; `Shaders/` (toon surface
  with ink outlines, old caps, glowing mushrooms, ferns, webs, spores, mist).
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
`Glow`, `Owner`, the index in `Map.Ground.Obstacles` of the giant mushroom it grows on or -1),
`Fog` (`FogPatch`: box or ellipsoid, center, size, density) and `Lights` (`GlowLight`).

- `GiantStem`, `GiantCap`, `Skirt`: one per Deep Forest obstacle (the last `Ground.DeepForestTrunkCount`
  circles), in order, the stem exactly the circle (radius, center), 12-30 m tall over the high side
  of its foot, the cap 2.5-4.4 times the stem's radius plus 2 m; the skirt is its root flare, from
  the stem's base to a little over the high side, widening into the ground.
- Planting: everything standing on the uneven ground has its base at `BaseHeight(x, z, radius,
  sink)`: the lowest `Ground.HeightAt` under its round footprint (`GroundUnder`: the center and
  rings out to the rim, samples at most `FootprintSpacing` 0.5 m apart) minus a sink (`BaseSink`
  0.15 m for giant stems, skirts and roots, `SmallSink` 0.1 m for glowing mushrooms, toadstools and
  ferns), so no base floats where the ground drops away. Giant stems' footprints are `FootRadius`
  (1.7) stem radii (their flared foot); stems, glowing mushrooms and toadstools are extended down by
  the drop (`Scale.Y`) so their height over the high side stays the same, and caps, side stems,
  shelves and webs hang off the high side. Roots (`PlantRoot`) are pitched to follow the ground
  from start to tip and lowered until their whole length is in it.
- `BranchStem`, `BranchCap`: one to four side stems leaning out from high on each giant stem, each
  with a cap: the lower canopy. Deep inside at least 85% of the ground is under a cap; at the
  entrance much less.
- `Shelf`, `Root`, `Web`: bracket shelves on the stems, big arching roots from their feet (the
  Map draws the low ones), and webs on about a third of the giant mushrooms (under the cap, and
  sometimes a low one between the roots); never on a monster spawn spot.
- `Glow`, `Toadstool`, `Fern`: on the open ground only (`OpenGround`: in the area, at least
  `StemClearance` 0.3 m off every obstacle, and off the path's middle, `PathClearHalfWidth` 1.4 m
  each side), planted on `Ground.HeightAt` (footprints `GlowFoot`, `ToadstoolFoot`, `FernFoot` times
  their scale; a toadstool's stem is `ToadstoolStem * Scale.Y` tall). Glowing mushrooms huddle at some stem feet and in
  clusters on the ground and the path's edges; ferns are thickest along the path and at the
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
`MultiMeshInstance3D` from its unit mesh (`Stem`, `Skirt`, `Cap`, `Shelf`, `Glow`, `Web`, `Root`, `Fern`;
spores and mist are quads); toadstools reuse the stem and cap meshes. Stems, skirts, caps, shelves,
roots and glowing mushrooms have ink outlines; stems, caps, shelves and roots cast shadows. Its
own props take less ambient light the deeper in the gloom (`DeepForestGloom`'s ramp in the
shaders). The fog is `FogVolume` nodes with a lavender `FogMaterial`: DayNight turns volumetric
fog on with no density of its own, so it shows only here; it needs the Forward+ renderer (the
OpenGL renderer of `./screenshot.sh` ignores it, while the mist puffs show in both). The ground
and its low roots are the Map's; sun, sky and environment are DayNight's.

## Screenshots

`DEEPFOREST_PREVIEW=inside` (deep in, at a spawn clearing), `DEEPFOREST_PREVIEW=entrance` (on
the path at the forest's west gap, looking in) or `DEEPFOREST_PREVIEW=foot` (close to the foot of the
giant stem on the most sloped ground, from its low side) makes a preview camera current and hides the
screens drawn over the world, for example
`DEEPFOREST_PREVIEW=inside DAYNIGHT_HOUR=12 ./screenshot.sh "" deepforest_inside.png`.
