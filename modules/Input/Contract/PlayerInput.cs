using System.Numerics;

namespace Fungtopia.Input;

/// <summary>
/// The player's input for one fixed tick, published by the Input module exactly once per tick.
/// </summary>
public readonly struct PlayerInput
{
    /// <summary>Move stick: x right, y forward, relative to the camera; length 0..1.</summary>
    public readonly Vector2 Move;

    /// <summary>Camera turn this tick in radians: x yaw right, y pitch up.</summary>
    public readonly Vector2 Look;

    /// <summary>Jump was pressed during this tick.</summary>
    public readonly bool Jump;

    /// <summary>Fire is held.</summary>
    public readonly bool Fire;

    /// <summary>Interact was pressed during this tick (talk, use a signpost or door).</summary>
    public readonly bool Interact;

    /// <summary>Open map was pressed during this tick.</summary>
    public readonly bool OpenMap;

    /// <summary>True when <see cref="AimDirection"/> is valid (a 3D camera exists).</summary>
    public readonly bool HasAim;

    /// <summary>
    /// World-space unit vector from the current 3D camera through the screen-center crosshair;
    /// zero when <see cref="HasAim"/> is false.
    /// </summary>
    public readonly Vector3 AimDirection;

    public PlayerInput(Vector2 move, Vector2 look, bool jump, bool fire, bool hasAim, Vector3 aimDirection)
        : this(move, look, jump, fire, hasAim, aimDirection, false, false)
    {
    }

    public PlayerInput(Vector2 move, Vector2 look, bool jump, bool fire, bool hasAim, Vector3 aimDirection,
        bool interact, bool openMap)
    {
        Move = move;
        Look = look;
        Jump = jump;
        Fire = fire;
        HasAim = hasAim;
        AimDirection = aimDirection;
        Interact = interact;
        OpenMap = openMap;
    }
}
