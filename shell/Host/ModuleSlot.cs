using System;

namespace Fungtopia.Shell;

/// <summary>One module in the host: its name, its Core, and whether its guard has tripped.</summary>
public sealed class ModuleSlot
{
    private readonly Action<ModuleSlot, string, Exception> _failed;

    internal ModuleSlot(string name, IModule? module, MessageBus bus, Action<ModuleSlot, string, Exception> failed)
    {
        Name = name;
        Module = module;
        Enabled = module != null;
        Bus = new ModuleBus(bus, this);
        _failed = failed;
    }

    public string Name { get; }

    /// <summary>The module's Core, or null when it could not be found or created.</summary>
    public IModule? Module { get; }

    /// <summary>False once the module has thrown (or could not be created): it no longer runs.</summary>
    public bool Enabled { get; private set; }

    public Exception? Error { get; private set; }

    internal IMessageBus Bus { get; }

    internal void Fail(string during, Exception error)
    {
        if (Error != null)
            return;
        Enabled = false;
        Error = error;
        _failed(this, during, error);
    }

    /// <summary>The bus as one module sees it: its subscriptions run inside its own guard.</summary>
    private sealed class ModuleBus(MessageBus bus, ModuleSlot owner) : IMessageBus
    {
        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct => bus.Subscribe(handler, owner);

        public void Publish<T>(in T message) where T : struct => bus.Publish(in message);
    }
}
