using System;
using System.Collections.Generic;
using System.Reflection;

namespace Fungtopia.Shell;

/// <summary>
/// Runs every module's fixed-timestep simulation. Each module's Start, Tick and message
/// handlers run inside its own guard: a module that throws is disabled and logged, and the
/// others go on.
/// </summary>
public sealed class ModuleHost
{
    private readonly MessageBus _bus = new();
    private readonly List<ModuleSlot> _modules = new();
    private readonly Action<string> _log;
    private bool _started;

    public ModuleHost(double stepSeconds, Action<string>? log = null)
    {
        if (!(stepSeconds > 0))
            throw new ArgumentOutOfRangeException(nameof(stepSeconds));
        StepSeconds = stepSeconds;
        _log = log ?? (_ => { });
    }

    public double StepSeconds { get; }

    /// <summary>Number of ticks run so far (the next tick's number).</summary>
    public long TickCount { get; private set; }

    public IReadOnlyList<ModuleSlot> Modules => _modules;

    /// <summary>Raised once when a module's guard trips (after it is logged).</summary>
    public event Action<ModuleSlot>? ModuleDisabled;

    public ModuleSlot? Find(string name)
    {
        foreach (var slot in _modules)
            if (slot.Name == name)
                return slot;
        return null;
    }

    public ModuleSlot Add(string name, IModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        return AddSlot(name, module);
    }

    /// <summary>
    /// Adds the module of every Fungtopia.&lt;Name&gt;.Core assembly among the given ones, in name
    /// order. A module whose class is missing, ambiguous or throws on creation is added disabled.
    /// </summary>
    public void AddDiscovered(IEnumerable<Assembly> assemblies)
    {
        var found = new SortedDictionary<string, Assembly>(StringComparer.Ordinal);
        foreach (var assembly in assemblies)
        {
            var name = ModuleDiscovery.ModuleNameOf(assembly.GetName().Name);
            if (name != null && Find(name) == null)
                found.TryAdd(name, assembly);
        }
        foreach (var (name, assembly) in found)
        {
            IModule? module = null;
            Exception? error;
            try
            {
                Type?[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types;
                }
                var type = ModuleDiscovery.FindModuleType(types, out var reason);
                error = type == null ? new InvalidOperationException(reason) : null;
                if (type != null)
                    module = (IModule)Activator.CreateInstance(type)!;
            }
            catch (Exception e)
            {
                error = e is TargetInvocationException { InnerException: { } inner } ? inner : e;
            }
            var slot = AddSlot(name, module);
            if (error != null)
                slot.Fail("discovery", error);
        }
    }

    private ModuleSlot AddSlot(string name, IModule? module)
    {
        if (_started)
            throw new InvalidOperationException("Modules must be added before Start.");
        if (Find(name) != null)
            throw new ArgumentException($"A module named '{name}' is already added.", nameof(name));
        var slot = new ModuleSlot(name, module, _bus, OnFailed);
        _modules.Add(slot);
        return slot;
    }

    /// <summary>Trips a module's guard from outside the host (for example when its view fails).</summary>
    public void Disable(ModuleSlot slot, string during, Exception error) => slot.Fail(during, error);

    private void OnFailed(ModuleSlot slot, string during, Exception error)
    {
        _log($"Fungtopia: module '{slot.Name}' disabled ({during}): {error}");
        ModuleDisabled?.Invoke(slot);
    }

    /// <summary>Starts every enabled module once, each inside its guard.</summary>
    public void Start()
    {
        if (_started)
            return;
        _started = true;
        foreach (var slot in _modules)
        {
            if (!slot.Enabled)
                continue;
            try
            {
                slot.Module!.Start(slot.Bus);
                _log($"Fungtopia: module '{slot.Name}' started");
            }
            catch (Exception e)
            {
                slot.Fail("Start", e);
            }
        }
    }

    /// <summary>Runs one fixed step of every enabled module, each inside its guard.</summary>
    public void Tick()
    {
        if (!_started)
            Start();
        var context = new TickContext(TickCount, StepSeconds);
        var modules = _modules;
        for (int i = 0; i < modules.Count; i++)
        {
            var slot = modules[i];
            if (!slot.Enabled)
                continue;
            try
            {
                slot.Module!.Tick(in context);
            }
            catch (Exception e)
            {
                slot.Fail("Tick", e);
            }
        }
        TickCount++;
    }
}
