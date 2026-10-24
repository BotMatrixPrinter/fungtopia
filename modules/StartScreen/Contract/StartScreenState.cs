namespace Fungtopia.StartScreen;

/// <summary>
/// Published by the StartScreen module once every tick: <see cref="Open"/> is true while the
/// start screen is showing (from the first tick until "Start!" is pressed), false after.
/// </summary>
public readonly struct StartScreenState
{
    public readonly bool Open;

    public StartScreenState(bool open) => Open = open;
}
