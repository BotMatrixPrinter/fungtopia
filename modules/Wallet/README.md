# Wallet module

The player's Mushroom Coins (M¢). Every defeated monster pays its reward into the wallet,
whoever made the kill (the player or the pet): 50 M¢ for a normal monster, 200 M¢ for a hard one
(the `Reward` the Monsters module puts in `MonsterKilled`). Other modules can grant coins
(quest rewards, `GrantCoins`) and spend them (purchases, `SpendCoins`, which only goes through
when there are enough). Nothing else takes coins away: the player's defeat costs none. Coins are kept for the session (saving to disk comes later). Each
reward pops a little burst of gold mushroom coins out of the kill.

- `Contract/` — `Fungtopia.Wallet.Contract` (namespace `Fungtopia.Wallet`), see below.
- `Core/` — `Fungtopia.Wallet.Core`: `WalletModule`, the module's one `IModule`. It listens to
  `Fungtopia.Monsters.MonsterKilled`, `SpendCoins`, `GrantCoins` and `Fungtopia.Player.PlayerState`
  (for the player's position only), and keeps the latest 16 rewards in `Recent` (a ring,
  counted by `EarnedCount`) for the view.
- `Godot/` — `Main.tscn` with `WalletView`, which plays a `CoinBurst` (seven spinning gold
  coins flying up, golden sparkles, a rising "+50 M¢") at each reward's position, from a pool
  of eight; `Preview.tscn` (three bursts on a patch of grass, for screenshots:
  `./screenshot.sh modules/Wallet/Godot/Preview.tscn`, or one coin up close with
  `WALLET_PREVIEW=close`); `Shaders/` (toon surface, ink outline).
- `Models/` — `coin.py`, the Blender script that builds and exports `coin.glb` next to it: a
  chunky gold coin with a spotted mushroom on both faces (no eyes: only creatures have eyes).
- `Tests/` — `WalletModuleTests.cs`: xUnit tests of `WalletModule` with a fake `IMessageBus`.

Test it with `./test.sh Wallet` from the repository root.

## Contract

| Type | Who publishes it | What it is |
| --- | --- | --- |
| `WalletState` | Wallet, every tick | `Coins`: M¢ earned this session. |
| `CoinsEarned` | Wallet, once per reward | `Amount` (M¢, more than 0), `Position` (where it was earned: the defeated monster's feet, or the player's feet for a `GrantCoins`, meters). |
| `SpendCoins` | anyone (shops) | `RequestId` (the asker's, echoed back), `Amount` (M¢), `What` (what is bought, e.g. "Mushroom Crow"). |
| `SpendResult` | Wallet, once per `SpendCoins`, right away | `RequestId`, `Ok` (the coins were taken), `CoinsLeft` (M¢ in the wallet afterwards). |
| `GrantCoins` | anyone (quests) | `Amount` (M¢), `Why` (e.g. "Millie's quest"). |

A `MonsterKilled` with a `Reward` of 0 or less adds nothing (and publishes no `CoinsEarned`);
the wallet stops at `int.MaxValue` rather than wrapping around.

A `SpendCoins` takes `Amount` only if the wallet holds at least that much (spending it all is
fine); otherwise, or when `Amount` is negative, the answer has `Ok` false and nothing changes.
Either way exactly one `SpendResult` is published, during the `Publish` of the request, with
the same `RequestId`. Spending publishes no `CoinsEarned`.

A `GrantCoins` adds `Amount` like a reward: it publishes a `CoinsEarned` at the player's
position (the latest `PlayerState`; the origin before the first one) so the HUD's "+N" pop-up
and the view's coin burst show there. An `Amount` of 0 or less adds nothing.
