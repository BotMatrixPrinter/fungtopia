using System;
using Fungtopia.Map;
using Fungtopia.Player;
using Fungtopia.Save;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.StartScreen.View;

/// <summary>
/// A stand-alone scene showing the start screen at one step, for screenshots:
/// <c>./screenshot.sh modules/StartScreen/Godot/Preview.tscn</c>. The environment variable
/// START_PREVIEW picks the step: "title" (default, no saves), "character", "boy", "look" (the
/// Girl's look step), "boylook" (the Boy's, with a few looks changed), "pet", "dog" or "ready"
/// (the Boy with the Dog); with made-up saves (slot 1 both saves, slot 2 damaged, slot 3 empty):
/// "menu" (the title with Continue), "load" (the slots), "loadsave" (slot 1's two saves),
/// "newslot" (New Game's slots) or "overwrite" (New Game in slot 1).
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
            "character" => new[] { StartButton.Confirm, StartButton.Confirm },
            "boy" => new[] { StartButton.Confirm, StartButton.Confirm, StartButton.Right },
            "look" => new[] { StartButton.Confirm, StartButton.Confirm, StartButton.Confirm },
            "boylook" => new[]
            {
                StartButton.Confirm, StartButton.Confirm, StartButton.Right, StartButton.Confirm, StartButton.Right, StartButton.Right,
                StartButton.Down, StartButton.Down, StartButton.Right, StartButton.Right, StartButton.Right,
            },
            "pet" => new[] { StartButton.Confirm, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm },
            "dog" => new[] { StartButton.Confirm, StartButton.Confirm, StartButton.Right, StartButton.Confirm, StartButton.Confirm, StartButton.Right },
            "ready" => new[]
            {
                StartButton.Confirm, StartButton.Confirm, StartButton.Right, StartButton.Confirm, StartButton.Confirm,
                StartButton.Right, StartButton.Confirm,
            },
            "load" => new[] { StartButton.Down, StartButton.Confirm },
            "loadsave" => new[] { StartButton.Down, StartButton.Confirm, StartButton.Confirm },
            "newslot" => new[] { StartButton.Down, StartButton.Down, StartButton.Confirm },
            "overwrite" => new[] { StartButton.Down, StartButton.Down, StartButton.Confirm, StartButton.Confirm },
            _ => Array.Empty<StartButton>(),
        };
        if (step is "menu" or "load" or "loadsave" or "newslot" or "overwrite")
            _screen.Handle(MadeUpSlots());
        foreach (var press in presses)
            _screen.Press(press);
        var view = GD.Load<PackedScene>("res://modules/StartScreen/Godot/Main.tscn").Instantiate<StartScreenView>();
        view.Bind(_screen);
        AddChild(view);
    }

    private static SaveSlots MadeUpSlots()
    {
        var now = DateTime.UtcNow;
        var manual = new SaveInfo(SaveFileState.Ok, now.AddHours(-3), "Girl", CharacterKind.Girl, 4, 120, Area.Forest);
        var auto = new SaveInfo(SaveFileState.Ok, now.AddMinutes(-20), "Girl", CharacterKind.Girl, 5, 245, Area.PetCity);
        return new SaveSlots(
            new SlotInfo(1, manual, auto),
            new SlotInfo(2, SaveInfo.Unreadable, SaveInfo.Missing),
            new SlotInfo(3, SaveInfo.Missing, SaveInfo.Missing));
    }

    public override void _PhysicsProcess(double delta) => _screen.Tick(new TickContext(_tick++, delta));
}
