using System;
using System.Threading;

namespace Fungtopia.Shell;

/// <summary>
/// The shared synchronous bus. Each message type gets a channel found by a static index, so
/// <see cref="Publish{T}"/> does no lookup allocation and no boxing. Subscribing copies the
/// channel's handler array, so handlers may subscribe while a message is being delivered.
/// A handler owned by a module runs inside that module's guard: if it throws, its module is
/// disabled and delivery goes on to the other handlers.
/// </summary>
public sealed class MessageBus : IMessageBus
{
    private object?[] _channels = new object?[8];

    public void Subscribe<T>(IMessageHandler<T> handler) where T : struct => Subscribe(handler, null);

    internal void Subscribe<T>(IMessageHandler<T> handler, ModuleSlot? owner) where T : struct
    {
        ArgumentNullException.ThrowIfNull(handler);
        int index = TypeIndex<T>.Value;
        if (index >= _channels.Length)
            Array.Resize(ref _channels, Math.Max(index + 1, _channels.Length * 2));
        var channel = _channels[index] as Channel<T>;
        if (channel == null)
            _channels[index] = channel = new Channel<T>();
        channel.Add(new Entry<T>(handler, owner));
    }

    public void Publish<T>(in T message) where T : struct
    {
        int index = TypeIndex<T>.Value;
        if (index >= _channels.Length || _channels[index] is not Channel<T> channel)
            return;
        var entries = channel.Entries;
        for (int i = 0; i < entries.Length; i++)
        {
            var owner = entries[i].Owner;
            if (owner == null)
            {
                entries[i].Handler.Handle(in message);
                continue;
            }
            if (!owner.Enabled)
                continue;
            try
            {
                entries[i].Handler.Handle(in message);
            }
            catch (Exception e)
            {
                owner.Fail("handling " + typeof(T).Name, e);
            }
        }
    }

    private readonly struct Entry<T>(IMessageHandler<T> handler, ModuleSlot? owner) where T : struct
    {
        public readonly IMessageHandler<T> Handler = handler;
        public readonly ModuleSlot? Owner = owner;
    }

    private sealed class Channel<T> where T : struct
    {
        public Entry<T>[] Entries = [];

        public void Add(Entry<T> entry)
        {
            var next = new Entry<T>[Entries.Length + 1];
            Array.Copy(Entries, next, Entries.Length);
            next[^1] = entry;
            Entries = next;
        }
    }

    private static int _nextTypeIndex = -1;

    private static class TypeIndex<T>
    {
        public static readonly int Value = Interlocked.Increment(ref _nextTypeIndex);
    }
}
