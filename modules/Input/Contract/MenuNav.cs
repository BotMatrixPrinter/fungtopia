namespace Fungtopia.Input;

/// <summary>
/// Published every tick by the Input module: one paced menu step, for menus to move their
/// selection by. A step fires on the tick a direction is first pushed (left stick past
/// <c>MenuStickThreshold</c> on its larger axis, D-pad, arrow keys or W A S D), then at most once
/// per <c>MenuRepeatSeconds</c> (0.2 s) while it stays held; steps are never closer than that,
/// even when the stick is flicked again and again. Every other tick both fields are 0.
/// At most one field is non-zero (vertical wins when both are pushed).
/// </summary>
public readonly struct MenuNav
{
    /// <summary>-1 up, +1 down, 0 no step.</summary>
    public readonly int Vertical;

    /// <summary>-1 left, +1 right, 0 no step.</summary>
    public readonly int Horizontal;

    public MenuNav(int vertical, int horizontal)
    {
        Vertical = System.Math.Sign(vertical);
        Horizontal = System.Math.Sign(horizontal);
    }

    /// <summary>True when this tick has a step.</summary>
    public bool Any => Vertical != 0 || Horizontal != 0;
}
