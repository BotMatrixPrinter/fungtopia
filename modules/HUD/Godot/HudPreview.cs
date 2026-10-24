using System;
using Fungtopia.Bow;
using Fungtopia.Player;
using Fungtopia.Shell;
using Fungtopia.Wallet;
using Godot;

namespace Fungtopia.HUD.View;

/// <summary>
/// A stand-alone scene showing the HUD over a soft forest-green backdrop, for screenshots:
/// <c>./screenshot.sh modules/HUD/Godot/Preview.tscn</c>. It runs a real <see cref="HudModule"/>
/// fed scripted messages. The environment variable HUD_PREVIEW picks the moment: "play" (default:
/// just hit down to 7 of 10, a "+50" rising after 1,200 M¢, the bow two-thirds drawn) or
/// "fainted" (the fade after a defeat).
/// </summary>
public partial class HudPreview : Node
{
    private sealed class SilentBus : IMessageBus
    {
        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct { }
        public void Publish<T>(in T message) where T : struct { }
    }

    private readonly HudModule _hud = new();
    private bool _fainted;
    private int _frame;

    public override void _Ready()
    {
        _fainted = OS.GetEnvironment("HUD_PREVIEW").Equals("fainted", StringComparison.OrdinalIgnoreCase);
        var backdrop = new TextureRect
        {
            Texture = new GradientTexture2D
            {
                Gradient = new Gradient { Colors = new[] { new Color(0.62f, 0.85f, 0.93f), new Color(0.48f, 0.75f, 0.45f) }, Offsets = new[] { 0.35f, 0.75f } },
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

        _hud.Start(new SilentBus());
        var view = (HudView)GD.Load<PackedScene>("res://modules/HUD/Godot/Main.tscn").Instantiate();
        view.Bind(_hud);
        AddChild(view);
    }

    public override void _PhysicsProcess(double delta)
    {
        int health = _fainted ? (_frame < 10 ? 2 : 0) : (_frame < 70 ? 10 : 7);
        _hud.Handle(new PlayerState(default, 0f, default, true, health, 10, !_fainted || _frame < 10, CharacterKind.Girl));
        if (_fainted && _frame == 10)
            _hud.Handle(new PlayerDefeated(default));
        if (_frame == 70)
            _hud.Handle(new CoinsEarned(50, default));
        _hud.Handle(new WalletState(_frame < 70 ? 1200 : 1250));
        _hud.Handle(new BowState(_fainted ? 0f : Math.Clamp((_frame - 40) / 36f * 0.6f, 0f, 0.65f), 0));
        _hud.Tick(new TickContext(_frame, delta));
        _frame++;
    }
}
