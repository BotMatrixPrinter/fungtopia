# Townsfolk module

Pet City's background life, so the city feels busy: 25 mushroom folk walking about the plaza and
the avenue, chatting in pairs, sitting on benches, looking into shop windows, going in and out of
the buildings, some walking a cat or a dog; about 10 Mushroom Cats, Dogs and Crows roaming on
their own; and butterflies over the planters and the plaza by day. Only Pet City. Townsfolk
cannot be talked to, are not obstacles and never push or block the player: they step aside.

- `Contract/` — `Fungtopia.Townsfolk.Contract`: empty; the module publishes nothing other modules need.
- `Core/` — `Fungtopia.Townsfolk.Core`: `TownsfolkModule`, the module's one `IModule`; `CityLife`
  (where life happens, from the Map's data); `Looks` (the palettes and the seeded mix of looks);
  `Body` (the 1.80 m rig's measures and how it walks, sits and looks); `Life.cs` (`Folk`,
  `Critter`, `Butterfly` and their states).
- `Godot/` — `Main.tscn` with `TownsfolkView`; `Figures` (loads the models, toon materials, tints);
  `Preview.tscn` (`TownsfolkPreview`, for screenshots); `Shaders/` (toon surface, ink outline).
- `Models/` — `townsfolk.py`, the Blender script that builds and exports `folk.glb`, `cat.glb`,
  `dog.glb`, `crow.glb` and `butterfly.glb` next to it.
- `Tests/` — `TownsfolkModuleTests.cs`: xUnit tests of `TownsfolkModule` and `CityLife` with a
  fake `IMessageBus` (looks and heights, spots, ten minutes of life staying in Pet City off
  obstacles and clear of the player, chats facing each other at their own heights, stepping aside,
  night and morning, nothing run far away, pets, butterflies, the same run every time; `Body`'s
  strides, bench sitting and looking up or down); `TownsfolkModelsTests.cs`: the `.glb` models read
  back (the folk's rig and proportions against `Body`).

Test it with `./test.sh Townsfolk` from the repository root.

## Core

`TownsfolkModule` reads `Fungtopia.Player.PlayerState` (`Position`, `Alive`) and
`Fungtopia.DayNight.TimeOfDay` (`Hour`), and the Map's `Areas`, `Ground`, `Buildings`, `Props` and
`Places`. It publishes nothing (only `TeleportPlayer` once for the screenshot start below).
Everything is seeded (`Seed`), so a run is the same every time; nothing allocates per tick.

It runs only while the player is within `ActiveMargin` (45 m) of Pet City's rectangle (`Active`,
`Near(x, z)`); far away it does nothing at all. Coming back, it first settles who should be in for
the night (or up for the day).

`CityLife` (fixed data from the Map, worked out once; every standing spot in `Area.PetCity`,
walkable and clear for a `FolkRadius` (0.35 m) walker):

- `Doors`: 1.3 m in front of each city block's front middle, and two at the pet store's open front,
  4.5 m to each side of Lucy; `Windows`: two beside each city block's door, facing it.
- `Seats` (two per Pet City bench, on the bench facing its front) and `SeatApproach` (in front of each).
- `Open`: a 2.5 m grid of open ground (1 m to spare), the plaza (17 m round `Buildings.CityPlaza`)
  and the avenue first (`BusyCount`).
- `Roofs` (the top middle of each Pet City building, for crows) and `Flowers` (each planter's top
  and six points round the plaza's middle, for butterflies).

**Folk** (`Folk`, 25; `Look` from `Looks.Mix`: skin tone, `HairStyle` (Short, Bob, Buns, Spiky,
Long) and hair color, outfit and trim colors, a dress or trousers, a mushroom-cap `HatStyle`
(Dome, Flat, Tall, or none for about 3 in 10) and its color, and a `Height`, 1.65..1.95 m (evenly,
so about 1.80 on average; `Scale` = `Height / 1.80`, how big folk.glb is drawn)). Each decides
in turn, after standing a moment (`Waiting`): meet someone strolling within 22 m for a chat (both
walk to either side of their midpoint, then `Chatting` 7..16 s facing each other, taking turns
`Speaking`), sit on a free bench seat (`Sitting` 10..30 s), walk to a door and go in (`Inside`
12..45 s, then out of the same door), look into a shop window (`Browsing` 4..10 s), or stroll to
open ground (mostly the plaza and the avenue). Walking (0.95..1.45 m/s) steers round
`Ground.Obstacles` and the buildings (trying turns of up to 2.5 rad either way), never onto
ground that is not walkable or not Pet City, keeps `PlayerClearance` (1.1 m) from the player
rather than walking closer, and folk walking into each other are pushed gently apart. Standing,
chatting or browsing folk shuffle out of the player's way when the player comes within
`StepAsideRadius` (0.95 m). A walk that takes too long (stuck) is given up.

**Night.** Every third one (`NightOwl`, 9 of 25) stays out at night. The others head for the
nearest door at their `HomeHour` (18:00..19:18) and go in (`InForNight`); any still out at dusk
(20:00) slip in at once. They come out after their `WakeHour` (6:18..8:30).

**Pets** (`Critter`): `RoamingPetCount` (10: 4 cats, 3 dogs, 3 crows) roaming on their own, then
`OwnedPetCount` (6) cats and dogs walking with every fourth townsperson (`Owner`), each a seeded
fur or feather color and size. A pet with its owner trots at their side (`LeashSide` 0.75 m to the
right, `LeashBack` 0.35 m behind), sits by them while they stand, chat or sit on a bench, and
goes in and out with them. Roaming cats and dogs wander, sniff about (`Sniff`), nap (`Nap`
12..35 s, mostly at night) and chase each other (`Chase`/`Flee`, 4..7 s). Crows hop (`Hop`) and
peck on the ground, fly (`Fly`, an arc) between rooftops and the plaza, perch (`Perch`) on roofs
(at night too), and fly off to a roof when the player comes within `CrowStartle` (2.5 m).

**Butterflies** (`Butterfly`, 16): each flutters round one of `CityLife.Flowers` (0.3..1.5 m up,
a jittery zigzag), now and then moving on to another within `MaxFlowerHop` (14 m).
`ButterfliesOut` fades to 0 over 3 s at night (all hidden) and back by day.

Each tick stores `Previous` positions and yaws so the view draws between ticks.

**Body** (`Body`): the rig's measures (the same numbers as townsfolk.py: hips 0.90, knees 0.48,
shoulders 1.40, neck 1.48, head 1.64 m, `SeatHeight` 0.52 m, the top of PetCity's bench seat) and
the pose math the view uses. `Legs(cycle, speed)`: half of each leg's cycle planted (straight, its
foot moving back at an even pace), half swinging forward with a bent knee, the right half a cycle
behind, and `Drop` lowers the body so the lower foot is always on the ground; `CycleRate` turns the
cycle so the planted foot moves back exactly as fast as the body goes forward (steps of about
0.6 m, scaled with height). `Sit(scale)`: the hips down on the seat, a little back toward the
backrest, the thighs forward over the seat's edge (tipped down for the short, whose shins would not
reach) and the knees bent so the feet stand on the ground. `LookPitch`: chatting, how far to tip
the head up or down to the other's eyes.

## View

`TownsfolkView` (hidden, and doing nothing, while the Core is not `Active`) makes a figure per
townsperson, pet and butterfly the first time Pet City comes alive. `Figures` loads each `.glb` at
run time from the file's bytes once, makes copies, draws them with the toon shader and an ink
outline, and tints by material name (`Skin`, `Hair`, `Outfit`, `Trim`, `Trousers`, `Hat`; `Fur`,
`FurDark`, `Feather`; `Wing`); the folk show only their `Look`'s hair and hat, and the `Trousers`
band only with trousers. Each frame every figure is placed between its previous and current tick
(`Engine.GetPhysicsInterpolationFraction`), drawn `Look.Scale` times folk.glb, its `Legs` tinted
the trousers' color or skin (under a dress, whose `Skirt` shows instead of `Trousers`), and
animated with `Body`: folk walk with real strides (thighs on the hips, shins on the knees), the
body sinking a little as the legs spread, sway, swing their floating hands against the legs (the
arms are empty pivots, the hands follow them), breathe, look about; chatting, they look each other
in the eyes, up or down to the other's height, the one speaking nods and gestures (the elbows
bending the hands up in front) while the other nods along; browsing, they tilt their heads to the
window; sitting, the hips and knees bend to fit the bench, feet on the ground, hands in the lap,
a skirt drawn up over the lap. Cats and dogs trot (diagonal legs together), sniff with the head down,
sit, nap lying down with legs tucked and slow breathing, wag their tails; crows hop, peck, look
about on roofs and flap in flight; butterflies (drawn 1.8 times the model's size, to be seen) flap fast and fade out at night.

`TOWNSFOLK_START=plaza` (screenshots only) makes the view ask its Core to publish one
`TeleportPlayer` to `TownsfolkModule.ScreenshotSpot`, on the avenue just south of the plaza. The
game's start screen still covers the first frames, so `TOWNSFOLK_START=plaza ./screenshot.sh`
alone shows the start screen; a game screenshot near the plaza needs the start screen skipped too.

## Screenshots

`./screenshot.sh modules/Townsfolk/Godot/Preview.tscn` shows the plaza from above the avenue by
day, two minutes into the simulation, on the Map's ground with Pet City (their scenes, read-only).
`TOWNSFOLK_PREVIEW=close` looks from among the folk on the plaza; `folk` from 4.5 m in front of
someone on a bench, at eye height, and `folk,chat` at a chatting pair (simulated on until a chat
starts); add `night` (for example `TOWNSFOLK_PREVIEW=night` or `close,night`) for 22:00. `SCREENSHOT_FRAMES=12` is enough.

## Models

`blender -b --factory-startup --python modules/Townsfolk/Models/townsfolk.py` (from the
repository root) writes the five models; commit them with the script. All in the NPCs' style
(see the NPCs README's Models) and the Pet module's, simpler, with no eyes or faces on hats or
objects. Feet at the origin, facing -Z in Godot.

- `folk.glb`: one base townsperson the view tints and dresses, a 1.80 m grown-up in Saria's
  simple-shapes style (feet at 0, the top of the head at 1.80; `Body` in the Core has the same
  numbers): a round `Head` (neck pivot 1.48 m; 0.27 m wide and 0.30 m tall round 1.64 m; skin, big
  dark eyes with sparkles, blush, a smile, little ears) floating 5 cm over a smooth tapered torso
  `Body` (hip pivot 0.90 m; 0.88..1.44 m, shoulders 0.40 m wide, waist 0.30, hips 0.34, 0.22 deep;
  `Outfit` with a `Trim` hem at 1.0 m; children `Trousers`, a band round the hips, `Skirt`, a
  flared skirt to 0.66 m with a `Trim` hem, and `Collar`, a ring at its top); real legs: thighs
  `LegL`/`LegR` (hip pivots at x -/+0.10, 0.90 m) and shins `ShinL`/`ShinR` (knee pivots, 0.48 m),
  rounded tubes 0.065 m round (`Legs`), with rounded shoes `FootL`/`FootR` (0.12 m tall, 0.24 long)
  on the shins; round hands `HandL`/`HandR` (0.055 m round) floating 0.66 m from empty shoulder
  pivots `ArmL`/`ArmR` (x -/+0.22, 1.40 m); no visible arms or neck. The head is drawn at the NPCs'
  size, then shrunk, so its face, hair and hats keep their look. The Head's children: hair styles `HairShort`, `HairBob`, `HairBuns`, `HairSpiky`, `HairLong` (material
  `Hair`) and mushroom-cap hats `HatDome`, `HatFlat`, `HatTall` (`Hat`, `HatSpots`, `Gills`).
- `cat.glb`, `dog.glb`: simpler Mushroom Cat (mushroom-cap ears) and Mushroom Dog (a little
  mushroom on its back, a collar): `Body`, `Head`, `LegFL`, `LegFR`, `LegBL`, `LegBR`, `Tail`.
- `crow.glb`: a simpler Mushroom Crow: `Body` (with a mushroom cap), `Head`, `WingL`, `WingR`,
  `Tail`, `LegL`, `LegR`.
- `butterfly.glb`: `Body`, `WingL`, `WingR` (`Wing`, tinted, with `WingSpot`s).
