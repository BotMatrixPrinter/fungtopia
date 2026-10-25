using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace Fungtopia.Shell.Tests;

// Runs the repository's screenshot.sh with fake dotnet, xvfb-run and godot commands on PATH.
// The fake godot records its arguments, prints the renderer banner the real one prints and
// writes two movie frames whose content names the driver; FAKE_VULKAN chooses how Vulkan behaves.
[System.Runtime.Versioning.SupportedOSPlatform("linux")]
public sealed class ScreenshotScriptTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "fungtopia-shot-" + Guid.NewGuid().ToString("N"));
    private string Repo => Path.Combine(_root, "repo");
    private string Bin => Path.Combine(_root, "bin");
    private string Shots => Path.Combine(_root, "shots");
    private string Calls => Path.Combine(_root, "godot-calls");

    public ScreenshotScriptTests()
    {
        Directory.CreateDirectory(Repo);
        Directory.CreateDirectory(Bin);
        File.Copy(Path.Combine(RepoRoot(), "screenshot.sh"), Path.Combine(Repo, "screenshot.sh"));
        WriteScript("dotnet", "exit 0");
        WriteScript("xvfb-run", "while [ \"${1:-}\" = -a ] || [ \"${1:-}\" = -s ]; do [ \"$1\" = -s ] && shift; shift; done\nexec \"$@\"");
        WriteScript("godot", """
            echo "$*" >> "$FAKE_CALLS"
            driver= movie=
            while [ $# -gt 0 ]; do
              case "$1" in
                --rendering-driver) driver="$2"; shift ;;
                --write-movie) movie="$2"; shift ;;
              esac
              shift
            done
            base="${movie%.png}"
            frames() { echo "$1 0" > "${base}00000000.png"; echo "$1 1" > "${base}00000001.png"; }
            if [ "$driver" = vulkan ]; then
              case "$FAKE_VULKAN" in
                ok) echo "Vulkan 1.4.305 - Forward+ - Using Device #0: llvmpipe"; frames vulkan; exit 0 ;;
                crash) echo "ERROR: vkCreateInstance failed"; exit 1 ;;
                godot-fallback)
                  echo "Your video card drivers seem not to support Vulkan, switching to OpenGL 3."
                  echo "OpenGL API 3.3 Mesa - Compatibility - Using Device: llvmpipe"; frames gl-by-godot; exit 0 ;;
              esac
            fi
            echo "OpenGL API 3.3 Mesa - Compatibility - Using Device: llvmpipe"; frames opengl3
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void RendersWithVulkanForwardPlus()
    {
        var (status, output) = Run("ok", Shots, "res://shell/Godot/Bootstrap.tscn", "main");

        Assert.Equal(0, status);
        var calls = File.ReadAllLines(Calls);
        Assert.Single(calls);
        Assert.StartsWith("--rendering-driver vulkan --path . --resolution 1920x1080 --fixed-fps 60 --write-movie ", calls[0]);
        Assert.EndsWith("--quit-after 90 res://shell/Godot/Bootstrap.tscn", calls[0]);
        Assert.Equal("vulkan 1", File.ReadAllText(Path.Combine(Shots, "main.png")).Trim());
        Assert.Contains($"screenshot: {Shots}/main.png (rendered with vulkan)", output);
        Assert.DoesNotContain("falling back", output);
    }

    [Theory]
    [InlineData("crash")]
    [InlineData("godot-fallback")]
    public void FallsBackToOpenGlOnlyWhenVulkanFailsAndSaysSo(string vulkan)
    {
        var (status, output) = Run(vulkan, Shots, "shell/Godot/Bootstrap.tscn");

        Assert.Equal(0, status);
        var calls = File.ReadAllLines(Calls);
        Assert.Equal(2, calls.Length);
        Assert.StartsWith("--rendering-driver vulkan ", calls[0]);
        Assert.StartsWith("--rendering-driver opengl3 ", calls[1]);
        Assert.EndsWith("--quit-after 90 res://shell/Godot/Bootstrap.tscn", calls[1]);
        Assert.Equal("opengl3 1", File.ReadAllText(Path.Combine(Shots, "screenshot.png")).Trim());
        Assert.Contains("Vulkan (Forward+) failed to start; falling back to OpenGL", output);
        Assert.Contains("(rendered with opengl3)", output);
    }

    [Fact]
    public void DefaultsToTheMainSceneAndScreenshotsFolder()
    {
        var (status, output) = Run("ok", shotDir: null);

        Assert.Equal(0, status);
        Assert.EndsWith("--quit-after 90", File.ReadAllLines(Calls)[0]);
        Assert.True(File.Exists(Path.Combine(Repo, "screenshots", "screenshot.png")), output);
    }

    private (int Status, string Output) Run(string vulkan, string? shotDir, params string[] args)
    {
        var start = new ProcessStartInfo("bash") { RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Path.Combine(Repo, "screenshot.sh"));
        foreach (var a in args) start.ArgumentList.Add(a);
        start.Environment["PATH"] = Bin + ":" + Environment.GetEnvironmentVariable("PATH");
        start.Environment["FAKE_VULKAN"] = vulkan;
        start.Environment["FAKE_CALLS"] = Calls;
        start.Environment.Remove("SCREENSHOT_FRAMES");
        if (shotDir is null) start.Environment.Remove("SCREENSHOT_DIR");
        else start.Environment["SCREENSHOT_DIR"] = shotDir;
        using var process = Process.Start(start)!;
        var stderr = process.StandardError.ReadToEndAsync();
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdout + stderr.Result);
    }

    private void WriteScript(string name, string body)
    {
        var path = Path.Combine(Bin, name);
        File.WriteAllText(path, "#!/usr/bin/env bash\n" + body + "\n");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "screenshot.sh"))) return dir.FullName;
        throw new InvalidOperationException("screenshot.sh not found above " + AppContext.BaseDirectory);
    }
}
