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
/// the chest) at about chest height of the 1.80 m character, right of and below its round head
/// (0.27 m wide and 0.30 m tall, its center <see cref="HeadUp"/> above the feet), more than
/// <see cref="MinHeadClearance"/> from the head's center, so the over-the-right-shoulder aim
/// camera sees it; the right hand draws the string back toward the right cheek.
/// </summary>
public static class BowGeometry
{
    /// <summary>The bow hand is this far to the character's right of its feet, meters (positive: on the right).</summary>
    public const float HoldRight = 0.40f;

    /// <summary>The bow hand is this high above the feet (about chest height, below the head), meters.</summary>
    public const float HoldUp = 1.30f;

    /// <summary>The bow hand is this far ahead of the feet along the facing, meters.</summary>
    public const float HoldForward = 0.55f;

    /// <summary>The head's center is about this high above the feet (over them), meters.</summary>
    public const float HeadUp = 1.64f;

    /// <summary>The head's radius, about (it is 0.27 m wide and 0.30 m tall), meters.</summary>
    public const float HeadRadius = 0.15f;

    /// <summary>
    /// The hold point is at least this far from the head's center (it is about 0.76 m), meters:
    /// the head's radius and the held bow's half width with room to spare.
    /// </summary>
    public const float MinHeadClearance = 0.45f;

    /// <summary>
    /// How far the string (and the nocked arrow's back) comes back from the bow toward the
    /// character at a full draw (<see cref="BowState.Draw"/> 1), meters; a draw of d pulls d times this.
    /// </summary>
    public const float FullDrawPull = 0.36f;

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
