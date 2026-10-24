using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Shell;
using Xunit;

namespace Fungtopia.Forest.Tests;

public class ForestTests
{
    /// <summary>Fake of the shell's bus: counts every message published and every subscription.</summary>
    private sealed class FakeBus : IMessageBus
    {
        public int Published;
        public int Subscriptions;

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct => Subscriptions++;

        public void Publish<T>(in T message) where T : struct => Published++;
    }

    /// <summary>Every 0.5 m over the walkable square plus a 20 m border around it.</summary>
    private static IEnumerable<(float X, float Z)> Grid(float step = 0.5f, float border = 20f)
    {
        float limit = ForestBounds.HalfSize + border;
        for (float x = -limit; x <= limit; x += step)
        for (float z = -limit; z <= limit; z += step)
            yield return (x, z);
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

    // Core

    [Fact]
    public void ModuleStartsQuietly()
    {
        var bus = new FakeBus();
        var forest = new ForestModule();
        forest.Start(bus);
        Assert.True(forest.Started);
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
