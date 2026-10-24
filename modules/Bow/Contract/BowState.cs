namespace Fungtopia.Bow;

/// <summary>The player's bow at the end of a fixed tick, published by the Bow module exactly once per tick.</summary>
public readonly struct BowState
{
    /// <summary>How far the bow is drawn: 0 (relaxed) to 1 (full, after 0.6 s of holding Fire).</summary>
    public readonly float Draw;

    /// <summary>Arrows flying right now (not those stuck in the ground or a trunk).</summary>
    public readonly int ArrowsInFlight;

    public BowState(float draw, int arrowsInFlight)
    {
        Draw = draw;
        ArrowsInFlight = arrowsInFlight;
    }
}
