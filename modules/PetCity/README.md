# PetCity module

Draws Pet City, north of Mushroom Forest: tall, colorful cartoon city blocks with many windows
around a lilac plaza, a paw-print fountain in its middle, and Lucy's Pet Store at its north end.
Everything stands exactly where the Map module's `Fungtopia.Map.Buildings` (Area `PetCity`) says;
the ground, the plaza paving and the road are the Map's. At night the windows, the shop sign's
letters and hearts and the lamps glow.

- `Contract/` — `Fungtopia.PetCity.Contract` (namespace `Fungtopia.PetCity`): empty; the module
  publishes no messages.
- `Core/` — `Fungtopia.PetCity.Core`: `PetCityModule`, the module's one `IModule`; `PetCityLayout`
  (what is drawn where).
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
building of `Area.PetCity`, in the Map's order, then the fountain:

- `Kind` (`PetStore`, `CityBlock`, `Fountain`), `BuildingIndex` (into `Buildings.All`; -1 for the
  fountain), `Position` (the building's x, z; y the lowest ground under its footprint, so no corner
  floats), `Yaw` (its `Rotation`), `Size` (`Width`, `Height`, `Depth`).
- City blocks: `Stories`, the upper stories that fit their height (`StoriesFor`: a 4 m shop floor,
  3 m stories, a 3 m roof; 3..10), and `Style` (one of four color schemes and roofs, in turn).
- The fountain stands on `Buildings.CityPlaza`, 7 m across. The Map has no obstacle for it, so
  walkers pass through it (the Map would need a circle there).
- `Counter`: the pet store's counter, along the inside of its open front right behind
  `Places.LucySpot`, facing out like it.

## View

`PetCityView` puts down each piece from `petcity.glb`: a city block is `Tower<Stories>` with a
roof (`RoofCap` or `RoofTerrace` by style) scaled to the building's exact width, height and depth,
its walls and roof recolored by style (mint/red, lilac/orange, yellow/blue, sky/pink); the pet store
is `PetStore` and `PetStoreInside` scaled to its footprint, the `Counter` at
`PetCityLayout.Counter` on the store's floor, and the pets on display (a kitten and a puppy in pet
beds, a bunny and a hamster in golden cages, the Mushroom Crow on its perch) breathing and bobbing a
little; the `Fountain` on the plaza. When `WindowsLit` changes, the shared window, lamp and sign
materials switch between daylight glass and a warm glow, the store's light brightens and the
fountain's light comes on.

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
- `Fountain`: a paw-print of pools with a stone bone on top and lamps on the rim.

Only the pets have eyes; buildings, caps, signs and lamps never do.

## Screenshots

`./screenshot.sh modules/PetCity/Godot/Preview.tscn` shows the plaza from the south entrance, on
the Map's ground. `PETCITY_PREVIEW=store`, `fountain` or `block` picks another view; add `night`
(for example `PETCITY_PREVIEW=store,night`) for the city at night.
