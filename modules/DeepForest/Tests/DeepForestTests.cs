using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Map;
using Fungtopia.Shell;
using Xunit;

namespace Fungtopia.DeepForest.Tests;

public class DeepForestTests
{
    /// <summary>Fake of the shell's bus: counts subscriptions and publications.</summary>
    private sealed class FakeBus : IMessageBus
    {
        public int Subscriptions, Published;

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct => Subscriptions++;

        public void Publish<T>(in T message) where T : struct => Published++;
    }

    private static readonly DeepForestScenery Scenery = DeepForestScenery.Create();

    private static int FirstDeepTrunk => Ground.Obstacles.Length - Ground.DeepForestTrunkCount;

    private static bool IsDeepTrunk(int index) => index >= FirstDeepTrunk && index < Ground.Obstacles.Length;

    private static float Horizontal(Vector3 a, float x, float z) => Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(x, z));

    // ---- The module

    [Fact]
    public void ModuleBuildsTheSceneryAtStartAndDoesNothingPerTick()
    {
        var bus = new FakeBus();
        var module = new DeepForestModule();
        module.Start(bus);
        Assert.True(module.Started);
        Assert.NotNull(module.Scenery);
        var scenery = module.Scenery;
        for (int i = 0; i < 600; i++)
            module.Tick(new TickContext(i, 1.0 / 60));
        Assert.Equal(0, bus.Subscriptions);
        Assert.Equal(0, bus.Published);
        Assert.Same(scenery, module.Scenery);
    }

    [Fact]
    public void TickBeforeStartThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new DeepForestModule().Tick(new TickContext(0, 1.0 / 60)));
        Assert.Throws<ArgumentNullException>(() => new DeepForestModule().Start(null!));
    }

    [Fact]
    public void TheSceneryIsTheSameEveryRun()
    {
        var again = DeepForestScenery.Create();
        foreach (var kind in Enum.GetValues<PropKind>())
            Assert.True(Scenery.Of(kind).SequenceEqual(again.Of(kind)), kind.ToString());
        Assert.Equal(Scenery.Fog, again.Fog);
        Assert.Equal(Scenery.Lights, again.Lights);
    }

    // ---- Giant mushrooms on the Map's obstacle spots

    [Fact]
    public void AGiantMushroomStandsOnEveryDeepForestObstacleExactly()
    {
        var stems = Scenery.Of(PropKind.GiantStem);
        var caps = Scenery.Of(PropKind.GiantCap);
        Assert.Equal(Ground.DeepForestTrunkCount, stems.Length);
        Assert.Equal(Ground.DeepForestTrunkCount, caps.Length);
        for (int i = 0; i < stems.Length; i++)
        {
            int o = FirstDeepTrunk + i;
            var c = Ground.Obstacles[o];
            Assert.Equal(o, stems[i].Owner);
            Assert.Equal(c.X, stems[i].Position.X);
            Assert.Equal(c.Z, stems[i].Position.Z);
            // The stem's radius is the obstacle's, its foot planted under its whole flared footprint.
            Assert.Equal(c.Radius, stems[i].Scale.X);
            Assert.Equal(c.Radius, stems[i].Scale.Z);
            AssertPlanted(stems[i].Position, c.Radius * DeepForestScenery.FootRadius, DeepForestScenery.BaseSink);
            // Huge and old: well over the player's head, caps far wider than the stem.
            Assert.InRange(stems[i].Scale.Y, 12f, 30f);
            Assert.Equal(o, caps[i].Owner);
            Assert.True(Horizontal(caps[i].Position, c.X, c.Z) < 0.01f);
            Assert.True(caps[i].Scale.X >= c.Radius * 2.5f);
            // On top of the stem (bells reach down over it).
            Assert.True(caps[i].Position.Y + caps[i].Scale.Y > stems[i].Position.Y + stems[i].Scale.Y);
        }
    }

    [Fact]
    public void NoGiantMushroomOrStemPropStandsOutsideTheMapsObstacles()
    {
        // Everything that is part of a giant mushroom grows from one of the Deep Forest's obstacle circles.
        foreach (var kind in new[] { PropKind.GiantStem, PropKind.Skirt, PropKind.GiantCap, PropKind.BranchStem, PropKind.Shelf, PropKind.Root })
        {
            foreach (var p in Scenery.Of(kind))
            {
                Assert.True(IsDeepTrunk(p.Owner), $"{kind} at {p.Position} grows on obstacle {p.Owner}, not a Deep Forest trunk");
                var c = Ground.Obstacles[p.Owner];
                Assert.True(Horizontal(p.Position, c.X, c.Z) <= c.Radius + 0.01f, $"{kind} at {p.Position} is not on its trunk {c.Center}");
            }
        }
        foreach (var kind in new[] { PropKind.BranchCap, PropKind.Web })
            foreach (var p in Scenery.Of(kind))
                Assert.True(IsDeepTrunk(p.Owner), $"{kind} at {p.Position} has no trunk");
        // Branch caps sit at the tip of their own trunk's side stems, high in the canopy.
        foreach (var p in Scenery.Of(PropKind.BranchCap))
            Assert.True(p.Position.Y - Ground.HeightAt(p.Position.X, p.Position.Z) > 6f, $"branch cap at {p.Position} is too low");
    }

    // ---- Planted on the uneven ground: no base floats where the ground drops away

    /// <summary>The lowest and highest ground on a dense check of a round footprint (the center, 48 points on each of 6 rings).</summary>
    private static (float Low, float High) DenseGround(float x, float z, float radius)
    {
        float low = Ground.HeightAt(x, z), high = low;
        for (int ring = 1; ring <= 6; ring++)
        for (int i = 0; i < 48; i++)
        {
            float a = i * MathF.Tau / 48f, d = radius * ring / 6f;
            float h = Ground.HeightAt(x + d * MathF.Cos(a), z + d * MathF.Sin(a));
            low = MathF.Min(low, h);
            high = MathF.Max(high, h);
        }
        return (low, high);
    }

    /// <summary>A base at <paramref name="at"/> is at or below the ground everywhere under a foot of <paramref name="radius"/>, and at most about 0.3 m below its lowest point.</summary>
    private static void AssertPlanted(Vector3 at, float radius, float sink)
    {
        var (low, _) = DenseGround(at.X, at.Z, radius);
        Assert.True(at.Y <= low, $"base at {at} (radius {radius}) floats {at.Y - low:F3} m over the ground under its foot");
        Assert.True(at.Y >= low - 0.3f, $"base at {at} (radius {radius}) is {low - at.Y:F3} m under the lowest ground of its foot");
        Assert.True(at.Y <= low - sink * 0.5f, $"base at {at} (radius {radius}) is barely in the ground");
    }

    [Fact]
    public void BaseHeightIsUnderTheWholeFootprintAndOnlyALittleUnderItsLowestPoint()
    {
        // Many spots over the Deep Forest's rough ground, its path and the slopes toward its cliffs, small feet and giant ones.
        int sloped = 0;
        var b = DeepForestLayout.Bounds;
        for (int k = 0; k < 600; k++)
        {
            float x = b.MinX - 4f + (b.MaxX - b.MinX + 20f) * DeepForestScenery.Rand(k, 900);
            float z = b.MinZ - 4f + (b.MaxZ - b.MinZ + 8f) * DeepForestScenery.Rand(k, 901);
            float radius = k % 3 == 0 ? 0.1f + 0.3f * DeepForestScenery.Rand(k, 902) : 1.3f * (1f + DeepForestScenery.Rand(k, 903) * 2f) * DeepForestScenery.FootRadius;
            var (low, high) = DeepForestScenery.GroundUnder(x, z, radius);
            Assert.True(low <= high);
            Assert.True(low <= Ground.HeightAt(x, z) && high >= Ground.HeightAt(x, z));
            float baseY = DeepForestScenery.BaseHeight(x, z, radius);
            Assert.Equal(low - DeepForestScenery.BaseSink, baseY);
            AssertPlanted(new Vector3(x, baseY, z), radius, DeepForestScenery.BaseSink);
            Assert.True(high - baseY < high - DenseGround(x, z, radius).Low + 0.3f);
            if (high - low > 0.3f)
                sloped++;
        }
        Assert.True(sloped > 50, $"only {sloped} sloped footprints sampled");
        // The sink is about 0.1-0.2 m.
        Assert.InRange(DeepForestScenery.BaseSink, 0.1f, 0.2f);
        Assert.InRange(DeepForestScenery.SmallSink, 0.1f, 0.2f);
    }

    [Fact]
    public void GiantStemsAreExtendedDownBySlopeAndFlaredIntoTheGround()
    {
        var stems = Scenery.Of(PropKind.GiantStem);
        var caps = Scenery.Of(PropKind.GiantCap);
        var skirts = Scenery.Of(PropKind.Skirt);
        Assert.Equal(stems.Length, skirts.Length);
        int sloped = 0;
        for (int i = 0; i < stems.Length; i++)
        {
            var s = stems[i];
            var c = Ground.Obstacles[s.Owner];
            var (low, high) = DenseGround(c.X, c.Z, c.Radius * DeepForestScenery.FootRadius);
            if (high - low > 0.3f)
                sloped++;
            // Still well over the high side: the stem's top (and its cap) never sink because the foot went down.
            float top = s.Position.Y + s.Scale.Y;
            Assert.True(top - high >= 10f + 3f * c.Radius - 0.05f, $"stem {i} stands only {top - high:F2} m over its high side");
            Assert.True(caps[i].Position.Y + caps[i].Scale.Y * 0.5f > high + 8f, $"cap {i} sinks");
            // Its skirt: from the same base, reaching over the high side, wider at the foot than the stem's radius, its top inside the stem.
            var k = skirts[i];
            Assert.Equal(s.Owner, k.Owner);
            Assert.Equal(s.Position, k.Position);
            Assert.True(k.Position.Y + k.Scale.Y > high + 0.5f, $"skirt {i} is buried");
            Assert.True(k.Scale.Y < 3f + (high - low) + 0.5f, $"skirt {i} is {k.Scale.Y} m tall");
            float flareTop = k.Scale.X, stemThere = c.Radius * DeepForestScenery.StemProfile(k.Scale.Y / s.Scale.Y);
            Assert.InRange(flareTop, stemThere * 0.9f, stemThere);
            Assert.True(flareTop * 1.12f > c.Radius * 1.3f, $"skirt {i} does not flare");
        }
        Assert.True(sloped > stems.Length / 4, $"only {sloped} giant stems on sloped ground");
    }

    [Fact]
    public void RootsLieInTheGroundAlongTheirWholeLength()
    {
        Assert.True(Scenery.Of(PropKind.Root).Length > 0);
        foreach (var p in Scenery.Of(PropKind.Root))
        {
            // The root's line in the world: from its position along its turned +X, length Scale.X.
            var along = Vector3.Transform(Vector3.UnitX, p.Rotation);
            var flat = Vector3.Normalize(new Vector3(along.X, 0f, along.Z));
            float length = p.Scale.X;
            for (int i = 0; i <= 20; i++)
            {
                var q = p.Position + along * (length * i / 20f);
                Assert.True(q.Y <= Ground.HeightAt(q.X, q.Z) + 0.01f, $"root at {p.Position} floats at {q}");
                var side = new Vector3(-flat.Z, 0f, flat.X) * (0.26f * p.Scale.Y);
                Assert.True(q.Y <= Ground.HeightAt(q.X + side.X, q.Z + side.Z) + 0.03f, $"root at {p.Position} floats beside {q}");
                Assert.True(q.Y <= Ground.HeightAt(q.X - side.X, q.Z - side.Z) + 0.03f, $"root at {p.Position} floats beside {q}");
            }
            Assert.True(p.Position.Y >= Ground.HeightAt(p.Position.X, p.Position.Z) - 0.8f, $"root at {p.Position} is buried");
        }
    }

    /// <summary>The highest point of a cap (its mesh from y 0 to 1, radius 1), tilted by its rotation.</summary>
    private static float Top(in Prop p)
    {
        float up = Vector3.Transform(Vector3.UnitY, p.Rotation).Y;
        return p.Position.Y + p.Scale.Y * up + p.Scale.X * MathF.Sqrt(MathF.Max(0f, 1f - up * up));
    }

    private static bool Stemless(CapShape shape) => shape is CapShape.Ball or CapShape.Cup or CapShape.Coral or CapShape.Jelly;

    /// <summary>The Map's Deep Forest medium mushrooms with their circle's index in Ground.Obstacles.</summary>
    private static List<(Fungtopia.Map.Prop Prop, int Circle)> MediumProps()
    {
        var list = new List<(Fungtopia.Map.Prop, int)>();
        int circle = Ground.PropCircleStart;
        foreach (var p in Props.All)
        {
            if (p.Kind == Fungtopia.Map.PropKind.MediumMushroom && p.Area == Area.DeepForest)
                list.Add((p, circle));
            circle += p.CircleCount;
        }
        return list;
    }

    [Fact]
    public void MediumMushroomsStandExactlyOnTheMapsPropsNoTallerThanThem()
    {
        var medium = MediumProps();
        Assert.True(medium.Count > 200, $"only {medium.Count} medium props");
        var owners = new HashSet<int>();
        foreach (var (mp, circle) in medium)
        {
            var c = Ground.Obstacles[circle];
            Assert.Equal(mp.X, c.X);
            Assert.Equal(mp.Radius, c.Radius);
            owners.Add(circle);
            var (_, high) = DeepForestScenery.GroundUnder(mp.X, mp.Z, mp.Radius);
            float tallest = 0f, reach = 0f;
            int caps = 0;
            foreach (var p in Scenery.Of(PropKind.MediumCap))
            {
                if (p.Owner != circle)
                    continue;
                caps++;
                tallest = MathF.Max(tallest, Top(p) - high);
                reach = MathF.Max(reach, Horizontal(p.Position, mp.X, mp.Z) + p.Scale.X);
                // A stemless body (puffball, coral) is solid itself: inside the circle.
                if (Stemless(p.Shape))
                    Assert.True(Horizontal(p.Position, mp.X, mp.Z) + p.Scale.X <= mp.Radius + 0.01f, $"{p.Species} at {p.Position} is outside its prop");
            }
            Assert.True(caps >= 1, $"medium prop at {mp.Center} is not drawn");
            // Visual height varies within the prop's height, and it covers the solid circle.
            Assert.InRange(tallest, 0.6f * mp.Height, mp.Height + 0.01f);
            Assert.True(reach >= 0.8f * mp.Radius, $"medium mushroom at {mp.Center} reaches only {reach} of {mp.Radius}");
            foreach (var p in Scenery.Of(PropKind.MediumStem))
            {
                if (p.Owner != circle)
                    continue;
                // Every stem's foot is inside the prop's circle, and planted.
                float foot = p.Scale.X * DeepForestScenery.StalkFoot;
                Assert.True(Horizontal(p.Position, mp.X, mp.Z) + foot <= mp.Radius + 0.01f, $"stem at {p.Position} is outside its prop");
                AssertPlanted(p.Position, foot, DeepForestScenery.SmallSink);
            }
        }
        foreach (var kind in new[] { PropKind.MediumStem, PropKind.MediumCap })
            foreach (var p in Scenery.Of(kind))
                Assert.Contains(p.Owner, owners);
    }

    [Fact]
    public void SmallFungiAndFernsStayLowEnoughToWalkOver()
    {
        Assert.True(DeepForestScenery.SmallMaxHeight <= 0.4f);
        Assert.True(Scenery.Of(PropKind.SmallCap).Length > 1500, $"only {Scenery.Of(PropKind.SmallCap).Length} small fungi");
        foreach (var p in Scenery.Of(PropKind.SmallCap))
        {
            var (_, high) = DeepForestScenery.GroundUnder(p.Position.X, p.Position.Z, DeepForestScenery.SmallSpotRadius);
            Assert.True(Top(p) - high <= DeepForestScenery.SmallMaxHeight + 0.02f, $"{p.Species} at {p.Position} is {Top(p) - high:F2} m tall");
            Assert.True(Top(p) - Ground.HeightAt(p.Position.X, p.Position.Z) > 0.03f, $"{p.Species} at {p.Position} is buried");
        }
        foreach (var p in Scenery.Of(PropKind.Fern))
            Assert.True(p.Scale.Y * DeepForestScenery.FernHeight <= DeepForestScenery.SmallMaxHeight + 0.001f, $"fern at {p.Position} is too tall");
    }

    // ---- Variety: many kinds, sizes, proportions and tilts; no two neighbors alike

    [Fact]
    public void ManyKindsOfMushroomsAndFungiGrowEverywhere()
    {
        var all = new HashSet<Fungus>();
        var giantShapes = new HashSet<CapShape>();
        var mediumKinds = new HashSet<Fungus>();
        var smallKinds = new HashSet<Fungus>();
        foreach (var kind in new[] { PropKind.GiantCap, PropKind.BranchCap })
            foreach (var p in Scenery.Of(kind))
            {
                giantShapes.Add(p.Shape);
                all.Add(p.Species);
            }
        foreach (var p in Scenery.Of(PropKind.MediumCap))
            mediumKinds.Add(p.Species);
        foreach (var p in Scenery.Of(PropKind.SmallCap))
            smallKinds.Add(p.Species);
        foreach (var p in Scenery.Of(PropKind.Shelf))
            all.Add(p.Species);
        all.UnionWith(mediumKinds);
        all.UnionWith(smallKinds);
        foreach (var f in Enum.GetValues<Fungus>())
            if (f != Fungus.None)
                Assert.Contains(f, all);
        Assert.True(giantShapes.Count >= 5, $"giant caps in only {giantShapes.Count} shapes");
        Assert.True(mediumKinds.Count >= 10, $"medium mushrooms of only {mediumKinds.Count} kinds");
        Assert.True(smallKinds.Count >= 12, $"small fungi of only {smallKinds.Count} kinds");
        // The giants lean to the strange kinds: glowing, amethyst and ink caps are a good share.
        int strange = 0;
        foreach (var p in Scenery.Of(PropKind.GiantCap))
            if (p.Species is Fungus.Glowing or Fungus.Amethyst or Fungus.InkCap)
                strange++;
        Assert.True(strange > Scenery.Of(PropKind.GiantCap).Length * 0.4f, $"only {strange} strange giant caps");
        foreach (var p in Scenery.Of(PropKind.Shelf))
            Assert.Equal(Fungus.Bracket, p.Species);
    }

    [Fact]
    public void SinglesPairsAndClustersOfDifferentHeightsProportionsAndTilts()
    {
        var perSpot = new Dictionary<int, List<Prop>>();
        foreach (var p in Scenery.Of(PropKind.MediumCap))
        {
            if (!perSpot.TryGetValue(p.Owner, out var list))
                perSpot[p.Owner] = list = new List<Prop>();
            list.Add(p);
        }
        int singles = 0, pairs = 0, clusters = 0, mixedHeights = 0;
        foreach (var list in perSpot.Values)
        {
            if (list.Count == 1)
                singles++;
            else if (list.Count == 2)
                pairs++;
            else
                clusters++;
            float lo = float.MaxValue, hi = 0f;
            foreach (var p in list)
            {
                float h = Top(p) - Ground.HeightAt(p.Position.X, p.Position.Z);
                lo = MathF.Min(lo, h);
                hi = MathF.Max(hi, h);
            }
            if (list.Count > 1 && lo < hi * 0.9f)
                mixedHeights++;
        }
        Assert.True(singles > 20 && pairs > 20 && clusters > 20, $"{singles} single, {pairs} pairs, {clusters} clusters");
        Assert.True(mixedHeights > (pairs + clusters) / 2, $"only {mixedHeights} groups mix heights");
        // Sizes, proportions and tilts vary within each kind.
        foreach (var species in new[] { Fungus.FlyAgaric, Fungus.Amethyst, Fungus.InkCap, Fungus.Parasol })
        {
            float minRatio = float.MaxValue, maxRatio = 0f, minSize = float.MaxValue, maxSize = 0f, maxTilt = 0f, minTilt = 1f;
            foreach (var p in Scenery.Of(PropKind.SmallCap))
            {
                if (p.Species != species)
                    continue;
                float ratio = p.Scale.Y / p.Scale.X;
                (minRatio, maxRatio) = (MathF.Min(minRatio, ratio), MathF.Max(maxRatio, ratio));
                (minSize, maxSize) = (MathF.Min(minSize, p.Scale.X), MathF.Max(maxSize, p.Scale.X));
                float tilt = MathF.Acos(Math.Clamp(Vector3.Transform(Vector3.UnitY, p.Rotation).Y, -1f, 1f));
                (minTilt, maxTilt) = (MathF.Min(minTilt, tilt), MathF.Max(maxTilt, tilt));
            }
            Assert.True(maxRatio > minRatio * 1.2f, $"{species} caps all have the same proportions");
            Assert.True(maxSize > minSize * 2f, $"{species} caps are all one size");
            Assert.True(maxTilt > 0.1f && minTilt < 0.03f, $"{species} tilts {minTilt:F2}..{maxTilt:F2}");
        }
    }

    /// <summary>Whether every spot's nearest neighbor has another look.</summary>
    private static void AssertNeighborsDiffer(string what, List<(Vector2 At, DeepForestScenery.Look Look)> spots)
    {
        Assert.True(spots.Count > 50, $"only {spots.Count} {what}");
        for (int i = 0; i < spots.Count; i++)
        {
            int nearest = -1;
            float best = float.MaxValue;
            for (int j = 0; j < spots.Count; j++)
            {
                float d = Vector2.DistanceSquared(spots[i].At, spots[j].At);
                if (j != i && d < best)
                    (best, nearest) = (d, j);
            }
            Assert.True(spots[i].Look != spots[nearest].Look, $"{what} at {spots[i].At} and its neighbor at {spots[nearest].At} are both {spots[i].Look}");
        }
    }

    [Fact]
    public void NoTwoNeighborsAreIdentical()
    {
        var giants = new List<(Vector2, DeepForestScenery.Look)>();
        foreach (var p in Scenery.Of(PropKind.GiantCap))
            giants.Add((new Vector2(p.Position.X, p.Position.Z), new DeepForestScenery.Look(p.Species, p.Shape)));
        AssertNeighborsDiffer("giant mushrooms", giants);
        // A side cap is never the same as its own giant's cap.
        var caps = Scenery.Of(PropKind.GiantCap);
        foreach (var p in Scenery.Of(PropKind.BranchCap))
        {
            var g = caps[p.Owner - FirstDeepTrunk];
            Assert.False(g.Species == p.Species && g.Shape == p.Shape, $"side cap at {p.Position} is like its giant's");
        }
        var medium = new List<(Vector2, DeepForestScenery.Look)>();
        var seen = new HashSet<int>();
        foreach (var p in Scenery.Of(PropKind.MediumCap))
            if (seen.Add(p.Owner))
                medium.Add((new Vector2(Ground.Obstacles[p.Owner].X, Ground.Obstacles[p.Owner].Z), new DeepForestScenery.Look(p.Species, p.Shape)));
        AssertNeighborsDiffer("medium mushrooms", medium);
        var small = new List<(Vector2, DeepForestScenery.Look)>();
        foreach (var s in Scenery.GroundSpots)
            small.Add((s.At, s.Look));
        AssertNeighborsDiffer("small fungi spots", small);
        // Each small fungus grows in a spot of its own look.
        foreach (var p in Scenery.Of(PropKind.SmallCap))
        {
            bool found = false;
            foreach (var s in Scenery.GroundSpots)
                found |= s.Look.Species == p.Species && s.Look.Shape == p.Shape
                    && Vector2.Distance(s.At, new Vector2(p.Position.X, p.Position.Z)) <= DeepForestScenery.SmallSpotRadius + 0.2f;
            Assert.True(found, $"{p.Species} at {p.Position} is in no spot of its kind");
        }
    }

    // ---- Everything inside the area

    [Fact]
    public void EveryPropIsInsideTheDeepForestOrItsPath()
    {
        int total = 0;
        foreach (var kind in Enum.GetValues<PropKind>())
        {
            foreach (var p in Scenery.Of(kind))
            {
                Assert.True(DeepForestGloom.InArea(p.Position.X, p.Position.Z), $"{kind} at {p.Position} is outside the Deep Forest");
                Assert.NotEqual(Area.Town, Areas.At(p.Position.X, p.Position.Z));
                total++;
            }
        }
        Assert.True(total > 5000, $"only {total} props");
        foreach (var l in Scenery.Lights)
            Assert.True(DeepForestGloom.InArea(l.Position.X, l.Position.Z), $"light at {l.Position}");
    }

    [Fact]
    public void GroundPropsStandOnTheGroundOffTheStemsAndOffThePathsMiddle()
    {
        foreach (var kind in new[] { PropKind.SmallStem, PropKind.SmallCap, PropKind.Fern })
        {
            Assert.True(Scenery.Of(kind).Length > 100, $"too few {kind}");
            foreach (var p in Scenery.Of(kind))
            {
                float x = p.Position.X, z = p.Position.Z;
                Assert.Equal(-1, p.Owner);
                if (kind == PropKind.SmallCap && !Stemless(p.Shape))
                    continue; // on its stem
                float foot = kind switch
                {
                    PropKind.SmallStem => DeepForestScenery.StalkFoot,
                    PropKind.SmallCap => 1f,
                    _ => DeepForestScenery.FernFoot,
                } * p.Scale.X;
                AssertPlanted(p.Position, foot, DeepForestScenery.SmallSink);
                Assert.False(Ground.Blocks(x, z, DeepForestScenery.StemClearance), $"{kind} at {p.Position} is inside a stem");
                if (Areas.DeepForestPath.Contains(x, z))
                    Assert.True(MathF.Abs(z) >= DeepForestScenery.PathClearHalfWidth, $"{kind} at {p.Position} blocks the path's middle");
            }
        }
        foreach (var kind in new[] { PropKind.Spore, PropKind.Mist })
            foreach (var p in Scenery.Of(kind))
                Assert.InRange(p.Position.Y - Ground.HeightAt(p.Position.X, p.Position.Z), 0.3f, 6.5f);
    }

    [Fact]
    public void TheMonsterSpawnSpotsStayClear()
    {
        // Undergrowth may cover them, but no root, shelf or web hides a spawning monster.
        foreach (var kind in new[] { PropKind.Root, PropKind.Web })
            foreach (var p in Scenery.Of(kind))
                foreach (var s in DeepForestLayout.SpawnPoints)
                    Assert.True(Horizontal(p.Position, s.X, s.Y) > 1.5f, $"{kind} at {p.Position} sits on spawn {s}");
    }

    // ---- The look

    private static float Saturation(Vector3 c) => (Max(c) - MathF.Min(c.X, MathF.Min(c.Y, c.Z))) / Max(c);

    private static float Max(Vector3 c) => MathF.Max(c.X, MathF.Max(c.Y, c.Z));

    [Fact]
    public void CapsAreVividJewelColorsWithWhiteSpotsAndPearlyInkCapsAndPuffballs()
    {
        int reds = 0, purples = 0, magentas = 0, teals = 0, golds = 0, spotted = 0;
        foreach (var kind in new[] { PropKind.GiantCap, PropKind.BranchCap, PropKind.MediumCap, PropKind.SmallCap })
        {
            foreach (var p in Scenery.Of(kind))
            {
                var c = p.Color;
                Assert.InRange(Max(c), 0.4f, 1f);
                if (p.Species is Fungus.InkCap or Fungus.Puffball)
                {
                    // Pale and pearly, but bright, never grey.
                    Assert.True(Max(c) >= 0.8f && MathF.Min(c.X, MathF.Min(c.Y, c.Z)) >= 0.6f, $"{p.Species} color {c} is not pearly");
                    continue;
                }
                // Clearly saturated and bright enough to pop, never washed out to white.
                Assert.True(Saturation(c) >= 0.5f, $"{kind} {p.Species} color {c} is dull");
                if (p.Species == Fungus.FlyAgaric)
                {
                    Assert.True(p.Spots >= 0.7f, $"fly agaric at {p.Position} has no white spots");
                    Assert.True(c.X > 0.7f && c.Y < 0.15f && c.Z < 0.15f, $"fly agaric {c} is not red");
                }
                if (p.Spots > 0f)
                    spotted++;
                if (c.X > 0.7f && c.Y < 0.2f && c.Z < 0.3f)
                    reds++;
                else if (c.Z > c.X && c.Y < c.X * 0.4f)
                    purples++;
                else if (c.X > 0.75f && c.Z > 0.5f && c.Y < 0.3f)
                    magentas++;
                else if (c.Y > c.X * 1.5f && c.Z > c.X * 1.5f)
                    teals++;
                else if (c.X > 0.85f && c.Y > 0.5f && c.Z < 0.2f)
                    golds++;
            }
        }
        Assert.True(reds > 50 && purples > 50 && magentas > 20 && teals > 50 && golds > 30,
            $"{reds} red, {purples} purple, {magentas} magenta, {teals} teal, {golds} gold caps");
        Assert.True(spotted > 200, $"only {spotted} spotted caps");
    }

    [Fact]
    public void BracketShelvesComeInSeveralRichColors()
    {
        int orange = 0, teal = 0, magenta = 0, gold = 0;
        foreach (var p in Scenery.Of(PropKind.Shelf))
        {
            var c = p.Color;
            Assert.True(Saturation(c) >= 0.8f, $"shelf color {c} is dull");
            if (c.Y > c.X)
                teal++;
            else if (c.Z > c.Y)
                magenta++;
            else if (c.Y > c.X * 0.65f)
                gold++;
            else
                orange++;
        }
        Assert.True(orange > 20 && teal > 20 && magenta > 20 && gold > 20, $"{orange} orange, {teal} teal, {magenta} magenta, {gold} gold shelves");
    }

    [Fact]
    public void StemsAreACleanLightCream()
    {
        var cream = DeepForestScenery.StemCream;
        Assert.True(cream.X > 0.85f && cream.X >= cream.Y && cream.Y > cream.Z && cream.Z > 0.65f, $"{cream} is not a light cream");
        foreach (var kind in new[] { PropKind.GiantStem, PropKind.BranchStem, PropKind.Skirt })
        {
            foreach (var p in Scenery.Of(kind))
            {
                // The cream itself, at most a little shaded on the old giants (skirts a touch more).
                float shade = p.Color.X / cream.X;
                Assert.InRange(shade, DeepForestScenery.GiantStemShade * 0.92f - 0.001f, 1.001f);
                Assert.True(Vector3.Distance(p.Color, cream * shade) < 0.001f, $"{kind} color {p.Color} is not the cream");
            }
        }
    }

    [Fact]
    public void TheCanopyBlocksMostOfTheSkyDeepInsideAndLessAtTheEntrance()
    {
        // Samples on the ground, how many have a cap (main or side) right above them.
        float Covered(Func<float, float, bool> where)
        {
            int inside = 0, covered = 0;
            var b = DeepForestLayout.Bounds;
            for (float x = b.MinX; x <= Areas.DeepForestPath.MaxX; x += 1.5f)
            for (float z = b.MinZ; z <= b.MaxZ; z += 1.5f)
            {
                if (!DeepForestGloom.InArea(x, z) || !where(x, z))
                    continue;
                inside++;
                bool under = false;
                foreach (var kind in new[] { PropKind.GiantCap, PropKind.BranchCap })
                    foreach (var p in Scenery.Of(kind))
                        under |= Horizontal(p.Position, x, z) < p.Scale.X * 0.95f;
                if (under)
                    covered++;
            }
            return covered / (float)Math.Max(1, inside);
        }

        float deep = Covered((x, z) => DeepForestLayout.Bounds.Contains(x, z, 15f) && x < -140f && MathF.Abs(z) < 40f);
        float entrance = Covered((x, z) => x > DeepForestLayout.Entrance.X - 4f);
        Assert.True(deep >= 0.85f, $"only {deep:P0} of the deep ground is under the canopy");
        Assert.True(entrance < deep - 0.2f, $"the entrance ({entrance:P0}) is as closed as the depths ({deep:P0})");
    }

    [Fact]
    public void WebsHangOnSomeCapsNotAll()
    {
        var owners = new HashSet<int>();
        foreach (var p in Scenery.Of(PropKind.Web))
        {
            owners.Add(p.Owner);
            Assert.InRange(p.Scale.X, 0.5f, 3.6f);
        }
        float share = owners.Count / (float)Ground.DeepForestTrunkCount;
        Assert.InRange(share, 0.15f, 0.5f);
    }

    [Fact]
    public void GlowingMushroomsGlowClearlyAndSporesGiveFaintLight()
    {
        // Small glowing fungi glow GlowStrength..twice it; every bioluminescent kind glows, no other does.
        int small = 0, giant = 0, medium = 0;
        float sum = 0f;
        foreach (var kind in new[] { PropKind.GiantCap, PropKind.BranchCap, PropKind.MediumCap, PropKind.SmallCap })
        {
            foreach (var p in Scenery.Of(kind))
            {
                if (p.Species != Fungus.Glowing)
                {
                    Assert.Equal(0f, p.Glow);
                    continue;
                }
                if (kind == PropKind.SmallCap)
                {
                    Assert.InRange(p.Glow, DeepForestScenery.GlowStrength, 2f * DeepForestScenery.GlowStrength);
                    sum += p.Glow;
                    small++;
                }
                else if (kind == PropKind.MediumCap)
                {
                    Assert.InRange(p.Glow, DeepForestScenery.GlowStrength, 2f * DeepForestScenery.GlowStrength);
                    medium++;
                }
                else
                {
                    Assert.InRange(p.Glow, 0.3f, DeepForestScenery.GiantGlow);
                    giant++;
                }
                // Blue, green, cyan or violet: never a warm color.
                Assert.True(p.Color.Z > p.Color.X || p.Color.Y > p.Color.X, $"glowing cap {p.Color} is warm");
            }
        }
        // More glowing fungi than before (over 300 small ones), and glowing giants and medium ones too.
        Assert.True(small > 600 && medium > 30 && giant > 30, $"{small} small, {medium} medium, {giant} giant glowing caps");
        Assert.InRange(sum / small, 1.6f, 2f);
        Assert.True(Scenery.Of(PropKind.Spore).Length > 500);
        foreach (var p in Scenery.Of(PropKind.Spore))
            Assert.Equal(1f, p.Glow);
        Assert.InRange(Scenery.Lights.Length, 6, DeepForestScenery.MaxLights);
        // Spread out, not all in one corner.
        foreach (var a in Scenery.Lights)
            foreach (var b in Scenery.Lights)
                Assert.True(a.Equals(b) || Vector3.Distance(a.Position, b.Position) > 20f);
    }

    [Fact]
    public void TheOvergrownPathHasUndergrowthAlongItsEdges()
    {
        int ferns = 0, glow = 0;
        foreach (var p in Scenery.Of(PropKind.Fern))
            if (Areas.DeepForestPath.Contains(p.Position.X, p.Position.Z))
                ferns++;
        foreach (var p in Scenery.Of(PropKind.SmallCap))
            if (p.Species == Fungus.Glowing && Areas.DeepForestPath.Contains(p.Position.X, p.Position.Z))
                glow++;
        Assert.True(ferns >= 40, $"only {ferns} ferns on the path");
        Assert.True(glow >= 3, $"only {glow} glowing mushrooms on the path");
    }

    // ---- Fog

    [Fact]
    public void FogIsSoftLowInsideTheAreaAndThickerInHollows()
    {
        Assert.NotEmpty(Scenery.Fog);
        int hollows = 0;
        foreach (var f in Scenery.Fog)
        {
            // Within the Deep Forest's rectangle or its path, never out over the Mushroom Forest.
            float minX = f.Center.X - f.Size.X * 0.5f, maxX = f.Center.X + f.Size.X * 0.5f;
            float minZ = f.Center.Z - f.Size.Z * 0.5f, maxZ = f.Center.Z + f.Size.Z * 0.5f;
            bool inRect = minX >= DeepForestLayout.Bounds.MinX - 0.01f && maxX <= DeepForestLayout.Bounds.MaxX + 0.01f
                && minZ >= DeepForestLayout.Bounds.MinZ - 0.01f && maxZ <= DeepForestLayout.Bounds.MaxZ + 0.01f;
            bool inPath = minX >= Areas.DeepForestPath.MinX - 0.01f && maxX <= Areas.DeepForestPath.MaxX + 0.01f
                && minZ >= Areas.DeepForestPath.MinZ - 0.01f && maxZ <= Areas.DeepForestPath.MaxZ + 0.01f;
            Assert.True(inRect || inPath, $"fog at {f.Center} size {f.Size} leaves the Deep Forest");
            // Slightly foggy, low.
            Assert.InRange(f.Density, 0.005f, 0.06f);
            Assert.True(f.Size.Y <= 12f, $"fog at {f.Center} is {f.Size.Y} m tall");
            if (f.Ellipsoid)
            {
                hollows++;
                Assert.True(DeepForestScenery.Hollowness(f.Center.X, f.Center.Z) >= 0.5f);
            }
        }
        Assert.True(hollows >= 3, $"only {hollows} hollow fogs");
        // The main layer covers the depths; the path's fog is thinner than the entrance's, which is thinner than the depths'.
        float Density(float x, float z)
        {
            float d = 0f;
            foreach (var f in Scenery.Fog)
                if (!f.Ellipsoid && MathF.Abs(x - f.Center.X) <= f.Size.X * 0.5f && MathF.Abs(z - f.Center.Z) <= f.Size.Z * 0.5f)
                    d += f.Density;
            return d;
        }
        Assert.True(Density(-98f, 0f) > 0f);
        Assert.True(Density(-98f, 0f) < Density(-115f, 0f));
        Assert.True(Density(-115f, 0f) < Density(-200f, 0f));
        Assert.Equal(0f, Density(-50f, 0f));
    }

    // ---- The gradual transition

    [Fact]
    public void GloomRisesGraduallyAlongThePathAndIsZeroOutside()
    {
        Assert.Equal(0f, DeepForestGloom.At(0f, 0f));
        Assert.Equal(0f, DeepForestGloom.At(-90f, 0f));
        Assert.Equal(0f, DeepForestGloom.At(0f, 215f));
        Assert.Equal(1f, DeepForestGloom.At(-200f, 30f));
        float previous = 0f;
        for (float x = -96f; x >= -140f; x -= 0.5f)
        {
            float g = DeepForestGloom.At(x, 0f);
            Assert.True(g >= previous, $"gloom falls at x = {x}");
            Assert.True(g - previous < 0.05f, $"gloom jumps at x = {x}");
            previous = g;
        }
        Assert.InRange(DeepForestGloom.At(DeepForestLayout.Entrance.X, 0f), 0.2f, 0.6f);
    }

    [Fact]
    public void InAreaIsTheRectangleAndThePath()
    {
        Assert.True(DeepForestGloom.InArea(-200f, 0f));
        Assert.True(DeepForestGloom.InArea(-104f, 0f));
        Assert.True(DeepForestGloom.InArea(-98f, 3f));
        Assert.False(DeepForestGloom.InArea(-104f, 8f)); // the cliff strip beside the path
        Assert.False(DeepForestGloom.InArea(-90f, 0f));  // the Mushroom Forest
        Assert.False(DeepForestGloom.InArea(-200f, 95f));
        foreach (var s in DeepForestLayout.SpawnPoints)
            Assert.True(DeepForestGloom.InArea(s.X, s.Y));
    }
}

internal static class SpanExtensions
{
    public static bool SequenceEqual(this ReadOnlySpan<Prop> a, ReadOnlySpan<Prop> b)
    {
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
            if (!a[i].Equals(b[i]))
                return false;
        return true;
    }
}
