using System;
using System.Collections.Generic;
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
        Assert.Equal(9, town);
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
    public void KindsTownDoesNotDrawAreSkippedNotThrown()
    {
        Assert.False(TownLayout.TryPieceFor(BuildingKind.PetStore, out _));
        Assert.False(TownLayout.TryPieceFor(BuildingKind.CityBlock, out _));
        Assert.False(TownLayout.TryPieceFor(BuildingKind.ClothingShop, out _));
        Assert.False(TownLayout.TryPieceFor((BuildingKind)999, out _));

        // A Town building of a kind not drawn yet (ClothingShop, drawn by a later job) gets no
        // placement, and the layout still builds.
        var buildings = Buildings.All;
        bool sawUndrawn = false;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (buildings[i].Area != Area.Town || TownLayout.TryPieceFor(buildings[i].Kind, out _))
                continue;
            sawUndrawn = true;
            foreach (var p in TownLayout.All)
                Assert.NotEqual(i, p.BuildingIndex);
        }
        Assert.True(sawUndrawn);
        Assert.True(TownLayout.Count > 9);
    }

    [Fact]
    public void BuildingsComeFirstThenOnlyProps()
    {
        var all = TownLayout.All;
        for (int i = 0; i < all.Length; i++)
            Assert.Equal(i < 9, all[i].BuildingIndex >= 0);
    }

    [Fact]
    public void TheTownHasLampsBenchesFencesAndFlowerBeds()
    {
        var counts = new Dictionary<TownPiece, int>();
        foreach (var p in TownLayout.All)
            counts[p.Piece] = counts.GetValueOrDefault(p.Piece) + 1;
        Assert.InRange(counts.GetValueOrDefault(TownPiece.StreetLamp), 12, 60);
        Assert.InRange(counts.GetValueOrDefault(TownPiece.Bench), 3, 12);
        Assert.InRange(counts.GetValueOrDefault(TownPiece.Fence), 30, 200);
        Assert.InRange(counts.GetValueOrDefault(TownPiece.FlowerBed), 8, 60);
    }

    [Fact]
    public void PropsStandOnTownGroundOffBuildingsPathsAndSpotsAndApart()
    {
        var all = TownLayout.All;
        var spots = new[] { Places.HomeDoor, Places.ClinicDoor, Places.MillieSpot, Places.FurnitureShopSpot, Places.SignpostTown };
        for (int i = 0; i < all.Length; i++)
        {
            var p = all[i];
            if (p.BuildingIndex >= 0)
                continue;
            string what = $"{p.Piece} at ({p.X}, {p.Z})";
            float r = p.Radius;
            Assert.True(r > 0f, what);
            Assert.Equal(Area.Town, Areas.At(p.X, p.Z));
            Assert.True(Areas.Walkable(p.X, p.Z, r), what);
            Assert.False(Ground.Blocks(p.X, p.Z), what);
            foreach (var b in Buildings.All)
                Assert.False(b.Contains(p.X, p.Z, r + 0.5f), $"{what} on {b.Kind}");
            Assert.True(TownLayout.PathDistance(p.X, p.Z) >= r + 1.5f, $"{what} on a path");
            foreach (var spot in spots)
                Assert.True(Vector2.Distance(new Vector2(p.X, p.Z), new Vector2(spot.Position.X, spot.Position.Z)) >= r + 1f, $"{what} by a spot");
            for (int k = i + 1; k < all.Length; k++)
            {
                var q = all[k];
                if (q.BuildingIndex < 0)
                    Assert.True(Vector2.Distance(new Vector2(p.X, p.Z), new Vector2(q.X, q.Z)) >= r + q.Radius - 0.02f, $"{what} overlaps {q.Piece}");
            }
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
    public void BenchesFaceTheFountain()
    {
        int benches = 0;
        foreach (var p in TownLayout.All)
        {
            if (p.Piece != TownPiece.Bench)
                continue;
            benches++;
            var front = new Vector2(-MathF.Sin(p.Yaw), -MathF.Cos(p.Yaw));
            var toFountain = Vector2.Normalize(Buildings.TownSquare - new Vector2(p.X, p.Z));
            Assert.True(Vector2.Dot(front, toFountain) > 0.99f);
        }
        Assert.True(benches > 0);
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
