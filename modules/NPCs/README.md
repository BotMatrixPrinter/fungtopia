# NPCs module

The townsfolk from Tony's drawing: **Dr. Jeremy** at his clinic, your best friend **Millie** next
door, **Lucy**, the Pet Store cashier, **Abby**, the clothing shopkeeper, and **Pip**, the cook at
the Mushroom Munchies food stall. Each stands at their spot, turns to face you when you come near
(Pip keeps stirring the soup pot and waves instead), and idles a little. Within 2.5 m a small
"[Interact icon] Talk" prompt ("[Interact icon] Food" at Pip's counter) floats over them, showing only
the button of the device in use (the E key, Xbox X, PlayStation Square...), and Interact starts a
conversation in speech bubbles (the Dialogue module shows them).

- `Contract/` — `Fungtopia.NPCs.Contract` (namespace `Fungtopia.NPCs`):
  - `QuestState(bool Active, string Title, int Progress, int Goal, bool Complete)`: Millie's
    current quest, published every tick (for the HUD). `Title` is empty and `Goal` 0 until she
    gives the first one.
- `Core/` — `Fungtopia.NPCs.Core`: `NPCsModule`, the module's one `IModule`; `MushroomLook`, how
  the mushroom parts are shaded.
- `Godot/` — `Main.tscn` with `NPCsView`; `NpcFigure` (loads a model, toon materials, idle and
  talking animation); `Preview.tscn` (the five side by side, or Pip at the stall, for screenshots);
  `Shaders/` (toon surface with optional gloss band and rim strength, ink outline).
- `Models/` — `npcs.py`, the Blender script that builds and exports `jeremy.glb`, `millie.glb`,
  `lucy.glb`, `abby.glb` and `pip.glb` next to it.
- `Tests/` — `NPCsModuleTests.cs`: xUnit tests of `NPCsModule` with a fake `IMessageBus`, a fake
  Dialogue, player and wallet: the prompt (each device kind's prompts show only its own Interact
  icon, and follow `ActiveDevice` live; no line names a button), facing, conversation flow, Jeremy's wake-up greeting,
  Millie's quests (progress, completion, payment, clothing rewards, the next ones), buying the
  crow (success, not enough coins, walking away, already owned), Abby opening her shop, Pip (where
  they stand, the prompt's range at the counter, greeting then the food menu, the greetings taking
  turns, no prompt or talk while a menu holds `UiFocus`), and saving and restoring (round trip, no
  side effects, bad or unknown JSON keeps the defaults); and the five `.glb` models read back (the
  1.80 m rig's parts and pivots `NpcFigure` animates, scaled to each one's `Heights`, the top of
  each under its `Tops`, no stool; the round head floating about 5 cm over the tapered torso;
  round tube legs in rounded shoes; floating round hands with air to the body, no arm meshes; and
  each one's look: Jeremy's coat, badge, stethoscope and trousers, Millie's ears, skirt and tail,
  Lucy's flower, "Pet Store" print and skirt, Abby's bun and flaring dress, Pip's hat, muffin
  apron over the legs and spoon); Pip's vivid mushroom hat (cherry red spots, pure white
cap, light cream gills, Pip's other colors unchanged) and `MushroomLook` (only the cap gets the
gloss and the stronger rim, and the toon shader takes them).

Test it with `./test.sh NPCs` from the repository root.

## Core

`NPCsModule` reads `Fungtopia.Input.PlayerInput` (`Interact`), `Fungtopia.Player.PlayerState`
(`Position`, `Alive`), `Fungtopia.Dialogue` (`DialogueState`, `DialogueAdvanced`,
`DialogueClosed`), `Fungtopia.DayNight.WokeUp`, `Fungtopia.Monsters.MonsterKilled`,
`Fungtopia.Wallet` (`CoinsEarned`, `SpendResult`), `Fungtopia.Pet.OwnedPets` and
`Fungtopia.Wardrobe.WardrobeState`, `Fungtopia.Input.UiFocus` and `ActiveDevice`, `Fungtopia.Save` `CollectSave` and
`RestorePart`, and the spots from `Fungtopia.Map.Places`. It publishes `SayLine`, `QuestState`
(every tick), `GrantCoins`, `SpendCoins`, `AdoptPet`, `Fungtopia.Wardrobe` `GrantItem` and
`OpenShop`, `Fungtopia.Food.OpenFoodMenu`, and `Fungtopia.Save.SavePart`.

| NPC (index) | Spot | Speaker id | Voice |
| --- | --- | --- | --- |
| Dr. Jeremy (0) | `Places.ClinicDoor` | 7100 | 2 kind |
| Millie (1) | `Places.MillieSpot` | 7101 | 1 bubbly |
| Lucy (2) | `Places.LucySpot` | 7102 | 0 deadpan |
| Abby (3) | `Places.AbbySpot` | 7103 | 1 bubbly |
| Pip (4) | behind the food stall's counter: `PipBehindCounter` (2.6 m) behind `Places.FoodSpot` and `PipBesidePot` (0.4 m) to their left, facing the same way; talked to from `Places.FoodSpot` | 7104 | 1 bubbly |

Each tick:

- **Facing.** Within `FaceRadius` (6 m) of a living player an NPC turns toward them at
  `TurnSpeed` (5 rad/s, the short way round), otherwise back to its spot's facing (`Yaw[i]`).
  Pip always faces the counter (stirring the pot on it). `Noticing` is the nearest NPC within
  `FaceRadius` of the living player, or -1 (the view makes Pip wave when it becomes Pip).
- **Prompt.** `PromptNpc` is the NPC whose talk spot (`TalkSpots`: its own spot, but
  `Places.FoodSpot` for Pip) is nearest within `PromptRadius` (2.5 m, horizontal) while the
  player is alive and no speech bubble, wardrobe or shop screen (`WardrobeState.Open`) or menu
  holding `UiFocus` (the food menu, the start screen...) is open, else -1; `Prompt` is
  `PromptKind.Talk`, `Food` for Pip, or `Buy` while Lucy offers the crow. `Device` is the latest
  `ActiveDevice`'s `Kind`, and `PromptText` the prompt as RichTextLabel BBCode for it:
  `Prompts.Rich(Interact, Device, PromptIconSize)` (36 px) and "Talk", "Food" or "Buy the Mushroom Crow
  (1,500 M¢)" (`PromptBbcode(kind, device)`; every prompt for every device is built once at startup).
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
  offers it for 1,500 M¢, ending with "Ask me again to buy it." (the speech bubbles are plain text, so lines name no
  buttons; the buy prompt then shows the icon). Once that line is dismissed she
  is `OfferingCrow`: Interact near her says yes and publishes `SpendCoins` (request ids from
  71000 up, "Mushroom Crow"); walking out of the prompt radius says no. On its `SpendResult`:
  `Ok` publishes `AdoptPet(Crow)` and she says "Congrats. It bites."; otherwise "Come back with
  1,500 M¢." When the crow is owned she just says so.
- **Abby.** Talking to her says "Ooh, you'd look so cute in something new!"; once that line is
  dismissed she publishes `OpenShop` (the Wardrobe module shows the shop).
- **Pip.** Talking to Pip says one short greeting (`PipGreetings`, a different one each talk, in
  turn: "Fresh from the oven!", "Hungry from the forest? I've got you!", "A pie a day keeps the
  doctor away... sorry, Dr. Jeremy!"); once it is dismissed Pip publishes `OpenFoodMenu` (the Food
  module opens the stall's menu, and stops opening it on its own). Not when another speaker took
  the bubble or the game was restored meanwhile. While the menu is open (it holds `UiFocus`),
  Interact buys there and does not talk to Pip.

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

`NPCsView` puts an `NpcFigure` at each spot (Pip on the stall's floor, `StallFloor` above the
ground), eases each one's yaw toward the Core's `Yaw`, and
shows the prompt (a cream rounded pill with an ink border, like the signposts', canvas layer 11)
over `PromptNpc`'s head (0.25 m over `NPCsModule.Tops`, the top of their hair, ears, bun or hat): a one-line `RichTextLabel` sized to its content (`FitContent`), so the pill
grows with the icon, redrawn when `PromptText` changes (another prompt or device). `SetPrompt` makes
the icons loadable when Godot has not imported them (as the Input module's view does), so the preview
shows them too. `NpcFigure` loads its `.glb` at run time from the file's bytes (so a fresh
checkout runs without an editor import), replaces each material with the toon shader plus an ink
outline (Pip's mushroom-cap hat, `MushroomLook` in Core, also gets a soft glossy highlight band on
the cap and a stronger rim light along its edge and spots; no glint inside the spots, which would
look like eyes), and animates the parts: breathing, a gentle sway and head tilt, the floating hands
swinging, the weight shifting from leg to leg (the hips and everything over them slide a little
sideways while the legs lean under them, the shoes staying put), and while talking a little bounce,
little steps in place, nodding and a waving right hand. The arms are empty pivots (nothing draws
them): turning an arm moves its hand along. The legs turn at the hips, the shoes hanging from them.
The models are at their real heights (`NPCsModule.Heights`), so nothing is scaled; Millie is the
liveliest, Abby nearly as lively, Lucy barely moves. Pip, tall enough to reach over the counter
(1.03 m), stirs the soup pot on it (the right hand up over its rim, circling, the spoon tipped into it), and when
the player comes near (`Noticing`) waves with the left hand for 2.5 s, and while talking.

Screenshots: `./screenshot.sh modules/NPCs/Godot/Preview.tscn` shows the five with Millie's
prompt (`NPC_DEVICE=xbox`, `playstation`, `nintendo` or `generic` for that pad's button, else the
keyboard's); `NPC_PREVIEW=jeremy`, `millie`, `lucy`, `abby` or `pip` shows only that one, closer.
`NPC_PREVIEW=stall` (best with `SCREENSHOT_FRAMES=70`, mid-wave) shows Pip behind the Mushroom
Munchies counter (the Map and Town modules' scenes, loaded read-only) with the "[Interact icon] Food" prompt,
as a player walking up to `Places.FoodSpot` sees it.

## Models

`blender -b --factory-startup --python modules/NPCs/Models/npcs.py` (from the repository root)
writes `jeremy.glb`, `millie.glb`, `lucy.glb`, `abby.glb` and `pip.glb` (`... npcs.py -- pip` only
that one); commit them with the script. Grown-ups made of simple shapes in Saria's style (floating
head and floating round hands like Animal Crossing, no visible arms or neck) with normal
proportions and real legs (Tony, 2026-10-25: "1.8 meters average", "they do have legs in the
drawings", "make them look more normal"). The rig of a 1.80 m character (feet at 0): rounded tube
legs (0.065 m radius) from the hip pivots `LegL`/`LegR` (y 0.90, x -/+0.10) down into rounded
shoes (`FootL`/`FootR`, children of the legs, 0.12 m tall, 0.24 m long); the torso (`Body`, pivot at
the hips) a smooth tapered shape of elliptic cross-sections from 0.88 to 1.44 m (shoulders 0.40 m
wide, waist 0.30, hips 0.34, 0.22 m deep); shoulder pivots `ArmL`/`ArmR` (y 1.40, x -/+0.22, empty,
invisible) with round hands `HandL`/`HandR` (0.055 m radius) at the ends of 0.66 m arms, beside the
thighs; the head (`Head`, pivot at the neck, y 1.48) round, 0.30 m tall and 0.27 m wide, centered at
1.64, floating 5 cm over the torso. Each one is that rig scaled uniformly by height / 1.80:
Dr. Jeremy 1.86 m, Millie 1.72, Lucy 1.78, Abby 1.75, Pip 1.70 (`HEIGHTS`, in step with
`NPCsModule.Heights`). The faces and hair are drawn on a sketch head and fitted to the head's size.
The outfits color the torso and legs (bare skin legs under dresses and skirts).
Dr. Jeremy: big dark eyes with sparkles, a neat side parting, his white doctor's coat over the torso
with tails down to the knees, open in a V at the top over a blue shirt and red tie, with lapels,
buttons and a name badge, a stethoscope round his shoulders with its chest piece in front, dark
blue trousers. Millie: cat ears with pink insides, happy ^ ^ closed eyes, honey hair in bunches, a
mint dress with white spots flaring into a skirt with a frilly hem, a pink bow at its top, a little
curled cat tail behind. Lucy: a pink flower in her purple bob, half-lidded unimpressed eyes and a
flat little mouth, a pale pink shirt saying "Pet Store" with little hearts over a blue skirt.
Abby: big dark eyes with sparkles, a happy smile, auburn hair with a tall top bun, her pink triangle
dress flaring wide from the waist with a white collar and hem. Pip: big dark eyes with sparkles,
rosy cheeks with freckles, a big smile, short ginger hair, a puffy chef's hat shaped like a
mushroom cap, vivid like every mushroom in the game: crisp pure white with pure cherry red spots
and clean light cream gills, a coral shirt, brown trousers, a cream apron from the chest down over
the legs (tied with a bow at the back) with a little muffin printed on it (pink cup, blueberries, a
cherry), a wooden spoon in the right hand (`Spoon`, a child of `HandR`). No step stool: at 1.70 m Pip
reaches over the stall's 1.03 m counter. Feet at the origin, facing -Z in Godot. No eyes on objects.
