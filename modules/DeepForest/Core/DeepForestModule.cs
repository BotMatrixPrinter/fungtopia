using System;
using Fungtopia.Shell;

namespace Fungtopia.DeepForest;

/// <summary>
/// The Deep Forest's look. Everything is fixed data: <see cref="Scenery"/> places the giant
/// mushrooms on the Map's Deep Forest obstacles and the props, fog and lights around them, once,
/// at start. Nothing happens per tick.
/// </summary>
public sealed class DeepForestModule : IModule
{
    private IMessageBus? _bus;

    public bool Started => _bus != null;

    /// <summary>What the view draws (built in <see cref="Start"/>).</summary>
    public DeepForestScenery Scenery { get; private set; } = null!;

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        Scenery = DeepForestScenery.Create();
    }

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("DeepForestModule.Tick before Start.");
    }
}
