using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace Fungtopia.Shell.Tests;

public class DiscoveryTests
{
    [Theory]
    [InlineData("Fungtopia.Clock.Core", "Clock")]
    [InlineData("Fungtopia.PetCity.Core", "PetCity")]
    [InlineData("Fungtopia.Clock.Contract", null)]
    [InlineData("Fungtopia.Clock.Tests", null)]
    [InlineData("Fungtopia.Shell.Core", null)]
    [InlineData("Fungtopia.Core", null)]
    [InlineData("Fungtopia..Core", null)]
    [InlineData("Fungtopia.A.B.Core", null)]
    [InlineData("Other.Clock.Core", null)]
    [InlineData(null, null)]
    public void ModuleNameComesFromTheCoreAssemblyName(string? assemblyName, string? expected)
    {
        Assert.Equal(expected, ModuleDiscovery.ModuleNameOf(assemblyName));
    }

    [Fact]
    public void FindsTheOneModuleClass()
    {
        var type = ModuleDiscovery.FindModuleType(
            new[] { typeof(string), typeof(AbstractModule), typeof(GoodModule), null }, out var error);
        Assert.Equal(typeof(GoodModule), type);
        Assert.Null(error);
    }

    [Fact]
    public void RefusesMoreThanOneModuleClass()
    {
        var type = ModuleDiscovery.FindModuleType(new[] { typeof(GoodModule), typeof(OtherGoodModule) }, out var error);
        Assert.Null(type);
        Assert.Contains("more than one", error);
    }

    [Fact]
    public void RefusesNoModuleClassOrOneWithoutParameterlessConstructor()
    {
        Assert.Null(ModuleDiscovery.FindModuleType(new[] { typeof(string), typeof(AbstractModule) }, out var none));
        Assert.Contains("no public IModule", none);
        Assert.Null(ModuleDiscovery.FindModuleType(new[] { typeof(NoDefaultConstructorModule) }, out var ctor));
        Assert.Contains("parameterless", ctor);
    }

    [Fact]
    public void FindsCoreAssemblyFilesInAFolder()
    {
        var dir = Directory.CreateTempSubdirectory("fungtopia-discovery").FullName;
        try
        {
            foreach (var file in new[] { "Fungtopia.Zoo.Core.dll", "Fungtopia.Clock.Core.dll", "Fungtopia.Clock.Contract.dll",
                         "Fungtopia.Shell.Host.dll", "Fungtopia.dll", "Fungtopia.Clock.Core.pdb" })
                File.WriteAllText(Path.Combine(dir, file), "");

            Assert.Equal(new[] { "Fungtopia.Clock.Core", "Fungtopia.Zoo.Core" }, ModuleDiscovery.FindCoreAssemblyNames(dir));
            Assert.Empty(ModuleDiscovery.FindCoreAssemblyNames(Path.Combine(dir, "missing")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void HostAddsDiscoveredModulesAndDisablesOnesItCannotUse()
    {
        var log = new System.Collections.Generic.List<string>();
        var host = new ModuleHost(1.0 / 60, log.Add);

        // Not a Fungtopia.<Name>.Core assembly: ignored. This test assembly holds many IModule
        // classes, so under a module name it is refused as ambiguous and added disabled.
        host.AddDiscovered(new[] { typeof(string).Assembly, new RenamedAssembly(typeof(GoodModule).Assembly, "Fungtopia.Fakes.Core") });

        var slot = Assert.Single(host.Modules);
        Assert.Equal("Fakes", slot.Name);
        Assert.False(slot.Enabled);
        Assert.Null(slot.Module);
        Assert.Contains(log, l => l.Contains("module 'Fakes' disabled (discovery)") && l.Contains("more than one"));
        host.Tick();
    }

    [Fact]
    public void HostCreatesTheModuleOfADiscoveredAssembly()
    {
        var host = new ModuleHost(1.0 / 60);
        host.AddDiscovered(new Assembly[] { new SingleTypeAssembly("Fungtopia.Good.Core", typeof(GoodModule), typeof(string)) });

        var slot = Assert.Single(host.Modules);
        Assert.Equal("Good", slot.Name);
        Assert.True(slot.Enabled);
        Assert.IsType<GoodModule>(slot.Module);
    }

    [Fact]
    public void AModuleWhoseConstructorThrowsIsAddedDisabled()
    {
        var log = new System.Collections.Generic.List<string>();
        var host = new ModuleHost(1.0 / 60, log.Add);
        host.AddDiscovered(new Assembly[]
        {
            new SingleTypeAssembly("Fungtopia.Bad.Core", typeof(ThrowingConstructorModule)),
            new SingleTypeAssembly("Fungtopia.Good.Core", typeof(GoodModule)),
        });

        Assert.Equal(new[] { "Bad", "Good" }, Array.ConvertAll(new[] { host.Modules[0], host.Modules[1] }, s => s.Name));
        Assert.False(host.Modules[0].Enabled);
        Assert.Equal("constructor boom", host.Modules[0].Error!.Message);
        Assert.True(host.Modules[1].Enabled);
        Assert.Contains(log, l => l.Contains("module 'Bad' disabled (discovery)"));
    }

    public sealed class ThrowingConstructorModule : IModule
    {
        public ThrowingConstructorModule() => throw new InvalidOperationException("constructor boom");
        public void Start(IMessageBus bus) { }
        public void Tick(in TickContext context) { }
    }

    /// <summary>An assembly stand-in with a chosen name and types.</summary>
    private class SingleTypeAssembly(string name, params Type[] types) : Assembly
    {
        public override AssemblyName GetName() => new(name);
        public override Type[] GetTypes() => types;
    }

    private sealed class RenamedAssembly(Assembly inner, string name) : SingleTypeAssembly(name, inner.GetTypes());
}
