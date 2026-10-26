using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Map;

namespace Fungtopia.PetCity;

/// <summary>The kinds of little fungi Pet City plants (cartoon-simplified; no eyes or faces).</summary>
public enum FungusKind
{
    /// <summary>Round red cap with white spots (a dome).</summary>
    FlyAgaric,
    /// <summary>A fat brown bolete / porcini: thick dome on a bulbous stem.</summary>
    Bolete,
    /// <summary>A golden chanterelle: a wavy funnel.</summary>
    Chanterelle,
    /// <summary>A tall thin parasol with a wide flat cap.</summary>
    Parasol,
    /// <summary>A shaggy ink cap: a tall bell with an inky rim.</summary>
    InkCap,
    /// <summary>A morel: a honeycomb cone.</summary>
    Morel,
    /// <summary>Enoki: a bunch of tall thin stems with tiny caps.</summary>
    Enoki,
    /// <summary>A purple amethyst deceiver: a wavy dome.</summary>
    Amethyst,
    /// <summary>A round puffball.</summary>
    Puffball,
    /// <summary>Shelf / bracket fungi growing out of a lamp post.</summary>
    Bracket,
    /// <summary>A scarlet cup fungus.</summary>
    Cup,
    /// <summary>Branching coral fungus (pink / orange).</summary>
    Coral,
    /// <summary>Glossy lobed jelly fungus.</summary>
    Jelly,
    /// <summary>Bioluminescent blue / green conical caps that glow at night.</summary>
    GlowCap,
}

/// <summary>What a group of fungi grows on.</summary>
public enum FungusHost
{
    /// <summary>The soil in a planter (on top of the solid prop, inside its footprint).</summary>
    Planter,
    /// <summary>The ground at a lamp's foot (walkable decoration).</summary>
    LampFoot,
    /// <summary>A lamp's post (bracket fungi, inside the lamp's footprint).</summary>
    LampPost,
    /// <summary>The ground along a building's plinth, on its sides and back (walkable decoration).</summary>
    BuildingFoot,
    /// <summary>A pot on a roof terrace.</summary>
    Terrace,
}

/// <summary>What a fungus kind looks like, as Pet City draws it.</summary>
public readonly record struct FungusLook(string Model, string Shape, float MinHeight, float MaxHeight,
    float MinWidth, float MaxWidth, float MaxTilt, Swatch TintA, Swatch TintB, bool Glows, bool Pale);

/// <summary>
/// One little fungus: the glb object <see cref="FungusLook.Model"/> of its <see cref="Kind"/> (built
/// 1 m tall) scaled to <see cref="Height"/> tall and <see cref="Height"/> x <see cref="Width"/> across,
/// turned by <see cref="Yaw"/> about +Y (for a bracket: at 0 it sticks out toward -Z), then leaning
/// <see cref="Tilt"/> radians toward <see cref="TiltYaw"/>, standing at <see cref="Position"/> (its base).
/// Its cap is drawn in <see cref="Tint"/>.
/// </summary>
public readonly record struct Fungus(FungusKind Kind, FungusHost Host, int Group, Vector3 Position, float Yaw,
    float Tilt, float TiltYaw, float Height, float Width, Swatch Tint);

/// <summary>One spot where a group of fungi grows: a single mushroom, a pair or a cluster.</summary>
public readonly record struct FungusGroup(FungusHost Host, FungusKind Main, Vector3 Center, int First, int Count);

/// <summary>
/// The fungi Pet City draws (fixed data from <see cref="Seed"/>, the same every run): a mix of
/// many kinds (<see cref="FungusKind"/>) in planters, on roof-terrace pots, at the lamps' feet, on
/// lamp posts and along the buildings' plinths, each a single, pair or cluster of different
/// heights, proportions and tilts; no two neighboring groups of the same kind. All of it is small
/// decoration (at most <see cref="MaxHeight"/> tall), so nothing solid changes. Also the lamps'
/// cap shapes and colors (<see cref="LampCapOf"/>) and the city blocks' cap roofs
/// (<see cref="RoofOf"/>), each chosen per spot so neighbors differ.
/// </summary>
public static class PetCityFungi
{
    /// <summary>The fixed seed everything here is chosen from.</summary>
    public const int Seed = 26_10_2026;

    /// <summary>The tallest any fungus is drawn (meters): under the 0.4 m of freely scattered decoration.</summary>
    public const float MaxHeight = 0.38f;

    /// <summary>Groups closer than this (meters) count as neighbors and never share a main kind.</summary>
    public const float NeighborDistance = 4f;

    /// <summary>Lamps closer than this (meters) never share a cap shape or a cap color.</summary>
    public const float LampNeighborDistance = 14f;

    /// <summary>City blocks with a cap roof closer than this (meters) never share a roof shape.</summary>
    public const float RoofNeighborDistance = 40f;

    /// <summary>The lamps' cap shapes (glb objects, each drawn over <c>CityLamp</c>'s post and bulb).</summary>
    public static readonly string[] LampCapModels =
        { "LampCapDome", "LampCapBell", "LampCapFlat", "LampCapFunnel", "LampCapWavy", "LampCapCone" };

    /// <summary>The city blocks' mushroom cap roofs (each at most 3 m tall, like the terrace).</summary>
    public static readonly string[] RoofCapModels = { "RoofCap", "RoofCapWavy", "RoofCapBell", "RoofCapFlat" };

    /// <summary>Where a planter's soil is in its model (meters, of <see cref="PetCityLayout.PropModel"/>'s 0.7).</summary>
    public const float PlanterSoil = 0.53f;

    /// <summary>Where a roof terrace's pots' moss is (meters above the roof's base, before scaling).</summary>
    public const float TerracePotTop = 0.83f;

    /// <summary>The roof terrace's pots, (x, z) in the roof's frame (front toward -Z), as petcity.py builds them.</summary>
    public static readonly Vector2[] TerracePots = { new(-3.5f, -3f), new(-1.5f, -3.4f), new(3.6f, -3.2f), new(-3.8f, 3.4f) };

    private static readonly FungusLook[] s_looks =
    {
        new("FungusFlyAgaric", "dome", 0.2f, 0.37f, 0.85f, 1.25f, 0.25f, new(1f, 0.1f, 0.1f), new(1f, 0.28f, 0.08f), false, false),
        new("FungusBolete", "thick dome", 0.16f, 0.32f, 0.9f, 1.3f, 0.15f, new(0.86f, 0.46f, 0.16f), new(0.92f, 0.58f, 0.2f), false, false),
        new("FungusChanterelle", "funnel", 0.14f, 0.26f, 0.85f, 1.3f, 0.3f, new(1f, 0.72f, 0.1f), new(1f, 0.6f, 0.06f), false, false),
        new("FungusParasol", "flat", 0.26f, 0.38f, 0.85f, 1.2f, 0.2f, new(1f, 0.62f, 0.36f), new(0.98f, 0.5f, 0.3f), false, false),
        new("FungusInkCap", "bell", 0.22f, 0.38f, 0.8f, 1.15f, 0.22f, new(0.98f, 0.95f, 1f), new(0.94f, 0.92f, 1f), false, true),
        new("FungusMorel", "honeycomb cone", 0.16f, 0.3f, 0.85f, 1.2f, 0.2f, new(0.94f, 0.66f, 0.28f), new(0.9f, 0.56f, 0.22f), false, false),
        new("FungusEnoki", "tiny caps on tall stems", 0.2f, 0.36f, 0.8f, 1.3f, 0.15f, new(1f, 0.92f, 0.5f), new(1f, 0.84f, 0.38f), false, false),
        new("FungusAmethyst", "wavy dome", 0.16f, 0.3f, 0.85f, 1.25f, 0.3f, new(0.66f, 0.24f, 1f), new(0.82f, 0.3f, 1f), false, false),
        new("FungusPuffball", "ball", 0.12f, 0.24f, 0.9f, 1.25f, 0.1f, new(1f, 0.97f, 0.88f), new(1f, 0.94f, 0.82f), false, true),
        new("FungusBracket", "shelf", 0.12f, 0.2f, 0.85f, 1.3f, 0.08f, new(1f, 0.5f, 0.14f), new(1f, 0.66f, 0.2f), false, false),
        new("FungusCup", "cup", 0.1f, 0.18f, 0.9f, 1.3f, 0.3f, new(1f, 0.08f, 0.2f), new(1f, 0.16f, 0.3f), false, false),
        new("FungusCoral", "branching", 0.14f, 0.28f, 0.85f, 1.3f, 0.15f, new(1f, 0.45f, 0.6f), new(1f, 0.56f, 0.2f), false, false),
        new("FungusJelly", "lobes", 0.1f, 0.2f, 0.9f, 1.3f, 0.3f, new(1f, 0.66f, 0.06f), new(1f, 0.5f, 0.1f), false, false),
        new("FungusGlowCap", "cones", 0.2f, 0.36f, 0.85f, 1.2f, 0.2f, new(0.2f, 0.8f, 1f), new(0.3f, 1f, 0.55f), true, false),
    };

    /// <summary>The sizes of a group's members relative to its first (a group never repeats a size).</summary>
    private static readonly float[] s_tiers = { 1f, 0.76f, 0.6f, 0.88f, 0.68f };

    private static readonly FungusGroup[] s_groups;
    private static readonly Fungus[] s_fungi;
    private static readonly int[] s_lampCaps;
    private static readonly int[] s_lampColors;
    private static readonly int[] s_roofs;

    static PetCityFungi()
    {
        (s_groups, s_fungi) = Build();
        (s_lampCaps, s_lampColors) = PickLampCaps();
        s_roofs = PickRoofs();
    }

    /// <summary>Every kind's look, indexed by <see cref="FungusKind"/>.</summary>
    public static ReadOnlySpan<FungusLook> Looks => s_looks;

    public static FungusLook Look(FungusKind kind) => s_looks[(int)kind];

    /// <summary>Every fungus drawn, group by group.</summary>
    public static ReadOnlySpan<Fungus> All => s_fungi;

    /// <summary>Every group (spot) of fungi.</summary>
    public static ReadOnlySpan<FungusGroup> Groups => s_groups;

    /// <summary>
    /// The cap of the lamp <see cref="PetCityLayout.CityProps"/>[<paramref name="cityProp"/>]: the glb
    /// object (one of <see cref="LampCapModels"/>) and its color (one of <see cref="PetCityLook.LampCaps"/>);
    /// null for a prop that is not a lamp.
    /// </summary>
    public static (string Model, Swatch Color)? LampCapOf(int cityProp) =>
        s_lampCaps[cityProp] < 0 ? null : (LampCapModels[s_lampCaps[cityProp]], PetCityLook.LampCaps[s_lampColors[cityProp]]);

    /// <summary>
    /// The roof of the piece <see cref="PetCityLayout.Pieces"/>[<paramref name="piece"/>] (a city block):
    /// <c>RoofTerrace</c> for odd styles, else one of <see cref="RoofCapModels"/>; null for the pet store.
    /// </summary>
    public static string? RoofOf(int piece) => s_roofs[piece] switch
    {
        -2 => null,
        -1 => "RoofTerrace",
        int k => RoofCapModels[k],
    };

    /// <summary>A small deterministic generator (SplitMix64), the same on every machine.</summary>
    private struct Rng
    {
        private ulong _state;

        public Rng(int seed, int stream) => _state = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL ^ (ulong)(uint)stream * 0xD1B54A32D192ED03UL;

        public uint Next()
        {
            ulong z = _state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return (uint)((z ^ (z >> 31)) >> 32);
        }

        /// <summary>0 (inclusive) .. 1 (exclusive).</summary>
        public float Float() => (Next() >> 8) / 16777216f;

        public float Range(float a, float b) => a + (b - a) * Float();

        public int Int(int n) => (int)(Next() % (uint)n);
    }

    private readonly record struct Spot(FungusHost Host, Vector3 Center, float Spread, float Outward);

    /// <summary>The spots, in a fixed order: planters, lamps (foot, post), buildings (their sides and back), terraces.</summary>
    private static List<Spot> Spots()
    {
        var spots = new List<Spot>();
        var props = PetCityLayout.CityProps;
        for (int i = 0; i < props.Length; i++)
        {
            var p = props[i];
            if (p.Kind == PropKind.Planter)
            {
                var model = PetCityLayout.PropModel(p.Kind)!.Value;
                float y = p.Position.Y + PlanterSoil * PetCityLayout.DrawnHeight(p) / model.Height;
                spots.Add(new Spot(FungusHost.Planter, new Vector3(p.Position.X, y, p.Position.Z), p.Radius * 0.35f, 0f));
            }
            else if (p.Kind == PropKind.Lamp)
            {
                float a = p.PropIndex * 2.39996f;
                var d = new Vector2(MathF.Sin(a), MathF.Cos(a));
                float r = p.Radius + 0.28f;
                float x = p.Position.X + d.X * r, z = p.Position.Z + d.Y * r;
                spots.Add(new Spot(FungusHost.LampFoot, new Vector3(x, Ground.HeightAt(x, z), z), 0.18f, a));
                if (i % 3 == 0)
                {
                    var model = PetCityLayout.PropModel(p.Kind)!.Value;
                    float scale = PetCityLayout.DrawnHeight(p) / model.Height;
                    // On the post, between 0.7 m and 1.5 m up, facing away from the foot's group.
                    float h = 0.7f + 0.8f * ((p.PropIndex * 7 % 10) / 10f);
                    float post = PostRadius(h) * (p.Radius / model.Radius);
                    float b = a + MathF.PI;
                    spots.Add(new Spot(FungusHost.LampPost,
                        new Vector3(p.Position.X + MathF.Sin(b) * post, p.Position.Y + h * scale, p.Position.Z + MathF.Cos(b) * post), post, b));
                }
            }
        }
        var pieces = PetCityLayout.Pieces;
        for (int n = 0; n < pieces.Length; n++)
        {
            var piece = pieces[n];
            var b = Buildings.All[piece.BuildingIndex];
            float off = PetCityLayout.PlinthOverhang + 0.5f;
            float hw = b.Width * 0.5f, hd = b.Depth * 0.5f;
            // The two sides (from just behind the front corner to the back) and the back; never the front.
            for (float lz = -hd + 1.5f; lz <= hd + 0.01f; lz += 2.6f)
            {
                AddFoot(spots, b, -hw - off, lz);
                AddFoot(spots, b, hw + off, lz);
            }
            for (float lx = -hw + 1.8f; lx <= hw - 1.7f; lx += 2.8f)
                AddFoot(spots, b, lx, hd + off);
            if (RoofOfStyle(piece) == "RoofTerrace")
            {
                float sx = piece.Size.X / 12f, sz = piece.Size.Z / 12f;
                float sy = piece.Size.Y / PetCityLayout.ModelHeight(piece.Stories);
                float top = piece.Footing.High + sy * (PetCityLayout.GroundFloorHeight + piece.Stories * PetCityLayout.StoryHeight + TerracePotTop);
                foreach (var pot in TerracePots)
                {
                    var w = Rotate(new Vector2(pot.X * sx, pot.Y * sz), piece.Yaw);
                    spots.Add(new Spot(FungusHost.Terrace, new Vector3(piece.Position.X + w.X, top, piece.Position.Z + w.Y), 0.22f, 0f));
                }
            }
        }
        return spots;
    }

    /// <summary>The city lamp's post radius at <paramref name="h"/> meters up, in its model (petcity.py: 0.09 at the foot, 0.07 at 2.25 m).</summary>
    public static float PostRadius(float h) => 0.09f - 0.02f * Math.Clamp((h - 0.15f) / 2.1f, 0f, 1f);

    private static void AddFoot(List<Spot> spots, in Building b, float lx, float lz)
    {
        var p = b.ToWorld(lx, lz);
        spots.Add(new Spot(FungusHost.BuildingFoot, new Vector3(p.X, Ground.HeightAt(p.X, p.Y), p.Y), 0.32f, 0f));
    }

    /// <summary>A local (x, z) turned by <paramref name="yaw"/> about +Y, as Godot turns it.</summary>
    public static Vector2 Rotate(Vector2 local, float yaw)
    {
        float c = MathF.Cos(yaw), s = MathF.Sin(yaw);
        return new Vector2(local.X * c + local.Y * s, -local.X * s + local.Y * c);
    }

    private static string? RoofOfStyle(in Piece piece) =>
        piece.Kind != PieceKind.CityBlock ? null : piece.Style % 2 == 0 ? "RoofCap" : "RoofTerrace";

    private static bool Allowed(FungusKind kind, FungusHost host) => host == FungusHost.LampPost
        ? kind == FungusKind.Bracket
        : kind != FungusKind.Bracket;

    /// <summary>Plants every group from <see cref="Seed"/> afresh (<see cref="All"/> and <see cref="Groups"/> hold the result).</summary>
    public static (FungusGroup[] Groups, Fungus[] Fungi) Build()
    {
        var spots = Spots();
        var groups = new FungusGroup[spots.Count];
        var fungi = new List<Fungus>();
        int kinds = s_looks.Length;
        for (int g = 0; g < spots.Count; g++)
        {
            var spot = spots[g];
            var rng = new Rng(Seed, g);
            // The main kind: one no neighboring group has.
            FungusKind main = FungusKind.Bracket;
            if (spot.Host != FungusHost.LampPost)
            {
                for (int tries = 0; ; tries++)
                {
                    main = (FungusKind)rng.Int(kinds);
                    if (!Allowed(main, spot.Host))
                        continue;
                    bool clash = false;
                    for (int k = 0; k < g && !clash; k++)
                        clash = groups[k].Main == main && Vector3.Distance(groups[k].Center, spot.Center) < NeighborDistance;
                    if (!clash || tries > 200)
                        break;
                }
            }
            // A single mushroom, a pair or a cluster (brackets: one to three shelves up the post).
            float roll = rng.Float();
            int count = spot.Host == FungusHost.LampPost ? 1 + rng.Int(3)
                : roll < 0.3f ? 1 : roll < 0.62f ? 2 : 3 + rng.Int(spot.Host == FungusHost.Planter || spot.Host == FungusHost.Terrace ? 2 : 3);
            int first = fungi.Count;
            float postSize = 0f;
            for (int m = 0; m < count; m++)
            {
                var kind = main;
                // Clusters mix in other kinds.
                if (m > 0 && spot.Host != FungusHost.LampPost && rng.Float() < 0.5f)
                {
                    do kind = (FungusKind)rng.Int(kinds);
                    while (!Allowed(kind, spot.Host));
                }
                var look = s_looks[(int)kind];
                if (m == 0)
                    postSize = rng.Range(look.MinHeight, look.MaxHeight);
                float height = Math.Clamp((spot.Host == FungusHost.LampPost ? postSize : rng.Range(look.MinHeight, look.MaxHeight)) * s_tiers[m], 0.05f, MaxHeight);
                // No two of a group alike: one of the same kind and nearly the same size becomes another kind.
                while (spot.Host != FungusHost.LampPost && Twin(fungi, first, kind, height))
                {
                    do kind = (FungusKind)rng.Int(kinds);
                    while (!Allowed(kind, spot.Host));
                    look = s_looks[(int)kind];
                    height = Math.Clamp(rng.Range(look.MinHeight, look.MaxHeight) * s_tiers[m], 0.05f, MaxHeight);
                }
                float width = rng.Range(look.MinWidth, look.MaxWidth);
                float t = rng.Float();
                var tint = new Swatch(look.TintA.R + (look.TintB.R - look.TintA.R) * t, look.TintA.G + (look.TintB.G - look.TintA.G) * t,
                    look.TintA.B + (look.TintB.B - look.TintA.B) * t);
                // As bright as its kind's colors (mixing two can dull it).
                float bright = (look.TintA.Value + (look.TintB.Value - look.TintA.Value) * t) / tint.Value;
                tint = new Swatch(MathF.Min(1f, tint.R * bright), MathF.Min(1f, tint.G * bright), MathF.Min(1f, tint.B * bright));
                Vector3 at;
                float yaw, tilt, tiltYaw;
                if (spot.Host == FungusHost.LampPost)
                {
                    // Shelves up the post, each turned a little around it.
                    float b = spot.Outward + (m - 1) * 0.9f;
                    var dir = new Vector2(MathF.Sin(b), MathF.Cos(b));
                    var axis = new Vector2(spot.Center.X - MathF.Sin(spot.Outward) * spot.Spread, spot.Center.Z - MathF.Cos(spot.Outward) * spot.Spread);
                    at = new Vector3(axis.X + dir.X * spot.Spread, spot.Center.Y + m * 0.16f, axis.Y + dir.Y * spot.Spread);
                    // Sticks out along dir: the model's -Z turned to dir.
                    yaw = MathF.Atan2(-dir.X, -dir.Y);
                    tilt = rng.Range(0f, look.MaxTilt);
                    tiltYaw = yaw;
                }
                else
                {
                    float a = rng.Range(0f, MathF.Tau);
                    float r = m == 0 && count == 1 ? 0f : spot.Spread * (0.45f + 0.55f * rng.Float());
                    if (m > 0)
                        a = MathF.Tau * m / count + rng.Range(-0.4f, 0.4f);
                    at = spot.Center + new Vector3(MathF.Sin(a) * r, 0f, MathF.Cos(a) * r);
                    if (spot.Host == FungusHost.BuildingFoot || spot.Host == FungusHost.LampFoot)
                        at.Y = Ground.HeightAt(at.X, at.Z) - 0.02f;
                    yaw = rng.Range(0f, MathF.Tau);
                    tilt = rng.Range(0.03f, look.MaxTilt);
                    // Outer members lean outward, like a real clump.
                    tiltYaw = m == 0 ? rng.Range(0f, MathF.Tau) : a;
                }
                fungi.Add(new Fungus(kind, spot.Host, g, at, yaw, tilt, tiltYaw, height, width, tint));
            }
            groups[g] = new FungusGroup(spot.Host, main, spot.Center, first, count);
        }
        return (groups, fungi.ToArray());
    }

    /// <summary>The share by which two fungi of a kind must differ in height not to count as alike.</summary>
    public const float AlikeHeight = 0.12f;

    private static bool Twin(List<Fungus> fungi, int first, FungusKind kind, float height)
    {
        for (int k = first; k < fungi.Count; k++)
            if (fungi[k].Kind == kind && MathF.Abs(fungi[k].Height - height) < AlikeHeight * MathF.Max(fungi[k].Height, height))
                return true;
        return false;
    }

    /// <summary>A random choice among the <paramref name="n"/> choices least used in <paramref name="chosen"/>[..count], so every one shows.</summary>
    private static int LeastUsed(int[] chosen, int count, int n, ref Rng rng)
    {
        Span<int> uses = stackalloc int[n];
        for (int i = 0; i < count; i++)
            if (chosen[i] >= 0)
                uses[chosen[i]]++;
        int fewest = int.MaxValue;
        foreach (int u in uses)
            fewest = Math.Min(fewest, u);
        // Every second try also allows the next-least used, so a clash with a neighbor can be avoided.
        int start = rng.Int(n);
        for (int k = 0; k < n; k++)
        {
            int c = (start + k) % n;
            if (uses[c] <= fewest + (rng.Int(2)))
                return c;
        }
        return start;
    }

    private static (int[], int[]) PickLampCaps()
    {
        var props = PetCityLayout.CityProps;
        var caps = new int[props.Length];
        var colors = new int[props.Length];
        for (int i = 0; i < props.Length; i++)
        {
            caps[i] = colors[i] = -1;
            if (props[i].Kind != PropKind.Lamp)
                continue;
            var rng = new Rng(Seed, 100_000 + i);
            for (int tries = 0; ; tries++)
            {
                int cap = LeastUsed(caps, i, LampCapModels.Length, ref rng), color = rng.Int(PetCityLook.LampCaps.Length);
                bool clash = false;
                for (int k = 0; k < i && !clash; k++)
                    clash = caps[k] >= 0 && (caps[k] == cap || colors[k] == color)
                        && Vector2.Distance(new(props[k].Position.X, props[k].Position.Z), new(props[i].Position.X, props[i].Position.Z)) < LampNeighborDistance;
                if (!clash || tries > 500)
                {
                    caps[i] = cap;
                    colors[i] = color;
                    break;
                }
            }
        }
        return (caps, colors);
    }

    private static int[] PickRoofs()
    {
        var pieces = PetCityLayout.Pieces;
        var roofs = new int[pieces.Length];
        for (int i = 0; i < pieces.Length; i++)
        {
            string? roof = RoofOfStyle(pieces[i]);
            roofs[i] = roof == null ? -2 : -1;
            if (roof != "RoofCap")
                continue;
            var rng = new Rng(Seed, 200_000 + i);
            for (int tries = 0; ; tries++)
            {
                int k = LeastUsed(roofs, i, RoofCapModels.Length, ref rng);
                bool clash = false;
                for (int j = 0; j < i && !clash; j++)
                    clash = roofs[j] == k && Vector3.Distance(pieces[j].Position, pieces[i].Position) < RoofNeighborDistance;
                if (!clash || tries > 200)
                {
                    roofs[i] = k;
                    break;
                }
            }
        }
        return roofs;
    }
}
