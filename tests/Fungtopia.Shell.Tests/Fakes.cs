using System;
using System.Collections.Generic;

namespace Fungtopia.Shell.Tests;

public struct Ping
{
    public int Value;
    public Ping(int value) => Value = value;
}

public struct Pong
{
    public int Value;
}

/// <summary>Records what it receives into a shared journal.</summary>
public sealed class RecordingHandler<T>(string name, List<string> journal, Func<T, int> value) : IMessageHandler<T>
    where T : struct
{
    public int Count;
    public int Sum;

    public void Handle(in T message)
    {
        Count++;
        Sum += value(message);
        journal.Add($"{name}:{value(message)}");
    }
}

public sealed class CountingHandler : IMessageHandler<Ping>
{
    public int Count;
    public int Sum;

    public void Handle(in Ping message)
    {
        Count++;
        Sum += message.Value;
    }
}

public sealed class ThrowingHandler : IMessageHandler<Ping>
{
    public void Handle(in Ping message) => throw new InvalidOperationException("handler boom");
}

/// <summary>A module that records its calls and can be told to throw.</summary>
public sealed class FakeModule(string name, List<string>? journal = null) : IModule
{
    public IMessageBus? Bus;
    public readonly List<TickContext> Ticks = new();
    public bool ThrowOnStart;
    public long ThrowOnTick = -1;

    public void Start(IMessageBus bus)
    {
        journal?.Add($"{name}.Start");
        if (ThrowOnStart)
            throw new InvalidOperationException($"{name} start boom");
        Bus = bus;
    }

    public void Tick(in TickContext context)
    {
        journal?.Add($"{name}.Tick{context.Tick}");
        Ticks.Add(context);
        if (context.Tick == ThrowOnTick)
            throw new InvalidOperationException($"{name} tick boom");
    }
}

/// <summary>Publishes a Ping carrying the tick number on every tick, without allocating.</summary>
public sealed class PingModule : IModule
{
    private IMessageBus? _bus;

    public void Start(IMessageBus bus) => _bus = bus;

    public void Tick(in TickContext context) => _bus!.Publish(new Ping((int)context.Tick));
}

/// <summary>Subscribes to Ping in Start with the given handler.</summary>
public sealed class SubscriberModule(IMessageHandler<Ping> handler) : IModule
{
    public void Start(IMessageBus bus) => bus.Subscribe(handler);

    public void Tick(in TickContext context) { }
}

// Types for discovery tests.
public sealed class GoodModule : IModule
{
    public void Start(IMessageBus bus) { }
    public void Tick(in TickContext context) { }
}

public sealed class OtherGoodModule : IModule
{
    public void Start(IMessageBus bus) { }
    public void Tick(in TickContext context) { }
}

public abstract class AbstractModule : IModule
{
    public void Start(IMessageBus bus) { }
    public void Tick(in TickContext context) { }
}

public sealed class NoDefaultConstructorModule(int unused) : IModule
{
    public int Unused = unused;
    public void Start(IMessageBus bus) { }
    public void Tick(in TickContext context) { }
}
