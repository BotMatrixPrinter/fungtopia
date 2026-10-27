# Home module

The player's house in Mushroom Town: the front door (in and out by teleporting), the furniture
shop in town, arranging furniture on the home's floor grid, sleeping in a bed, the pets that stay at home, and the
wardrobe (the Wardrobe module's screen for changing your look). The house starts cozy but sparse:
a basic bed under the round window and a big wooden wardrobe in the north-east corner (the room,
its windows and its door are drawn by the Map module). The furniture is saved with the game
through the Save module (see **Saving** below).

- `Contract/` — `Fungtopia.Home.Contract` (namespace `Fungtopia.Home`): `HomeState`.
- `Core/` — `Fungtopia.Home.Core`: `HomeModule`, the module's one `IModule`; `Furniture` (the
  catalog), `HomeRoom` (the floor grid), `HomeText` (the texts, and the prompts and hint lines as RichTextLabel BBCode with button icons per device), `HomeSave` (the saved furniture's JSON).
- `Godot/` — `Main.tscn` with `HomeView` (furniture, the catalog book, pets at home, the grid and
  cursor while decorating), `HomePanels` (prompt, shop catalog, decorating picker and decorating help, a
  `CanvasLayer` on layer 11); `HomePetModel`, `HomeModels` (loads .glb models at run time, toon
  materials), `HomeLook` (each material's glow, gloss and rim, plain C#); `Preview.tscn` (a decorated room, for screenshots); `Shaders/` (toon surface, ink
  outline, floor grid).
- `Models/` — `furniture.py`, the Blender script that builds and exports `furniture.glb` next to it.
- `Tests/` — `HomeModuleTests.cs`: xUnit tests of `HomeModule` with a fake `IMessageBus` (which
  also answers `SpendCoins` like the wallet and records `UiFocus`), and `MushroomLookTests`
  (`furniture.glb`'s colors and faces, read back, and `HomeLook`).

Test it with `./test.sh Home` from the repository root.

## Contract

| Message | Who publishes it | What it is |
| --- | --- | --- |
| `HomeState` | Home, once every tick | `ItemsOwned` (furniture owned, placed or not, the starting bed included), `ItemsPlaced` (standing in the house), `Decorating` (decorating is open: the picker or placement mode). |

## Core

`HomeModule` reads `Fungtopia.Player.PlayerState`, `Fungtopia.Input.PlayerInput` (`Interact`), `Fungtopia.Input.MenuNav` (menu steps), `Fungtopia.Input.ActiveDevice` (`Device`, for the prompts),
`Fungtopia.Wallet.WalletState` and `SpendResult`, `Fungtopia.Pet.OwnedPets`,
`Fungtopia.Wardrobe.WardrobeState`, `Fungtopia.StartScreen.StartScreenState` and `Fungtopia.Save.CollectSave` and
`RestorePart`; it publishes `Fungtopia.Save.SavePart`, `Fungtopia.Player.TeleportPlayer`, `Fungtopia.Player.HealPlayer`, `Fungtopia.DayNight.Sleep`,
`Fungtopia.Wallet.SpendCoins`, `Fungtopia.Wardrobe.OpenWardrobe`, `Fungtopia.Pet.SetActivePet`, `Fungtopia.Input.UiFocus`, `Fungtopia.Input.InteractAvailable` and `HomeState`, and takes the
world's spots from the Map Contract (`Places`, `Areas`). An Interact press is acted on at the
next tick, for whatever `Prompt` shows then (only while the player is alive, and never while
the start screen is open, below):

- **Start screen.** While `StartScreenState.Open` (`StartScreenOpen`, the character creator is
  showing) there is no prompt and Interact does nothing (no door, shop, decorating or wardrobe;
  a press made then is dropped, not acted on after "Start!"); `StartDecorating` is refused, and
  the shop and placement mode close if they were open.

- **Door.** Within 1.5 m of `Places.HomeDoor`: "[Interact] Enter" teleports the player to
  `Places.HomeInterior`. Inside, within 1.2 m of `Places.HomeExit` (the spot by the door):
  "[Interact] Leave" teleports the player out to `Places.HomeDoor`, facing the street. (The Map
  defines `HomeExit` as the inside spot that leaves the house, so leaving lands at `HomeDoor`.)
- **Shop.** Within 1.5 m of `Places.FurnitureShopSpot`: "[Interact] Shop" opens the catalog panel
  (`ShopOpen`); it closes with Back or once the player is 3 m away. Interact buys the highlighted
  item (`ShopSelection`, `MoveShopSelection`): one `SpendCoins(RequestId, price, name)`, and
  when its `SpendResult` is `Ok` the item goes into the inventory (owned, not placed);
  `LastShopResult` is `Bought`, `TooFewCoins` or `Full` (64 items at most).
- **Smart A.** While a prompt shows (`Prompt` is not `None`: the door outside or inside, the shop,
  decorating, the wardrobe, a bed or a pet at home) the module holds `Fungtopia.Input.InteractAvailable(InteractOwner,
  true)` (7461003), published on the tick the prompt appears and released (false) on the tick it
  goes, so the gamepad's A button interacts there instead of jumping.
- **Menus and UiFocus.** The shop's panel (`ShopFocusOwner`) and decorating (`DecorateFocusOwner`,
  held from opening the picker until decorating closes, through placement mode and back) each publish `UiFocus(owner, true)` at the tick they open and
  `UiFocus(owner, false)` at the tick they close, however they close (Back, walking away, leaving
  the house, fainting, a wardrobe screen or the start screen), so the Input module stops walking,
  looking, jumping and firing while one is open. Selections move only on the Input module's
  `MenuNav` steps (one when a direction is first pushed, then at most one per 0.2 s while held,
  from the left stick, D-pad, arrow keys and WASD alike): in the shop and the decorating picker each
  `Vertical` step moves the highlight one line (wrapping), in placement mode each step moves the cursor one cell; with no
  panel open they do nothing. The module reads no stick or keys of its own for this. `Confirm()` (Enter; a gamepad's A arrives as Interact)
  acts like an Interact press at the next tick, only while a panel is open.
- **Catalog.** `Furniture.ForSale`, prices in M¢: Mushroom Bed 300, Rug 120, Lamp 150, Table 200,
  Chair 100, Potted Mushroom 80, Painting (on an easel) 250, Pet Bed 180, Bookshelf 220,
  Toadstool Stool 90. The Basic Bed comes with the house and is not sold.
- **Decorating.** Inside, within 1.2 m of the little catalog book on its stand (south wall, east
  of the door, `HomeRoom.BookPosition`): "[Interact] Decorate" opens decorating at its **picker**
  (`Picking`): a list of the owned furniture not yet placed, one line per kind with how many
  (`PickerEntries`, `PickerEntry` `Kind` and `Count`, in `FurnitureKind` order; `HomeText.PickerLine`,
  e.g. "Chair x2"), then a last line "Move placed items" (`PickerSelection == PickerCount`); with
  nothing to place the list says "Everything is placed" above that line. `MenuNav` moves the
  highlight (`MovePickerSelection`), and Interact / Confirm (`Choose()`) holds the first unplaced
  item of the highlighted kind (`Held`; `Choose(kind)` does it directly) and switches to
  **placement mode**, or on "Move placed items" (`MovePlacedItems`) opens placement mode with
  nothing held. There `MoveCursor` moves on the 0.5 m floor grid (24 x 20 cells over `Areas.Home`,
  staying inside), `Rotate` turns the held item a quarter turn (odd turns swap its footprint), and
  Interact (`PlaceOrPickUp`) places it if it fits, or with nothing held picks up the placed item
  under the cursor (`Highlighted`) to move it. Placing an item chosen in the picker opens the
  picker again; placing one picked up in placement mode stays there with nothing held. An item
  fits (`CanPlace`) only inside the room, off the door's way in (2 m wide, from the door to 0.5 m
  past `HomeInterior`), off the catalog book's stand, and overlapping no other furniture (rugs
  included). Back in placement mode returns to the picker (a held item goes back to the
  inventory); Back in the picker closes decorating. Leaving the house or fainting ends it too.
- **The mouse in decorating.** While the picker or placement mode is open the mouse cursor shows
  (the view frees it; the Input module captures it again when decorating, the last menu, closes, and
  does not take its left clicks meanwhile). In the picker `HoverPicker(line)` highlights the line
  under the mouse and `ClickPicker(line)` highlights it and chooses it like Confirm (next tick);
  off the lines nothing happens. In placement mode `PointAt(floorPoint)` (or `PointAt(origin,
  direction)`, the ray from the current camera through the mouse onto the floor plane at
  `Areas.HomeFloorY`, `HomeRoom.TryFloorPoint`) moves the cursor to `HomeRoom.CellUnder(point, width,
  depth)`: a pure function giving the north-west cell of the footprint centered as near as the 0.5 m
  grid allows on the point (a 1 x 1 cursor: the cell under it), clamped inside the room. The held item
  moves with it (green fits, red does not). `ClickFloor(point)` points there and acts like Interact at
  the next tick (places if it fits; with nothing held picks up the placed item there);
  `MouseRotate()` (the middle button) is `Rotate`. While the mouse was used last (`MouseActive`),
  `Rotate` keeps the turned footprint centered on the pointed spot. A `MenuNav` step or a gamepad
  clears `MouseActive`: the device used last moves the cursor, and keyboard and gamepad keep working.
  `MouseHints` (keyboard and mouse in use and `MouseActive`) switches the hints to
  `HomeText.MouseHelp(held, overItem)` ("Left click Place", "Middle click Rotate"; with nothing held
  over an item "Left click Pick up" and "[R] Put away"; Back always) and `PickerMouseHelp()`.
- **Putting away.** In placement mode with nothing held, the placed item under the cursor
  (`Highlighted`) can be put away (`PutAway`, the view's R key or the gamepad's left face button):
  it goes back to the inventory, no longer placed (saved as `"p":false`), still owned, and counted
  again in the picker. The Basic Bed can be put away too. (The gamepad button also arrives as
  Interact from the Input module, after the view's put away: on the now empty cell it does nothing.)
- **Sleeping.** Inside, within 1.2 m of the footprint of a placed Basic Bed or Mushroom Bed (and no
  door, catalog book or wardrobe prompt there): "[Interact] Sleep" publishes
  `Fungtopia.DayNight.Sleep` (the DayNight module fades the screen and skips to 7:00 the next
  morning; the player stays by the bed) and `HealPlayer(HealPlayer.Full)`. Like every prompt, not
  while decorating, in the shop or a wardrobe screen, on the start screen or while defeated.
- **Wardrobe.** A big wooden wardrobe stands in the house from the start, against the north wall
  in the north-east corner (`HomeRoom.WardrobeColumn`/`WardrobeRow`, 3 x 2 cells, doors facing
  into the room). It is not furniture: not in `Items`, never moved or picked up, and no furniture
  can be placed over it (its cells are `KeptClear`); pets walk around it. Within 1.2 m of its
  front (`HomeRoom.WardrobeFront`): "[Interact] Wardrobe" publishes `OpenWardrobe` (the Wardrobe
  module opens its screen). While `WardrobeState.Open` (`WardrobeOpen`, either Wardrobe screen)
  there is no prompt and Interact does nothing here: no leaving, no decorating
  (`StartDecorating` is refused, and placement mode ends if it was open, a held item going back
  to the inventory).
- **Pets at home.** Each pet `OwnedPets.AtHome` (owned, not active) is in `Pets` (`Present`),
  on the home floor: it wanders to free spots, naps (on the nearest placed pet bed no other pet
  has taken, `OnBed`, else on the floor) and plays (chasing another pet at home, or zooming
  around), 3 to 14 s at a time, steering clear of furniture. The order is a fixed pseudo-random
  sequence: the same every run.
- **Taking a pet along.** Inside, within 1.2 m of a pet at home (`Present`; the nearest one when
  several are close) and no door, catalog book or wardrobe prompt there: "[Interact] Take Mushroom
  Cat along" (`TakeCat`; `TakeDog`, `TakeCrow` for "Mushroom Dog", "Mushroom Crow") publishes
  `SetActivePet(kind)`. Nothing changes at home until the Pet module's next `OwnedPets`: then the
  chosen pet leaves the home floor (the Pet module shows it beside the player) and the one that was
  active comes home on the free spot nearest 0.75 m east of the player and starts wandering (a pet
  coming home while the player is not in the house appears at its own spot). No such prompt while
  decorating, in the shop or a wardrobe screen, on the start screen or while defeated.

- **Saving.** `CollectSave(RequestId)` is answered at once with one `SavePart(RequestId, "Home",
  json)`: every owned item in order, compact JSON from `HomeSave` (version `HomeSave.Version`, 1),
  e.g. `{"v":1,"items":[{"k":"BasicBed","p":true,"c":2,"r":0,"t":0}]}` (kind by name, placed,
  north-west column and row, quarter turns). `RestorePart("Home", json)` puts back exactly that
  furniture and nothing else happens (no `SpendCoins`, no shop result, no coins); the shop and
  placement mode close, a purchase still waiting for its answer is dropped, and pets choose their
  beds again. Missing or unreadable JSON, another version, an unknown kind, more than 64 items, a
  turn outside 0..3, or placed furniture outside the room, over a kept-clear cell or overlapping
  keeps the furniture as it was and logs one line (`HomeModule.Log`, the console by default).
  Pets at home and the shop's highlight are not saved (owned pets are the Pet module's).

Furniture footprints, in cells: Basic Bed 2 x 4, Mushroom Bed 3 x 4, Rug 4 x 3, Table 2 x 2,
Pet Bed 2 x 2, Bookshelf 3 x 1, the rest 1 x 1. No allocations per tick.

## View

`HomeView` draws the wardrobe and each placed item from `furniture.glb` at its footprint's center, turned by its
rotation, toon-shaded (`HomeLook`): mushroom caps (`Cap`, `CapPink`, `CapGlow`, the pets' mushroom
hats included) get a small soft white specular band and a brighter rim light (0.6, everything else
0.3); the lamp's `Glow` bulb glows at 2.4 (twice its old 1.2) and its `CapGlow` cap at 0.5; in placement mode it shows a soft grid over the floor and the held item floating at the
cursor over a green (fits) or red (does not) footprint, or with nothing held the footprint of the
placed item under the cursor in yellow (`Highlighted`). `HomePanels` shows the picker as a list
panel on the left, like the shop's, with the hint line under it, and placement mode's title (the
held or highlighted item's name) and hint line at the top. The pets at home use the Pet module's
models (`modules/Pet/Models/*.glb`, loaded read-only at run time; nothing shows if they are
missing) with their own simple animation: a bouncy walk, hops while playing, curled up with
floating "z z" while napping. Buttons for the panels, read by the view (Interact comes from the
Input module):

| Action | Keyboard | Gamepad |
| --- | --- | --- |
| Shop: choose | Up / Down, W / S (`MenuNav`) | D-pad up / down, left stick (`MenuNav`) |
| Decorating picker: choose | Up / Down, W / S (`MenuNav`) | D-pad up / down, left stick (`MenuNav`) |
| Decorating: move | Arrow keys, WASD (`MenuNav`) | D-pad, left stick (`MenuNav`) |
| Decorating: turn (an item held) | R | RB |
| Decorating: put away (nothing held) | R | X / Square / Nintendo Y (the left face button) |
| Decorating with the mouse | Point (highlight a picker line, move the cursor), left click (choose / place / pick up), middle click (turn) | |
| Buy / take / place / pick up | E or Enter | A / Cross (through Interact; X / Square works too) |
| Close / done | Q or Esc | B / Circle |

While decorating `HomeView` keeps the mouse cursor visible and reads the mouse itself (`_Input`, not
marked handled): motion of 2 px or more marks the mouse moved, and on the next frame the picker line
under it (`HomePanels.PickerLineAtMouse()`) is hovered or the camera ray through it is pointed at the
floor (also once on entering placement mode while `MouseActive`); left and middle presses become
`ClickPicker` / `ClickFloor` and `MouseRotate`.

**Prompts.** Every prompt and hint shows only the buttons of the device used last
(`HomeModule.Device`, the latest `ActiveDevice`), as icons in `RichTextLabel`s, rebuilt the moment
it changes. `HomeText.Prompt(prompt, device)` is the Interact icon (`Prompts.Rich`) and the verb
("[E key] Enter", "[A] Enter" on Xbox and Generic pads, "[Cross] Enter" on PlayStation, "[B] Enter"
on Nintendo); `HomeText.ShopHelp(device)`, `PickerHelp(device)` (choose, take, done),
`DecorateHelp(device)` (an item held: move, turn, place, back) and `MoveHelp(device, overItem)`
(nothing held: move, back, and over a placed item "[Interact] Pick up" and "[Put away] Put away",
`PutAwayIcon(device)`) the hint lines. On the keyboard they show Up / Down (choose), WASD (move),
R (turn, put away), E (buy / take / place / pick up) and Esc (close / back); on a gamepad D-pad Up
/ Down, the left stick, RB, X / Square / Y (put away), Confirm (A / Cross / B) and Back (B / Circle
/ A), each family's own icons. Icon sizes are parameters (`PromptIconSize` 40 next to 30 px text,
`HelpIconSize` 28 in the 18 px hints); the labels fit their content, so the panels grow with the
icons. `HomePanels` makes each icon it shows loadable when Godot has not imported it (as the Input
view does for its own), so the preview works without the Input module. E, X / Square and A / Cross
(the bottom button gives Interact while a panel holds `UiFocus`, so the view does not take A as a
Confirm too, which would buy or place twice) arrive through the Input module's Interact and the directions as its `MenuNav`; the rest the view reads itself. While a
panel is open the player stands still (its `UiFocus` stops movement in the Input module); the
player walks through furniture (it collides only with the Map's obstacles).

## Models

`blender -b --factory-startup --python modules/Home/Models/furniture.py` (from the repository
root) writes `furniture.glb`; commit it with the script. One object per `FurnitureKind` plus
`CatalogBook` and `Wardrobe`, each on the floor at the origin, centered on its footprint, front toward Godot +Z:
a wooden bed with a spotted blanket; a bed with a big spotted mushroom-cap headboard; an oval
green polka-dot rug; a mushroom lamp with a glowing pink cap; a round spotted mushroom table; a
chair with a mushroom-cap back; a potted mushroom; an easel with a mushroom painting (and a little
sun); a puffy lilac pet bed; a bookshelf of colorful books with two little mushrooms; a toadstool
stool; a mushroom-stem lectern with an open pink book; the `Wardrobe`: a big wooden wardrobe on round
feet, two doors with spotted mushroom panels and round knobs, a drawer, a mushroom-cap crown. No
object has eyes or a face (only creatures do).

The mushrooms are vivid, as everywhere in the game: caps a cherry red `Cap` (0.93, 0.07, 0.1) or a
bright pink `CapPink` (1, 0.36, 0.64), the lamp's cap `CapGlow` (1, 0.42, 0.7); spots a pure white
`Spots` (1, 1, 1); stems a light cream `Stem` (1, 0.96, 0.86). The bits that are not mushrooms keep
their old colors in their own materials: the blanket's and pet bed's dots `Dots`, the wardrobe's
knobs and handle `Knob`, the catalog's cover `BookCover` (0.98, 0.6, 0.68) and red dot `BookDot`, the painting's sun `Sun`.

## Screenshots

`./screenshot.sh modules/Home/Godot/Preview.tscn` shows a decorated room (the Cat napping on the
pet bed, the Dog about); `HOME_PREVIEW=decorate` shows placement mode with an item held,
`HOME_PREVIEW=picker` the decorating picker, `HOME_PREVIEW=shop` the furniture shop's catalog, `HOME_PREVIEW=wardrobe` the wardrobe with its
prompt. `HOME_PREVIEW_DEVICE=xbox` (or `playstation`, `nintendo`, `generic`) shows that pad's
buttons instead of the keyboard's, e.g. `HOME_PREVIEW=shop HOME_PREVIEW_DEVICE=playstation
./screenshot.sh modules/Home/Godot/Preview.tscn`.
