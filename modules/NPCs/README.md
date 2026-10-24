# NPCs module

The townsfolk from Tony's drawing: **Dr. Jeremy** at his clinic, your best friend **Millie** next
door, **Lucy**, the Pet Store cashier, and **Abby**, the clothing shopkeeper. Each stands at their spot, turns to face you when you
come near, and idles a little. Within 2.5 m a small "E / X: Talk" prompt floats over them, and
Interact starts a conversation in speech bubbles (the Dialogue module shows them).

- `Contract/` — `Fungtopia.NPCs.Contract` (namespace `Fungtopia.NPCs`):
  - `QuestState(bool Active, string Title, int Progress, int Goal, bool Complete)`: Millie's
    current quest, published every tick (for the HUD). `Title` is empty and `Goal` 0 until she
    gives the first one.
- `Core/` — `Fungtopia.NPCs.Core`: `NPCsModule`, the module's one `IModule`.
- `Godot/` — `Main.tscn` with `NPCsView`; `NpcFigure` (loads a model, toon materials, idle and
  talking animation); `Preview.tscn` (the four side by side, for screenshots); `Shaders/`
  (toon surface, ink outline).
- `Models/` — `npcs.py`, the Blender script that builds and exports `jeremy.glb`, `millie.glb`,
  `lucy.glb` and `abby.glb` next to it.
- `Tests/` — `NPCsModuleTests.cs`: xUnit tests of `NPCsModule` with a fake `IMessageBus`, a fake
  Dialogue, player and wallet: the prompt, facing, conversation flow, Jeremy's wake-up greeting,
  Millie's quests (progress, completion, payment, clothing rewards, the next ones), buying the
  crow (success, not enough coins, walking away, already owned), Abby opening her shop, and saving
  and restoring (round trip, no side effects, bad or unknown JSON keeps the defaults).

Test it with `./test.sh NPCs` from the repository root.

## Core

`NPCsModule` reads `Fungtopia.Input.PlayerInput` (`Interact`), `Fungtopia.Player.PlayerState`
(`Position`, `Alive`), `Fungtopia.Dialogue` (`DialogueState`, `DialogueAdvanced`,
`DialogueClosed`), `Fungtopia.DayNight.WokeUp`, `Fungtopia.Monsters.MonsterKilled`,
`Fungtopia.Wallet` (`CoinsEarned`, `SpendResult`), `Fungtopia.Pet.OwnedPets` and
`Fungtopia.Wardrobe.WardrobeState`, `Fungtopia.Save` `CollectSave` and `RestorePart`, and the
spots from `Fungtopia.Map.Places`. It publishes `SayLine`, `QuestState` (every tick),
`GrantCoins`, `SpendCoins`, `AdoptPet`, `Fungtopia.Wardrobe` `GrantItem` and `OpenShop`, and
`Fungtopia.Save.SavePart`.

| NPC (index) | Spot | Speaker id | Voice |
| --- | --- | --- | --- |
| Dr. Jeremy (0) | `Places.ClinicDoor` | 7100 | 2 kind |
| Millie (1) | `Places.MillieSpot` | 7101 | 1 bubbly |
| Lucy (2) | `Places.LucySpot` | 7102 | 0 deadpan |
| Abby (3) | `Places.AbbySpot` | 7103 | 1 bubbly |

Each tick:

- **Facing.** Within `FaceRadius` (6 m) of a living player an NPC turns toward them at
  `TurnSpeed` (5 rad/s, the short way round), otherwise back to its spot's facing (`Yaw[i]`).
- **Prompt.** `PromptNpc` is the nearest NPC within `PromptRadius` (2.5 m, horizontal) while the
  player is alive and no speech bubble, wardrobe or shop screen (`WardrobeState.Open`) is open,
  else -1; `PromptText` is "E / X: Talk" (or the
  buy prompt while Lucy offers the crow).
- **Talking.** `Interact` with the prompt showing starts that NPC's conversation: its lines go out
  one `SayLine` at a time, the next on its `DialogueAdvanced`, the last one with `Last` set. The
  press that dismisses a last line (`DialogueClosed` that tick) never starts a new talk. `Talking`
  is the NPC in a conversation, or -1.
- **Dr. Jeremy.** On `WokeUp` he says, without Interact, "Good morning! You fainted in the
  forest, but you're all patched up now." and that fainting costs no coins. Talking to him gives
  a friendly health tip (a different one each time).
- **Millie.** The first talk greets you, gives a quest and a tip (ants are fast, poison keeps
  working, pets fight on their own...). Quests come one at a time in this order, then again with
  bigger numbers (`Quest(n)`: goals and rewards times 1, 2, 3...): "Defeat 5 Mushroom
  Caterpillars" (150 M¢), "Defeat 3 Mushroom Ants" (150 M¢), "Bring me... earn 500 M¢ in the
  forest" (200 M¢). Progress counts only while a quest is active: `MonsterKilled` of the right
  kind (by the player or the pet), or the `Amount` of each `CoinsEarned` whose `Position` is in
  `Area.Forest` or `Area.DeepForest` (so her own reward in town does not count); it stops at the
  goal, and then the quest is `Complete`. Talking to her before that tells the progress and a
  tip; after it she pays the reward with one `GrantCoins` and gives the next quest. The second
  quest also gives the Flower Clip and the fourth the Mushroom Backpack (`GrantItem`, "Millie's
  quest"; `QuestItem(n)`, first round only), and she says so.
- **Lucy.** If the player does not own the Mushroom Crow (from `OwnedPets`), talking to her
  offers it for 3,000 M¢, ending with "Press E / X again to buy." Once that line is dismissed she
  is `OfferingCrow`: Interact near her says yes and publishes `SpendCoins` (request ids from
  71000 up, "Mushroom Crow"); walking out of the prompt radius says no. On its `SpendResult`:
  `Ok` publishes `AdoptPet(Crow)` and she says "Congrats. It bites."; otherwise "Come back with
  3,000 M¢." When the crow is owned she just says so.
- **Abby.** Talking to her says "Ooh, you'd look so cute in something new!"; once that line is
  dismissed she publishes `OpenShop` (the Wardrobe module shows the shop).

- **Saving.** On `CollectSave` it answers at once with one `SavePart` (module `"NPCs"`): compact
  JSON of `NPCsModule.SaveData` (`Version` = `SaveVersion` 1, `QuestsDone`, `QuestActive`,
  `QuestProgress`, `QuestComplete`, `RewardsGiven` (the clothes ids Millie gave) and `CrowBought`).
  On `RestorePart` for `"NPCs"` it restores exactly that (the quest's goal, reward and title follow
  from its number), quietly: no lines, coins, items, purchases or shop; any talk, crow offer or
  purchase under way is dropped. Empty, unreadable, negative or unknown-version JSON keeps the
  defaults (a new game's state) and logs one `WARNING: Fungtopia: NPCs: ...` line (`Log`, stderr).
  Millie never gives the same clothes twice.

No allocations per tick (only the occasional dialogue lines are built when a talk starts).

## View

`NPCsView` puts an `NpcFigure` at each spot, eases each one's yaw toward the Core's `Yaw`, and
shows the prompt (a cream rounded pill with an ink border, like the signposts', canvas layer 11)
over `PromptNpc`'s head. `NpcFigure` loads its `.glb` at run time from the file's bytes (so a fresh
checkout runs without an editor import), replaces each material with the toon shader plus an ink
outline, and animates the parts: breathing, a gentle sway and head tilt, arm swing, and while
talking a little bounce, nodding and a waving right arm. Jeremy is drawn 1.12 times as big (a
grown-up); Millie is the liveliest, Abby nearly as lively, Lucy barely moves.

Screenshots: `./screenshot.sh modules/NPCs/Godot/Preview.tscn` shows the four with Millie's
prompt; `NPC_PREVIEW=jeremy`, `millie`, `lucy` or `abby` shows only that one, closer.

## Models

`blender -b --factory-startup --python modules/NPCs/Models/npcs.py` (from the repository root)
writes `jeremy.glb`, `millie.glb`, `lucy.glb` and `abby.glb`; commit them with the script. Chibi folk in the
player characters' style: a big round head, a rounded body, short arms with round hands, stubby
legs. Dr. Jeremy: big dark eyes with sparkles, a neat side parting, a white doctor's coat with
lapels, buttons and a name badge over a blue shirt and red tie, a stethoscope. Millie: cat ears
with pink insides, happy ^ ^ closed eyes, honey hair in bunches, a pink bow at her neck, a mint
dress with white spots and a frilly hem, a little cat tail. Lucy: a pink flower in her purple bob,
half-lidded unimpressed eyes and a flat little mouth, a pale pink shirt saying "Pet Store" with little
hearts. Abby: big dark eyes with sparkles, a happy smile, auburn hair with a tall top bun, a pink
triangle dress with a white collar and hem. Objects with pivots where they move: `Body`, `Head` (neck), `LegL`/`LegR` (hip),
`ArmL`/`ArmR` (shoulder), `HandL`/`HandR`. Feet at the origin, facing -Z in Godot.
