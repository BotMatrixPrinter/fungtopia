namespace Fungtopia.Home;

/// <summary>
/// Turns a held menu direction into steps: one at once when it is pressed, then after
/// <see cref="Delay"/> one every <see cref="Interval"/> while it stays held. A new direction starts
/// over; letting go (0, 0) stops. Plain struct, no allocations.
/// </summary>
public struct MenuRepeat
{
    /// <summary>Seconds a direction must be held before it starts repeating.</summary>
    public const float Delay = 0.35f;

    /// <summary>Seconds between repeats once repeating.</summary>
    public const float Interval = 0.12f;

    private int _x, _y;
    private float _wait;

    /// <summary>
    /// The direction held now (each -1, 0 or 1) after <paramref name="seconds"/>; true when it
    /// should step this time.
    /// </summary>
    public bool Step(int x, int y, float seconds)
    {
        if (x == 0 && y == 0)
        {
            _x = _y = 0;
            return false;
        }
        if (x != _x || y != _y)
        {
            _x = x;
            _y = y;
            _wait = Delay;
            return true;
        }
        _wait -= seconds;
        if (_wait > 0f)
            return false;
        _wait += Interval;
        if (_wait <= 0f)
            _wait = Interval;
        return true;
    }

    /// <summary>Forgets the held direction (the next press steps at once).</summary>
    public void Reset() => _x = _y = 0;
}
