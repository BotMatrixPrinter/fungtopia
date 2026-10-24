# Forest module

The Mushroom Forest, the first place: a 200 m x 200 m square of gentle rolling ground with
winding dirt paths and clearings, giant mushrooms as trees, and a wall of giant mushrooms around
it. The forest is fixed data in the Contract, so walkers (player, pets, monsters) in other
modules use it directly; the Core does nothing per tick for now.

- `Contract/` — `Fungtopia.Forest.Contract` (namespace `Fungtopia.Forest`), see below.
- `Core/` — `Fungtopia.Forest.Core`: `ForestModule`, the module's one `IModule` (no work per tick).
- `Godot/` — `Main.tscn` with `ForestView`, which draws the forest from the Contract.
- `Tests/` — `ForestTests.cs`: xUnit tests of the Contract and of `ForestModule` with a fake `IMessageBus`.

Test it with `./test.sh Forest` from the repository root.

## Contract

Meters, Y up, System.Numerics; north is -Z (Godot's forward), south is +Z. Everything is fixed:
the same on every run, computed from math only (no random seed, no clock).

| Member | What it is |
| --- | --- |
| `Terrain.HeightAt(float x, float z)` | Ground height at (x, z). Pure, deterministic, allocation-free; defined everywhere. Always within ±`Terrain.MaxAbsHeight` (1.9 m), never steeper than about 1 in 6. |
| `ForestBounds.HalfSize` | `100f`: the walkable area is the square \|x\| ≤ 100, \|z\| ≤ 100. Walkers stay inside it (the mushroom wall beyond it is scenery, not obstacles). |
| `ForestBounds.PlayerStart` | `Vector3` on the ground (`Y == Terrain.HeightAt(X, Z)`) in the start clearing near the south edge, (0, h, 84). At least 8 m from any trunk. |
| `ForestBounds.Contains(x, z, margin = 0)` | Whether (x, z) is inside the square, `margin` meters from its edge. |
| `Obstacles.All` | `ReadOnlySpan<Obstacle>`: the giant mushroom trunks, a fixed array of circles (`X`, `Z`, `Radius`, `Center`) that walkers must not pass through. All inside the bounds, off paths and clearings, more than 1 m apart. |
| `Obstacles.Count` | Number of trunks (235). |
| `Obstacles.Blocks(x, z, radius = 0)` | Whether a walker of `radius` at (x, z) overlaps a trunk. Allocation-free. |
| `ForestLayout.Clearings` | `ReadOnlySpan<Clearing>` (`X`, `Z`, `Radius`); the first holds the start. |
| `ForestLayout.PathPoints`, `PathHalfWidth`, `PathDistance(x, z)`, `OnPath(x, z)` | The dirt paths: polylines of (x, z) points (NaN separates paths), 1.8 m half-width. |
| `ForestLayout.ClearingDepth(x, z)` | Meters inside the nearest clearing (negative outside all). |

## View

`ForestView` builds everything in code at startup (no imported assets, so a fresh checkout loads
headless): the ground mesh from `Terrain.HeightAt` (1 m grid, vertex-colored grass, moss, meadow
clearings and dirt paths), a giant mushroom on every `Obstacles` circle (stem radius = the
circle's radius; fat stems, big spotted caps in reds, browns and purples, about one in six
glowing softly, many with big round eyes looking at the nearest path), the dense mushroom wall
outside the square, small mushroom clusters, ferns, grass tufts, mossy logs and fireflies. Small
props and all mushrooms are MultiMesh instances (one draw per kind). Shaders in
`Godot/Shaders/`: soft two-band toon light with ink outlines; a low warm late-afternoon sun whose
cap shadows dapple the ground, light warm fog, glow.

Props (clusters, ferns, logs, grass) are not obstacles: they never stand on trunks or paths and
walkers may cross them.

A `PreviewCamera` stands at `PlayerStart` at eye height (1.6 m) looking north up the path; it is
current only until another module makes its own camera current.
