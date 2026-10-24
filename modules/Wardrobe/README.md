# Wardrobe module

The player's look and clothes: the catalog of clothes, the clothing and hair models, what the
player owns and wears, the wardrobe screen (the wardrobe in the house: change the look and owned
clothes for free) and Abby's shop screen (Mushroom Town: buy clothes for Mushroom Coins, M¢).
Some clothes are rewards. Adding a piece of clothing is one `Catalog` entry plus one model script.

- `Contract/` — `Fungtopia.Wardrobe.Contract` (namespace `Fungtopia.Wardrobe`), see below.
- `Core/` — `Fungtopia.Wardrobe.Core`: `WardrobeModule`, the module's one `IModule`, and
  `WardrobePrompts` (the screens' controls hints as button icons, see [Prompts](#prompts)).
- `Godot/` — `Main.tscn` with `WardrobeView` (the screens); `WardrobeScreen`, `ShopScreen` and their
  frame `ScreenPanel`; `CapButton` (the big mushroom Done / Buy buttons); `AbbyPortrait` (Abby drawn in the shop's header); `WardrobeGlyphs` (makes this module's icons loadable unimported); `IconStudio` (renders each
  model's little icon in its own `SubViewport`); `ToonGlb` (loads the .glb files toon-shaded with ink
  outlines); `Mannequin` and `Preview.tscn` (for screenshots only); `Shaders/`; `Glyphs/`
  (`key_pgup`, `key_pgdn`: the tab keys' icons, which the Input module lacks, drawn like its
  keycaps by `make_glyphs.py`, run with `python3`).
- `Models/` — `kit.py` (shared Blender helpers and the rig's sizes), `Clothes/<id>.py` and
  `Hair/<id>.py`: one script per item or hair style, each exporting `<id>.glb` next to itself.
- `Tests/` — `WardrobeModuleTests.cs`: `CatalogTests` (the catalog and looks are well-formed, every
  model and its script exist) and `WardrobeModuleTests` (`WardrobeModule` with a fake `IMessageBus`
  that also plays the wallet; saving and restoring round trips, bad save data keeps the defaults,
  the prompt device, the menu steps: a held direction moves the focus one item per 0.2 s, a short
  flick exactly one) and `WardrobePromptsTests` (each device's hints show its own icons only).

Test it with `./test.sh Wardrobe` from the repository root.

## Contract

| Type | Who publishes it | What it is |
| --- | --- | --- |
| `Slot` | | `Top`, `Bottom`, `Shoes`, `Head`, `Face`, `Back`. |
| `ClothingItem` | | `Id` (snake_case, also the model's file name), `Name`, `Slot`, `Price` (M¢, shop items only), `Fit` (`Fits.Both` 0, `Fits.Girl` 1, `Fits.Boy` 2), `Source` (`Sources.Starter` "starter", `Shop` "shop", `Reward` "reward"), `ModelPath` (res:// .glb), `Dress` (a top that hides the bottom). |
| `Catalog` | | `Items`, `Count`, `IndexOf(id)`, `TryGet(id, out item)`, `StarterOutfits` (3 `StarterOutfit`s: `Name`, `Fit`, `Top`, `Bottom`, `Shoes`, `Head`), `DefaultOutfit(character)` (the Girl starts in the Mushroom Dress outfit, the Boy in Sunny Stripes), `SpiderSilkScarf`, `LadybugBackpack` (the reward ids). |
| `Looks` | | `SkinTones` (6, light to deep), `HairStyles` (6 `HairStyle`: `Id`, `Name`, `ModelPath`: short, bob, pigtails, ponytail, top bun like Abby's, curly), `HairColors` (8, pink and mint among them) and `HairColorNames`, `EyeColors` (6) and `EyeColorNames`, `Valid(...)`. Colors are `Vector3` RGB 0..1, the same kind of value as a Blender swatch. |
| `Attach` | | How models sit on the Player rig (below): part names `Head`, `Body`, `LegL`, `LegR` and their pivots. |
| `AppearanceState` | Wardrobe, every tick | `Skin`, `HairStyle`, `HairColor`, `EyeColor` (indices into `Looks`), `Top`, `Bottom`, `Shoes`, `Head`, `Face`, `Back` (shown item ids, empty for none: what is worn, with the item tried on in the shop in its slot; `Bottom` is empty under a dress); `Worn(slot)`. |
| `WardrobeState` | Wardrobe, every tick | `Open` (a screen is open: the player should stand still and the camera frame them), `Shop` (it is Abby's shop). |
| `PreviewYaw` | Wardrobe, every tick a screen is open | `Yaw`: how the player turned the character to look at it, radians in [-π, π); 0 faces the camera, positive turns its front to the screen's right (counterclockwise seen from above); 0 each time a screen opens. The Player module should turn the character by it while `WardrobeState.Open`. |
| `SetLook` | anyone (character creation) | `Skin`, `HairStyle`, `HairColor`, `EyeColor`; ignored when any is out of range. |
| `Equip` | anyone | `ItemId` puts on an owned item that fits, in its own slot; an empty `ItemId` takes off `EquipSlot` (`Equip.Unequip(slot)`). Others are ignored. |
| `GrantItem` | anyone (quests) | `ItemId`, `Why`: gives the item (unknown or already owned: nothing). |
| `ItemReceived` | Wardrobe, once per new item | `ItemId`, `Why` ("Abby's shop", the `GrantItem`'s reason, "First Mushroom Tarantula"...). |
| `OpenWardrobe`, `OpenShop` | the house's wardrobe, Abby | Open the wardrobe or the shop screen. |

## Catalog

Starter outfits (owned from the start): **Sunny Stripes** (Striped Tee, Shorts, Sneakers),
**Mushroom Dress** (Spotted Mushroom Dress, Mary-Janes, Big Bow; girl), **Cozy Overalls** (Overalls
Top, Overalls, Rain Boots, Beanie). Abby sells: Mushroom Pocket Hoodie 350, Sweater Vest 250, Flower
Dress 400 (girl), Triangle Party Dress 600, Skirt 150 (girl), Jeans 200, Red Mushroom-Cap Hat 450,
Flower Clip 100, Round Glasses 200, Star Sunglasses 300, Little Backpack 300, Mushroom Backpack 500
(M¢). Rewards: **Spider Silk Scarf** (Face slot, worn around the neck) for the first Mushroom
Tarantula defeated, **Ladybug Backpack** (Back) for the first Mushroom Ladybug.

To add a piece: one entry in `Catalog` (`Contract/Wardrobe.cs`) and one script
`Models/Clothes/<id>.py` built from `kit.py`; run it to write `<id>.glb` and commit both. The tests
check the model and the script exist.

## Core

`WardrobeModule` reads `Fungtopia.Player.PlayerState` (the character: Girl or Boy),
`Fungtopia.Monsters.MonsterKilled`, `Fungtopia.Wallet.WalletState` and `SpendResult`,
`Fungtopia.Input.ActiveDevice` and `MenuNav`,
`Fungtopia.Save.CollectSave` and `RestorePart`, and the messages above; it publishes
`Fungtopia.Wallet.SpendCoins` and `Fungtopia.Save.SavePart`.

- **Owning.** Every starter item is owned at the start; shop items by buying, rewards and anything
  else by `GrantItem`. Each new item publishes one `ItemReceived`.
- **Wearing.** One item per slot. Only owned items that fit the character go on. A dress hides the
  bottom: a bottom put on under it is kept and shows again once another top is worn. Until the
  player changes clothes, the first `PlayerState` dresses the character in its `DefaultOutfit`;
  switching character later takes off what does not fit. `WearOutfit(i)` puts on a starter outfit.
- **Rewards.** The first `MonsterKilled` of a `Tarantula` gives the Spider Silk Scarf, of a
  `Ladybug` the Ladybug Backpack (whoever made the kill, the player or the pet), once each.
- **Buying.** `Buy(id)` (the shop's Buy button) works only while the shop is open, for a shop item
  that fits and is not owned. On the next tick it publishes `SpendCoins` (`RequestId` from
  `RequestIdBase`, the price, the name); a matching `SpendResult` with `Ok` adds the item
  (`LastBuy` = `Bought`), otherwise nothing changes (`NotEnough`, the view's "Not enough M¢").
- **Trying on.** `TryOn(id)` (the shop's highlighted card) shows a shop item that fits on the
  character in its slot while the shop is open (`TryingOn`), without wearing it: `Appearance`
  shows it, `Worn(slot)` does not. An empty or other id, closing or reopening a screen show what
  is worn again. An item bought while tried on is put on and stays on.
- **Turning.** While a screen is open each tick turns `Yaw` and publishes it as `PreviewYaw`:
  `SetTurnButtons(left, right)` (Q / LB, E / RB, held) turn at `TurnRadiansPerSecond` (3), else
  `SetTurnStick(x)` (the right stick) past `StickDeadZone` (0.2) up to that speed, plus
  `Drag(pixels)` (a mouse drag, added up between ticks, `DragRadiansPerPixel` 0.01; ignored while
  closed). Opening a screen sets `Yaw` to 0.
- **Screens.** `OpenWardrobe` / `OpenShop` open them, `Close()` (Done, Esc, B) closes them.
- **Saving.** Each `CollectSave` is answered at once with one `SavePart` (Module `"Wardrobe"`):
  `SaveJson()`, compact JSON `{"v":1,"look":[skin,hairStyle,hairColor,eyeColor],"owned":[ids],
  "worn":[6 ids in Slot order, "" for none]}` (`SaveVersion` 1; `worn` keeps a bottom hidden under a
  dress). A `RestorePart` for `"Wardrobe"` (`Restore(json)`) puts back exactly that look, owned and
  worn items (starters stay owned; ids no longer in the catalog are skipped), closes an open screen,
  forgets a pending purchase and counts the clothes as chosen, so the next `PlayerState` does not
  dress the default outfit. It publishes nothing: no `ItemReceived`, no `SpendCoins`; the first
  Tarantula / Ladybug rewards count as given when their item is owned. Missing or unreadable JSON, or
  another version, changes nothing and writes one line to `Log` (the console) per game.
- **Prompt device.** `Device` is the device whose buttons the hints show: the `ActiveDevice` kind
  whenever the Input module reports a new one, or the one the view passes to `NoteDevice(kind)`
  since (while a screen is open the view takes the input, so the Input module sees no change);
  opening a screen goes back to the Input module's device.
- **Menu steps.** While a screen is open each `MenuNav` step (the Input module paces them: one when
  a direction is first pushed, then one per 0.2 s held, from the left stick, D-pad, arrows and
  W A S D) is kept for the view, which takes it with `TakeMenuStep(out vertical, out horizontal)`
  and moves the focus one item. Only the latest untaken step is kept (they never pile up); steps
  while closed are dropped, and opening or closing a screen forgets an untaken one.
- `Coins` (the latest `WalletState`), `Owns`, `Worn(slot)`, `BottomHidden`, `CanBuy`, `Appearance`
  are for the view. No allocations per tick.

## View

`WardrobeView` builds the screens the first time one opens and shows the wardrobe or the shop on a
canvas layer (20), styled like the start screen's "Make your look": a big cream heading, the
rounded cream panel and big mushroom buttons (Done; Buy in the shop) under it, all on the right
half of the screen (scaled down on narrow screens so it never crosses the middle). The left half
is left free: the Player module draws the character there and the Camera frames it while
`WardrobeState.Open` (their jobs). While a screen is open the mouse pointer is free and the view
takes the input events before the Input module (so the game does not jump, talk or open the map).
The focus moves only by the Input module's `MenuNav` steps (it polls the held directions, so it still
sees them); the view reads no stick, D-pad or arrow of its own for that:

| Action | Mouse | Keyboard | Gamepad |
| --- | --- | --- | --- |
| Turn the character | Drag on the left half | Q / E (held) | Right stick, LB / RB (held) |
| Move the focus | Hover a shop card | Arrows, W A S D (via `MenuNav`) | D-pad, left stick (via `MenuNav`) |
| Choose | Click | Enter, Space | A |
| Tabs (wardrobe) | Click a tab | Page Up / Page Down | LT / RT |
| Done | Done | Esc | B |

The Input module still polls held W A S D for `PlayerInput.Move`: the character stands still only
if the Player module ignores `Move` while `WardrobeState.Open` (its job).

- **Wardrobe:** tabs Look / Top / Bottom / Shoes / Head / Face / Back. Look has round swatches for
  skin, hair color and eyes and icon cards for the hair styles; each slot tab has "None" and the
  owned items that fit, with their icons. A pick applies at once (mint card: worn).
- **Abby's shop:** Abby (big dark eyes, happy smile, tall top bun, triangle dress), the wallet's
  M¢, and every shop item that fits with its icon and price; owned ones are greyed "Owned". The
  highlighted card (focused or hovered; else the picked one) is tried on the character. Picking a
  card selects it and focuses Buy; the answer shows under it ("Not enough M¢" or "Thank you!").

## Prompts

The footer of each screen is a `RichTextLabel` hint with the button icons of `Device` only,
each followed by what it does, rebuilt the frame the device changes (it grows to fit its lines
at any icon size; a group of icons and words wraps as a whole). `WardrobePrompts.Wardrobe(device,
size = 32)` and `Shop(device, size)` build its BBCode with `Prompts.Rich`:

| | Keyboard and mouse | Xbox (and Generic) | PlayStation | Nintendo |
| --- | --- | --- | --- | --- |
| Turn | Q E, left mouse (drag) | LB RB, right stick | L1 R1, right stick | L R, right stick |
| Tabs (wardrobe) | Page Up, Page Down | LT RT | L2 R2 | ZL ZR |
| Choose / Pick, then Buy | Enter | A | Cross | B |
| Done | Esc | B | Circle | A |

`TabIcon(direction, device)` is the tab buttons' icon (no `PromptAction` has them). The view
tells the device of the input it takes from the event: keyboard, mouse buttons and mouse motion of
2 px or more are keyboard and mouse, a pad's button or an axis past the dead zone that pad's
family (`PadFamily(name, guid)`, the Input module's rules).

## Models

Run a script from the repository root, e.g. `blender -b --factory-startup --python
modules/Wardrobe/Models/Clothes/striped_tee.py`, or all of them with
`for f in modules/Wardrobe/Models/*/*.py; do [ "$(basename "$f")" = kit.py ] || blender -b --factory-startup --python "$f"; done`.

Sized for the Player's chibi rig (`modules/Player/Models/characters.py`). Each .glb holds one
object per rig part it covers, named like the part and with its origin at that part's pivot, so
the Player module parents it to the part with no offset and it moves with it: `Head` (pivot at the
neck, 0.72 m: hats, glasses, hair), `Body` (hip, 0.3 m: tops, dresses, the hips of bottoms,
backpacks, the scarf), `LegL` / `LegR` (hip joints at x ∓0.11, 0.32 m: trouser legs and shoes).
Materials carry plain colors; a hair style's material `Hair` takes the hair color. No eyes or faces
on clothes.

## Screenshots

`./screenshot.sh modules/Wardrobe/Godot/Preview.tscn` shows the wardrobe screen (Top tab) beside a
dressed mannequin (the Player's rig with the hair, skin and items applied), turned by `PreviewYaw`
from a simulated mouse drag; `WARDROBE_PREVIEW` picks `look` (the Look tab), `shop` (Abby's shop
after buying the Flower Clip, trying on the Triangle Party Dress it fails to afford) or `items`
(four outfits side by side); `WARDROBE_DEVICE` (`keyboard`, `xbox`, `playstation`, `nintendo`,
`generic`) the device whose prompts show.
