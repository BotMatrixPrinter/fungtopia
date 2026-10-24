# PetCity module

Draws Pet City, north of Mushroom Forest: tall, colorful cartoon city blocks with many windows
around a lilac plaza, and Lucy's Pet Store at its north end.
Everything stands exactly where the Map module's `Fungtopia.Map.Buildings` (Area `PetCity`) says,
and the lamps, benches, planters and the statue exactly where, as turned and as large as its
`Fungtopia.Map.Props` (Area `PetCity`) say, so every one of them is a Map obstacle; Pet City places
nothing taller than 0.4 m of its own. The ground, the plaza paving and the road are the Map's. At night the windows, the shop sign's
letters and hearts and the lamps glow.

- `Contract/` — `Fungtopia.PetCity.Contract` (namespace `Fungtopia.PetCity`): empty; the module
  publishes no messages.
- `Core/` — `Fungtopia.PetCity.Core`: `PetCityModule`, the module's one `IModule`; `PetCityLayout`
  (what is drawn where); `Planting` (how things stand on the uneven ground).
- `Godot/` — `Main.tscn` with `PetCityView`, `PetCityModels` (loads `petcity.glb` at run time, toon
  materials, night glow); `Preview.tscn` (`PetCityPreview`, for screenshots); `Shaders/` (toon
  surface, ink outline).
- `Models/` — `petcity.py`, the Blender script that builds and exports `petcity.glb` next to it.
- `Tests/` — `PetCityTests.cs`: xUnit tests of `PetCityLayout` and `PetCityModule` with a fake
  `IMessageBus`.

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
- `Counter`: the pet store's counter, along the inside of its open front right behind
  `Places.LucySpot`, facing out like it.

## View

`PetCityView` plants everything on its `Footing`: each building's model stands at the footing's
high side on a stone plinth (`PlinthOverhang` wider than the footprint, a wider skirt step at its
foot) from the base in the ground up to `PlinthAbove` over the high side; each prop is scaled from
its base to `DrawnHeight`, so a lamp's cap stays at its full height above the high side, and
planters and the statue get a root flare (a collar from their radius out to `FlareRadius` in the
ground, `FlareAbove` high over the high side). It puts down each piece from `petcity.glb`: a city block is `Tower<Stories>` with a
roof (`RoofCap` or `RoofTerrace` by style) scaled to the building's exact width, height and depth,
its walls and roof recolored by style (mint/red, lilac/orange, yellow/blue, sky/pink); the pet store
is `PetStore` and `PetStoreInside` scaled to its footprint, the `Counter` at
`PetCityLayout.Counter` on the store's floor, and the pets on display (a kitten and a puppy in pet
beds, a bunny and a hamster in golden cages, the Mushroom Crow on its perch) breathing and bobbing a
little. When `WindowsLit` changes, the shared window, lamp and sign
materials switch between daylight glass and a warm glow, and the store's light brightens.

## Models

`blender -b --factory-startup --python modules/PetCity/Models/petcity.py` (from the repository
root) writes `petcity.glb`; commit it with the script. Objects, each standing at the origin,
centered on its footprint, front toward Godot -Z (as a Map building at rotation 0):

- `Tower3` .. `Tower10`: a 12 m x 12 m city block, a shop floor (shop window, round-topped door,
  striped awning) and 3 .. 10 stories of arched windows with flower boxes on every side.
- `RoofCap` (a big spotted mushroom cap) and `RoofTerrace` (a parapet, a little mushroom water
  tower, potted mushrooms).
- `PetStore` (18 m x 12 m x 11 m): an open front under a big "Pet Store" sign with little hearts,
  a scalloped striped awning, round heart windows, shelves of pet food, lamps, a pink spotted
  mushroom-cap roof. `PetStoreInside`: pet beds, cages, the crow's perch.
- `Counter`, `Kitten`, `Puppy`, `Bunny`, `Hamster`, `MushroomCrow` (the pets in the store's frame).
- `CityLamp` (0.6 m across, 2.6 m tall: a glowing bulb under a spotted mushroom cap), `Bench`
  (1.6 m long, 0.9 m tall, seat along X), `Planter` (1.2 m across, 0.7 m tall), `Statue` (a
  verdigris Mushroom Cat on a pedestal, 2.2 m across, 3.4 m tall): the Map's props, built at the
  Map's sizes.

Only the pets have eyes; buildings, caps, signs, lamps and the statue never do.

## Screenshots

`./screenshot.sh modules/PetCity/Godot/Preview.tscn` shows the plaza from the south entrance, on
the Map's ground. `PETCITY_PREVIEW=store`, `block`, `props` (the statue, benches and lamps) or `slope` (the building
with the most drop under it, from its low corner, on its plinth) picks another view; add `night`
(for example `PETCITY_PREVIEW=store,night`) for the city at night.
