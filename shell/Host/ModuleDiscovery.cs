using System;
using System.Collections.Generic;
using System.IO;

namespace Fungtopia.Shell;

/// <summary>
/// Finds modules by convention: module &lt;Name&gt;'s Core builds the assembly
/// Fungtopia.&lt;Name&gt;.Core, which holds exactly one public, non-abstract IModule class with a
/// public parameterless constructor.
/// </summary>
public static class ModuleDiscovery
{
    private const string Prefix = "Fungtopia.";
    private const string Suffix = ".Core";

    /// <summary>"Fungtopia.Clock.Core" gives "Clock"; anything else gives null.</summary>
    public static string? ModuleNameOf(string? assemblyName)
    {
        if (assemblyName == null
            || assemblyName.Length <= Prefix.Length + Suffix.Length
            || !assemblyName.StartsWith(Prefix, StringComparison.Ordinal)
            || !assemblyName.EndsWith(Suffix, StringComparison.Ordinal))
            return null;
        var name = assemblyName.Substring(Prefix.Length, assemblyName.Length - Prefix.Length - Suffix.Length);
        return name.Length == 0 || name.Contains('.') || name == "Shell" ? null : name;
    }

    /// <summary>Names of the module Core assemblies (Fungtopia.&lt;Name&gt;.Core.dll) in a folder, sorted.</summary>
    public static List<string> FindCoreAssemblyNames(string directory)
    {
        var names = new List<string>();
        if (!Directory.Exists(directory))
            return names;
        foreach (var file in Directory.GetFiles(directory, Prefix + "*" + Suffix + ".dll"))
        {
            var assemblyName = Path.GetFileNameWithoutExtension(file);
            if (ModuleNameOf(assemblyName) != null)
                names.Add(assemblyName);
        }
        names.Sort(StringComparer.Ordinal);
        return names;
    }

    /// <summary>The one module class among the types, or null with the reason in <paramref name="error"/>.</summary>
    public static Type? FindModuleType(IEnumerable<Type?> types, out string? error)
    {
        Type? found = null;
        foreach (var type in types)
        {
            if (type == null || !type.IsClass || type.IsAbstract || !type.IsVisible
                || !typeof(IModule).IsAssignableFrom(type))
                continue;
            if (found != null)
            {
                error = $"more than one IModule class ({found.FullName}, {type.FullName})";
                return null;
            }
            found = type;
        }
        if (found == null)
            error = "no public IModule class";
        else if (found.GetConstructor(Type.EmptyTypes) == null)
            error = $"{found.FullName} has no public parameterless constructor";
        else
            error = null;
        return error == null ? found : null;
    }
}
