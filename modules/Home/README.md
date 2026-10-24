# Home module

The player's house in Mushroom Town: the front door (in and out by teleporting), the furniture
shop in town, arranging furniture on the home's floor grid, and the pets that stay at home. The
house starts cozy but sparse: a basic bed under the round window (the room, its windows and its
door are drawn by the Map module). Everything is kept for the session only (saving to disk comes
later).

- `Contract/` — `Fungtopia.Home.Contract` (namespace `Fungtopia.Home`): `HomeState`.
- `Core/` — `Fungtopia.Home.Core`: `HomeModule`, the module's one `IModule`; `Furniture` (the
  catalog), `HomeRoom` (the floor grid), `HomeText` (the texts).
- `Godot/` — `Main.tscn` with `HomeView` (furniture, the catalog book, pets at home, the grid and
  cursor while decorating), `HomePanels` (prompt, shop catalog and decorating help, a
  `CanvasLayer` on layer 11), `HomePetModel`, `HomeModels` (loads .glb models at run time, toon
  materials); `Preview.tscn` (a decorated room, for screenshots); `Shaders/` (toon surface, ink
  outline, floor grid).
- `Models/` — `furniture.py`, the Blender script that builds and exports `furniture.glb` next to it.
- `Tests/` — `HomeModuleTests.cs`: xUnit tests of `HomeModule` with a fake `IMessageBus` (which
  also answers `SpendCoins` like the wallet).

Test it with `./test.sh Home` from the repository root.

## Contract

| Message | Who publishes it | What it is |
| --- | --- | --- |
| `HomeState` | Home, once every tick | `ItemsOwned` (furniture owned, placed or not, the starting bed included), `ItemsPlaced` (standing in the house), `Decorating` (placement mode is open). |

## Core

`HomeModule` reads `Fungtopia.Player.PlayerState`, `Fungtopia.Input.PlayerInput` (`Interact`),
`Fungtopia.Wallet.WalletState` and `SpendResult`, and `Fungtopia.Pet.OwnedPets`; it publishes
`Fungtopia.Player.TeleportPlayer`, `Fungtopia.Wallet.SpendCoins` and `HomeState`, and takes the
world's spots from the Map Contract (`Places`, `Areas`). An Interact press is acted on at the
next tick, for whatever `Prompt` shows then (only while the player is alive):

- **Door.** Within 1.5 m of `Places.HomeDoor`: "E / X: Enter" teleports the player to
  `Places.HomeInterior`. Inside, within 1.2 m of `Places.HomeExit` (the spot by the door):
  "E / X: Leave" teleports the player out to `Places.HomeDoor`, facing the street. (The Map
  defines `HomeExit` as the inside spot that leaves the house, so leaving lands at `HomeDoor`.)
- **Shop.** Within 1.5 m of `Places.FurnitureShopSpot`: "E / X: Shop" opens the catalog panel
  (`ShopOpen`); it closes with Back or once the player is 3 m away. Interact buys the highlighted
  item (`ShopSelection`, `MoveShopSelection`): one `SpendCoins(RequestId, price, name)`, and
  when its `SpendResult` is `Ok` the item goes into the inventory (owned, not placed);
  `LastShopResult` is `Bought`, `TooFewCoins` or `Full` (64 items at most).
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
- **Pets at home.** Each pet `OwnedPets.AtHome` (owned, not active) is in `Pets` (`Present`),
  on the home floor: it wanders to free spots, naps (on the nearest placed pet bed no other pet
  has taken, `OnBed`, else on the floor) and plays (chasing another pet at home, or zooming
  around), 3 to 14 s at a time, steering clear of furniture. The order is a fixed pseudo-random
  sequence: the same every run.

Furniture footprints, in cells: Basic Bed 2 x 4, Mushroom Bed 3 x 4, Rug 4 x 3, Table 2 x 2,
Pet Bed 2 x 2, Bookshelf 3 x 1, the rest 1 x 1. No allocations per tick.

## View

`HomeView` draws each placed item from `furniture.glb` at its footprint's center, turned by its
rotation; while decorating it shows a soft grid over the floor and the held item floating at the
cursor over a green (fits) or red (does not) footprint. The pets at home use the Pet module's
models (`modules/Pet/Models/*.glb`, loaded read-only at run time; nothing shows if they are
missing) with their own simple animation: a bouncy walk, hops while playing, curled up with
floating "z z" while napping. Buttons for the panels, read by the view (Interact comes from the
Input module):

| Action | Keyboard | Gamepad |
| --- | --- | --- |
| Shop: choose | Up / Down | D-pad up / down |
| Decorating: move | Arrow keys | D-pad |
| Decorating: turn | R | RB |
| Decorating: next item | Tab | LB |
| Buy / place / pick up | E | X / Square |
| Close / done | Q or Backspace | B / Circle |

The player can still walk while a panel is open (the Player module stops only for dialogue), and
walks through furniture (it collides only with the Map's obstacles).

## Models

`blender -b --factory-startup --python modules/Home/Models/furniture.py` (from the repository
root) writes `furniture.glb`; commit it with the script. One object per `FurnitureKind` plus
`CatalogBook`, each on the floor at the origin, centered on its footprint, front toward Godot +Z:
a wooden bed with a spotted blanket; a bed with a big spotted mushroom-cap headboard with a face;
an oval green polka-dot rug; a mushroom lamp with a glowing pink cap and eyes; a round spotted
mushroom table; a chair with a mushroom-cap back; a potted mushroom with a face; an easel with a
mushroom painting; a puffy lilac pet bed; a bookshelf of colorful books; a toadstool stool with a
face; a mushroom-stem lectern with an open pink book.

## Screenshots

`./screenshot.sh modules/Home/Godot/Preview.tscn` shows a decorated room (the Cat napping on the
pet bed, the Dog about); `HOME_PREVIEW=decorate` shows placement mode with an item held,
`HOME_PREVIEW=shop` the furniture shop's catalog.
