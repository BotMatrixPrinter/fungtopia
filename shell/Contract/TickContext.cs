namespace Fungtopia.Shell;

/// <summary>One fixed simulation step.</summary>
public readonly struct TickContext
{
    /// <summary>Number of this step, starting at 0.</summary>
    public readonly long Tick;

    /// <summary>Length of one step in seconds (1/60 in the game).</summary>
    public readonly double StepSeconds;

    public TickContext(long tick, double stepSeconds)
    {
        Tick = tick;
        StepSeconds = stepSeconds;
    }
}
