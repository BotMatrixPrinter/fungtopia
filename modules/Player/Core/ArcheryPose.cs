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
    /// point (on the right) in the left hand. Moves at most 1 per <see cref="ArcheryPose.BlendSeconds"/>.
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

    /// <summary>Lowest aim pitch (looking down), radians: steeper, the nock would come up past the right cheek into the head.</summary>
    public const float MinAimPitch = -0.2f;

    /// <summary>Highest aim pitch (looking up), radians.</summary>
    public const float MaxAimPitch = 0.7f;

    /// <summary>
    /// The held bow's string at rest is this far behind the grip, and its tips this far above and
    /// below it, meters (the HandBow in characters.py, 1.3 times the slung bow: arc radius 0.546 over ±0.34π).
    /// </summary>
    public const float StringRest = 0.283f;

    /// <inheritdoc cref="StringRest"/>
    public const float TipHalfSpan = 0.478f;

    /// <summary>The nocked arrow from nock to tip, meters (NockArrow in characters.py).</summary>
    public const float ArrowLength = 0.78f;

    /// <summary>"Never released": larger than any pose time.</summary>
    public const float LongAgo = 1000f;

    /// <summary>The bow hand's place in the full pose: <see cref="BowGeometry.HoldOffset"/> in the model frame.</summary>
    public static Vector3 HoldLocal => new(BowGeometry.HoldRight, BowGeometry.HoldUp, -BowGeometry.HoldForward);

    /// <summary>
    /// Which side of the head's middle the hold point is on: 1 right (as now: the bow on the
    /// right, in the left hand), -1 left. The bow's top tilts and the right hand follows through
    /// to that side, the bow swings over that shoulder, and the head keeps to the other side.
    /// </summary>
    public static float HoldSide => BowGeometry.HoldRight < 0f ? -1f : 1f;

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
    /// swings over the shoulder on the hold point's side (<see cref="HoldSide"/>: the right one).
    /// </summary>
    public static Vector3 BowGrip(float weight, Vector3 backGrip)
    {
        float w = Math.Clamp(weight, 0f, 1f);
        if (w >= 1f)
            return HoldLocal;
        float arc = MathF.Sin(MathF.PI * w);
        return Vector3.Lerp(backGrip, HoldLocal, Eased(w)) + new Vector3(HoldSide * 0.25f, 0.35f, 0f) * arc;
    }

    // ---- The full pose: body, head, arms and bow (the rig as characters.py builds it) ----

    /// <summary>The body's pivot (the hips): it turns and leans about it, the legs stay below.</summary>
    public static readonly Vector3 BodyPivot = new(0f, 0.42f, 0f);

    /// <summary>The head's pivot (the neck) at rest; it rides on the body.</summary>
    public static readonly Vector3 NeckRest = new(0f, 1.07f, 0f);

    /// <summary>The head's middle above the neck (the 0.38 m ball floating over the body).</summary>
    public const float HeadAboveNeck = 0.21f;

    /// <summary>The head's radius.</summary>
    public const float HeadRadius = 0.19f;

    /// <summary>The left shoulder (ArmL's pivot) at rest; the right one is its mirror.</summary>
    public static readonly Vector3 ShoulderLRest = new(-0.06f, 1.06f, 0f);

    /// <summary>
    /// The arms' length, shoulder to hand center (ArmL / ArmR in characters.py: hanging, the
    /// big oval hands float beside the middle of the body). Arms are only ever turned at the shoulder, never stretched.
    /// </summary>
    public const float ArmLength = 0.46f;

    /// <summary>The left arm's direction at rest, shoulder to hand (down and a little out); the right one is its mirror.</summary>
    public static readonly Vector3 ArmLRestDirection = Vector3.Normalize(new Vector3(-0.273f, -0.373f, -0.03f));

    /// <summary>
    /// How far the body turns to the right of the aim in the full pose (the left shoulder coming
    /// forward so the left arm reaches across the chest to the hold point on the right), radians;
    /// the solver keeps it near this, and within <see cref="MaxTurn"/>.
    /// </summary>
    public const float SideOn = 1.1f;

    /// <summary>
    /// The body never turns further than this to the right of the aim, radians: less than a
    /// quarter turn, so the chest always faces the target's side and the back is away from it.
    /// </summary>
    public const float MaxTurn = 1.5f;

    /// <summary>
    /// The hips may slide this far (meters, sideways and forward or back) toward the bow, shifting
    /// the weight so the left arm reaches without leaning far over; the legs stay where they are.
    /// </summary>
    public const float MaxHipShift = 0.2f;

    /// <summary>The torso's back is this far behind its middle (the body's axis), meters: its back plane (the capsule's 0.16 m radius and the slung bow's strap).</summary>
    public const float TorsoBack = 0.17f;

    /// <summary>The held bow's top tilts this far away from the head (radians about its forward), like a cute archer's.</summary>
    public const float BowCant = 0.15f;

    /// <summary>How far each limb of the held bow bends back toward the string at a full draw, radians.</summary>
    public const float MaxLimbFlex = 0.1f;

    /// <summary>Where the head's middle stays sideways while aiming (X, meters): about over the feet, away from the bow.</summary>
    public static float HeadMiddle => -HoldSide * 0.1f;

    /// <summary>The head tilts at most this much on the neck (roll, radians).</summary>
    public const float MaxHeadRoll = 0.5f;

    /// <summary>
    /// Where everything is in the archery pose, in the model frame (meters from the feet, X right,
    /// Y up, -Z the facing). Rotations turn the part from its rest in characters.py (the arms'
    /// turn their rest direction, <see cref="ArmLRestDirection"/>); nothing is scaled.
    /// </summary>
    public struct Frame
    {
        /// <summary>The body's turn about its pivot, <see cref="Hips"/>.</summary>
        public Quaternion Body;
        /// <summary>The body's pivot: <see cref="BodyPivot"/> slid (by at most <see cref="MaxHipShift"/>) toward the bow.</summary>
        public Vector3 Hips;
        /// <summary>The body's turn as <see cref="BodyTurn"/>'s yaw, roll (lean to its left) and pitch (forward).</summary>
        public float Yaw, Roll, Pitch;
        /// <summary>Where the neck (the head's pivot) is, and the head's turn (absolute: it looks along the arrow).</summary>
        public Vector3 Neck;
        public Quaternion Head;
        public Vector3 ShoulderL, ShoulderR;
        /// <summary>The arms' turns about their shoulders.</summary>
        public Quaternion ArmL, ArmR;
        /// <summary>The hand centers: always <see cref="ArmLength"/> from their shoulders.</summary>
        public Vector3 HandL, HandR;
        /// <summary>The bow's grip (<see cref="HoldLocal"/>) and its turn: its Y up the limbs, -Z <see cref="Forward"/>.</summary>
        public Vector3 Grip;
        public Quaternion Bow;
        /// <summary>The arrow's direction, from the hold point toward the target.</summary>
        public Vector3 Forward;
        /// <summary>
        /// The string's visible pull (<see cref="StringPull"/>, shortened when the right arm cannot
        /// reach that far back), the limbs' bend, the nock (the string's middle and the arrow's back).
        /// </summary>
        public float Pull, Flex;
        public Vector3 Nock;
    }

    /// <summary>
    /// The arrow's aim relative to the player (as <see cref="AimAngles"/>): from the hold point
    /// toward <paramref name="target"/> (<see cref="BowState.Target"/>, where the crosshair is and
    /// the Bow module's arrows fly), or along the input's aim when there is no target (zero) or it
    /// is closer than a meter.
    /// </summary>
    public static (float Yaw, float Pitch) ArrowAim(Vector3 feet, float facing, Vector3 target, Vector3 aimDirection, bool hasAim)
    {
        var toTarget = target - BowGeometry.HoldPoint(feet, facing);
        if (target != Vector3.Zero && float.IsFinite(toTarget.LengthSquared()) && toTarget.LengthSquared() > 1f)
            return AimAngles(toTarget, true, facing);
        return AimAngles(aimDirection, hasAim, facing);
    }

    /// <summary>The left (<paramref name="side"/> -1) or right (1) shoulder for the body turned by <paramref name="body"/> about <paramref name="hips"/>.</summary>
    public static Vector3 Shoulder(Quaternion body, Vector3 hips, float side) =>
        hips + Vector3.Transform(new Vector3(-side * ShoulderLRest.X, ShoulderLRest.Y, ShoulderLRest.Z) - BodyPivot, body);

    /// <summary>The body's forward (the chest's facing) for its turn.</summary>
    public static Vector3 Chest(Quaternion body) => Vector3.Transform(-Vector3.UnitZ, body);

    /// <summary>
    /// How far <paramref name="point"/> is in front of the torso's back plane (through its back,
    /// <see cref="TorsoBack"/> behind the hips, square to <see cref="Chest"/>), meters: negative behind the back.
    /// </summary>
    public static float InFrontOfBack(in Frame f, Vector3 point) =>
        Vector3.Dot(point - (f.Hips + Vector3.Transform(new Vector3(0f, 0f, TorsoBack), f.Body)), Chest(f.Body));

    /// <summary>The arm's elbow (it never bends: the middle of the arm) for its shoulder and hand.</summary>
    public static Vector3 Elbow(Vector3 shoulder, Vector3 hand) => (shoulder + hand) * 0.5f;

    /// <summary>The arm's rest direction, shoulder to hand, for the left (-1) or right (1) arm.</summary>
    public static Vector3 ArmRestDirection(float side) => new(-side * ArmLRestDirection.X, ArmLRestDirection.Y, ArmLRestDirection.Z);

    /// <summary>The body's turn for a yaw (left positive), a lean to its own left (roll) and forward (pitch, down positive).</summary>
    public static Quaternion BodyTurn(float yaw, float roll, float pitch) =>
        Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw) * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, roll) *
        Quaternion.CreateFromAxisAngle(Vector3.UnitX, -pitch);

    /// <summary>
    /// The arm's turn at the shoulder that points it at <paramref name="target"/> (the shortest
    /// turn from its rest direction), and where its hand is: <see cref="ArmLength"/> from the
    /// shoulder toward the target, exactly on it when the target is that far.
    /// </summary>
    public static (Quaternion Turn, Vector3 Hand) ReachArm(Vector3 shoulder, float side, Vector3 target)
    {
        var rest = ArmRestDirection(side);
        var to = target - shoulder;
        if (to.LengthSquared() < 1e-8f)
            return (Quaternion.Identity, shoulder + rest * ArmLength);
        to = Vector3.Normalize(to);
        var turn = FromTo(rest, to);
        return (turn, shoulder + Vector3.Transform(rest, turn) * ArmLength);
    }

    /// <summary>The shortest rotation taking unit vector <paramref name="from"/> to unit vector <paramref name="to"/>.</summary>
    public static Quaternion FromTo(Vector3 from, Vector3 to)
    {
        float d = Vector3.Dot(from, to);
        if (d < -0.9999f)
        {
            var axis = Vector3.Cross(from, MathF.Abs(from.X) < 0.9f ? Vector3.UnitX : Vector3.UnitY);
            return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.PI);
        }
        var c = Vector3.Cross(from, to);
        return Quaternion.Normalize(new Quaternion(c, 1f + d));
    }

    /// <summary>The bow's turn: limbs up (world up made square to the arrow), -Z along <paramref name="forward"/>, the top tilted <see cref="BowCant"/> away from the head.</summary>
    public static Quaternion BowTurn(Vector3 forward)
    {
        var z = -forward;
        var x = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, z)); // aim pitch is clamped, never vertical
        var y = Vector3.Cross(z, x);
        var m = new Matrix4x4(x.X, x.Y, x.Z, 0f, y.X, y.Y, y.Z, 0f, z.X, z.Y, z.Z, 0f, 0f, 0f, 0f, 1f);
        // The head is on the side away from HoldSide: tilt the top toward HoldSide.
        return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(m) * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -HoldSide * BowCant));
    }

    /// <summary>
    /// Where the right hand goes: the nock while drawing (pulled back <paramref name="reach"/>
    /// times <see cref="StringPull"/>: less than 1 when the arm cannot reach that far); after a
    /// release it flies back off the string, out to the right and up.
    /// </summary>
    public static Vector3 StringHandTarget(in ArcheryState s, Vector3 grip, Vector3 forward, float reach = 1f)
    {
        float follow = FollowThrough(s);
        float pull = reach * (s.Draw > 0f ? StringPull(s) : s.ReleasedDraw * BowGeometry.FullDrawPull);
        var side = HoldSide * Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        return Nock(grip, forward, pull) + (-forward * 0.08f + side * 0.12f + Vector3.UnitY * 0.1f) * follow;
    }

    /// <summary>
    /// The full archery pose for the state and the arrow's aim (<see cref="ArrowAim"/>): the bow
    /// at the hold point (on the right) along the arrow, the left hand on its grip, the right hand
    /// on the string (or following through), the body turned right of the aim (never more than
    /// <see cref="MaxTurn"/>), leaning and sliding its hips just enough that both arms, turned only
    /// at the shoulders, reach exactly; where the right arm still cannot reach, the visible pull
    /// is shortened instead. The head on the neck looks along the arrow.
    /// </summary>
    public static Frame Solve(in ArcheryState s, float aimYaw, float aimPitch)
    {
        var f = new Frame { Grip = HoldLocal, Forward = AimLocal(aimYaw, aimPitch) };
        f.Bow = BowTurn(f.Forward);

        // Levenberg-Marquardt on (yaw, roll, pitch, hips x, hips z, pull): both arms exactly long
        // enough (the bow hand first), the body near its turn and upright, the hips near their
        // rest, the pull as drawn. Starts from the usual answer.
        float yaw0 = aimYaw - SideOn * (0.6f + 0.4f * Math.Clamp((s.Draw > 0f ? s.Draw : s.ReleasedDraw), 0f, 1f));
        Span<float> p = stackalloc float[Unknowns] { yaw0, 0f, 0.1f, 0f, 0f, 0.5f * BowGeometry.FullDrawPull };
        // First toward a half draw's pose, then from there to this one: nearby states land in the same pose.
        Fit(p, aimYaw, yaw0, f.Grip, f.Forward, 0.5f * BowGeometry.FullDrawPull);
        // After a shot the body keeps the drawn pose; only the right arm swings back off the string.
        float drawn = s.Draw > 0f ? StringPull(s) : s.ReleasedDraw * BowGeometry.FullDrawPull;
        p[5] = Math.Min(p[5], drawn);
        Fit(p, aimYaw, yaw0, f.Grip, f.Forward, drawn);
        float reach = drawn > 1e-5f ? Math.Clamp(p[5] / drawn, 0f, 1f) : 1f;
        f.Pull = StringPull(s) * reach;
        f.Flex = MaxLimbFlex * Math.Clamp(f.Pull / BowGeometry.FullDrawPull, 0f, 1f);
        f.Nock = Nock(f.Grip, f.Forward, f.Pull);
        var right = StringHandTarget(s, f.Grip, f.Forward, reach);
        (f.Yaw, f.Roll, f.Pitch) = (p[0], p[1], p[2]);
        f.Body = BodyTurn(p[0], p[1], p[2]);
        f.Hips = BodyPivot + new Vector3(p[3], 0f, p[4]);
        f.ShoulderL = Shoulder(f.Body, f.Hips, -1f);
        f.ShoulderR = Shoulder(f.Body, f.Hips, 1f);
        (f.ArmL, f.HandL) = ReachArm(f.ShoulderL, -1f, f.Grip);
        (f.ArmR, f.HandR) = ReachArm(f.ShoulderR, 1f, right);
        f.Neck = f.Hips + Vector3.Transform(NeckRest - BodyPivot, f.Body);
        // The head looks along the arrow; it rides on the leaning neck but tilts back over the feet
        // (HeadMiddle), clear of the bow and the string hand.
        float roll = Math.Clamp(MathF.Asin(Math.Clamp((f.Neck.X - HeadMiddle) / HeadAboveNeck, -1f, 1f)), -MaxHeadRoll, MaxHeadRoll);
        f.Head = Quaternion.CreateFromAxisAngle(Vector3.UnitY, aimYaw) *
            Quaternion.CreateFromAxisAngle(Vector3.UnitX, aimPitch * 0.6f) * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, roll);
        return f;
    }

    /// <summary>The solver keeps the body's lean (roll and pitch) within this, radians.</summary>
    public const float MaxLean = 0.6f;

    private const int Unknowns = 6, Residual = 8;

    /// <summary>Levenberg-Marquardt from <paramref name="p"/> (yaw, roll, pitch, hips x, hips z, pull) on <see cref="Residuals"/>.</summary>
    private static void Fit(Span<float> p, float aimYaw, float yaw0, Vector3 grip, Vector3 forward, float pull)
    {
        Span<float> q = stackalloc float[Unknowns];
        Span<float> r = stackalloc float[Residual];
        Span<float> rp = stackalloc float[Residual];
        Span<float> j = stackalloc float[Residual * Unknowns];
        Span<float> a = stackalloc float[Unknowns * (Unknowns + 1)];
        Clamp(p, aimYaw, pull);
        float cost = Residuals(p, yaw0, grip, forward, pull, r), lambda = 1e-3f;
        for (int iteration = 0; iteration < 60 && cost > 1e-9f; iteration++)
        {
            for (int k = 0; k < Unknowns; k++)
            {
                p.CopyTo(q);
                q[k] += 1e-3f;
                Residuals(q, yaw0, grip, forward, pull, rp);
                for (int i = 0; i < Residual; i++)
                    j[i * Unknowns + k] = (rp[i] - r[i]) / 1e-3f;
            }
            // (JᵀJ + λ diag) δ = -Jᵀr, by Gaussian elimination on [A | b].
            const int w = Unknowns + 1;
            for (int row = 0; row < Unknowns; row++)
            {
                for (int col = 0; col < Unknowns; col++)
                {
                    float sum = 0f;
                    for (int i = 0; i < Residual; i++)
                        sum += j[i * Unknowns + row] * j[i * Unknowns + col];
                    a[row * w + col] = sum;
                }
                float b = 0f;
                for (int i = 0; i < Residual; i++)
                    b -= j[i * Unknowns + row] * r[i];
                a[row * w + Unknowns] = b;
                a[row * w + row] += lambda * (a[row * w + row] + 1e-4f);
            }
            if (!SolveLinear(a))
                break;
            for (int k = 0; k < Unknowns; k++)
                q[k] = p[k] + a[k * w + Unknowns];
            Clamp(q, aimYaw, pull);
            float tried = Residuals(q, yaw0, grip, forward, pull, rp);
            if (tried < cost)
            {
                q.CopyTo(p);
                rp.CopyTo(r);
                cost = tried;
                lambda = Math.Max(1e-6f, lambda * 0.3f);
            }
            else
                lambda *= 10f;
        }
    }

    /// <summary>The unknowns within their limits: the turn, the lean, the hips' slide and the pull (up to as drawn).</summary>
    private static void Clamp(Span<float> p, float aimYaw, float pull)
    {
        p[0] = Math.Clamp(p[0], aimYaw - MaxTurn, aimYaw - 0.4f);
        p[1] = Math.Clamp(p[1], -MaxLean, MaxLean);
        p[2] = Math.Clamp(p[2], -MaxLean, MaxLean);
        p[3] = Math.Clamp(p[3], -MaxHipShift, MaxHipShift);
        p[4] = Math.Clamp(p[4], -MaxHipShift, MaxHipShift);
        p[5] = Math.Clamp(p[5], 0f, Math.Max(0f, pull));
    }

    /// <summary>Solves the augmented square system <paramref name="a"/> ([A | b], rows of <see cref="Unknowns"/> + 1) in place: the answer in its last column.</summary>
    private static bool SolveLinear(Span<float> a)
    {
        const int n = Unknowns, w = Unknowns + 1;
        for (int c = 0; c < n; c++)
        {
            int pivot = c;
            for (int row = c + 1; row < n; row++)
                if (MathF.Abs(a[row * w + c]) > MathF.Abs(a[pivot * w + c]))
                    pivot = row;
            if (MathF.Abs(a[pivot * w + c]) < 1e-12f)
                return false;
            if (pivot != c)
                for (int k = 0; k < w; k++)
                    (a[c * w + k], a[pivot * w + k]) = (a[pivot * w + k], a[c * w + k]);
            for (int row = 0; row < n; row++)
            {
                if (row == c)
                    continue;
                float factor = a[row * w + c] / a[c * w + c];
                for (int k = c; k < w; k++)
                    a[row * w + k] -= factor * a[c * w + k];
            }
        }
        for (int row = 0; row < n; row++)
            a[row * w + n] /= a[row * w + row];
        return true;
    }

    private static float Residuals(ReadOnlySpan<float> p, float yaw0, Vector3 grip, Vector3 forward, float pull, Span<float> r)
    {
        var body = BodyTurn(p[0], p[1], p[2]);
        var hips = BodyPivot + new Vector3(p[3], 0f, p[4]);
        r[0] = 6f * (Vector3.Distance(Shoulder(body, hips, -1f), grip) - ArmLength); // the bow hand first
        r[1] = 5f * (Vector3.Distance(Shoulder(body, hips, 1f), Nock(grip, forward, p[5])) - ArmLength);
        r[2] = 0.04f * (p[0] - yaw0);
        r[3] = 0.4f * p[1];
        r[4] = 0.4f * p[2];
        r[5] = 0.15f * p[3];
        r[6] = 0.6f * p[4];
        r[7] = 1f * (p[5] - pull); // shorten the pull only when the right arm cannot reach
        float cost = 0f;
        for (int i = 0; i < Residual; i++)
            cost += r[i] * r[i];
        return cost;
    }

    /// <summary>The head's middle for a frame (on the neck, turned with the head).</summary>
    public static Vector3 HeadCenter(in Frame f) => f.Neck + Vector3.Transform(new Vector3(0f, HeadAboveNeck, 0f), f.Head);

    /// <summary>
    /// Where the arrow's nock and the string's middle are, for the arrow's direction
    /// <paramref name="forward"/> and a pull back of <paramref name="pull"/> meters.
    /// </summary>
    public static Vector3 Nock(Vector3 grip, Vector3 forward, float pull) => grip - forward * (StringRest + pull);

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
