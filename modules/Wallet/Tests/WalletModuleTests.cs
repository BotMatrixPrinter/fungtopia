using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Monsters;
using Fungtopia.Player;
using Fungtopia.Save;
using Fungtopia.Shell;
using Xunit;

namespace Fungtopia.Wallet.Tests;

public class WalletModuleTests
{
    private const int Player = 0;
    private const int Pet = 1;

    /// <summary>
    /// Fake of the shell's bus: delivers messages to subscribers like the real one, and records
    /// every WalletState, CoinsEarned, SpendResult and SavePart published.
    /// </summary>
    private sealed class FakeBus : IMessageBus
    {
        private readonly Dictionary<Type, List<object>> _handlers = new();
        public readonly List<WalletState> States = new();
        public readonly List<CoinsEarned> Earned = new();
        public readonly List<SpendResult> Results = new();
        public readonly List<SavePart> Parts = new();
        public readonly List<Type> Subscribed = new();

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct
        {
            Subscribed.Add(typeof(T));
            if (!_handlers.TryGetValue(typeof(T), out var list))
                _handlers[typeof(T)] = list = new List<object>();
            list.Add(handler);
        }

        public void Publish<T>(in T message) where T : struct
        {
            if (message is WalletState state)
                States.Add(state);
            if (message is CoinsEarned earned)
                Earned.Add(earned);
            if (message is SpendResult result)
                Results.Add(result);
            if (message is SavePart part)
                Parts.Add(part);
            if (_handlers.TryGetValue(typeof(T), out var list))
                foreach (var handler in list)
                    ((IMessageHandler<T>)handler).Handle(message);
        }
    }

    private static readonly TickContext Step = new(0, 1.0 / 60);

    private static WalletModule Started(FakeBus bus)
    {
        var wallet = new WalletModule();
        wallet.Start(bus);
        return wallet;
    }

    private static MonsterKilled Kill(int id, MonsterKind kind, int source, Vector3 position = default) =>
        new(id, kind, MonsterRewards.For(kind), position, source);

    [Fact]
    public void StartsEmptyAndPublishesItsStateEveryTick()
    {
        var bus = new FakeBus();
        var wallet = Started(bus);
        for (int i = 0; i < 3; i++)
            wallet.Tick(Step);
        Assert.Equal(0, wallet.Coins);
        Assert.Equal(3, bus.States.Count);
        Assert.All(bus.States, s => Assert.Equal(0, s.Coins));
        Assert.Empty(bus.Earned);
    }

    [Fact]
    public void RewardsAddUp()
    {
        var bus = new FakeBus();
        var wallet = Started(bus);
        bus.Publish(Kill(1, MonsterKind.Caterpillar, Player));
        bus.Publish(Kill(2, MonsterKind.Ant, Player));
        wallet.Tick(Step);
        Assert.Equal(100, wallet.Coins);
        Assert.Equal(100, bus.States[^1].Coins);

        bus.Publish(Kill(3, MonsterKind.Tarantula, Player));
        bus.Publish(Kill(4, MonsterKind.Ladybug, Player));
        wallet.Tick(Step);
        Assert.Equal(100 + 200 + 200, wallet.Coins);
        Assert.Equal(500, bus.States[^1].Coins);
    }

    [Fact]
    public void AddsTheRewardTheKillCarries()
    {
        var bus = new FakeBus();
        var wallet = Started(bus);
        bus.Publish(new MonsterKilled(1, MonsterKind.Ant, 75, Vector3.Zero, Player));
        Assert.Equal(75, wallet.Coins);
    }

    [Fact]
    public void KillsByThePlayerAndByThePetBothCount()
    {
        var bus = new FakeBus();
        var wallet = Started(bus);
        bus.Publish(Kill(1, MonsterKind.Caterpillar, Player));
        Assert.Equal(50, wallet.Coins);
        bus.Publish(Kill(2, MonsterKind.Ant, Pet));
        Assert.Equal(100, wallet.Coins);
        bus.Publish(Kill(3, MonsterKind.Ladybug, Pet));
        Assert.Equal(300, wallet.Coins);
        Assert.Equal(new[] { 50, 50, 200 }, bus.Earned.ConvertAll(e => e.Amount));
    }

    [Fact]
    public void PublishesCoinsEarnedOncePerRewardAtTheKillPosition()
    {
        var bus = new FakeBus();
        var wallet = Started(bus);
        var where = new Vector3(3f, 0.5f, -7f);
        bus.Publish(Kill(1, MonsterKind.Ant, Pet, where));
        wallet.Tick(Step);
        wallet.Tick(Step);
        var earned = Assert.Single(bus.Earned);
        Assert.Equal(50, earned.Amount);
        Assert.Equal(where, earned.Position);
        Assert.Equal(1, wallet.EarnedCount);
        Assert.Equal(where, wallet.Recent[0].Position);
    }

    [Fact]
    public void NothingIsLostOnDefeat()
    {
        var bus = new FakeBus();
        var wallet = Started(bus);
        bus.Publish(Kill(1, MonsterKind.Caterpillar, Player));
        bus.Publish(Kill(2, MonsterKind.Tarantula, Pet));
        wallet.Tick(Step);
        Assert.Equal(250, wallet.Coins);

        bus.Publish(new PlayerDefeated(new Vector3(1f, 0f, 2f)));
        for (int i = 0; i < 600; i++)
            wallet.Tick(Step);
        Assert.Equal(250, wallet.Coins);
        Assert.All(bus.States, s => Assert.Equal(250, s.Coins));
        Assert.DoesNotContain(typeof(PlayerDefeated), bus.Subscribed);

        // Coins earned after coming back add to the ones kept.
        bus.Publish(Kill(3, MonsterKind.Ant, Player));
        Assert.Equal(300, wallet.Coins);
    }

    [Fact]
    public void ARewardOfNothingAddsNothingAndTakesNothing()
    {
        var bus = new FakeBus();
        var wallet = Started(bus);
        bus.Publish(Kill(1, MonsterKind.Ant, Player));
        bus.Publish(new MonsterKilled(2, MonsterKind.Ant, 0, Vector3.Zero, Player));
        bus.Publish(new MonsterKilled(3, MonsterKind.Ant, -50, Vector3.Zero, Pet));
        Assert.Equal(50, wallet.Coins);
        Assert.Single(bus.Earned);
    }

    [Fact]
    public void AFullWalletStaysFull()
    {
        var bus = new FakeBus();
        var wallet = Started(bus);
        bus.Publish(new MonsterKilled(1, MonsterKind.Ant, int.MaxValue - 10, Vector3.Zero, Player));
        bus.Publish(Kill(2, MonsterKind.Ladybug, Player));
        Assert.Equal(int.MaxValue, wallet.Coins);
    }

    [Fact]
    public void RecentKeepsTheLatestRewardsInARing()
    {
        var bus = new FakeBus();
        var wallet = Started(bus);
        int n = WalletModule.RecentCapacity + 3;
        for (int i = 0; i < n; i++)
            bus.Publish(Kill(i, MonsterKind.Ant, Player, new Vector3(i, 0f, 0f)));
        Assert.Equal(n, wallet.EarnedCount);
        for (int i = n - WalletModule.RecentCapacity; i < n; i++)
            Assert.Equal(i, wallet.Recent[i % WalletModule.RecentCapacity].Position.X);
    }

    private static PlayerState PlayerAt(Vector3 position) =>
        new(position, 0f, Vector3.Zero, true, 10, 10, true, CharacterKind.Girl);

    [Fact]
    public void SpendingTakesTheCoinsWhenThereAreEnough()
    {
        var bus = new FakeBus();
        var wallet = Started(bus);
        bus.Publish(new GrantCoins(5000, "test"));
        bus.Publish(new SpendCoins(7, 3000, "Mushroom Crow"));
        var result = Assert.Single(bus.Results);
        Assert.Equal(7, result.RequestId);
        Assert.True(result.Ok);
        Assert.Equal(2000, result.CoinsLeft);
        Assert.Equal(2000, wallet.Coins);
        wallet.Tick(Step);
        Assert.Equal(2000, bus.States[^1].Coins);
        // Spending is not earning: no "+N" pop-up for it.
        Assert.Single(bus.Earned);
    }

    [Fact]
    public void SpendingEverythingIsAllowed()
    {
        var bus = new FakeBus();
        var wallet = Started(bus);
        bus.Publish(new GrantCoins(3000, "test"));
        bus.Publish(new SpendCoins(1, 3000, "Mushroom Crow"));
        var result = Assert.Single(bus.Results);
        Assert.True(result.Ok);
        Assert.Equal(0, result.CoinsLeft);
        Assert.Equal(0, wallet.Coins);
    }

    [Fact]
    public void SpendingMoreThanThereIsChangesNothing()
    {
        var bus = new FakeBus();
        var wallet = Started(bus);
        bus.Publish(Kill(1, MonsterKind.Tarantula, Player));
        bus.Publish(new SpendCoins(42, 3000, "Mushroom Crow"));
        var result = Assert.Single(bus.Results);
        Assert.Equal(42, result.RequestId);
        Assert.False(result.Ok);
        Assert.Equal(200, result.CoinsLeft);
        Assert.Equal(200, wallet.Coins);
        wallet.Tick(Step);
        Assert.Equal(200, bus.States[^1].Coins);
    }

    [Fact]
    public void ANegativeSpendIsRefused()
    {
        var bus = new FakeBus();
        var wallet = Started(bus);
        bus.Publish(new GrantCoins(100, "test"));
        bus.Publish(new SpendCoins(3, -500, "trick"));
        var result = Assert.Single(bus.Results);
        Assert.False(result.Ok);
        Assert.Equal(100, result.CoinsLeft);
        Assert.Equal(100, wallet.Coins);
    }

    [Fact]
    public void EachSpendIsAnsweredOnceInOrder()
    {
        var bus = new FakeBus();
        var wallet = Started(bus);
        bus.Publish(new GrantCoins(500, "test"));
        bus.Publish(new SpendCoins(1, 300, "chair"));
        bus.Publish(new SpendCoins(2, 300, "table"));
        bus.Publish(new SpendCoins(3, 200, "lamp"));
        Assert.Equal(new[] { 1, 2, 3 }, bus.Results.ConvertAll(r => r.RequestId));
        Assert.Equal(new[] { true, false, true }, bus.Results.ConvertAll(r => r.Ok));
        Assert.Equal(new[] { 200, 200, 0 }, bus.Results.ConvertAll(r => r.CoinsLeft));
        Assert.Equal(0, wallet.Coins);
    }

    [Fact]
    public void GrantingAddsCoinsAndPopsThemUpAtThePlayer()
    {
        var bus = new FakeBus();
        var wallet = Started(bus);
        bus.Publish(Kill(1, MonsterKind.Ant, Player));
        var where = new Vector3(12f, 0.25f, -30f);
        bus.Publish(PlayerAt(new Vector3(1f, 0f, 1f)));
        bus.Publish(PlayerAt(where));
        bus.Publish(new GrantCoins(500, "Millie's quest"));
        Assert.Equal(550, wallet.Coins);
        wallet.Tick(Step);
        Assert.Equal(550, bus.States[^1].Coins);
        Assert.Equal(2, bus.Earned.Count);
        Assert.Equal(500, bus.Earned[1].Amount);
        Assert.Equal(where, bus.Earned[1].Position);
        Assert.Equal(2, wallet.EarnedCount);
        Assert.Equal(where, wallet.Recent[1].Position);
        Assert.Empty(bus.Results);
    }

    [Fact]
    public void GrantingNothingAddsNothing()
    {
        var bus = new FakeBus();
        var wallet = Started(bus);
        bus.Publish(new GrantCoins(0, "nothing"));
        bus.Publish(new GrantCoins(-100, "negative"));
        Assert.Equal(0, wallet.Coins);
        Assert.Empty(bus.Earned);
    }

    [Fact]
    public void GrantingStopsAtAFullWallet()
    {
        var bus = new FakeBus();
        var wallet = Started(bus);
        bus.Publish(new GrantCoins(int.MaxValue - 10, "lots"));
        bus.Publish(new GrantCoins(500, "more"));
        Assert.Equal(int.MaxValue, wallet.Coins);
    }

    [Fact]
    public void TickBeforeStartThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new WalletModule().Tick(Step));
    }

    /// <summary>A started wallet holding <paramref name="coins"/>, earned through a grant.</summary>
    private static WalletModule StartedWith(FakeBus bus, int coins)
    {
        var wallet = Started(bus);
        bus.Publish(new GrantCoins(coins, "test"));
        Assert.Equal(coins, wallet.Coins);
        return wallet;
    }

    [Fact]
    public void AnswersCollectSaveWithOneCompactVersionedWalletPart()
    {
        var bus = new FakeBus();
        StartedWith(bus, 1234);
        bus.Publish(new CollectSave(7));
        var part = Assert.Single(bus.Parts);
        Assert.Equal(7, part.RequestId);
        Assert.Equal("Wallet", part.Module);
        Assert.DoesNotContain(" ", part.Json);
        Assert.DoesNotContain("\n", part.Json);
        using var json = System.Text.Json.JsonDocument.Parse(part.Json);
        Assert.Equal(WalletModule.SaveVersion, json.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(1234, json.RootElement.GetProperty("coins").GetInt32());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1234)]
    [InlineData(int.MaxValue)]
    public void SaveThenRestoreGivesBackTheSameWalletQuietly(int coins)
    {
        var savedBus = new FakeBus();
        var saved = StartedWith(savedBus, coins);
        savedBus.Publish(new CollectSave(1));
        var part = Assert.Single(savedBus.Parts);

        // A fresh wallet (a new session) and one that already holds other coins both end up equal.
        foreach (int before in new[] { 0, 99 })
        {
            var bus = new FakeBus();
            var wallet = before == 0 ? Started(bus) : StartedWith(bus, before);
            var logs = new List<string>();
            wallet.Log = logs.Add;
            int earnedBefore = bus.Earned.Count;
            long countBefore = wallet.EarnedCount;
            bus.Publish(new RestorePart(part.Module, part.Json));
            Assert.Equal(saved.Coins, wallet.Coins);
            Assert.Equal(earnedBefore, bus.Earned.Count);
            Assert.Equal(countBefore, wallet.EarnedCount);
            Assert.Empty(bus.Results);
            Assert.Empty(logs);
            wallet.Tick(Step);
            Assert.Equal(coins, bus.States[^1].Coins);
            bus.Publish(new CollectSave(2));
            Assert.Equal(part.Json, bus.Parts[^1].Json);
        }
    }

    [Fact]
    public void RestoringAnotherModulesPartChangesNothing()
    {
        var bus = new FakeBus();
        var wallet = StartedWith(bus, 300);
        wallet.Log = _ => Assert.Fail("nothing to log");
        bus.Publish(new RestorePart("Pet", "{\"version\":1,\"coins\":5}"));
        bus.Publish(new RestorePart("Pet", "not json"));
        Assert.Equal(300, wallet.Coins);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"version\":1,\"coins\":")]
    [InlineData("[1,2]")]
    [InlineData("null")]
    [InlineData("{\"coins\":500}")]
    [InlineData("{\"version\":2,\"coins\":500}")]
    [InlineData("{\"version\":1,\"coins\":-5}")]
    [InlineData("{\"version\":1,\"coins\":\"lots\"}")]
    [InlineData("{\"version\":1,\"coins\":99999999999}")]
    public void BadOrUnknownJsonKeepsTheDefaultsAndLogsOnce(string json)
    {
        var bus = new FakeBus();
        var wallet = StartedWith(bus, 300);
        var logs = new List<string>();
        wallet.Log = logs.Add;
        int earnedBefore = bus.Earned.Count;
        bus.Publish(new RestorePart("Wallet", json));
        bus.Publish(new RestorePart("Wallet", json));
        Assert.Equal(new WalletModule().Coins, wallet.Coins);
        Assert.Equal(0, wallet.Coins);
        Assert.Equal(earnedBefore, bus.Earned.Count);
        Assert.Single(logs);
    }

    [Fact]
    public void ANullJsonKeepsTheDefaults()
    {
        var bus = new FakeBus();
        var wallet = Started(bus);
        var logs = new List<string>();
        wallet.Log = logs.Add;
        bus.Publish(new RestorePart("Wallet", null!));
        Assert.Equal(0, wallet.Coins);
        Assert.Single(logs);
    }

    /// <summary>The materials of coin.glb (the exported Mushroom Coin), read from its glTF JSON chunk.</summary>
    private static List<string> CoinMaterials()
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "modules/Wallet/Models/coin.glb")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var glb = System.IO.File.ReadAllBytes(System.IO.Path.Combine(dir!.FullName, "modules/Wallet/Models/coin.glb"));
        Assert.Equal("glTF", System.Text.Encoding.ASCII.GetString(glb, 0, 4));
        int length = BitConverter.ToInt32(glb, 12);
        Assert.Equal("JSON", System.Text.Encoding.ASCII.GetString(glb, 16, 4));
        using var json = System.Text.Json.JsonDocument.Parse(new ReadOnlyMemory<byte>(glb, 20, length));
        var names = new List<string>();
        foreach (var material in json.RootElement.GetProperty("materials").EnumerateArray())
            names.Add(material.GetProperty("name").GetString()!);
        return names;
    }

    [Fact]
    public void TheCoinIsAGoldCoinWithASpottedMushroomAndNoEyes()
    {
        var materials = CoinMaterials();
        Assert.Equal(new[] { "Gold", "GoldRim", "Cap", "Spots", "Stem" }, materials);
        Assert.DoesNotContain(materials, m => m.Contains("Eye", StringComparison.OrdinalIgnoreCase));
    }
}
