using System;
using Fungtopia.Shell;

namespace Fungtopia.Forest;

/// <summary>
/// The Mushroom Forest's simulation. The forest itself is fixed data in the Contract
/// (<see cref="Terrain"/>, <see cref="ForestBounds"/>, <see cref="Obstacles"/>), so there is
/// nothing to do per tick for now.
/// </summary>
public sealed class ForestModule : IModule
{
    private IMessageBus? _bus;

    public bool Started => _bus != null;

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
    }

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("ForestModule.Tick before Start.");
    }
}
