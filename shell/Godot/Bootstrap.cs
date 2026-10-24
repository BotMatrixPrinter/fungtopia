using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.ShellGodot;

/// <summary>
/// The main scene. Finds every module's Core, ticks the host once per physics frame, and loads
/// each module's modules/&lt;Name&gt;/Godot/Main.tscn (when it exists) bound to that module's Core.
/// </summary>
public partial class Bootstrap : Node
{
    private ModuleHost _host = null!;
    private readonly Dictionary<string, Node> _views = new();

    public override void _Ready()
    {
        _host = new ModuleHost(1.0 / Engine.PhysicsTicksPerSecond, Log);
        _host.ModuleDisabled += OnModuleDisabled;
        _host.AddDiscovered(LoadCoreAssemblies());
        _host.Start();
        foreach (var slot in _host.Modules)
            if (slot.Enabled)
                LoadView(slot);
        GD.Print($"Fungtopia: bootstrap ready with {_host.Modules.Count} module(s)");
    }

    public override void _PhysicsProcess(double delta) => _host.Tick();

    private static void Log(string message)
    {
        if (message.Contains(" disabled ", StringComparison.Ordinal))
            GD.PushWarning(message);
        else
            GD.Print(message);
    }

    private static List<Assembly> LoadCoreAssemblies()
    {
        var self = typeof(Bootstrap).Assembly;
        var context = AssemblyLoadContext.GetLoadContext(self) ?? AssemblyLoadContext.Default;
        var assemblies = new List<Assembly>();
        // Godot loads the game assembly from memory, so its Location is empty; the base
        // directory is then the folder it was built (or exported) to.
        var directory = string.IsNullOrEmpty(self.Location) ? AppContext.BaseDirectory : Path.GetDirectoryName(self.Location)!;
        foreach (var name in ModuleDiscovery.FindCoreAssemblyNames(directory))
        {
            try
            {
                assemblies.Add(context.LoadFromAssemblyName(new AssemblyName(name)));
            }
            catch (Exception e)
            {
                GD.PushWarning($"Fungtopia: module assembly '{name}' could not be loaded: {e.Message}");
            }
        }
        return assemblies;
    }

    private void LoadView(ModuleSlot slot)
    {
        var path = $"res://modules/{slot.Name}/Godot/Main.tscn";
        if (!ResourceLoader.Exists(path))
            return;
        Node? view = null;
        try
        {
            view = ResourceLoader.Load<PackedScene>(path).Instantiate();
            if (view is IModuleView moduleView)
                moduleView.Bind(slot.Module!);
            AddChild(view);
            _views[slot.Name] = view;
        }
        catch (Exception e)
        {
            view?.Free();
            _host.Disable(slot, "view", e);
        }
    }

    private void OnModuleDisabled(ModuleSlot slot)
    {
        if (_views.Remove(slot.Name, out var view))
            view.QueueFree();
    }
}
