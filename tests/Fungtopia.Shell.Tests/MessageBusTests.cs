using System;
using System.Collections.Generic;
using Xunit;

namespace Fungtopia.Shell.Tests;

public class MessageBusTests
{
    [Fact]
    public void DeliversSynchronouslyToEverySubscriberInOrder()
    {
        var bus = new MessageBus();
        var journal = new List<string>();
        bus.Subscribe(new RecordingHandler<Ping>("a", journal, p => p.Value));
        bus.Subscribe(new RecordingHandler<Ping>("b", journal, p => p.Value));

        bus.Publish(new Ping(7));
        journal.Add("after");

        Assert.Equal(new[] { "a:7", "b:7", "after" }, journal);
    }

    [Fact]
    public void KeepsMessageTypesApart()
    {
        var bus = new MessageBus();
        var journal = new List<string>();
        var pings = new RecordingHandler<Ping>("ping", journal, p => p.Value);
        var pongs = new RecordingHandler<Pong>("pong", journal, p => p.Value);
        bus.Subscribe(pings);
        bus.Subscribe(pongs);

        bus.Publish(new Ping(1));
        bus.Publish(new Pong { Value = 2 });
        bus.Publish(new Ping(3));

        Assert.Equal(new[] { "ping:1", "pong:2", "ping:3" }, journal);
    }

    [Fact]
    public void PublishWithoutSubscribersDoesNothing()
    {
        var bus = new MessageBus();
        bus.Publish(new Pong { Value = 1 });
    }

    [Fact]
    public void BusesAreIndependent()
    {
        var first = new MessageBus();
        var second = new MessageBus();
        var handler = new CountingHandler();
        first.Subscribe(handler);

        second.Publish(new Ping(1));

        Assert.Equal(0, handler.Count);
    }

    [Fact]
    public void SubscribingDuringDeliveryTakesEffectFromTheNextMessage()
    {
        var bus = new MessageBus();
        var late = new CountingHandler();
        bus.Subscribe(new ActionHandler(() => bus.Subscribe(late)));

        bus.Publish(new Ping(1));
        Assert.Equal(0, late.Count);

        bus.Publish(new Ping(2));
        Assert.Equal(1, late.Count);
    }

    [Fact]
    public void PublishDoesNotAllocate()
    {
        var bus = new MessageBus();
        var a = new CountingHandler();
        var b = new CountingHandler();
        bus.Subscribe(a);
        bus.Subscribe(b);
        bus.Publish(new Ping(0)); // warm up

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 1; i <= 10_000; i++)
            bus.Publish(new Ping(i));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.Equal(10_001, b.Count);
    }

    [Fact]
    public void AStandaloneHandlerThatThrowsReachesThePublisher()
    {
        var bus = new MessageBus();
        bus.Subscribe(new ThrowingHandler());
        Assert.Throws<InvalidOperationException>(() => bus.Publish(new Ping(1)));
    }

    private sealed class ActionHandler(Action action) : IMessageHandler<Ping>
    {
        public void Handle(in Ping message) => action();
    }
}
