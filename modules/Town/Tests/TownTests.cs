using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Numerics;
using Fungtopia.DayNight;
using Fungtopia.Map;
using Fungtopia.Shell;
using Xunit;

namespace Fungtopia.Town.Tests;

public class TownTests
{
    /// <summary>Fake of the shell's bus: delivers messages like the real one and records what Town publishes.</summary>
    private sealed class FakeBus : IMessageBus
    {
        private readonly Dictionary<Type, List<object>> _handlers = new();
        public readonly List<TownLights> Lights = new();
        public int Published;

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
                _handlers[typeof(T)] = list = new List<object>();
            list.Add(handler);
        }

        public void Publish<T>(in T message) where T : struct
        {
            if (message is TownLights lights)
            {
                Lights.Add(lights);
                Published++;
            }
            if (_handlers.TryGetValue(typeof(T), out var list))
                foreach (var handler in list)
                    ((IMessageHandler<T>)handler).Handle(message);
        }
    }

    private static readonly TickContext Step = new(0, 1.0 / 60);

    private static TimeOfDay At(int day, float hour) => new(day, hour, TimeOfDay.IsNightAt(hour));

    // ---- The placement list ----

    [Fact]
    public void EveryTownBuildingOfTheMapIsDrawnExactlyOnceWhereTheMapPutsIt()
    {
        var buildings = Buildings.All;
        int town = 0;
        for (int i = 0; i < buildings.Length; i++)
        {
            var b = buildings[i];
            int drawn = 0;
            foreach (var p in TownLayout.All)
            {
                if (p.BuildingIndex != i)
                    continue;
                drawn++;
                Assert.True(TownLayout.TryPieceFor(b.Kind, out var piece));
                Assert.Equal(piece, p.Piece);
                Assert.Equal(b.X, p.X);
                Assert.Equal(b.Z, p.Z);
                Assert.Equal(b.Rotation, p.Yaw);
                Assert.Equal(b.Width, p.Width);
                Assert.Equal(b.Depth, p.Depth);
                Assert.Equal(b.Height, p.Height);
            }
            bool drawable = b.Area == Area.Town && TownLayout.TryPieceFor(b.Kind, out _);
            Assert.Equal(drawable ? 1 : 0, drawn);
            if (drawable)
                town++;
        }
        Assert.Equal(10, town);
    }

    [Fact]
    public void EachNamedBuildingAndFourPlainHousesInDifferentLooks()
    {
        int Count(TownPiece piece)
        {
            int n = 0;
            foreach (var p in TownLayout.All)
                if (p.Piece == piece)
                    n++;
            return n;
        }
        Assert.Equal(1, Count(TownPiece.PlayerHouse));
        Assert.Equal(1, Count(TownPiece.MillieHouse));
        Assert.Equal(1, Count(TownPiece.Clinic));
        Assert.Equal(1, Count(TownPiece.FurnitureShop));
        Assert.Equal(1, Count(TownPiece.Fountain));
        Assert.Equal(1, Count(TownPiece.ClothingShop));
        Assert.Equal(4, Count(TownPiece.House));
        var looks = new HashSet<int>();
        foreach (var p in TownLayout.All)
            if (p.Piece == TownPiece.House)
                looks.Add(p.Variant);
        Assert.Equal(new HashSet<int> { 0, 1, 2, 3 }, looks);

        // Your house is the biggest.
        var home = Buildings.First(BuildingKind.PlayerHouse);
        foreach (var b in Buildings.All)
            if (b.Area == Area.Town && b.Kind != BuildingKind.PlayerHouse)
                Assert.True(b.Width * b.Depth * b.Height < home.Width * home.Depth * home.Height, b.Kind.ToString());
    }

    [Fact]
    public void AbbysClothingShopIsDrawnOnceAtTheMapsSpot()
    {
        Assert.True(TownLayout.TryPieceFor(BuildingKind.ClothingShop, out var piece));
        Assert.Equal(TownPiece.ClothingShop, piece);
        var buildings = Buildings.All;
        int index = -1;
        for (int i = 0; i < buildings.Length; i++)
            if (buildings[i].Kind == BuildingKind.ClothingShop)
                index = i;
        Assert.True(index >= 0);
        var shop = buildings[index];

        int drawn = 0;
        foreach (var p in TownLayout.All)
        {
            if (p.Piece != TownPiece.ClothingShop)
                continue;
            drawn++;
            Assert.Equal(index, p.BuildingIndex);
            Assert.Equal(0, p.Variant);
            Assert.Equal(shop.X, p.X);
            Assert.Equal(shop.Z, p.Z);
            Assert.Equal(shop.Rotation, p.Yaw);
            Assert.Equal(shop.Width, p.Width);
            Assert.Equal(shop.Depth, p.Depth);
            Assert.Equal(shop.Height, p.Height);
        }
        Assert.Equal(1, drawn);
    }

    /// <summary>The names of the objects in Models/town.glb (read from its JSON chunk).</summary>
    private static HashSet<string> ModelObjects()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "modules", "Town", "Models", "town.glb")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        byte[] glb = File.ReadAllBytes(Path.Combine(dir!.FullName, "modules", "Town", "Models", "town.glb"));
        Assert.Equal("glTF", Encoding.ASCII.GetString(glb, 0, 4));
        int length = BitConverter.ToInt32(glb, 12);
        using var json = JsonDocument.Parse(Encoding.UTF8.GetString(glb, 20, length));
        var names = new HashSet<string>();
        foreach (var node in json.RootElement.GetProperty("nodes").EnumerateArray())
            if (node.TryGetProperty("name", out var name))
                names.Add(name.GetString()!);
        return names;
    }

    [Fact]
    public void TheModelHasAnObjectForEveryPlacementIncludingAbbysShop()
    {
        var names = ModelObjects();
        Assert.Contains("ClothingShop", names);
        foreach (var p in TownLayout.All)
        {
            // TownView.ModelName: a House by its look, anything else by its piece.
            string name = p.Piece == TownPiece.House ? $"House{p.Variant}" : p.Piece.ToString();
            Assert.Contains(name, names);
        }
    }

    [Fact]
    public void KindsTownDoesNotDrawAreSkippedNotThrown()
    {
        Assert.False(TownLayout.TryPieceFor(BuildingKind.PetStore, out _));
        Assert.False(TownLayout.TryPieceFor(BuildingKind.CityBlock, out _));
        Assert.False(TownLayout.TryPieceFor((BuildingKind)999, out _));

        // PetCity's buildings get no placement, and the layout still builds.
        var buildings = Buildings.All;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (buildings[i].Area == Area.Town)
                continue;
            foreach (var p in TownLayout.All)
                Assert.NotEqual(i, p.BuildingIndex);
        }
        Assert.True(TownLayout.Count > 10);
    }

    [Fact]
    public void BuildingsComeFirstThenTheMapsPropsThenDecoration()
    {
        var all = TownLayout.All;
        int stage = 0;
        foreach (var p in all)
        {
            int s = p.BuildingIndex >= 0 ? 0 : p.PropIndex >= 0 ? 1 : 2;
            Assert.False(p.BuildingIndex >= 0 && p.PropIndex >= 0);
            Assert.True(s >= stage);
            stage = s;
        }
        Assert.Equal(10, CountWhere(p => p.BuildingIndex >= 0));
    }

    private static int CountWhere(Func<TownPlacement, bool> match)
    {
        int n = 0;
        foreach (var p in TownLayout.All)
            if (match(p))
                n++;
        return n;
    }

    [Fact]
    public void TheDrawnPropsAreExactlyTheMapsTownProps()
    {
        // Each Town prop of the Map, in order, drawn once, where, turned and as large as the Map says.
        var props = Props.All;
        var drawn = new List<TownPlacement>();
        foreach (var p in TownLayout.All)
            if (p.PropIndex >= 0)
                drawn.Add(p);
        var expected = new List<int>();
        for (int i = 0; i < props.Length; i++)
            if (props[i].Area == Area.Town)
                expected.Add(i);
        Assert.True(expected.Count > 40);
        Assert.Equal(expected, drawn.ConvertAll(p => p.PropIndex));

        foreach (var p in drawn)
        {
            var m = props[p.PropIndex];
            string what = $"{m.Kind} at ({m.X}, {m.Z})";
            Assert.True(TownLayout.TryPieceFor(m.Kind, out var piece), what);
            Assert.Equal(piece, p.Piece);
            Assert.Equal(m.X, p.X);
            Assert.Equal(m.Z, p.Z);
            Assert.Equal(m.Rotation, p.Yaw);
            Assert.Equal(m.Height, p.Height);
            switch (m.Kind)
            {
                case PropKind.Bench:
                case PropKind.FenceSegment:
                    // As long as the Map's segment (along local X), thinner than its circles.
                    Assert.Equal(m.Radius * 2f, p.Width, 4);
                    Assert.True(p.Depth <= Math.Max(m.Radius, Props.FencePostRadius) * 2f, what);
                    break;
                case PropKind.Crate:
                    // A square whose corners touch its circle.
                    Assert.Equal(p.Width, p.Depth);
                    Assert.Equal(m.Radius, MathF.Sqrt(p.Width * p.Width + p.Depth * p.Depth) * 0.5f, 4);
                    break;
                default:
                    Assert.Equal(m.Radius * 2f, p.Width, 4);
                    Assert.Equal(m.Radius * 2f, p.Depth, 4);
                    break;
            }
        }
    }

    [Fact]
    public void EveryTownPropKindOfTheMapHasAPieceAndOthersAreSkipped()
    {
        foreach (var m in Props.All)
            if (m.Area == Area.Town)
                Assert.True(TownLayout.TryPieceFor(m.Kind, out _), m.Kind.ToString());
        Assert.False(TownLayout.TryPieceFor(PropKind.MediumMushroom, out _));
        Assert.False(TownLayout.TryPieceFor((PropKind)999, out _));
        // Other areas' props are not Town's to draw.
        foreach (var p in TownLayout.All)
            if (p.PropIndex >= 0)
                Assert.Equal(Area.Town, Props.All[p.PropIndex].Area);
    }

    [Fact]
    public void TheTownHasTheMapsLampsBenchesFencesPlantersAndCrates()
    {
        Assert.True(CountWhere(p => p.Piece == TownPiece.StreetLamp) >= 10);
        Assert.True(CountWhere(p => p.Piece == TownPiece.Bench) >= 3);
        Assert.True(CountWhere(p => p.Piece == TownPiece.Fence) >= 14);
        Assert.True(CountWhere(p => p.Piece == TownPiece.Planter) >= 3);
        Assert.Equal(2, CountWhere(p => p.Piece == TownPiece.Crate));
        foreach (var p in TownLayout.All)
            if (p.Piece is TownPiece.StreetLamp or TownPiece.Bench or TownPiece.Fence or TownPiece.Planter or TownPiece.Crate)
                Assert.True(p.PropIndex >= 0, $"{p.Piece} at ({p.X}, {p.Z}) is not one of the Map's props");
    }

    [Fact]
    public void OnlySmallFlowerClumpsArePlacedFreely()
    {
        int flowers = 0;
        foreach (var p in TownLayout.All)
        {
            if (!p.IsDecoration)
                continue;
            flowers++;
            Assert.Equal(TownPiece.Flowers, p.Piece);
            Assert.True(p.Height <= TownLayout.DecorationMaxHeight);
            Assert.True(p.Width <= TownLayout.DecorationMaxWidth && p.Depth <= TownLayout.DecorationMaxWidth);
            Assert.True(TownLayout.DecorationMaxHeight <= 0.4f && TownLayout.DecorationMaxWidth <= 0.6f);
        }
        Assert.InRange(flowers, 10, 200);
    }

    [Fact]
    public void FlowerClumpsStayOffObstaclesPathsAndSpots()
    {
        var all = TownLayout.All;
        var spots = new[] { Places.HomeDoor, Places.ClinicDoor, Places.MillieSpot, Places.FurnitureShopSpot, Places.AbbySpot, Places.SignpostTown };
        for (int i = 0; i < all.Length; i++)
        {
            var p = all[i];
            if (!p.IsDecoration)
                continue;
            string what = $"{p.Piece} at ({p.X}, {p.Z})";
            float r = p.Radius;
            Assert.Equal(Area.Town, Areas.At(p.X, p.Z));
            Assert.True(Areas.Walkable(p.X, p.Z, r), what);
            Assert.False(Ground.Blocks(p.X, p.Z, r), what);
            Assert.True(TownLayout.PathDistance(p.X, p.Z) >= r + 1.5f, $"{what} on a path");
            foreach (var spot in spots)
                Assert.True(Vector2.Distance(new Vector2(p.X, p.Z), new Vector2(spot.Position.X, spot.Position.Z)) >= r + 1f, $"{what} by a spot");
            for (int k = i + 1; k < all.Length; k++)
                if (all[k].IsDecoration)
                    Assert.True(Vector2.Distance(new Vector2(p.X, p.Z), new Vector2(all[k].X, all[k].Z)) >= r + all[k].Radius - 0.02f, $"{what} overlaps another");
        }
    }

    [Fact]
    public void PathDistanceFollowsTheStreetAndTheDoorPaths()
    {
        Assert.Equal(0f, TownLayout.PathDistance(0f, 180f), 3);
        Assert.Equal(4f, TownLayout.PathDistance(4f, 180f), 3);
        var home = Buildings.First(BuildingKind.PlayerHouse);
        var front = home.Center + home.Front * (home.Depth * 0.5f + 1f);
        Assert.Equal(0f, TownLayout.PathDistance(front.X, front.Y), 3);
    }

    [Fact]
    public void TheLayoutIsTheSameEveryTime()
    {
        var a = TownLayout.All.ToArray();
        var b = TownLayout.All.ToArray();
        Assert.Equal(a, b);
        Assert.Equal(TownLayout.Count, a.Length);
    }

    // ---- Standing on the ground ----

    /// <summary>Ground under the footprint, densely: <paramref name="rings"/> rings of 64 points out to its edge, and the center.</summary>
    private static IEnumerable<Vector2> DenseFootprint(TownPiece piece, float x, float z, float yaw, float width, float depth, int rings = 4)
    {
        yield return new Vector2(x, z);
        float cy = MathF.Cos(yaw), sy = MathF.Sin(yaw);
        bool boxy = TownGround.IsBoxy(piece);
        for (int r = 1; r <= rings; r++)
            for (int i = 0; i < 64; i++)
            {
                float a = i * MathF.Tau / 64f, c = MathF.Cos(a), s = MathF.Sin(a);
                if (boxy)
                {
                    float m = MathF.Max(MathF.Abs(c), MathF.Abs(s));
                    c /= m;
                    s /= m;
                }
                float lx = c * width * 0.5f * r / rings, lz = s * depth * 0.5f * r / rings;
                yield return new Vector2(x + lx * cy + lz * sy, z - lx * sy + lz * cy);
            }
    }

    /// <summary>Points over the town, the roads and the rough Deep Forest, every few meters, walkable ones only.</summary>
    private static IEnumerable<Vector2> SamplePositions()
    {
        foreach (var area in new[] { Areas.Town, Areas.DeepForest, Areas.Forest })
            for (float x = area.MinX + 1.3f; x < area.MaxX; x += 3.7f)
                for (float z = area.MinZ + 0.9f; z < area.MaxZ; z += 3.7f)
                    if (Areas.Walkable(x, z, 0.5f))
                        yield return new Vector2(x, z);
        for (int i = 0; i < Areas.RoadPoints.Length; i++)
            if (!float.IsNaN(Areas.RoadPoints[i].X))
                yield return Areas.RoadPoints[i];
    }

    [Fact]
    public void TheFootprintIsTheCenterAndAtLeastEightPointsOnItsEdge()
    {
        Assert.True(TownGround.EdgePoints >= 8);
        var center = TownGround.FootprintPoint(TownPiece.StreetLamp, 10f, 200f, 0.7f, 0.8f, 0.8f, 0);
        Assert.Equal(new Vector2(10f, 200f), center);
        var angles = new HashSet<int>();
        for (int k = 1; k <= TownGround.EdgePoints; k++)
        {
            var w = TownGround.FootprintPoint(TownPiece.StreetLamp, 10f, 200f, 0.7f, 0.8f, 0.8f, k);
            Assert.Equal(0.4f, Vector2.Distance(w, center), 3);
            angles.Add((int)MathF.Round(MathF.Atan2(w.Y - 200f, w.X - 10f) * 100f));
        }
        Assert.Equal(TownGround.EdgePoints, angles.Count);
        // A box's edge points reach its corners.
        var corner = TownGround.FootprintPoint(TownPiece.Crate, 0f, 0f, 0f, 2f, 1f, 1 + TownGround.EdgePoints / 8);
        Assert.Equal(1f, MathF.Abs(corner.X), 3);
        Assert.Equal(0.5f, MathF.Abs(corner.Y), 3);
        Assert.Throws<ArgumentOutOfRangeException>(() => TownGround.FootprintPoint(TownPiece.Crate, 0f, 0f, 0f, 1f, 1f, TownGround.FootprintPoints));
    }

    [Fact]
    public void StemsAnywhereSitInTheGroundNeverFloatingNorSunkDeep()
    {
        // Street lamps, planters and mushroom stems of the sizes Town draws, all over the walkable world.
        var stems = new (TownPiece Piece, float Width, float Height)[]
        {
            (TownPiece.StreetLamp, 0.8f, 3.2f), (TownPiece.Planter, 1.2f, 0.7f), (TownPiece.StreetLamp, 0.6f, 2.5f),
            (TownPiece.Crate, 0.71f, 0.8f), (TownPiece.Flowers, 0.5f, 0.35f),
        };
        int checkedCount = 0, sloped = 0;
        foreach (var at in SamplePositions())
            foreach (var (piece, width, height) in stems)
            {
                float yaw = at.X * 0.37f + at.Y * 0.11f;
                var fit = TownGround.Fit(piece, at.X, at.Y, yaw, width, width, height);
                string what = $"{piece} at ({at.X}, {at.Y})";
                float high = float.MinValue, low = float.MaxValue;
                foreach (var w in DenseFootprint(piece, at.X, at.Y, yaw, width, width))
                {
                    float h = Ground.HeightAt(w.X, w.Y);
                    Assert.True(fit.Base <= h, $"{what}: base {fit.Base} floats over ground {h} at ({w.X}, {w.Y})");
                    high = MathF.Max(high, h);
                    low = MathF.Min(low, h);
                }
                // A stem (up to 0.8 m across) at most 0.3 m under its highest ground; wider pieces sink only by the drop under them.
                if (width <= 0.8f)
                    Assert.True(fit.Base >= high - 0.3f, $"{what}: base {fit.Base} sunk more than 0.3 m under the highest ground {high}");
                Assert.True(fit.Base >= low - 0.2f, $"{what}: base {fit.Base} sunk more than 0.2 m under the lowest ground {low}");
                Assert.Equal(height, fit.Top - fit.High, 3);
                Assert.Equal(fit.Top - fit.Base, fit.DrawnHeight, 3);
                checkedCount++;
                if (fit.High - fit.Low > 0.02f)
                    sloped++;
            }
        Assert.True(checkedCount > 2000, $"{checkedCount}");
        Assert.True(sloped > 500, $"only {sloped} sample spots on sloped ground");
    }

    [Fact]
    public void EveryPlacementStandsInTheGroundAndLooksAsTallAsItIs()
    {
        foreach (var p in TownLayout.All)
        {
            var fit = TownGround.Fit(p);
            string what = $"{p.Piece} at ({p.X}, {p.Z})";
            float low = float.MaxValue;
            foreach (var w in DenseFootprint(p.Piece, p.X, p.Z, p.Yaw, p.Width, p.Depth, rings: 8))
            {
                float h = Ground.HeightAt(w.X, w.Y);
                Assert.True(fit.Base <= h, $"{what}: base {fit.Base} floats over ground {h} at ({w.X}, {w.Y})");
                low = MathF.Min(low, h);
            }
            for (int k = 0; k < TownGround.FootprintPoints; k++)
            {
                var w = TownGround.FootprintPoint(p, k);
                Assert.True(fit.Base <= Ground.HeightAt(w.X, w.Y) - TownGround.SinkOf(p.Piece) + 1e-4f, what);
            }
            Assert.InRange(TownGround.SinkOf(p.Piece), 0.01f, 0.2f);
            Assert.True(fit.Base >= low - 0.2f, $"{what}: sunk {low - fit.Base} m");
            // Its stem reaches down; from the high side it is exactly as tall as the Map says.
            Assert.Equal(p.Height, fit.Top - fit.High, 3);
            Assert.True(fit.DrawnHeight >= p.Height + TownGround.SinkOf(p.Piece) - 1e-4f, what);
            if (p.BuildingIndex < 0)
                Assert.True(fit.Extra <= 0.3f, $"{what}: stem reaches {fit.Extra} m down");
        }
    }

    [Fact]
    public void BigStemsAndFoundationsFlareIntoTheGround()
    {
        foreach (var piece in new[] { TownPiece.PlayerHouse, TownPiece.MillieHouse, TownPiece.Clinic, TownPiece.House, TownPiece.ClothingShop, TownPiece.Fountain, TownPiece.StreetLamp, TownPiece.Planter })
            Assert.True(TownGround.HasRootFlare(piece), piece.ToString());
        foreach (var piece in new[] { TownPiece.Flowers, TownPiece.Fence, TownPiece.Bench, TownPiece.Crate })
            Assert.False(TownGround.HasRootFlare(piece), piece.ToString());
    }

    // ---- The module: lamps follow night ----

    [Fact]
    public void LampsAreOffByDayAndLightUpAtNightfall()
    {
        var bus = new FakeBus();
        var town = new TownModule();
        town.Start(bus);
        Assert.False(town.LampsLit);

        bus.Publish(At(1, 9f));
        town.Tick(Step);
        Assert.False(town.LampsLit);
        Assert.Empty(bus.Lights);

        bus.Publish(At(1, 19.99f));
        Assert.False(town.LampsLit);
        bus.Publish(At(1, 20f));
        Assert.True(town.LampsLit);
        Assert.Equal(new[] { true }, bus.Lights.ConvertAll(l => l.On));

        bus.Publish(At(2, 3f));
        Assert.True(town.LampsLit);
        bus.Publish(At(2, 6f));
        Assert.False(town.LampsLit);
        Assert.Equal(new[] { true, false }, bus.Lights.ConvertAll(l => l.On));
    }

    [Fact]
    public void LampsFollowIsNightAsPublished()
    {
        var bus = new FakeBus();
        var town = new TownModule();
        town.Start(bus);
        bus.Publish(new TimeOfDay(1, 12f, true));
        Assert.True(town.LampsLit);
        bus.Publish(new TimeOfDay(1, 23f, false));
        Assert.False(town.LampsLit);
    }

    [Fact]
    public void LightsArePublishedOnlyWhenTheySwitch()
    {
        var bus = new FakeBus();
        var town = new TownModule();
        town.Start(bus);
        for (int i = 0; i < 600; i++)
        {
            bus.Publish(At(1, 19.5f + i * 0.0008f));
            town.Tick(Step);
        }
        Assert.Equal(0, bus.Published);
        for (int i = 0; i < 600; i++)
        {
            bus.Publish(At(1, 20f + i * 0.001f));
            town.Tick(Step);
        }
        Assert.Equal(1, bus.Published);
    }

    [Fact]
    public void WokeUpAtTheClinicInTheMorningTheLampsAreOff()
    {
        var bus = new FakeBus();
        var town = new TownModule();
        town.Start(bus);
        bus.Publish(At(1, 22f));
        Assert.True(town.LampsLit);
        // Fainting skips the clock straight to 7:00 the next morning.
        bus.Publish(new WokeUp(2));
        bus.Publish(At(2, 7f));
        Assert.False(town.LampsLit);
    }

    [Fact]
    public void TickAndTimeDoNotAllocate()
    {
        var bus = new FakeBus();
        var town = new TownModule();
        town.Start(bus);
        var day = At(1, 12f);
        town.Handle(day);
        town.Tick(Step);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            town.Handle(day);
            town.Tick(new TickContext(i, 1.0 / 60));
        }
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    [Fact]
    public void TickBeforeStartThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new TownModule().Tick(Step));
        Assert.Throws<ArgumentNullException>(() => new TownModule().Start(null!));
    }
}
