# fungtopia

A cute third-person 3D adventure game set in a mushroom world. You pick a girl or boy character
and a starter pet, a Mushroom Cat or a Mushroom Dog, fight cute mushroom monsters in Mushroom
Forest for Mushroom Coins, buy more pets in Pet City, and have a house in Mushroom Town.

It is made with Godot 4.7.2 .NET and C# on .NET 10, and built by many hub jobs at once, one per
module. The game design comes later; this repository holds the structure every job builds on.

## Open it on your computer

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
2. Install [Godot 4.7.2 .NET](https://godotengine.org/download) (the ".NET" build, not the
   standard one).
3. Clone this repository.
4. Start Godot, choose **Import**, and pick `project.godot` in the clone.
5. Press **Play** (F5). Godot builds the C# code (packages come from nuget.org) and runs the
   main scene.

From a terminal: `./test.sh` runs all tests and a headless load of the game;
`./screenshot.sh [scene] [out.png]` saves a 1920x1080 screenshot (Linux, needs `xvfb-run`).

## Layout

```
project.godot          Godot project; the main scene is shell/Godot/Bootstrap.tscn
Fungtopia.csproj       the Godot game assembly (Godot.NET.Sdk 4.7.2, net10.0); compiles
                       shell/Godot and every modules/*/Godot, and references every
                       modules/*/Contract and modules/*/Core project by wildcard
Fungtopia.sln          the solution Godot builds (lists only Fungtopia.csproj)
Directory.Build.props  for test projects (names ending in .Tests) only: no tiered compilation
                       or tiered PGO, so allocation tests measure our code, not the JIT
global.json            .NET SDK 10.0.100 (rolls forward to newer feature bands), Godot.NET.Sdk 4.7.2
nuget.config           nuget.org as the package source
test.sh                ./test.sh, or ./test.sh <Name> for one module (see below)
screenshot.sh          renders a scene to $SCREENSHOT_DIR (or ./screenshots/)
hub.json               the hub's test commands
shell/
  Contract/            IModule, IModuleView, IMessageBus, IMessageHandler<T>, TickContext
  Host/                ModuleHost: discovery, fixed-timestep ticking, the runtime guard; MessageBus
  Godot/               Bootstrap scene: ticks the host every physics frame, loads module scenes
tests/
  Fungtopia.Shell.Tests/   xUnit tests of the bus, host, guard and discovery
modules/
  <Name>/
    Contract/          Fungtopia.<Name>.Contract: the module's public structs and interfaces
    Core/              Fungtopia.<Name>.Core: the simulation, exactly one IModule class
    Godot/             Main.tscn and its scripts: drawing, input, sound
    Tests/             Fungtopia.<Name>.Tests: xUnit tests of Core with fakes
    README.md          what the module does
  Clock/               the example module: counts fixed steps, publishes ClockTicked
```

The `.gdignore` files keep Godot from importing the plain C# folders (Contract, Core, Tests,
shell/Contract, shell/Host, tests); give a new module's Contract, Core and Tests one too.

## Rules every job follows

- **Shell vs modules.** The shell (`shell/`, `tests/`, the root files) is changed only by
  whole-repo jobs. Everything about the game lives in modules.
- **One job changes one module.** A module job changes only files in its `modules/<Name>/`
  folder and is tested with `./test.sh <Name>`.
- **Add a module without touching anything outside it.** Create `modules/<Name>/` with the four
  projects named as above (copy `modules/Clock`). The game project picks up its Contract, Core
  and Godot files by wildcard, the host finds its `IModule` class at startup, `test.sh` finds its
  Tests, and its `Godot/Main.tscn` is loaded when it exists. No list of modules exists anywhere.
- **Modules couple only through Contracts.** A Core references only Contract projects
  (`shell/Contract` and other modules' `Contract`), never another Core and never Godot. Modules
  talk through `IMessageBus` with struct messages defined in Contracts. A module's Godot scripts
  talk only to their own Core: the root node of `Main.tscn` implements `IModuleView`, and
  `Bind(IModule)` hands it that Core.
- **Per-module tests with fakes.** Each module's Tests test its Core alone, with hand-written
  fakes of the Contracts it uses (for example a fake `IMessageBus`). `./test.sh <Name>` runs
  module `<Name>`'s tests plus those of every module whose projects reference `<Name>`'s
  Contract, then builds the game and loads it headless.
- **Runtime guard.** Each module's Start, Tick, message handlers and view binding run inside its
  own guard: a module that throws is disabled and logged (a `WARNING: Fungtopia: module '<Name>'
  disabled ...` line), and the game goes on without it. `test.sh` fails when a module it tests
  is disabled, or when Godot prints any `ERROR`.
- **Performance.** The simulation is in Core and runs on a fixed timestep (60 ticks a second,
  one per physics frame). Use plain structs and arrays; per-tick code (Tick, message handlers)
  does not allocate, use LINQ, or box. Messages are structs passed by `in` reference. Godot only
  renders and reads input.
- **Never commit screenshots.** `screenshot.sh` writes to `$SCREENSHOT_DIR` or `./screenshots/`,
  which is git-ignored along with `.godot/`, `bin/`, `obj/` and other build output.

## Tests

- `./test.sh` runs the shell's tests and every module's tests, builds `Fungtopia.csproj`
  (failing unless it targets net10.0), and runs `godot --headless --path . --fixed-fps 60
  --quit-after 10`, failing on any `ERROR` in its output.
- `./test.sh <Name>` does the same for module `<Name>` and the modules that depend on its Contract.
- Allocation tests (`GC.GetAllocatedBytesForCurrentThread()` around many ticks, expecting no
  change) warm up for a few hundred ticks first; with tiering off (see `Directory.Build.props`)
  every method is fully compiled on its first call, so the runtime does not allocate mid-measure.
