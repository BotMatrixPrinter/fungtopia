using System;
using System.Collections.Generic;
using Fungtopia.Shell;
using Xunit;

namespace Fungtopia.Clock.Tests;

public class ClockModuleTests
{
    /// <summary>Fake of the shell's bus: records every ClockTicked published.</summary>
    private sealed class FakeBus : IMessageBus
    {
        public readonly List<ClockTicked> Published = new();
        public int OtherMessages;

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct { }

        public void Publish<T>(in T message) where T : struct
        {
            if (message is ClockTicked ticked)
                Published.Add(ticked);
            else
                OtherMessages++;
        }
    }

    private static ClockModule Started(FakeBus bus)
    {
        var clock = new ClockModule();
        clock.Start(bus);
        return clock;
    }

    [Fact]
    public void CountsEveryFixedStep()
    {
        var clock = Started(new FakeBus());
        for (int i = 0; i < 5; i++)
            clock.Tick(new TickContext(i, 0.5));
        Assert.Equal(5, clock.Steps);
        Assert.Equal(2.5, clock.Seconds, 9);
    }

    [Fact]
    public void PublishesOneClockTickedPerStep()
    {
        var bus = new FakeBus();
        var clock = Started(bus);
        for (int i = 0; i < 3; i++)
            clock.Tick(new TickContext(i, 1.0 / 60));
        Assert.Equal(3, bus.Published.Count);
        Assert.Equal(0, bus.OtherMessages);
        Assert.Equal(new long[] { 1, 2, 3 }, bus.Published.ConvertAll(m => m.Steps));
        Assert.Equal(3.0 / 60, bus.Published[2].Seconds, 9);
    }

    [Fact]
    public void PublishesNothingBeforeItTicks()
    {
        var bus = new FakeBus();
        var clock = Started(bus);
        Assert.Empty(bus.Published);
        Assert.Equal(0, clock.Steps);
    }

    [Fact]
    public void TickBeforeStartThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new ClockModule().Tick(new TickContext(0, 1.0 / 60)));
    }
}
