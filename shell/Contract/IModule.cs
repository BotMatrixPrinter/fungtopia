namespace Fungtopia.Shell;

/// <summary>
/// A module's simulation, found by the host at startup (exactly one per module Core assembly,
/// with a public parameterless constructor). Runs on the fixed timestep, never touches Godot.
/// </summary>
public interface IModule
{
    /// <summary>Called once before the first tick. Subscribe to messages here.</summary>
    void Start(IMessageBus bus);

    /// <summary>One fixed simulation step. Must not allocate.</summary>
    void Tick(in TickContext context);
}
