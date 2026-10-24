# Food module

Food that heals, bought in Mushroom Town: the cozy food stall **Mushroom Munchies** on the town
square sells Berry Muffins, Mushroom Soup and Spore Pies. You carry up to 9 of each in your food
bag, pick one, and eat it (F, or D-pad Up) to get hearts back. The module owns the food catalog,
the food bag, the stall's menu and eating. The bag is saved with the game through the Save module.

- `Contract/` — `Fungtopia.Food.Contract` (namespace `Fungtopia.Food`): `FoodBag`, `FoodEaten`,
  `OpenFoodMenu` and `FoodKinds` (the kinds' numbers).
- `Core/` — `Fungtopia.Food.Core`: `FoodModule`, the module's one `IModule`; `FoodCatalog` (names,
  prices, healing), `FoodText` (the texts, and the prompts and hint lines as RichTextLabel BBCode with
  button icons per device), `FoodSave` (the saved bag's JSON), `MenuRepeat` (a held menu direction:
  one step at once, repeating after a short delay).
- `Godot/` — `Main.tscn` with `FoodView` (the eating effect, pictures of the foods), `FoodPanels`
  (prompt, menu and food bar, a `CanvasLayer` on layer 11), `FoodModels` (loads food.glb at run time,
  toon materials); `Preview.tscn` (for screenshots); `Shaders/` (toon surface, ink outline).
- `Models/` — `food.py`, the Blender script that builds and exports `food.glb` next to it.
- `Tests/` — `FoodModuleTests.cs`: xUnit tests of `FoodModule` with a fake `IMessageBus` (which also
  answers `SpendCoins` like the wallet).

Test it with `./test.sh Food` from the repository root.

## Contract

| Message | Who publishes it | What it is |
| --- | --- | --- |
| `FoodBag` | Food, once every tick | `Muffins`, `Soups`, `Pies` (0..9 each), `Selected` (the kind Eat eats, 0..2), `Eating` (a meal is under way), `EatProgress` (0..1, 0 when not eating); `Count(kind)` and `Empty` read it. |
| `FoodEaten` | Food, once per meal | `Kind` eaten (`FoodKinds`) and `Healed`: the health given back (2 health is 1 heart), at most what was missing. |
| `OpenFoodMenu` | the stall's cook, Pip (NPCs module), when the player talks to them | Opens the stall's menu. |

`FoodKinds`: `Muffin` 0, `Soup` 1, `Pie` 2, `Count` 3.

## Core

`FoodModule` reads `Fungtopia.Player.PlayerState`, `Fungtopia.Input.PlayerInput` (`Interact`, `Eat`,
`FoodSelect`, `FoodCycle`), `Fungtopia.Input.ActiveDevice` (for the prompts),
`Fungtopia.Wallet.WalletState` and `SpendResult`, `OpenFoodMenu`,
`Fungtopia.StartScreen.StartScreenState` and `Fungtopia.Save.CollectSave` and `RestorePart`; it
publishes `FoodBag`, `FoodEaten`, `Fungtopia.Player.HealPlayer`, `Fungtopia.Wallet.SpendCoins`,
`Fungtopia.Input.UiFocus` and `Fungtopia.Save.SavePart`, and takes the stall's spot from the Map
Contract (`Places.FoodSpot`). Presses are acted on at the next tick.

- **Catalog** (`FoodCatalog`), cheap on purpose (one 50 M¢ caterpillar or ant buys about 10 muffins,
  4 soups or a pie): Berry Muffin 5 M¢, heals 1 heart; Mushroom Soup 12 M¢, 3 hearts; Spore Pie
  35 M¢, heals fully. A heart is 2 health (the HUD draws 5 hearts for 10), so eating sends
  `HealPlayer(2)`, `HealPlayer(6)` or `HealPlayer(HealPlayer.Full)` (999).
- **The stall's menu.** `OpenFoodMenu` opens it (`MenuOpen`; not while defeated or on the start
  screen). Pip owns the prompt and the talking; until Pip exists, Interact within 1.5 m of
  `Places.FoodSpot` also opens it, with the prompt "[Interact] Food" (`StallPrompt`: "E Food" on the
  keyboard, "X Food" on an Xbox pad). Once any `OpenFoodMenu` has arrived (`CookPresent`) the module
  stops doing that. The menu lists the three foods with price, healing and how many you carry
  (`FoodText.MenuLine`, e.g. "Mushroom Soup   12 M¢   +3 ♥   2/9"); `Navigate(y, seconds)` (Up /
  Down, wrapping, repeating while held) moves the highlight (`MenuSelection`); Interact or
  `Confirm()` (Enter, gamepad A) buys it: one `SpendCoins(RequestId, price, name)` (request ids from
  `FirstRequestId`, 8240000), and when its `SpendResult` is `Ok` one more is in the bag.
  `LastBuyResult` is `Bought`, `TooFewCoins` ("Not enough M¢") or `Full` (9 carried already; no
  coins asked). `Back()` (Q / Esc, gamepad B) closes it; it also closes when the player is defeated
  or the start screen opens. While it is open the module holds `UiFocus(MenuFocusOwner, true)`
  (8241001), released when it closes, however it closes.
- **Selection.** `FoodSelect` 0 / 1 / 2 (keys 1 / 2 / 3) selects that kind, carried or not;
  `FoodCycle` -1 / +1 (D-pad Left / Right) selects the previous / next kind you carry, wrapping and
  skipping empty kinds (unchanged when no other kind is carried). Not while eating or in the menu.
- **Eating.** `Eat` (F, D-pad Up) starts a meal of the selected food (`CanEat`) only if you carry
  one, are alive and below full health (`PlayerState.Alive`, `FullHealth`), and no menu, meal or
  start screen is open. The meal takes `EatSeconds` (1 s): `Eating` is true and the module holds
  `UiFocus(EatFocusOwner, true)` (8241002), so the Input module stops walking, looking, jumping and
  firing (you stop to eat). Then it publishes `HealPlayer` with the food's amount and one
  `FoodEaten`, and one fewer is in the bag. Being defeated (or healed to full some other way) during
  the meal stops it and keeps the food.
- **Saving.** `CollectSave(RequestId)` is answered at once with one `SavePart(RequestId, "Food",
  json)` (`FoodModule.SaveName`): `{"v":1,"muffins":3,"soups":0,"pies":1,"selected":0}` (`FoodSave`,
  version 1). `RestorePart("Food", json)` puts back exactly that bag and selection and nothing else
  happens (no `SpendCoins`, no `HealPlayer`, no `FoodEaten`, no purchase answer); the menu closes, a
  meal under way stops and a purchase still waiting for its answer is dropped. Missing or unreadable
  JSON, another version, a missing field, a count outside 0..9 or a selection outside 0..2 keeps the
  bag as it was and logs one line (`FoodModule.Log`, the console by default). Other modules' parts
  are ignored.

No allocations per tick.

## View

`FoodPanels`, in the game's panel style (rounded cream panels, ink outline, a pink title tab):

- **Prompt** above the bottom center: the Interact icon and "Food", while `StallPrompt`.
- **Menu** (left middle): "Mushroom Munchies", the wallet's coins, a row per food with its picture,
  name, price, healing and "n/9" (the highlighted row in pink; rows you cannot afford or are full
  faded), the answer to the last purchase, and the hint line (Up / Down choose, E / A buy, Q / B close,
  the active device's icons only).
- **Food bar** (bottom center): the three foods' pictures with their counts, the selected one
  framed in pink and a little bigger, kinds you do not carry faded, the number keys 1 / 2 / 3 on the
  keyboard, and a hint (eat, choose). Hidden when the bag is empty and while the start screen shows.

The pictures are the foods' models rendered in small off-screen viewports. While eating,
`FoodView` shows the food held up in front of the player's face, shrinking bite by bite, with crumbs
falling; when the meal is done a burst of crumbs and a pink "+3 ♥" (the hearts actually healed,
`FoodText.Healed`: "+½ ♥" for 1 health) rises over the player's head and fades. Buttons the view
reads itself while the menu is open (E / X arrive through the Input module's Interact):

| Action | Keyboard | Gamepad |
| --- | --- | --- |
| Choose | Up / Down, W / S (held: repeats) | D-pad Up / Down, left stick |
| Buy | E or Enter | A / Cross (X / Square, Interact, works too) |
| Close | Q or Esc | B / Circle |

## Models

`blender -b --factory-startup --python modules/Food/Models/food.py` (from the repository root)
writes `food.glb`; commit it with the script. Objects, each about 0.25 m across, on its base at the
origin: `BerryMuffin` (a domed muffin in a pleated pink paper cup, blueberries and raspberries on
top, a sprinkle of sugar), `MushroomSoup` (a wooden bowl of creamy soup with red mushroom-cap slices
and a herb leaf), `SporePie` (a golden lattice pie in a blue dish, a spotted mushroom cap baked in
the middle, glowing spores) and `Crumb` (for the crumbs). No faces: only creatures have eyes.

## Screenshots

`./screenshot.sh modules/Food/Godot/Preview.tscn` shows the menu over a stocked food bar (after a
"Not enough M¢"); `FOOD_PREVIEW=bar` the food bar alone, `FOOD_PREVIEW=eat` a soup halfway eaten
with crumbs, `FOOD_PREVIEW=eaten` the "+3 ♥" just after. `FOOD_PREVIEW_DEVICE=xbox` (or
`playstation`, `nintendo`, `generic`) shows that pad's buttons. The player is a simple stand-in.
