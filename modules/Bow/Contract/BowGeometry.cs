using System;
using System.Numerics;

namespace Fungtopia.Bow;

/// <summary>
/// Where the character holds the bow, shared by the Bow module (arrows leave from here) and the
/// Player module (it puts the character's bow hand here and draws the bow and the nocked arrow).
/// The hold point is an offset from the player's feet in the player's facing frame: Right, Up and
/// Forward, in meters, where Forward is the facing direction (-sin Facing, 0, -cos Facing) of
/// <c>Fungtopia.Player.PlayerState.Facing</c> and Right is (cos Facing, 0, -sin Facing).
/// The bow is held on the character's right side by the left hand (reaching forward and across
/// the chest), right of and below the big chibi head (about 0.86 m wide, its center
/// <see cref="HeadUp"/> above the feet), more than <see cref="MinHeadClearance"/> from the head's
/// center, so the over-the-right-shoulder aim camera sees it; the right hand draws the string to
/// the right cheek.
/// </summary>
public static class BowGeometry
{
    /// <summary>The bow hand is this far to the character's right of its feet, meters (positive: on the right).</summary>
    public const float HoldRight = 0.45f;

    /// <summary>The bow hand is this high above the feet (below the head's center), meters.</summary>
    public const float HoldUp = 0.80f;

    /// <summary>The bow hand is this far ahead of the feet along the facing, meters.</summary>
    public const float HoldForward = 0.45f;

    /// <summary>The chibi head's center is about this high above the feet (over them), meters.</summary>
    public const float HeadUp = 1.09f;

    /// <summary>The hold point is at least this far from the head's center (it is about 0.70 m), meters.</summary>
    public const float MinHeadClearance = 0.6f;

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
