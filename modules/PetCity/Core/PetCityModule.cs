using System;
using Fungtopia.DayNight;
using Fungtopia.Shell;

namespace Fungtopia.PetCity;

/// <summary>
/// Pet City's simulation. Where everything stands is fixed data (<see cref="PetCityLayout"/>, from
/// the Map's buildings), so there is nothing to do per tick; it only remembers from
/// <see cref="TimeOfDay"/> whether it is night, when the city's windows are lit.
/// </summary>
public sealed class PetCityModule : IModule, IMessageHandler<TimeOfDay>
{
    private IMessageBus? _bus;

    public bool Started => _bus != null;

    /// <summary>Whether it is night (the last <see cref="TimeOfDay"/>; day until one arrives).</summary>
    public bool IsNight { get; private set; }

    /// <summary>Whether the windows, the shop sign and the lamps glow.</summary>
    public bool WindowsLit => IsNight;

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
            throw new InvalidOperationException("PetCityModule.Tick before Start.");
    }
}
