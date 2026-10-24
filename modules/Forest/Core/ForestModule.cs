using System;
using Fungtopia.DayNight;
using Fungtopia.Shell;

namespace Fungtopia.Forest;

/// <summary>
/// The Mushroom Forest's simulation. The forest itself is fixed data in the Contract
/// (<see cref="Terrain"/>, <see cref="ForestBounds"/>, <see cref="Obstacles"/>, forwarding to the
/// Map module). The Core only remembers whether it is night (from DayNight's
/// <see cref="TimeOfDay"/>), so the view can make the glowing mushrooms brighter.
/// </summary>
public sealed class ForestModule : IModule, IMessageHandler<TimeOfDay>
{
    private IMessageBus? _bus;

    public bool Started => _bus != null;

    /// <summary>Whether the latest <see cref="TimeOfDay"/> said it is night (false before any).</summary>
    public bool IsNight { get; private set; }

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        bus.Subscribe<TimeOfDay>(this);
    }

    public void Handle(in TimeOfDay message) => IsNight = message.IsNight;

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("ForestModule.Tick before Start.");
    }
}
