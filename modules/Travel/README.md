# Travel module

Fast travel between the three places of the world. A wooden signpost with three arrow boards
stands at each signpost of the Map (`Places.SignpostTown`, `SignpostForestSouth`,
`SignpostForestNorth`, `SignpostPetCity`). Near one a prompt "[Interact button] Map" shows (the
icon of the device in use only: E key, Xbox X, PlayStation Square, ...); Interact there,
or OpenMap (M / Back) anywhere outdoors, opens a full-screen map: three big circles on parchment,
Mushroom Town, Mushroom Forest and Pet City, labeled, with "you are here". Picking one closes the
map with a short fade and teleports the player to that place's signpost.

- `Contract/` — `Fungtopia.Travel.Contract` (namespace `Fungtopia.Travel`):
  - `MapState(bool Open)`: published by Travel every tick; `Open` is true while the map shows (from
    opening until the fade after a pick is black), so the HUD can hide and the player can stand still.
- It also publishes `Fungtopia.Input.UiFocus` (owner `TravelModule.FocusOwner`) open on the tick the
  map opens and closed on the tick it closes (`Close`, `OpenMap` again, indoors, or the teleport after
  a pick), once each, so Input stops walking, looking, jumping and firing while the map shows.
- `Core/` — `Fungtopia.Travel.Core`: `TravelModule` (the one `IModule`) and `MapLayout`.
- `Godot/` — `Main.tscn` with `TravelView`; `MapCanvas` (the map, drawn in code); `SignpostModel`
  (loads the model); `Preview.tscn` for screenshots; `Shaders/` (toon surface, ink outline).
- `Models/` — `signpost.py`, the Blender script that builds and exports `signpost.glb` next to it.
- `Tests/` — `TravelModuleTests.cs`: xUnit tests of `TravelModule` and `MapLayout` with a fake `IMessageBus`.

Test it with `./test.sh Travel` from the repository root.

## Core

`TravelModule` reads `Fungtopia.Input.PlayerInput` (`Interact`, `OpenMap`), `Fungtopia.Input.MenuNav` and
`Fungtopia.Player.PlayerState` (`Position`, `Area`, `Alive`), and the world from the Map Contract.
It publishes `MapState` every tick, `UiFocus` when the map opens or closes, and
`Fungtopia.Player.TeleportPlayer` once per trip. Each tick:

- **Prompt.** `NearSignpost` is the signpost within `PromptRadius` (2.5 m, horizontal) of the
  player, or -1; `ShowPrompt` while near one, alive, outdoors, with the map closed and no fade running.
- **Device.** `Device` is the last `Fungtopia.Input.ActiveDevice` (keyboard and mouse until one
  arrives). `Prompt` / `PromptRich(device)` is the signpost prompt and `MapHint` / `MapHintRich(device)`
  the map's hint line, RichTextLabel BBCode built with `Prompts.Rich` once per device (no allocation
  per tick), with only that device's icons: the prompt is the Interact icon (`PromptIconSize`, 40)
  and "Map"; the hint (`HintIconSize`, 44) is left click "Pick a place" (keyboard and mouse only),
  MenuLeft + MenuRight (and the left stick, Move, on pads) "Choose", Confirm "Travel", Back "Close".
- **Opening.** While closed, alive and not in `Area.Home`: `OpenMap`, or `Interact` near a
  signpost, opens the map. `Here` ("you are here") is the destination nearest the player (the area
  it is in; on a road the one it is least outside of); the selection starts there.
- **While open.** No teleport is sent until a place is chosen. The selection moves only by
  `Fungtopia.Input.MenuNav`, Input's paced menu step (one on a push, then one per 0.2 s while held,
  from the left stick, D-pad, arrow keys and W A S D alike): right / up toward Pet City, left / down
  toward Town, no wrapping; each message counts for its own tick only. `PlayerInput.Move` never moves it.
  Mouse hover (`Hover`), click (`Pick`) and confirm (`Confirm`) come from the view and apply at the next tick.
  `Interact` or `Confirm` picks the selected place. `Close()` (Esc / B), `OpenMap` again, or ending up indoors
  closes it without a trip.
- **A trip.** On a pick: `FadeOutSeconds` (0.3 s) to black with the map still shown, then the map
  closes and one `TeleportPlayer` goes to `ArrivalPoint(area)`: `ArrivalGap` (1.5 m) in front of
  the destination's signpost along its facing, on the ground, facing like the signpost (into its
  place) — Mushroom Town: `SignpostTown`, Mushroom Forest: its south entrance
  `SignpostForestSouth`, Pet City: `SignpostPetCity`. Then `FadeInSeconds` (0.4 s) back. Input
  during the fade is ignored. `Fade` (0..1) and `Traveling` tell the view.

Destinations are indexed in map order: `Destinations` = Town, Forest, PetCity (`IndexOf(area)`).
`MapLayout` puts the circles on a virtual 1920 x 1080 canvas (y down): Mushroom Town left, Mushroom
Forest raised in the middle, Pet City right, radius 215; `Hit(point)`, `ToCanvas(pixel, screen)`
(the canvas is scaled to fit and centered) and `Name(index or area)`. No allocations per tick.

## View

`TravelView` (a `Node3D`) builds at startup:

- a signpost at each of the four signposts, turned to its facing; its boards (top to bottom Pet
  City, Mushroom Forest, Mushroom Town) turn to point at the city plaza, the forest's center and
  the town square, each with the place's name on both faces (`Label3D`);
- the prompt, a cream rounded pill with an ink border over the near signpost (canvas layer 11),
  a `RichTextLabel` (`FitContent`) showing `Prompt`; the pill resizes to the icon and word;
- the map, `MapCanvas` on canvas layer 20: dimmed world behind parchment, the title, dotted roads
  between the circles, Mushroom Town (sky, clouds, houses with spotted mushroom roofs and eye
  windows), Mushroom Forest (three big spotted mushrooms with eyes on grass), Pet City (tall pastel
  buildings with windows, a mushroom dome, a paw print), each with its name on a ribbon, a bobbing
  pink pin over "You are here!", a pulsing gold glow on the selected one, and the hint line, a
  `RichTextLabel` showing `MapHint`, scaled with the canvas and centered on y 985 at any icon height;
- the fade, a plum full-screen rect on canvas layer 21.

While the map is open the mouse is freed (and captured again when it closes); mouse motion,
left clicks (handled, so the Input module does not take them to recapture the mouse), Enter / Space /
gamepad A (`Confirm`), Esc and gamepad B (`Close`) go to the Core. The view reads no arrows, W A S D,
D-pad or stick for choosing: those reach the Core only as `MenuNav`.

Screenshots: `./screenshot.sh modules/Travel/Godot/Preview.tscn` shows the open map;
`TRAVEL_PREVIEW=signpost` shows the Town signpost with its prompt instead; `TRAVEL_DEVICE=keyboard`
(default), `xbox`, `playstation` or `nintendo` picks the device the prompts show (the preview loads
the icons itself, as the Input view does in the game). Both labels pick up a new text when the
device changes.

## Models

`blender -b --factory-startup --python modules/Travel/Models/signpost.py` (from the repository
root) writes `signpost.glb`; commit both. A chunky wooden post on a grassy mound, topped with a red
mushroom cap with white spots and big round eyes looking forward, and three wooden arrow boards
(`Board0..2`, pivots on the post's axis, pointing +X) with nails. Loaded at run time from the
file's bytes (like the other modules' models) and drawn toon-shaded with ink outlines.

## Not here

The player standing still and the HUD hiding while `MapState.Open` is true are the Player's and
the HUD's to do: they read `MapState` from this Contract.
