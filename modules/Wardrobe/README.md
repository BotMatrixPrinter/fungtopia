# Wardrobe module

The player's look and clothes: the catalog of clothes, the clothing and hair models, what the
player owns and wears, the wardrobe screen (the wardrobe in the house: change the look and owned
clothes for free) and Abby's shop screen (Mushroom Town: buy clothes for Mushroom Coins, M¢).
Some clothes are rewards. Adding a piece of clothing is one `Catalog` entry plus one model script.

- `Contract/` — `Fungtopia.Wardrobe.Contract` (namespace `Fungtopia.Wardrobe`), see below.
- `Core/` — `Fungtopia.Wardrobe.Core`: `WardrobeModule`, the module's one `IModule`.
- `Godot/` — `Main.tscn` with `WardrobeView` (the screens); `WardrobeScreen`, `ShopScreen` and their
  frame `ScreenPanel`; `AbbyPortrait` (Abby drawn in the shop's header); `IconStudio` (renders each
  model's little icon in its own `SubViewport`); `ToonGlb` (loads the .glb files toon-shaded with ink
  outlines); `Mannequin` and `Preview.tscn` (for screenshots only); `Shaders/`.
- `Models/` — `kit.py` (shared Blender helpers and the rig's sizes), `Clothes/<id>.py` and
  `Hair/<id>.py`: one script per item or hair style, each exporting `<id>.glb` next to itself.
- `Tests/` — `WardrobeModuleTests.cs`: `CatalogTests` (the catalog and looks are well-formed, every
  model and its script exist) and `WardrobeModuleTests` (`WardrobeModule` with a fake `IMessageBus`
  that also plays the wallet).

Test it with `./test.sh Wardrobe` from the repository root.

## Contract

| Type | Who publishes it | What it is |
| --- | --- | --- |
| `Slot` | | `Top`, `Bottom`, `Shoes`, `Head`, `Face`, `Back`. |
| `ClothingItem` | | `Id` (snake_case, also the model's file name), `Name`, `Slot`, `Price` (M¢, shop items only), `Fit` (`Fits.Both` 0, `Fits.Girl` 1, `Fits.Boy` 2), `Source` (`Sources.Starter` "starter", `Shop` "shop", `Reward` "reward"), `ModelPath` (res:// .glb), `Dress` (a top that hides the bottom). |
| `Catalog` | | `Items`, `Count`, `IndexOf(id)`, `TryGet(id, out item)`, `StarterOutfits` (3 `StarterOutfit`s: `Name`, `Fit`, `Top`, `Bottom`, `Shoes`, `Head`), `DefaultOutfit(character)` (the Girl starts in the Mushroom Dress outfit, the Boy in Sunny Stripes), `SpiderSilkScarf`, `LadybugBackpack` (the reward ids). |
| `Looks` | | `SkinTones` (6, light to deep), `HairStyles` (6 `HairStyle`: `Id`, `Name`, `ModelPath`: short, bob, pigtails, ponytail, top bun like Abby's, curly), `HairColors` (8, pink and mint among them) and `HairColorNames`, `EyeColors` (6) and `EyeColorNames`, `Valid(...)`. Colors are `Vector3` RGB 0..1, the same kind of value as a Blender swatch. |
| `Attach` | | How models sit on the Player rig (below): part names `Head`, `Body`, `LegL`, `LegR` and their pivots. |
| `AppearanceState` | Wardrobe, every tick | `Skin`, `HairStyle`, `HairColor`, `EyeColor` (indices into `Looks`), `Top`, `Bottom`, `Shoes`, `Head`, `Face`, `Back` (worn item ids, empty for none; `Bottom` is empty under a dress); `Worn(slot)`. |
| `WardrobeState` | Wardrobe, every tick | `Open` (a screen is open: the player should stand still and the camera frame them), `Shop` (it is Abby's shop). |
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
`Fungtopia.Monsters.MonsterKilled`, `Fungtopia.Wallet.WalletState` and `SpendResult`, and the
messages above; it publishes `Fungtopia.Wallet.SpendCoins`.

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
- **Screens.** `OpenWardrobe` / `OpenShop` open them, `Close()` (Done, Esc, B) closes them.
- `Coins` (the latest `WalletState`), `Owns`, `Worn(slot)`, `BottomHidden`, `CanBuy`, `Appearance`
  are for the view. No allocations per tick.

## View

`WardrobeView` builds the screens the first time one opens and shows the wardrobe or the shop on a
canvas layer (20), on the right of the screen: the Player module draws the character live behind
the panel and the Camera frames it while `WardrobeState.Open` (their jobs). While a screen is open
the mouse pointer is free and the view takes all input before the Input module (so the game does
not walk, jump or open the map): mouse clicks (hit-tested on the buttons), arrows / W A S D /
d-pad / left stick move the focus, Enter / Space / A choose, Q / E / Page Up / Page Down / LB / RB
change tabs, Esc / B is Done.

- **Wardrobe:** tabs Look / Top / Bottom / Shoes / Head / Face / Back. Look has round swatches for
  skin, hair color and eyes and icon cards for the hair styles; each slot tab has "None" and the
  owned items that fit, with their icons. A pick applies at once (mint card: worn).
- **Abby's shop:** Abby (big dark eyes, happy smile, tall top bun, triangle dress), the wallet's
  M¢, and every shop item that fits with its icon and price; owned ones are greyed "Owned". Picking
  a card selects it and focuses Buy; the answer shows under it ("Not enough M¢" or "Thank you!").

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
dressed mannequin (the Player's rig with the hair, skin and items applied); `WARDROBE_PREVIEW`
picks `look` (the Look tab), `shop` (Abby's shop after buying the Flower Clip and failing to afford
the Triangle Party Dress) or `items` (four outfits side by side).
