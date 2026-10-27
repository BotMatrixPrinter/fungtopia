using System;
using Fungtopia.DayNight;
using Fungtopia.Shell;

namespace Fungtopia.Town;

/// <summary>
/// Mushroom Town's simulation. The town itself is fixed data (<see cref="TownLayout"/>); the state
/// is whether the street lamps are lit, which follows <see cref="TimeOfDay.IsNight"/>.
/// <see cref="TownLights"/> is published only when the lamps switch; nothing allocates per tick.
/// </summary>
public sealed class TownModule : IModule, IMessageHandler<TimeOfDay>
{
    private IMessageBus? _bus;

    /// <summary>Whether the street lamps are lit (night). Off until the first night.</summary>
    public bool LampsLit { get; private set; }

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        bus.Subscribe<TimeOfDay>(this);
    }

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("TownModule.Tick before Start.");
    }

    public void Handle(in TimeOfDay message)
    {
        if (message.IsNight == LampsLit)
            return;
        LampsLit = message.IsNight;
        _bus?.Publish(new TownLights(LampsLit));
    }
}
