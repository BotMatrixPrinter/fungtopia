using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Fungtopia.DayNight;
using Fungtopia.Map;
using Fungtopia.Shell;
using Xunit;

namespace Fungtopia.Forest.Tests;

public class ForestTests
{
    /// <summary>Fake of the shell's bus: counts every message published and every subscription, and delivers TimeOfDay.</summary>
    private sealed class FakeBus : IMessageBus
    {
        public int Published;
        public int Subscriptions;
        public IMessageHandler<TimeOfDay>? Time;

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct
        {
            Subscriptions++;
            if (handler is IMessageHandler<TimeOfDay> time)
                Time = time;
        }

        public void Publish<T>(in T message) where T : struct => Published++;
    }

    /// <summary>Every 0.5 m over the walkable square (plus a border around it, beyond which the Map's roads and cliffs begin).</summary>
    private static IEnumerable<(float X, float Z)> Grid(float step = 0.5f, float border = 0f)
    {
        float limit = ForestBounds.HalfSize + border;
        for (float x = -limit; x <= limit; x += step)
        for (float z = -limit; z <= limit; z += step)
            yield return (x, z);
    }

    // Forwarding to the Map

    [Fact]
    public void TerrainForwardsToTheMapGround()
    {
        // Everywhere: the forest square, its ring, the roads, the towns and the cliffs.
        foreach (var (x, z) in Grid(step: 3.7f, border: 200f))
            Assert.Equal(Ground.HeightAt(x, z), Terrain.HeightAt(x, z));
        Assert.Equal(Ground.HeightAt(0f, 215f), Terrain.HeightAt(0f, 215f));
    }

    [Fact]
    public void ObstaclesAreTheMapObstaclesInTheForest()
    {
        // Every Map obstacle whose center is in the forest area, whatever it is, in the Map's order.
        var expected = new List<Circle>();
        foreach (var c in Ground.Obstacles)
            if (Areas.Forest.Contains(c.X, c.Z))
                expected.Add(c);
        Assert.True(expected.Count >= Ground.ForestTrunkCount);
        Assert.Equal(expected.Count, Obstacles.Count);
        var forest = Obstacles.All;
        Assert.Equal(expected.Count, forest.Length);
        for (int i = 0; i < forest.Length; i++)
        {
            Assert.Equal(expected[i].X, forest[i].X);
            Assert.Equal(expected[i].Z, forest[i].Z);
            Assert.Equal(expected[i].Radius, forest[i].Radius);
        }
        // The forest trunks are among them, first.
        for (int i = 0; i < Ground.ForestTrunkCount; i++)
            Assert.Equal(Ground.Obstacles[i].X, forest[i].X);
        foreach (var (x, z) in Grid(step: 1.3f, border: 20f))
        {
            bool blocked = false;
            foreach (var c in expected)
            {
                float dx = x - c.X, dz = z - c.Z, r = c.Radius + 0.4f;
                blocked |= dx * dx + dz * dz < r * r;
            }
            Assert.Equal(blocked, Obstacles.Blocks(x, z, 0.4f));
        }
    }

    [Fact]
    public void BoundsForwardToTheMapForestArea()
    {
        Assert.Equal(Areas.ForestHalfSize, ForestBounds.HalfSize);
        Assert.Equal(ForestBounds.HalfSize, Areas.Forest.MaxX);
        Assert.Equal(ForestBounds.HalfSize, -Areas.Forest.MinZ);
        Assert.Equal(new Vector3(0f, Ground.HeightAt(0f, 84f), 84f), ForestBounds.PlayerStart);
        Assert.Equal(Area.Forest, Areas.At(ForestBounds.PlayerStart.X, ForestBounds.PlayerStart.Z));
        foreach (var (x, z) in Grid(step: 4.9f, border: 30f))
        {
            Assert.Equal(Areas.Forest.Contains(x, z), ForestBounds.Contains(x, z));
            Assert.Equal(Areas.Forest.Contains(x, z, 2.5f), ForestBounds.Contains(x, z, 2.5f));
        }
    }

    // Terrain

    [Fact]
    public void HeightsStayInAGentleRange()
    {
        float min = float.MaxValue, max = float.MinValue;
        foreach (var (x, z) in Grid())
        {
            float h = Terrain.HeightAt(x, z);
            Assert.True(float.IsFinite(h));
            Assert.InRange(h, -Terrain.MaxAbsHeight, Terrain.MaxAbsHeight);
            min = MathF.Min(min, h);
            max = MathF.Max(max, h);
        }
        Assert.InRange(Terrain.MaxAbsHeight, 0.5f, 2.5f);
        Assert.True(max - min > 1.5f, $"the ground should roll, but only spans {max - min} m");
    }

    [Fact]
    public void SlopesAreGentle()
    {
        const float step = 0.5f;
        foreach (var (x, z) in Grid(step))
        {
            float h = Terrain.HeightAt(x, z);
            Assert.True(MathF.Abs(Terrain.HeightAt(x + step, z) - h) / step < 0.2f, $"too steep in x at ({x}, {z})");
            Assert.True(MathF.Abs(Terrain.HeightAt(x, z + step) - h) / step < 0.2f, $"too steep in z at ({x}, {z})");
        }
    }

    [Fact]
    public void HeightIsDeterministic()
    {
        float first = Terrain.HeightAt(12.5f, -33.25f);
        float sum = 0f;
        for (int i = 0; i < 10000; i++)
            sum += Terrain.HeightAt(i * 0.37f - 90f, 90f - i * 0.21f);
        Assert.True(float.IsFinite(sum));
        Assert.Equal(first, Terrain.HeightAt(12.5f, -33.25f));
    }

    // Bounds and start

    [Fact]
    public void WalkableAreaIsA200MeterSquareOnTheOrigin()
    {
        Assert.Equal(100f, ForestBounds.HalfSize);
        Assert.True(ForestBounds.Contains(0f, 0f));
        Assert.True(ForestBounds.Contains(-100f, 100f));
        Assert.False(ForestBounds.Contains(100.1f, 0f));
        Assert.False(ForestBounds.Contains(0f, -100.1f));
        Assert.False(ForestBounds.Contains(95f, 0f, margin: 6f));
    }

    [Fact]
    public void StartIsOnTheGroundNearTheSouthEdge()
    {
        var start = ForestBounds.PlayerStart;
        Assert.Equal(Terrain.HeightAt(start.X, start.Z), start.Y);
        Assert.True(ForestBounds.Contains(start.X, start.Z, margin: 5f));
        Assert.True(start.Z > ForestBounds.HalfSize * 0.6f, "south is +Z: the start should be near the south edge");
    }

    [Fact]
    public void StartIsInAClearClearing()
    {
        var start = ForestBounds.PlayerStart;
        Assert.True(ForestLayout.ClearingDepth(start.X, start.Z) >= 8f);
        Assert.False(Obstacles.Blocks(start.X, start.Z, radius: 8f));
        foreach (var o in Obstacles.All)
            Assert.True(Vector2.Distance(o.Center, new Vector2(start.X, start.Z)) > o.Radius + 8f);
    }

    // Obstacles

    [Fact]
    public void ThereIsAForestOfObstacles()
    {
        // Trunks today, any solid prop the Map adds later: plenty of them, none bigger than a giant trunk.
        Assert.Equal(Obstacles.All.Length, Obstacles.Count);
        Assert.True(Obstacles.Count >= 80, $"only {Obstacles.Count} obstacles");
        foreach (var o in Obstacles.All)
        {
            Assert.True(o.Radius > 0f && o.Radius <= 3f, $"obstacle at ({o.X}, {o.Z}) has radius {o.Radius}");
            Assert.Equal(new Vector2(o.X, o.Z), o.Center);
        }
    }

    [Fact]
    public void ObstaclesLieInsideTheForestOrOnItsWall()
    {
        // The center is in the square; the circle may reach into the mushroom wall around it, never past it.
        float wall = ForestBounds.HalfSize + ForestRing.Depth;
        foreach (var o in Obstacles.All)
        {
            Assert.True(ForestBounds.Contains(o.X, o.Z), $"obstacle at ({o.X}, {o.Z}) is outside the square");
            Assert.True(MathF.Abs(o.X) + o.Radius <= wall && MathF.Abs(o.Z) + o.Radius <= wall, $"obstacle at ({o.X}, {o.Z}) reaches past the wall");
        }
    }

    [Fact]
    public void ObstaclesLeaveTheGapsClear()
    {
        // (The start is kept clear in StartIsInAClearClearing, paths and clearings in ObstaclesKeepOffPathsAndClearingsAndEachOther.)
        foreach (var o in Obstacles.All)
        {
            // The Map's road gaps north and south: nothing stands in their whole width at the square's edge.
            foreach (var gap in new[] { Areas.ForestGapSouth, Areas.ForestGapNorth })
            {
                bool inGap = MathF.Abs(o.X - gap.X) < Areas.ForestGapHalfWidth + o.Radius
                    && MathF.Abs(o.Z - gap.Y) < 1f + o.Radius;
                Assert.False(inGap, $"obstacle at ({o.X}, {o.Z}) blocks the road gap at {gap}");
            }
            // The west gap: the Deep Forest path runs into the square.
            Assert.False(Areas.DeepForestPath.Contains(o.X, o.Z, -o.Radius), $"obstacle at ({o.X}, {o.Z}) blocks the west gap");
        }
        // A walker comes in through each road gap: 8 m straight into the square (then the path test takes over), nothing in the way.
        foreach (var gap in new[] { Areas.ForestGapSouth, Areas.ForestGapNorth })
            for (float d = 0f; d <= 8f; d += 0.25f)
                Assert.False(Obstacles.Blocks(gap.X, gap.Y - MathF.Sign(gap.Y) * d, radius: 0.5f), $"the way in through the gap at {gap} is blocked {d} m in");
        // And in through the west gap, along the Deep Forest path.
        for (float x = Areas.DeepForestPath.MinX; x <= Areas.DeepForestPath.MaxX + 4f; x += 0.25f)
            Assert.False(Obstacles.Blocks(x, 0f, radius: 0.5f), $"the way in through the west gap is blocked at x = {x}");
    }

    [Fact]
    public void ObstaclesKeepOffPathsAndClearingsAndEachOther()
    {
        var all = Obstacles.All;
        for (int i = 0; i < all.Length; i++)
        {
            var o = all[i];
            Assert.True(ForestLayout.PathDistance(o.X, o.Z) > ForestLayout.PathHalfWidth + o.Radius);
            Assert.True(ForestLayout.ClearingDepth(o.X, o.Z) < -o.Radius);
        }
        // The trunks (the first ForestTrunkCount, before any prop the Map adds) stand more than 1 m apart.
        for (int i = 0; i < Ground.ForestTrunkCount; i++)
        for (int j = i + 1; j < Ground.ForestTrunkCount; j++)
            Assert.True(Vector2.Distance(all[i].Center, all[j].Center) > all[i].Radius + all[j].Radius + 1f);
    }

    [Fact]
    public void BlocksMatchesTheObstacleCircles()
    {
        var o = Obstacles.All[0];
        Assert.True(Obstacles.Blocks(o.X, o.Z));
        Assert.True(Obstacles.Blocks(o.X + o.Radius * 0.9f, o.Z));
        // Trunks stand more than 1 m apart, so just outside one there is no other.
        Assert.False(Obstacles.Blocks(o.X + o.Radius + 0.1f, o.Z));
        Assert.True(Obstacles.Blocks(o.X + o.Radius + 0.3f, o.Z, radius: 0.5f));
    }

    [Fact]
    public void ObstaclesAreTheSameEveryTime()
    {
        // The layout depends on no random seed or clock, only on fixed math: every call gives the same list.
        var first = Obstacles.All.ToArray();
        var second = Obstacles.All.ToArray();
        Assert.NotEmpty(first);
        Assert.Equal(first, second);
        Assert.Equal(first.Length, Obstacles.Count);
    }

    [Fact]
    public void PathsLeadFromTheStartThroughTheMiddleClearing()
    {
        var start = ForestBounds.PlayerStart;
        Assert.True(ForestLayout.OnPath(start.X, start.Z));
        Assert.True(ForestLayout.OnPath(0f, -5f));
        Assert.False(ForestLayout.OnPath(-80f, -80f));
        Assert.Equal(0f, ForestLayout.PathDistance(5f, 70f), 3);
    }

    // The mushroom ring around the square

    [Fact]
    public void RingLeavesTheMapRoadGapsOpen()
    {
        foreach (var gap in new[] { Areas.ForestGapSouth, Areas.ForestGapNorth })
        {
            float outward = MathF.Sign(gap.Y);
            for (float d = 0f; d <= ForestRing.Depth; d += 0.5f)
            for (float dx = -Areas.ForestGapHalfWidth + 0.05f; dx < Areas.ForestGapHalfWidth; dx += 0.5f)
            for (float r = 0.5f; r <= 3f; r += 0.5f)
                Assert.False(ForestRing.Keeps(gap.X + dx, gap.Y + outward * d, r), $"a ring mushroom in the gap at ({gap.X + dx}, {gap.Y + outward * d})");
            Assert.True(ForestRing.InGap(gap.X, gap.Y + outward * 3f, 0f));
            Assert.True(ForestRing.InGap(gap.X + Areas.ForestGapHalfWidth - 0.1f, gap.Y + outward * 3f, 0f));
            Assert.False(ForestRing.InGap(gap.X + Areas.ForestGapHalfWidth + 0.1f, gap.Y + outward * 3f, 0f));
        }
    }

    [Fact]
    public void RingStaysOffTheRoadsAndAllWalkableGroundOutsideTheSquare()
    {
        foreach (var (x, z) in Grid(step: 0.7f, border: ForestRing.Depth))
        {
            if (ForestBounds.Contains(x, z) || !Areas.Walkable(x, z))
                continue;
            for (float r = 0.5f; r <= 3f; r += 0.5f)
                Assert.False(ForestRing.Keeps(x, z, r), $"a ring mushroom on walkable ground at ({x}, {z})");
        }
        // Also with its stem: never within the stem's radius of a road's edge.
        Assert.False(ForestRing.Keeps(0f, 110f + 0f, 1f));
        Assert.False(ForestRing.Keeps(Areas.RoadHalfWidth + 1.2f, 104f, 1f));
    }

    [Fact]
    public void RingClosesTheEdgeAwayFromTheGaps()
    {
        Assert.True(ForestRing.Keeps(50f, 103f, 1.5f));
        Assert.True(ForestRing.Keeps(-50f, -103f, 1.5f));
        Assert.True(ForestRing.Keeps(103f, 20f, 1.5f));
        Assert.True(ForestRing.Keeps(-103f, 40f, 1.5f));
        Assert.True(ForestRing.Keeps(Areas.ForestGapHalfWidth + 2.6f, 102.2f, 1.5f));
        Assert.False(ForestRing.Keeps(50f, 99f, 1.5f)); // inside the square
        Assert.False(ForestRing.Keeps(50f, 100f + ForestRing.Depth + 1f, 1.5f)); // past the ring
    }

    [Fact]
    public void RingMushroomsMatchTheMapObstaclesAndGround()
    {
        var ring = ForestRing.Mushrooms;
        Assert.InRange(ring.Length, 300, 900);
        foreach (var m in ring)
        {
            Assert.True(ForestRing.Keeps(m.X, m.Z, m.Radius), $"ring mushroom at ({m.X}, {m.Z}) where the ring may not stand");
            // Off all walkable ground (stem and all), so it never stands on the Map's roads, towns or paths...
            Assert.False(Areas.Walkable(m.X, m.Z, -m.Radius), $"ring mushroom at ({m.X}, {m.Z}) reaches walkable ground");
            // ...and overlaps none of the Map's obstacle circles (trunks, buildings, the Deep Forest's mushrooms).
            Assert.False(Ground.Blocks(m.X, m.Z, m.Radius), $"ring mushroom at ({m.X}, {m.Z}) overlaps a Map obstacle");
        }
        Assert.Equal(ring.ToArray(), ForestRing.Mushrooms.ToArray());
    }

    [Fact]
    public void NoForestPropBlocksTheWestGap()
    {
        var path = Areas.DeepForestPath;
        Assert.Equal(-Areas.ForestHalfSize, Areas.ForestGapWest.X);
        // Every ring mushroom, small dark mushroom and piece of decoration keeps off the Map's Deep Forest path.
        foreach (var m in ForestRing.Mushrooms)
            Assert.True(path.Depth(m.X, m.Z) < -m.Radius, $"ring mushroom at ({m.X}, {m.Z}) in the west gap");
        foreach (var m in ForestProps.DarkMushrooms)
            Assert.True(path.Depth(m.X, m.Z) < -m.Size, $"dark mushroom at ({m.X}, {m.Z}) in the west gap");
        foreach (var d in ForestScenery.Decoration)
            if (d.Kind != DecorKind.Grass) // only grass overgrows the gap
                Assert.True(path.Depth(d.X, d.Z) < -d.Radius, $"{d.Kind} at ({d.X}, {d.Z}) in the west gap");
        // The whole gap, from inside the square out to the Deep Forest: walkable, no prop may stand
        // there, and no ring mushroom is there.
        for (float x = path.MinX; x <= path.MaxX; x += 0.5f)
        for (float z = path.MinZ + 0.05f; z < path.MaxZ; z += 0.5f)
        {
            Assert.True(Areas.Walkable(x, z), $"the Map's gap is not walkable at ({x}, {z})");
            Assert.True(ForestProps.InWestGap(x, z, 0f));
            for (float r = 0.1f; r <= 3f; r += 0.7f)
            {
                Assert.False(ForestProps.Open(x, z, r), $"a prop may stand in the west gap at ({x}, {z})");
                Assert.False(ForestRing.Keeps(x, z, r), $"a ring mushroom may stand in the west gap at ({x}, {z})");
            }
            foreach (var m in ForestRing.Mushrooms)
                Assert.True(Vector2.Distance(new Vector2(x, z), new Vector2(m.X, m.Z)) > m.Radius + 1f);
        }
    }

    [Fact]
    public void RingOpensAtTheWestGapJustAsWideAsTheMapPath()
    {
        // On each side of the gap the ring's first stem stands right past the path's edge (its
        // clearance from walkable ground), and the ring stays closed along the strip beyond it,
        // so the gap is the only way through.
        float half = Areas.DeepForestGapHalfWidth;
        for (int side = -1; side <= 1; side += 2)
        {
            float nearest = float.MaxValue;
            var reached = new List<(float From, float To)>();
            foreach (var m in ForestRing.Mushrooms)
            {
                if (m.X > -Areas.ForestHalfSize || m.X < -Areas.ForestHalfSize - 12f || MathF.Sign(m.Z) != side)
                    continue;
                float edge = MathF.Abs(m.Z) - m.Radius;
                Assert.True(edge >= half + ForestRing.WalkableClearance - 0.01f, $"stem at ({m.X}, {m.Z}) narrows the gap");
                nearest = MathF.Min(nearest, edge);
                reached.Add((edge, MathF.Abs(m.Z) + m.Radius));
            }
            Assert.InRange(nearest, half + ForestRing.WalkableClearance, half + ForestRing.WalkableClearance + 0.5f);
            // Along the whole strip to the Deep Forest's corners, no hole between stems wider than
            // 2 m (the caps close it above).
            float reach = Areas.DeepForest.MaxZ;
            reached.Sort();
            float covered = nearest;
            foreach (var (from, to) in reached)
            {
                if (from > reach)
                    break;
                Assert.True(from - covered < 2f, $"a {from - covered} m hole in the ring at z = {side * covered} west of the forest");
                covered = MathF.Max(covered, to);
            }
            Assert.True(covered > reach - 2f, $"the ring west of the forest stops at z = {side * covered}");
        }
    }

    [Fact]
    public void AFewDarkMushroomsHintAtTheDeepForest()
    {
        var gap = Areas.ForestGapWest;
        int dark = 0;
        foreach (var m in ForestRing.Mushrooms)
        {
            float toGap = Vector2.Distance(new Vector2(m.X, m.Z), gap);
            Assert.Equal(toGap <= ForestRing.DarkReach, m.Dark);
            if (m.Dark)
                dark++;
        }
        Assert.InRange(dark, 4, 12);
        Assert.InRange(ForestProps.DarkMushrooms.Length, 3, 8);
        foreach (var m in ForestProps.DarkMushrooms)
        {
            Assert.True(ForestProps.Open(m.X, m.Z, m.Size), $"dark mushroom at ({m.X}, {m.Z}) on a path or trunk");
            Assert.True(Vector2.Distance(new Vector2(m.X, m.Z), gap) < 14f, $"dark mushroom at ({m.X}, {m.Z}) far from the gap");
        }
    }

    [Fact]
    public void OvergrownTrailLeadsFromTheGapToTheForestPaths()
    {
        var trail = ForestProps.TrailPoints;
        Assert.Equal(Areas.ForestGapWest, trail[0]);
        var end = trail[^1];
        Assert.True(ForestLayout.PathDistance(end.X, end.Y) <= ForestLayout.PathHalfWidth);
        for (int i = 0; i + 1 < trail.Length; i++)
        for (float t = 0f; t <= 1f; t += 0.05f)
        {
            var p = Vector2.Lerp(trail[i], trail[i + 1], t);
            Assert.True(Areas.Walkable(p.X, p.Y), $"trail off walkable ground at {p}");
            Assert.False(Obstacles.Blocks(p.X, p.Y, ForestProps.TrailHalfWidth), $"a trunk on the trail at {p}");
            Assert.Equal(0f, ForestProps.TrailDistance(p.X, p.Y), 3);
            Assert.False(ForestProps.Open(p.X, p.Y, 0.1f), $"a prop may stand on the trail at {p}");
        }
    }

    // What the view draws

    [Fact]
    public void MediumClustersStandExactlyOnTheMapsForestProps()
    {
        var expected = new List<Prop>();
        foreach (var p in Props.All)
            if (p.Kind == PropKind.MediumMushroom && p.Area == Area.Forest)
                expected.Add(p);
        Assert.True(expected.Count >= 50, $"only {expected.Count} medium mushrooms in the forest");
        var clusters = ForestScenery.MediumClusters;
        var drawn = ForestScenery.MediumMushrooms;
        Assert.Equal(expected.Count, clusters.Length);
        int next = 0;
        for (int i = 0; i < clusters.Length; i++)
        {
            var p = expected[i];
            var c = clusters[i];
            Assert.Equal((p.X, p.Z, p.Radius, p.Height), (c.X, c.Z, c.Radius, c.Height));
            Assert.Equal(next, c.Start);
            Assert.InRange(c.Count, 1, 12);
            next += c.Count;
            // Each stands on its own obstacle circle in the forest.
            Assert.Contains(Obstacles.All.ToArray(), o => o.X == p.X && o.Z == p.Z && o.Radius == p.Radius);
            float tallest = 0f, reach = 0f;
            for (int k = c.Start; k < c.Start + c.Count; k++)
            {
                var m = drawn[k];
                bool stemless = ForestSpecies.Of(m.Species).Stemless;
                Assert.Equal(c.Species, m.Species);
                Assert.True(m.CapRadius >= m.StemRadius, "the cap is narrower than the stem");
                Assert.InRange(m.CapHeight, 0.01f, m.Height * 0.5f);
                Assert.True(m.Height <= p.Height + 1e-4f, $"a mushroom of the cluster at ({p.X}, {p.Z}) is taller than its prop");
                // Every stem (a stemless fungus' whole body) stands inside the prop's circle, so what
                // looks solid is where the Map's obstacle is.
                float d = Vector2.Distance(new Vector2(m.X, m.Z), new Vector2(p.X, p.Z)) + (stemless ? m.CapRadius : m.StemRadius);
                Assert.True(d <= p.Radius + 1e-3f, $"a mushroom of the cluster at ({p.X}, {p.Z}) stands outside its circle: {d} > {p.Radius}");
                tallest = MathF.Max(tallest, m.Height);
                reach = MathF.Max(reach, d);
            }
            // As tall as the prop (stemless balls and corals no taller than the circle allows, within the Map's 0.8..2.5 m) and filling most of its circle.
            Assert.InRange(tallest, MathF.Min(p.Height, 0.8f) - 1e-4f, p.Height + 1e-4f);
            Assert.True(reach >= 0.55f * p.Radius, $"the {c.Species} cluster ({c.Count}) at ({p.X}, {p.Z}) fills only {reach} of its {p.Radius} m circle");
        }
        Assert.Equal(drawn.Length, next);
    }

    [Fact]
    public void MediumClustersMixSinglesPairsClustersAndTufts()
    {
        int singles = 0, pairs = 0, groups = 0, tufts = 0;
        foreach (var c in ForestScenery.MediumClusters)
        {
            if (ForestSpecies.Of(c.Species).Habit == Habit.Tuft)
            {
                tufts++;
                Assert.True(c.Count >= 7, "an enoki tuft of fewer than 7 stems");
            }
            else if (c.Count == 1)
                singles++;
            else if (c.Count == 2)
                pairs++;
            else
                groups++;
        }
        int n = ForestScenery.MediumClusters.Length;
        Assert.True(singles >= n / 10 && pairs >= n / 10 && groups >= n / 10 && tufts >= 5,
            $"singles {singles}, pairs {pairs}, clusters {groups}, tufts {tufts} of {n}");
        // In a cluster the mushrooms are of different heights.
        foreach (var c in ForestScenery.MediumClusters)
        {
            if (c.Count < 2)
                continue;
            float low = float.MaxValue, high = 0f;
            for (int k = c.Start; k < c.Start + c.Count; k++)
            {
                low = MathF.Min(low, ForestScenery.MediumMushrooms[k].Height);
                high = MathF.Max(high, ForestScenery.MediumMushrooms[k].Height);
            }
            Assert.True(high - low >= 0.05f * high, $"the cluster at ({c.X}, {c.Z}) is all one height");
        }
    }

    [Fact]
    public void GiantStemsAreExactlyTheTrunkObstacles()
    {
        var giants = ForestScenery.Giants;
        Assert.Equal(Ground.ForestTrunkCount, giants.Length);
        for (int i = 0; i < giants.Length; i++)
        {
            var o = Ground.Obstacles[i];
            Assert.Equal(o.X, giants[i].X);
            Assert.Equal(o.Z, giants[i].Z);
            Assert.Equal(o.Radius, giants[i].StemRadius);
            Assert.True(giants[i].Height > 5f);
            Assert.True(giants[i].CapRadius > giants[i].StemRadius); // the cap may overhang
        }
        // The stem mesh at radius scale 1 is never wider than 1 anywhere along it, so a stem of the
        // obstacle's radius stays inside the obstacle's circle.
        Assert.Equal(1f, ForestScenery.StemMeshMaxRadius);
        for (float t = 0f; t <= 1f; t += 0.01f)
            Assert.InRange(ForestScenery.StemProfile(t), 0.3f, ForestScenery.StemFootRadius);
        Assert.Equal(ForestScenery.StemFootRadius, ForestScenery.StemProfile(0f), 4);
    }

    // Planting on the uneven ground

    /// <summary>The ground under a footprint, much denser than ForestFooting samples it: the center, 48 points on the rim and rings at 1/4, 1/2 and 3/4 of the radius.</summary>
    private static (float Low, float High) DenseGround(float x, float z, float radius)
    {
        float low = Ground.HeightAt(x, z), high = low;
        foreach (float f in new[] { 0.25f, 0.5f, 0.75f, 1f })
            for (int k = 0; k < 48; k++)
            {
                float a = k * MathF.Tau / 48f + f;
                float h = Ground.HeightAt(x + MathF.Cos(a) * radius * f, z + MathF.Sin(a) * radius * f);
                low = MathF.Min(low, h);
                high = MathF.Max(high, h);
            }
        return (low, high);
    }

    /// <summary>Every mushroom the view draws: giants, medium, the ring and the tiny ones.</summary>
    private static List<DrawnMushroom> AllDrawnMushrooms()
    {
        var all = new List<DrawnMushroom>();
        all.AddRange(ForestScenery.Giants.ToArray());
        all.AddRange(ForestScenery.MediumMushrooms.ToArray());
        all.AddRange(ForestScenery.RingMushrooms.ToArray());
        foreach (var d in ForestScenery.Decoration)
            if (d.Kind is DecorKind.Mushroom or DecorKind.DarkMushroom)
                all.Add(ForestScenery.AsMushroom(d));
        return all;
    }

    /// <summary>Asserts a stem planted by ForestFooting touches or goes into the ground all round its foot, sunk at most 0.3 m below its lowest point, its cap clear of the ground.</summary>
    private static void AssertPlanted(DrawnMushroom m)
    {
        var p = ForestFooting.Plant(m);
        var (low, high) = DenseGround(m.X, m.Z, m.StemRadius);
        string at = $"mushroom (r {m.StemRadius}) at ({m.X}, {m.Z})";
        Assert.True(p.BaseY <= low, $"{at}: base {p.BaseY} floats above the ground {low}");
        Assert.True(p.BaseY >= low - 0.3f, $"{at}: base {p.BaseY} sunk more than 0.3 m below the ground {low}");
        if (m.Height >= 0.8f)
            Assert.True(p.BaseY <= low - 0.08f, $"{at}: base {p.BaseY} does not sink into the ground {low}");
        // The stem reaches from the base up to the cap, well above the high side of its foot.
        Assert.Equal(p.CapY + m.CapHeight * 0.18f, p.BaseY + p.StemHeight, 3);
        Assert.True(p.CapY > high, $"{at}: cap {p.CapY} sunk into the ground {high}");
        // The cap stands as tall above the ground at the center as asked, never lower.
        float ground = Ground.HeightAt(m.X, m.Z);
        Assert.True(p.CapY + m.CapHeight >= ground + m.Height - 1e-3f, $"{at}: cap lower than its height");
        // Nor does the ground under the cap's rim reach it (to a few centimeters on the cliffs).
        Assert.True(p.CapY + 0.02f * m.CapHeight >= DenseGround(m.X, m.Z, m.CapRadius * 0.93f).High - 0.05f, $"{at}: cap rim in the ground");
    }

    [Fact]
    public void EveryDrawnMushroomStandsWhollyOnTheGround()
    {
        var all = AllDrawnMushrooms();
        Assert.True(all.Count > 800, $"only {all.Count} mushrooms");
        foreach (var m in all)
            AssertPlanted(m);
    }

    [Fact]
    public void StemsArePlantedOnSlopesHillsAndRoughGround()
    {
        // Many sample positions and sizes: over the forest's hills, the Deep Forest's rough
        // ground, the cliffs past the ring and the roads; where the ground slopes, the base drops.
        int sloped = 0;
        for (int i = 0; i < 1500; i++)
        {
            float x = -280f + 380f * ForestRandom.Value(i, 900);
            float z = -130f + 260f * ForestRandom.Value(i, 901);
            float r = 0.05f + 3.45f * ForestRandom.Value(i, 902);
            float height = r < 0.3f ? 0.3f : 2f + 10f * ForestRandom.Value(i, 903);
            float capRadius = r * (1.5f + 2f * ForestRandom.Value(i, 904));
            var m = new DrawnMushroom(DecorKind.Mushroom, x, z, 0f, r, height, capRadius, MathF.Min(capRadius * 0.6f, height * 0.5f), i);
            AssertPlanted(m);
            if (Ground.HeightAt(x, z) - DenseGround(x, z, r).Low > 0.2f)
                sloped++;
        }
        Assert.True(sloped > 100, $"only {sloped} samples on sloped ground");
    }

    [Fact]
    public void FootingTakesTheLowestOfTheFootprint()
    {
        for (int i = 0; i < 400; i++)
        {
            float x = -250f + 340f * ForestRandom.Value(i, 910), z = -100f + 200f * ForestRandom.Value(i, 911);
            float r = 0.1f + 3f * ForestRandom.Value(i, 912);
            float low = ForestFooting.Lowest(x, z, r);
            Assert.True(low <= Ground.HeightAt(x, z));
            Assert.True(ForestFooting.RimSamples >= 8);
            for (int k = 0; k < ForestFooting.RimSamples; k++)
            {
                float a = k * MathF.Tau / ForestFooting.RimSamples;
                Assert.True(low <= Ground.HeightAt(x + MathF.Cos(a) * r, z + MathF.Sin(a) * r) + 1e-5f);
            }
            Assert.True(ForestFooting.Highest(x, z, r) >= Ground.HeightAt(x, z));
            Assert.Equal(low - ForestFooting.SinkFor(5f), ForestFooting.BaseY(x, z, r, 5f));
            Assert.InRange(ForestFooting.SinkFor(5f), 0.1f, 0.2f);
        }
        // On flat ground a giant stands where it did: cap top Height above the ground, base just sunk.
        var flat = new DrawnMushroom(DecorKind.Mushroom, 600f, 600f, 0f, 2f, 10f, 5f, 3f, 1);
        var p = ForestFooting.Plant(flat);
        Assert.Equal(Areas.HomeFloorY - ForestFooting.Sink, p.BaseY, 4);
        Assert.Equal(Areas.HomeFloorY + 10f, p.CapY + 3f, 4);
    }

    [Fact]
    public void BigStemsFlareIntoTheGround()
    {
        // The skirt is wider than the stem's foot up to the ground line, never wider than the stem's
        // circle, and its top hides inside the stem.
        Assert.Equal(ForestScenery.StemMeshMaxRadius, ForestScenery.SkirtProfile(0f), 4);
        Assert.Equal(ForestScenery.StemMeshMaxRadius, ForestScenery.SkirtProfile(ForestFooting.SkirtGroundFraction), 4);
        for (float t = 0f; t <= 1f; t += 0.01f)
        {
            Assert.InRange(ForestScenery.SkirtProfile(t), ForestFooting.SkirtTopRadius - 1e-4f, ForestScenery.StemMeshMaxRadius);
            Assert.True(ForestScenery.SkirtProfile(1f) < ForestScenery.StemProfile(t));
        }
        int flared = 0;
        foreach (var m in AllDrawnMushrooms())
        {
            var p = ForestFooting.Plant(m);
            if (m.StemRadius < ForestFooting.FlareMinRadius)
            {
                Assert.Equal(0f, p.SkirtHeight);
                continue;
            }
            flared++;
            // It rises out of the ground past the high side of the foot, and stays inside the stem.
            Assert.True(p.BaseY + p.SkirtHeight > DenseGround(m.X, m.Z, m.StemRadius).High,
                $"flare of the mushroom at ({m.X}, {m.Z}) hidden in the ground");
            Assert.True(p.SkirtHeight <= p.StemHeight * ForestFooting.SkirtMaxFraction + 1e-4f);
            // Where it meets the high side of the ground it sticks out of the stem, visibly.
            float ground = DenseGround(m.X, m.Z, m.StemRadius).High - p.BaseY;
            float skirtR = ForestScenery.SkirtProfile(ground / p.SkirtHeight), stemR = ForestScenery.StemProfile(ground / p.StemHeight);
            Assert.True(skirtR >= stemR + 0.04f, $"flare of the mushroom at ({m.X}, {m.Z}) hidden in the stem: {skirtR} vs {stemR}");
        }
        Assert.True(flared >= Ground.ForestTrunkCount, $"only {flared} flared stems");
    }

    [Fact]
    public void RingMushroomsAreDrawnOnTheRingCircles()
    {
        var ring = ForestRing.Mushrooms;
        var drawn = ForestScenery.RingMushrooms;
        Assert.Equal(ring.Length, drawn.Length);
        for (int i = 0; i < ring.Length; i++)
        {
            Assert.Equal(ring[i].X, drawn[i].X);
            Assert.Equal(ring[i].Z, drawn[i].Z);
            Assert.Equal(ring[i].Radius, drawn[i].StemRadius);
            Assert.True(drawn[i].Height > 7f);
        }
    }

    [Fact]
    public void DecorationIsSmallAndWalkable()
    {
        var decoration = ForestScenery.Decoration;
        var kinds = new HashSet<DecorKind>();
        foreach (var d in decoration)
        {
            kinds.Add(d.Kind);
            Assert.InRange(d.Height, 0.02f, ForestScenery.DecorationMaxHeight - 0.001f);
            Assert.InRange(d.Radius, 0.01f, ForestScenery.DecorationMaxRadius - 0.001f);
            if (d.Kind is DecorKind.Mushroom or DecorKind.DarkMushroom)
                Assert.InRange(d.CapHeight, 0.01f, d.Height * 0.5f);
            // Off trunks and paths (grass may grow on the trail, in the gap and around the square).
            Assert.False(Obstacles.Blocks(d.X, d.Z, 0.05f), $"{d.Kind} at ({d.X}, {d.Z}) inside an obstacle");
            Assert.True(ForestLayout.PathDistance(d.X, d.Z) > ForestLayout.PathHalfWidth, $"{d.Kind} at ({d.X}, {d.Z}) on a path");
            if (d.Kind != DecorKind.Grass)
                Assert.True(ForestProps.Open(d.X, d.Z, 0f), $"{d.Kind} at ({d.X}, {d.Z}) not in the open");
        }
        Assert.Equal(new HashSet<DecorKind> { DecorKind.Mushroom, DecorKind.DarkMushroom, DecorKind.Fern, DecorKind.Flower, DecorKind.Grass }, kinds);
        int tiny = 0;
        foreach (var d in decoration)
            if (d.Kind == DecorKind.Mushroom)
                tiny++;
        Assert.True(tiny > 300, $"only {tiny} tiny mushrooms");
    }

    [Fact]
    public void EverythingTallerThanTheLimitInsideTheSquareStandsOnAnObstacle()
    {
        // Inside the square only the giant and medium mushrooms rise above the decoration limit,
        // and each stem lies wholly within one obstacle circle; the ring stands off all walkable ground.
        var tall = new List<DrawnMushroom>();
        tall.AddRange(ForestScenery.Giants.ToArray());
        tall.AddRange(ForestScenery.MediumMushrooms.ToArray());
        foreach (var g in ForestScenery.Giants)
            Assert.True(g.Height > ForestScenery.DecorationMaxHeight);
        foreach (var c in ForestScenery.MediumClusters)
            Assert.True(c.Height > ForestScenery.DecorationMaxHeight);
        foreach (var m in tall)
        {
            bool inside = false;
            foreach (var o in Ground.Obstacles)
                if (Vector2.Distance(new Vector2(o.X, o.Z), new Vector2(m.X, m.Z)) + m.StemRadius * ForestScenery.StemMeshMaxRadius <= o.Radius + 1e-4f)
                    inside = true;
            Assert.True(inside, $"mushroom at ({m.X}, {m.Z}) stands outside every obstacle");
        }
        foreach (var d in ForestScenery.Decoration)
            Assert.True(d.Height < ForestScenery.DecorationMaxHeight);
        foreach (var r in ForestRing.Mushrooms)
            Assert.True(Areas.Depth(r.X, r.Z) < -r.Radius, $"ring mushroom at ({r.X}, {r.Z}) on walkable ground");
    }

    [Fact]
    public void PropsKeepOffPathsAndTrunks()
    {
        Assert.False(ForestProps.Open(5f, 70f, 0.2f)); // on the path near the start
        var o = Obstacles.All[0];
        Assert.False(ForestProps.Open(o.X, o.Z + o.Radius + 0.2f, 0.2f));
        Assert.False(ForestProps.Open(ForestBounds.HalfSize + 1f, 0f, 0.2f)); // outside the square
        Assert.True(ForestProps.Open(-80f, -80f, 0.2f) || ForestProps.Open(-78f, -80f, 0.2f) || ForestProps.Open(-80f, -78f, 0.2f));
    }

    // Core

    [Fact]
    public void ModuleStartsQuietly()
    {
        var bus = new FakeBus();
        var forest = new ForestModule();
        forest.Start(bus);
        Assert.True(forest.Started);
        Assert.Equal(0, bus.Published);
        Assert.False(forest.IsNight);
    }

    [Fact]
    public void ModuleFollowsTheTimeOfDay()
    {
        var bus = new FakeBus();
        var forest = new ForestModule();
        forest.Start(bus);
        Assert.NotNull(bus.Time);
        bus.Time!.Handle(new TimeOfDay(1, 21f, true));
        Assert.True(forest.IsNight);
        bus.Time.Handle(new TimeOfDay(2, 7f, false));
        Assert.False(forest.IsNight);
        for (int i = 0; i < 1000; i++)
            bus.Time.Handle(new TimeOfDay(2, i % 24, TimeOfDay.IsNightAt(i % 24)));
        Assert.Equal(0, bus.Published);
    }

    [Fact]
    public void ModuleDoesNothingPerTick()
    {
        var bus = new FakeBus();
        var forest = new ForestModule();
        forest.Start(bus);
        for (int i = 0; i < 1000; i++)
            forest.Tick(new TickContext(i, 1.0 / 60));
        Assert.Equal(0, bus.Published);
    }

    [Fact]
    public void TickBeforeStartThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new ForestModule().Tick(new TickContext(0, 1.0 / 60)));
        Assert.Throws<ArgumentNullException>(() => new ForestModule().Start(null!));
    }

    // Vivid mushrooms (Tony, 2026-10-25)

    /// <summary>Hue in degrees (0..360), saturation and value (0..1) of an RGB color.</summary>
    private static (float Hue, float Sat, float Val) Hsv(Vector3 c)
    {
        float max = MathF.Max(c.X, MathF.Max(c.Y, c.Z)), min = MathF.Min(c.X, MathF.Min(c.Y, c.Z));
        float d = max - min;
        float hue = d <= 0f ? 0f
            : max == c.X ? 60f * (((c.Y - c.Z) / d + 6f) % 6f)
            : max == c.Y ? 60f * ((c.Z - c.X) / d + 2f)
            : 60f * ((c.X - c.Y) / d + 4f);
        return (hue, max <= 0f ? 0f : d / max, max);
    }

    private static void AssertInRgb(Vector3 c)
    {
        Assert.InRange(c.X, 0f, 1f);
        Assert.InRange(c.Y, 0f, 1f);
        Assert.InRange(c.Z, 0f, 1f);
    }

    /// <summary>The palette before the vivid pass: ordinary, glowing and dark caps, and the stem creams.</summary>
    private static readonly Vector3[] OldCaps =
    {
        new(0.88f, 0.24f, 0.22f), new(0.93f, 0.36f, 0.26f), new(0.8f, 0.18f, 0.24f),
        new(0.62f, 0.4f, 0.24f), new(0.72f, 0.5f, 0.3f), new(0.55f, 0.33f, 0.22f),
        new(0.6f, 0.36f, 0.72f), new(0.72f, 0.42f, 0.78f),
    };

    private static readonly Vector3[] OldGlowCaps = { new(0.62f, 0.45f, 0.95f), new(0.95f, 0.5f, 0.85f), new(0.4f, 0.8f, 0.9f) };

    private static readonly Vector3[] OldDarkCaps =
    {
        new(0.32f, 0.2f, 0.42f), new(0.24f, 0.22f, 0.4f), new(0.42f, 0.18f, 0.28f), new(0.2f, 0.28f, 0.36f),
    };

    [Fact]
    public void CapsAreClearlySaturatedAndBright()
    {
        // Every ordinary cap is strongly saturated (never pastel, dull pink or washed-out white) and
        // bright, and still has room below white so daylight does not flatten it.
        foreach (var c in ForestPalette.Caps)
        {
            AssertInRgb(c);
            var (_, sat, val) = Hsv(c);
            Assert.InRange(sat, 0.78f, 1f);
            Assert.InRange(val, 0.75f, 1f);
            Assert.True(MathF.Min(c.X, MathF.Min(c.Y, c.Z)) <= 0.2f, $"cap {c} has too much white in it");
        }
        foreach (var c in ForestPalette.GlowCaps)
        {
            AssertInRgb(c);
            var (_, sat, val) = Hsv(c);
            Assert.InRange(sat, 0.65f, 1f);
            Assert.InRange(val, 0.95f, 1f);
        }
        // The Deep Forest's caps stay dark but rich, not muddy grey.
        foreach (var c in ForestPalette.DarkCaps)
        {
            AssertInRgb(c);
            var (_, sat, val) = Hsv(c);
            Assert.InRange(sat, 0.65f, 1f);
            Assert.InRange(val, 0.5f, 0.65f);
        }
    }

    [Fact]
    public void CapsAreMoreVividThanBefore()
    {
        // Each cap, color by color, is more saturated than the one it replaces and no darker.
        void Compare(Vector3[] now, Vector3[] before)
        {
            Assert.Equal(before.Length, now.Length);
            for (int i = 0; i < now.Length; i++)
            {
                var (_, satNow, valNow) = Hsv(now[i]);
                var (_, satBefore, valBefore) = Hsv(before[i]);
                Assert.True(satNow >= satBefore + 0.15f, $"cap {i} {now[i]} is not clearly more saturated than {before[i]}");
                Assert.True(valNow >= valBefore - 0.001f, $"cap {i} {now[i]} is darker than {before[i]}");
            }
        }
        Compare(ForestPalette.Caps, OldCaps);
        // The glowing caps that replaced old ones (the bioluminescent blue and green came after).
        Compare(ForestPalette.GlowCaps.AsSpan(0, OldGlowCaps.Length).ToArray(), OldGlowCaps);
        Compare(ForestPalette.DarkCaps, OldDarkCaps);
    }

    [Fact]
    public void RedCapsArePureCherryOrTomatoRed()
    {
        // Reds (hue within 15 degrees of red): pure and bright, green and blue nearly gone, not brownish or pink.
        int reds = 0;
        foreach (var c in ForestPalette.Caps)
        {
            var (hue, sat, val) = Hsv(c);
            if (hue > 15f && hue < 340f)
                continue;
            reds++;
            Assert.True(hue <= 12f || hue >= 350f, $"red cap {c} drifts toward orange or pink (hue {hue})");
            Assert.True(sat >= 0.88f && val >= 0.85f, $"red cap {c} is not a pure bright red");
            Assert.True(c.Y <= 0.22f && c.Z <= 0.2f, $"red cap {c} is brownish or pinkish");
        }
        Assert.Equal(3, reds);
    }

    [Fact]
    public void CapsKeepEveryHueFamily()
    {
        // Still reds, oranges/browns and purples, so the forest keeps its mix; glowing caps violet, pink, cyan.
        static int Count(Vector3[] colors, float from, float to)
        {
            int n = 0;
            foreach (var c in colors)
            {
                float hue = Hsv(c).Hue;
                if (from <= to ? hue >= from && hue < to : hue >= from || hue < to)
                    n++;
            }
            return n;
        }
        Assert.Equal(3, Count(ForestPalette.Caps, 340f, 15f));
        Assert.Equal(3, Count(ForestPalette.Caps, 15f, 45f));
        Assert.Equal(2, Count(ForestPalette.Caps, 260f, 310f));
        Assert.Equal(1, Count(ForestPalette.GlowCaps, 250f, 290f));
        Assert.Equal(1, Count(ForestPalette.GlowCaps, 290f, 340f));
        Assert.Equal(1, Count(ForestPalette.GlowCaps, 170f, 210f));
    }

    [Fact]
    public void SpotsArePureWhiteAndStemsCleanLightCream()
    {
        Assert.Equal(new Vector3(1f, 1f, 1f), ForestPalette.Spot);
        foreach (var c in new[] { ForestPalette.StemLight, ForestPalette.StemShade, ForestPalette.Gills })
        {
            AssertInRgb(c);
            var (hue, sat, val) = Hsv(c);
            // Light, warm (yellowish) and only faintly tinted: cream, not beige or tan.
            Assert.InRange(val, 0.97f, 1f);
            Assert.InRange(sat, 0.05f, 0.2f);
            Assert.InRange(hue, 30f, 55f);
            Assert.True(c.Y >= 0.9f, $"{c} is too dark for clean light cream");
        }
    }

    [Fact]
    public void CapsHaveAGlossAndARimAndGlowAboutTwiceAsBright()
    {
        // A soft gloss (present, but not a hard white blob) and a rim light stronger than the toon light's 0.18.
        Assert.InRange(ForestPalette.CapGloss, 0.15f, 0.5f);
        Assert.InRange(ForestPalette.CapRim, 0.3f, 0.6f);
        // At night the emission is about twice what it was (2.4 before: scale 1, night 2.4); by day
        // no more than before (1), so glowing caps do not wash out to white in the sun.
        Assert.InRange(ForestPalette.GlowEmission * ForestPalette.NightGlow, 2f * 2.4f * 0.9f, 2f * 2.4f * 1.1f);
        Assert.InRange(ForestPalette.GlowEmission * ForestPalette.DayGlow, 0.5f, 1f);
        Assert.True(ForestPalette.NightGlow > ForestPalette.DayGlow * 2f, "glowing caps should clearly glow brighter at night");
    }

    /// <summary>The repository's root (where hub.json is), found from the test's output folder.</summary>
    private static string RepoRoot()
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "hub.json")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    [Fact]
    public void ViewAndCapShaderDrawTheVividPalette()
    {
        // The view takes every mushroom color and the cap shading from ForestPalette, and the cap
        // shader has the gloss band, the rim and the glow scale it sets.
        string forest = System.IO.Path.Combine(RepoRoot(), "modules", "Forest", "Godot");
        string view = System.IO.File.ReadAllText(System.IO.Path.Combine(forest, "ForestView.cs"));
        foreach (string use in new[]
        {
            "ForestSpecies.Colors", "ForestSpecies.Of", "ForestScenery.Shelves", "ForestPalette.ShelfCaps", "ForestPalette.DarkCaps", "ForestPalette.StemLight",
            "ForestPalette.StemShade", "ForestPalette.Spot", "ForestPalette.Gills", "ForestPalette.CapGloss",
            "ForestPalette.CapRim", "ForestPalette.GlowEmission", "ForestPalette.DayGlow", "ForestPalette.NightGlow",
        })
            Assert.Contains(use, view);
        string cap = System.IO.File.ReadAllText(System.IO.Path.Combine(forest, "Shaders", "mushroom_cap.gdshader"));
        foreach (string part in new[] { "uniform float gloss", "uniform float rim", "uniform float glow_emission", "SPECULAR_LIGHT", "glow_emission * night_glow" })
            Assert.Contains(part, cap);
        Assert.DoesNotContain("specular_disabled", cap);
        // A pattern for every kind's CapPattern (the view passes it as INSTANCE_CUSTOM.z = pattern / 8).
        Assert.Contains("(int)look.Pattern / 8f", view);
        Assert.Contains("int(custom.z * 8.0 + 0.5)", cap);
        foreach (var pattern in Enum.GetValues<CapPattern>())
            if (pattern != CapPattern.Plain)
                Assert.Contains($"mode == {(int)pattern}", cap);
        // Stems and their ink outlines slim and lean as ForestScenery says.
        foreach (string shader in new[] { "toon_color.gdshader", "outline.gdshader" })
            Assert.Contains("bend_stem(VERTEX, UV.y, INSTANCE_CUSTOM)", System.IO.File.ReadAllText(System.IO.Path.Combine(forest, "Shaders", shader)));
        string bend = System.IO.File.ReadAllText(System.IO.Path.Combine(forest, "Shaders", "stem_bend.gdshaderinc"));
        Assert.Contains("(t - 0.1) / 0.5", bend);
        Assert.Contains("custom.y * t * t", bend);
        Assert.Contains("SetShaderParameter(\"bend\", true)", view);
        // One MultiMesh per cap shape and one for the shelves, not one per mushroom.
        Assert.Contains("ForestMeshes.Cap(shape)", view);
        Assert.Contains("ForestMeshes.Shelf()", view);
        // The ground and foliage take ForestPalette's vibrant colors.
        foreach (string use in new[]
        {
            "ForestPalette.Grass", "ForestPalette.GrassLight", "ForestPalette.Moss", "ForestPalette.Meadow", "ForestPalette.Dirt",
            "ForestPalette.Soil", "ForestPalette.SoilDark", "ForestPalette.FernDark", "ForestPalette.FernLight",
            "ForestPalette.Flowers", "ForestPalette.FlowerStem",
        })
            Assert.Contains(use, view);
    }

    // Variety: many kinds of mushrooms and fungi

    /// <summary>Every tall spot the view draws a mushroom on: the giants, the medium clusters and the ring, with its kind.</summary>
    private static List<(Vector2 At, MushroomKind Kind)> TallSpots()
    {
        var spots = new List<(Vector2, MushroomKind)>();
        foreach (var g in ForestScenery.Giants)
            spots.Add((new Vector2(g.X, g.Z), g.Species));
        foreach (var c in ForestScenery.MediumClusters)
            spots.Add((new Vector2(c.X, c.Z), c.Species));
        foreach (var r in ForestScenery.RingMushrooms)
            spots.Add((new Vector2(r.X, r.Z), r.Species));
        return spots;
    }

    /// <summary>Asserts no spot shares its kind with any of its <see cref="ForestScenery.Neighbors"/> nearest.</summary>
    private static void AssertNeighborsDiffer(List<(Vector2 At, MushroomKind Kind)> spots, string what)
    {
        Assert.True(ForestScenery.Neighbors >= 2);
        for (int i = 0; i < spots.Count; i++)
        {
            var nearest = new List<(float D, int J)>();
            for (int j = 0; j < spots.Count; j++)
                if (j != i)
                    nearest.Add((Vector2.DistanceSquared(spots[i].At, spots[j].At), j));
            nearest.Sort();
            for (int k = 0; k < ForestScenery.Neighbors; k++)
                Assert.True(spots[nearest[k].J].Kind != spots[i].Kind,
                    $"{what} at {spots[i].At} and its neighbor at {spots[nearest[k].J].At} are both {spots[i].Kind}");
        }
    }

    [Fact]
    public void NoTwoNeighborsAreTheSameKind()
    {
        AssertNeighborsDiffer(TallSpots(), "tall mushroom");
        var patches = new List<(Vector2, MushroomKind)>();
        foreach (var p in ForestScenery.SmallPatches)
            patches.Add((new Vector2(p.X, p.Z), p.Species));
        AssertNeighborsDiffer(patches, "patch of tiny fungi");
    }

    [Fact]
    public void ManyKindsGrowEverywhere()
    {
        static HashSet<MushroomKind> Kinds(IEnumerable<MushroomKind> kinds) => new(kinds);
        var giants = ForestScenery.Giants.ToArray();
        var giantKinds = Kinds(giants.Select(g => g.Species));
        var mediumKinds = Kinds(ForestScenery.MediumClusters.ToArray().Select(c => c.Species));
        var smallKinds = Kinds(ForestScenery.SmallPatches.ToArray().Select(c => c.Species));
        Assert.True(giantKinds.Count >= 8, $"only {giantKinds.Count} kinds of giants");
        Assert.True(mediumKinds.Count >= 10, $"only {mediumKinds.Count} kinds of medium mushrooms");
        Assert.Equal(Enum.GetValues<MushroomKind>().Length, smallKinds.Count);
        // Mixed, none taking over.
        foreach (var kind in giantKinds)
            Assert.True(giants.Count(g => g.Species == kind) <= giants.Length / 4, $"too many {kind} giants");
        // Caps of many shapes: domes, flat, funnels, bells, wavy, conical, and among the small ones balls, cups and corals.
        var giantShapes = new HashSet<CapShape>(giants.Select(g => g.Shape));
        Assert.Superset(new HashSet<CapShape> { CapShape.Dome, CapShape.Flat, CapShape.Funnel, CapShape.Bell, CapShape.Cone, CapShape.Wavy }, giantShapes);
        var smallShapes = new HashSet<CapShape>();
        foreach (var d in ForestScenery.Decoration)
            if (d.Kind == DecorKind.Mushroom)
                smallShapes.Add(d.Shape);
        Assert.Equal(Enum.GetValues<CapShape>().Length, smallShapes.Count);
        // Every mushroom's shape is one its kind grows.
        foreach (var m in AllDrawnMushrooms())
            Assert.Contains(m.Shape, ForestSpecies.Of(m.Species).Shapes);
        // Bioluminescent ones among the giants, the medium and the tiny ones.
        Assert.Contains(MushroomKind.Glowing, giantKinds);
        Assert.Contains(MushroomKind.Glowing, mediumKinds);
    }

    [Fact]
    public void KindsLookLikeThemselves()
    {
        foreach (var kind in Enum.GetValues<MushroomKind>())
        {
            var look = ForestSpecies.Of(kind);
            Assert.Equal(kind, look.Kind);
            Assert.NotEmpty(look.Shapes);
            Assert.NotEmpty(ForestSpecies.Colors(kind));
            Assert.True(look.CapPerHeight.Min > 0f && look.CapPerHeight.Min <= look.CapPerHeight.Max);
            Assert.True(look.Slender.Min > 0.5f && look.Slender.Max <= 1f);
            // Fungi with no stem are never giants on a trunk.
            if (look.Stemless)
                Assert.Equal(Tier.None, look.Tiers & Tier.Giant);
        }
        Assert.Equal(CapPattern.Spots, ForestSpecies.Of(MushroomKind.FlyAgaric).Pattern);
        Assert.Equal(new[] { CapShape.Funnel }, ForestSpecies.Of(MushroomKind.Chanterelle).Shapes);
        Assert.Equal(new[] { CapShape.Bell }, ForestSpecies.Of(MushroomKind.InkCap).Shapes);
        Assert.Equal(CapPattern.Honeycomb, ForestSpecies.Of(MushroomKind.Morel).Pattern);
        Assert.Equal(Habit.Tuft, ForestSpecies.Of(MushroomKind.Enoki).Habit);
        Assert.True(ForestSpecies.Of(MushroomKind.Glowing).Glows);
        Assert.Single(ForestSpecies.All.ToArray(), l => l.Glows);
        // Parasols are tall and thin with wide flat caps; boletes fat.
        var parasol = ForestSpecies.Of(MushroomKind.Parasol);
        var bolete = ForestSpecies.Of(MushroomKind.Bolete);
        Assert.True(parasol.GiantHeight > bolete.GiantHeight && parasol.GiantCap.Min > bolete.GiantCap.Max && parasol.Slender.Max < bolete.Slender.Min);
        Assert.True(parasol.CapDepth.Max < ForestSpecies.Of(MushroomKind.InkCap).CapDepth.Min);
    }

    [Fact]
    public void GiantsVaryInHeightCapStemAndLean()
    {
        var giants = ForestScenery.Giants.ToArray();
        Assert.True(giants.Max(g => g.Height) - giants.Min(g => g.Height) >= 8f, "giants are all about one height");
        var ratios = giants.Select(g => g.CapRadius / g.StemRadius).ToArray();
        Assert.True(ratios.Max() - ratios.Min() >= 1.5f, "giant caps are all about one size for their stems");
        var depths = giants.Select(g => g.CapHeight / g.CapRadius).ToArray();
        Assert.True(depths.Max() - depths.Min() >= 0.8f, "giant caps are all about one depth");
        Assert.Contains(giants, g => g.Slender <= 0.75f);
        Assert.Contains(giants, g => g.Slender >= 0.95f);
        Assert.True(giants.Count(g => g.Lean > 0.01f) >= giants.Length / 4, "hardly any giant leans");
        foreach (var g in giants)
            Assert.InRange(g.Lean, 0f, ForestScenery.MaxGiantLean);
        foreach (var m in AllDrawnMushrooms())
        {
            Assert.InRange(m.Lean, 0f, ForestScenery.MaxLean);
            Assert.InRange(m.Slender, 0.5f, 1f);
        }
        // A slim stem keeps its full foot (its circle) and narrows only above it; never wider.
        foreach (float slender in new[] { 0.55f, 0.7f, 1f })
        {
            Assert.Equal(ForestScenery.StemFootRadius, ForestScenery.StemRadiusAt(slender, 0f), 4);
            for (float t = 0f; t <= 1f; t += 0.01f)
                Assert.InRange(ForestScenery.StemRadiusAt(slender, t), 0.3f * slender, ForestScenery.StemProfile(t) + 1e-5f);
        }
        Assert.Equal(0.55f * ForestScenery.StemProfile(0.8f), ForestScenery.StemRadiusAt(0.55f, 0.8f), 4);
        // A slim giant's root flare still hides its top inside the stem.
        foreach (var m in AllDrawnMushrooms())
        {
            var p = ForestFooting.Plant(m);
            if (p.SkirtHeight > 0f)
                Assert.True(ForestFooting.SkirtTopRadius < ForestScenery.StemRadiusAt(m.Slender, p.SkirtHeight / p.StemHeight),
                    $"the flare of the slim stem at ({m.X}, {m.Z}) pokes out above the stem");
        }
        // Leaning moves the top along the stem's local +X, more toward the top, not the foot.
        var lean = new DrawnMushroom(DecorKind.Mushroom, 0f, 0f, 0f, 1f, 10f, 3f, 2f, 1, Lean: 0.05f);
        Assert.Equal(Vector2.Zero, ForestScenery.LeanOffset(lean, 9f, 0f));
        Assert.Equal(0.45f, ForestScenery.LeanOffset(lean, 9f, 1f).X, 4);
        Assert.Equal(-0.45f, ForestScenery.LeanOffset(lean with { Yaw = MathF.PI / 2f }, 9f, 1f).Y, 4);
    }

    [Fact]
    public void ShelfFungiGrowOnSomeGiantStems()
    {
        var giants = ForestScenery.Giants;
        var with = new HashSet<int>();
        foreach (var s in ForestScenery.Shelves)
        {
            Assert.InRange(s.Giant, 0, giants.Length - 1);
            with.Add(s.Giant);
            var g = giants[s.Giant];
            var p = ForestFooting.Plant(g);
            Assert.InRange(s.Along, 0.1f, 0.6f);
            Assert.InRange(s.Radius, 0.1f * g.StemRadius, 0.8f * g.StemRadius);
            Assert.InRange(s.Thickness, 0.1f * s.Radius, 0.4f * s.Radius);
            // On the stem's surface, above the high side of the ground and under the cap.
            var at = ForestScenery.StemPoint(g, p, s.Along, s.Angle);
            var axis = ForestScenery.LeanOffset(g, p.StemHeight, s.Along);
            float r = Vector2.Distance(new Vector2(at.X, at.Z), new Vector2(g.X + axis.X, g.Z + axis.Y));
            Assert.Equal(g.StemRadius * ForestScenery.StemRadiusAt(g.Slender, s.Along), r, 3);
            Assert.True(at.Y > DenseGround(g.X, g.Z, g.StemRadius).High + 0.5f, $"a shelf on the giant at ({g.X}, {g.Z}) in the ground");
            Assert.True(at.Y + s.Thickness < p.CapY, $"a shelf on the giant at ({g.X}, {g.Z}) above its cap");
        }
        Assert.InRange(with.Count, giants.Length * 15 / 100, giants.Length * 60 / 100);
    }

    [Fact]
    public void TinyFungiLineThePathsAndFillTheClearings()
    {
        int byPaths = 0, inClearings = 0;
        var kindsByPaths = new HashSet<MushroomKind>();
        foreach (var d in ForestScenery.Decoration)
        {
            if (d.Kind != DecorKind.Mushroom)
                continue;
            float path = ForestLayout.PathDistance(d.X, d.Z);
            if (path <= ForestLayout.PathHalfWidth + 1.6f)
            {
                byPaths++;
                kindsByPaths.Add(d.Species);
            }
            if (ForestLayout.ClearingDepth(d.X, d.Z) > 0f)
                inClearings++;
        }
        Assert.True(byPaths >= 300, $"only {byPaths} tiny fungi along the paths");
        Assert.True(inClearings >= 100, $"only {inClearings} tiny fungi in the clearings");
        Assert.True(kindsByPaths.Count >= 10, $"only {kindsByPaths.Count} kinds along the paths");
        // Each patch is one kind, near its spot, of varied heights when it has several.
        var decoration = ForestScenery.Decoration;
        foreach (var p in ForestScenery.SmallPatches)
            for (int k = p.Start; k < p.Start + p.Count; k++)
            {
                Assert.Equal(p.Species, decoration[k].Species);
                Assert.True(Vector2.Distance(new Vector2(decoration[k].X, decoration[k].Z), new Vector2(p.X, p.Z)) <= ForestScenery.PatchRadius);
            }
    }

    // The vivid caps of every kind and the vibrant world

    [Fact]
    public void EveryKindsCapsAreVivid()
    {
        foreach (var kind in Enum.GetValues<MushroomKind>())
            foreach (var c in ForestSpecies.Colors(kind))
            {
                AssertInRgb(c);
                var (_, sat, val) = Hsv(c);
                Assert.True(sat >= 0.5f && val >= 0.84f, $"{kind} cap {c} is not saturated and bright");
            }
        foreach (var c in ForestPalette.ShelfCaps)
        {
            var (_, sat, val) = Hsv(c);
            Assert.True(sat >= 0.75f && val >= 0.85f, $"shelf {c} is dull");
        }
        // Glowing ones include bioluminescent blue and green.
        Assert.Contains(ForestPalette.GlowCaps, c => Hsv(c).Hue is > 205f and < 240f);
        Assert.Contains(ForestPalette.GlowCaps, c => Hsv(c).Hue is > 100f and < 150f);
        // The kinds read apart: golden chanterelles, red fly agarics, purple amethysts, scarlet cups.
        Assert.All(ForestPalette.ChanterelleCaps, c => Assert.InRange(Hsv(c).Hue, 35f, 55f));
        Assert.All(ForestPalette.AmethystCaps, c => Assert.InRange(Hsv(c).Hue, 260f, 310f));
        Assert.All(ForestPalette.CupCaps, c => Assert.True(Hsv(c).Hue < 10f || Hsv(c).Hue > 350f));
    }

    private static float Luminance(Vector3 c) => 0.2126f * c.X + 0.7152f * c.Y + 0.0722f * c.Z;

    [Fact]
    public void TheWorldIsLushBrightAndReadable()
    {
        // Greens (grass, sunny patches, moss, meadow, ferns): saturated, clearly more than the old
        // grey-olive ones, bright but not neon or washed out.
        var greens = new[] { ForestPalette.Grass, ForestPalette.GrassLight, ForestPalette.Moss, ForestPalette.Meadow, ForestPalette.FernDark, ForestPalette.FernLight, ForestPalette.FlowerStem };
        foreach (var c in greens)
        {
            AssertInRgb(c);
            var (hue, sat, val) = Hsv(c);
            Assert.InRange(hue, 70f, 140f);
            Assert.True(sat >= 0.7f, $"green {c} is greyish");
            Assert.InRange(val, 0.55f, 0.92f);
        }
        var before = new[] { (ForestPalette.Grass, new Vector3(0.5f, 0.74f, 0.33f)), (ForestPalette.Moss, new Vector3(0.3f, 0.56f, 0.32f)), (ForestPalette.Meadow, new Vector3(0.66f, 0.8f, 0.38f)) };
        foreach (var (now, old) in before)
        {
            Assert.True(Hsv(now).Sat >= Hsv(old).Sat + 0.15f, $"{now} is not clearly more saturated than {old}");
            Assert.True(Hsv(now).Val >= Hsv(old).Val - 0.03f, $"{now} is darker than {old}");
        }
        // Yellow-green highlights: the sunny patches are yellower and lighter than the grass.
        Assert.True(Hsv(ForestPalette.GrassLight).Hue < Hsv(ForestPalette.Grass).Hue - 10f);
        Assert.True(Luminance(ForestPalette.GrassLight) > Luminance(ForestPalette.Grass));
        // Warm golden paths that stand out from the grass.
        var (dirtHue, dirtSat, dirtVal) = Hsv(ForestPalette.Dirt);
        Assert.InRange(dirtHue, 30f, 48f);
        Assert.InRange(dirtSat, 0.55f, 0.8f);
        Assert.InRange(dirtVal, 0.85f, 1f);
        Assert.True(dirtSat >= Hsv(new Vector3(0.7f, 0.52f, 0.36f)).Sat + 0.1f, "the paths are no more golden than before");
        Assert.True(Luminance(ForestPalette.Dirt) >= Luminance(ForestPalette.Grass) + 0.15f, "paths do not stand out from the grass");
        Assert.True(Luminance(ForestPalette.Dirt) >= Luminance(ForestPalette.Meadow) + 0.08f, "paths do not stand out from the meadows");
        Assert.True(Luminance(ForestPalette.SoilDark) < Luminance(ForestPalette.Soil));
        // Flowers in many clear hues (one white).
        var hues = new HashSet<int>();
        foreach (var c in ForestPalette.Flowers)
        {
            AssertInRgb(c);
            var (hue, sat, val) = Hsv(c);
            Assert.True(val >= 0.9f, $"flower {c} is dull");
            if (sat >= 0.5f)
                hues.Add((int)(hue / 30f));
        }
        Assert.True(hues.Count >= 7, $"flowers in only {hues.Count} hue families");
        Assert.Single(ForestPalette.Flowers, c => Hsv(c).Sat < 0.5f);
    }
}
