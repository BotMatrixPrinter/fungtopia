using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Shell;
using Xunit;

namespace Fungtopia.Map.Tests;

public class MapTests
{
    /// <summary>Fake of the shell's bus: counts every message published and every subscription.</summary>
    private sealed class FakeBus : IMessageBus
    {
        public int Published;
        public int Subscriptions;

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct => Subscriptions++;

        public void Publish<T>(in T message) where T : struct => Published++;
    }

    private const float WalkerRadius = 0.4f;

    /// <summary>modules/Forest/Contract's Terrain.HeightAt, as it was before the Map existed.</summary>
    private static float OldForestHeight(float x, float z) =>
        1.1f * MathF.Sin(0.031f * x + 0.4f) * MathF.Cos(0.027f * z - 1.1f)
        + 0.5f * MathF.Sin(0.052f * x - 0.061f * z + 2.3f)
        + 0.3f * MathF.Sin(0.13f * x + 1.7f) * MathF.Sin(0.11f * z + 0.3f);

    private static bool Free(float x, float z) => Areas.Walkable(x, z, WalkerRadius) && !Ground.Blocks(x, z, WalkerRadius);

    private static IEnumerable<Place> TownAndCityPlaces()
    {
        yield return Places.HomeDoor;
        yield return Places.ClinicDoor;
        yield return Places.MillieSpot;
        yield return Places.FurnitureShopSpot;
        yield return Places.AbbySpot;
        yield return Places.FoodSpot;
        yield return Places.LucySpot;
        yield return Places.SignpostTown;
        yield return Places.SignpostForestSouth;
        yield return Places.SignpostForestNorth;
        yield return Places.SignpostPetCity;
        yield return Places.SignpostDeepForest;
    }

    // Forest

    [Fact]
    public void ForestHeightsMatchTheOldForestFormula()
    {
        for (float x = -100f; x <= 100f; x += 0.5f)
        for (float z = -100f; z <= 100f; z += 0.5f)
            Assert.True(MathF.Abs(Ground.HeightAt(x, z) - OldForestHeight(x, z)) < 0.03f, $"height changed at ({x}, {z})");
    }

    [Fact]
    public void GroundJustOutsideTheForestMeetsItsEdge()
    {
        // The Map's ground meets the Forest's floor at the square's edge without a step.
        for (float t = -100f; t <= 100f; t += 1f)
        {
            Assert.True(MathF.Abs(Ground.HeightAt(t, 100.5f) - OldForestHeight(t, 100f)) < 0.15f, $"seam step at ({t}, 100)");
            Assert.True(MathF.Abs(Ground.HeightAt(t, -100.5f) - OldForestHeight(t, -100f)) < 0.15f, $"seam step at ({t}, -100)");
            Assert.True(MathF.Abs(Ground.HeightAt(100.5f, t) - OldForestHeight(100f, t)) < 0.15f, $"seam step at (100, {t})");
            Assert.True(MathF.Abs(Ground.HeightAt(-100.5f, t) - OldForestHeight(-100f, t)) < 0.15f, $"seam step at (-100, {t})");
        }
    }

    [Fact]
    public void ForestTrunksAreTheForestsGiantMushrooms()
    {
        Assert.Equal(235, Ground.ForestTrunkCount); // modules/Forest's Obstacles.Count
        var obstacles = Ground.Obstacles;
        for (int i = 0; i < Ground.ForestTrunkCount; i++)
        {
            var o = obstacles[i];
            Assert.True(Areas.Forest.Contains(o.X, o.Z, o.Radius + 2f));
            Assert.InRange(o.Radius, 0.9f, 2.4f);
            Assert.Equal(Area.Forest, Areas.At(o.X, o.Z));
        }
        // Spot checks: the trunk circles of modules/Forest's generator.
        Assert.True(Ground.Blocks(obstacles[0].X, obstacles[0].Z));
        Assert.False(Ground.Blocks(0f, 84f, 8f)); // the start clearing stays open
        Assert.False(Ground.Blocks(0f, -5f, 10f)); // and the big middle one
    }

    // Road gaps and roads

    [Fact]
    public void RoadGapsOpenTheForestRingSouthAndNorth()
    {
        foreach (float side in new[] { 1f, -1f })
        {
            for (float d = 94f; d <= 104f; d += 0.5f)
                for (float x = -1.5f; x <= 1.5f; x += 0.5f)
                    Assert.True(Free(x, side * d), $"gap closed at ({x}, {side * d})");
            Assert.Equal(Area.Road, Areas.At(0f, side * 104f));
            // Elsewhere the ring stays closed.
            for (float x = -95f; x <= 95f; x += 1f)
                if (MathF.Abs(x) > Areas.ForestGapHalfWidth + 2f)
                    Assert.False(Areas.Walkable(x, side * 103f), $"ring open at ({x}, {side * 103f})");
        }
        for (float z = -95f; z <= 95f; z += 1f)
        {
            Assert.False(Areas.Walkable(103f, z));
            if (MathF.Abs(z) > Areas.DeepForestGapHalfWidth + 2f)
                Assert.False(Areas.Walkable(-103f, z)); // the west gap is the Deep Forest's
        }
        Assert.Equal(new Vector2(0f, 100f), Areas.ForestGapSouth);
        Assert.Equal(new Vector2(0f, -100f), Areas.ForestGapNorth);
        Assert.InRange(Areas.ForestGapHalfWidth * 2f, 8f, 12f);
    }

    [Fact]
    public void RoadsAreAbout6MetersWideGentleAndFlatIsh()
    {
        Assert.InRange(Areas.RoadHalfWidth * 2f, 5f, 7f);
        var points = Areas.RoadPoints;
        for (int i = 0; i + 1 < points.Length; i++)
        {
            Vector2 a = points[i], b = points[i + 1];
            if (float.IsNaN(a.X) || float.IsNaN(b.X))
                continue;
            for (float t = 0f; t <= 1f; t += 0.05f)
            {
                var p = Vector2.Lerp(a, b, t);
                Assert.True(Areas.Walkable(p.X, p.Y, Areas.RoadHalfWidth - 0.5f), $"road too narrow at {p}");
                Assert.False(Ground.Blocks(p.X, p.Y, Areas.RoadHalfWidth - 0.5f), $"road blocked at {p}");
                // Flat-ish across, gentle along.
                Vector2 dir = Vector2.Normalize(b - a), across = new(-dir.Y, dir.X);
                var l = p + across * (Areas.RoadHalfWidth - 0.2f);
                var r = p - across * (Areas.RoadHalfWidth - 0.2f);
                Assert.True(MathF.Abs(Ground.HeightAt(l.X, l.Y) - Ground.HeightAt(r.X, r.Y)) < 0.8f, $"road tilted at {p}");
                var q = p + dir * 0.5f;
                Assert.True(MathF.Abs(Ground.HeightAt(q.X, q.Y) - Ground.HeightAt(p.X, p.Y)) / 0.5f < 0.2f, $"road steep at {p}");
            }
        }
        // Gently curving: consecutive segments turn by less than 35 degrees.
        for (int i = 1; i + 1 < points.Length; i++)
        {
            Vector2 a = points[i - 1], b = points[i], c = points[i + 1];
            if (float.IsNaN(a.X) || float.IsNaN(b.X) || float.IsNaN(c.X))
                continue;
            float cos = Vector2.Dot(Vector2.Normalize(b - a), Vector2.Normalize(c - b));
            Assert.True(cos > MathF.Cos(35f * MathF.PI / 180f), $"sharp bend at {b}");
        }
    }

    /// <summary>Flood fill on a 0.5 m grid of spots free for a walker, from <paramref name="start"/>.</summary>
    private static bool[,] Reachable(Vector3 start, AreaBounds box, out Func<float, float, bool> reached)
    {
        const float step = 0.5f;
        int nx = (int)((box.MaxX - box.MinX) / step) + 1, nz = (int)((box.MaxZ - box.MinZ) / step) + 1;
        var seen = new bool[nx, nz];
        var queue = new Queue<(int, int)>();
        int si = (int)MathF.Round((start.X - box.MinX) / step), sj = (int)MathF.Round((start.Z - box.MinZ) / step);
        Assert.True(Free(box.MinX + si * step, box.MinZ + sj * step), $"start {start} is not free");
        seen[si, sj] = true;
        queue.Enqueue((si, sj));
        while (queue.Count > 0)
        {
            var (i, j) = queue.Dequeue();
            foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int a = i + di, b = j + dj;
                if (a < 0 || b < 0 || a >= nx || b >= nz || seen[a, b])
                    continue;
                if (!Free(box.MinX + a * step, box.MinZ + b * step))
                    continue;
                seen[a, b] = true;
                queue.Enqueue((a, b));
            }
        }
        reached = (x, z) =>
        {
            int i = (int)MathF.Round((x - box.MinX) / step), j = (int)MathF.Round((z - box.MinZ) / step);
            for (int a = i - 1; a <= i + 1; a++)
            for (int b = j - 1; b <= j + 1; b++)
                if (a >= 0 && b >= 0 && a < nx && b < nz && seen[a, b])
                    return true;
            return false;
        };
        return seen;
    }

    [Fact]
    public void RoadsConnectTownForestAndPetCityOnFoot()
    {
        var box = new AreaBounds(-285f, -300f, 110f, 300f);
        var seen = Reachable(Places.HomeDoor.Position, box, out var reached);
        foreach (var place in TownAndCityPlaces())
            Assert.True(reached(place.Position.X, place.Position.Z), $"{place.Position} cannot be reached on foot from home");
        Assert.True(reached(0f, 84f), "the forest's start clearing cannot be reached");
        Assert.True(reached(0f, -5f), "the forest's middle clearing cannot be reached");
        Assert.True(reached(0f, 215f + 6f), "the town square cannot be reached");
        Assert.True(reached(0f, -215f), "Pet City's plaza cannot be reached");
        // Nothing reached lies off the walkable ground.
        for (int i = 0; i < seen.GetLength(0); i += 7)
        for (int j = 0; j < seen.GetLength(1); j += 7)
            if (seen[i, j])
                Assert.True(Areas.Walkable(box.MinX + i * 0.5f, box.MinZ + j * 0.5f));
    }

    [Fact]
    public void HomeInteriorIsAFarAwayRoomEnteredOnlyByItsDoor()
    {
        var home = Areas.Home;
        Assert.InRange(home.MaxX - home.MinX, 11f, 13f);
        Assert.InRange(home.MaxZ - home.MinZ, 9f, 11f);
        Assert.InRange(home.Center.X, 590f, 610f);
        Assert.InRange(home.Center.Y, 590f, 610f);
        Assert.Equal(Areas.HomeFloorY, Ground.HeightAt(home.Center.X, home.Center.Y));
        Assert.InRange(Areas.HomeFloorY, -55f, -45f);
        Reachable(Places.HomeInterior.Position, new AreaBounds(590f, 590f, 610f, 610f), out var reached);
        Assert.True(reached(Places.HomeExit.Position.X, Places.HomeExit.Position.Z));
        Assert.False(Areas.Walkable(600f, 607f));
        Assert.False(Areas.Walkable(592f, 600f));
        Assert.False(Areas.Walkable(600f, 400f));
    }

    // Deep Forest

    [Fact]
    public void DeepForestLiesWestOfTheForestBehindANarrowGap()
    {
        Assert.Equal((-280f, -90f, -110f, 90f), (Areas.DeepForest.MinX, Areas.DeepForest.MinZ, Areas.DeepForest.MaxX, Areas.DeepForest.MaxZ));
        Assert.Equal(Areas.DeepForest, DeepForestLayout.Bounds);
        Assert.Equal(new Vector2(-100f, 0f), Areas.ForestGapWest);
        Assert.InRange(Areas.DeepForestGapHalfWidth * 2f, 7f, 9f); // about 8 m: a narrow path
        Assert.Equal(new Vector2(-110f, 0f), DeepForestLayout.Entrance);
        // Through the gap: free from inside the forest to inside the Deep Forest.
        for (float x = -114f; x <= -94f; x += 0.5f)
        for (float z = -3f; z <= 3f; z += 0.5f)
        {
            Assert.True(Free(x, z), $"west gap closed at ({x}, {z})");
            Assert.Equal(x < -100f ? Area.DeepForest : Area.Forest, Areas.At(x, z));
        }
        // Elsewhere the strip between them is cliff.
        for (float x = -109f; x <= -101f; x += 1f)
        for (float z = -100f; z <= 100f; z += 1f)
            if (MathF.Abs(z) > Areas.DeepForestGapHalfWidth)
                Assert.False(Areas.Walkable(x, z), $"strip open at ({x}, {z})");
        Assert.True(Ground.HeightAt(-105f, 40f) > Ground.HeightAt(-105f, 0f) + 1f, "no ridge beside the gap");
    }

    [Fact]
    public void DeepForestIsReachedOnFootOnlyThroughTheWestGap()
    {
        var box = new AreaBounds(-285f, -95f, 0f, 95f);
        var seen = Reachable(new Vector3(0f, 0f, -5f), box, out var reached);
        var sign = Places.SignpostDeepForest.Position;
        Assert.True(reached(sign.X, sign.Z), "the Deep Forest's signpost cannot be reached from the forest");
        foreach (var s in DeepForestLayout.SpawnPoints)
            Assert.True(reached(s.X, s.Y), $"spawn spot {s} cannot be reached from the forest");
        Assert.True(reached(-270f, -80f) || reached(-268f, -80f) || reached(-270f, -78f), "the Deep Forest's far corner cannot be reached");
        // Every reached spot west of the forest is in the Deep Forest or on its path, and the
        // ground between them is crossed only through the gap.
        for (int i = 0; i < seen.GetLength(0); i++)
        for (int j = 0; j < seen.GetLength(1); j++)
        {
            if (!seen[i, j])
                continue;
            float x = box.MinX + i * 0.5f, z = box.MinZ + j * 0.5f;
            if (x < -100f)
                Assert.Equal(Area.DeepForest, Areas.At(x, z));
            if (x is > -110f and < -100f)
                Assert.True(MathF.Abs(z) <= Areas.DeepForestGapHalfWidth, $"crossed outside the gap at ({x}, {z})");
        }
    }

    [Fact]
    public void DeepForestSpawnSpotsAreWalkableSpreadAndAwayFromTheEntrance()
    {
        var spawns = DeepForestLayout.SpawnPoints;
        Assert.InRange(spawns.Length, 8, 16);
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        for (int i = 0; i < spawns.Length; i++)
        {
            var s = spawns[i];
            Assert.True(Areas.Walkable(s.X, s.Y, 2f), $"spawn spot {s} not walkable");
            Assert.False(Ground.Blocks(s.X, s.Y, 2f), $"spawn spot {s} blocked");
            Assert.Equal(Area.DeepForest, Areas.At(s.X, s.Y));
            Assert.True(Vector2.Distance(s, DeepForestLayout.Entrance) >= DeepForestLayout.SpawnMinEntranceDistance, $"spawn spot {s} near the entrance");
            Assert.True(Vector2.Distance(s, Areas.ForestGapWest) >= 40f);
            for (int k = 0; k < i; k++)
                Assert.True(Vector2.Distance(s, spawns[k]) >= 20f, $"spawn spots {s} and {spawns[k]} bunched up");
            minX = MathF.Min(minX, s.X); maxX = MathF.Max(maxX, s.X);
            minZ = MathF.Min(minZ, s.Y); maxZ = MathF.Max(maxZ, s.Y);
        }
        // Spread around the whole area.
        Assert.True(maxX - minX > 100f && maxZ - minZ > 120f);
        Assert.InRange(DeepForestLayout.SpawnMinEntranceDistance, 30f, 60f);
    }

    [Fact]
    public void DeepForestMushroomsAreDenserAndBiggerThanTheForests()
    {
        var all = Ground.Obstacles;
        int deep = Ground.DeepForestTrunkCount, first = all.Length - deep;
        for (int i = Ground.ForestTrunkCount; i < Ground.PropCircleStart; i++)
            Assert.NotEqual(Area.DeepForest, Areas.At(all[i].X, all[i].Z)); // the buildings' circles come before
        Assert.Equal(first, Ground.PropCircleStart + Ground.PropCircleCount); // then the props', then the Deep Forest's trunks
        float forestArea = 200f * 200f, deepArea = 170f * 180f;
        Assert.True(deep / deepArea > 1.2f * (Ground.ForestTrunkCount / forestArea), $"only {deep} Deep Forest mushrooms");
        float forestRadius = 0f, deepRadius = 0f;
        for (int i = 0; i < Ground.ForestTrunkCount; i++)
            forestRadius += all[i].Radius / Ground.ForestTrunkCount;
        for (int i = first; i < all.Length; i++)
        {
            var o = all[i];
            deepRadius += o.Radius / deep;
            Assert.True(Areas.DeepForest.Contains(o.X, o.Z, o.Radius + 2f), $"Deep Forest mushroom at ({o.X}, {o.Z}) too near the edge");
            Assert.Equal(Area.DeepForest, Areas.At(o.X, o.Z));
            Assert.True(Ground.Blocks(o.X, o.Z));
            Assert.True(Vector2.Distance(o.Center, DeepForestLayout.Entrance) > o.Radius + 6f, "a mushroom in the entrance");
            for (int k = first; k < i; k++)
                Assert.True(Vector2.Distance(o.Center, all[k].Center) > o.Radius + all[k].Radius + 1f, "Deep Forest mushrooms squeeze shut");
        }
        Assert.True(deepRadius > forestRadius * 1.3f, $"Deep Forest mushrooms ({deepRadius} m) not bigger than the forest's ({forestRadius} m)");
        Assert.False(Ground.Blocks(Places.SignpostDeepForest.Position.X, Places.SignpostDeepForest.Position.Z, 2f));
    }

    [Fact]
    public void DeepForestGroundIsRougherButStillWalkable()
    {
        // Bumpiness: mean absolute second difference of height over a 1 m grid.
        static float Bumpiness(AreaBounds box)
        {
            float sum = 0f;
            int n = 0;
            for (float x = box.MinX + 5f; x <= box.MaxX - 5f; x += 1f)
            for (float z = box.MinZ + 5f; z <= box.MaxZ - 5f; z += 1f, n++)
                sum += MathF.Abs(Ground.HeightAt(x + 1f, z) + Ground.HeightAt(x - 1f, z) - 2f * Ground.HeightAt(x, z))
                     + MathF.Abs(Ground.HeightAt(x, z + 1f) + Ground.HeightAt(x, z - 1f) - 2f * Ground.HeightAt(x, z));
            return sum / n;
        }
        Assert.True(Bumpiness(Areas.DeepForest) > 2f * Bumpiness(Areas.Forest), "the Deep Forest's ground is no rougher than the forest's");
        float lowest = float.MaxValue, highest = float.MinValue;
        var b = Areas.DeepForest;
        for (float x = b.MinX + 2f; x <= b.MaxX - 2f; x += 0.5f)
        for (float z = b.MinZ + 2f; z <= b.MaxZ - 2f; z += 0.5f)
        {
            float h = Ground.HeightAt(x, z);
            lowest = MathF.Min(lowest, h);
            highest = MathF.Max(highest, h);
            float sx = MathF.Abs(Ground.HeightAt(x + 0.5f, z) - h) / 0.5f, sz = MathF.Abs(Ground.HeightAt(x, z + 0.5f) - h) / 0.5f;
            Assert.True(sx < 0.3f && sz < 0.3f, $"Deep Forest ground too steep at ({x}, {z})");
        }
        Assert.InRange(highest - lowest, 1f, 4f);
    }

    [Fact]
    public void DeepForestLayoutIsDeterministic()
    {
        // Fixed data: the same spots and mushrooms every run (no seed, no clock).
        Assert.Equal(new Vector2(-150f, -65f), DeepForestLayout.SpawnPoints[0]);
        Assert.Equal(DeepForestLayout.SpawnPoints.ToArray(), DeepForestLayout.SpawnPoints.ToArray());
        var all = Ground.Obstacles;
        var first = all[all.Length - Ground.DeepForestTrunkCount];
        Assert.Equal(DeepForestTrunkCountPinned, Ground.DeepForestTrunkCount);
        Assert.True(Ground.Blocks(first.X, first.Z));
        float h = Ground.HeightAt(-200.25f, 33.5f);
        Assert.Equal(h, Ground.HeightAt(-200.25f, 33.5f));
        Assert.Equal(Places.SignpostDeepForest.Position, new Place(-112f, 5.5f, MathF.PI / 2f).Position);
    }

    private const int DeepForestTrunkCountPinned = 311;

    // Outside

    [Fact]
    public void OutsideTheAreasIsNotWalkable()
    {
        for (float x = -320f; x <= 200f; x += 2f)
        for (float z = -350f; z <= 350f; z += 2f)
        {
            bool inside = Areas.Forest.Contains(x, z) || Areas.Town.Contains(x, z) || Areas.PetCity.Contains(x, z)
                          || Areas.DeepForest.Contains(x, z) || Areas.DeepForestPath.Contains(x, z)
                          || Areas.RoadDistance(x, z) <= Areas.RoadHalfWidth;
            Assert.Equal(inside, Areas.Walkable(x, z));
            if (!Areas.Walkable(x, z, -(Ground.HedgeBand + Ground.CliffRise)) && MathF.Max(MathF.Abs(x), MathF.Abs(z)) > 104f)
                Assert.True(Ground.HeightAt(x, z) > Ground.HeightAt(0f, 215f) + 4f,
                    $"no cliff at ({x}, {z})");
        }
        Assert.False(Areas.Walkable(0f, 125f, 0f) && Areas.Walkable(20f, 125f));
        Assert.False(Areas.Walkable(90f, 220f));
        Assert.False(Areas.Walkable(0f, 300f));
        Assert.False(Areas.Walkable(0f, -300f));
        Assert.False(Areas.Walkable(140f, 0f));
        Assert.False(Areas.Walkable(-290f, 0f));
        Assert.False(Areas.Walkable(-200f, 100f));
    }

    [Fact]
    public void AreasAtNamesEachPlace()
    {
        Assert.Equal(Area.Forest, Areas.At(0f, 0f));
        Assert.Equal(Area.Forest, Areas.At(99f, -99f));
        Assert.Equal(Area.Town, Areas.At(0f, 220f));
        Assert.Equal(Area.Town, Areas.At(-79f, 289f));
        Assert.Equal(Area.PetCity, Areas.At(0f, -220f));
        Assert.Equal(Area.Road, Areas.At(Areas.RoadPoints[3].X, Areas.RoadPoints[3].Y));
        Assert.Equal(Area.Home, Areas.At(600f, 600f));
        Assert.Equal(Area.DeepForest, Areas.At(-200f, 0f));
        Assert.Equal(Area.DeepForest, Areas.At(-279f, -89f));
        Assert.Equal(Area.DeepForest, Areas.At(-105f, 0f)); // the overgrown path
        Assert.Equal(Area.Forest, Areas.At(-99f, 0f));
        // Off the walkable ground: the nearest area.
        Assert.Equal(Area.Town, Areas.At(0f, 300f));
        Assert.Equal(Area.PetCity, Areas.At(-90f, -200f));
        Assert.Equal(Area.Forest, Areas.At(110f, 0f));
        Assert.Equal(Area.DeepForest, Areas.At(-290f, 0f));
        Assert.Equal(Area.DeepForest, Areas.At(-200f, 110f));
        Assert.Equal(Areas.DeepForest, Areas.Bounds(Area.DeepForest));
        Assert.Equal(Area.Town, Areas.Bounds(Area.Town).Contains(0f, 200f) ? Area.Town : Area.Road);
        Assert.Throws<ArgumentOutOfRangeException>(() => Areas.Bounds(Area.Road));
    }

    [Fact]
    public void TownAndPetCityLieWhereDesigned()
    {
        Assert.Equal(new AreaBounds(-80f, 150f, 80f, 290f).Center, Areas.Town.Center);
        Assert.Equal((-80f, 150f, 80f, 290f), (Areas.Town.MinX, Areas.Town.MinZ, Areas.Town.MaxX, Areas.Town.MaxZ));
        Assert.Equal((-80f, -290f, 80f, -150f), (Areas.PetCity.MinX, Areas.PetCity.MinZ, Areas.PetCity.MaxX, Areas.PetCity.MaxZ));
        Assert.Equal((-100f, -100f, 100f, 100f), (Areas.Forest.MinX, Areas.Forest.MinZ, Areas.Forest.MaxX, Areas.Forest.MaxZ));
    }

    // Ground

    [Fact]
    public void GroundIsContinuousAndWalkableGroundIsGentle()
    {
        const float step = 0.5f;
        for (float x = -310f; x <= 150f; x += step)
        for (float z = -320f; z <= 320f; z += step)
        {
            float h = Ground.HeightAt(x, z);
            Assert.True(float.IsFinite(h));
            float dx = MathF.Abs(Ground.HeightAt(x + step, z) - h), dz = MathF.Abs(Ground.HeightAt(x, z + step) - h);
            Assert.True(dx < 0.6f && dz < 0.6f, $"step in the ground at ({x}, {z})");
            if (Areas.Walkable(x, z) && Areas.Walkable(x + step, z + step))
                Assert.True(dx / step < 0.3f && dz / step < 0.3f, $"walkable ground too steep at ({x}, {z})");
        }
        // Town and city are flat-ish.
        for (float x = -80f; x <= 80f; x += 2f)
        for (float z = 150f; z <= 290f; z += 2f)
        {
            Assert.InRange(Ground.HeightAt(x, z), -0.6f, 0.6f);
            Assert.InRange(Ground.HeightAt(x, -z), -0.6f, 0.6f);
        }
    }

    // Places

    [Fact]
    public void PlacesAreWalkableOnTheGroundAndInTheirAreas()
    {
        void Check(Place p, Area area)
        {
            Assert.Equal(Ground.HeightAt(p.Position.X, p.Position.Z), p.Position.Y);
            Assert.True(Areas.Walkable(p.Position.X, p.Position.Z, 0.5f), $"{p.Position} not walkable");
            Assert.False(Ground.Blocks(p.Position.X, p.Position.Z, 0.5f), $"{p.Position} blocked");
            Assert.Equal(area, Areas.At(p.Position.X, p.Position.Z));
            Assert.InRange(p.Facing.Length(), 0.999f, 1.001f);
            Assert.Equal(0f, p.Facing.Y);
        }
        Check(Places.HomeDoor, Area.Town);
        Check(Places.ClinicDoor, Area.Town);
        Check(Places.MillieSpot, Area.Town);
        Check(Places.FurnitureShopSpot, Area.Town);
        Check(Places.AbbySpot, Area.Town);
        Check(Places.FoodSpot, Area.Town);
        Check(Places.SignpostTown, Area.Town);
        Check(Places.SignpostForestSouth, Area.Forest);
        Check(Places.SignpostForestNorth, Area.Forest);
        Check(Places.SignpostPetCity, Area.PetCity);
        Check(Places.LucySpot, Area.PetCity);
        Check(Places.SignpostDeepForest, Area.DeepForest);
        Check(Places.HomeInterior, Area.Home);
        Check(Places.HomeExit, Area.Home);
        Assert.Equal(Areas.HomeFloorY, Places.HomeInterior.Position.Y);
    }

    [Fact]
    public void SignpostsStandAtEachEntrance()
    {
        Assert.Equal(Places.SignpostTown, Places.Signpost(Area.Town));
        Assert.Equal(Places.SignpostForestSouth, Places.Signpost(Area.Forest));
        Assert.Equal(Places.SignpostPetCity, Places.Signpost(Area.PetCity));
        Assert.Equal(Places.SignpostDeepForest, Places.Signpost(Area.DeepForest));
        Assert.Throws<ArgumentOutOfRangeException>(() => Places.Signpost(Area.Road));
        Assert.Throws<ArgumentOutOfRangeException>(() => Places.Signpost(Area.Home));
        // Within 10 m of its entrance (and of the road there).
        Assert.True(Vector2.Distance(new(Places.SignpostTown.Position.X, Places.SignpostTown.Position.Z), new(0f, 150f)) < 10f);
        Assert.True(Vector2.Distance(new(Places.SignpostPetCity.Position.X, Places.SignpostPetCity.Position.Z), new(0f, -150f)) < 10f);
        Assert.True(Vector2.Distance(new(Places.SignpostForestSouth.Position.X, Places.SignpostForestSouth.Position.Z), Areas.ForestGapSouth) < 10f);
        Assert.True(Vector2.Distance(new(Places.SignpostForestNorth.Position.X, Places.SignpostForestNorth.Position.Z), Areas.ForestGapNorth) < 10f);
        // The Deep Forest's stands just inside the west gap, beside the overgrown path, facing west into it.
        var deep = Places.SignpostDeepForest;
        Assert.True(Vector2.Distance(new(deep.Position.X, deep.Position.Z), DeepForestLayout.Entrance) < 7f);
        Assert.True(Areas.DeepForest.Contains(deep.Position.X, deep.Position.Z));
        Assert.InRange(MathF.Abs(deep.Position.Z), Areas.DeepForestGapHalfWidth, Areas.DeepForestGapHalfWidth + 3f);
        Assert.True(deep.Facing.X < -0.99f);
        foreach (var p in new[] { Places.SignpostTown, Places.SignpostPetCity, Places.SignpostForestSouth, Places.SignpostForestNorth })
            Assert.InRange(Areas.RoadDistance(p.Position.X, p.Position.Z), Areas.RoadHalfWidth - 2f, Areas.RoadHalfWidth + 3f);
    }

    [Fact]
    public void DoorsAndSpotsAreInFrontOfTheirBuildings()
    {
        void Check(Place p, BuildingKind kind, float maxDistance)
        {
            var b = Buildings.First(kind);
            var spot = new Vector2(p.Position.X, p.Position.Z);
            Assert.False(b.Contains(spot.X, spot.Y), $"{kind} spot inside it");
            Assert.True(b.Contains(spot.X, spot.Y, maxDistance), $"{kind} spot too far from it");
            Assert.True(Vector2.Dot(spot - b.Center, b.Front) > 0f, $"{kind} spot behind it");
        }
        Check(Places.HomeDoor, BuildingKind.PlayerHouse, 2.5f);
        Check(Places.ClinicDoor, BuildingKind.Clinic, 2.5f);
        Check(Places.MillieSpot, BuildingKind.MillieHouse, 2.5f);
        Check(Places.FurnitureShopSpot, BuildingKind.FurnitureShop, 2.5f);
        Check(Places.AbbySpot, BuildingKind.ClothingShop, 2.5f);
        Check(Places.FoodSpot, BuildingKind.FoodStall, 2.5f);
        Check(Places.LucySpot, BuildingKind.PetStore, 2.5f);
        // Millie lives next door.
        var home = Buildings.First(BuildingKind.PlayerHouse);
        var millie = Buildings.First(BuildingKind.MillieHouse);
        Assert.True(Vector2.Distance(home.Center, millie.Center) < 30f);
        // Your house is near the town center.
        Assert.True(Vector2.Distance(home.Center, Areas.Town.Center) < 35f);
    }

    // Buildings

    [Fact]
    public void BuildingsAreWhereTheDesignPutsThem()
    {
        int houses = 0, blocks = 0;
        foreach (var kind in new[] { BuildingKind.PlayerHouse, BuildingKind.MillieHouse, BuildingKind.Clinic, BuildingKind.FurnitureShop, BuildingKind.Fountain, BuildingKind.PetStore, BuildingKind.ClothingShop, BuildingKind.FoodStall })
        {
            int n = 0;
            foreach (var b in Buildings.All)
                if (b.Kind == kind)
                    n++;
            Assert.Equal(1, n);
        }
        foreach (var b in Buildings.All)
        {
            houses += b.Kind == BuildingKind.House ? 1 : 0;
            blocks += b.Kind == BuildingKind.CityBlock ? 1 : 0;
            Assert.Equal(b.Kind is BuildingKind.PetStore or BuildingKind.CityBlock ? Area.PetCity : Area.Town, b.Area);
            Assert.Equal(b.Area, Areas.At(b.X, b.Z));
            float reach = 0.5f * MathF.Sqrt(b.Width * b.Width + b.Depth * b.Depth);
            Assert.True(Areas.Bounds(b.Area).Contains(b.X, b.Z, reach + 2f), $"{b.Kind} at ({b.X}, {b.Z}) too near the edge");
            Assert.True(Areas.RoadDistance(b.X, b.Z) > reach + Areas.RoadHalfWidth, $"{b.Kind} on a road");
            Assert.True(b.Width > 0f && b.Depth > 0f && b.Height > 0f);
            if (b.Kind == BuildingKind.CityBlock)
                Assert.True(b.Height >= 15f, "Pet City's blocks are tall");
            foreach (var other in Buildings.All)
                if (!other.Equals(b))
                    Assert.True(Vector2.Distance(b.Center, other.Center) > reach + 0.5f * MathF.Sqrt(other.Width * other.Width + other.Depth * other.Depth) + 1f,
                        $"{b.Kind} at ({b.X}, {b.Z}) overlaps {other.Kind} at ({other.X}, {other.Z})");
        }
        Assert.Equal(4, houses);
        Assert.True(blocks >= 6);
        var fountain = Buildings.First(BuildingKind.Fountain);
        Assert.Equal(Buildings.TownSquare, fountain.Center);
    }

    [Fact]
    public void AbbysClothingShopIsABoutiqueNearTheTownSquare()
    {
        // Added after every older kind and building, so their values and indices stay.
        Assert.Equal(8, (int)BuildingKind.ClothingShop);
        Assert.Equal(BuildingKind.ClothingShop, Buildings.All[20].Kind);

        var shop = Buildings.First(BuildingKind.ClothingShop);
        Assert.Equal(Area.Town, shop.Area);
        Assert.True(Vector2.Distance(shop.Center, Buildings.TownSquare) < 40f, "the clothing shop is near the square");
        Assert.True(shop.Height >= 6f, "a mushroom house, not a stall");
        // Its front faces the square, and the street in from the entrance does not run through it.
        Assert.True(Vector2.Dot(Buildings.TownSquare - shop.Center, shop.Front) > 0f);
        for (float z = Areas.Town.MinZ; z <= Buildings.TownSquare.Y; z += 0.5f)
            Assert.False(shop.Contains(0f, z, 2f), "the shop blocks the town street");

        // Abby stands at its door, facing out towards the square, and you can walk up to her.
        var abby = new Vector2(Places.AbbySpot.Position.X, Places.AbbySpot.Position.Z);
        Assert.True(Vector2.Dot(new Vector2(Places.AbbySpot.Facing.X, Places.AbbySpot.Facing.Z), shop.Front) > 0.99f);
        Assert.True(Free(abby.X, abby.Y));
        Assert.True(Ground.Blocks(shop.X, shop.Z));

        // Its obstacle circles come right after the other buildings' and before the props' and the Deep Forest's trunks.
        var all = Ground.Obstacles;
        bool found = false;
        for (int i = Ground.ForestTrunkCount; i < Ground.PropCircleStart; i++)
            found |= shop.Contains(all[i].X, all[i].Z);
        Assert.True(found, "no obstacle circle on the clothing shop");
    }

    [Fact]
    public void TheFoodStallIsASmallOpenStallOnTheTownSquare()
    {
        // Added at the end of the enum and of Buildings.All, so older kinds and indices keep their values.
        var kinds = Enum.GetValues<BuildingKind>();
        Assert.Equal(BuildingKind.FoodStall, kinds[^1]);
        Assert.Equal(9, (int)BuildingKind.FoodStall);
        Assert.Equal(22, Buildings.Count);
        Assert.Equal(BuildingKind.FoodStall, Buildings.All[Buildings.Count - 1].Kind);

        var stall = Buildings.First(BuildingKind.FoodStall);
        var square = Buildings.TownSquare;
        Assert.Equal(Area.Town, stall.Area);
        // Small and open: lower and smaller than the furniture stall, nothing like a house.
        var furniture = Buildings.First(BuildingKind.FurnitureShop);
        Assert.True(stall.Height <= furniture.Height && stall.Width * stall.Depth < furniture.Width * furniture.Depth, "the food stall is a small stall");
        // On the square's stone paving (12 m around the fountain), its counter facing the fountain.
        for (float u = -0.5f; u <= 0.5f; u += 0.25f)
        for (float v = -0.5f; v <= 0.5f; v += 0.25f)
        {
            var p = stall.ToWorld(u * stall.Width, v * stall.Depth);
            Assert.True(Vector2.Distance(p, square) < 12.5f, $"stall corner {p} off the square");
        }
        Assert.True(Vector2.Dot(Vector2.Normalize(square - stall.Center), stall.Front) > 0.99f, "the counter does not face the fountain");

        // Not in the way: half a meter around it is clear of the street, every other building's path,
        // the roads, the fountain and the other buildings; it keeps 3 m from every door or named spot.
        var fountain = Buildings.First(BuildingKind.Fountain);
        for (float u = -0.5f; u <= 0.5f; u += 0.05f)
        for (float v = -0.5f; v <= 0.5f; v += 0.05f)
        {
            var p = stall.ToWorld(u * (stall.Width + 1f), v * (stall.Depth + 1f));
            float paths = Segment(p, new Vector2(0f, Areas.Town.MinZ), square);
            foreach (var b in Buildings.All)
            {
                if (b.Equals(stall))
                    continue;
                Assert.False(b.Contains(p.X, p.Y), $"the stall's surroundings {p} run into the {b.Kind}");
                if (b.Area == Area.Town && b.Kind != BuildingKind.Fountain)
                    paths = MathF.Min(paths, Segment(p, square, b.Center + b.Front * (b.Depth * 0.5f + 1f)));
            }
            Assert.True(paths > Areas.TownPathHalfWidth, $"the stall blocks a town path at {p}");
            Assert.True(Areas.RoadDistance(p.X, p.Y) > Areas.RoadHalfWidth + 10f);
            Assert.True(Vector2.Distance(p, fountain.Center) > fountain.Width * 0.5f + 3f, "the stall crowds the fountain");
        }
        foreach (var place in TownAndCityPlaces())
            if (!place.Equals(Places.FoodSpot))
                Assert.False(stall.Contains(place.Position.X, place.Position.Z, 3f), $"the stall stands in front of a door or spot at {place.Position}");

        // Solid: its obstacle circles are the last building circles, before the props' and the Deep Forest's trunks.
        Assert.True(Ground.Blocks(stall.X, stall.Z));
        var all = Ground.Obstacles;
        var last = all[Ground.PropCircleStart - 1];
        Assert.True(stall.Contains(last.X, last.Z), "the stall's circles are not the last building circles");

        // The food spot is in front of its counter on the square, facing out, walkable and free.
        var spot = new Vector2(Places.FoodSpot.Position.X, Places.FoodSpot.Position.Z);
        Assert.Equal(Ground.HeightAt(spot.X, spot.Y), Places.FoodSpot.Position.Y);
        Assert.True(Vector2.Distance(spot, stall.Center + stall.Front * (stall.Depth * 0.5f + Places.FrontGap)) < 1e-3f);
        Assert.True(Vector2.Dot(new Vector2(Places.FoodSpot.Facing.X, Places.FoodSpot.Facing.Z), stall.Front) > 0.99f);
        Assert.True(Vector2.Distance(spot, square) < 12f, "the food spot is not on the square");
        Assert.True(Areas.Walkable(spot.X, spot.Y, 0.5f));
        Assert.True(Free(spot.X, spot.Y));
        Assert.False(Ground.Blocks(spot.X, spot.Y, 0.5f));
    }

    private static float Segment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0f, 1f);
        return Vector2.Distance(p, a + ab * t);
    }

    [Fact]
    public void ObstaclesCoverEveryBuildingAndTheFountain()
    {
        int circles = Ground.PropCircleStart - Ground.ForestTrunkCount;
        Assert.True(circles >= Buildings.Count);
        foreach (var b in Buildings.All)
        {
            for (float u = -0.5f; u <= 0.5f; u += 0.05f)
            for (float v = -0.5f; v <= 0.5f; v += 0.05f)
            {
                if (b.Kind == BuildingKind.Fountain && u * u + v * v > 0.25f)
                    continue; // round
                var p = b.ToWorld(u * b.Width, v * b.Depth);
                Assert.True(Ground.Blocks(p.X, p.Y), $"{b.Kind} at ({b.X}, {b.Z}) not covered at {p}");
            }
            // ...and not much more than the footprint: 2 m past it is free.
            var front = b.Center + b.Front * (b.Depth * 0.5f + 2f);
            Assert.False(Ground.Blocks(front.X, front.Y), $"{b.Kind}'s obstacles reach far past its front");
        }
        foreach (var o in Ground.Obstacles)
            Assert.True(o.Radius > 0f && float.IsFinite(o.X) && float.IsFinite(o.Z));
    }

    [Fact]
    public void BuildingFrameTurnsWithRotation()
    {
        var b = new Building(BuildingKind.House, Area.Town, 10f, 20f, -MathF.PI / 2f, 4f, 2f, 3f);
        Assert.True(Vector2.Distance(new Vector2(1f, 0f), b.Front) < 1e-5f); // faces east (+X)
        var p = b.ToWorld(0f, -1f); // the front middle
        Assert.True(Vector2.Distance(new Vector2(11f, 20f), p) < 1e-5f);
        Assert.True(b.Contains(10.9f, 21.9f));
        Assert.False(b.Contains(11.5f, 20f));
        Assert.True(Vector3.Distance(new Vector3(0f, 0f, -1f), new Place(Vector3.Zero, 0f).Facing) < 1e-6f);
    }

    // Props

    private static IEnumerable<(Prop Prop, Circle Circle)> PropCircles()
    {
        foreach (var p in Props.All.ToArray())
            for (int k = 0; k < p.CircleCount; k++)
                yield return (p, p.Circle(k));
    }

    private static int CountOf(Area area, PropKind kind)
    {
        int n = 0;
        foreach (var p in Props.All)
            n += p.Area == area && p.Kind == kind ? 1 : 0;
        return n;
    }

    [Fact]
    public void PropKindsAreAddedAtTheEnd()
    {
        Assert.Equal(
            new[] { PropKind.MediumMushroom, PropKind.Lamp, PropKind.Bench, PropKind.FenceSegment, PropKind.Planter, PropKind.Crate, PropKind.Statue },
            Enum.GetValues<PropKind>());
        Assert.Equal(0, (int)PropKind.MediumMushroom);
        Assert.Equal(6, (int)PropKind.Statue);
    }

    [Fact]
    public void PropsAreInTheObstaclesInOrderAndBlockInsideThem()
    {
        // Every prop's circles, in order, between the buildings' and the Deep Forest's trunks.
        var all = Ground.Obstacles;
        int i = Ground.PropCircleStart;
        foreach (var (_, c) in PropCircles())
        {
            Assert.Equal(c.X, all[i].X);
            Assert.Equal(c.Z, all[i].Z);
            Assert.Equal(c.Radius, all[i].Radius);
            i++;
        }
        Assert.Equal(Ground.PropCircleStart + Ground.PropCircleCount, i);
        Assert.Equal(all.Length - Ground.DeepForestTrunkCount, i);
        // Solid: Blocks is true anywhere on a prop, and a walker cannot stand on it.
        foreach (var p in Props.All)
        {
            string what = $"{p.Kind} at ({p.X}, {p.Z})";
            Assert.True(Ground.Blocks(p.X, p.Z), what);
            Assert.True(Ground.Blocks(p.X, p.Z, WalkerRadius), what);
            if (p.Kind == PropKind.FenceSegment)
            {
                // Along its whole length, and no walker slips between its circles.
                for (float t = -p.Radius + 0.05f; t <= p.Radius - 0.05f; t += 0.05f)
                {
                    var q = p.Center + p.Along * t;
                    Assert.True(Ground.Blocks(q.X, q.Y, 0.05f), $"{what} open at {q}");
                    Assert.True(Ground.Blocks(q.X, q.Y, 0.3f), $"{what} lets a walker through at {q}");
                }
                continue;
            }
            for (int a = 0; a < 8; a++)
            {
                float angle = a * MathF.Tau / 8f, d = 0.95f * p.Radius;
                Assert.True(Ground.Blocks(p.X + MathF.Cos(angle) * d, p.Z + MathF.Sin(angle) * d), $"{what} open near its edge");
            }
            // ...and not much more: just past it is free of it.
            Assert.False(Ground.Blocks(p.X + p.Radius + 0.05f, p.Z) && Ground.Blocks(p.X - p.Radius - 0.05f, p.Z)
                         && Ground.Blocks(p.X, p.Z + p.Radius + 0.05f) && Ground.Blocks(p.X, p.Z - p.Radius - 0.05f), $"{what} blocks all around it");
        }
    }

    [Fact]
    public void PropsStandInsideTheirAreasOnWalkableGround()
    {
        foreach (var (p, c) in PropCircles())
        {
            string what = $"{p.Kind} at ({c.X}, {c.Z})";
            Assert.Equal(p.Area, Areas.At(c.X, c.Z));
            Assert.Equal(p.Area, Areas.At(p.X, p.Z));
            if (p.Area == Area.Road)
            {
                // Lamps beside the roads, off the forest square and the towns.
                Assert.Equal(PropKind.Lamp, p.Kind);
                Assert.False(Areas.Forest.Contains(c.X, c.Z, -c.Radius), what);
                Assert.True(Areas.RoadDistance(c.X, c.Z) < Areas.RoadHalfWidth + c.Radius + Props.Clearance + 1f, $"{what} far from the road");
                continue;
            }
            Assert.True(Areas.Bounds(p.Area).Contains(c.X, c.Z, c.Radius + 2f), $"{what} too near its area's edge");
            Assert.True(Areas.Walkable(c.X, c.Z, c.Radius + Props.Clearance), $"{what} off the walkable ground");
            Assert.Equal(p.Kind == PropKind.MediumMushroom, p.Area is Area.Forest or Area.DeepForest);
        }
    }

    [Fact]
    public void PropsKeepPathsRoadsDoorsPlacesAndSpawnSpotsClear()
    {
        const float keep = Props.Clearance;
        var spots = new List<Vector2>();
        foreach (var place in TownAndCityPlaces())
            spots.Add(new Vector2(place.Position.X, place.Position.Z));
        foreach (var (p, c) in PropCircles())
        {
            string what = $"{p.Kind} at ({c.X}, {c.Z})";
            var at = new Vector2(c.X, c.Z);
            float r = c.Radius;
            Assert.True(Areas.RoadDistance(c.X, c.Z) >= Areas.RoadHalfWidth + r + keep, $"{what} on a road");
            foreach (var s in spots)
                Assert.True(Vector2.Distance(at, s) >= r + keep + 0.5f, $"{what} by the place at {s}");
            foreach (var s in DeepForestLayout.SpawnPoints)
                Assert.True(Vector2.Distance(at, s) >= DeepForestLayout.ClearingRadius + r, $"{what} in the spawn clearing at {s}");
            foreach (var b in Buildings.All)
                Assert.False(b.Contains(c.X, c.Z, r + keep), $"{what} against {b.Kind} at ({b.X}, {b.Z})");
            Assert.True(Vector2.Distance(at, new Vector2(Places.HomeDoor.Position.X, Places.HomeDoor.Position.Z)) >= r + keep + 0.5f);
            switch (p.Area)
            {
                case Area.Forest:
                    Assert.True(ForestFloor.PolylineDistance(ForestFloor.PathPoints, c.X, c.Z) >= ForestFloor.PathHalfWidth + r + keep, $"{what} on a forest path");
                    Assert.True(ForestFloor.PolylineDistance(ForestFloor.TrailPoints, c.X, c.Z) >= ForestFloor.TrailHalfWidth + r + keep, $"{what} on the trail to the west gap");
                    Assert.True(ForestFloor.ClearingDepth(c.X, c.Z) <= -(r + keep), $"{what} in a clearing");
                    // The road gaps and 12 m in from them, the west gap and the Deep Forest path.
                    Assert.False(MathF.Abs(c.X) < Areas.ForestGapHalfWidth + r + keep && MathF.Abs(c.Z) > Areas.ForestHalfSize - 12f, $"{what} in a road gap");
                    Assert.True(Vector2.Distance(at, Areas.ForestGapWest) >= 12f + r, $"{what} in the west gap");
                    break;
                case Area.DeepForest:
                    Assert.True(Areas.DeepForestPath.Depth(c.X, c.Z) <= -(r + keep), $"{what} on the overgrown path");
                    Assert.True(Vector2.Distance(at, DeepForestLayout.Entrance) >= DeepForestLayout.ClearingRadius + 4f + r, $"{what} in the entrance");
                    break;
                case Area.Town:
                    Assert.True(Areas.TownPathDistance(c.X, c.Z) >= Areas.TownPathHalfWidth + r + keep, $"{what} on a town path");
                    break;
                case Area.PetCity:
                    Assert.True(Props.CityAvenueDistance(c.X, c.Z) >= Props.CityAvenueHalfWidth + r + keep, $"{what} on the avenue");
                    Assert.True(Vector2.Distance(at, Buildings.CityPlaza) >= 10f + r, $"{what} in the plaza's middle");
                    break;
            }
        }
        // Between a prop and any other obstacle there is room to walk (a fence's own circles meet).
        var all = Ground.Obstacles;
        for (int i = Ground.PropCircleStart; i < Ground.PropCircleStart + Ground.PropCircleCount; i++)
        for (int k = 0; k < all.Length; k++)
        {
            if (k == i)
                continue;
            var a = all[i];
            var b = all[k];
            if (a.Radius == Props.FencePostRadius && b.Radius == Props.FencePostRadius)
                continue;
            Assert.True(Vector2.Distance(a.Center, b.Center) >= a.Radius + b.Radius + keep - 0.001f,
                $"prop circle at ({a.X}, {a.Z}) squeezes against the obstacle at ({b.X}, {b.Z})");
        }
        // The town square's middle, Pet City's avenue and the forest's start clearing stay open.
        Assert.False(Ground.Blocks(0f, 84f, 8f));
        for (float z = Areas.PetCity.MaxZ; z >= Places.LucySpot.Position.Z + 3f; z -= 0.5f) // up to the pet store
            Assert.False(Ground.Blocks(0f, z, Props.CityAvenueHalfWidth), $"Pet City's avenue blocked at z {z}");
        for (float z = Areas.Town.MinZ; z <= Buildings.TownSquare.Y - 8f; z += 0.5f)
            Assert.False(Ground.Blocks(0f, z, Areas.TownPathHalfWidth), $"the town street blocked at z {z}");
    }

    [Fact]
    public void PropsAreWhatEachAreaHas()
    {
        foreach (var p in Props.All)
        {
            // Solid things: taller than about 0.4 m or wider than about 0.6 m.
            Assert.True(p.Height > 0.4f || p.Radius * 2f > 0.6f, $"{p.Kind} at ({p.X}, {p.Z}) is small decoration");
            Assert.True(p.Radius > 0f && p.Radius <= 2f && p.Height <= 4f && float.IsFinite(p.Rotation));
            if (p.Kind == PropKind.MediumMushroom)
                Assert.InRange(p.Height, 0.8f, 2.5f);
        }
        // Mushroom Forest: medium mushroom clusters at about the density the Forest view scattered (~250).
        int forest = CountOf(Area.Forest, PropKind.MediumMushroom), deep = CountOf(Area.DeepForest, PropKind.MediumMushroom);
        Assert.InRange(forest, 150, 320);
        // The Deep Forest: denser and a bit bigger.
        Assert.True(deep / (170f * 180f) > 1.5f * forest / (200f * 200f), $"only {deep} Deep Forest mushrooms");
        float forestHeight = 0f, deepHeight = 0f;
        foreach (var p in Props.All)
        {
            if (p.Area == Area.Forest) forestHeight += p.Height / forest;
            if (p.Area == Area.DeepForest) deepHeight += p.Height / deep;
        }
        Assert.True(deepHeight > forestHeight + 0.1f, $"Deep Forest mushrooms ({deepHeight} m) not bigger than the forest's ({forestHeight} m)");
        // Mushroom Town: lamps along the street and around the square, benches, garden fences, planters.
        Assert.True(CountOf(Area.Town, PropKind.Lamp) >= 10);
        Assert.True(CountOf(Area.Town, PropKind.Bench) >= 4);
        Assert.True(CountOf(Area.Town, PropKind.FenceSegment) >= 4 * 7, "fewer than four fenced gardens");
        Assert.True(CountOf(Area.Town, PropKind.Planter) >= 4);
        int streetLamps = 0;
        foreach (var p in Props.All)
            streetLamps += p.Area == Area.Town && p.Kind == PropKind.Lamp && Areas.TownPathDistance(p.X, p.Z) < 5f && MathF.Abs(p.X) < 5f ? 1 : 0;
        Assert.True(streetLamps >= 4, "no lamps along the town street");
        // Pet City: lamps, benches, planters and a statue.
        Assert.True(CountOf(Area.PetCity, PropKind.Lamp) >= 8);
        Assert.True(CountOf(Area.PetCity, PropKind.Bench) >= 4);
        Assert.True(CountOf(Area.PetCity, PropKind.Planter) >= 4);
        Assert.Equal(1, CountOf(Area.PetCity, PropKind.Statue));
        // And lamps beside the roads.
        Assert.True(CountOf(Area.Road, PropKind.Lamp) >= 4);
    }

    [Fact]
    public void FenceSegmentsAreRowsOfSmallCircles()
    {
        var fence = new Prop(PropKind.FenceSegment, Area.Town, 10f, 20f, MathF.PI / 2f, 1.75f, 0.8f);
        Assert.True(Vector2.Distance(new Vector2(0f, -1f), fence.Along) < 1e-5f); // turned a quarter: runs north-south
        Assert.True(fence.CircleCount >= 2);
        for (int k = 0; k < fence.CircleCount; k++)
        {
            var c = fence.Circle(k);
            Assert.Equal(Props.FencePostRadius, c.Radius);
            Assert.InRange(c.X, 10f - 1e-4f, 10f + 1e-4f);
            if (k > 0)
                Assert.True(Vector2.Distance(fence.Circle(k - 1).Center, c.Center) <= Props.FenceCircleSpacing + 1e-4f);
        }
        // From end to end of its length.
        Assert.Equal(fence.Radius, MathF.Abs(fence.Circle(0).Z - 20f) + Props.FencePostRadius, 3);
        Assert.Equal(fence.Radius, MathF.Abs(fence.Circle(fence.CircleCount - 1).Z - 20f) + Props.FencePostRadius, 3);
        var lamp = new Prop(PropKind.Lamp, Area.Town, 1f, 2f, 0f, 0.3f, 2f);
        Assert.Equal(1, lamp.CircleCount);
        Assert.Equal((1f, 2f, 0.3f), (lamp.Circle(0).X, lamp.Circle(0).Z, lamp.Circle(0).Radius));
        Assert.True(Vector2.Distance(new Vector2(0f, -1f), lamp.Front) < 1e-6f);
    }

    [Fact]
    public void LoneGiantMushroomsStandInPropFreeGlades()
    {
        var all = Ground.Obstacles;
        int lone = 0;
        for (int i = 0; i < Ground.ForestTrunkCount; i++)
        {
            var o = all[i];
            bool alone = true;
            for (int k = 0; k < Ground.ForestTrunkCount && alone; k++)
                alone = k == i || Vector2.Distance(o.Center, all[k].Center) >= o.Radius + all[k].Radius + Props.GladeRadius;
            if (!alone)
                continue;
            lone++;
            foreach (var p in Props.All)
                Assert.True(Vector2.Distance(p.Center, o.Center) >= o.Radius + Props.GladeRadius + p.Radius, $"a prop in the glade at ({o.X}, {o.Z})");
        }
        Assert.True(lone >= 1);
    }

    [Fact]
    public void PropsAreDeterministic()
    {
        // Fixed data: the same props every run (no seed, no clock).
        Assert.Equal(Props.All.ToArray(), Props.All.ToArray());
        Assert.Equal(PropCountPinned, Props.Count);
        Assert.Equal(Props.Count, Props.All.Length);
        var first = Props.All[0];
        Assert.Equal(PropKind.MediumMushroom, first.Kind);
        Assert.Equal(Area.Forest, first.Area);
        Assert.Equal((first.X, first.Z), (Ground.Obstacles[Ground.PropCircleStart].X, Ground.Obstacles[Ground.PropCircleStart].Z));
        Assert.Equal(PropKind.Lamp, Props.All[Props.Count - 1].Kind);
        Assert.Equal(Area.Road, Props.All[Props.Count - 1].Area);
    }

    private const int PropCountPinned = 662;

    // Determinism

    [Fact]
    public void QueriesAreDeterministic()
    {
        float h = Ground.HeightAt(12.5f, 133.25f);
        bool blocked = Ground.Blocks(-24f, 212f);
        float sum = 0f;
        int count = 0;
        for (int i = 0; i < 20000; i++)
        {
            float x = i * 0.37f % 400f - 300f, z = i * 0.53f % 600f - 300f;
            sum += Ground.HeightAt(x, z);
            count += Ground.Blocks(x, z, 0.4f) ? 1 : 0;
            count += Areas.Walkable(x, z, 0.4f) ? 1 : 0;
            count += (int)Areas.At(x, z);
        }
        Assert.True(float.IsFinite(sum) && count > 0);
        Assert.Equal(h, Ground.HeightAt(12.5f, 133.25f));
        Assert.Equal(blocked, Ground.Blocks(-24f, 212f));
        Assert.True(blocked);
    }

    [Fact]
    public void BlocksAgreesWithABruteForceCheck()
    {
        var obstacles = Ground.Obstacles.ToArray();
        for (float x = -290f; x <= 110f; x += 1.7f)
        for (float z = -300f; z <= 300f; z += 1.3f)
        {
            bool expected = false;
            foreach (var o in obstacles)
            {
                float dx = x - o.X, dz = z - o.Z, r = o.Radius + 0.6f;
                expected |= dx * dx + dz * dz < r * r;
            }
            Assert.Equal(expected, Ground.Blocks(x, z, 0.6f));
        }
    }

    // Mushroom colors ("vivid", Tony 2026-10-25) and variety (Tony 2026-10-26)

    /// <summary>Hue in degrees (0..360), saturation and value (0..1) of an sRGB color.</summary>
    private static (float Hue, float Saturation, float Value) Hsv(Vector3 c)
    {
        float max = MathF.Max(c.X, MathF.Max(c.Y, c.Z)), min = MathF.Min(c.X, MathF.Min(c.Y, c.Z));
        float d = max - min;
        float hue = d == 0f ? 0f
            : max == c.X ? 60f * ((c.Y - c.Z) / d)
            : max == c.Y ? 60f * ((c.Z - c.X) / d + 2f)
            : 60f * ((c.X - c.Y) / d + 4f);
        return ((hue + 360f) % 360f, max == 0f ? 0f : d / max, max);
    }

    private static float HueDistance(float a, float b) => MathF.Min(MathF.Abs(a - b), 360f - MathF.Abs(a - b));

    private static readonly FungusKind[] AllKinds = Enum.GetValues<FungusKind>();

    [Fact]
    public void EveryKindsCapsAreSaturatedAndBright()
    {
        // Puffballs are the one pale kind: soft cream, still bright.
        foreach (var kind in AllKinds)
        {
            Assert.NotEmpty(MushroomPalette.Colors(kind));
            foreach (var color in MushroomPalette.Colors(kind))
            {
                var (_, saturation, value) = Hsv(color);
                Assert.InRange(value, kind == FungusKind.Puffball ? 0.97f : 0.84f, 1f);
                Assert.InRange(saturation, kind == FungusKind.Puffball ? 0.1f : 0.6f, kind == FungusKind.Puffball ? 0.25f : 1f);
            }
        }
        foreach (var color in MushroomPalette.LampColors)
        {
            var (_, saturation, value) = Hsv(color);
            Assert.InRange(saturation, 0.8f, 1f);
            Assert.InRange(value, 0.95f, 1f);
        }
    }

    [Fact]
    public void KindsHaveTheirRecognizableColors()
    {
        float Hue(Vector3 c) => Hsv(c).Hue;
        foreach (var c in MushroomPalette.FlyAgaric)
            Assert.True(HueDistance(Hue(c), 0f) <= 12f && Hsv(c).Saturation >= 0.88f, $"fly agaric {c} is not a pure red");
        foreach (var c in MushroomPalette.Chanterelle)
            Assert.InRange(Hue(c), 30f, 50f); // golden
        foreach (var c in MushroomPalette.Porcini)
            Assert.InRange(Hue(c), 20f, 35f); // warm brown
        foreach (var c in MushroomPalette.Amethyst)
            Assert.InRange(Hue(c), 265f, 300f); // purple
        foreach (var c in MushroomPalette.ScarletCup)
            Assert.True(HueDistance(Hue(c), 355f) <= 15f, $"scarlet cup {c}");
        foreach (var c in MushroomPalette.GlowCap)
            Assert.InRange(Hue(c), 120f, 200f); // blue to green
        // Every spot kind looks different from every other: no two share a color.
        var seen = new HashSet<Vector3>();
        foreach (var kind in AllKinds)
            foreach (var c in MushroomPalette.Colors(kind))
                Assert.True(seen.Add(c), $"{kind} repeats color {c}");
    }

    [Fact]
    public void SpotsArePureWhiteAndStemsCleanLightCream()
    {
        Assert.Equal(Vector3.One, MushroomPalette.SpotColor);
        foreach (var stem in new[] { MushroomPalette.StemLight, MushroomPalette.StemDark })
        {
            var (hue, saturation, value) = Hsv(stem);
            Assert.InRange(value, 0.96f, 1f);
            Assert.InRange(saturation, 0.08f, 0.22f);
            Assert.InRange(hue, 35f, 55f);
        }
    }

    [Fact]
    public void GlowingMushroomsGlowTwiceAsBright()
    {
        // Before: glowing giant caps 1.1, lamps 1.4.
        Assert.InRange(MushroomPalette.GiantGlow, 1.1f * 1.9f, 1.1f * 2.2f);
        Assert.InRange(MushroomPalette.LampGlow, 1.4f * 1.9f, 1.4f * 2.2f);
    }

    /// <summary>Every group of the cliffs' grid (as MapView lays it: 62 x 84 cells of 8 m), with its cell's kind.</summary>
    private static List<(FungusKind Spot, Fungus[] Group)> CliffGroups()
    {
        var kinds = Fungi.CliffKinds(62, 84);
        var all = new List<(FungusKind, Fungus[])>();
        var group = new Fungus[Fungi.MaxGroup];
        for (int k = 0; k < kinds.Length; k++)
        {
            float size = 0.9f + 1.4f * Fungi.Hash(k, 12);
            int n = Fungi.CliffGroup(kinds[k], k, size, group);
            all.Add((kinds[k], group[..n]));
        }
        return all;
    }

    [Fact]
    public void CliffsMixManyKindsInAllShapes()
    {
        var groups = CliffGroups();
        var kinds = new HashSet<FungusKind>();
        var shapes = new HashSet<CapShape>();
        foreach (var (_, group) in groups)
            foreach (var f in group)
            {
                kinds.Add(f.Kind);
                shapes.Add(f.Shape);
            }
        // Every kind (fly agarics ... glowing ones, brackets on the big stems) and every cap shape.
        Assert.Equal(AllKinds.Length, kinds.Count);
        Assert.Equal(Enum.GetValues<CapShape>().Length, shapes.Count);
        // No kind takes over: each spot kind is between 4% and 12% of the spots.
        foreach (var kind in AllKinds)
        {
            if (kind == FungusKind.Bracket)
                continue;
            int count = groups.FindAll(g => g.Spot == kind).Count;
            Assert.InRange(count, groups.Count * 0.04f, groups.Count * 0.12f);
        }
        // Singles, pairs and clusters (of different heights) are all common.
        int singles = 0, pairs = 0, clusters = 0, varied = 0;
        foreach (var (spot, group) in groups)
        {
            int standing = Array.FindAll(group, f => f.Kind != FungusKind.Bracket).Length;
            if (standing == 1) singles++;
            else if (standing == 2) pairs++;
            else clusters++;
            float min = float.MaxValue, max = 0f;
            foreach (var f in group)
                if (f.Kind != FungusKind.Bracket)
                {
                    min = MathF.Min(min, f.StemHeight + f.CapHeight);
                    max = MathF.Max(max, f.StemHeight + f.CapHeight);
                }
            if (standing > 1 && max > min * 1.2f)
                varied++;
        }
        // Nearly every pair or cluster has mushrooms of clearly different heights.
        Assert.True(varied >= (pairs + clusters) * 0.85f, $"{varied} of {pairs + clusters} groups vary in height");
        Assert.True(singles > groups.Count / 5 && pairs > groups.Count / 6 && clusters > groups.Count / 6, $"{singles} {pairs} {clusters}");
        // Every enoki spot is a bunch of thin stems with tiny caps.
        foreach (var (spot, group) in groups)
            if (spot == FungusKind.Enoki)
            {
                Assert.InRange(group.Length, 5, 9);
                foreach (var f in group)
                    Assert.True(f.CapRadius < f.StemHeight * 0.45f && f.StemRadius < f.CapRadius * 0.6f, "enoki stems are tall and thin, caps tiny");
            }
    }

    [Fact]
    public void SizesProportionsAndTiltsVaryWithinAKind()
    {
        var groups = CliffGroups();
        foreach (var kind in new[] { FungusKind.FlyAgaric, FungusKind.Porcini, FungusKind.Parasol, FungusKind.InkCap, FungusKind.GlowCap })
        {
            float minH = float.MaxValue, maxH = 0f, minRatio = float.MaxValue, maxRatio = 0f, maxTilt = 0f;
            foreach (var (_, group) in groups)
                foreach (var f in group)
                    if (f.Kind == kind)
                    {
                        float h = f.StemHeight + f.CapHeight;
                        minH = MathF.Min(minH, h);
                        maxH = MathF.Max(maxH, h);
                        minRatio = MathF.Min(minRatio, f.CapHeight / f.CapRadius);
                        maxRatio = MathF.Max(maxRatio, f.CapHeight / f.CapRadius);
                        maxTilt = MathF.Max(maxTilt, f.Tilt);
                        Assert.InRange(f.Tilt, 0f, 0.35f); // leaning, never toppling
                    }
            Assert.True(maxH > minH * 2f, $"{kind} heights {minH}..{maxH}");
            Assert.True(maxRatio > minRatio * 1.15f, $"{kind} cap proportions {minRatio}..{maxRatio}");
            Assert.True(maxTilt > 0.15f, $"{kind} never leans");
        }
        // Each kind keeps its silhouette: parasols wide and flat on tall thin stems, ink caps tall
        // bells, porcini fat, morels honeycomb cones, puffballs, coral and jelly sit on the ground.
        foreach (var (_, group) in groups)
            foreach (var f in group)
            {
                switch (f.Kind)
                {
                    case FungusKind.Parasol:
                        Assert.Equal(CapShape.Flat, f.Shape);
                        Assert.True(f.CapRadius > f.StemRadius * 6f && f.CapHeight < f.CapRadius * 0.5f);
                        break;
                    case FungusKind.InkCap:
                        Assert.Equal(CapShape.Bell, f.Shape);
                        Assert.True(f.CapHeight > f.CapRadius * 1.5f);
                        break;
                    case FungusKind.Porcini:
                        Assert.True(f.StemHeight < f.StemRadius * 8f, "porcini stems are fat");
                        break;
                    case FungusKind.Morel:
                        Assert.Equal(CapShape.Cone, f.Shape);
                        Assert.Equal(1, f.Pattern);
                        break;
                    case FungusKind.Chanterelle:
                        Assert.Contains(f.Shape, new[] { CapShape.Funnel, CapShape.Wavy });
                        break;
                    case FungusKind.Puffball:
                    case FungusKind.Coral:
                    case FungusKind.Jelly:
                        Assert.Equal(0f, f.StemHeight);
                        break;
                    case FungusKind.Bracket:
                        Assert.Equal(CapShape.Shelf, f.Shape);
                        Assert.True(f.Lift > 0f, "brackets grow on a stem, above the ground");
                        break;
                }
                Assert.Equal(f.Kind == FungusKind.FlyAgaric ? 1f : f.Spots, f.Spots); // fly agarics fully spotted
                Assert.Contains(f.Color, MushroomPalette.Colors(f.Kind));
                // Only the bioluminescent kind glows on the cliffs, and clearly.
                Assert.Equal(f.Kind == FungusKind.GlowCap ? MushroomPalette.GiantGlow : 0f, f.Glow);
            }
    }

    [Fact]
    public void BracketsGrowOnTheirMainStem()
    {
        int brackets = 0, leaning = 0;
        foreach (var (_, group) in CliffGroups())
        {
            var main = group[0];
            var axis = Vector3.Transform(Vector3.UnitY,
                Quaternion.CreateFromAxisAngle(new Vector3(MathF.Cos(main.TiltYaw), 0f, -MathF.Sin(main.TiltYaw)), main.Tilt));
            foreach (var f in group)
            {
                if (f.Kind != FungusKind.Bracket)
                    continue;
                brackets++;
                if (main.Tilt > 0.05f)
                    leaning++;
                Assert.NotEqual(FungusKind.Bracket, main.Kind);
                // The offset from the main foot: up the leaned stem axis, and a stem radius out from it.
                var at = Fungi.OnStem(main, f);
                float along = Vector3.Dot(at, axis);
                Assert.InRange(along, 0.01f, main.StemHeight);
                // Horizontally, from the leaned axis at the bracket's height.
                float t = at.Y / axis.Y;
                var off = at - axis * t;
                float out_ = new Vector2(off.X, off.Z).Length();
                Assert.InRange(out_, main.StemRadius * 0.85f, main.StemRadius * 1.1f);
            }
        }
        Assert.True(brackets > 20 && leaning > 5, $"{brackets} brackets, {leaning} on leaning stems");
        // Upright stem: straight up from the foot.
        var upright = new Fungus(FungusKind.Porcini, CapShape.Dome, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 6f, 2f, 1f, Vector3.One, 0f, 0, 0f);
        var shelf = upright with { Kind = FungusKind.Bracket, Shape = CapShape.Shelf, X = 0.95f, Lift = 3f };
        Assert.Equal(new Vector3(0.95f, 3f, 0f), Fungi.OnStem(upright, shelf));
    }

    [Fact]
    public void NoTwoNeighboringSpotsAreTheSameKindAndTheChoiceIsFixed()
    {
        var kinds = Fungi.CliffKinds(62, 84);
        for (int c = 0; c < 62; c++)
        for (int r = 0; r < 84; r++)
        for (int dc = -1; dc <= 1; dc++)
        for (int dr = -1; dr <= 1; dr++)
        {
            int nc = c + dc, nr = r + dr;
            if ((dc != 0 || dr != 0) && nc >= 0 && nc < 62 && nr >= 0 && nr < 84)
                Assert.NotEqual(kinds[c * 84 + r], kinds[nc * 84 + nr]);
        }
        // The same world every run.
        Assert.Equal(kinds, Fungi.CliffKinds(62, 84));
        var a = new Fungus[Fungi.MaxGroup];
        var b = new Fungus[Fungi.MaxGroup];
        for (int k = 0; k < 200; k++)
        {
            int n = Fungi.CliffGroup(kinds[k], k, 1.5f, a);
            Assert.Equal(n, Fungi.CliffGroup(kinds[k], k, 1.5f, b));
            Assert.Equal(a[..n], b[..n]);
        }
        Assert.Throws<ArgumentException>(() => Fungi.CliffGroup(FungusKind.FlyAgaric, 0, 1f, new Fungus[3]));
    }

    [Fact]
    public void RoadLampsStayExactlyTheirPropsAndVaryInShapeAndColor()
    {
        var shapes = new HashSet<CapShape>();
        var colors = new HashSet<Vector3>();
        int lamp = 0;
        Fungus previous = default;
        foreach (var p in Props.All)
        {
            if (p.Area != Area.Road || p.Kind != PropKind.Lamp)
                continue;
            var f = Fungi.Lamp(lamp, p.Height, p.Radius);
            Assert.Equal(f, Fungi.Lamp(lamp, p.Height, p.Radius));
            // Exactly where and as tall as the prop, never wider than its footprint, nearly upright.
            Assert.Equal((0f, 0f, 0f), (f.X, f.Z, f.Lift));
            Assert.Equal(p.Height, f.StemHeight);
            Assert.InRange(f.CapRadius, p.Radius * 0.75f, p.Radius);
            Assert.True(f.StemRadius < f.CapRadius);
            Assert.InRange(f.Tilt, 0f, 0.06f);
            // Each still a clear glowing lamp: a cap on a stem, glowing at LampGlow.
            Assert.Contains(f.Shape, Fungi.LampShapes);
            Assert.Equal(MushroomPalette.LampGlow, f.Glow);
            Assert.Contains(f.Color, MushroomPalette.LampColors);
            if (lamp > 0)
                Assert.NotEqual(previous.Color, f.Color);
            shapes.Add(f.Shape);
            colors.Add(f.Color);
            previous = f;
            lamp++;
        }
        Assert.True(lamp >= 6, $"{lamp} road lamps");
        Assert.True(shapes.Count >= 4, $"lamps in {shapes.Count} shapes");
        Assert.True(colors.Count >= 6, $"lamps in {colors.Count} colors");
    }

    // The vibrant world (Tony, 2026-10-26)

    [Fact]
    public void GroundIsLushBrightAndRicherThanBeforeButNeverNeon()
    {
        // Before: grass (0.5, 0.74, 0.33), meadow (0.62, 0.82, 0.38), dirt (0.7, 0.52, 0.36).
        void Richer(Vector3 color, Vector3 old)
        {
            var (_, s, v) = Hsv(color);
            var (_, oldS, oldV) = Hsv(old);
            Assert.True(s >= oldS + 0.1f, $"{color} is not clearly more saturated than {old}");
            Assert.True(v >= oldV, $"{color} is darker than {old}");
        }
        Richer(WorldPalette.Grass, new(0.5f, 0.74f, 0.33f));
        Richer(WorldPalette.Meadow, new(0.62f, 0.82f, 0.38f));
        Richer(WorldPalette.Dirt, new(0.7f, 0.52f, 0.36f));
        Richer(WorldPalette.Moss, new(0.3f, 0.56f, 0.32f));
        // Lush greens (not grey-olive), a yellow-green highlight, warm golden dirt.
        foreach (var green in new[] { WorldPalette.Grass, WorldPalette.Meadow, WorldPalette.Moss, WorldPalette.HedgeDark, WorldPalette.HedgeLight })
        {
            var (hue, s, _) = Hsv(green);
            Assert.InRange(hue, 85f, 145f);
            Assert.InRange(s, 0.6f, 0.85f);
        }
        Assert.InRange(Hsv(WorldPalette.GrassHighlight).Hue, 65f, 85f);
        Assert.InRange(Hsv(WorldPalette.Dirt).Hue, 30f, 45f);
        // Never neon nor washed out to white: no channel pinned at both ends for the ground, values below white.
        foreach (var c in new[] { WorldPalette.Grass, WorldPalette.Meadow, WorldPalette.Moss, WorldPalette.GrassHighlight, WorldPalette.Dirt, WorldPalette.Rock, WorldPalette.Stone, WorldPalette.Paving })
        {
            var (_, s, v) = Hsv(c);
            Assert.True(v <= 0.95f && s <= 0.85f, $"{c} is garish or washed out");
        }
        // Clean bright stone and paving, brighter than before ((0.7, 0.64, 0.56), (0.5, 0.46, 0.58)).
        Assert.True(Hsv(WorldPalette.Stone).Value >= 0.88f && Hsv(WorldPalette.Paving).Value >= 0.8f);
        // Paths still stand out from the grass: a clearly different hue and color.
        foreach (var green in new[] { WorldPalette.Grass, WorldPalette.Meadow, WorldPalette.GrassHighlight })
        {
            Assert.True(HueDistance(Hsv(WorldPalette.Dirt).Hue, Hsv(green).Hue) >= 35f, $"dirt too like {green}");
            Assert.True(Vector3.Distance(WorldPalette.Dirt, green) >= 0.3f, $"dirt too like {green}");
        }
    }

    [Fact]
    public void FlowersComeInManyHues()
    {
        Assert.True(WorldPalette.Flowers.Length >= 5);
        var hues = new List<float>();
        foreach (var c in WorldPalette.Flowers)
        {
            var (hue, s, v) = Hsv(c);
            Assert.True(s >= 0.6f && v >= 0.9f, $"flower {c} is dull");
            foreach (var other in hues)
                Assert.True(HueDistance(hue, other) >= 25f, $"flower {c} is too like another");
            hues.Add(hue);
        }
    }

    /// <summary>A file of this module, found by walking up from the test's output folder.</summary>
    private static string ModuleFile(string relative)
    {
        for (var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string path = System.IO.Path.Combine(dir.FullName, "modules", "Map", relative);
            if (System.IO.File.Exists(path))
                return System.IO.File.ReadAllText(path);
        }
        throw new System.IO.FileNotFoundException(relative);
    }

    [Fact]
    public void ViewDrawsMushroomsFromThePaletteWithGlossRimAndNoEyes()
    {
        string view = ModuleFile("Godot/MapView.cs");
        foreach (string member in new[] { "LampColors", "StemLight", "StemDark", "SpotColor", "LampGlow" })
            Assert.Contains("MushroomPalette." + member, view);
        // The mushrooms come from Fungi (kinds, groups, lamps), one cap MultiMesh per shape.
        foreach (string call in new[] { "Fungi.CliffKinds(", "Fungi.CliffGroup(", "Fungi.Lamp(", "MapMeshes.Cap(shape)" })
            Assert.Contains(call, view);
        string meshes = ModuleFile("Godot/MapMeshes.cs");
        foreach (var shape in Enum.GetValues<CapShape>())
            Assert.Contains("CapShape." + shape + " =>", meshes);
        // The ground from WorldPalette, with highlights and flowers in its shader.
        foreach (string member in new[] { "Grass", "Moss", "Meadow", "Dirt", "Rock", "Stone", "Paving", "Tiles", "GrassHighlight", "Flowers", "HedgeDark", "HedgeLight" })
            Assert.Contains("WorldPalette." + member, view);
        string ground = ModuleFile("Godot/Shaders/ground.gdshader");
        Assert.Contains("uniform vec3 flowers[6]", ground);
        Assert.Contains("uniform vec3 highlight", ground);
        // No eyes or faces on mushrooms (the designer's rule).
        Assert.DoesNotContain("Eye", view);
        Assert.DoesNotContain("eye", view);
        Assert.DoesNotContain("Eye", meshes);
        Assert.DoesNotContain("face", ModuleFile("Core/Fungi.cs"));
        Assert.Throws<System.IO.FileNotFoundException>(() => ModuleFile("Godot/Shaders/eye.gdshader"));
        // The caps' toon shading has a soft specular band and a rim light along the edge, spots
        // per mushroom and the morels' honeycomb.
        string cap = ModuleFile("Godot/Shaders/mushroom_cap.gdshader");
        Assert.Contains("uniform float gloss", cap);
        Assert.Contains("uniform float rim", cap);
        Assert.Contains("SPECULAR_LIGHT +=", cap);
        Assert.Contains("custom.z", cap);
        Assert.Contains("pits", cap);
        Assert.DoesNotContain("specular_disabled", cap);
    }

    // Module

    [Fact]
    public void ModuleStartsAndTicksWithoutMessages()
    {
        var module = new MapModule();
        var bus = new FakeBus();
        Assert.Throws<InvalidOperationException>(() => module.Tick(new TickContext(0, 1.0 / 60)));
        module.Start(bus);
        Assert.True(module.Started);
        for (int i = 0; i < 120; i++)
            module.Tick(new TickContext(i, 1.0 / 60));
        Assert.Equal(0, bus.Published);
        Assert.Equal(0, bus.Subscriptions);
        Assert.Throws<ArgumentNullException>(() => new MapModule().Start(null!));
    }
}
