namespace Fungtopia.Clock;

/// <summary>Published by the Clock module once per fixed step.</summary>
public readonly struct ClockTicked
{
    /// <summary>Steps counted so far, including this one (1 on the first step).</summary>
    public readonly long Steps;

    /// <summary>Simulated seconds so far.</summary>
    public readonly double Seconds;

    public ClockTicked(long steps, double seconds)
    {
        Steps = steps;
        Seconds = seconds;
    }
}
