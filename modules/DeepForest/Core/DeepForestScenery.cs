using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Map;

namespace Fungtopia.DeepForest;

/// <summary>The kinds of things the Deep Forest view draws, one MultiMesh each.</summary>
public enum PropKind
{
    /// <summary>A giant mushroom's stem, on a Map obstacle: the circle is the stem's foot.</summary>
    GiantStem,
    /// <summary>A giant mushroom's main cap, on top of its stem.</summary>
    GiantCap,
    /// <summary>The root flare at a giant stem's foot: a short skirt that widens into the ground (the Skirt mesh).</summary>
    Skirt,
    /// <summary>A side stem growing out of a giant mushroom's stem, leaning out into the canopy.</summary>
    BranchStem,
    /// <summary>The cap at the end of a side stem: the lower layer of the canopy.</summary>
    BranchCap,
    /// <summary>A bracket fungus shelf on a giant stem.</summary>
    Shelf,
    /// <summary>A spider web under a giant cap or between its roots (the tarantulas live here).</summary>
    Web,
    /// <summary>A big arching root from the foot of a giant stem.</summary>
    Root,
    /// <summary>The stem of a medium mushroom: one of the Map's Deep Forest MediumMushroom props (solid; the Stalk mesh).</summary>
    MediumStem,
    /// <summary>The cap (or whole stemless body: puffball, coral) of a medium mushroom, its mesh by <see cref="Prop.Shape"/>.</summary>
    MediumCap,
    /// <summary>The stem of a small ground fungus under <see cref="DeepForestScenery.SmallMaxHeight"/>, scattered freely (the Stalk mesh).</summary>
    SmallStem,
    /// <summary>The cap (or whole stemless body: puffball, cup, coral, jelly) of a small ground fungus, its mesh by <see cref="Prop.Shape"/>.</summary>
    SmallCap,
    /// <summary>A dark fern: the undergrowth, thickest along the overgrown path.</summary>
    Fern,
    /// <summary>A glowing spore drifting in the air (a billboard).</summary>
    Spore,
    /// <summary>A soft puff of low mist (a billboard; fog that also shows without volumetric fog).</summary>
    Mist,
}

/// <summary>The kinds of mushrooms and fungi (cartoon-simplified, cute, never with eyes or faces).</summary>
public enum Fungus
{
    /// <summary>Not a fungus (stems of the giants' old bark, roots, ferns, webs, air).</summary>
    None,
    /// <summary>A round red dome with crisp white spots.</summary>
    FlyAgaric,
    /// <summary>A fat brown bolete/porcini: a plump dome on a stout stem.</summary>
    Bolete,
    /// <summary>A golden funnel-shaped chanterelle.</summary>
    Chanterelle,
    /// <summary>A tall thin parasol with a wide flat, scaly cap.</summary>
    Parasol,
    /// <summary>A shaggy ink cap: a tall bell, inky toward its rim.</summary>
    InkCap,
    /// <summary>A morel: a honeycomb cone.</summary>
    Morel,
    /// <summary>A cluster of tall thin enoki-like stems with tiny caps.</summary>
    Enoki,
    /// <summary>A purple amethyst cap with a wavy rim.</summary>
    Amethyst,
    /// <summary>A round puffball, no stem.</summary>
    Puffball,
    /// <summary>A shelf/bracket fungus on a big stem.</summary>
    Bracket,
    /// <summary>A scarlet cup fungus.</summary>
    ScarletCup,
    /// <summary>A branching pink/orange coral fungus.</summary>
    Coral,
    /// <summary>A soft, glossy jelly fungus.</summary>
    Jelly,
    /// <summary>A bioluminescent blue/green/violet mushroom that clearly glows at night.</summary>
    Glowing,
}

/// <summary>The cap meshes of Models/deep_forest.glb (the giants' caps use Wavy, Dome, Flat, Funnel or Bell).</summary>
public enum CapShape
{
    /// <summary>The broad old cap with a wavy, drooping rim (the Cap mesh).</summary>
    Wavy,
    Dome,
    Flat,
    Funnel,
    Bell,
    Cone,
    Ball,
    Cup,
    Coral,
    Jelly,
}

/// <summary>The pattern a cap's shader draws on top of its color.</summary>
public enum CapPattern
{
    /// <summary>Plain, or crisp white spots when <see cref="Prop.Spots"/> is above 0.</summary>
    Spots,
    /// <summary>A parasol's darker scales.</summary>
    Scales,
    /// <summary>A morel's honeycomb pits.</summary>
    Honeycomb,
    /// <summary>An ink cap's shaggy, inky lower half.</summary>
    Ink,
}

/// <summary>
/// One thing to draw: placed at <see cref="Position"/> (meters, System.Numerics, Y up), turned by
/// <see cref="Rotation"/>, scaled by <see cref="Scale"/> (the unit models of Models/deep_forest.glb).
/// </summary>
public readonly struct Prop
{
    public readonly PropKind Kind;
    public readonly Vector3 Position;
    public readonly Quaternion Rotation;
    public readonly Vector3 Scale;
    /// <summary>The color (linear-ish RGB as drawn, 0..1).</summary>
    public readonly Vector3 Color;
    /// <summary>A fixed value in [0, 1) for shader variation (spot pattern, drift phase).</summary>
    public readonly float Seed;
    /// <summary>How much it glows (0 none).</summary>
    public readonly float Glow;
    /// <summary>The index in <see cref="Ground.Obstacles"/> of the giant mushroom it grows on, or of the medium mushroom's circle; -1 for none.</summary>
    public readonly int Owner;
    /// <summary>What kind of fungus it is (<see cref="Fungus.None"/> for the giants' bark, roots, plants and air).</summary>
    public readonly Fungus Species;
    /// <summary>For caps: which cap mesh draws it.</summary>
    public readonly CapShape Shape;
    /// <summary>For caps: how many crisp white spots (0 none, up to 1).</summary>
    public readonly float Spots;
    /// <summary>For caps: the pattern on top.</summary>
    public readonly CapPattern Pattern;

    public Prop(PropKind kind, Vector3 position, Quaternion rotation, Vector3 scale, Vector3 color, float seed, float glow = 0f, int owner = -1,
        Fungus species = Fungus.None, CapShape shape = CapShape.Wavy, float spots = 0f, CapPattern pattern = CapPattern.Spots)
    {
        Species = species;
        Shape = shape;
        Spots = spots;
        Pattern = pattern;
        Kind = kind;
        Position = position;
        Rotation = rotation;
        Scale = scale;
        Color = color;
        Seed = seed;
        Glow = glow;
        Owner = owner;
    }
}

/// <summary>A FogVolume: a box or an ellipsoid of fog (meters).</summary>
public readonly struct FogPatch
{
    public readonly Vector3 Center;
    public readonly Vector3 Size;
    public readonly float Density;
    public readonly bool Ellipsoid;

    public FogPatch(Vector3 center, Vector3 size, float density, bool ellipsoid)
    {
        Center = center;
        Size = size;
        Density = density;
        Ellipsoid = ellipsoid;
    }
}

/// <summary>A small light over a cluster of glowing mushrooms.</summary>
public readonly struct GlowLight
{
    public readonly Vector3 Position;
    public readonly Vector3 Color;
    public readonly float Range;

    public GlowLight(Vector3 position, Vector3 color, float range)
    {
        Position = position;
        Color = color;
        Range = range;
    }
}

/// <summary>
/// Everything the Deep Forest view draws, computed once from the Map's Contract: a huge, old giant
/// mushroom on every Deep Forest obstacle (<see cref="Ground.Obstacles"/>, the last
/// <see cref="Ground.DeepForestTrunkCount"/>) with side stems and caps that close the canopy, shelves,
/// roots and webs on them; glowing mushrooms, toadstools, ferns, spores and mist on the open ground;
/// fog volumes and a few soft lights. Every prop stands inside the Deep Forest or its path
/// (<see cref="DeepForestGloom.InArea"/>). Deterministic: no random seed, no clock.
/// </summary>
public sealed class DeepForestScenery
{
    /// <summary>Glowing mushrooms, toadstools and ferns keep this far (meters) from the giant stems.</summary>
    public const float StemClearance = 0.3f;

    /// <summary>The middle of the overgrown path stays this clear (meters each side of z = 0) of undergrowth taller than moss.</summary>
    public const float PathClearHalfWidth = 1.4f;

    // Vivid caps that pop in the gloom (the game's "vivid mushroom" rule): deep jewel colors per
    // kind, clearly saturated, never brownish-dull or washed out. Ink caps and puffballs are the
    // pale, pearly ones.
    private static readonly Vector3[][] s_colors = BuildColors();

    private static Vector3[][] BuildColors()
    {
        var c = new Vector3[Enum.GetValues<Fungus>().Length][];
        c[(int)Fungus.None] = new Vector3[] { new(0.5f, 0.5f, 0.5f) };
        c[(int)Fungus.FlyAgaric] = new Vector3[] { new(0.92f, 0.05f, 0.07f), new(0.84f, 0.03f, 0.1f), new(1f, 0.12f, 0.05f) };
        c[(int)Fungus.Bolete] = new Vector3[] { new(0.66f, 0.27f, 0.07f), new(0.72f, 0.34f, 0.09f), new(0.58f, 0.2f, 0.08f) };
        c[(int)Fungus.Chanterelle] = new Vector3[] { new(1f, 0.66f, 0.06f), new(0.98f, 0.54f, 0.04f), new(1f, 0.76f, 0.14f) };
        c[(int)Fungus.Parasol] = new Vector3[] { new(0.95f, 0.5f, 0.28f), new(0.66f, 0.18f, 0.58f), new(0.9f, 0.36f, 0.36f) };
        c[(int)Fungus.InkCap] = new Vector3[] { new(0.88f, 0.8f, 1f), new(0.95f, 0.86f, 1f) };
        c[(int)Fungus.Morel] = new Vector3[] { new(0.86f, 0.5f, 0.14f), new(0.76f, 0.42f, 0.1f), new(0.92f, 0.62f, 0.2f) };
        c[(int)Fungus.Enoki] = new Vector3[] { new(1f, 0.48f, 0.8f), new(1f, 0.68f, 0.42f), new(0.36f, 1f, 0.78f) };
        c[(int)Fungus.Amethyst] = new Vector3[] { new(0.55f, 0.1f, 0.9f), new(0.7f, 0.18f, 1f), new(0.44f, 0.07f, 0.76f), new(0.9f, 0.1f, 0.7f) };
        c[(int)Fungus.Puffball] = new Vector3[] { new(0.96f, 0.88f, 1f), new(1f, 0.93f, 0.86f) };
        c[(int)Fungus.Bracket] = new Vector3[] { new(0.9f, 0.42f, 0.06f), new(0.08f, 0.62f, 0.66f), new(0.86f, 0.14f, 0.56f), new(0.96f, 0.7f, 0.08f) };
        c[(int)Fungus.ScarletCup] = new Vector3[] { new(0.96f, 0.04f, 0.12f), new(1f, 0.1f, 0.24f) };
        c[(int)Fungus.Coral] = new Vector3[] { new(1f, 0.4f, 0.6f), new(1f, 0.5f, 0.18f), new(0.94f, 0.24f, 0.76f) };
        c[(int)Fungus.Jelly] = new Vector3[] { new(1f, 0.55f, 0.04f), new(1f, 0.76f, 0.1f), new(0.62f, 0.18f, 0.96f) };
        c[(int)Fungus.Glowing] = new Vector3[] { new(0.15f, 0.88f, 1f), new(0.32f, 0.5f, 1f), new(0.25f, 1f, 0.55f), new(0.72f, 0.36f, 1f) };
        return c;
    }

    /// <summary>The colors a cap of <paramref name="species"/> takes (one is picked per spot, then lightly varied).</summary>
    public static ReadOnlySpan<Vector3> ColorsOf(Fungus species) => s_colors[(int)species];

    // The giant caps' kinds (repeats weight them): leaning to the strange ones, glowing, amethyst and ink caps.
    private static readonly Look[] s_giantLooks =
    {
        new(Fungus.Amethyst, CapShape.Wavy), new(Fungus.Glowing, CapShape.Wavy), new(Fungus.FlyAgaric, CapShape.Dome),
        new(Fungus.Amethyst, CapShape.Dome), new(Fungus.InkCap, CapShape.Bell), new(Fungus.Glowing, CapShape.Dome),
        new(Fungus.Parasol, CapShape.Flat), new(Fungus.Chanterelle, CapShape.Funnel), new(Fungus.Bolete, CapShape.Dome),
        new(Fungus.Amethyst, CapShape.Flat),
    };

    // The medium mushrooms' kinds (the Map's solid props).
    private static readonly Look[] s_mediumLooks =
    {
        new(Fungus.Glowing, CapShape.Dome), new(Fungus.InkCap, CapShape.Bell), new(Fungus.Amethyst, CapShape.Wavy),
        new(Fungus.Coral, CapShape.Coral), new(Fungus.FlyAgaric, CapShape.Dome), new(Fungus.Enoki, CapShape.Dome),
        new(Fungus.Glowing, CapShape.Bell), new(Fungus.Morel, CapShape.Cone), new(Fungus.Parasol, CapShape.Flat),
        new(Fungus.Bolete, CapShape.Dome), new(Fungus.Chanterelle, CapShape.Funnel), new(Fungus.Puffball, CapShape.Ball),
        new(Fungus.Glowing, CapShape.Cone), new(Fungus.Amethyst, CapShape.Dome),
    };

    // The small ground fungi's kinds (glowing ones come in their own clusters, s_glowLooks).
    private static readonly Look[] s_smallLooks =
    {
        new(Fungus.Jelly, CapShape.Jelly), new(Fungus.Coral, CapShape.Coral), new(Fungus.InkCap, CapShape.Bell),
        new(Fungus.Amethyst, CapShape.Wavy), new(Fungus.ScarletCup, CapShape.Cup), new(Fungus.FlyAgaric, CapShape.Dome),
        new(Fungus.Enoki, CapShape.Dome), new(Fungus.Morel, CapShape.Cone), new(Fungus.Puffball, CapShape.Ball),
        new(Fungus.Parasol, CapShape.Flat), new(Fungus.Bolete, CapShape.Dome), new(Fungus.Chanterelle, CapShape.Funnel),
    };

    private static readonly Look[] s_glowLooks =
    {
        new(Fungus.Glowing, CapShape.Dome), new(Fungus.Glowing, CapShape.Bell), new(Fungus.Glowing, CapShape.Cone),
        new(Fungus.Glowing, CapShape.Wavy), new(Fungus.Glowing, CapShape.Flat),
    };

    /// <summary>A kind of fungus with its cap shape: two neighboring spots never share one.</summary>
    public readonly record struct Look(Fungus Species, CapShape Shape);

    /// <summary>Glowing giant caps glow this much at most (softly, not like the little ones).</summary>
    public const float GiantGlow = 0.7f;

    /// <summary>The clean light cream of every mushroom stem (giant, side and toadstool stems; giant stems a little shaded, by <see cref="GiantStemShade"/>).</summary>
    public static readonly Vector3 StemCream = new(0.93f, 0.87f, 0.72f);

    /// <summary>How much darker than <see cref="StemCream"/> the old giant stems are at most (their bark still reads cream, just a little aged).</summary>
    public const float GiantStemShade = 0.82f;

    /// <summary>How brightly the glowing mushrooms glow (their <see cref="Prop.Glow"/>): from this to twice it.</summary>
    public const float GlowStrength = 1.2f;

    private readonly Prop[][] _props;

    /// <summary>The fog: one low, soft layer over the whole area, thinner volumes easing it in along the path, thicker ones in hollows.</summary>
    public FogPatch[] Fog { get; }

    /// <summary>Soft lights over the biggest glowing clusters (no shadows).</summary>
    public GlowLight[] Lights { get; }

    private readonly Spot[] _groundSpots;

    /// <summary>The small fungi's ground spots: where each little single, pair or cluster grows and its look.</summary>
    public ReadOnlySpan<Spot> GroundSpots => _groundSpots;

    /// <summary>A ground spot of small fungi: its center and look (no two neighbors share one).</summary>
    public readonly record struct Spot(Vector2 At, Look Look);

    private DeepForestScenery(Prop[][] props, FogPatch[] fog, GlowLight[] lights, Spot[] groundSpots)
    {
        _props = props;
        _groundSpots = groundSpots;
        Fog = fog;
        Lights = lights;
    }

    /// <summary>The props of one kind.</summary>
    public ReadOnlySpan<Prop> Of(PropKind kind) => _props[(int)kind];

    /// <summary>A fixed pseudo-random value in [0, 1) for (i, k): the same forest every run.</summary>
    public static float Rand(int i, int k)
    {
        uint h = unchecked((uint)i * 2654435761u ^ (uint)k * 2246822519u ^ 0x5bd1e995u);
        h = unchecked((h ^ (h >> 15)) * 2246822519u);
        h = unchecked((h ^ (h >> 13)) * 3266489917u);
        h ^= h >> 16;
        return (h >> 8) * (1f / 16777216f);
    }

    private static Vector3 OnGround(float x, float z, float sink = 0f) => new(x, Ground.HeightAt(x, z) - sink, z);

    /// <summary>How far (meters) the base of a big standing thing (giant stem, skirt, root) goes below the lowest ground under it.</summary>
    public const float BaseSink = 0.15f;

    /// <summary>How far (meters) the base of a small ground prop (glowing mushroom, toadstool, fern) goes below the lowest ground under it.</summary>
    public const float SmallSink = 0.1f;

    /// <summary>A giant stem's footprint radius, in stem radii: its flared, gnarled foot and skirt reach this far out.</summary>
    public const float FootRadius = 1.7f;

    /// <summary>The fewest points sampled on each ring of a footprint (<see cref="GroundUnder"/>).</summary>
    public const int FootprintPoints = 16;

    /// <summary>The most distance (meters) between neighboring samples of a footprint, along and across its rings.</summary>
    public const float FootprintSpacing = 0.5f;

    /// <summary>
    /// The lowest and highest ground under a round footprint of <paramref name="radius"/> at (x, z):
    /// <see cref="Ground.HeightAt"/> at the center and on rings out to the rim (at least
    /// <see cref="FootprintPoints"/> points on each, at most <see cref="FootprintSpacing"/> apart).
    /// </summary>
    public static (float Low, float High) GroundUnder(float x, float z, float radius)
    {
        float low = Ground.HeightAt(x, z), high = low;
        int rings = Math.Max(1, (int)MathF.Ceiling(radius / FootprintSpacing));
        for (int ring = 1; ring <= rings; ring++)
        {
            float d = radius * ring / rings;
            int points = Math.Max(FootprintPoints, (int)MathF.Ceiling(MathF.Tau * d / FootprintSpacing));
            for (int i = 0; i < points; i++)
            {
                float a = (i + 0.5f * (ring & 1)) * MathF.Tau / points;
                float h = Ground.HeightAt(x + d * MathF.Cos(a), z + d * MathF.Sin(a));
                low = MathF.Min(low, h);
                high = MathF.Max(high, h);
            }
        }
        return (low, high);
    }

    /// <summary>
    /// Where the base of something standing at (x, z) with a round foot of <paramref name="radius"/>
    /// goes: <paramref name="sink"/> below the lowest ground under the foot, so no part of it floats
    /// where the ground drops away. Extend its height by <c>High - base</c> to keep its height above the high side.
    /// </summary>
    public static float BaseHeight(float x, float z, float radius, float sink = BaseSink) => GroundUnder(x, z, radius).Low - sink;

    /// <summary>The unit Stem mesh's radius at <paramref name="t"/> (0 at its foot, 1 at its top), without its gnarls (Models/deep_forest.py).</summary>
    public static float StemProfile(float t)
    {
        float u = 1f - Math.Clamp(t, 0f, 1f);
        return 0.82f + 0.18f * u + 0.5f * u * u * u * u * u;
    }

    private static Quaternion Yaw(float yaw) => Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw);

    /// <summary>Turns +Y by <paramref name="tilt"/> toward the horizontal direction at angle <paramref name="a"/> (x = cos a, z = sin a), after a spin about Y.</summary>
    private static Quaternion Lean(float a, float tilt, float spin)
    {
        var axis = new Vector3(MathF.Sin(a), 0f, -MathF.Cos(a));
        return Quaternion.CreateFromAxisAngle(axis, tilt) * Yaw(spin);
    }

    /// <summary>Whether a small ground prop of <paramref name="radius"/> at (x, z) is in the Deep Forest and off every stem.</summary>
    public static bool OpenGround(float x, float z, float radius) =>
        DeepForestGloom.InArea(x, z, radius) && !Ground.Blocks(x, z, radius + StemClearance)
        && !(Areas.DeepForestPath.Contains(x, z) && MathF.Abs(z) < PathClearHalfWidth + radius);

    public static DeepForestScenery Create()
    {
        var lists = new List<Prop>[Enum.GetValues<PropKind>().Length];
        for (int i = 0; i < lists.Length; i++)
            lists[i] = new List<Prop>();
        void Add(in Prop p) => lists[(int)p.Kind].Add(p);

        var obstacles = Ground.Obstacles;
        int first = obstacles.Length - Ground.DeepForestTrunkCount;
        var glowClusters = new List<(Vector3 Center, Vector3 Color, int Size)>();
        // Ground spots of small fungi: where, whether glowing, the seed, the glow color, the glowing cluster (or -1).
        var ground = new List<(Vector2 At, bool Glow, int Seed, Vector3 Color, int Cluster)>();
        var giantAt = new Vector2[obstacles.Length - first];
        for (int o = first; o < obstacles.Length; o++)
            giantAt[o - first] = new Vector2(obstacles[o].X, obstacles[o].Z);
        var giantLooks = Distinct(giantAt, s_giantLooks, null, 5);

        // The giant mushrooms, one on each of the Map's Deep Forest obstacles.
        for (int o = first; o < obstacles.Length; o++)
        {
            var c = obstacles[o];
            int id = o;
            float r = c.Radius;
            float height = 10f + 3f * r + 6f * Rand(id, 1);
            // Planted: the base under the lowest ground of the whole flared foot, the stem extended
            // down by the drop so it stands `height` above the high side; everything above hangs off `rise`.
            var (_, high) = GroundUnder(c.X, c.Z, r * FootRadius);
            var foot = new Vector3(c.X, BaseHeight(c.X, c.Z, r * FootRadius), c.Z);
            var rise = new Vector3(c.X, high, c.Z);
            float stemHeight = height + high - foot.Y;
            float spin = Rand(id, 2) * MathF.Tau;
            var stemColor = StemCream * (GiantStemShade + (1f - GiantStemShade) * Rand(id, 3));
            Add(new Prop(PropKind.GiantStem, foot, Yaw(spin), new Vector3(r, stemHeight, r), stemColor, Rand(id, 4), owner: o));
            // The root flare: from the base to a little over the high side, its top just inside the stem.
            float skirtHeight = high - foot.Y + 0.8f + 0.15f * r;
            float skirtRadius = r * 0.97f * StemProfile(skirtHeight / stemHeight);
            Add(new Prop(PropKind.Skirt, foot, Yaw(spin + 0.4f), new Vector3(skirtRadius, skirtHeight, skirtRadius), stemColor * 0.92f, Rand(id, 64), owner: o));

            var look = giantLooks[o - first];
            var capColor = Tint(look.Species, id, 5);
            float capRadius = r * (3f + 1.4f * Rand(id, 7)) + 2f;
            float capHeight = capRadius * GiantCapHeight(look.Shape, Rand(id, 8));
            var tilt = Lean(Rand(id, 9) * MathF.Tau, 0.07f * Rand(id, 10), spin);
            var capAt = rise + new Vector3(0f, height - Seat(look.Shape) * capHeight, 0f);
            Add(new Prop(PropKind.GiantCap, capAt, tilt, new Vector3(capRadius, capHeight, capRadius), capColor, Rand(id, 13),
                GiantCapGlow(look.Species, id), o, look.Species, look.Shape, CapSpots(look.Species, id, 11), PatternOf(look.Species)));

            // Side stems leaning out from high on the stem, each with a cap: the lower canopy.
            int branches = (int)(1.2f + 2.4f * Rand(id, 14) + 0.3f * r);
            for (int b = 0; b < branches; b++)
            {
                int bid = o * 8 + b;
                float a = (b + 0.6f * Rand(bid, 20)) * MathF.Tau / branches + spin;
                float lean = 0.55f + 0.4f * Rand(bid, 21);
                float length = height * (0.32f + 0.18f * Rand(bid, 22));
                float baseY = height * (0.45f + 0.2f * Rand(bid, 23));
                var dir = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                var rot = Lean(a, lean, Rand(bid, 24) * MathF.Tau);
                var basePoint = rise + new Vector3(0f, baseY, 0f) + dir * (r * 0.3f);
                var tip = basePoint + Vector3.Transform(Vector3.UnitY, rot) * length;
                float branchCap = r * (1.6f + 0.9f * Rand(bid, 25)) + 1.5f;
                if (!DeepForestGloom.InArea(tip.X, tip.Z))
                    continue; // would hang out over the cliffs
                float stemR = MathF.Max(0.35f, r * 0.28f);
                Add(new Prop(PropKind.BranchStem, basePoint, rot, new Vector3(stemR, length, stemR), StemCream * GiantStemShade, Rand(bid, 26), owner: o));
                float bcH;
                // A side cap of its own kind, never the same as its giant's.
                var bl = s_giantLooks[(int)(Rand(bid, 28) * s_giantLooks.Length)];
                if (bl == look)
                    bl = s_giantLooks[(Array.IndexOf(s_giantLooks, bl) + 1 + (int)(Rand(bid, 29) * 3f)) % s_giantLooks.Length];
                if (bl == look)
                    bl = s_giantLooks[(Array.IndexOf(s_giantLooks, bl) + 1) % s_giantLooks.Length];
                bcH = branchCap * GiantCapHeight(bl.Shape, Rand(bid, 27)) * 0.9f;
                Add(new Prop(PropKind.BranchCap, tip - new Vector3(0f, Seat(bl.Shape) * bcH, 0f), Lean(a, lean * 0.2f, Rand(bid, 30) * MathF.Tau),
                    new Vector3(branchCap, bcH, branchCap), Tint(bl.Species, bid, 31), Rand(bid, 31), GiantCapGlow(bl.Species, bid), o,
                    bl.Species, bl.Shape, CapSpots(bl.Species, bid, 32), PatternOf(bl.Species)));
            }

            // Bracket shelves climbing the stem.
            int shelves = (int)(4.5f * Rand(id, 15));
            for (int s = 0; s < shelves; s++)
            {
                int sid = o * 8 + s;
                float a = Rand(sid, 40) * MathF.Tau;
                var dir = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                float y = 1.5f + Math.Min(6f, height * 0.4f) * Rand(sid, 41);
                float size = r * (0.5f + 0.4f * Rand(sid, 42)) + 0.4f;
                // Brackets in tiers of different colors: orange, teal, magenta and gold.
                Add(new Prop(PropKind.Shelf, rise + new Vector3(0f, y, 0f) + dir * (r * 0.92f), Yaw(-a),
                    new Vector3(size, size * (0.6f + 0.4f * Rand(sid, 45)), size), Tint(Fungus.Bracket, sid, 43), Rand(sid, 44), owner: o, species: Fungus.Bracket));
            }

            // Big arching roots around the foot.
            int roots = 3 + (int)(4f * Rand(id, 16));
            for (int k = 0; k < roots; k++)
            {
                int rid = o * 8 + k;
                float a = (k + 0.7f * Rand(rid, 50)) * MathF.Tau / roots;
                var dir = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                float length = r * (1.3f + 1.2f * Rand(rid, 51)) + 0.8f;
                float girth = r * (0.9f + 0.5f * Rand(rid, 52));
                var at = new Vector3(c.X, 0f, c.Z) + dir * (r * 0.75f);
                var (rootAt, rootTurn) = PlantRoot(at.X, at.Z, dir, length, girth, -a);
                Add(new Prop(PropKind.Root, rootAt, rootTurn, new Vector3(length, girth, girth),
                    new Vector3(0.42f, 0.25f, 0.3f) * (0.85f + 0.3f * Rand(rid, 53)), Rand(rid, 54), owner: o));
            }

            // Webs on some caps: hung between the stem and the cap's rim, and low between roots.
            if (Rand(id, 17) < 0.3f)
            {
                float a = Rand(id, 18) * MathF.Tau;
                var dir = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                float webR = MathF.Min(3.5f, (capRadius - r) * 0.42f);
                var center = rise + new Vector3(0f, height - 0.18f * capHeight - webR * 0.85f, 0f) + dir * (r + webR * 0.95f);
                // The web's plane holds the stem's direction out and up: face it sideways.
                var rot = Yaw(-a) * Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI * 0.5f);
                if (DeepForestGloom.InArea(center.X, center.Z))
                    Add(new Prop(PropKind.Web, center, rot, new Vector3(webR, 1f, webR), new Vector3(0.82f, 0.84f, 0.95f), Rand(id, 19), owner: o));
                if (Rand(id, 60) < 0.6f)
                {
                    float b = a + 1.5f + Rand(id, 61);
                    var low = new Vector3(MathF.Cos(b), 0f, MathF.Sin(b));
                    float lowR = 0.7f + 0.5f * r * Rand(id, 62);
                    var p = new Vector3(c.X, 0f, c.Z) + low * (r + lowR * 0.9f);
                    if (DeepForestGloom.InArea(p.X, p.Z))
                        Add(new Prop(PropKind.Web, OnGround(p.X, p.Z) + new Vector3(0f, lowR * 0.9f, 0f), Yaw(-b) * Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI * 0.5f),
                            new Vector3(lowR, 1f, lowR), new Vector3(0.82f, 0.84f, 0.95f), Rand(id, 63), owner: o));
                }
            }

            // Glowing mushrooms huddled at some feet.
            if (Rand(id, 70) < 0.5f)
                GlowCluster(new Vector2(c.X, c.Z), r * FootRadius + 0.4f, r * FootRadius + 2.4f, 3 + (int)(5f * Rand(id, 71)), id * 16);
        }

        // The medium mushrooms: one spot on each of the Map's Deep Forest MediumMushroom props, exactly
        // in its circle and no taller than it.
        var medium = new List<(Fungtopia.Map.Prop Prop, int Circle)>();
        int circle = Ground.PropCircleStart;
        foreach (var mp in Props.All)
        {
            if (mp.Kind == Fungtopia.Map.PropKind.MediumMushroom && mp.Area == Area.DeepForest)
                medium.Add((mp, circle));
            circle += mp.CircleCount;
        }
        var mediumAt = new Vector2[medium.Count];
        for (int i = 0; i < medium.Count; i++)
            mediumAt[i] = new Vector2(medium[i].Prop.X, medium[i].Prop.Z);
        var mediumLooks = Distinct(mediumAt, s_mediumLooks, null, 300);
        for (int i = 0; i < medium.Count; i++)
        {
            var (mp, ci) = medium[i];
            int seed = 300000 + i * 32;
            float h = mp.Height * (0.8f + 0.2f * Rand(seed, 1));
            float glow = mediumLooks[i].Species == Fungus.Glowing ? GlowStrength * (1f + Rand(seed, 2)) : 0f;
            Grow(mediumLooks[i], mediumAt[i], h, mp.Radius, seed, ci, PropKind.MediumStem, PropKind.MediumCap, glow);
        }

        // Glowing clusters on the open ground and along the path's edges, and small fungi of every kind.
        var b0 = DeepForestLayout.Bounds;
        for (int k = 0; k < 220; k++)
        {
            float x = b0.MinX + (b0.MaxX - b0.MinX) * Rand(k, 80), z = b0.MinZ + (b0.MaxZ - b0.MinZ) * Rand(k, 81);
            GlowCluster(new Vector2(x, z), 0f, 1.8f, 3 + (int)(5f * Rand(k, 82)), 100000 + k * 16);
        }
        for (int k = 0; k < 14; k++)
        {
            float x = Areas.DeepForestPath.MinX + (Areas.DeepForestPath.MaxX - Areas.DeepForestPath.MinX) * (k + 0.5f) / 14f;
            float z = (k % 2 == 0 ? 1f : -1f) * (2.6f + Rand(k, 83));
            if (Rand(k, 84) < DeepForestGloom.At(x, z) + 0.25f)
                GlowCluster(new Vector2(x, z), 0f, 1f, 2 + (int)(3f * Rand(k, 85)), 200000 + k * 16);
        }
        for (int k = 0; k < 1100; k++)
        {
            float x = b0.MinX + (b0.MaxX - b0.MinX) * Rand(k, 90), z = b0.MinZ + (b0.MaxZ - b0.MinZ) * Rand(k, 91);
            if (!OpenGround(x, z, SmallSpotRadius))
                continue;
            ground.Add((new Vector2(x, z), false, 400000 + k * 32, Vector3.Zero, -1));
        }

        // Every ground spot gets its kind: never the same as its nearest neighbor's.
        var groundAt = new Vector2[ground.Count];
        var wantsGlow = new bool[ground.Count];
        for (int i = 0; i < ground.Count; i++)
        {
            groundAt[i] = ground[i].At;
            wantsGlow[i] = ground[i].Glow;
        }
        var groundLooks = Distinct(groundAt, s_smallLooks, wantsGlow, 500);
        for (int i = 0; i < ground.Count; i++)
        {
            var g = ground[i];
            float h = SmallMaxHeight * (0.35f + 0.6f * Rand(g.Seed, 1));
            float glow = groundLooks[i].Species == Fungus.Glowing ? GlowStrength * (1f + Rand(g.Seed, 2)) : 0f;
            int placed = Grow(groundLooks[i], g.At, h, SmallSpotRadius, g.Seed, -1, PropKind.SmallStem, PropKind.SmallCap, glow, g.Color);
            if (g.Cluster >= 0 && placed > 0)
            {
                var cl = glowClusters[g.Cluster];
                glowClusters[g.Cluster] = (cl.Center, cl.Color, cl.Size + 1);
            }
        }

        // Ferns: everywhere in the gloom, thickest along the overgrown path and at the entrance.
        for (int k = 0; k < 7000; k++)
        {
            float x, z;
            if (k < 1400)
            {
                // The path and the entrance clearing.
                x = Areas.DeepForestPath.MinX - 14f + (Areas.DeepForestPath.MaxX - Areas.DeepForestPath.MinX + 14f) * Rand(k, 100);
                z = (Rand(k, 101) * 2f - 1f) * (x < Areas.DeepForestPath.MinX ? 14f : Areas.DeepForestGapHalfWidth);
            }
            else
            {
                x = b0.MinX + (b0.MaxX - b0.MinX) * Rand(k, 100);
                z = b0.MinZ + (b0.MaxZ - b0.MinZ) * Rand(k, 101);
            }
            // Low (under SmallMaxHeight), so they are walked through.
            float size = (0.6f + 0.4f * Rand(k, 102)) * SmallMaxHeight / FernHeight;
            if (!OpenGround(x, z, 0.3f))
                continue;
            // Deep jewel greens, teal to emerald, a touch darker deeper in.
            var color = new Vector3(0.05f, 0.42f, 0.38f).Lerp(new Vector3(0.18f, 0.5f, 0.12f), Rand(k, 103)) * (1.15f - 0.25f * DeepForestGloom.At(x, z));
            Add(new Prop(PropKind.Fern, new Vector3(x, BaseHeight(x, z, size * FernFoot, SmallSink), z), Yaw(Rand(k, 104) * MathF.Tau), new Vector3(size), color, Rand(k, 105)));
        }

        // Spores drifting in the air, more near the glowing clusters.
        for (int k = 0; k < 1400; k++)
        {
            float x, z;
            if (k < 600 && glowClusters.Count > 0)
            {
                var g = glowClusters[(int)(Rand(k, 110) * glowClusters.Count)].Center;
                float a = Rand(k, 111) * MathF.Tau, d = 5f * Rand(k, 112);
                x = g.X + MathF.Cos(a) * d;
                z = g.Z + MathF.Sin(a) * d;
            }
            else
            {
                x = b0.MinX + (b0.MaxX - b0.MinX + 14f) * Rand(k, 110);
                z = x > b0.MaxX ? (Rand(k, 111) * 2f - 1f) * Areas.DeepForestGapHalfWidth : b0.MinZ + (b0.MaxZ - b0.MinZ) * Rand(k, 111);
            }
            if (!DeepForestGloom.InArea(x, z) || Ground.Blocks(x, z))
                continue;
            float size = 0.05f + 0.07f * Rand(k, 113);
            Add(new Prop(PropKind.Spore, OnGround(x, z) + new Vector3(0f, 0.4f + 5.5f * Rand(k, 114), 0f), Quaternion.Identity, new Vector3(size),
                ColorsOf(Fungus.Glowing)[(int)(Rand(k, 115) * ColorsOf(Fungus.Glowing).Length)].Lerp(Vector3.One, 0.4f), Rand(k, 116), 1f));
        }

        // Low mist puffs, gathering in the hollows; thin at the path, thicker deeper in.
        for (int k = 0; k < 1100; k++)
        {
            float x = b0.MinX + (b0.MaxX - b0.MinX + 14f) * Rand(k, 120);
            float z = x > b0.MaxX ? (Rand(k, 121) * 2f - 1f) * Areas.DeepForestGapHalfWidth : b0.MinZ + (b0.MaxZ - b0.MinZ) * Rand(k, 121);
            if (!DeepForestGloom.InArea(x, z, 0.5f))
                continue;
            float hollow = Hollowness(x, z);
            if (Rand(k, 122) > 0.35f + 1.5f * hollow || Rand(k, 123) > DeepForestGloom.At(x, z) + 0.1f)
                continue;
            float size = 3.5f + 4f * Rand(k, 124) + 3f * hollow;
            Add(new Prop(PropKind.Mist, OnGround(x, z) + new Vector3(0f, 0.6f + 0.6f * Rand(k, 125), 0f), Quaternion.Identity, new Vector3(size),
                new Vector3(0.52f, 0.42f, 0.7f), Rand(k, 126), 0.3f + 0.4f * hollow));
        }

        var props = new Prop[lists.Length][];
        for (int i = 0; i < lists.Length; i++)
            props[i] = lists[i].ToArray();
        var spots = new Spot[groundAt.Length];
        for (int i = 0; i < spots.Length; i++)
            spots[i] = new Spot(groundAt[i], groundLooks[i]);
        return new DeepForestScenery(props, BuildFog(), BuildLights(glowClusters), spots);

        // A glowing cluster: a few ground spots of glowing fungi (of different shapes) around a center.
        void GlowCluster(Vector2 center, float minD, float maxD, int count, int seed)
        {
            var colors = ColorsOf(Fungus.Glowing);
            var color = colors[(int)(Rand(seed, 1) * colors.Length)];
            int placed = 0;
            var sum = Vector3.Zero;
            for (int k = 0; k < count; k++)
            {
                int gid = seed + k;
                float a = Rand(gid, 2) * MathF.Tau, d = minD + (maxD - minD) * Rand(gid, 3);
                float x = center.X + MathF.Cos(a) * d, z = center.Y + MathF.Sin(a) * d;
                if (!OpenGround(x, z, SmallSpotRadius))
                    continue;
                ground.Add((new Vector2(x, z), true, 600000 + gid * 32, color * (0.9f + 0.2f * Rand(gid, 6)), glowClusters.Count));
                sum += OnGround(x, z);
                placed++;
            }
            if (placed > 0)
                glowClusters.Add((sum / placed, color, 0));
        }

        // One spot: a single mushroom, a pair or a cluster of different heights (enoki: many thin
        // stems), of `look`, the tallest `height` over the high ground under the spot, every stem's
        // foot within `radius` of `at`. Returns how many it placed.
        int Grow(Look look, Vector2 at, float height, float radius, int seed, int owner, PropKind stemKind, PropKind capKind, float glow, Vector3 color = default)
        {
            var species = look.Species;
            if (color == default)
                color = Tint(species, seed, 3);
            bool stemless = look.Shape is CapShape.Ball or CapShape.Cup or CapShape.Coral or CapShape.Jelly;
            float roll = Rand(seed, 4);
            int count = species == Fungus.Enoki ? 5 + (int)(5f * roll)
                : stemless ? 1 + (int)(3.99f * roll)
                : roll < 0.45f ? 1 : roll < 0.75f ? 2 : 3;
            bool solid = owner >= 0;
            var (_, high) = GroundUnder(at.X, at.Y, radius);
            float spin = Rand(seed, 5) * MathF.Tau;
            float spots = CapSpots(species, seed, 6);
            var pattern = PatternOf(species);
            int placed = 0;
            for (int j = 0; j < count; j++)
            {
                int mid = seed + j;
                float h = height * (j == 0 ? 1f : species == Fungus.Enoki ? 0.65f + 0.35f * Rand(mid, 10) : 0.4f + 0.45f * Rand(mid, 10));
                var (capR, capH, stemR) = Proportions(species, look.Shape, h, Rand(mid, 11), Rand(mid, 12));
                if (solid && stemless)
                {
                    // A stemless body fills the prop's circle (its fellows are smaller, inside it).
                    capR = j == 0 ? radius * (0.85f + 0.15f * Rand(mid, 13)) : MathF.Min(capR, radius * 0.6f);
                    capH = MathF.Min(h, j == 0 ? h : capR * 2.2f);
                }
                else if (solid && j == 0 && species != Fungus.Enoki)
                {
                    float grow = MathF.Max(1f, radius / capR);
                    capR *= grow;
                    stemR = MathF.Min(stemR * grow, radius * 0.6f);
                }
                float foot = stemless ? capR : stemR * StalkFoot;
                // Where in the spot: the first near the middle, the others around it.
                float a = spin + j * 2.39996f + 0.5f * Rand(mid, 14);
                float reach = MathF.Max(0f, (solid ? radius : SmallSpotRadius) - foot);
                float d = reach * (j == 0 && species != Fungus.Enoki ? 0.25f * Rand(mid, 15) : 0.45f + 0.55f * Rand(mid, 15));
                float x = at.X + MathF.Cos(a) * d, z = at.Y + MathF.Sin(a) * d;
                if (!solid && !OpenGround(x, z, foot))
                    continue;
                // Tilted a little (enoki splay outward); the stem is shortened so the tilted cap's top and rim stay under `h`.
                float tiltMax = stemless ? 0f : species == Fungus.Enoki ? 0.3f : solid ? 0.12f : 0.2f;
                float tilt = tiltMax * (species == Fungus.Enoki ? 0.4f + 0.6f * Rand(mid, 16) : Rand(mid, 16));
                float lean = species == Fungus.Enoki ? a : Rand(mid, 17) * MathF.Tau;
                var rot = Lean(lean, tilt, Rand(mid, 18) * MathF.Tau);
                float baseY = BaseHeight(x, z, foot, SmallSink);
                var shade = Vector3.Min(color * (0.92f + 0.12f * Rand(mid, 19)), Vector3.One);
                if (stemless)
                {
                    // Sits on the ground, reaching down to its base where the ground drops away.
                    Add(new Prop(capKind, new Vector3(x, baseY, z), rot, new Vector3(capR, capH + high - baseY, capR), shade, Rand(mid, 20),
                        glow, owner, species, look.Shape, spots, pattern));
                }
                else
                {
                    float stemTop = h - (1f - Seat(look.Shape)) * capH * MathF.Cos(tilt) - capR * MathF.Sin(tilt);
                    float length = (high + stemTop - baseY) / MathF.Cos(tilt);
                    var basePoint = new Vector3(x, baseY, z);
                    Add(new Prop(stemKind, basePoint, rot, new Vector3(stemR, length, stemR), StemCream, Rand(mid, 21), glow * 0.3f, owner, species));
                    var capAt = basePoint + Vector3.Transform(Vector3.UnitY, rot) * length - Vector3.Transform(Vector3.UnitY, rot) * (Seat(look.Shape) * capH);
                    Add(new Prop(capKind, capAt, rot, new Vector3(capR, capH, capR), shade, Rand(mid, 20), glow, owner, species, look.Shape, spots, pattern));
                }
                placed++;
            }
            return placed;
        }
    }

    /// <summary>A small ground spot's radius (meters): its little cluster stays inside it.</summary>
    public const float SmallSpotRadius = 0.3f;

    /// <summary>Freely scattered ground things (small fungi, ferns) stay under this height (meters) over the ground.</summary>
    public const float SmallMaxHeight = 0.38f;

    /// <summary>A Stalk's footprint radius, in stem radii (its bulb, Models/deep_forest.py).</summary>
    public const float StalkFoot = 1.25f;

    /// <summary>How far (in cap heights) a cap's origin sits below the top of its stem (the stem goes up into it).</summary>
    public static float Seat(CapShape shape) => shape switch
    {
        CapShape.Wavy => 0.16f,
        CapShape.Dome => 0.14f,
        CapShape.Flat => 0.36f,
        CapShape.Funnel => 0.05f,
        CapShape.Bell => 0.8f,
        CapShape.Cone => 0.06f,
        _ => 0f,
    };

    private static float GiantCapHeight(CapShape shape, float t) => shape switch
    {
        CapShape.Flat => 0.2f + 0.08f * t,
        CapShape.Bell => 0.6f + 0.2f * t,
        CapShape.Funnel => 0.4f + 0.12f * t,
        CapShape.Dome => 0.4f + 0.15f * t,
        _ => 0.32f + 0.16f * t,
    };

    /// <summary>A cap's radius, height and stem radius for a mushroom `h` tall (t, u: fixed variation 0..1).</summary>
    private static (float CapR, float CapH, float StemR) Proportions(Fungus species, CapShape shape, float h, float t, float u)
    {
        switch (species)
        {
            case Fungus.FlyAgaric: { float r = h * (0.36f + 0.1f * t); return (r, r * (0.55f + 0.2f * u), r * 0.22f); }
            case Fungus.Bolete: { float r = h * (0.45f + 0.1f * t); return (r, r * (0.6f + 0.15f * u), r * (0.42f + 0.08f * u)); }
            case Fungus.Chanterelle: { float r = h * (0.38f + 0.1f * t); return (r, h * (0.45f + 0.15f * u), r * 0.2f); }
            case Fungus.Parasol: { float r = h * (0.42f + 0.13f * t); return (r, r * (0.25f + 0.07f * u), r * (0.08f + 0.03f * u)); }
            case Fungus.InkCap: { float r = h * (0.16f + 0.06f * t); return (r, h * (0.45f + 0.15f * u), r * 0.35f); }
            case Fungus.Morel: { float r = h * (0.2f + 0.06f * t); return (r, h * (0.5f + 0.12f * u), r * 0.6f); }
            case Fungus.Enoki: { float r = h * (0.06f + 0.03f * t); return (r, r * 0.8f, h * 0.022f); }
            case Fungus.Amethyst: { float r = h * (0.38f + 0.12f * t); return (r, r * (0.4f + 0.15f * u), r * 0.2f); }
            case Fungus.Puffball: { float r = h * (0.5f + 0.1f * t); return (r, h, 0f); }
            case Fungus.ScarletCup: { float r = h * (0.8f + 0.3f * t); return (r, h, 0f); }
            case Fungus.Coral: { float r = h * (0.5f + 0.2f * t); return (r, h, 0f); }
            case Fungus.Jelly: { float r = h * (0.6f + 0.2f * t); return (r, h, 0f); }
        }
        // Glowing: shaped like its cap.
        return shape switch
        {
            CapShape.Bell => Proportions(Fungus.InkCap, shape, h, t, u),
            CapShape.Cone => Proportions(Fungus.Morel, shape, h, t, u),
            CapShape.Flat => Proportions(Fungus.Parasol, shape, h, t, u),
            CapShape.Wavy => Proportions(Fungus.Amethyst, shape, h, t, u),
            _ => Proportions(Fungus.FlyAgaric, shape, h, t, u),
        };
    }

    /// <summary>One of the colors of <paramref name="species"/>, lightly varied, by the fixed seed (i, k).</summary>
    private static Vector3 Tint(Fungus species, int i, int k)
    {
        var colors = ColorsOf(species);
        var c = colors[(int)(Rand(i, k) * colors.Length)] * (0.92f + 0.08f * Rand(i, k + 1));
        return Vector3.Min(c, Vector3.One);
    }

    private static float CapSpots(Fungus species, int i, int k) => species switch
    {
        Fungus.FlyAgaric => 0.7f + 0.3f * Rand(i, k),
        Fungus.Amethyst => Rand(i, k) < 0.35f ? 0.4f + 0.3f * Rand(i, k + 1) : 0f,
        Fungus.Puffball => 0.3f,
        Fungus.Glowing => Rand(i, k) < 0.3f ? 0.5f : 0f,
        _ => 0f,
    };

    private static CapPattern PatternOf(Fungus species) => species switch
    {
        Fungus.Parasol => CapPattern.Scales,
        Fungus.Morel => CapPattern.Honeycomb,
        Fungus.InkCap => CapPattern.Ink,
        _ => CapPattern.Spots,
    };

    private static float GiantCapGlow(Fungus species, int i) => species == Fungus.Glowing ? GiantGlow * (0.7f + 0.3f * Rand(i, 90)) : 0f;

    /// <summary>
    /// Gives every spot a look: by its fixed seed from <paramref name="looks"/> (from the glowing
    /// ones where <paramref name="glowing"/> says), but never the look of a spot it is nearest to or
    /// that is nearest to it, so no two neighbors are identical.
    /// </summary>
    private static Look[] Distinct(Vector2[] at, Look[] looks, bool[]? glowing, int salt)
    {
        int n = at.Length;
        var nearest = new int[n];
        for (int i = 0; i < n; i++)
        {
            float best = float.MaxValue;
            nearest[i] = -1;
            for (int j = 0; j < n; j++)
            {
                float d = Vector2.DistanceSquared(at[i], at[j]);
                if (j != i && d < best)
                    (best, nearest[i]) = (d, j);
            }
        }
        var neighbors = new List<int>[n];
        for (int i = 0; i < n; i++)
            neighbors[i] = new List<int>();
        for (int i = 0; i < n; i++)
        {
            if (nearest[i] < 0)
                continue;
            neighbors[i].Add(nearest[i]);
            neighbors[nearest[i]].Add(i);
        }
        var result = new Look[n];
        var taken = new bool[n];
        for (int i = 0; i < n; i++)
        {
            var first = glowing != null && glowing[i] ? s_glowLooks : looks;
            var second = first == looks ? s_glowLooks : looks;
            int start = (int)(Rand(i, salt) * first.Length);
            result[i] = first[start];
            bool found = false;
            foreach (var pool in new[] { first, second })
            {
                for (int k = 0; k < pool.Length && !found; k++)
                {
                    var look = pool[(start + k) % pool.Length];
                    bool free = true;
                    foreach (int j in neighbors[i])
                        free &= !(taken[j] && result[j] == look);
                    if (free)
                    {
                        result[i] = look;
                        found = true;
                    }
                }
            }
            taken[i] = true;
        }
        return result;
    }

    /// <summary>The footprint radius of a fern of scale 1 (its tuft of frond bases).</summary>
    public const float FernFoot = 0.15f;

    /// <summary>The unit Fern mesh's height (its fronds' highest arch).</summary>
    public const float FernHeight = 0.62f;

    /// <summary>The ground points a root of <paramref name="length"/> along <paramref name="dir"/> from (x, z) is checked at, from its start (0) to its tip (<see cref="RootSamples"/>).</summary>
    public const int RootSamples = 12;

    /// <summary>
    /// Plants a root (the unit Root mesh: from the origin along +X to x = 1, yawed by
    /// <paramref name="yaw"/> so +X is <paramref name="dir"/>): pitched to follow the ground from its
    /// start to its tip, and lowered so its whole underside (its line and both sides of it, girth
    /// wide) is <see cref="BaseSink"/> or more under the ground.
    /// </summary>
    public static (Vector3 Position, Quaternion Rotation) PlantRoot(float x, float z, Vector3 dir, float length, float girth, float yaw)
    {
        float start = Ground.HeightAt(x, z), tip = Ground.HeightAt(x + dir.X * length, z + dir.Z * length);
        float slope = (tip - start) / length;
        var side = new Vector3(-dir.Z, 0f, dir.X) * (0.26f * girth);
        float lowest = 0f;
        for (int i = 0; i <= RootSamples; i++)
        {
            float d = length * i / RootSamples;
            var p = new Vector3(x, 0f, z) + dir * d;
            float line = start + slope * d;
            lowest = MathF.Min(lowest, Ground.HeightAt(p.X, p.Z) - line);
            lowest = MathF.Min(lowest, Ground.HeightAt(p.X + side.X, p.Z + side.Z) - line);
            lowest = MathF.Min(lowest, Ground.HeightAt(p.X - side.X, p.Z - side.Z) - line);
        }
        // Yaw then pitch about the root's own side axis: +X tips down (or up) to the tip's ground.
        float pitch = MathF.Atan(slope);
        var rotation = Yaw(yaw) * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, pitch);
        return (new Vector3(x, start + lowest - BaseSink, z), rotation);
    }

    /// <summary>How far (x, z) lies below the ground around it, 0..1 (1 = a clear hollow, a quarter meter or more below the ground 10 m around).</summary>
    public static float Hollowness(float x, float z)
    {
        const float reach = 10f;
        float h = Ground.HeightAt(x, z);
        float around = 0f;
        for (int i = 0; i < 8; i++)
        {
            float a = i * MathF.Tau / 8f;
            around += Ground.HeightAt(x + reach * MathF.Cos(a), z + reach * MathF.Sin(a));
        }
        return Math.Clamp((around / 8f - h) / 0.25f, 0f, 1f);
    }

    /// <summary>The fog volumes' heights: from a little under the lowest ground to this far above it (meters).</summary>
    public const float FogTop = 6f;

    private static FogPatch[] BuildFog()
    {
        var b = DeepForestLayout.Bounds;
        float low = float.MaxValue, high = float.MinValue;
        for (float x = b.MinX; x <= b.MaxX; x += 4f)
        for (float z = b.MinZ; z <= b.MaxZ; z += 4f)
        {
            float h = Ground.HeightAt(x, z);
            low = MathF.Min(low, h);
            high = MathF.Max(high, h);
        }
        float bottom = low - 1f, top = high + FogTop;
        var fog = new List<FogPatch>();
        // The main layer, from a dozen meters past the entrance to the far west.
        const float easeIn = 12f;
        fog.Add(Box(b.MinX, b.MinZ, b.MaxX - easeIn, b.MaxZ, bottom, top, 0.035f));
        // Easing in: thinner at the entrance clearing, thinner still along the path.
        fog.Add(Box(b.MaxX - easeIn, b.MinZ, b.MaxX, b.MaxZ, bottom, top, 0.02f));
        var path = Areas.DeepForestPath;
        fog.Add(Box(path.MinX, path.MinZ, path.MaxX, path.MaxZ, bottom, top - 1f, 0.008f));
        // A bit thicker in the hollows.
        for (float x = b.MinX + 8f; x <= b.MaxX - easeIn - 8f; x += 10f)
        for (float z = b.MinZ + 8f; z <= b.MaxZ - 8f; z += 10f)
        {
            if (Hollowness(x, z) < 0.5f)
                continue;
            fog.Add(new FogPatch(OnGround(x, z) + new Vector3(0f, 0.6f, 0f), new Vector3(16f, 3f, 16f), 0.05f, true));
        }
        return fog.ToArray();

        static FogPatch Box(float minX, float minZ, float maxX, float maxZ, float minY, float maxY, float density) =>
            new(new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f),
                new Vector3(maxX - minX, maxY - minY, maxZ - minZ), density, false);
    }

    /// <summary>The number of soft lights (one per biggest glowing cluster, spread out).</summary>
    public const int MaxLights = 16;

    private static GlowLight[] BuildLights(List<(Vector3 Center, Vector3 Color, int Size)> clusters)
    {
        var lights = new List<GlowLight>();
        for (int want = 6; want >= 3 && lights.Count < MaxLights; want--)
        {
            foreach (var (center, color, size) in clusters)
            {
                if (lights.Count >= MaxLights)
                    break;
                if (size < want)
                    continue;
                bool far = true;
                foreach (var l in lights)
                    far &= Vector2.Distance(new Vector2(l.Position.X, l.Position.Z), new Vector2(center.X, center.Z)) > 25f;
                if (far)
                    lights.Add(new GlowLight(center + new Vector3(0f, 1.2f, 0f), color, 7f));
            }
        }
        return lights.ToArray();
    }
}

internal static class VectorExtensions
{
    public static Vector3 Lerp(this Vector3 a, Vector3 b, float t) => Vector3.Lerp(a, b, t);
}
