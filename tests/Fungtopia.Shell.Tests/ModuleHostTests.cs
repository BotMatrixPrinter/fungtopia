using System;
using System.Collections.Generic;
using Xunit;

namespace Fungtopia.Shell.Tests;

public class ModuleHostTests
{
    [Fact]
    public void StartsEveryModuleOnceThenTicksThemInOrder()
    {
        var journal = new List<string>();
        var host = new ModuleHost(0.25);
        host.Add("A", new FakeModule("A", journal));
        host.Add("B", new FakeModule("B", journal));

        host.Start();
        host.Start();
        host.Tick();
        host.Tick();

        Assert.Equal(new[] { "A.Start", "B.Start", "A.Tick0", "B.Tick0", "A.Tick1", "B.Tick1" }, journal);
        Assert.Equal(2, host.TickCount);
    }

    [Fact]
    public void TickStartsTheModulesWhenStartWasNotCalled()
    {
        var module = new FakeModule("A");
        var host = new ModuleHost(0.25);
        host.Add("A", module);

        host.Tick();

        Assert.NotNull(module.Bus);
        Assert.Single(module.Ticks);
    }

    [Fact]
    public void TickContextCarriesTickNumberAndFixedStep()
    {
        var module = new FakeModule("A");
        var host = new ModuleHost(1.0 / 60);
        host.Add("A", module);

        for (int i = 0; i < 3; i++)
            host.Tick();

        Assert.Equal(new long[] { 0, 1, 2 }, module.Ticks.ConvertAll(t => t.Tick));
        Assert.All(module.Ticks, t => Assert.Equal(1.0 / 60, t.StepSeconds));
    }

    [Fact]
    public void ModulesTalkThroughTheBus()
    {
        var handler = new CountingHandler();
        var host = new ModuleHost(1.0 / 60);
        host.Add("Ping", new PingModule());
        host.Add("Listener", new SubscriberModule(handler));

        for (int i = 0; i < 4; i++)
            host.Tick();

        Assert.Equal(4, handler.Count);
        Assert.Equal(0 + 1 + 2 + 3, handler.Sum);
    }

    [Fact]
    public void TickDoesNotAllocate()
    {
        var handler = new CountingHandler();
        var host = new ModuleHost(1.0 / 60);
        host.Add("Ping", new PingModule());
        host.Add("Listener", new SubscriberModule(handler));
        host.Tick(); // warm up

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10_000; i++)
            host.Tick();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.Equal(10_001, handler.Count);
    }

    [Fact]
    public void RejectsDuplicateNamesAndLateAdds()
    {
        var host = new ModuleHost(1.0 / 60);
        host.Add("A", new FakeModule("A"));
        Assert.Throws<ArgumentException>(() => host.Add("A", new FakeModule("A")));
        host.Start();
        Assert.Throws<InvalidOperationException>(() => host.Add("B", new FakeModule("B")));
    }

    [Fact]
    public void RejectsANonPositiveStep()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModuleHost(0));
    }
}
