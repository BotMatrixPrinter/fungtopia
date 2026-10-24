# Clock module

Counts fixed simulation steps and publishes a `ClockTicked` message (steps so far, simulated
seconds so far) on every step. It is the example module: copy its layout for a new one.

- `Contract/` — `Fungtopia.Clock.Contract`: the `ClockTicked` message.
- `Core/` — `Fungtopia.Clock.Core`: `ClockModule`, the module's one `IModule`.
- `Godot/` — `Main.tscn` with `ClockView`, a label showing the simulated seconds.
- `Tests/` — xUnit tests of `ClockModule` with a fake `IMessageBus`.

Test it with `./test.sh Clock` from the repository root.
