using System.Numerics;

namespace Fungtopia.Bow;

/// <summary>What an arrow slot of the pool is doing.</summary>
public enum ArrowPhase
{
    /// <summary>Unused: ready for the next shot.</summary>
    Free,
    Flying,
    /// <summary>Stuck in the ground or a giant mushroom trunk, vanishing after a while.</summary>
    Stuck,
}

/// <summary>Where a stuck arrow is stuck.</summary>
public enum ArrowStuckIn
{
    None,
    Ground,
    Trunk,
}

/// <summary>One slot of the bow's fixed arrow pool (read by the view; no Godot types).</summary>
public struct Arrow
{
    public ArrowPhase Phase;

    /// <summary>Tip position at the end of the latest tick, meters.</summary>
    public Vector3 Position;

    /// <summary>Tip position at the end of the tick before (the view interpolates between them).</summary>
    public Vector3 PreviousPosition;

    /// <summary>Meters per second while flying; zero once stuck.</summary>
    public Vector3 Velocity;

    /// <summary>Unit vector the arrow points along (its flight direction; kept when it sticks).</summary>
    public Vector3 Direction;

    /// <summary>The draw (0..1) it was shot with.</summary>
    public float Draw;

    /// <summary>Seconds since it was shot.</summary>
    public float FlightSeconds;

    /// <summary>Seconds since it stuck.</summary>
    public float StuckSeconds;

    public ArrowStuckIn StuckIn;

    /// <summary>Counts shots: a new value each time the slot is reused (1 for the first shot ever).</summary>
    public int Shot;
}
