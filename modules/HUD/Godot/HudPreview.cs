using System;
using Fungtopia.Bow;
using Fungtopia.DayNight;
using Fungtopia.Map;
using Fungtopia.NPCs;
using Fungtopia.Player;
using Fungtopia.Shell;
using Fungtopia.Wallet;
using Godot;

namespace Fungtopia.HUD.View;

/// <summary>
/// A stand-alone scene showing the HUD over a soft forest-green backdrop, for screenshots:
/// <c>./screenshot.sh modules/HUD/Godot/Preview.tscn</c>. It runs a real <see cref="HudModule"/>
/// fed scripted messages. The environment variable HUD_PREVIEW picks the moment: "play" (default:
/// Day 3 at 2:05 PM, just entered Mushroom Forest with its banner sliding in, the quest at 3/5, just
/// hit down to 7 of 10, a "+50" rising after 1,200 M¢, the bow two-thirds drawn), "night" (10:40 PM
/// under the moon, the quest complete with its check), "fainted" (black after a defeat) or "woke"
/// (Day 4's wake-up text as the screen comes back in Mushroom Town).
/// </summary>
public partial class HudPreview : Node
{
    private sealed class SilentBus : IMessageBus
    {
        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct { }
        public void Publish<T>(in T message) where T : struct { }
    }

    private readonly HudModule _hud = new();
    private bool _fainted, _woke, _night;
    private int _frame;

    public override void _Ready()
    {
        string mode = OS.GetEnvironment("HUD_PREVIEW");
        _woke = mode.Equals("woke", StringComparison.OrdinalIgnoreCase);
        _fainted = _woke || mode.Equals("fainted", StringComparison.OrdinalIgnoreCase);
        _night = mode.Equals("night", StringComparison.OrdinalIgnoreCase);
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
        // Woke: fainted in the forest and already black at frame 0, woken in town at frame 20.
        if (_woke && _frame == 0)
        {
            _hud.Handle(new PlayerState(default, 0f, default, true, 0, 10, false, CharacterKind.Girl, Area.Forest));
            _hud.Handle(new PlayerDefeated(default));
            for (int i = 0; i < 90; i++)
                _hud.Tick(new TickContext(i, delta));
        }
        bool awake = _woke ? _frame >= 20 : !_fainted || _frame < 10;
        int health = _woke ? (awake ? 10 : 0) : _fainted ? (_frame < 10 ? 2 : 0) : (_frame < 70 ? 10 : 7);
        var area = _woke ? (awake ? Area.Town : Area.Forest) : _frame < 50 ? Area.Road : Area.Forest;
        if (_woke && _frame == 20)
            _hud.Handle(new WokeUp(4));
        _hud.Handle(new PlayerState(default, 0f, default, true, health, 10, awake, CharacterKind.Girl, area));
        if (_fainted && _frame == (_woke ? 0 : 10))
            _hud.Handle(new PlayerDefeated(default));
        float hour = _woke ? 7f : _night ? 22.67f : 14.09f;
        _hud.Handle(new TimeOfDay(_woke ? 4 : 3, hour, TimeOfDay.IsNightAt(hour)));
        _hud.Handle(new QuestState(true, "Defeat 5 Mushroom Caterpillars", _night ? 5 : 3, 5, _night));
        if (_frame == 70 && !_fainted)
            _hud.Handle(new CoinsEarned(50, default));
        _hud.Handle(new WalletState(_frame < 70 ? 1200 : 1250));
        _hud.Handle(new BowState(_fainted ? 0f : Math.Clamp((_frame - 40) / 36f * 0.6f, 0f, 0.65f), 0));
        _hud.Tick(new TickContext(_frame, delta));
        _frame++;
    }
}
