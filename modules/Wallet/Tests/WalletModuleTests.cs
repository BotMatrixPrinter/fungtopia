using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Monsters;
using Fungtopia.Player;
using Fungtopia.Shell;
using Xunit;

namespace Fungtopia.Wallet.Tests;

public class WalletModuleTests
{
    private const int Player = 0;
    private const int Pet = 1;

    /// <summary>
    /// Fake of the shell's bus: delivers messages to subscribers like the real one, and records
    /// every WalletState and CoinsEarned published.
    /// </summary>
    private sealed class FakeBus : IMessageBus
    {
        private readonly Dictionary<Type, List<object>> _handlers = new();
        public readonly List<WalletState> States = new();
        public readonly List<CoinsEarned> Earned = new();
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
        Assert.Equal(new[] { typeof(MonsterKilled) }, bus.Subscribed);

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

    [Fact]
    public void TickBeforeStartThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new WalletModule().Tick(Step));
    }
}
