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
        yield return Places.LucySpot;
        yield return Places.SignpostTown;
        yield return Places.SignpostForestSouth;
        yield return Places.SignpostForestNorth;
        yield return Places.SignpostPetCity;
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
            Assert.False(Areas.Walkable(-103f, z));
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
        var box = new AreaBounds(-110f, -300f, 110f, 300f);
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

    // Outside

    [Fact]
    public void OutsideTheAreasIsNotWalkable()
    {
        for (float x = -200f; x <= 200f; x += 2f)
        for (float z = -350f; z <= 350f; z += 2f)
        {
            bool inside = Areas.Forest.Contains(x, z) || Areas.Town.Contains(x, z) || Areas.PetCity.Contains(x, z)
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
        // Off the walkable ground: the nearest area.
        Assert.Equal(Area.Town, Areas.At(0f, 300f));
        Assert.Equal(Area.PetCity, Areas.At(-90f, -200f));
        Assert.Equal(Area.Forest, Areas.At(110f, 0f));
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
        for (float x = -150f; x <= 150f; x += step)
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
        Check(Places.SignpostTown, Area.Town);
        Check(Places.SignpostForestSouth, Area.Forest);
        Check(Places.SignpostForestNorth, Area.Forest);
        Check(Places.SignpostPetCity, Area.PetCity);
        Check(Places.LucySpot, Area.PetCity);
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
        Assert.Throws<ArgumentOutOfRangeException>(() => Places.Signpost(Area.Road));
        Assert.Throws<ArgumentOutOfRangeException>(() => Places.Signpost(Area.Home));
        // Within 10 m of its entrance (and of the road there).
        Assert.True(Vector2.Distance(new(Places.SignpostTown.Position.X, Places.SignpostTown.Position.Z), new(0f, 150f)) < 10f);
        Assert.True(Vector2.Distance(new(Places.SignpostPetCity.Position.X, Places.SignpostPetCity.Position.Z), new(0f, -150f)) < 10f);
        Assert.True(Vector2.Distance(new(Places.SignpostForestSouth.Position.X, Places.SignpostForestSouth.Position.Z), Areas.ForestGapSouth) < 10f);
        Assert.True(Vector2.Distance(new(Places.SignpostForestNorth.Position.X, Places.SignpostForestNorth.Position.Z), Areas.ForestGapNorth) < 10f);
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
        foreach (var kind in new[] { BuildingKind.PlayerHouse, BuildingKind.MillieHouse, BuildingKind.Clinic, BuildingKind.FurnitureShop, BuildingKind.Fountain, BuildingKind.PetStore })
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
    public void ObstaclesCoverEveryBuildingAndTheFountain()
    {
        int circles = Ground.Obstacles.Length - Ground.ForestTrunkCount;
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

    // Determinism and cost

    [Fact]
    public void QueriesAreDeterministicAndDoNotAllocate()
    {
        float h = Ground.HeightAt(12.5f, 133.25f);
        bool blocked = Ground.Blocks(-24f, 212f);
        Areas.At(0f, 0f); Areas.Walkable(0f, 0f, 0.4f); Ground.Blocks(0f, 0f, 0.4f); // warm up
        long before = GC.GetAllocatedBytesForCurrentThread();
        float sum = 0f;
        int count = 0;
        for (int i = 0; i < 20000; i++)
        {
            float x = i * 0.37f % 200f - 100f, z = i * 0.53f % 600f - 300f;
            sum += Ground.HeightAt(x, z);
            count += Ground.Blocks(x, z, 0.4f) ? 1 : 0;
            count += Areas.Walkable(x, z, 0.4f) ? 1 : 0;
            count += (int)Areas.At(x, z);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.True(float.IsFinite(sum) && count > 0);
        Assert.Equal(h, Ground.HeightAt(12.5f, 133.25f));
        Assert.Equal(blocked, Ground.Blocks(-24f, 212f));
        Assert.True(blocked);
    }

    [Fact]
    public void BlocksAgreesWithABruteForceCheck()
    {
        var obstacles = Ground.Obstacles.ToArray();
        for (float x = -110f; x <= 110f; x += 1.7f)
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
