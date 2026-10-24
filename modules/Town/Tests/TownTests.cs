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
