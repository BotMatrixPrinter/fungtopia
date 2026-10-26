# Player module

The player character: a simple-shapes Girl or Boy (Saria's drawing) who walks the whole world (Mushroom Town, the roads,
Mushroom Forest, Pet City, the home interior) in third person, relative to the camera, turns
smoothly, jumps, follows the ground, slides along giant mushroom trunks, buildings and the edge
of the walkable ground, stands still while talking, stands idle and turns for viewing while the
wardrobe or Abby's shop is open, takes damage from monsters and stays down
after defeat until it is teleported (to the clinic the next morning).

- `Contract/` — `Fungtopia.Player.Contract` (namespace `Fungtopia.Player`), see below.
- `Core/` — `Fungtopia.Player.Core`: `PlayerModule`, the module's one `IModule`; `ArcheryPose`,
  `ArcheryState` and `ArcheryPose.Frame` (the archery pose as pure functions, for the view); `CharacterLook`, `Rig`
  and `BuiltinPieces` (what the character looks like from the Wardrobe's `AppearanceState`);
  `Mushrooms` (the carried mushrooms' materials, colors, gloss, rim light and glow).
- `Godot/` — `Main.tscn` with `PlayerView`, which draws the character; `CharacterModel` (the
  model, toon materials and procedural animation); `Preview.tscn` (both characters side by side,
  for screenshots); `Shaders/` (toon surface with hit flash and, for mushroom caps, a glossy band and
  rim light; ink outline).
- `Models/` — `characters.py`, the Blender script that builds and exports `girl.glb` and
  `boy.glb` next to it.
- `Tests/` — `PlayerModuleTests.cs`: xUnit tests of `PlayerModule` (with a fake `IMessageBus`;
  also the save part's round trip and bad saved Json),
  `ArcheryPose` and `CharacterLook` (also standing idle and turning while the wardrobe is open),
  and of `girl.glb` and `boy.glb` read back (the rig's parts and pivots; the big bald round head
  with only two big dark eyes floating over the Girl's rounded triangle or the Boy's egg with its big bow
  tie, both bodies shorter (0.36 to 0.88 m) and round at the base with no sharp rim; the big floating
  oval hands with finger lines, the tube legs set wide apart inside the base and rounded feet, the bow sized to the body, no arm
  meshes, no hair or mouth; the vivid mushroom colors and materials, the other colors unchanged).

Test it with `./test.sh Player` from the repository root.

## Contract

Meters, seconds, Y up, System.Numerics. Yaw angles use Godot's convention: 0 faces -Z,
positive turns left; the direction of yaw `a` is `(-sin a, 0, -cos a)`.

| Message | Who publishes it | What it is |
| --- | --- | --- |
| `PlayerState` | Player, once every tick | `Position` (feet), `Facing` (yaw), `Velocity`, `Grounded`, `Health`, `MaxHealth`, `Alive`, `FullHealth` (`Health` at `MaxHealth`), `Character` (`CharacterKind.Girl` or `Boy`), `Area` (`Fungtopia.Map.Area` at `Position`; the old constructor without it computes it with `Areas.At`). |
| `TeleportPlayer` | anyone (fast travel, the house door, DayNight at the clinic) | `Position`, `Facing`: the player is put on the ground there at once with zero velocity; after a defeat it also comes back `Alive` with full health. |
| `DamagePlayer` | monsters (anyone) | `Amount` of health to take, `From` where the hit came from. |
| `HealPlayer` | anyone (the Food module when you eat) | `Amount` of health to give back (`HealPlayer.Full`, 999: fully); never above `MaxHealth`, ignored while defeated or when 0 or less. |
| `PlayerDefeated` | Player, once per defeat | `Position` where the player fell, on its next tick after health reached 0. |

## Core

`PlayerModule` reads `Fungtopia.Input.PlayerInput`, `Fungtopia.Dialogue.DialogueState`,
`Fungtopia.Bow.BowState`, `Fungtopia.StartScreen.GameStart` and `StartScreenState`,
`Fungtopia.Wardrobe.AppearanceState`, `WardrobeState` and `PreviewYaw` (and the Wardrobe's `Catalog` and `Looks`),
`TeleportPlayer`, `DamagePlayer` and `HealPlayer`, the Save Contract's `CollectSave` and `RestorePart`, and the
world from the Map Contract (`Ground`, `Areas`, `Places`). The Player Contract references the Map Contract (for `Area`); the Core also references the Save
Contract. The game starts at
`Places.HomeDoor`, facing out.

- **Start screen.** The player is hidden (`Visible` false: the view hides the model) only before
  the start screen opens. While `StartScreenState.Open` is true it is shown standing idle at
  `Places.HomeDoor`, facing out (`Places.HomeDoor.Yaw`): the Camera's front view is built from
  that facing (yaw `Facing` + π), so the character faces the camera on character creation.
  Meanwhile `PlayerInput` is ignored (no walking, jumping, turning or aim) and `BowState` counts
  as no draw (the bow stays on the back); until the first `GameStart` (`Started`) `DamagePlayer`
  is ignored too. The character shown is the `Character` setting (Girl by default): the
  StartScreen Contract tells the chosen character only at `GameStart`; it is dressed live from
  the Wardrobe's `AppearanceState` (the look step's `SetLook` / `Equip`). `GameStart` sets
  `Character` from its `Character` (`GameStart.Girl` 0 or `Boy` 1, replacing the setting) and
  puts the player at `Places.HomeDoor` in play; later `GameStart`s are ignored. It still
  publishes `PlayerState` every tick. Without the StartScreen module the player never appears.
- **Wardrobe and shop.** While `WardrobeState.Open` (the wardrobe or Abby's shop), as on the
  start screen, `Move`, `Jump`, `Fire`, `Look` and the aim are ignored completely (from the tick
  the screen opens, whichever message comes first) and `BowState` counts as no draw: the player
  stands where it is, on the ground, with zero velocity and the idle pose (`Archery` relaxed, also
  when it opened mid-draw or mid-jump). `Facing` does not change, so the Camera's front view (yaw
  `Facing` + π) keeps facing the character.
- **Preview turn.** While the start screen or a wardrobe screen is open (`OnScreen`),
  `PreviewTurn` eases (`PreviewTurnSharpness`, 12 per second, the short way round) toward the
  latest `PreviewYaw.Yaw`: 0 faces the camera, positive turns the front to the screen's right
  (yaw left). `ShownFacing` = `Facing` + `PreviewTurn` is what the view turns the model to. Once
  closed the turn eases back to 0 (the facing as before opening), the latest `PreviewYaw` is
  forgotten (the next screen starts facing the camera) and control comes back as before.

Each tick:

- **Camera yaw.** When the input has an aim (`HasAim`), the camera's yaw is the aim direction's
  horizontal part; otherwise `Look.X` (yaw right) is added up (on the tick, only in play).
- **Walking.** `Move` (x right, y forward, clamped to length 1) is turned by the camera yaw:
  `WalkSpeed`, 5 m/s, at full deflection. While `DialogueState.Open` is true, `Move` and `Jump` are ignored.
- **Running.** While `PlayerInput.Run` is held (the left trigger or Shift) and the player moves,
  the speed is `RunSpeed` = 1.25 × `WalkSpeed` (6.25 m/s at full deflection, scaled by the stick
  the same way) instead; `Running` is true on such ticks. Turning, jumping, the ground, collisions
  and what stops walking (dialogue, screens, defeat) are the same as walking.
- **Facing.** Turns at up to 10 rad/s the short way round: toward the aim while `Fire` is held,
  else toward the move direction; it keeps its facing when standing still.
- **Jump.** On `Jump` while grounded: 1.2 m high under 20 m/s² gravity (about 0.7 s in the air).
- **Ground.** While grounded the feet stay on `Ground.HeightAt` (up and down hills).
- **Collisions.** A 0.4 m circle. When `Ground.Blocks` says it overlaps an obstacle it is pushed
  out of each overlapping `Ground.Obstacles` circle along its normal (so it slides around trunks
  and buildings); when `Areas.Walkable(x, z, 0.4)` is false it is pushed back along the gradient
  of `Areas.Depth` (slides along the edges of roads, towns and the forest). If that does not
  settle (a tight corner) it stays where it was. `CanStand(x, z)` tells whether a spot is free.
- **Health.** 10 of 10. `DamagePlayer` with `Amount > 0` lowers it (never below 0), then 0.7 s of
  invulnerability ignores more hits. `HealPlayer` with `Amount > 0` raises it up to `MaxHealth`
  (999 or more: fully), not while defeated. At 0: `Alive` false, one `PlayerDefeated`, no moving, for
  as long as it takes: the next `TeleportPlayer` brings it back with full health (DayNight sends
  it the next morning at Dr. Jeremy's clinic).

- **Saved games.** On `Fungtopia.Save.CollectSave` it publishes at once one `SavePart` (same
  `RequestId`, Module `"Player"`, `SaveJson()`): compact camelCase JSON of a small record,
  `{"version":1,"x":..,"y":..,"z":..,"facing":..,"area":"Forest","character":"Boy","health":7}`
  (feet position, `Areas.At` there, yaw, `CharacterKind`, health; `SaveVersion` 1). On
  `RestorePart` for `"Player"` it puts that back: `Character`, `Health`, the player in play
  (`Started`, so a later `GameStart` is ignored), standing on the ground at the saved spot (`PlaceAt`,
  zero velocity, idle) with the saved facing. When that spot is not standable or not in the saved
  area any more it uses the area's entry instead (`Places.HomeInterior` for Home, the area's
  `Signpost`, `Places.HomeDoor` for the road), so a load into the house stays on its floor. A save
  made while defeated (health 0) comes back alive with full health, as after the clinic. Restoring
  publishes nothing (no `PlayerDefeated`, no damage flash: invulnerability and defeat are reset).
  Unreadable Json (not JSON, another version, a missing or out-of-range field, an unknown area or
  character) keeps the current state and is logged once (`Log`, Console by default); other
  modules' parts are ignored.

- **Archery.** `Archery` (an `ArcheryState`) is advanced every tick by `ArcheryPose.Advance`
  from the latest `BowState.Draw` (see the View for what it means).

- **Look.** The latest `AppearanceState` is kept as `Appearance` (`HasAppearance` once one came;
  `AppearanceVersion` counts its changes, not its arrivals). `Look` is the `CharacterLook` of it on
  `Character` (`CharacterLook.From`), or `CharacterLook.Default` before any.

`CharacterLook` (plain values, no allocations) says how to draw the character:

- `SkinColor` tints the skin parts (material `Skin`), `EyeColor` the eyes (`Eye`), `HairColor` the
  hair style's `Hair` material (all from `Looks`); `HairModel` is the hair style's `ModelPath`, put
  on the bald head; empty for "No hair" (`Looks.NoHair`, a new character's default), an
  out-of-range style or no look yet: the models have no hair of their own.
- `Top`, `Bottom`, `Shoes`, `Head`, `Face`, `Back` (`Model(slot)`): the `Catalog` `ModelPath` of the
  item worn there. An item counts only in its own slot and when it fits the character; unknown
  ids are nothing. A catalog dress replaces the bottom.
- `Builtin` (`Shows(piece)`): the old fixed outfits are the defaults, one `BuiltinPieces` per slot,
  each shown while nothing is worn there: `Top` (the Girl's pink spotted dress; the Boy's blue
  shirt, big bow tie and buttons), `Bottom` (the Boy's trousers; hidden under a dress), `Shoes`
  (the rounded feet) and `Head` (the Girl's ribbon bow). There is no built-in hair piece. The Girl's
  built-in dress does not hide a worn bottom (it shows under it).
- Out-of-range look indices keep the built-in color (or no hair). `CharacterLook.Default(kind)`
  (`Custom` false) is the model as characters.py drew it.

`Rig`: the attach points (`Head`, `Body`, `LegL`, `LegR`; `IsAttachPoint`; a worn model's
`BodyGirl` / `BodyBoy` objects go on the Body of that character only, `Attach.PartFor`), the node name
prefixes of the built-in pieces (`NodePrefix`), the pieces each character has (`Pieces`), and
the built-in colors.

Settings and helpers: `Character` (the character setting, Girl by default, until `GameStart`
picks it), `PlaceAt(feet,
facing)` puts the player on the ground somewhere. The view also reads `CameraYaw`,
`Visible`, `ShownFacing`, `Running`, `InvulnerableSeconds`, `DefeatedSeconds`, `Ticks`, `Archery`, `BowTarget`, `AimDirection` and `HasAim` (the
latest input's aim). No allocations per tick.

## View

`PlayerView` sets the Core's `Character` from its exported `Character` property (`"Girl"` in
`Main.tscn`), which the user argument `--character=boy` overrides (for example
`godot --path . -- --character=boy`), until the start screen's choice replaces it. The model is
hidden while `Visible` is false (before the start screen opens); on the start screen it stands
idle (no motion, no aim, no bow drawn), so the look step shows it. The frame `AppearanceVersion` changes
(or the character does) it redresses the model with the look (`CharacterModel.Apply`), so the
wardrobe screen and character creation show each pick at once. It places the model between the last two ticks
(`Engine.GetPhysicsInterpolationFraction`), turned to `ShownFacing` (`Facing`, turned by the
wardrobe's preview while a screen is open), so the wardrobe screen shows it idle and turning; a teleport jumps instead of
gliding. The view has no camera: the camera module follows `PlayerState`.

`CharacterModel` loads `girl.glb` or `boy.glb` at run time with `GltfDocument` from the file's
bytes (so a fresh checkout runs without an editor import; an export must include `*.glb` as
non-resource files), replaces each material with the toon shader in the glTF material's color
plus an ink outline (mushroom caps, `Mushrooms.IsCap`, also get the shader's soft white specular
band, `Mushrooms.Gloss`, and a stronger rim light, `Mushrooms.RimLight`; the poison cap's glow
spots glow at `Mushrooms.GlowLight`, 1.2, twice the eye sparkles' 0.6), and animates the parts. `Apply(CharacterLook)` dresses it: it tints the
character's own `Skin`, `Eye` and `Hair` materials, shows or hides the built-in pieces, and loads
the hair style's and each worn item's .glb (cached bytes) the same way, parenting each of its
objects to the rig part it goes on with no offset (`Fungtopia.Wardrobe.Attach.PartFor`: the part
of the same name; `BodyGirl` / `BodyBoy` only on that character's body), so worn
models move with every animation and the archery pose; while a `Back` item is worn the slung bow
sits 0.13 m further out behind it. The model has no arms (their parts are invisible pivots): the
animation swings the floating hands (at the invisible arms' ends, turned a little out,
`HandsOut`) and the tube legs at the hips (`LegSwing`, 0.5 rad) back and forth with the body
sway, the whole character dipping by `LegReach` × (1 − cos swing) so both rounded feet stay on
the ground; in the air the hands go up and the legs tuck; the body (squashed and stretched about
its bottom, which stays put on the legs) stretches rising and squashes on landing, the floating
head riding 4 cm above it; a blinking white flash on every part while invulnerable, and tipping
over on its back when defeated. While `Running` (passed to `Animate`, blended in and out over
about 0.25 s) the stride is quicker (`RunCadence`, 20% more steps per meter) and wider
(`RunSwing`, 30% more leg and hand swing), the whole character bounces up to `RunBounce` (5 cm) off
each step and leans forward about its feet by `RunLean` (0.12 rad).

**Archery** (the Bow module draws only the arrows in flight; the character holds and draws the
bow). `ArcheryPose` (Core, pure functions, positions in the model frame: X right, Y up, -Z the
facing) says what `CharacterModel` draws; `ArcheryPose.Solve` gives the whole pose (`Frame`):

- Idle and walking (`Weight` 0): the bow stays slung on the back (`BackBow`).
- As soon as `Draw` > 0 the pose blends in over 0.15 s (`Weight`, eased): `BackBow` is hidden and
  `HandBow` (the same bow, 1.3 times bigger; it grows as it comes) swings from the back over the
  right shoulder into the left hand, its grip and the left hand at `BowGeometry`'s hold point
  (`HoldLocal`, on the player's right, where the Bow module's arrows start).
- The arrow points from the hold point at `BowState.Target` (`ArrowAim`; along the input's aim
  when there is no target or it is within a meter), yaw within ±0.7 rad of the facing, pitch
  within -0.2..0.7 rad (steeper down, the nock would come up past the right cheek). The bow
  stands upright along it (`BowTurn`: limbs up, the string toward the character), its top tilted
  `BowCant` (0.15 rad) away from the head (to the right). The right hand pinches the string at the
  nock with the arrow (`NockArrow`, a poison mushroom tip), pulled back along the arrow `Draw` ×
  `BowGeometry.FullDrawPull` behind the string's rest (`StringRest`), to the right of the head at
  chest height below the chin (the short invisible arms reach no higher); the string bends into a V and each limb (`HandBowLimbTop` / `Bottom`) bends back
  about the grip by up to `MaxLimbFlex` (0.1 rad) with the visible pull (`Frame.Pull`).
- The arms are invisible limits now (no arm meshes; only the floating hands at their ends show),
  but the pose keeps them: they are never scaled or stretched: they keep `ArmLength` (0.46 m, from
  shoulders at `ShoulderLRest` (∓0.06, 0.89 m), just under the floating head) and only turn at the
  shoulders (`ReachArm`), the hands at their ends. The left arm reaches forward and across, in
  front of the chest, to the grip on the right. For that the body turns to the right of the aim
  (near `SideOn`, less at the start of a draw, never more than `MaxTurn`, 1.5 rad: the chest
  always faces the target's side), leans a little about the hips (`BodyTurn`; the body leans
  reluctantly, sideways slides cost less than forward ones) and slides its hips up to
  `MaxHipShift` (0.2 m; `Frame.Hips`) just enough that the shoulders put both hands where they
  belong: a small damped least-squares solve (bow hand first; within 2 cm of the grip, 3 cm of the
  nock), started from a half draw's pose so the pose moves smoothly. Where the right arm still
  cannot reach the full pull, the visible pull (and the nocked arrow with it) is shortened instead
  (`Frame.Pull` ≤ `StringPull`; about 80% at a level full draw); an arm is never wrapped round.
  Both hands and both elbows (the middle of the arm: it never bends) stay in front of the torso's
  back plane (`InFrontOfBack`, `TorsoBack` 0.17 m behind its middle) and never far behind the
  neck or head. The feet keep walking and keep the facing logic. The head rides on the neck,
  looks along the arrow, and tilts on it to stay about over the feet, a little away from the bow
  (`HeadMiddle`), clear of the bow and the string hand. A slight squash pulses as the draw reaches
  full (body and head only; squash and stretch never scale the hands or feet).
- On release (`Draw` back to 0): the arrow is gone, the string snaps forward with a little
  overshoot and the limbs straighten, the body holds the drawn pose and the right hand swings back
  off the string, out to the right and up (`StringHandTarget`); after 0.6 s without drawing the bow swings back onto the
  back (0.15 s). Drawing again meanwhile keeps the bow in hand.
- `PlayerView` blends `Weight` between ticks, so the pose glides at any frame rate, and reads
  `BowTarget` (the latest `BowState.Target`) from the Core.

## Models

`blender -b --factory-startup --python modules/Player/Models/characters.py` (from the
repository root) writes `girl.glb` and `boy.glb`; commit both with the script. Simple clean
shapes, following Saria's drawing "you choose between these" (Tony, 2026-10-26: closer to the
drawing, no mouth, no hair; then, the same day, from a screenshot of the player: "too elongated,
the torso should be somewhat shorter and more rounded at the base, and the legs should be further
apart but still within the base of the torso"): about 1.30 m tall, the body 25% shorter than
before and everything above it lowered with it.

- **Head**: a big plain round ball (0.38 m across, `HeadRadius` 0.19; its center at
  `BowGeometry.HeadUp`, 1.11 m, its top at 1.30 m; the `Head` pivot, the neck, at 0.90 m)
  floating 4 cm above the body (no neck), with only two big dark oval eyes (`Eye`, each with a
  white sparkle, `EyeShine`), set low-middle on the face and wide apart, and small soft rosy
  cheeks (`Blush`). No mouth, no nose, no hair.
- **Body** (the `Body` pivot at the hips, 0.42 m): a smooth solid of revolution from 0.36 to
  0.88 m, its outline (`BODIES`) smoothed through a few points, round at the base. The Girl's is a
  triangle from the front (0.42 m wide low down, narrow and round at the top) with a fully
  rounded, softly curved bottom (no sharp rim), colored by her pink dress with white spots
  (`DefaultTop`) and her ribbon bow on the round head (`DefaultHead`). The Boy's is a soft egg
  (about 0.28 m wide near the top, widest, 0.38 m, low down, curving round underneath), colored by
  his blue shirt with a big red bow tie at its top (about as wide as the body there) and two
  buttons (`DefaultTop`), and trousers as a band round and under its base (`DefaultBottom`).
- **Hands** (`HandL` / `HandR`): big ovals (0.14 m wide, 0.18 m tall) with three short dark
  finger lines (`Finger`) down their fronts, floating well out beside the body at about its
  middle height (0.58 m), at the ends of the invisible arms (`ArmL` / `ArmR`, empty pivots at the
  shoulders). No arms.
- **Legs** (`LegL` / `LegR`, pivots at the hips, x ∓0.11 m: wide apart, the legs and feet still
  inside the base's outline seen from the front): two plain straight skin tubes (0.06 m radius)
  from inside the body down to rounded feet a little wider than them, the
  built-in shoes (`DefaultShoesLegL` / `R`, children of the legs), so they swing with the legs.

Both are the same modular rig: skin parts (material `Skin`: body, head, hands, legs) with dark
eyes (`Eye`), and the outfit as separate child objects of their part, at its pivot, for the game
to hide. The Wardrobe's clothes and hair styles are fitted to both bodies (its `kit.py`). A wooden
bow (sized to the shorter body: an arc of radius 0.34 m, 0.6 m tip to tip) slung on the back at
0.65 m (its strap a band across the body) with mushroom-red tips (material
`MushroomRed`, its own, not the ribbon's), and for shooting the same bow 1.3 times bigger, without
its string (the game draws the string), its two limbs separate children for bending, and a nocked
poison mushroom-tipped arrow (0.78 m). The mushrooms are "vivid" (Tony, 2026-10-25, the rule for
every mushroom in the game; `Core/Mushrooms.cs` keeps the same colors): the tips a pure tomato
red (0.96, 0.1, 0.06), the arrow's `PoisonCap` a rich bright purple (0.62, 0.12, 0.95) with bright
green `PoisonSpots` (0.5, 1.0, 0.25) on a clean light cream `Stem` (1.0, 0.96, 0.86). Separate
objects with pivots where they move: `Body`, `Head` (neck), `LegL`/`LegR` (hip), `ArmL`/`ArmR`
(shoulder), `HandL`/`HandR` (hand center; the game keeps them at the invisible arms' ends),
`BackBow`, `HandBow` (grip; it rests exactly where `BackBow` is, its own frame being the held
bow's: tips up and down, arc bulging forward; children `HandBowLimbTop` / `HandBowLimbBottom` at
the same pivot) and `NockArrow` (nock; at rest it stands upright in the body, hidden by the game;
its own frame points forward). Feet at the origin, facing -Z in Godot.

## Screenshots

`./screenshot.sh modules/Player/Godot/Preview.tscn` shows both characters; the environment
variable `PLAYER_PREVIEW` picks the pose: `front` (default), `back`, `walk`, `run`, `jump`, `hit`,
`draw` (full draw), `drawback` (full draw from behind, like the game camera), `release` (just
after a shot), `swing` (the bow on its way back), or `aim`, `side` and `facing`: a held draw
(`PLAYER_PREVIEW_DRAW`, 0..1, default 1) at a level aim from the Camera module's aim camera (0.9 m
right of and 0.25 m above the head, 2.75 m back, 62 degrees), from the right side (the bow's) and
from the front
(`PLAYER_PREVIEW_WALK=1` walks meanwhile); `PLAYER_PREVIEW_CHARACTER=girl` or `boy`
shows only that character, larger; `PLAYER_PREVIEW_LOOKS=1` shows four characters dressed from
different Wardrobe looks (skin, hair style and color, eyes, clothes) in the chosen pose.
