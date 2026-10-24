using System;
using System.Collections.Generic;
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
    public void ObstaclesAreTheMapForestTrunks()
    {
        Assert.Equal(Ground.ForestTrunkCount, Obstacles.Count);
        var map = Ground.Obstacles;
        var forest = Obstacles.All;
        for (int i = 0; i < forest.Length; i++)
        {
            Assert.Equal(map[i].X, forest[i].X);
            Assert.Equal(map[i].Z, forest[i].Z);
            Assert.Equal(map[i].Radius, forest[i].Radius);
        }
        foreach (var (x, z) in Grid(step: 1.3f))
            Assert.Equal(Obstacles.Blocks(x, z, 0.4f), Ground.Blocks(x, z, 0.4f));
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
    public void HeightIsDeterministicAndDoesNotAllocate()
    {
        float first = Terrain.HeightAt(12.5f, -33.25f);
        Terrain.HeightAt(0f, 0f); // warm up
        long before = GC.GetAllocatedBytesForCurrentThread();
        float sum = 0f;
        for (int i = 0; i < 10000; i++)
            sum += Terrain.HeightAt(i * 0.37f - 90f, 90f - i * 0.21f);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
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
        Assert.Equal(Obstacles.All.Length, Obstacles.Count);
        Assert.InRange(Obstacles.Count, 80, 400);
        foreach (var o in Obstacles.All)
            Assert.InRange(o.Radius, 0.5f, 3f);
    }

    [Fact]
    public void ObstaclesAreInsideTheBounds()
    {
        foreach (var o in Obstacles.All)
            Assert.True(ForestBounds.Contains(o.X, o.Z, margin: o.Radius), $"trunk at ({o.X}, {o.Z}) leaves the bounds");
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
            for (int j = i + 1; j < all.Length; j++)
                Assert.True(Vector2.Distance(o.Center, all[j].Center) > o.Radius + all[j].Radius + 1f);
        }
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
        var first = Obstacles.All.ToArray();
        var second = Obstacles.All.ToArray();
        Assert.Equal(first, second);
        // Pinned values: the layout depends on no random seed or clock, only on fixed math.
        Assert.Equal(235, first.Length);
        Assert.Equal(-94.2996f, first[0].X, 3);
        Assert.Equal(-95.3378f, first[0].Z, 3);
        Assert.Equal(1.3795f, first[0].Radius, 3);
        Assert.Equal(92.2112f, first[^1].X, 3);
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
        // The west gap (the Deep Forest path) is the Map's too.
        Assert.False(ForestRing.Keeps(Areas.ForestGapWest.X - 3f, Areas.ForestGapWest.Y, 1f));
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
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
            bus.Time.Handle(new TimeOfDay(2, i % 24, TimeOfDay.IsNightAt(i % 24)));
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(0, bus.Published);
    }

    [Fact]
    public void ModuleDoesNothingPerTickAndDoesNotAllocate()
    {
        var bus = new FakeBus();
        var forest = new ForestModule();
        forest.Start(bus);
        forest.Tick(new TickContext(0, 1.0 / 60));
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 1; i < 1000; i++)
            forest.Tick(new TickContext(i, 1.0 / 60));
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(0, bus.Published);
    }

    [Fact]
    public void TickBeforeStartThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new ForestModule().Tick(new TickContext(0, 1.0 / 60)));
        Assert.Throws<ArgumentNullException>(() => new ForestModule().Start(null!));
    }
}
