# Home module

The player's house in Mushroom Town: the front door (in and out by teleporting), the furniture
shop in town, arranging furniture on the home's floor grid, the pets that stay at home, and the
wardrobe (the Wardrobe module's screen for changing your look). The house starts cozy but sparse:
a basic bed under the round window and a big wooden wardrobe in the north-east corner (the room,
its windows and its door are drawn by the Map module). Everything is kept for the session only (saving to disk comes
later).

- `Contract/` — `Fungtopia.Home.Contract` (namespace `Fungtopia.Home`): `HomeState`.
- `Core/` — `Fungtopia.Home.Core`: `HomeModule`, the module's one `IModule`; `Furniture` (the
  catalog), `HomeRoom` (the floor grid), `HomeText` (the texts and hint lines), `MenuRepeat` (a held
  menu direction: one step at once, repeating after a short delay).
- `Godot/` — `Main.tscn` with `HomeView` (furniture, the catalog book, pets at home, the grid and
  cursor while decorating), `HomePanels` (prompt, shop catalog and decorating help, a
  `CanvasLayer` on layer 11), `HomePetModel`, `HomeModels` (loads .glb models at run time, toon
  materials); `Preview.tscn` (a decorated room, for screenshots); `Shaders/` (toon surface, ink
  outline, floor grid).
- `Models/` — `furniture.py`, the Blender script that builds and exports `furniture.glb` next to it.
- `Tests/` — `HomeModuleTests.cs`: xUnit tests of `HomeModule` with a fake `IMessageBus` (which
  also answers `SpendCoins` like the wallet and records `UiFocus`).

Test it with `./test.sh Home` from the repository root.

## Contract

| Message | Who publishes it | What it is |
| --- | --- | --- |
| `HomeState` | Home, once every tick | `ItemsOwned` (furniture owned, placed or not, the starting bed included), `ItemsPlaced` (standing in the house), `Decorating` (placement mode is open). |

## Core

`HomeModule` reads `Fungtopia.Player.PlayerState`, `Fungtopia.Input.PlayerInput` (`Interact`),
`Fungtopia.Wallet.WalletState` and `SpendResult`, `Fungtopia.Pet.OwnedPets`,
`Fungtopia.Wardrobe.WardrobeState` and `Fungtopia.StartScreen.StartScreenState`; it publishes `Fungtopia.Player.TeleportPlayer`,
`Fungtopia.Wallet.SpendCoins`, `Fungtopia.Wardrobe.OpenWardrobe`, `Fungtopia.Input.UiFocus` and `HomeState`, and takes the
world's spots from the Map Contract (`Places`, `Areas`). An Interact press is acted on at the
next tick, for whatever `Prompt` shows then (only while the player is alive, and never while
the start screen is open, below):

- **Start screen.** While `StartScreenState.Open` (`StartScreenOpen`, the character creator is
  showing) there is no prompt and Interact does nothing (no door, shop, decorating or wardrobe;
  a press made then is dropped, not acted on after "Start!"); `StartDecorating` is refused, and
  the shop and placement mode close if they were open.

- **Door.** Within 1.5 m of `Places.HomeDoor`: "E / X: Enter" teleports the player to
  `Places.HomeInterior`. Inside, within 1.2 m of `Places.HomeExit` (the spot by the door):
  "E / X: Leave" teleports the player out to `Places.HomeDoor`, facing the street. (The Map
  defines `HomeExit` as the inside spot that leaves the house, so leaving lands at `HomeDoor`.)
- **Shop.** Within 1.5 m of `Places.FurnitureShopSpot`: "E / X: Shop" opens the catalog panel
  (`ShopOpen`); it closes with Back or once the player is 3 m away. Interact buys the highlighted
  item (`ShopSelection`, `MoveShopSelection`): one `SpendCoins(RequestId, price, name)`, and
  when its `SpendResult` is `Ok` the item goes into the inventory (owned, not placed);
  `LastShopResult` is `Bought`, `TooFewCoins` or `Full` (64 items at most).
- **Menus and UiFocus.** The shop's panel (`ShopFocusOwner`) and placement mode
  (`DecorateFocusOwner`) each publish `UiFocus(owner, true)` at the tick they open and
  `UiFocus(owner, false)` at the tick they close, however they close (Back, walking away, leaving
  the house, fainting, a wardrobe screen or the start screen), so the Input module stops walking,
  looking, jumping and firing while one is open. `Navigate(x, y, seconds)` takes the direction
  the view reads as held each frame: one step at once when pressed, then after
  `MenuRepeat.Delay` (0.35 s) one every `MenuRepeat.Interval` (0.12 s) while held; in the shop it
  moves the highlight (Up / Down, wrapping), while decorating the cursor. `Confirm()` (Enter, A)
  acts like an Interact press at the next tick, only while a panel is open.
- **Catalog.** `Furniture.ForSale`, prices in M¢: Mushroom Bed 300, Rug 120, Lamp 150, Table 200,
  Chair 100, Potted Mushroom 80, Painting (on an easel) 250, Pet Bed 180, Bookshelf 220,
  Toadstool Stool 90. The Basic Bed comes with the house and is not sold.
- **Decorating.** Inside, within 1.2 m of the little catalog book on its stand (south wall, east
  of the door, `HomeRoom.BookPosition`): "E / X: Decorate" opens placement mode. It holds the
  first owned item not yet placed (`Held`); `NextItem` holds the next one, `MoveCursor` moves it
  on the 0.5 m floor grid (24 x 20 cells over `Areas.Home`, staying inside), `Rotate` turns it a
  quarter turn (odd turns swap its footprint), and Interact (`PlaceOrPickUp`) places it if it
  fits, or with nothing held picks up the placed item under the cursor. An item fits
  (`CanPlace`) only inside the room, off the door's way in (2 m wide, from the door to 0.5 m past
  `HomeInterior`), off the catalog book's stand, and overlapping no other furniture (rugs
  included). Back, leaving the house or fainting ends it; a held item goes back to the inventory.
- **Wardrobe.** A big wooden wardrobe stands in the house from the start, against the north wall
  in the north-east corner (`HomeRoom.WardrobeColumn`/`WardrobeRow`, 3 x 2 cells, doors facing
  into the room). It is not furniture: not in `Items`, never moved or picked up, and no furniture
  can be placed over it (its cells are `KeptClear`); pets walk around it. Within 1.2 m of its
  front (`HomeRoom.WardrobeFront`): "E / X: Wardrobe" publishes `OpenWardrobe` (the Wardrobe
  module opens its screen). While `WardrobeState.Open` (`WardrobeOpen`, either Wardrobe screen)
  there is no prompt and Interact does nothing here: no leaving, no decorating
  (`StartDecorating` is refused, and placement mode ends if it was open, a held item going back
  to the inventory).
- **Pets at home.** Each pet `OwnedPets.AtHome` (owned, not active) is in `Pets` (`Present`),
  on the home floor: it wanders to free spots, naps (on the nearest placed pet bed no other pet
  has taken, `OnBed`, else on the floor) and plays (chasing another pet at home, or zooming
  around), 3 to 14 s at a time, steering clear of furniture. The order is a fixed pseudo-random
  sequence: the same every run.

Furniture footprints, in cells: Basic Bed 2 x 4, Mushroom Bed 3 x 4, Rug 4 x 3, Table 2 x 2,
Pet Bed 2 x 2, Bookshelf 3 x 1, the rest 1 x 1. No allocations per tick.

## View

`HomeView` draws the wardrobe and each placed item from `furniture.glb` at its footprint's center, turned by its
rotation; while decorating it shows a soft grid over the floor and the held item floating at the
cursor over a green (fits) or red (does not) footprint. The pets at home use the Pet module's
models (`modules/Pet/Models/*.glb`, loaded read-only at run time; nothing shows if they are
missing) with their own simple animation: a bouncy walk, hops while playing, curled up with
floating "z z" while napping. Buttons for the panels, read by the view (Interact comes from the
Input module):

| Action | Keyboard | Gamepad |
| --- | --- | --- |
| Shop: choose | Up / Down, W / S (held: repeats) | D-pad up / down, left stick |
| Decorating: move | Arrow keys, WASD (held: repeats) | D-pad, left stick |
| Decorating: turn | R | RB |
| Decorating: next item | Tab | LB |
| Buy / place / pick up | E or Enter | A / Cross (X / Square, Interact, works too) |
| Close / done | Q or Esc | B / Circle |

The panels' hint lines (`HomeText.ShopHelp`, `DecorateHelp`) name exactly these buttons. E and
X / Square arrive through the Input module's Interact; the rest the view reads itself. While a
panel is open the player stands still (its `UiFocus` stops movement in the Input module); the
player walks through furniture (it collides only with the Map's obstacles).

## Models

`blender -b --factory-startup --python modules/Home/Models/furniture.py` (from the repository
root) writes `furniture.glb`; commit it with the script. One object per `FurnitureKind` plus
`CatalogBook` and `Wardrobe`, each on the floor at the origin, centered on its footprint, front toward Godot +Z:
a wooden bed with a spotted blanket; a bed with a big spotted mushroom-cap headboard with a face;
an oval green polka-dot rug; a mushroom lamp with a glowing pink cap and eyes; a round spotted
mushroom table; a chair with a mushroom-cap back; a potted mushroom with a face; an easel with a
mushroom painting; a puffy lilac pet bed; a bookshelf of colorful books; a toadstool stool with a
face; a mushroom-stem lectern with an open pink book; the `Wardrobe`: a big wooden wardrobe on round
feet, two doors with spotted mushroom panels and round knobs, a drawer, a mushroom-cap crown (no
face).

## Screenshots

`./screenshot.sh modules/Home/Godot/Preview.tscn` shows a decorated room (the Cat napping on the
pet bed, the Dog about); `HOME_PREVIEW=decorate` shows placement mode with an item held,
`HOME_PREVIEW=shop` the furniture shop's catalog, `HOME_PREVIEW=wardrobe` the wardrobe with its
prompt.
