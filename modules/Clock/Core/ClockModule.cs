using System;
using Fungtopia.Shell;

namespace Fungtopia.Clock;

/// <summary>Counts fixed steps and publishes <see cref="ClockTicked"/> on each one.</summary>
public sealed class ClockModule : IModule
{
    private IMessageBus? _bus;

    public long Steps { get; private set; }

    public double Seconds { get; private set; }

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
    }

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("ClockModule.Tick before Start.");
        Steps++;
        Seconds += context.StepSeconds;
        _bus.Publish(new ClockTicked(Steps, Seconds));
    }
}
