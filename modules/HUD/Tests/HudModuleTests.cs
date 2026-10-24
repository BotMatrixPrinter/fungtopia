using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Bow;
using Fungtopia.Player;
using Fungtopia.Shell;
using Fungtopia.Wallet;
using Xunit;

namespace Fungtopia.HUD.Tests;

public class HudModuleTests
{
    private const double Step = 1.0 / 60;

    /// <summary>Fake of the shell's bus: keeps the handlers and delivers what the tests publish.</summary>
    private sealed class FakeBus : IMessageBus
    {
        private readonly Dictionary<Type, List<object>> _handlers = new();
        public int Published;

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
                _handlers[typeof(T)] = list = new List<object>();
            list.Add(handler);
        }

        public void Publish<T>(in T message) where T : struct
        {
            Published++;
            if (_handlers.TryGetValue(typeof(T), out var list))
                foreach (var handler in list)
                    ((IMessageHandler<T>)handler).Handle(message);
        }

        public bool Has<T>() where T : struct => _handlers.ContainsKey(typeof(T));
    }

    private readonly FakeBus _bus = new();
    private readonly HudModule _hud = new();
    private long _tick;

    public HudModuleTests() => _hud.Start(_bus);

    private static PlayerState Player(int health, int maxHealth = 10, bool alive = true) =>
        new(Vector3.Zero, 0f, Vector3.Zero, true, health, maxHealth, alive, CharacterKind.Girl);

    private void Send<T>(T message) where T : struct => _bus.Publish(message);

    private void Tick(int count = 1)
    {
        for (int i = 0; i < count; i++)
            _hud.Tick(new TickContext(_tick++, Step));
    }

    private static int Ticks(float seconds) => (int)Math.Ceiling(seconds * 60) + 1;

    [Fact]
    public void ListensToPlayerWalletAndBowAndPublishesNothing()
    {
        Assert.True(_bus.Has<PlayerState>());
        Assert.True(_bus.Has<PlayerDefeated>());
        Assert.True(_bus.Has<WalletState>());
        Assert.True(_bus.Has<CoinsEarned>());
        Assert.True(_bus.Has<BowState>());
        Tick(10);
        Assert.Equal(0, _bus.Published);
    }

    [Fact]
    public void TickBeforeStartThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new HudModule().Tick(new TickContext(0, Step)));
    }

    // Hearts

    [Fact]
    public void FiveHeartsForTenHealthEachHoldingTwo()
    {
        Send(Player(10));
        Tick();
        Assert.Equal(10, _hud.Health);
        Assert.Equal(10, _hud.MaxHealth);
        Assert.Equal(5, _hud.HeartCount);
        for (int i = 0; i < 5; i++)
            Assert.Equal(1f, _hud.HeartFill(i));
    }

    [Theory]
    [InlineData(7, new[] { 1f, 1f, 1f, 0.5f, 0f })]
    [InlineData(4, new[] { 1f, 1f, 0f, 0f, 0f })]
    [InlineData(1, new[] { 0.5f, 0f, 0f, 0f, 0f })]
    [InlineData(0, new[] { 0f, 0f, 0f, 0f, 0f })]
    public void HeartsFillFromTheLeftInHalves(int health, float[] fills)
    {
        Send(Player(health));
        Tick();
        Assert.Equal(fills.Length, _hud.HeartCount);
        for (int i = 0; i < fills.Length; i++)
            Assert.Equal(fills[i], _hud.HeartFill(i));
    }

    [Fact]
    public void OddMaxHealthRoundsUpToAWholeHeart()
    {
        Send(Player(9, 9));
        Tick();
        Assert.Equal(5, _hud.HeartCount);
        Assert.Equal(0.5f, _hud.HeartFill(4));
        Assert.Equal(0f, _hud.HeartFill(5));
        Assert.Equal(0f, _hud.HeartFill(-1));
    }

    [Fact]
    public void NoHeartsBeforeThePlayerIsSeen()
    {
        Tick();
        Assert.False(_hud.HasPlayer);
        Assert.Equal(0, _hud.HeartCount);
    }

    [Fact]
    public void HeartsShakeOnAHitAndSettle()
    {
        Send(Player(10));
        Tick();
        Assert.False(_hud.HeartsShaking);
        Assert.Equal(0f, _hud.HeartsShake(0f));

        Send(Player(8));
        Tick(3);
        Assert.True(_hud.HeartsShaking);
        Assert.NotEqual(0f, _hud.HeartsShake(0f));
        Assert.InRange(MathF.Abs(_hud.HeartsShake(0f)), 0f, HudModule.ShakePixels);

        Send(Player(8));
        Tick(Ticks(HudModule.ShakeSeconds));
        Assert.False(_hud.HeartsShaking);
        Assert.Equal(0f, _hud.HeartsShake(0.5f));
    }

    [Fact]
    public void HealingAndTheFirstStateDoNotShake()
    {
        Send(Player(3));
        Tick();
        Assert.False(_hud.HeartsShaking);
        Send(Player(10));
        Tick();
        Assert.False(_hud.HeartsShaking);
    }

    // Mushroom Coins

    [Theory]
    [InlineData(0, "M¢ 0")]
    [InlineData(50, "M¢ 50")]
    [InlineData(1250, "M¢ 1,250")]
    [InlineData(1234567, "M¢ 1,234,567")]
    public void CoinsTextHasTheSignAndThousandsCommas(int coins, string text)
    {
        Assert.Equal(text, HudText.Coins(coins));
    }

    [Fact]
    public void FloatingTextIsAPlusAndTheAmount()
    {
        Assert.Equal("+50", HudText.Earned(50));
        Assert.Equal("+1,200", HudText.Earned(1200));
    }

    [Fact]
    public void ShowsTheWalletsCoins()
    {
        Send(new WalletState(1250));
        Tick();
        Assert.Equal(1250, _hud.Coins);
        Assert.Equal("M¢ 1,250", HudText.Coins(_hud.Coins));
    }

    [Fact]
    public void CoinsPopWhenEarnedThenSettle()
    {
        Send(new WalletState(0));
        Tick();
        Assert.Equal(1f, _hud.CoinsPopScale(0f));

        Send(new CoinsEarned(50, new Vector3(1f, 0f, 2f)));
        Send(new WalletState(50));
        Tick(Ticks(HudModule.PopSeconds / 2) - 1);
        Assert.True(_hud.CoinsPopScale(0f) > 1.2f);
        Assert.True(_hud.CoinsPopScale(0f) <= 1f + HudModule.PopGrowth);

        Tick(Ticks(HudModule.PopSeconds));
        Assert.Equal(1f, _hud.CoinsPopScale(0f));
    }

    [Fact]
    public void EarningFloatsAPlusFiftyThatRisesFadesAndGoes()
    {
        Send(new CoinsEarned(50, Vector3.Zero));
        Tick();
        Assert.Equal(1, _hud.EarnedCount);
        var floater = _hud.Floater(0);
        Assert.True(floater.Active);
        Assert.Equal(50, floater.Amount);
        Assert.Equal("+50", HudText.Earned(floater.Amount));
        float firstRise = floater.Rise(0f);
        float firstAlpha = floater.Alpha(0f);

        Tick(Ticks(HudModule.FloaterSeconds * 0.75f));
        floater = _hud.Floater(0);
        Assert.True(floater.Active);
        Assert.True(floater.Rise(0f) > firstRise);
        Assert.True(floater.Alpha(0f) < firstAlpha);
        Assert.True(floater.Alpha(0f) > 0f);

        Tick(Ticks(HudModule.FloaterSeconds));
        Assert.False(_hud.Floater(0).Active);
    }

    [Fact]
    public void KeepsTheLatestFloatersInARing()
    {
        for (int i = 1; i <= HudModule.FloaterCapacity + 2; i++)
        {
            Send(new CoinsEarned(i * 10, Vector3.Zero));
            Tick();
        }
        Assert.Equal(HudModule.FloaterCapacity + 2, _hud.EarnedCount);
        var amounts = new List<int>();
        for (int i = 0; i < HudModule.FloaterCapacity; i++)
            amounts.Add(_hud.Floater(i).Amount);
        amounts.Sort();
        Assert.Equal(new[] { 30, 40, 50, 60 }, amounts);
    }

    [Fact]
    public void IgnoresNonPositiveEarnings()
    {
        Send(new CoinsEarned(0, Vector3.Zero));
        Tick();
        Assert.Equal(0, _hud.EarnedCount);
        Assert.Equal(1f, _hud.CoinsPopScale(0f));
    }

    // Bow draw ring

    [Fact]
    public void DrawRingFollowsTheBowsDraw()
    {
        Tick();
        Assert.Equal(0f, _hud.DrawRing(1f));
        Assert.False(_hud.DrawRingVisible);

        Send(new BowState(0.5f, 0));
        Tick();
        Assert.Equal(0.5f, _hud.DrawRing(1f), 5);
        Assert.Equal(0.25f, _hud.DrawRing(0.5f), 5);
        Assert.True(_hud.DrawRingVisible);
        Assert.False(_hud.DrawFull);

        Send(new BowState(1f, 1));
        Tick();
        Assert.Equal(1f, _hud.DrawRing(1f), 5);
        Assert.True(_hud.DrawFull);
    }

    [Fact]
    public void DrawRingIsClampedToZeroToOne()
    {
        Send(new BowState(3f, 0));
        Tick();
        Assert.Equal(1f, _hud.DrawRing(1f));
        Send(new BowState(-2f, 0));
        Tick();
        Assert.Equal(0f, _hud.DrawRing(1f));
        Send(new BowState(float.NaN, 0));
        Tick();
        Assert.Equal(0f, _hud.DrawRing(1f));
    }

    // Fainting

    [Fact]
    public void FaintingFadesInSoftlyWithItsText()
    {
        Send(Player(10));
        Tick();
        Assert.Equal(0f, _hud.FaintFade(1f));
        Assert.False(_hud.Fainted);

        Send(Player(0, alive: false));
        Send(new PlayerDefeated(Vector3.Zero));
        Tick();
        Assert.True(_hud.Fainted);
        Assert.Equal("You fainted...", HudText.Fainted);
        float early = _hud.FaintFade(1f);
        Assert.InRange(early, 0.001f, 0.2f);

        Tick(Ticks(HudModule.FadeSeconds / 2));
        float middle = _hud.FaintFade(1f);
        Assert.True(middle > early && middle < HudModule.FadeMaxAlpha);

        Tick(Ticks(HudModule.FadeSeconds));
        Assert.Equal(HudModule.FadeMaxAlpha, _hud.FaintFade(1f), 5);
        Assert.Equal(1f, _hud.FaintTextAlpha(1f), 5);
    }

    [Fact]
    public void FadesOutWhenThePlayerComesBack()
    {
        Send(new PlayerDefeated(Vector3.Zero));
        Send(Player(0, alive: false));
        Tick(Ticks(HudModule.FadeSeconds));
        Assert.Equal(HudModule.FadeMaxAlpha, _hud.FaintFade(1f), 5);

        Send(Player(10));
        Tick();
        Assert.False(_hud.Fainted);
        Assert.True(_hud.FaintFade(1f) < HudModule.FadeMaxAlpha);
        Tick(Ticks(HudModule.FadeSeconds));
        Assert.Equal(0f, _hud.FaintFade(1f));
        Assert.Equal(0f, _hud.FaintTextAlpha(1f));
    }

    [Fact]
    public void AStillAlivePlayerStateDoesNotUndoTheFaintOfTheSameTick()
    {
        // PlayerDefeated comes a tick after health reached 0, while Alive is already false.
        Send(Player(0, alive: false));
        Send(new PlayerDefeated(Vector3.Zero));
        Tick();
        Send(Player(0, alive: false));
        Tick();
        Assert.True(_hud.Fainted);
    }

    // Interpolation, determinism, allocations

    [Fact]
    public void ViewValuesInterpolateBetweenTicks()
    {
        Send(new BowState(0.2f, 0));
        Tick();
        Send(new BowState(0.6f, 0));
        Tick();
        Assert.Equal(0.2f, _hud.DrawRing(0f), 5);
        Assert.Equal(0.4f, _hud.DrawRing(0.5f), 5);
        Assert.Equal(0.6f, _hud.DrawRing(1f), 5);
    }

    [Fact]
    public void SameMessagesGiveTheSameHud()
    {
        static float[] Run()
        {
            var bus = new FakeBus();
            var hud = new HudModule();
            hud.Start(bus);
            var values = new List<float>();
            for (int t = 0; t < 120; t++)
            {
                bus.Publish(Player(10 - t / 20));
                bus.Publish(new BowState(t % 40 / 40f, 0));
                if (t % 30 == 0)
                    bus.Publish(new CoinsEarned(50, Vector3.Zero));
                hud.Tick(new TickContext(t, Step));
                values.Add(hud.HeartsShake(0.3f));
                values.Add(hud.CoinsPopScale(0.3f));
                values.Add(hud.DrawRing(0.3f));
                values.Add(hud.Floater(0).Rise(0.3f));
            }
            return values.ToArray();
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void TicksAndHandlersDoNotAllocate()
    {
        var hit = Player(9);
        var full = Player(10);
        var wallet = new WalletState(100);
        var earned = new CoinsEarned(50, Vector3.Zero);
        var bow = new BowState(0.5f, 1);
        var defeated = new PlayerDefeated(Vector3.Zero);
        var context = new TickContext(0, Step);
        // Warm up (JIT) before measuring.
        for (int i = 0; i < 3; i++)
        {
            _hud.Handle(full); _hud.Handle(hit); _hud.Handle(wallet); _hud.Handle(earned);
            _hud.Handle(bow); _hud.Handle(defeated); _hud.Tick(context);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 200; i++)
        {
            _hud.Handle(full); _hud.Handle(hit); _hud.Handle(wallet); _hud.Handle(earned);
            _hud.Handle(bow); _hud.Handle(defeated); _hud.Tick(context);
            _hud.HeartsShake(0.5f); _hud.CoinsPopScale(0.5f); _hud.FaintFade(0.5f);
        }
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }
}
