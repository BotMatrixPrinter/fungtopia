using System;
using System.Numerics;

namespace Fungtopia.Townsfolk;

/// <summary>The angles of a townsperson's two tube legs (radians about the side axis): the thigh
/// (LegL / LegR, at the hip) and the shin (ShinL / ShinR, at the knee, relative to the thigh).
/// A positive hip angle swings the leg forward; a negative knee angle bends the shin back.</summary>
public readonly struct LegPose
{
    public readonly float HipL, KneeL, HipR, KneeR;

    public LegPose(float hipL, float kneeL, float hipR, float kneeR)
    {
        HipL = hipL;
        KneeL = kneeL;
        HipR = hipR;
        KneeR = kneeR;
    }
}

/// <summary>
/// folk.glb's leg rig (as townsfolk.py builds it, at <see cref="Look.ModelHeight"/>) and how the
/// view poses it: walking swings the legs from the hip (the knee bending as a leg swings forward),
/// sitting puts the body on the bench seat with the thighs forward along it and the shins bent
/// down over its front edge, swinging a little.
/// </summary>
public static class Stride
{
    /// <summary>The hips (LegL / LegR pivots), just inside the bottom of the body.</summary>
    public const float HipHeight = 0.42f;
    /// <summary>The knees (ShinL / ShinR pivots).</summary>
    public const float KneeHeight = 0.22f;
    /// <summary>The ankle (the middle of the rounded foot's tube end).</summary>
    public const float AnkleHeight = 0.07f;
    /// <summary>The bottom of the body (the dress's hem, the capsule's bottom).</summary>
    public const float BodyBottom = 0.37f;
    /// <summary>The top of a Pet City bench's seat (meters above the ground).</summary>
    public const float SeatTop = 0.5f;
    /// <summary>A bench's seat edge, in front of a <see cref="CityLife.Seats"/> spot (meters).</summary>
    public const float SeatEdge = 0.13f;

    /// <summary>The legs walking: <paramref name="phase"/> runs a stride every 2π, <paramref name="walk"/> is 0 standing, 1 at an easy walk.</summary>
    public static LegPose Walk(float phase, float walk)
    {
        float s = MathF.Sin(phase), c = MathF.Cos(phase);
        float swing = 0.5f * walk;
        return new LegPose(s * swing, -MathF.Max(0f, c) * 0.7f * walk, -s * swing, -MathF.Max(0f, -c) * 0.7f * walk);
    }

    /// <summary>The legs sitting on a bench at time <paramref name="t"/> (seconds): thighs forward, shins down, swinging.</summary>
    public static LegPose Sit(float t)
    {
        const float Forward = MathF.PI / 2f;
        return new LegPose(Forward, -Forward + MathF.Sin(t * 2.4f) * 0.25f, Forward, -Forward - MathF.Sin(t * 2.4f + 0.7f) * 0.25f);
    }

    /// <summary>How far up (or down) the figure is moved to sit on a bench seat at <paramref name="scale"/>: its body's bottom on the seat.</summary>
    public static float SitLift(float scale) => SeatTop - BodyBottom * scale;

    /// <summary>
    /// Where a posed leg's knee and ankle are, relative to the figure's feet point (Godot's frame:
    /// Y up, -Z forward), for a leg at <paramref name="scale"/> lifted by <paramref name="lift"/>.
    /// </summary>
    public static (Vector3 Knee, Vector3 Ankle) Joints(float hip, float knee, float scale, float lift = 0f)
    {
        var hipAt = new Vector3(0f, HipHeight * scale + lift, 0f);
        float thigh = (HipHeight - KneeHeight) * scale, shin = (KneeHeight - AnkleHeight) * scale;
        // Down the leg, turned forward (toward -Z) by the angle.
        static Vector3 Down(float a) => new(0f, -MathF.Cos(a), -MathF.Sin(a));
        var kneeAt = hipAt + Down(hip) * thigh;
        return (kneeAt, kneeAt + Down(hip + knee) * shin);
    }
}
