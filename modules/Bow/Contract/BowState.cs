using System.Numerics;

namespace Fungtopia.Bow;

/// <summary>The player's bow at the end of a fixed tick, published by the Bow module exactly once per tick.</summary>
public readonly struct BowState
{
    /// <summary>How far the bow is drawn: 0 (relaxed) to 1 (full, after 0.6 s of holding Fire).</summary>
    public readonly float Draw;

    /// <summary>Arrows flying right now (not those stuck in the ground or a trunk).</summary>
    public readonly int ArrowsInFlight;

    /// <summary>
    /// The world point the crosshair (screen center) is on: the first monster, solid obstacle or
    /// ground along the aim ray, else the point 60 m along it. Arrows are shot from the hold point
    /// toward it. Zero until the bow has a player.
    /// </summary>
    public readonly Vector3 Target;

    public BowState(float draw, int arrowsInFlight)
        : this(draw, arrowsInFlight, Vector3.Zero)
    {
    }

    public BowState(float draw, int arrowsInFlight, Vector3 target)
    {
        Draw = draw;
        ArrowsInFlight = arrowsInFlight;
        Target = target;
    }
}
