using System;
using System.Numerics;

namespace Fungtopia.Bow;

/// <summary>
/// Where the character holds the bow, shared by the Bow module (arrows leave from here) and the
/// Player module (it puts the character's bow hand here and draws the bow and the nocked arrow).
/// The hold point is an offset from the player's feet in the player's facing frame: Right, Up and
/// Forward, in meters, where Forward is the facing direction (-sin Facing, 0, -cos Facing) of
/// <c>Fungtopia.Player.PlayerState.Facing</c> and Right is (cos Facing, 0, -sin Facing).
/// </summary>
public static class BowGeometry
{
    /// <summary>The bow hand is this far to the character's right of its feet, meters.</summary>
    public const float HoldRight = 0.15f;

    /// <summary>The bow hand is this high above the feet (the chibi character's chest and hands), meters.</summary>
    public const float HoldUp = 1.15f;

    /// <summary>The bow hand is this far ahead of the feet along the facing, meters.</summary>
    public const float HoldForward = 0.45f;

    /// <summary>
    /// How far the string (and the nocked arrow's back) comes back from the bow toward the
    /// character at a full draw (<see cref="BowState.Draw"/> 1), meters; a draw of d pulls d times this.
    /// </summary>
    public const float FullDrawPull = 0.26f;

    /// <summary>The hold point offset in the facing frame: X Right, Y Up, Z Forward, meters.</summary>
    public static Vector3 HoldOffset => new(HoldRight, HoldUp, HoldForward);

    /// <summary>
    /// The hold point in the world for a player whose feet are at <paramref name="feet"/> facing
    /// <paramref name="facing"/> (yaw in radians, 0 faces -Z, positive turns left).
    /// </summary>
    public static Vector3 HoldPoint(Vector3 feet, float facing)
    {
        float s = MathF.Sin(facing), c = MathF.Cos(facing);
        var right = new Vector3(c, 0f, -s);
        var forward = new Vector3(-s, 0f, -c);
        return feet + right * HoldRight + Vector3.UnitY * HoldUp + forward * HoldForward;
    }
}
