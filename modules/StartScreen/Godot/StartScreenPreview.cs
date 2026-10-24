using System;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.StartScreen.View;

/// <summary>
/// A stand-alone scene showing the start screen at one step, for screenshots:
/// <c>./screenshot.sh modules/StartScreen/Godot/Preview.tscn</c>. The environment variable
/// START_PREVIEW picks the step: "title" (default), "character", "boy", "pet", "dog" or "ready"
/// (the Boy with the Dog).
/// </summary>
public partial class StartScreenPreview : Node
{
    /// <summary>A bus nobody listens to.</summary>
    private sealed class QuietBus : IMessageBus
    {
        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct { }

        public void Publish<T>(in T message) where T : struct { }
    }

    private readonly StartScreenModule _screen = new();
    private long _tick;

    public override void _Ready()
    {
        _screen.Start(new QuietBus());
        string step = OS.GetEnvironment("START_PREVIEW").ToLowerInvariant();
        StartButton[] presses = step switch
        {
            "character" => new[] { StartButton.Confirm },
            "boy" => new[] { StartButton.Confirm, StartButton.Right },
            "pet" => new[] { StartButton.Confirm, StartButton.Confirm },
            "dog" => new[] { StartButton.Confirm, StartButton.Right, StartButton.Confirm, StartButton.Right },
            "ready" => new[] { StartButton.Confirm, StartButton.Right, StartButton.Confirm, StartButton.Right, StartButton.Confirm },
            _ => Array.Empty<StartButton>(),
        };
        foreach (var press in presses)
            _screen.Press(press);
        var view = GD.Load<PackedScene>("res://modules/StartScreen/Godot/Main.tscn").Instantiate<StartScreenView>();
        view.Bind(_screen);
        AddChild(view);
    }

    public override void _PhysicsProcess(double delta) => _screen.Tick(new TickContext(_tick++, delta));
}
