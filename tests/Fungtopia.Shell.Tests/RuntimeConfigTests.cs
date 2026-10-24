using System;
using System.IO;
using Xunit;

namespace Fungtopia.Shell.Tests;

/// <summary>The root Directory.Build.props: test projects run without tiered compilation or tiered PGO,
/// so the JIT does not recompile (and allocate) in the middle of an allocation test.</summary>
public class RuntimeConfigTests
{
    [Theory]
    [InlineData("System.Runtime.TieredCompilation")]
    [InlineData("System.Runtime.TieredPGO")]
    public void TestsRunWithoutTiering(string setting)
    {
        Assert.Equal("false", AppContext.GetData(setting)?.ToString(), ignoreCase: true);
    }

    [Fact]
    public void EveryTestProjectGetsTheTestSettings()
    {
        // Directory.Build.props picks the test projects by a name ending in ".Tests".
        var root = RepositoryRoot();
        var projects = Directory.GetFiles(Path.Combine(root, "modules"), "*.csproj", SearchOption.AllDirectories);
        int tests = 0;
        foreach (var project in projects)
        {
            string folder = Path.GetFileName(Path.GetDirectoryName(project))!;
            string name = Path.GetFileNameWithoutExtension(project);
            Assert.Equal(folder == "Tests", name.EndsWith(".Tests", StringComparison.Ordinal));
            if (folder == "Tests")
                tests++;
        }
        Assert.True(tests > 0);
        Assert.Contains("$(MSBuildProjectName.EndsWith('.Tests'))", File.ReadAllText(Path.Combine(root, "Directory.Build.props")));
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Fungtopia.csproj")))
                return dir.FullName;
        throw new InvalidOperationException("no Fungtopia.csproj above " + AppContext.BaseDirectory);
    }
}
