using System;

namespace Fungtopia.Townsfolk;

/// <summary>How a walking townsperson's legs are posed (radians; positive swings a leg forward, a negative knee bends it back).</summary>
public readonly struct Gait
{
    public readonly float HipL, KneeL, HipR, KneeR;
    /// <summary>How far the body is lowered (model meters) so the lower foot stays on the ground as the legs spread.</summary>
    public readonly float Drop;

    public Gait(float hipL, float kneeL, float hipR, float kneeR, float drop)
    {
        HipL = hipL;
        KneeL = kneeL;
        HipR = hipR;
        KneeR = kneeR;
        Drop = drop;
    }
}

/// <summary>How a townsperson sits on a Pet City bench.</summary>
public readonly struct SitPose
{
    /// <summary>The model's height above the ground (world meters, negative: the hips come down to the seat).</summary>
    public readonly float Lift;
    /// <summary>How far back of the seat spot the hips are (world meters), so the back is near the backrest.</summary>
    public readonly float Back;
    /// <summary>The thighs' turn forward at the hips and the shins' back at the knees (radians, the knee negative).</summary>
    public readonly float Hip, Knee;

    public SitPose(float lift, float back, float hip, float knee)
    {
        Lift = lift;
        Back = back;
        Hip = hip;
        Knee = knee;
    }
}

/// <summary>
/// The townsfolk's body (folk.glb, built by Models/townsfolk.py with these numbers) for a 1.80 m
/// character, feet at 0, and how it walks, sits and looks: a character of another height is the same
/// rig scaled by <c>Height / 1.80</c> (<see cref="Look.Scale"/>). Model meters, Godot's frame.
/// </summary>
public static class Body
{
    /// <summary>The rig's height (top of the head).</summary>
    public const float Height = 1.8f;
    /// <summary>The hip pivots (LegL / LegR at x -/+<see cref="HipX"/>), also the Body's pivot.</summary>
    public const float HipY = 0.9f, HipX = 0.1f;
    /// <summary>The knee pivots (ShinL / ShinR).</summary>
    public const float KneeY = 0.48f;
    /// <summary>The legs' tube radius.</summary>
    public const float LegRadius = 0.065f;
    /// <summary>The shoulder pivots (ArmL / ArmR at x -/+<see cref="ShoulderX"/>) and the invisible arms' length to the hands.</summary>
    public const float ShoulderY = 1.4f, ShoulderX = 0.22f, ArmLength = 0.66f;
    /// <summary>The Head's pivot (the neck) and the middle of the head.</summary>
    public const float NeckY = 1.48f, HeadY = 1.64f;
    /// <summary>The top of a Pet City bench seat (PetCity's bench model: its seat board's top).</summary>
    public const float SeatHeight = 0.52f;

    /// <summary>Thigh (hip to knee) and shin (knee to sole) lengths.</summary>
    public const float Thigh = HipY - KneeY, Shin = KneeY;
    /// <summary>How far forward a foot reaches from under the hip at the end of a stride (model meters).</summary>
    public const float StrideReach = 0.3f;
    /// <summary>How much a swinging knee bends at mid-swing (radians).</summary>
    public const float SwingKnee = 0.95f;
    /// <summary>The walking speed at and over which the full stride is taken (slower, shorter steps).</summary>
    public const float FullStrideSpeed = 0.9f;

    /// <summary>How much of the full stride is taken at <paramref name="speed"/> (0 standing, 1 walking).</summary>
    public static float Walk(float speed) => Math.Clamp(speed / FullStrideSpeed, 0f, 1f);

    /// <summary>
    /// How fast the walk cycle turns (cycles a second, one cycle = two steps) at <paramref name="speed"/>
    /// (m/s) for a character drawn <paramref name="scale"/> times the rig: the planted foot moves back
    /// under the hip exactly as fast as the body goes forward, so it does not slide.
    /// </summary>
    public static float CycleRate(float speed, float scale)
    {
        float reach = StrideReach * MathF.Max(Walk(speed), 0.2f) * scale;
        // A foot goes from reach in front to reach behind in half a cycle.
        return speed / (4f * reach);
    }

    /// <summary>The leg pose at <paramref name="cycle"/> (cycles; the right leg half a cycle behind the left) and <paramref name="speed"/>.</summary>
    public static Gait Legs(float cycle, float speed)
    {
        float walk = Walk(speed);
        Leg(cycle, walk, out float hipL, out float kneeL);
        Leg(cycle + 0.5f, walk, out float hipR, out float kneeR);
        float reach = MathF.Max(Reach(hipL, kneeL), Reach(hipR, kneeR));
        return new Gait(hipL, kneeL, hipR, kneeR, HipY - reach);
    }

    /// <summary>One leg: on the ground for the first half of the cycle (straight, its foot moving back at an even pace), swinging forward with a bent knee for the second.</summary>
    private static void Leg(float cycle, float walk, out float hip, out float knee)
    {
        float u = cycle - MathF.Floor(cycle);
        float a = StrideReach * walk;
        float forward;
        if (u < 0.5f)
        {
            forward = a * (1f - 4f * u);
            knee = 0f;
        }
        else
        {
            float w = (u - 0.5f) * 2f;
            forward = -a * MathF.Cos(MathF.PI * w);
            // The knee is straight again a little before the foot comes down in front.
            knee = -SwingKnee * walk * MathF.Sin(MathF.PI * MathF.Min(w / 0.85f, 1f));
        }
        hip = MathF.Asin(forward / HipY);
    }

    /// <summary>How far down from the hip a leg posed so reaches (model meters).</summary>
    public static float Reach(float hip, float knee) => Thigh * MathF.Cos(hip) + Shin * MathF.Cos(hip + knee);

    /// <summary>Where the sole of a leg posed so is, from under its hip: (forward, height above the ground) in model meters, the body lowered by <paramref name="drop"/>.</summary>
    public static (float Forward, float Height) Foot(float hip, float knee, float drop) =>
        (Thigh * MathF.Sin(hip) + Shin * MathF.Sin(hip + knee), HipY - drop - Reach(hip, knee));

    /// <summary>
    /// Sitting on a bench (seat top <see cref="SeatHeight"/>) at <paramref name="scale"/>: the hips
    /// come down onto the seat (the thighs' tubes resting on it), the thighs forward over its front,
    /// a little down for those whose shins are short of the ground, and the shins down to it, the
    /// knees bent so the feet stand on the ground in front.
    /// </summary>
    public static SitPose Sit(float scale)
    {
        float hipHeight = SeatHeight + LegRadius * scale;
        float shin = Shin * scale, thigh = Thigh * scale;
        // The thighs tip down so the knees are low enough for the shins to reach the ground...
        float down = shin < hipHeight ? MathF.Asin(MathF.Min((hipHeight - shin) / thigh, 0.6f)) : 0f;
        float kneeHeight = hipHeight - thigh * MathF.Sin(down);
        // ...and the shins tip forward when they are longer than that.
        float shinTip = shin > kneeHeight ? MathF.Acos(kneeHeight / shin) : 0f;
        float hip = MathF.PI / 2f - down;
        float knee = -(hip - shinTip);
        return new SitPose(hipHeight - HipY * scale, 0.14f * scale, hip, knee);
    }

    /// <summary>
    /// How far to tip the head up (radians, negative is down) to look a partner
    /// <paramref name="distance"/> meters away in the eyes, both drawn at their own scale.
    /// </summary>
    public static float LookPitch(float scale, float otherScale, float distance) =>
        MathF.Atan2(HeadY * (otherScale - scale), MathF.Max(distance, 0.3f));
}
