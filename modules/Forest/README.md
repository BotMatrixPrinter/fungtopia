# Forest module

The Mushroom Forest, the first place: a 200 m x 200 m square of gentle rolling ground with
winding dirt paths and clearings, giant mushrooms as trees, and a wall of giant mushrooms around
it. The Map module (`modules/Map`) now owns the world's ground and layout (the forest square,
its roads to Mushroom Town and Pet City, the Deep Forest), and the DayNight module owns the sun,
sky and environment. The Forest Contract forwards to the Map, so walkers in other modules that
use it keep working; new code should use `Fungtopia.Map` directly. The Core only remembers
whether it is night, for the glowing mushrooms.

- `Contract/` — `Fungtopia.Forest.Contract` (namespace `Fungtopia.Forest`), see below.
- `Core/` — `Fungtopia.Forest.Core`: `ForestModule`, the module's one `IModule` (no work per tick;
  reads `Fungtopia.DayNight.TimeOfDay` into `IsNight`), and `ForestRing`, where the view's ring of
  giant mushrooms may stand.
- `Godot/` — `Main.tscn` with `ForestView`, which draws the forest from the Contract.
- `Tests/` — `ForestTests.cs`: xUnit tests of the Contract (its forwarding values equal the Map's),
  of `ForestRing` and of `ForestModule` with a fake `IMessageBus`.

Test it with `./test.sh Forest` from the repository root.

## Contract

Meters, Y up, System.Numerics; north is -Z (Godot's forward), south is +Z. Everything is fixed:
the same on every run, computed from math only (no random seed, no clock). The Contract
references `Fungtopia.Map.Contract`; members marked *forwarding* just pass on the Map's values.

| Member | What it is |
| --- | --- |
| `Terrain.HeightAt(float x, float z)` | *Forwarding* to `Fungtopia.Map.Ground.HeightAt`. Pure, deterministic, allocation-free; defined everywhere. Inside the square within ±`Terrain.MaxAbsHeight` (1.9 m) and never steeper than about 1 in 6; beyond it the Map's roads, towns and cliffs. |
| `ForestBounds.HalfSize` | *Forwarding*: `Map.Areas.ForestHalfSize`, `100f`: the forest is the square \|x\| ≤ 100, \|z\| ≤ 100 (`Map.Areas.Forest`). Beyond it, walkable ground is the Map's (`Map.Areas.Walkable`). |
| `ForestBounds.PlayerStart` | `Vector3` on the ground (`Y == Map.Ground.HeightAt(X, Z)`) in the start clearing near the south edge, (0, h, 84). At least 8 m from any trunk. Unchanged in value. |
| `ForestBounds.Contains(x, z, margin = 0)` | *Forwarding* to `Map.Areas.Forest.Contains`: whether (x, z) is inside the square, `margin` meters from its edge. |
| `Obstacles.All` | *Forwarding*: the Map's forest trunks, the first `Map.Ground.ForestTrunkCount` circles of `Map.Ground.Obstacles` in the same order, as `ReadOnlySpan<Obstacle>` (`X`, `Z`, `Radius`, `Center`). All inside the bounds, off paths and clearings, more than 1 m apart. |
| `Obstacles.Count` | Number of trunks (235, `Map.Ground.ForestTrunkCount`). |
| `Obstacles.Blocks(x, z, radius = 0)` | Whether a walker of `radius` at (x, z) overlaps a forest trunk (`Map.Ground.Blocks` also checks buildings and the Deep Forest). Allocation-free. |
| `ForestLayout.Clearings` | `ReadOnlySpan<Clearing>` (`X`, `Z`, `Radius`); the first holds the start. |
| `ForestLayout.PathPoints`, `PathHalfWidth`, `PathDistance(x, z)`, `OnPath(x, z)` | The dirt paths: polylines of (x, z) points (NaN separates paths), 1.8 m half-width. |
| `ForestLayout.ClearingDepth(x, z)` | Meters inside the nearest clearing (negative outside all). |

## View

`ForestView` builds everything in code at startup (no imported assets, so a fresh checkout loads
headless): the forest square's floor from `Map.Ground.HeightAt` on a 1 m grid from -100 to 100
(the Map draws the ground around it on the same grid, so the seam matches; vertex-colored grass,
moss, meadow clearings and dirt paths), a giant mushroom on every `Obstacles` circle (stem radius
= the circle's radius; fat stems, big spotted caps in reds, browns and purples, about one in six
glowing softly, many with big round eyes looking at the nearest path), the ring of dense giant
mushrooms outside the square (`ForestRing`: open in the Map's road gaps, `Areas.ForestGapSouth`
and `ForestGapNorth`, `ForestGapHalfWidth` 5 m to each side, and in its west gap to the Deep
Forest; off every road and walkable ground; not beside the roads where the Map draws its own cliff
mushrooms), small mushroom clusters, ferns, grass tufts, mossy logs and fireflies. Small props
and all mushrooms are MultiMesh instances (one draw per kind). Shaders in `Godot/Shaders/`: soft
two-band toon light with ink outlines. The glowing caps shine 2.4 times as bright at night
(`ForestModule.IsNight`, fading over about two seconds). The forest has no light, sky or
environment of its own: DayNight's global sun and `WorldEnvironment` light it.

Props (clusters, ferns, logs, grass) are not obstacles: they never stand on trunks or paths and
walkers may cross them.

A `PreviewCamera` stands at `PlayerStart` at eye height (1.6 m) looking north up the path; it is
current only until another module makes its own camera current.
