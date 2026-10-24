using System;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.Dialogue.View;

/// <summary>
/// A stand-alone scene showing the speech bubble over a soft town backdrop, for screenshots:
/// <c>./screenshot.sh modules/Dialogue/Godot/Preview.tscn</c>. It runs a real
/// <see cref="DialogueModule"/> fed a scripted line from Millie. The environment variable
/// DIALOGUE_PREVIEW picks the moment: "done" (default: the line fully shown, the arrow bouncing)
/// or "typing" (the line half typed out).
/// </summary>
public partial class DialoguePreview : Node
{
    private sealed class SilentBus : IMessageBus
    {
        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct { }
        public void Publish<T>(in T message) where T : struct { }
    }

    private readonly DialogueModule _dialogue = new();
    private int _sayAt;
    private int _frame;

    public override void _Ready()
    {
        _sayAt = OS.GetEnvironment("DIALOGUE_PREVIEW").Equals("typing", StringComparison.OrdinalIgnoreCase) ? 60 : 0;
        var backdrop = new TextureRect
        {
            Texture = new GradientTexture2D
            {
                Gradient = new Gradient { Colors = new[] { new Color(0.66f, 0.86f, 0.96f), new Color(0.98f, 0.86f, 0.74f), new Color(0.55f, 0.78f, 0.47f) }, Offsets = new[] { 0.2f, 0.55f, 0.8f } },
                FillFrom = new Vector2(0, 0),
                FillTo = new Vector2(0, 1),
                Width = 64,
                Height = 64,
            },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
        };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(backdrop);

        _dialogue.Start(new SilentBus());
        var view = (DialogueView)GD.Load<PackedScene>("res://modules/Dialogue/Godot/Main.tscn").Instantiate();
        view.Bind(_dialogue);
        AddChild(view);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_frame == _sayAt)
            _dialogue.Handle(new SayLine(1, "Millie", "Good morning, neighbor! Dr. Jeremy says the forest is extra sparkly today. Want to go look?", Voices.Bubbly, false));
        _dialogue.Tick(new TickContext(_frame, delta));
        _frame++;
    }
}
