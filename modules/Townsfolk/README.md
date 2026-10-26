# Townsfolk module

Pet City's background life, so the city feels busy: 25 townsfolk walking about the plaza and
the avenue, chatting in pairs, sitting on benches, looking into shop windows, going in and out of
the buildings, some walking a cat or a dog; about 10 Mushroom Cats, Dogs and Crows roaming on
their own; and butterflies over the planters and the plaza by day. Only Pet City. Townsfolk
cannot be talked to, are not obstacles and never push or block the player: they step aside.

- `Contract/` — `Fungtopia.Townsfolk.Contract`: empty; the module publishes nothing other modules need.
- `Core/` — `Fungtopia.Townsfolk.Core`: `TownsfolkModule`, the module's one `IModule`; `CityLife`
  (where life happens, from the Map's data); `Looks` (the palettes and the seeded mix of looks);
  `Stride` (folk.glb's leg rig and the leg poses for walking and sitting); `Life.cs` (`Folk`,
  `Critter`, `Butterfly` and their states).
- `Godot/` — `Main.tscn` with `TownsfolkView`; `Figures` (loads the models, toon materials, tints);
  `Preview.tscn` (`TownsfolkPreview`, for screenshots); `Shaders/` (toon surface, ink outline).
- `Models/` — `townsfolk.py`, the Blender script that builds and exports `folk.glb`, `cat.glb`,
  `dog.glb`, `crow.glb` and `butterfly.glb` next to it.
- `Tests/` — `TownsfolkModuleTests.cs`: xUnit tests of `TownsfolkModule` and `CityLife` with a
  fake `IMessageBus` (looks, leg poses walking and sitting, spots, ten minutes of life staying in Pet City off obstacles and
  clear of the player, chats facing each other, stepping aside, night and morning, nothing run far
  away, pets, butterflies, the same run every time); `TownsfolkModelsTests.cs`: the `.glb` models
  read back (the folk's rig and the drawing's shapes and proportions: the short body round at the
  base, the legs inside it).

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

**Folk** (`Folk`, 25; `Look` from `Looks.Mix`: skin tone, a `BodyShape` (a `Dress` or a
`Capsule`, about half each) and its color, the legs' color (about 1 in 3 the skin's), an
`Accessory` (a `BowTie` on most capsules, a `Bow` on the head for some dresses without a hat) and
its color, a mushroom-cap `HatStyle` (Dome, Flat, Tall, for about 4 in 10) and its color, and a
`Height` of 1.19..1.37 m; the model, 1.30 m (`Look.ModelHeight`), is drawn at `Look.Scale`, 0.92..1.05). Each decides
in turn, after standing a moment (`Waiting`): meet someone strolling within 22 m for a chat (both
walk to either side of their midpoint, then `Chatting` 7..16 s facing each other, taking turns
`Speaking`), sit on a free bench seat (`Sitting` 10..30 s), walk to a door and go in (`Inside`
12..45 s, then out of the same door), look into a shop window (`Browsing` 4..10 s), or stroll to
open ground (mostly the plaza and the avenue). Walking (0.95..1.45 m/s) steers round
`Ground.Obstacles` and the buildings (trying turns of up to 2.5 rad either way), never onto
ground that is not walkable or not Pet City, keeps `PlayerClearance` (1.1 m) from the player
rather than walking closer (within it, not even one step closer), and folk walking into each other are pushed gently apart. Standing,
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

## View

`TownsfolkView` (hidden, and doing nothing, while the Core is not `Active`) makes a figure per
townsperson, pet and butterfly the first time Pet City comes alive. `Figures` loads each `.glb` at
run time from the file's bytes once, makes copies, draws them with the toon shader and an ink
outline, and tints by material name (`Skin`, `Outfit`, `Legs`, `Bow`, `Hat`; `Fur`, `FurDark`,
`Feather`; `Wing`); the folk show only their `Look`'s body shape, hat, bow tie or bow. Each frame every figure is placed between its previous and current tick
(`Engine.GetPhysicsInterpolationFraction`) and animated: folk bob as they walk, sway, swing their
floating hands (the arms are empty pivots, the hands follow them) and their tube legs from the hip,
the knee bending as a leg swings forward (`Stride.Walk`), breathe, look about; chatting, the one speaking nods and gestures with both hands while the other
nods along; browsing, they tilt their heads to the window; sitting, they sit with the body's bottom on the
seat (`Stride.SitLift`), hands in the lap, thighs along the seat and shins bent down over its front
edge, feet swinging (`Stride.Sit`). Cats and dogs trot (diagonal legs together), sniff with the head down,
sit, nap lying down with legs tucked and slow breathing, wag their tails; crows hop, peck, look
about on roofs and flap in flight; butterflies (drawn 1.8 times the model's size, to be seen) flap fast and fade out at night.

`TOWNSFOLK_START=plaza` (screenshots only) makes the view ask its Core to publish one
`TeleportPlayer` to `TownsfolkModule.ScreenshotSpot`, on the avenue just south of the plaza. The
game's start screen still covers the first frames, so `TOWNSFOLK_START=plaza ./screenshot.sh`
alone shows the start screen; a game screenshot near the plaza needs the start screen skipped too.

## Screenshots

`./screenshot.sh modules/Townsfolk/Godot/Preview.tscn` shows the plaza from above the avenue by
day, two minutes into the simulation, on the Map's ground with Pet City (their scenes, read-only).
`TOWNSFOLK_PREVIEW=close` looks from among the folk on the plaza; `near` from 3 m at the one out
nearest the plaza's middle, `sit` from 2.4 m at one sitting on a bench; add `night` (for example
`TOWNSFOLK_PREVIEW=night` or `close,night`) for 22:00. `SCREENSHOT_FRAMES=12` is enough.

## Models

`blender -b --factory-startup --python modules/Townsfolk/Models/townsfolk.py` (from the
repository root) writes the five models; commit them with the script. The folk follow the
designer's drawing (Saria's "you choose between these" page, girl and boy); the pets are in the
Pet module's style, simpler. No eyes or faces on hats or objects. Feet at the origin, facing -Z in
Godot.

- `folk.glb`: one base townsperson, 1.30 m, of a few clean simple shapes the view tints and
  dresses (Tony, 2026-10-26: the body about 25% shorter than the old 1.47 m one's and rounder at
  the base, the legs wider apart inside it, as the Player's): a big plain round `Head` (0.38 m
  across, its center at 1.11 m, its top at 1.30 m, pivot at its bottom) with only two big dark oval
  eyes with white sparkles, set low and wide, and small soft rosy cheeks (no mouth, nose, ears or
  hair), floating 4 cm over the body (no neck); `Body`, an empty at the hip (0.42 m) holding the
  two body shapes, smooth solids of revolution from 0.36 to 0.88 m: `Dress` (a triangle from the
  front, narrow and round at the top, 0.42 m wide low down, with a fully rounded, softly curved
  bottom, no sharp rim) and `Capsule` (a soft egg: 0.28 m wide near the top, widest, 0.38 m, low
  down, curving round underneath), and `BowTie` (a big bow tie, as wide as the egg there, at its
  top); two big oval hands `HandL`/`HandR` (0.18 m tall, 0.14 m wide, three dark finger grooves
  each) floating well out beside the body at its middle height, at the ends of empty
  `ArmL`/`ArmR`; two tube legs (0.06 m radius) from under the round base, inside its outline seen
  from the front, `LegL`/`LegR` (thighs, hip pivots at x -/+0.11 m) with `ShinL`/`ShinR` (knee
  pivots at 0.22 m) down to rounded feet a little wider than the legs; the heights are `Stride`'s. The Head's children: mushroom-cap hats `HatDome`,
  `HatFlat`, `HatTall` (`Hat`, `HatSpots`, `Gills`) and `Bow` (a bow on the head).
- `cat.glb`, `dog.glb`: simpler Mushroom Cat (mushroom-cap ears) and Mushroom Dog (a little
  mushroom on its back, a collar): `Body`, `Head`, `LegFL`, `LegFR`, `LegBL`, `LegBR`, `Tail`.
- `crow.glb`: a simpler Mushroom Crow: `Body` (with a mushroom cap), `Head`, `WingL`, `WingR`,
  `Tail`, `LegL`, `LegR`.
- `butterfly.glb`: `Body`, `WingL`, `WingR` (`Wing`, tinted, with `WingSpot`s).
