using System;
using Fungtopia.Shell;

namespace Fungtopia.Map;

/// <summary>
/// The world map's simulation. The ground and layout are fixed data in the Contract
/// (<see cref="Ground"/>, <see cref="Areas"/>, <see cref="Places"/>, <see cref="Buildings"/>),
/// so there is nothing to do per tick.
/// </summary>
public sealed class MapModule : IModule
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
            throw new InvalidOperationException("MapModule.Tick before Start.");
    }
}
