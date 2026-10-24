using Fungtopia.Shell;
using Godot;

namespace Fungtopia.Clock.View;

/// <summary>Shows the clock's simulated seconds in a corner of the screen.</summary>
public partial class ClockView : Label, IModuleView
{
    private ClockModule? _clock;
    private long _shownSecond = -1;

    public void Bind(IModule module) => _clock = (ClockModule)module;

    public override void _Process(double delta)
    {
        if (_clock == null)
            return;
        long second = (long)_clock.Seconds;
        if (second == _shownSecond)
            return;
        _shownSecond = second;
        Text = $"{second} s";
    }
}
