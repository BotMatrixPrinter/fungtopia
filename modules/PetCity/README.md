# PetCity module

Draws Pet City, north of Mushroom Forest: tall, candy-bright cartoon city blocks with many windows
around a lilac plaza, Lucy's Pet Store at its north end, and a mix of many kinds of little fungi
in its planters, on its lamps and terraces and along its buildings.
Everything stands exactly where the Map module's `Fungtopia.Map.Buildings` (Area `PetCity`) says,
and the lamps, benches, planters and the statue exactly where, as turned and as large as its
`Fungtopia.Map.Props` (Area `PetCity`) say, so every one of them is a Map obstacle; Pet City places
nothing taller than 0.4 m of its own (the little fungi are at most 0.38 m). The ground, the plaza paving and the road are the Map's. At night the windows, the shop sign's
letters and hearts, the lamps and the bioluminescent fungi glow.

- `Contract/` — `Fungtopia.PetCity.Contract` (namespace `Fungtopia.PetCity`): empty; the module
  publishes no messages.
- `Core/` — `Fungtopia.PetCity.Core`: `PetCityModule`, the module's one `IModule`; `PetCityLayout`
  (what is drawn where); `Planting` (how things stand on the uneven ground); `PetCityLook` (the
  mushrooms' colors, gloss and glow, and the city's paints); `PetCityFungi` (the little fungi, the
  lamps' caps and the cap roofs, chosen from a fixed seed).
- `Godot/` — `Main.tscn` with `PetCityView`, `PetCityModels` (loads `petcity.glb` at run time, toon
  materials, night glow); `Preview.tscn` (`PetCityPreview`, for screenshots); `Shaders/` (toon
  surface, ink outline).
- `Models/` — `petcity.py`, the Blender script that builds and exports `petcity.glb` next to it.
- `Tests/` — `PetCityTests.cs`: xUnit tests of `PetCityLayout`, `PetCityLook`, `PetCityFungi` and
  `PetCityModule` with a fake `IMessageBus`, and of `petcity.glb`'s materials and objects.

Test it with `./test.sh PetCity` from the repository root.

## Core

`PetCityModule` reads `Fungtopia.DayNight.TimeOfDay` and keeps `IsNight` (`WindowsLit`); it does
nothing per tick and publishes nothing.

`PetCityLayout.Pieces` (fixed data, the same every run) holds one `Piece` per `Buildings.All`
building of `Area.PetCity`, in the Map's order, and nothing else:

- `Kind` (`PetStore`, `CityBlock`), `BuildingIndex` (into `Buildings.All`), `Position` (the building's x, z; y its `Footing`'s base),
  `Footing` (`FootingOf(building)`, `Planting.Rect`), `Yaw` (its `Rotation`), `Size` (`Width`, `Height`, `Depth`).
- City blocks: `Stories`, the upper stories that fit their height (`StoriesFor`: a 4 m shop floor,
  3 m stories, a 3 m roof; 3..10), and `Style` (one of four color schemes and roofs, in turn).
- `CityProps`: one `CityProp` per `Props.All` prop of `Area.PetCity`, in the Map's order and
  nothing else: `Kind`, `PropIndex` (into `Props.All`), `Position` (its x, z; y its `Footing`'s
  base), `Footing` (`FootingOf(prop)`, `Planting.Round`), `Yaw` (its `Rotation`), `Radius`, `Height`;
  `DrawnHeight(prop)` (its height plus the footing's extension) and `FlareRadius(prop)` (planters
  and the statue get a root flare; lamps and benches none). `PropModel(kind)` names the
  glb object that draws a kind and the radius and height it is built at (null for a kind Pet City
  has no model of: the view warns and draws nothing).
- `Planting`: a `Footing` (`Base`, `Low`, `High`, `Extension` = `High - Base`) from
  `Ground.HeightAt` sampled under a footprint: `Round(x, z, radius)` samples the center and rings
  out to the rim (at least 16 points each, at most 0.5 m apart, rings at most 1 m apart),
  `Rect(building)` a 7 x 7 grid over the footprint, edges and corners included. The base is the
  lowest sample less `Sink` (0.15 m), so no part of a base floats and none sinks deep; what stands
  there is extended down by `Extension`, so its height above the high side (`High`) stays the same.
- `PetCityLook`: the mushrooms, vivid as everywhere in the game (only these colors, nothing else
  of the city's): caps in saturated bright colors (`Cap` cherry red for the roof terrace's mushroom
  water tower and the pets' mushroom hats, `StoreCap` bright pink for the pet store's roof only,
  `StyleCaps` red, orange, blue, pink for a city block's cap roof by style, `LampCaps` pink, red,
  orange, purple, sky blue, golden yellow for the lamps), `Spots` pure white,
  `Stem` a clean light cream, a glossy highlight band (`CapGloss`) and a light rim (`CapRim`) on
  each cap, and the lamps' bulbs (`LampGlow`) glowing `LampGlowNight` (3.2, twice the store's
  ceiling lamps' `GlowNight`) at night. `MushroomMaterials` names the glb materials that draw
  mushrooms, `CapMaterials` the caps, `TintedMaterials` the little fungi's white caps the view tints
  per fungus (`FungusGlow` glowing `FungusGlowDay` 0.35, `FungusGlowNight` 2.4 at night).
  The rest of the city is vibrant like a cheerful cartoon, never washed out or neon: `Paints` (every
  other glb material but the pets': candy-bright paints, clean bright stone and cream `Neutrals`,
  lush `Greens` — `Leaf`, a yellow-green `LeafLight` highlight, a deeper `LeafDeep` — and `Flowers`
  in five hues), `StyleWalls` (mint, lilac, lemon, coral) and `StyleRoofs` (strawberry, tangerine,
  blueberry, sky) per style, `PlinthStone`, `PlanterFlare`, `StatueFlare`.
- `PetCityFungi` (fixed data from `Seed`, the same every run; `Build()` plants it afresh):
  - `Looks`, one `FungusLook` per `FungusKind` — fly agaric (red dome, white spots), bolete (fat
    brown dome), chanterelle (golden wavy funnel), parasol (tall, flat wide cap), ink cap (tall
    shaggy bell, inky rim), morel (honeycomb cone), enoki (tall thin stems, tiny caps), amethyst
    (purple wavy dome), puffball (ball), bracket (shelves on lamp posts), cup (scarlet), coral
    (branching pink/orange), jelly (glossy lobes), glow cap (blue/green cones that glow) — with its
    glb `Model`, height, width and tilt ranges and two tints (vivid; ink caps and puffballs pale).
  - `Groups` (`FungusGroup`: `Host`, `Main` kind, `Center`, its fungi) on every planter's soil, at
    every lamp's foot, on every third lamp's post (brackets), along every building's sides and back
    (never its front) and on the roof terraces' pots (`TerracePots`); each a single mushroom, a pair
    or a cluster of 3..5 mixing kinds. Groups closer than `NeighborDistance` (4 m) never share a
    main kind, and no two of a group are alike (same kind within `AlikeHeight` of the same height).
  - `All` (`Fungus`: `Kind`, `Host`, `Group`, `Position` (its base), `Yaw`, `Tilt` toward `TiltYaw`,
    `Height` (at most `MaxHeight`, 0.38 m), `Width` (proportion), `Tint`): small decoration only;
    on the ground outside every building's plinth and prop, or on a planter or lamp inside its
    footprint, so no obstacle changes.
  - `LampCapOf(cityProp)`: a lamp's cap (one of `LampCapModels`: dome, bell, flat, funnel, wavy,
    cone) and color (`PetCityLook.LampCaps`); lamps closer than `LampNeighborDistance` (14 m) share
    neither. `RoofOf(piece)`: `RoofTerrace` for odd styles, else one of `RoofCapModels` (dome, wavy,
    bell, flat), neighbors within `RoofNeighborDistance` (40 m) differing. Every shape is used.
- `Counter`: the pet store's counter, along the inside of its open front right behind
  `Places.LucySpot`, facing out like it.

## View

`PetCityView` plants everything on its `Footing`: each building's model stands at the footing's
high side on a stone plinth (`PlinthOverhang` wider than the footprint, a wider skirt step at its
foot) from the base in the ground up to `PlinthAbove` over the high side; each prop is scaled from
its base to `DrawnHeight`, so a lamp's cap stays at its full height above the high side, and
planters and the statue get a root flare (a collar from their radius out to `FlareRadius` in the
ground, `FlareAbove` high over the high side). It puts down each piece from `petcity.glb`: a city block is `Tower<Stories>` with a
roof (`PetCityFungi.RoofOf`) scaled to the building's exact width, height and depth, its walls and
roof recolored by style (`PetCityLook.StyleWalls` / `StyleRoofs`; a cap roof's mushroom cap in the
style's `PetCityLook.StyleCaps` color), its plinth clean bright stone (`PlinthStone` with a hint of
the wall's color); the pet store
is `PetStore` and `PetStoreInside` scaled to its footprint, the `Counter` at
`PetCityLayout.Counter` on the store's floor, and the pets on display (a kitten and a puppy in pet
beds, a bunny and a hamster in golden cages, the Mushroom Crow on its perch) breathing and bobbing a
little. Each lamp gets its own cap (`PetCityFungi.LampCapOf`, recolored) over its post and bulb.
The little fungi (`PetCityFungi.All`) are drawn as one MultiMesh per kind (no shadows), each
instance scaled to its height and width, turned, leaning, and its cap tinted (`instance_tint` in the
toon shader, from the instance's custom data). When `WindowsLit` changes, the shared window, lamp and sign
materials switch between daylight glass and a warm glow (the city lamps' bulbs twice as bright as
the store's lamps), the glow caps light up, and the store's light brightens. The toon shader (`Shaders/toon.gdshader`) gives
the mushroom caps (`PetCityLook.CapMaterials`) a soft specular band (`gloss`) and a light rim
(`rim_light`); every other surface has neither.

## Models

`blender -b --factory-startup --python modules/PetCity/Models/petcity.py` (from the repository
root) writes `petcity.glb`; commit it with the script. Its mushroom materials (`Cap`, `CapRoof`,
`StoreCap`, `LampCap`, `Spots`, `Stem`, `LampGlow`, `MorelPit`, `Ink`; `FungusCap` and `FungusGlow`
white, tinted by the view) carry `PetCityLook`'s colors and every other material but the pets' its
`Paints` (the tests check); mushroom materials are used by mushrooms only (the pets' mushroom hats
too); the pets' muzzles are `Cream`, the store's floor tiles `StoreRoof`, its ceiling lamps `Glow`,
the terraces' parapets `Roof`. Objects, each standing at the origin,
centered on its footprint, front toward Godot -Z (as a Map building at rotation 0):

- `Tower3` .. `Tower10`: a 12 m x 12 m city block, a shop floor (shop window, round-topped door,
  striped awning) and 3 .. 10 stories of arched windows with flower boxes on every side.
- `RoofCap` (a big white-spotted mushroom cap) and `RoofTerrace` (a parapet, a little mushroom water
  tower, pots of moss where the view plants fungi); `RoofCapWavy`, `RoofCapBell`, `RoofCapFlat`
  (the other cap roofs, 3 m tall at most).
- `PetStore` (18 m x 12 m x 11 m): an open front under a big "Pet Store" sign with little hearts,
  a scalloped striped awning, round heart windows, shelves of pet food, lamps, a bright pink
  white-spotted mushroom-cap roof. `PetStoreInside`: pet beds, cages, the crow's perch.
- `Counter`, `Kitten`, `Puppy`, `Bunny`, `Hamster`, `MushroomCrow` (the pets in the store's frame).
- `CityLamp` (0.6 m across, 2.6 m tall: a post and a glowing bulb) and its caps `LampCapDome`,
  `LampCapBell`, `LampCapFlat`, `LampCapFunnel`, `LampCapWavy`, `LampCapCone` (`LampCap`, some
  white-spotted; top at 2.6 m), `Bench`
  (1.6 m long, 0.9 m tall, seat along X), `Planter` (1.2 m across, 0.7 m tall: greens and flowers in
  every hue around soil at 0.53 m), `Statue` (a
  verdigris Mushroom Cat on a pedestal, 2.2 m across, 3.4 m tall): the Map's props, built at the
  Map's sizes.
- `FungusFlyAgaric`, `FungusBolete`, `FungusChanterelle`, `FungusParasol`, `FungusInkCap`,
  `FungusMorel`, `FungusEnoki`, `FungusAmethyst`, `FungusPuffball`, `FungusBracket` (against a stem
  at Godot +Z, sticking out toward -Z), `FungusCup`, `FungusCoral`, `FungusJelly`, `FungusGlowCap`:
  the little fungi, each 1 m tall at the origin (the view scales them).

Only the pets have eyes; buildings, caps, fungi, signs, lamps and the statue never do.

## Screenshots

`./screenshot.sh modules/PetCity/Godot/Preview.tscn` shows the plaza from the south entrance, on
the Map's ground. `PETCITY_PREVIEW=store`, `block`, `props` (the statue, benches and lamps), `fungi` (close up along a
city block's side, where a row of little fungi grows by its plinth), `planter` (the planter nearest
the plaza, its lamp and their fungi) or `slope` (the building
with the most drop under it, from its low corner, on its plinth) picks another view; add `night`
(for example `PETCITY_PREVIEW=store,night`) for the city at night.
