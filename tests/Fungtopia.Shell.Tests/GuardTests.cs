using System;
using System.Collections.Generic;
using Xunit;

namespace Fungtopia.Shell.Tests;

public class GuardTests
{
    [Fact]
    public void AModuleThatThrowsInTickIsDisabledAndLoggedWhileTheOthersGoOn()
    {
        var log = new List<string>();
        var disabled = new List<string>();
        var host = new ModuleHost(1.0 / 60, log.Add);
        host.ModuleDisabled += slot => disabled.Add(slot.Name);
        var broken = new FakeModule("Broken") { ThrowOnTick = 1 };
        var healthy = new FakeModule("Healthy");
        var brokenSlot = host.Add("Broken", broken);
        var healthySlot = host.Add("Healthy", healthy);

        for (int i = 0; i < 5; i++)
            host.Tick();

        Assert.Equal(2, broken.Ticks.Count);
        Assert.Equal(5, healthy.Ticks.Count);
        Assert.False(brokenSlot.Enabled);
        Assert.True(healthySlot.Enabled);
        Assert.IsType<InvalidOperationException>(brokenSlot.Error);
        Assert.Equal(new[] { "Broken" }, disabled);
        Assert.Single(log, l => l.Contains("module 'Broken' disabled (Tick)") && l.Contains("tick boom"));
    }

    [Fact]
    public void AModuleThatThrowsInStartNeverTicks()
    {
        var log = new List<string>();
        var host = new ModuleHost(1.0 / 60, log.Add);
        var broken = new FakeModule("Broken") { ThrowOnStart = true };
        var healthy = new FakeModule("Healthy");
        host.Add("Broken", broken);
        host.Add("Healthy", healthy);

        host.Start();
        host.Tick();

        Assert.Empty(broken.Ticks);
        Assert.Single(healthy.Ticks);
        Assert.False(host.Find("Broken")!.Enabled);
        Assert.Contains(log, l => l.Contains("module 'Broken' disabled (Start)"));
        Assert.Contains(log, l => l.Contains("module 'Healthy' started"));
        Assert.DoesNotContain(log, l => l.Contains("module 'Broken' started"));
    }

    [Fact]
    public void AThrowingHandlerDisablesItsOwnModuleNotThePublisher()
    {
        var log = new List<string>();
        var host = new ModuleHost(1.0 / 60, log.Add);
        var counter = new CountingHandler();
        var publisher = host.Add("Ping", new PingModule());
        var brokenListener = host.Add("BrokenListener", new SubscriberModule(new ThrowingHandler()));
        var listener = host.Add("Listener", new SubscriberModule(counter));

        for (int i = 0; i < 3; i++)
            host.Tick();

        Assert.True(publisher.Enabled);
        Assert.False(brokenListener.Enabled);
        Assert.True(listener.Enabled);
        Assert.Equal(3, counter.Count);
        Assert.Single(log, l => l.Contains("module 'BrokenListener' disabled (handling Ping)"));
    }

    [Fact]
    public void ADisabledModulesHandlersStopReceiving()
    {
        var host = new ModuleHost(1.0 / 60);
        var counter = new CountingHandler();
        host.Add("Ping", new PingModule());
        var listener = host.Add("Listener", new SubscriberModule(counter));

        host.Tick();
        host.Disable(listener, "view", new Exception("view boom"));
        host.Tick();

        Assert.Equal(1, counter.Count);
        Assert.False(listener.Enabled);
    }

    [Fact]
    public void AModuleIsDisabledAndReportedOnlyOnce()
    {
        var disabled = 0;
        var host = new ModuleHost(1.0 / 60);
        host.ModuleDisabled += _ => disabled++;
        var slot = host.Add("A", new FakeModule("A") { ThrowOnTick = 0 });

        host.Tick();
        host.Disable(slot, "view", new Exception("again"));

        Assert.Equal(1, disabled);
        Assert.Equal("A tick boom", slot.Error!.Message);
    }
}
