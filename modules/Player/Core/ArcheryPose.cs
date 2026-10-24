using System;
using System.Numerics;
using Fungtopia.Bow;

namespace Fungtopia.Player;

/// <summary>
/// Where the character is in its archery pose, advanced once per tick from the Bow module's
/// <see cref="BowState.Draw"/> by <see cref="ArcheryPose.Advance"/>. Plain data for the view.
/// </summary>
public struct ArcheryState
{
    /// <summary>The latest draw, 0 (relaxed) to 1 (full).</summary>
    public float Draw;

    /// <summary>
    /// How far the archery pose is blended in: 0 is the bow on the back, 1 the bow at the hold
    /// point in the left hand. Moves at most 1 per <see cref="ArcheryPose.BlendSeconds"/>.
    /// </summary>
    public float Weight;

    /// <summary>Seconds since the last release (the draw dropping to 0); large before the first.</summary>
    public float SecondsSinceRelease;

    /// <summary>The draw just before the last release (how far the string snaps).</summary>
    public float ReleasedDraw;

    /// <summary>Seconds since the draw reached 1, or -1 while it is not full.</summary>
    public float SecondsAtFullDraw;

    /// <summary>The relaxed state: the bow on the back, nothing shot yet.</summary>
    public static ArcheryState Relaxed => new()
    {
        SecondsSinceRelease = ArcheryPose.LongAgo,
        SecondsAtFullDraw = -1f,
    };
}

/// <summary>
/// The archery pose as pure functions, for <c>CharacterModel</c> to draw and the tests to check.
/// Positions are in the model frame: meters from the feet, X right, Y up, -Z forward (Godot's
/// convention for a model turned to the player's facing, so <see cref="ToWorld"/> places them).
/// </summary>
public static class ArcheryPose
{
    /// <summary>Seconds to blend between poses (back to hand and back again).</summary>
    public const float BlendSeconds = 0.15f;

    /// <summary>After a release the bow stays in hand this long without drawing, seconds.</summary>
    public const float HoldAfterRelease = 0.6f;

    /// <summary>The right hand follows through to its end this many seconds after a release.</summary>
    public const float FollowThroughSeconds = 0.12f;

    /// <summary>The little squash at full draw lasts this long, seconds.</summary>
    public const float FullDrawSquashSeconds = 0.25f;

    /// <summary>Aim yaw is clamped to this much either side of the facing (the feet turn the rest), radians.</summary>
    public const float MaxAimYaw = 0.7f;

    /// <summary>Lowest aim pitch (looking down), radians.</summary>
    public const float MinAimPitch = -0.6f;

    /// <summary>Highest aim pitch (looking up), radians.</summary>
    public const float MaxAimPitch = 0.7f;

    /// <summary>
    /// The held bow's string at rest is this far behind the grip, and its tips this far above and
    /// below it, meters (the HandBow in characters.py: arc radius 0.42 over ±0.34π).
    /// </summary>
    public const float StringRest = 0.218f;

    /// <inheritdoc cref="StringRest"/>
    public const float TipHalfSpan = 0.368f;

    /// <summary>
    /// The held bow points this much left of the aim (radians about the up axis), so the string
    /// hand comes back beside the big chibi head instead of into it (the hold point is at face
    /// height). The Bow module's arrows still fly along the aim.
    /// </summary>
    public const float BowSkew = 0.35f;

    /// <summary>The nocked arrow from nock to tip, meters (NockArrow in characters.py).</summary>
    public const float ArrowLength = 0.66f;

    /// <summary>"Never released": larger than any pose time.</summary>
    public const float LongAgo = 1000f;

    /// <summary>The bow hand's place in the full pose: <see cref="BowGeometry.HoldOffset"/> in the model frame.</summary>
    public static Vector3 HoldLocal => new(BowGeometry.HoldRight, BowGeometry.HoldUp, -BowGeometry.HoldForward);

    /// <summary>
    /// The state one tick of <paramref name="dt"/> seconds later, with the bow drawn
    /// <paramref name="draw"/> (0..1, <see cref="BowState.Draw"/>) on this tick.
    /// </summary>
    public static ArcheryState Advance(ArcheryState s, float draw, float dt)
    {
        draw = float.IsFinite(draw) ? Math.Clamp(draw, 0f, 1f) : 0f;
        dt = float.IsFinite(dt) ? Math.Max(0f, dt) : 0f;
        if (draw <= 0f && s.Draw > 0f)
        {
            s.ReleasedDraw = s.Draw;
            s.SecondsSinceRelease = 0f;
        }
        else if (draw <= 0f)
        {
            s.SecondsSinceRelease = Math.Min(LongAgo, s.SecondsSinceRelease + dt);
        }
        s.SecondsAtFullDraw = draw >= 1f ? (s.SecondsAtFullDraw < 0f ? 0f : s.SecondsAtFullDraw + dt) : -1f;
        s.Draw = draw;
        float target = TargetWeight(draw, s.SecondsSinceRelease);
        float step = dt / BlendSeconds;
        s.Weight = Math.Clamp(s.Weight + Math.Clamp(target - s.Weight, -step, step), 0f, 1f);
        return s;
    }

    /// <summary>The pose to blend toward: 1 (bow in hand) while drawing and for a while after a release, else 0.</summary>
    public static float TargetWeight(float draw, float secondsSinceRelease) =>
        draw > 0f || secondsSinceRelease < HoldAfterRelease ? 1f : 0f;

    /// <summary>The weight eased in and out, so a blend starts and ends gently.</summary>
    public static float Eased(float weight)
    {
        float w = Math.Clamp(weight, 0f, 1f);
        return w * w * (3f - 2f * w);
    }

    /// <summary>The nocked arrow shows on the string only while drawing; once shot the Bow module draws it.</summary>
    public static bool ArrowVisible(in ArcheryState s) => s.Draw > 0f && s.Weight > 0f;

    /// <summary>The bow in the hand (true) or on the back (false).</summary>
    public static bool BowInHand(in ArcheryState s) => s.Weight > 0f;

    /// <summary>
    /// How far the string is pulled back from rest, meters: <see cref="BowGeometry.FullDrawPull"/>
    /// × draw while drawing; after a release it snaps forward, overshoots a little and settles.
    /// </summary>
    public static float StringPull(in ArcheryState s)
    {
        if (s.Draw > 0f)
            return s.Draw * BowGeometry.FullDrawPull;
        float t = s.SecondsSinceRelease;
        if (t >= 0.3f)
            return 0f;
        return s.ReleasedDraw * BowGeometry.FullDrawPull * MathF.Exp(-30f * t) * MathF.Cos(70f * t);
    }

    /// <summary>0..1: the right hand's follow-through after a release (0 while drawing).</summary>
    public static float FollowThrough(in ArcheryState s)
    {
        if (s.Draw > 0f || s.ReleasedDraw <= 0f)
            return 0f;
        return Eased(s.SecondsSinceRelease / FollowThroughSeconds);
    }

    /// <summary>0..1: a short squash pulse as the draw reaches full.</summary>
    public static float FullDrawSquash(in ArcheryState s)
    {
        float t = s.SecondsAtFullDraw;
        if (t < 0f || t >= FullDrawSquashSeconds)
            return 0f;
        return MathF.Sin(MathF.PI * t / FullDrawSquashSeconds);
    }

    /// <summary>
    /// The aim relative to the player: yaw from the facing (positive to the left) within
    /// ±<see cref="MaxAimYaw"/>, and pitch (positive up) within
    /// [<see cref="MinAimPitch"/>, <see cref="MaxAimPitch"/>]. Straight ahead without an aim.
    /// </summary>
    public static (float Yaw, float Pitch) AimAngles(Vector3 aimDirection, bool hasAim, float facing)
    {
        if (!hasAim || !float.IsFinite(aimDirection.X) || !float.IsFinite(aimDirection.Y) ||
            !float.IsFinite(aimDirection.Z) || !float.IsFinite(facing))
            return (0f, 0f);
        float horizontal = MathF.Sqrt(aimDirection.X * aimDirection.X + aimDirection.Z * aimDirection.Z);
        if (horizontal < 1e-5f && MathF.Abs(aimDirection.Y) < 1e-5f)
            return (0f, 0f);
        float yaw = horizontal < 1e-5f ? 0f : PlayerModule.WrapAngle(MathF.Atan2(-aimDirection.X, -aimDirection.Z) - facing);
        float pitch = MathF.Atan2(aimDirection.Y, horizontal);
        return (Math.Clamp(yaw, -MaxAimYaw, MaxAimYaw), Math.Clamp(pitch, MinAimPitch, MaxAimPitch));
    }

    /// <summary>The unit aim direction in the model frame for <see cref="AimAngles"/>' yaw and pitch.</summary>
    public static Vector3 AimLocal(float yaw, float pitch)
    {
        float cp = MathF.Cos(pitch);
        return new Vector3(-MathF.Sin(yaw) * cp, MathF.Sin(pitch), -MathF.Cos(yaw) * cp);
    }

    /// <summary>
    /// Where the bow's grip (and the left hand on it) is: at <paramref name="weight"/> 0 on the
    /// back (<paramref name="backGrip"/>), at 1 exactly <see cref="HoldLocal"/>; in between it
    /// swings over the left shoulder.
    /// </summary>
    public static Vector3 BowGrip(float weight, Vector3 backGrip)
    {
        float w = Math.Clamp(weight, 0f, 1f);
        if (w >= 1f)
            return HoldLocal;
        float arc = MathF.Sin(MathF.PI * w);
        return Vector3.Lerp(backGrip, HoldLocal, Eased(w)) + new Vector3(-0.25f, 0.2f, 0f) * arc;
    }

    /// <summary>The held bow's forward (where its nocked arrow points): the aim turned <see cref="BowSkew"/> to the left.</summary>
    public static Vector3 BowForward(Vector3 aimLocal)
    {
        float s = MathF.Sin(BowSkew), c = MathF.Cos(BowSkew);
        return Vector3.Normalize(new Vector3(aimLocal.X * c + aimLocal.Z * s, aimLocal.Y, -aimLocal.X * s + aimLocal.Z * c));
    }

    /// <summary>
    /// Where the arrow's nock and the string's middle are, for the bow's forward
    /// (<see cref="BowForward"/>) and a pull back of <paramref name="pull"/> meters.
    /// </summary>
    public static Vector3 Nock(Vector3 grip, Vector3 bowForward, float pull) => grip - bowForward * (StringRest + pull);

    /// <summary>
    /// Where the right hand is in the full pose: on the string at the nock while drawing; after a
    /// release it follows through, back and out to the right.
    /// </summary>
    public static Vector3 RightHand(in ArcheryState s, Vector3 grip, Vector3 bowForward)
    {
        float follow = FollowThrough(s);
        float pull = s.Draw > 0f ? StringPull(s) : s.ReleasedDraw * BowGeometry.FullDrawPull;
        var side = Vector3.Normalize(Vector3.Cross(bowForward, Vector3.UnitY)); // aim pitch is clamped, never vertical
        return Nock(grip, bowForward, pull) - bowForward * (0.12f * follow) + side * (0.08f * follow);
    }

    /// <summary>
    /// A point of the model frame in the world, for a player whose feet are at
    /// <paramref name="feet"/> facing <paramref name="facing"/> (how the view turns the model).
    /// </summary>
    public static Vector3 ToWorld(Vector3 feet, float facing, Vector3 local)
    {
        float s = MathF.Sin(facing), c = MathF.Cos(facing);
        return feet + new Vector3(local.X * c + local.Z * s, local.Y, -local.X * s + local.Z * c);
    }
}
