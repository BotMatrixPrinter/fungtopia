using System;
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
    /// World-space unit vector from the current 3D camera through the screen center;
    /// zero when <see cref="HasAim"/> is false.
    /// </summary>
    public readonly Vector3 AimDirection;

    /// <summary>
    /// World-space start of the aim ray: the current 3D camera's position;
    /// zero when <see cref="HasAim"/> is false.
    /// </summary>
    public readonly Vector3 AimOrigin;

    /// <summary>Eat was pressed during this tick (eat the selected food).</summary>
    public readonly bool Eat;

    // Stored plus one so that default(PlayerInput) reads FoodSelect -1 (none).
    private readonly int _foodSelectPlusOne;

    /// <summary>Food slot picked during this tick: 0, 1 or 2 (keys 1 / 2 / 3); -1 when none.</summary>
    public int FoodSelect => _foodSelectPlusOne - 1;

    /// <summary>Food selection step during this tick: -1 (previous), +1 (next) or 0.</summary>
    public readonly int FoodCycle;

    public PlayerInput(Vector2 move, Vector2 look, bool jump, bool fire, bool hasAim, Vector3 aimDirection)
        : this(move, look, jump, fire, hasAim, aimDirection, false, false)
    {
    }

    public PlayerInput(Vector2 move, Vector2 look, bool jump, bool fire, bool hasAim, Vector3 aimDirection,
        bool interact, bool openMap)
        : this(move, look, jump, fire, hasAim, aimDirection, interact, openMap, Vector3.Zero)
    {
    }

    public PlayerInput(Vector2 move, Vector2 look, bool jump, bool fire, bool hasAim, Vector3 aimDirection,
        bool interact, bool openMap, Vector3 aimOrigin)
        : this(move, look, jump, fire, hasAim, aimDirection, interact, openMap, aimOrigin, false, -1, 0)
    {
    }

    /// <summary>
    /// Full constructor. <paramref name="foodSelect"/> outside 0..2 means none (-1);
    /// <paramref name="foodCycle"/> is reduced to its sign (-1, 0 or +1).
    /// </summary>
    public PlayerInput(Vector2 move, Vector2 look, bool jump, bool fire, bool hasAim, Vector3 aimDirection,
        bool interact, bool openMap, Vector3 aimOrigin, bool eat, int foodSelect, int foodCycle)
    {
        Move = move;
        Look = look;
        Jump = jump;
        Fire = fire;
        HasAim = hasAim;
        AimDirection = aimDirection;
        AimOrigin = hasAim ? aimOrigin : Vector3.Zero;
        Interact = interact;
        OpenMap = openMap;
        Eat = eat;
        _foodSelectPlusOne = foodSelect is >= 0 and <= 2 ? foodSelect + 1 : 0;
        FoodCycle = Math.Sign(foodCycle);
    }
}
