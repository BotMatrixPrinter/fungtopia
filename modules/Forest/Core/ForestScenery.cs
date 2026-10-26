using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Map;

namespace Fungtopia.Forest;

/// <summary>
/// What the view draws standing in the forest, and how big: a giant mushroom on every forest
/// trunk (<see cref="Giants"/>, stem exactly the trunk's circle), a medium mushroom on every Map
/// prop of kind <see cref="PropKind.MediumMushroom"/> in <see cref="Area.Forest"/>
/// (<see cref="MediumMushrooms"/>, exactly at the prop, stem radius the prop's radius, as tall as
/// the prop), and small walkable ground decoration (<see cref="Decoration"/>: tiny mushrooms,
/// ferns, flowers, grass tufts), every piece lower than <see cref="DecorationMaxHeight"/> and
/// narrower than <see cref="DecorationMaxRadius"/>. Anything taller stands on an obstacle.
/// Fixed: the same every run.
/// </summary>
public static class ForestScenery
{
    /// <summary>Decoration (meters) is always lower than this: anything taller is solid and comes from the Map.</summary>
    public const float DecorationMaxHeight = 0.4f;

    /// <summary>Decoration's footprint radius (meters) is always smaller than this (0.6 m across).</summary>
    public const float DecorationMaxRadius = 0.3f;

    /// <summary>Height (meters) of the fern mesh at scale 1.</summary>
    public const float FernMeshHeight = 0.5f;

    /// <summary>Footprint radius (meters) of the fern mesh at scale 1.</summary>
    public const float FernMeshRadius = 0.6f;

    /// <summary>Height (meters) of the tallest blade of the grass tuft mesh at scale 1.</summary>
    public const float GrassMeshHeight = 0.32f;

    /// <summary>Footprint radius (meters) of the grass tuft mesh at scale 1.</summary>
    public const float GrassMeshRadius = 0.16f;

    /// <summary>Height (meters) of the flower mesh at scale 1.</summary>
    public const float FlowerMeshHeight = 1f;

    /// <summary>Footprint radius (meters) of the flower mesh at scale 1.</summary>
    public const float FlowerMeshRadius = 0.6f;

    /// <summary>The widest a stem's meshes get at radius scale 1 (the root flare's base): never wider than its circle.</summary>
    public const float StemMeshMaxRadius = 1f;

    /// <summary>The stem mesh's radius at its foot, a little inside <see cref="StemMeshMaxRadius"/> so a root flare shows round it.</summary>
    public const float StemFootRadius = 0.94f;

    /// <summary>The stem mesh's radius at height fraction <paramref name="t"/> (0 foot .. 1 top), at most <see cref="StemFootRadius"/>.</summary>
    public static float StemProfile(float t) => 0.68f + 0.26f * MathF.Pow(1f - t, 4f) - 0.1f * t;

    /// <summary>
    /// The root flare's radius at height fraction <paramref name="t"/> (0 base .. 1 top) at radius
    /// scale 1: <see cref="StemMeshMaxRadius"/>, wider than the stem's foot, from its base up to
    /// <see cref="ForestFooting.SkirtGroundFraction"/> (where <see cref="ForestFooting.Plant"/> puts
    /// the high side of the ground), then narrowing smoothly to <see cref="ForestFooting.SkirtTopRadius"/>
    /// at its top, inside the stem there.
    /// </summary>
    public static float SkirtProfile(float t)
    {
        float u = Math.Clamp((t - ForestFooting.SkirtGroundFraction) / (1f - ForestFooting.SkirtGroundFraction), 0f, 1f);
        return StemMeshMaxRadius - (StemMeshMaxRadius - ForestFooting.SkirtTopRadius) * u * u * (3f - 2f * u);
    }

    /// <summary>The steepest a stem leans: its top this many meters aside per meter of stem.</summary>
    public const float MaxLean = 0.22f;

    /// <summary>The steepest a giant's stem leans (they are tall: a little goes a long way).</summary>
    public const float MaxGiantLean = 0.06f;

    /// <summary>
    /// A stem's radius at height fraction <paramref name="t"/> (0 foot .. 1 top) at radius scale 1
    /// when it is <paramref name="slender"/> (1: the full <see cref="StemProfile"/>; less: it narrows
    /// above its foot to that part of it). The foot keeps the full profile, so the stem's footprint
    /// stays its circle. The stem shader draws the same.
    /// </summary>
    public static float StemRadiusAt(float slender, float t)
    {
        float u = Math.Clamp((t - 0.1f) / 0.5f, 0f, 1f);
        return StemProfile(t) * (1f - (1f - slender) * u * u * (3f - 2f * u));
    }

    /// <summary>How far (x, z, meters) the axis of <paramref name="m"/>'s stem of <paramref name="stemHeight"/> lies aside at height fraction <paramref name="t"/>: its <see cref="DrawnMushroom.Lean"/> bends it along its local +X (Godot's yaw), growing with t squared.</summary>
    public static Vector2 LeanOffset(in DrawnMushroom m, float stemHeight, float t)
    {
        float d = m.Lean * stemHeight * t * t;
        return new Vector2(MathF.Cos(m.Yaw) * d, -MathF.Sin(m.Yaw) * d);
    }

    /// <summary>The point on the surface of <paramref name="m"/>'s stem (planted as <paramref name="p"/>) at height fraction <paramref name="t"/>, toward <paramref name="angle"/> (radians from +X toward +Z).</summary>
    public static Vector3 StemPoint(in DrawnMushroom m, in PlantedMushroom p, float t, float angle)
    {
        var axis = LeanOffset(m, p.StemHeight, t);
        float r = m.StemRadius * StemRadiusAt(m.Slender, t);
        return new Vector3(m.X + axis.X + MathF.Cos(angle) * r, p.BaseY + p.StemHeight * t, m.Z + axis.Y + MathF.Sin(angle) * r);
    }

    private static readonly DrawnMushroom[] s_giants;
    private static readonly MushroomCluster[] s_clusters;
    private static readonly DrawnMushroom[] s_medium;
    private static readonly DrawnMushroom[] s_ring;
    private static readonly Shelf[] s_shelves;
    private static readonly Decoration[] s_decoration;
    private static MushroomCluster[] s_patches = Array.Empty<MushroomCluster>();

    static ForestScenery()
    {
        // One kind per tall spot (giants, the Map's medium props, the ring), never the same as
        // any of its nearest neighbors'.
        var trunks = Obstacles.All;
        var ring = ForestRing.Mushrooms;
        var props = new List<Prop>();
        foreach (var p in Props.All)
            if (p.Kind == PropKind.MediumMushroom && p.Area == Area.Forest)
                props.Add(p);
        int giants = Ground.ForestTrunkCount;
        var spots = new Vector2[giants + props.Count + ring.Length];
        var ids = new int[spots.Length];
        var allowed = new MushroomKind[spots.Length][];
        var giantKinds = ForestSpecies.For(Tier.Giant);
        var mediumKinds = ForestSpecies.For(Tier.Medium);
        for (int i = 0; i < giants; i++)
            (spots[i], ids[i], allowed[i]) = (new Vector2(trunks[i].X, trunks[i].Z), i, giantKinds);
        for (int i = 0; i < props.Count; i++)
            (spots[giants + i], ids[giants + i], allowed[giants + i]) = (new Vector2(props[i].X, props[i].Z), 20000 + i, mediumKinds);
        for (int i = 0; i < ring.Length; i++)
            (spots[giants + props.Count + i], ids[giants + props.Count + i], allowed[giants + props.Count + i]) = (new Vector2(ring[i].X, ring[i].Z), 200000 + ring[i].Id, giantKinds);
        var kinds = AssignKinds(spots, ids, allowed, 90);

        s_giants = PlaceGiants(kinds);
        (s_clusters, s_medium) = PlaceMedium(props, kinds.AsSpan(giants, props.Count));
        s_ring = PlaceRing(kinds.AsSpan(giants + props.Count));
        s_shelves = PlaceShelves();
        s_decoration = PlaceDecoration();
    }

    /// <summary>One giant mushroom per forest trunk (the first <see cref="Ground.ForestTrunkCount"/> of <see cref="Obstacles.All"/>), in order.</summary>
    public static ReadOnlySpan<DrawnMushroom> Giants => s_giants;

    /// <summary>
    /// Every mushroom of the medium clusters, cluster by cluster (<see cref="MediumClusters"/>):
    /// one, two, a few or a tuft of one kind on each Map prop of kind MediumMushroom in the forest,
    /// every stem inside the prop's circle.
    /// </summary>
    public static ReadOnlySpan<DrawnMushroom> MediumMushrooms => s_medium;

    /// <summary>One cluster per Map prop of kind MediumMushroom in the forest, in <see cref="Props.All"/> order.</summary>
    public static ReadOnlySpan<MushroomCluster> MediumClusters => s_clusters;

    /// <summary>One giant mushroom per <see cref="ForestRing.Mushrooms"/>, in order, stem exactly its circle.</summary>
    public static ReadOnlySpan<DrawnMushroom> RingMushrooms => s_ring;

    /// <summary>Shelf (bracket) fungi growing out of some giants' stems.</summary>
    public static ReadOnlySpan<Shelf> Shelves => s_shelves;

    /// <summary>Small walkable ground decoration, every piece under <see cref="DecorationMaxHeight"/>.</summary>
    public static ReadOnlySpan<Decoration> Decoration => s_decoration;

    /// <summary>The patches of tiny fungi (one kind each, never the kind of the nearest patches'): their spot, about <see cref="PatchRadius"/> across, tallest one's height and their pieces of <see cref="Decoration"/> (<c>Start</c>, <c>Count</c>).</summary>
    public static ReadOnlySpan<MushroomCluster> SmallPatches => s_patches;

    /// <summary>How far (meters) from its spot a patch of tiny fungi spreads.</summary>
    public const float PatchRadius = 0.45f;

    private static float Rand(int i, int k) => ForestRandom.Value(i, k);

    private static float Lerp((float Min, float Max) range, float t) => range.Min + (range.Max - range.Min) * t;

    /// <summary>
    /// A kind for each spot from <paramref name="allowed"/>, picked by a fixed hash of its id and
    /// never the kind of any of its <see cref="Neighbors"/> nearest spots (nor of a spot that has
    /// it among its nearest).
    /// </summary>
    private static MushroomKind[] AssignKinds(Vector2[] spots, int[] ids, MushroomKind[][] allowed, int salt)
    {
        int n = spots.Length;
        var near = new List<int>[n];
        for (int i = 0; i < n; i++)
            near[i] = new List<int>();
        var best = new int[Neighbors];
        var bestD = new float[Neighbors];
        for (int i = 0; i < n; i++)
        {
            Array.Fill(best, -1);
            Array.Fill(bestD, float.MaxValue);
            for (int j = 0; j < n; j++)
            {
                if (j == i)
                    continue;
                float d = Vector2.DistanceSquared(spots[i], spots[j]);
                if (d >= bestD[Neighbors - 1])
                    continue;
                int k = Neighbors - 1;
                while (k > 0 && bestD[k - 1] > d)
                {
                    best[k] = best[k - 1];
                    bestD[k] = bestD[k - 1];
                    k--;
                }
                best[k] = j;
                bestD[k] = d;
            }
            foreach (int j in best)
                if (j >= 0 && !near[i].Contains(j))
                {
                    near[i].Add(j);
                    near[j].Add(i);
                }
        }
        var kinds = new MushroomKind[n];
        for (int i = 0; i < n; i++)
        {
            var options = allowed[i];
            int first = (int)(Rand(ids[i], salt) * options.Length);
            int fewest = int.MaxValue;
            for (int t = 0; t < options.Length; t++)
            {
                var kind = options[(first + t) % options.Length];
                int clashes = 0;
                foreach (int j in near[i])
                    if (j < i && kinds[j] == kind)
                        clashes++;
                if (clashes < fewest)
                {
                    fewest = clashes;
                    kinds[i] = kind;
                }
                if (clashes == 0)
                    break;
            }
        }
        return kinds;
    }

    /// <summary>How many nearest spots each spot's kind differs from.</summary>
    public const int Neighbors = 3;

    private static DrawnMushroom Giant(MushroomKind kind, float x, float z, float stemRadius, float height, int id)
    {
        var look = ForestSpecies.Of(kind);
        var shape = look.Shapes[(int)(Rand(id, 24) * look.Shapes.Length)];
        float capRadius = stemRadius * Lerp(look.GiantCap, Rand(id, 21));
        float capHeight = MathF.Min(capRadius * Lerp(look.CapDepth, Rand(id, 6)), height * 0.5f);
        float lean = MaxGiantLean * Rand(id, 25) * Rand(id, 26);
        var m = new DrawnMushroom(DecorKind.Mushroom, x, z, Rand(id, 1) * MathF.Tau, stemRadius, height, capRadius, capHeight, id,
            kind, shape, lean, Lerp(look.Slender, Rand(id, 27)));
        return FitFlare(m);
    }

    /// <summary><paramref name="m"/>, its stem no slimmer than lets a root flare hide its top inside it.</summary>
    private static DrawnMushroom FitFlare(DrawnMushroom m)
    {
        var planted = ForestFooting.Plant(m);
        if (planted.SkirtHeight > 0f)
        {
            float t = planted.SkirtHeight / planted.StemHeight;
            while (m.Slender < 1f && StemRadiusAt(m.Slender, t) < ForestFooting.SkirtTopRadius + 0.04f)
                m = m with { Slender = MathF.Min(1f, m.Slender + 0.02f) };
        }
        return m;
    }

    private static DrawnMushroom[] PlaceGiants(MushroomKind[] kinds)
    {
        var all = Obstacles.All;
        var giants = new DrawnMushroom[Ground.ForestTrunkCount];
        for (int i = 0; i < giants.Length; i++)
        {
            var o = all[i];
            float height = (6f + o.Radius * 2.6f + 5f * Rand(i, 20)) * ForestSpecies.Of(kinds[i]).GiantHeight;
            giants[i] = Giant(kinds[i], o.X, o.Z, o.Radius, MathF.Max(height, 5.5f), i);
        }
        return giants;
    }

    private static DrawnMushroom[] PlaceRing(ReadOnlySpan<MushroomKind> kinds)
    {
        var ring = ForestRing.Mushrooms;
        var drawn = new DrawnMushroom[ring.Length];
        for (int i = 0; i < ring.Length; i++)
        {
            var r = ring[i];
            var look = ForestSpecies.Of(kinds[i]);
            float capRadius = r.Radius * Lerp(look.GiantCap, Rand(r.Id, 34));
            float capHeight = capRadius * Lerp(look.CapDepth, Rand(r.Id, 6));
            float height = (7f + 9f * Rand(r.Id, 33)) * look.GiantHeight + MathF.Min(capHeight, 6f) * 0.82f;
            drawn[i] = Giant(kinds[i], r.X, r.Z, r.Radius, MathF.Max(height, 7.5f), r.Id) with { Yaw = Rand(r.Id, 1) * MathF.Tau };
        }
        return drawn;
    }

    /// <summary>How many mushrooms grow on a spot of <paramref name="kind"/>: one or two, a few, or a tuft of many thin ones.</summary>
    private static int Members(MushroomKind kind, int id, int salt, bool small)
    {
        var look = ForestSpecies.Of(kind);
        float r = Rand(id, salt);
        return look.Habit switch
        {
            Habit.Tuft => (small ? 4 : 7) + (int)(Rand(id, salt + 1) * 5f),
            Habit.SingleOrPair => r < 0.55f ? 1 : 2,
            _ => r < 0.4f ? 1 : r < 0.7f ? 2 : 3 + (int)(Rand(id, salt + 1) * (look.Stemless ? 1f : 3f)),
        };
    }

    private static (MushroomCluster[], DrawnMushroom[]) PlaceMedium(List<Prop> props, ReadOnlySpan<MushroomKind> kinds)
    {
        var clusters = new MushroomCluster[props.Count];
        var list = new List<DrawnMushroom>();
        int id = 20000;
        for (int c = 0; c < props.Count; c++)
        {
            var p = props[c];
            var kind = kinds[c];
            var look = ForestSpecies.Of(kind);
            int start = list.Count, count = Members(kind, 20000 + c, 91, small: false);
            // Stemless bodies are as wide as they are tall: the whole cluster keeps inside the circle.
            // (Alone, when a few would stand lower than the Map's smallest medium mushroom.)
            if (look.Stemless && count > 1 && p.Radius * 0.7f / look.CapPerHeight.Max < MathF.Min(p.Height, 0.8f))
                count = 1;
            float top = !look.Stemless ? p.Height
                : MathF.Min(p.Height, count == 1 ? p.Radius / look.CapPerHeight.Min : p.Radius * 0.7f / look.CapPerHeight.Max);
            for (int k = 0; k < count; k++, id++)
            {
                float h = k == 0 ? top : top * (0.42f + 0.4f * Rand(id, 10));
                float capRadius = h * Lerp(look.CapPerHeight, Rand(id, 21));
                float stemRadius, along, lean;
                if (look.Habit == Habit.Tuft)
                {
                    stemRadius = p.Radius * (0.07f + 0.03f * Rand(id, 11));
                    along = k == 0 ? 0f : p.Radius * (0.22f + 0.45f * (k * 0.618034f % 1f) + 0.03f * Rand(id, 12));
                    lean = k == 0 ? 0.02f : 0.08f + 0.12f * Rand(id, 13);
                    capRadius = MathF.Max(capRadius, stemRadius * 1.6f);
                }
                else if (count == 1)
                {
                    if (look.Stemless)
                        capRadius = Math.Clamp(capRadius, 0.62f * p.Radius, p.Radius);
                    stemRadius = look.Stemless ? capRadius * look.StemOfCap : p.Radius * (0.72f + 0.28f * Rand(id, 11));
                    capRadius = MathF.Max(capRadius, stemRadius * 1.5f);
                    along = 0f;
                    lean = 0.08f * Rand(id, 13);
                }
                else
                {
                    stemRadius = look.Stemless ? capRadius * look.StemOfCap
                        : MathF.Min(p.Radius * (count == 2 ? 0.42f : 0.32f) * (k == 0 ? 1.1f : 0.85f), capRadius * 0.7f);
                    capRadius = MathF.Max(capRadius, stemRadius * 1.4f);
                    float room = p.Radius - (look.Stemless ? capRadius : stemRadius);
                    along = k == 0 && count > 2 ? room * 0.25f : room * (0.85f + 0.15f * Rand(id, 12));
                    lean = 0.04f + 0.1f * Rand(id, 13);
                }
                float angle = (Rand(c, 92) + k / (float)count) * MathF.Tau + 0.4f * Rand(id, 14);
                float x = p.X + MathF.Cos(angle) * along, z = p.Z + MathF.Sin(angle) * along;
                // Leaning outward, away from the cluster's middle (local +X toward the lean).
                float yaw = along > 0f ? MathF.Atan2(-MathF.Sin(angle), MathF.Cos(angle)) : p.Rotation;
                var shape = look.Shapes[(int)(Rand(id, 24) * look.Shapes.Length)];
                float capHeight = look.Stemless ? h * 0.5f : MathF.Min(capRadius * Lerp(look.CapDepth, Rand(id, 6)), h * 0.5f);
                list.Add(FitFlare(new DrawnMushroom(DecorKind.Mushroom, x, z, yaw, stemRadius, h, capRadius, capHeight, id,
                    kind, shape, look.Stemless ? 0f : lean, Lerp(look.Slender, Rand(id, 27)))));
            }
            clusters[c] = new MushroomCluster(kind, p.X, p.Z, p.Radius, p.Height, start, list.Count - start);
        }
        return (clusters, list.ToArray());
    }

    private static Shelf[] PlaceShelves()
    {
        // Stacks of one to four brackets up one side of about two giant stems in five.
        var list = new List<Shelf>();
        for (int i = 0; i < s_giants.Length; i++)
        {
            if (Rand(i, 100) > 0.4f)
                continue;
            int count = 1 + (int)(Rand(i, 101) * 4f);
            float side = Rand(i, 102) * MathF.Tau;
            for (int k = 0; k < count; k++)
            {
                int id = i * 8 + k;
                float t = 0.16f + 0.1f * k + 0.08f * Rand(id, 103);
                float radius = s_giants[i].StemRadius * (0.5f + 0.25f * Rand(id, 104)) * (1f - 0.1f * k);
                list.Add(new Shelf(i, side + (k % 2 == 0 ? -0.35f : 0.35f) * Rand(id, 105), t, radius, radius * (0.22f + 0.08f * Rand(id, 106)), id));
            }
        }
        return list.ToArray();
    }

    /// <summary>A tiny decoration mushroom (<see cref="DecorKind.Mushroom"/> or <see cref="DecorKind.DarkMushroom"/>) as the view draws it, its stem its kind's part of its cap.</summary>
    public static DrawnMushroom AsMushroom(in Decoration d) =>
        new(d.Kind, d.X, d.Z, d.Yaw, d.Radius * ForestSpecies.Of(d.Species).StemOfCap, d.Height, d.Radius, d.CapHeight, d.Id + 7,
            d.Species, d.Shape, d.Lean, 1f);

    /// <summary>Spots for the tiny ground fungi: through the open forest, along the paths' edges and in the clearings.</summary>
    private static List<Vector2> SmallSpots()
    {
        var spots = new List<Vector2>();
        for (int c = 0; c < 420; c++)
        {
            float cx = (Rand(c, 40) * 2f - 1f) * ForestBounds.HalfSize, cz = (Rand(c, 41) * 2f - 1f) * ForestBounds.HalfSize;
            if (ForestProps.Open(cx, cz, 0.5f))
                spots.Add(new Vector2(cx, cz));
        }
        // Along the paths: every few meters, on alternating sides, just off the dirt.
        var points = ForestLayout.PathPoints;
        int s = 0;
        for (int i = 0; i + 1 < points.Length; i++)
        {
            var a = points[i];
            var b = points[i + 1];
            if (float.IsNaN(a.X) || float.IsNaN(b.X))
                continue;
            float length = Vector2.Distance(a, b);
            var dir = (b - a) / length;
            var normal = new Vector2(-dir.Y, dir.X);
            for (float u = 0f; u < length; u += 3.2f, s++)
            {
                float side = (s % 2 == 0 ? 1f : -1f) * (ForestLayout.PathHalfWidth + 0.55f + 0.9f * Rand(s, 110));
                var p = a + dir * (u + 1.2f * Rand(s, 111)) + normal * side;
                if (ForestProps.Open(p.X, p.Y, 0.4f))
                    spots.Add(p);
            }
        }
        // In the clearings' meadows.
        int k = 0;
        foreach (var clearing in ForestLayout.Clearings)
        {
            int count = (int)(clearing.Radius * 1.2f);
            for (int i = 0; i < count; i++, k++)
            {
                float a = Rand(k, 112) * MathF.Tau, d = clearing.Radius * 0.85f * MathF.Sqrt(Rand(k, 113));
                var p = new Vector2(clearing.X + MathF.Cos(a) * d, clearing.Z + MathF.Sin(a) * d);
                if (ForestProps.Open(p.X, p.Y, 0.4f) && Vector2.Distance(p, new Vector2(ForestBounds.PlayerStart.X, ForestBounds.PlayerStart.Z)) > 3f)
                    spots.Add(p);
            }
        }
        return spots;
    }

    private static Decoration[] PlaceDecoration()
    {
        var list = new List<Decoration>();

        // Tiny fungi of every kind, one kind to a spot and never the same as the nearest spots'.
        var spots = SmallSpots().ToArray();
        var ids = new int[spots.Length];
        var allowed = new MushroomKind[spots.Length][];
        var smallKinds = ForestSpecies.For(Tier.Small);
        for (int c = 0; c < spots.Length; c++)
            (ids[c], allowed[c]) = (c, smallKinds);
        var kinds = AssignKinds(spots, ids, allowed, 93);
        var patches = new List<MushroomCluster>();
        for (int c = 0; c < spots.Length; c++)
        {
            int first = list.Count;
            float tallest = 0f;
            int count = Members(kinds[c], c, 94, small: true);
            for (int k = 0; k < count; k++)
            {
                int id = 30000 + c * 16 + k;
                float a = k * 2.4f + Rand(c, 43) * 6f, d = k == 0 ? 0f : 0.08f + (PatchRadius - 0.15f) * Rand(id, 44);
                float x = spots[c].X + MathF.Cos(a) * d, z = spots[c].Y + MathF.Sin(a) * d;
                var m = Mushroom(kinds[c], x, z, k == 0 ? 1f : 0.45f + 0.5f * Rand(id, 45), id);
                if (k > 0 && ForestSpecies.Of(kinds[c]).Habit == Habit.Tuft)
                    m = m with { Yaw = MathF.Atan2(-MathF.Sin(a), MathF.Cos(a)), Lean = 0.1f + 0.12f * Rand(id, 47) };
                if (ForestProps.Open(x, z, m.Radius))
                {
                    list.Add(m);
                    tallest = MathF.Max(tallest, m.Height);
                }
            }
            if (list.Count > first)
                patches.Add(new MushroomCluster(kinds[c], spots[c].X, spots[c].Y, PatchRadius, tallest, first, list.Count - first));
        }
        s_patches = patches.ToArray();

        // Ferns gather at trunk feet and along the paths' edges.
        int f = 0;
        foreach (var o in Obstacles.All)
        {
            for (int k = 0; k < 3; k++, f++)
            {
                float a = Rand(f, 50) * MathF.Tau, d = o.Radius + 0.4f + 1.4f * Rand(f, 51);
                AddFern(list, o.X + MathF.Cos(a) * d, o.Z + MathF.Sin(a) * d, f, 50);
            }
        }
        for (int k = 0; k < 900; k++, f++)
        {
            float x = (Rand(f, 55) * 2f - 1f) * ForestBounds.HalfSize, z = (Rand(f, 56) * 2f - 1f) * ForestBounds.HalfSize;
            if (ForestLayout.ClearingDepth(x, z) < 1f)
                AddFern(list, x, z, f, 55);
        }

        // Flowers in the meadows of the clearings and scattered through the forest.
        for (int k = 0; k < 2200; k++)
        {
            int id = 40000 + k;
            float x = (Rand(id, 65) * 2f - 1f) * ForestBounds.HalfSize, z = (Rand(id, 66) * 2f - 1f) * ForestBounds.HalfSize;
            if (ForestLayout.ClearingDepth(x, z) < -6f && Rand(id, 67) > 0.25f)
                continue;
            float height = 0.12f + 0.16f * Rand(id, 68);
            if (ForestProps.Open(x, z, 0.1f))
                list.Add(new Decoration(DecorKind.Flower, x, z, Rand(id, 69) * MathF.Tau, height, height * FlowerMeshRadius / FlowerMeshHeight, 0f, id));
        }

        // Ankle-high grass tufts everywhere but on paths and trunks: on the trail and in the gap
        // too (the overgrowth), and off the square away from the Map's walkable ground.
        for (int k = 0; k < 40000; k++)
        {
            float x = (Rand(k, 60) * 2f - 1f) * (ForestBounds.HalfSize + 6f), z = (Rand(k, 61) * 2f - 1f) * (ForestBounds.HalfSize + 6f);
            if (ForestLayout.PathDistance(x, z) < ForestLayout.PathHalfWidth + 0.2f || Obstacles.Blocks(x, z, 0.1f))
                continue;
            if (!ForestBounds.Contains(x, z) && Areas.Walkable(x, z, -0.5f))
                continue; // the Map's roads and the Deep Forest path run out through the gaps
            float scale = 0.7f + 0.5f * Rand(k, 63);
            list.Add(new Decoration(DecorKind.Grass, x, z, Rand(k, 62) * MathF.Tau, GrassMeshHeight * scale, GrassMeshRadius * scale, 0f, k));
        }

        // A few tiny dark Deep Forest mushrooms beside the trail at the gap's mouth.
        foreach (var m in ForestProps.DarkMushrooms)
        {
            int id = 50000 + list.Count;
            var kind = Rand(id, 48) < 0.5f ? MushroomKind.InkCap : MushroomKind.FlyAgaric;
            float capHeight = MathF.Min(m.Size * (0.5f + 0.3f * Rand(id, 6)), m.Size * 0.85f);
            list.Add(new Decoration(DecorKind.DarkMushroom, m.X, m.Z, Rand(id, 1) * MathF.Tau, m.Size * 1.7f, m.Size, capHeight, id,
                kind, ForestSpecies.Of(kind).Shapes[0]));
        }
        return list.ToArray();
    }

    /// <summary>A tiny fungus of <paramref name="kind"/>, <paramref name="scale"/> of the biggest its kind grows small, under the decoration limits.</summary>
    private static Decoration Mushroom(MushroomKind kind, float x, float z, float scale, int id)
    {
        var look = ForestSpecies.Of(kind);
        // The tallest a tiny one of its kind stands: cups and jellies hug the ground, enoki stand tall.
        float tallest = kind switch
        {
            MushroomKind.CupFungus or MushroomKind.Jelly => 0.13f,
            MushroomKind.Puffball => 0.24f,
            MushroomKind.Enoki or MushroomKind.Parasol or MushroomKind.InkCap => 0.37f,
            _ => 0.3f,
        };
        float height = MathF.Max(0.05f, tallest * scale * (0.65f + 0.35f * Rand(id, 46)));
        float capRadius = Math.Clamp(height * Lerp(look.CapPerHeight, Rand(id, 21)), 0.012f, DecorationMaxRadius - 0.02f);
        float capHeight = look.Stemless ? height * 0.5f : Math.Clamp(capRadius * Lerp(look.CapDepth, Rand(id, 6)), 0.012f, height * 0.5f);
        var shape = look.Shapes[(int)(Rand(id, 24) * look.Shapes.Length)];
        float lean = look.Stemless ? 0f : 0.1f * Rand(id, 25);
        return new Decoration(DecorKind.Mushroom, x, z, Rand(id, 1) * MathF.Tau, height, capRadius, capHeight, id, kind, shape, lean);
    }
    private static void AddFern(List<Decoration> list, float x, float z, int f, int salt)
    {
        if (!ForestProps.Open(x, z, 0.3f))
            return;
        // Small enough to stay under the decoration limits (0.48 x 0.6 m = 0.288 m in radius).
        float scale = 0.32f + 0.16f * Rand(f, salt + 3);
        list.Add(new Decoration(DecorKind.Fern, x, z, Rand(f, salt + 2) * MathF.Tau, FernMeshHeight * scale, FernMeshRadius * scale, 0f, f));
    }
}

/// <summary>What a piece of the forest's scenery is.</summary>
public enum DecorKind
{
    Mushroom,
    DarkMushroom,
    Fern,
    Flower,
    Grass,
}

/// <summary>
/// A mushroom the view draws: at (x, z) on the ground, turned by <see cref="Yaw"/>, a stem never
/// wider than <see cref="StemRadius"/> and a cap of <see cref="CapRadius"/> (it may overhang) and
/// <see cref="CapHeight"/>, <see cref="Height"/> meters tall above the ground in all.
/// Of kind <see cref="Species"/> with a cap of <see cref="Shape"/>; its stem leans by <see cref="Lean"/>
/// (meters aside per meter up, along its local +X) and narrows above its foot to <see cref="Slender"/>
/// of its profile (<see cref="ForestScenery.StemRadiusAt"/>).
/// </summary>
public readonly record struct DrawnMushroom(DecorKind Kind, float X, float Z, float Yaw, float StemRadius, float Height, float CapRadius, float CapHeight, int Id,
    MushroomKind Species = MushroomKind.FlyAgaric, CapShape Shape = CapShape.Dome, float Lean = 0f, float Slender = 1f);

/// <summary>A medium cluster: the Map prop it stands on (its kind, center, circle and height) and its mushrooms, <see cref="Count"/> of <see cref="ForestScenery.MediumMushrooms"/> from <see cref="Start"/>.</summary>
public readonly record struct MushroomCluster(MushroomKind Species, float X, float Z, float Radius, float Height, int Start, int Count);

/// <summary>A shelf (bracket) fungus on giant <see cref="Giant"/>'s stem, at height fraction <see cref="Along"/>, toward <see cref="Angle"/>, reaching <see cref="Radius"/> out of it, <see cref="Thickness"/> thick.</summary>
public readonly record struct Shelf(int Giant, float Angle, float Along, float Radius, float Thickness, int Id);

/// <summary>
/// A piece of small ground decoration at (x, z), turned by <see cref="Yaw"/>, <see cref="Height"/>
/// tall and <see cref="Radius"/> across in radius (a mushroom's cap radius, its stem a third of it;
/// <see cref="CapHeight"/> only for mushrooms, as are their <see cref="Species"/>, <see cref="Shape"/> and <see cref="Lean"/>).
/// </summary>
public readonly record struct Decoration(DecorKind Kind, float X, float Z, float Yaw, float Height, float Radius, float CapHeight, int Id,
    MushroomKind Species = MushroomKind.FlyAgaric, CapShape Shape = CapShape.Dome, float Lean = 0f);
