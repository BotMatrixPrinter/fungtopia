using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Bow;
using Fungtopia.DayNight;
using Fungtopia.Map;
using Fungtopia.NPCs;
using Fungtopia.Player;
using Fungtopia.Shell;
using Fungtopia.StartScreen;
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

    private static PlayerState In(Area area, bool alive = true) =>
        new(Vector3.Zero, 0f, Vector3.Zero, true, 10, 10, alive, CharacterKind.Girl, area);

    private static QuestState Quest(int progress, int goal = 5, bool complete = false, string title = "Defeat 5 Mushroom Caterpillars") =>
        new(true, title, progress, goal, complete);

    private void Send<T>(T message) where T : struct => _bus.Publish(message);

    private void Tick(int count = 1)
    {
        for (int i = 0; i < count; i++)
            _hud.Tick(new TickContext(_tick++, Step));
    }

    private static int Ticks(float seconds) => (int)Math.Ceiling(seconds * 60) + 1;

    [Fact]
    public void ListensToPlayerWalletBowClockAndQuestAndPublishesNothing()
    {
        Assert.True(_bus.Has<TimeOfDay>());
        Assert.True(_bus.Has<WokeUp>());
        Assert.True(_bus.Has<QuestState>());
        Assert.True(_bus.Has<PlayerState>());
        Assert.True(_bus.Has<PlayerDefeated>());
        Assert.True(_bus.Has<WalletState>());
        Assert.True(_bus.Has<CoinsEarned>());
        Assert.True(_bus.Has<BowState>());
        Assert.True(_bus.Has<StartScreenState>());
        Tick(10);
        Assert.Equal(0, _bus.Published);
    }

    [Fact]
    public void ScreenshotTeleportPublishesOnceAndCanShowUnderTheStartScreen()
    {
        var teleports = new List<TeleportPlayer>();
        _bus.Subscribe<TeleportPlayer>(new Collect<TeleportPlayer>(teleports));
        _hud.TeleportPlayerOnce(new Vector3(0f, 0f, -200f), 1f);
        Tick(3);
        var t = Assert.Single(teleports);
        Assert.Equal(-200f, t.Position.Z);
        Assert.Equal(1f, t.Facing);
        _hud.ShowUnderStartScreen = true;
        Send(new StartScreenState(true));
        Assert.False(_hud.Hidden);
    }

    private sealed class Collect<T> : IMessageHandler<T> where T : struct
    {
        private readonly List<T> _into;
        public Collect(List<T> into) => _into = into;
        public void Handle(in T message) => _into.Add(message);
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

        Send(new BowState(0.5f, 0));
        Tick();
        Assert.Equal(0.5f, _hud.DrawRing(1f), 5);
        Assert.Equal(0.25f, _hud.DrawRing(0.5f), 5);
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

    // Crosshair visibility

    [Fact]
    public void CrosshairIsHiddenWhileTheBowIsNotInUse()
    {
        Assert.False(_hud.CrosshairVisible);
        // Walking around, talking, menus: the bow stays relaxed.
        for (int i = 0; i < 300; i++)
        {
            Send(Player(10));
            Send(new BowState(0f, 0));
            Tick();
            Assert.False(_hud.CrosshairVisible);
            Assert.Equal(0f, _hud.CrosshairAlpha(0f));
            Assert.Equal(0f, _hud.CrosshairAlpha(1f));
        }
    }

    [Fact]
    public void CrosshairFadesInQuicklyOnceTheBowIsDrawn()
    {
        Send(new BowState(0.05f, 0));
        Tick();
        Assert.True(_hud.CrosshairVisible);
        Assert.Equal(0f, _hud.CrosshairAlpha(0f), 5);
        float first = _hud.CrosshairAlpha(1f);
        Assert.InRange(first, 0.1f, 0.25f); // one tick of a 0.1 s fade
        Assert.Equal(first * 0.5f, _hud.CrosshairAlpha(0.5f), 5);

        Tick(2); // 0.05 s in: still fading
        Assert.InRange(_hud.CrosshairAlpha(1f), 0.3f, 0.7f);

        Tick(Ticks(HudModule.CrosshairFadeSeconds));
        Assert.Equal(1f, _hud.CrosshairAlpha(0f));
        Assert.Equal(1f, _hud.CrosshairAlpha(1f));
    }

    [Fact]
    public void CrosshairStaysWhileDrawingAndLingersHalfASecondAfterTheShot()
    {
        for (int i = 0; i < 90; i++) // 1.5 s of drawing up to full
        {
            Send(new BowState(Math.Min(1f, i / 40f + 0.01f), 0));
            Tick();
        }
        Assert.Equal(1f, _hud.CrosshairAlpha(1f));

        Send(new BowState(0f, 1)); // the shot: the draw is back to 0
        for (int i = 0; i < 27; i++) // 0.45 s later it still shows, fully
        {
            Tick();
            Assert.Equal(1f, _hud.CrosshairAlpha(1f));
            Assert.True(_hud.CrosshairVisible);
        }

        Tick(6); // 0.55 s: fading out
        Assert.True(_hud.CrosshairVisible);
        Assert.InRange(_hud.CrosshairAlpha(1f), 0.05f, 0.95f);

        Tick(Ticks(HudModule.CrosshairFadeSeconds)); // gone by about 0.65 s
        Assert.False(_hud.CrosshairVisible);
        Assert.Equal(0f, _hud.CrosshairAlpha(0f));
        Assert.Equal(0f, _hud.CrosshairAlpha(1f));

        Tick(120);
        Assert.False(_hud.CrosshairVisible);
    }

    [Fact]
    public void DrawingAgainWhileLingeringKeepsTheCrosshair()
    {
        Send(new BowState(0.8f, 0));
        Tick(Ticks(HudModule.CrosshairFadeSeconds));
        Send(new BowState(0f, 1));
        Tick(20); // a third of a second after the shot
        Send(new BowState(0.3f, 1)); // the next draw
        Tick(60);
        Assert.Equal(1f, _hud.CrosshairAlpha(1f));
        Send(new BowState(0f, 2));
        Tick(27);
        Assert.Equal(1f, _hud.CrosshairAlpha(1f)); // the linger starts again from this shot
        Tick(Ticks(HudModule.CrosshairFadeSeconds) + 6);
        Assert.False(_hud.CrosshairVisible);
    }

    // Fainting

    [Fact]
    public void FaintingFadesToBlackWithItsText()
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

        Tick(Ticks(HudModule.FadeInSeconds / 2));
        float middle = _hud.FaintFade(1f);
        Assert.True(middle > early && middle < HudModule.FadeMaxAlpha);

        Tick(Ticks(HudModule.FadeInSeconds));
        Assert.Equal(1f, HudModule.FadeMaxAlpha); // fully black
        Assert.Equal(HudModule.FadeMaxAlpha, _hud.FaintFade(1f), 5);
        Assert.Equal(1f, _hud.FaintTextAlpha(1f), 5);
    }

    [Fact]
    public void FadesOutWhenThePlayerComesBack()
    {
        Send(new PlayerDefeated(Vector3.Zero));
        Send(Player(0, alive: false));
        Tick(Ticks(HudModule.FadeInSeconds));
        Assert.Equal(HudModule.FadeMaxAlpha, _hud.FaintFade(1f), 5);

        Send(Player(10));
        Tick();
        Assert.False(_hud.Fainted);
        Assert.True(_hud.FaintFade(1f) < HudModule.FadeMaxAlpha);
        Tick(Ticks(HudModule.FadeOutSeconds / 2));
        Assert.True(_hud.FaintFade(1f) > 0f); // the screen comes back slowly
        Tick(Ticks(HudModule.FadeOutSeconds));
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

    [Fact]
    public void WakeUpTextReplacesTheFaintAsTheScreenComesBack()
    {
        Assert.Equal("Day 4 - you wake up at Dr. Jeremy's", HudText.WokeUp(4));
        Assert.False(_hud.Waking);
        Assert.Equal(0f, _hud.WakeTextAlpha(1f));

        // Defeat: black with "You fainted...".
        Send(Player(0, alive: false));
        Send(new PlayerDefeated(Vector3.Zero));
        Tick(Ticks(3f));
        Assert.Equal(1f, _hud.FaintFade(1f), 5);
        Assert.Equal(1f, _hud.FaintTextAlpha(1f), 5);

        // DayNight wakes the player at the clinic: the wake-up text takes over, still in the dark.
        Send(new WokeUp(4));
        Tick();
        Assert.Equal(4, _hud.WakeDay);
        Assert.True(_hud.Waking);
        Assert.Equal(0f, _hud.FaintTextAlpha(1f));
        float first = _hud.WakeTextAlpha(1f);
        Assert.InRange(first, 0.001f, 0.2f);

        // The Player comes back alive (on DayNight's teleport): the screen fades back in under the text.
        Send(Player(10));
        Tick(Ticks(HudModule.WakeTextInSeconds));
        Assert.Equal(1f, _hud.WakeTextAlpha(1f), 5);
        float fade = _hud.FaintFade(1f);
        Assert.InRange(fade, 0.01f, 0.99f);
        Tick(Ticks(HudModule.FadeOutSeconds));
        Assert.Equal(0f, _hud.FaintFade(1f));
        Assert.True(_hud.WakeTextAlpha(1f) > 0f); // read on the bright screen a moment longer

        Tick(Ticks(HudModule.WakeSeconds));
        Assert.False(_hud.Waking);
        Assert.Equal(0f, _hud.WakeTextAlpha(1f));
    }

    // Place-name banner

    [Theory]
    [InlineData(Area.Town, "Mushroom Town")]
    [InlineData(Area.Forest, "Mushroom Forest")]
    [InlineData(Area.PetCity, "Pet City")]
    [InlineData(Area.Home, "Home")]
    [InlineData(Area.DeepForest, "Deep Forest")]
    [InlineData(Area.Road, "")]
    public void AreaNamesForTheBanner(Area area, string name)
    {
        Assert.Equal(name, HudText.AreaName(area));
    }

    [Fact]
    public void BannerSlidesInWhenEnteringANewPlaceThenOut()
    {
        Assert.False(_hud.BannerVisible);
        Send(In(Area.Town));
        Tick();
        Assert.True(_hud.BannerVisible);
        Assert.Equal(Area.Town, _hud.BannerArea);
        Assert.Equal(1, _hud.BannerCount);
        float early = _hud.BannerSlide(1f);
        Assert.InRange(early, 0.001f, 0.5f);

        Tick(Ticks(HudModule.BannerSlideSeconds));
        Assert.Equal(1f, _hud.BannerSlide(1f), 5);

        Send(In(Area.Town)); // still in town: nothing new
        Tick(Ticks(HudModule.BannerSeconds - HudModule.BannerSlideSeconds) - Ticks(HudModule.BannerSlideSeconds / 2));
        float leaving = _hud.BannerSlide(1f);
        Assert.InRange(leaving, 0.01f, 0.99f);
        Tick(Ticks(HudModule.BannerSlideSeconds));
        Assert.False(_hud.BannerVisible);
        Assert.Equal(0f, _hud.BannerSlide(1f));
        Assert.Equal(1, _hud.BannerCount);

        Send(In(Area.Forest));
        Tick();
        Assert.True(_hud.BannerVisible);
        Assert.Equal(Area.Forest, _hud.BannerArea);
        Assert.Equal(2, _hud.BannerCount);
    }

    [Fact]
    public void RoadsShowNoBannerAndComingBackToTheSamePlaceShowsNone()
    {
        Send(In(Area.Town));
        Tick(Ticks(HudModule.BannerSeconds));
        Send(In(Area.Road));
        Tick();
        Assert.False(_hud.BannerVisible);
        Send(In(Area.Town)); // turned back on the road
        Tick();
        Assert.False(_hud.BannerVisible);
        Assert.Equal(1, _hud.BannerCount);

        Send(In(Area.Road));
        Tick();
        Send(In(Area.PetCity));
        Tick();
        Assert.True(_hud.BannerVisible);
        Assert.Equal(Area.PetCity, _hud.BannerArea);
        Assert.Equal("Pet City", HudText.AreaName(_hud.BannerArea));
    }

    [Theory]
    [InlineData(Area.DeepForest, true)]
    [InlineData(Area.Town, false)]
    [InlineData(Area.Forest, false)]
    [InlineData(Area.PetCity, false)]
    [InlineData(Area.Home, false)]
    [InlineData(Area.Road, false)]
    public void OnlyTheDeepForestBannerIsSpooky(Area area, bool spooky)
    {
        Assert.Equal(spooky, HudText.IsSpooky(area));
    }

    [Fact]
    public void EnteringTheDeepForestSlidesInASpookyDeepForestBanner()
    {
        Send(In(Area.Forest));
        Tick(Ticks(HudModule.BannerSeconds));
        Assert.False(_hud.BannerSpooky);
        Send(In(Area.DeepForest));
        Tick();
        Assert.True(_hud.BannerVisible);
        Assert.Equal(Area.DeepForest, _hud.BannerArea);
        Assert.Equal("Deep Forest", HudText.AreaName(_hud.BannerArea));
        Assert.True(_hud.BannerSpooky);
        Assert.Equal(2, _hud.BannerCount);

        Send(In(Area.Forest)); // back out: the ordinary style again
        Tick();
        Assert.Equal(Area.Forest, _hud.BannerArea);
        Assert.False(_hud.BannerSpooky);
    }

    [Fact]
    public void NoBannerWhileFaintedThenTheWakingPlace()
    {
        Send(In(Area.Forest));
        Tick(Ticks(HudModule.BannerSeconds));
        Send(new PlayerDefeated(Vector3.Zero));
        Send(In(Area.Town, alive: false));
        Tick();
        Assert.False(_hud.BannerVisible);
        Send(In(Area.Town)); // woken at the clinic in town
        Tick();
        Assert.True(_hud.BannerVisible);
        Assert.Equal(Area.Town, _hud.BannerArea);
    }

    // Clock

    [Theory]
    [InlineData(0, "12:00 AM")]
    [InlineData(9 * 60 + 5, "9:05 AM")]
    [InlineData(12 * 60, "12:00 PM")]
    [InlineData(20 * 60 + 30, "8:30 PM")]
    [InlineData(23 * 60 + 59, "11:59 PM")]
    public void ClockTimeTexts(int minute, string text)
    {
        Assert.Equal(text, HudText.Time(minute));
    }

    [Fact]
    public void ClockShowsTheDayTimeAndSunOrMoon()
    {
        Assert.False(_hud.HasClock);
        Assert.Equal("Day 3", HudText.Day(3));

        Send(new TimeOfDay(3, 9.09f, false));
        Tick();
        Assert.True(_hud.HasClock);
        Assert.Equal(3, _hud.Day);
        Assert.Equal(9 * 60 + 5, _hud.ClockMinute);
        Assert.False(_hud.IsNight); // the sun
        Assert.Equal("9:05 AM", HudText.Time(_hud.ClockMinute));

        Send(new TimeOfDay(3, 22.5f, true));
        Tick();
        Assert.True(_hud.IsNight); // the moon
        Assert.Equal("10:30 PM", HudText.Time(_hud.ClockMinute));

        Send(new TimeOfDay(4, 23.9999f, true));
        Assert.Equal(4, _hud.Day);
        Assert.Equal(24 * 60 - 1, _hud.ClockMinute);
    }

    // Quest tracker

    [Fact]
    public void QuestTrackerText()
    {
        Assert.Equal("Defeat 5 Mushroom Caterpillars 3/5", HudText.Quest("Defeat 5 Mushroom Caterpillars", 3, 5));
    }

    [Fact]
    public void QuestTrackerFollowsTheActiveQuestAndChecksWhenComplete()
    {
        Assert.False(_hud.QuestActive);
        Send(new QuestState(false, "", 0, 0, false)); // before Millie gives one
        Assert.False(_hud.QuestActive);

        Send(Quest(3));
        Tick();
        Assert.True(_hud.QuestActive);
        Assert.False(_hud.QuestComplete);
        Assert.Equal("Defeat 5 Mushroom Caterpillars 3/5", HudText.Quest(_hud.QuestTitle, _hud.QuestProgress, _hud.QuestGoal));

        Send(Quest(5, complete: true));
        Tick();
        Assert.True(_hud.QuestComplete); // the check
        Assert.Equal("Defeat 5 Mushroom Caterpillars 5/5", HudText.Quest(_hud.QuestTitle, _hud.QuestProgress, _hud.QuestGoal));

        Send(Quest(0, goal: 3, title: "Defeat 3 Mushroom Ants")); // paid out, the next one
        Assert.False(_hud.QuestComplete);
        Assert.Equal("Defeat 3 Mushroom Ants 0/3", HudText.Quest(_hud.QuestTitle, _hud.QuestProgress, _hud.QuestGoal));

        Send(new QuestState(false, "", 0, 0, false));
        Assert.False(_hud.QuestActive);
    }

    [Fact]
    public void QuestVersionChangesOnlyWithTheTrackerAndProgressPops()
    {
        Send(Quest(1));
        Tick(Ticks(HudModule.PopSeconds));
        int version = _hud.QuestVersion;
        Assert.Equal(1f, _hud.QuestPopScale(1f));
        Send(Quest(1));
        Send(Quest(1, title: new string("Defeat 5 Mushroom Caterpillars".ToCharArray()))); // equal text, another string
        Assert.Equal(version, _hud.QuestVersion);

        Send(Quest(2));
        Tick();
        Assert.Equal(version + 1, _hud.QuestVersion);
        Assert.True(_hud.QuestPopScale(1f) > 1f);
        Tick(Ticks(HudModule.PopSeconds));
        Assert.Equal(1f, _hud.QuestPopScale(1f));
    }

    [Fact]
    public void QuestProgressIsClampedToTheGoal()
    {
        Send(new QuestState(true, "Bring me... earn 500 M¢ in the forest", 730, 500, true));
        Assert.Equal(500, _hud.QuestProgress);
        Send(new QuestState(true, "Bring me... earn 500 M¢ in the forest", -4, 500, false));
        Assert.Equal(0, _hud.QuestProgress);
    }

    // Interpolation and determinism

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
                bus.Publish(In(t < 60 ? Area.Town : Area.Forest));
                bus.Publish(new QuestState(true, "Defeat 5 Mushroom Caterpillars", t / 30, 5, false));
                if (t == 40)
                    bus.Publish(new WokeUp(2));
                hud.Tick(new TickContext(t, Step));
                values.Add(hud.HeartsShake(0.3f));
                values.Add(hud.CoinsPopScale(0.3f));
                values.Add(hud.DrawRing(0.3f));
                values.Add(hud.CrosshairAlpha(0.3f));
                values.Add(hud.Floater(0).Rise(0.3f));
                values.Add(hud.BannerSlide(0.3f));
                values.Add(hud.QuestPopScale(0.3f));
                values.Add(hud.WakeTextAlpha(0.3f));
            }
            return values.ToArray();
        }
        Assert.Equal(Run(), Run());
    }

    // Start screen

    [Fact]
    public void HiddenWhileTheStartScreenIsOpenThenShown()
    {
        Assert.False(_hud.Hidden); // no start screen (yet): the HUD shows
        Send(new StartScreenState(true));
        Send(Player(10));
        Tick();
        Assert.True(_hud.Hidden);
        Send(new StartScreenState(true));
        Tick(30);
        Assert.True(_hud.Hidden);
        Send(new StartScreenState(false)); // "Start!" pressed
        Tick();
        Assert.False(_hud.Hidden);
        Assert.True(_hud.HasPlayer);
        Send(new StartScreenState(false));
        Tick(30);
        Assert.False(_hud.Hidden);
    }

    // Minimap

    private static PlayerState At(float x, float z, float facing = 0f, bool alive = true) =>
        new(new Vector3(x, 0f, z), facing, Vector3.Zero, true, 10, 10, alive, CharacterKind.Girl);

    private const float Near = 1e-4f;

    [Theory]
    [InlineData(0f, 215f, Area.Town)]
    [InlineData(30f, 30f, Area.Forest)]
    [InlineData(0f, -215f, Area.PetCity)]
    [InlineData(-200f, 0f, Area.DeepForest)]
    public void MinimapShowsTheWholeZoneOfPlayerStateArea(float x, float z, Area area)
    {
        Send(At(x, z));
        Tick();
        Assert.Equal(area, _hud.MinimapArea);
        Assert.True(_hud.MinimapVisible);
        var picture = _hud.Minimap!;
        Assert.Equal(area, picture.Area);
        var bounds = Areas.Bounds(area);
        Assert.Equal(bounds.MinX, picture.Bounds.MinX);
        Assert.Equal(bounds.MinZ, picture.Bounds.MinZ);
        Assert.Equal(bounds.MaxX, picture.Bounds.MaxX);
        Assert.Equal(bounds.MaxZ, picture.Bounds.MaxZ);
        var window = _hud.MinimapWindow(1f);
        Assert.Equal(bounds.MinX, window.MinX);
        Assert.Equal(bounds.MaxZ, window.MaxZ);
        // One 1 m cell per meter of the zone, built once and shared.
        Assert.Equal((int)(bounds.MaxX - bounds.MinX), picture.Width);
        Assert.Equal((int)(bounds.MaxZ - bounds.MinZ), picture.Height);
        Assert.Equal(picture.Width * picture.Height, picture.Cells.Length);
        Assert.Same(picture, MinimapPicture.For(area));
    }

    [Fact]
    public void MinimapFollowsTheAreaFromZoneToRoadToZone()
    {
        Send(At(0f, 80f));
        Tick();
        Assert.Equal(Area.Forest, _hud.MinimapArea);
        var forest = _hud.Minimap;
        Send(At(3f, 125f)); // on the south road
        Tick();
        Assert.Equal(Area.Road, _hud.MinimapArea);
        Assert.Equal(Area.Road, _hud.Minimap!.Area);
        Send(At(0f, 200f));
        Tick();
        Assert.Equal(Area.Town, _hud.MinimapArea);
        Send(At(0f, 80f));
        Tick();
        Assert.Same(forest, _hud.Minimap); // built once, shown again
    }

    [Fact]
    public void OnTheRoadTheMinimapShowsA120MeterWindowAroundThePlayer()
    {
        Send(At(4f, 124f));
        Tick();
        Assert.Equal(Area.Road, _hud.MinimapArea);
        var window = _hud.MinimapWindow(1f);
        Assert.Equal(-56f, window.MinX, 3);
        Assert.Equal(64f, window.MaxX, 3);
        Assert.Equal(64f, window.MinZ, 3);
        Assert.Equal(184f, window.MaxZ, 3);
        Assert.Equal(HudModule.RoadWindowMeters, window.MaxX - window.MinX, 3);
        Assert.Equal(new Vector2(0.5f, 0.5f), _hud.MinimapPlayer(1f));

        // The window walks with the player; the road picture under it has the road, the forest's
        // south edge and the town's north edge in it.
        Send(At(0f, 140f));
        Tick();
        Assert.Equal(80f, _hud.MinimapWindow(1f).MinZ, 3);
        var road = _hud.Minimap!;
        Assert.Equal(MinimapCell.Path, road.CellAt(0.5f, 140.5f));
        Assert.Equal(MinimapCell.Outside, road.CellAt(30f, 125f)); // cliffs beside the road
        Assert.Equal(MinimapCell.Ground, road.CellAt(30f, 95f)); // the forest's south edge
        Assert.Equal(MinimapCell.Ground, road.CellAt(30f, 160f)); // the town's north edge
        Assert.Equal(MinimapCell.Outside, road.CellAt(30f, 130f));
        Assert.Contains(road.Marks, m => m.Kind == MinimapMarkKind.Signpost && m.X == Places.SignpostTown.Position.X && m.Z == Places.SignpostTown.Position.Z);
    }

    [Fact]
    public void PlayerMapsOntoTheMinimapNorthUpWithTheZoneCornersAtThePictureCorners()
    {
        var town = Areas.Town; // x -80..80, z 150..290
        Send(At(town.MinX, town.MinZ)); // north-west corner
        Tick();
        Assert.Equal(Area.Town, _hud.MinimapArea);
        Assert.Equal(new Vector2(0f, 0f), _hud.MinimapPlayer(1f));
        Assert.Equal(new Vector2(0f, 0f), _hud.Minimap!.ToPicture(town.MinX, town.MinZ));
        Assert.Equal(new Vector2(1f, 0f), _hud.Minimap.ToPicture(town.MaxX, town.MinZ)); // north-east: top right
        Assert.Equal(new Vector2(0f, 1f), _hud.Minimap.ToPicture(town.MinX, town.MaxZ)); // south-west: bottom left
        Send(At(town.MaxX, town.MaxZ)); // south-east corner
        Tick();
        Assert.Equal(new Vector2(1f, 1f), _hud.MinimapPlayer(1f));

        // Mushroom Town's fountain: the middle across, 65 of 140 m down.
        Send(At(0f, 215f));
        Tick();
        var p = _hud.MinimapPlayer(1f);
        Assert.Equal(0.5f, p.X, 4);
        Assert.Equal(65f / 140f, p.Y, 4);

        // In the forest, a point north-east of the middle is up and right of the picture's middle.
        Send(At(50f, -50f));
        Tick();
        p = _hud.MinimapPlayer(1f);
        Assert.Equal(0.75f, p.X, 4);
        Assert.Equal(0.25f, p.Y, 4);
    }

    [Fact]
    public void MinimapArrowGlidesBetweenTicksAndJumpsOnATeleport()
    {
        Send(At(0f, 80f));
        Tick();
        Send(At(0f, 76f));
        Tick();
        Assert.Equal(80f, _hud.MinimapSpot(0f).Y, 3);
        Assert.Equal(78f, _hud.MinimapSpot(0.5f).Y, 3);
        Assert.Equal(76f, _hud.MinimapSpot(1f).Y, 3);
        Send(At(0f, 215f)); // fast travel to town
        Tick();
        Assert.Equal(215f, _hud.MinimapSpot(0f).Y, 3);
    }

    [Theory]
    [InlineData(0f, 0f)] // facing north: up
    [InlineData(MathF.PI / 2f, -MathF.PI / 2f)] // facing west (-X): left
    [InlineData(-MathF.PI / 2f, MathF.PI / 2f)] // facing east (+X): right
    [InlineData(MathF.PI / 4f, -MathF.PI / 4f)] // north-west: up and left
    [InlineData(5f * MathF.PI / 2f, -MathF.PI / 2f)] // a full turn more is the same
    public void ArrowAngleFollowsFacing(float facing, float angle)
    {
        Send(At(0f, 80f, facing));
        Tick(2);
        Assert.Equal(angle, _hud.MinimapArrow(1f), 4);
        Assert.Equal(angle, MinimapMath.ArrowAngle(facing), 4);
        // The arrow's tip, (sin a, -cos a) on screen (y down), points the way the player faces (x, z).
        var dir = new Vector2(-MathF.Sin(facing), -MathF.Cos(facing));
        Assert.Equal(dir.X, MathF.Sin(angle), 4);
        Assert.Equal(dir.Y, -MathF.Cos(angle), 4);
    }

    [Fact]
    public void ArrowAngleFacingSouthTurnsTheShortWayBetweenTicks()
    {
        Send(At(0f, 80f, MathF.PI - 0.1f));
        Tick();
        Send(At(0f, 80f, -MathF.PI + 0.1f));
        Tick();
        Assert.Equal(MathF.PI, MathF.Abs(_hud.MinimapArrow(0.5f)), 3); // straight down, not through north
    }

    [Fact]
    public void TownPictureMarksBuildingsPathsSignpostAndSpotsWhereTheMapSays()
    {
        var town = MinimapPicture.For(Area.Town)!;
        int count = 0;
        foreach (var b in Buildings.All)
        {
            if (b.Area != Area.Town)
                continue;
            count++;
            var mark = Assert.Single(town.Buildings, m => m.Kind == b.Kind && m.Center == b.Center);
            // Its corners are the footprint's, inside the picture.
            for (int i = 0; i < 4; i++)
            {
                var c = mark.Corner(i);
                Assert.True(b.Contains(c.X, c.Y, 0.01f));
                Assert.False(b.Contains(c.X, c.Y, -0.01f));
                var uv = town.ToPicture(c.X, c.Y);
                Assert.InRange(uv.X, 0f, 1f);
                Assert.InRange(uv.Y, 0f, 1f);
            }
        }
        Assert.Equal(count, town.Buildings.Length);
        Assert.Contains(town.Buildings, m => m.Kind == BuildingKind.PlayerHouse);
        Assert.Contains(town.Buildings, m => m.Kind == BuildingKind.MillieHouse);
        Assert.Contains(town.Buildings, m => m.Kind == BuildingKind.Clinic);
        Assert.DoesNotContain(town.Buildings, m => m.Kind == BuildingKind.PetStore);

        // Paths: the street from the entrance, the path to your door; grass beside them; cliffs beyond the town's corner.
        Assert.Equal(MinimapCell.Path, town.CellAt(0.5f, 170.5f));
        var door = Places.HomeDoor.Position;
        Assert.Equal(MinimapCell.Path, town.CellAt(door.X, door.Z));
        Assert.Equal(MinimapCell.Ground, town.CellAt(65.5f, 220.5f));
        int path = 0, ground = 0;
        for (int row = 0; row < town.Height; row++)
            for (int col = 0; col < town.Width; col++)
            {
                float x = town.Bounds.MinX + col + 0.5f, z = town.Bounds.MinZ + row + 0.5f;
                var cell = town.Cells[row * town.Width + col];
                Assert.Equal(MinimapPicture.Sample(x, z), cell);
                bool onPath = Areas.TownPathDistance(x, z) <= Areas.TownPathHalfWidth || Areas.RoadDistance(x, z) <= Areas.RoadHalfWidth;
                Assert.Equal(onPath && Areas.Walkable(x, z), cell == MinimapCell.Path);
                Assert.Equal(Areas.Walkable(x, z), cell != MinimapCell.Outside);
                if (cell == MinimapCell.Path) path++;
                if (cell == MinimapCell.Ground) ground++;
            }
        Assert.True(path > 300 && ground > path * 5);

        // The signpost and the spots.
        Assert.Contains(town.Marks, m => m.Kind == MinimapMarkKind.Signpost && m.X == Places.SignpostTown.Position.X && m.Z == Places.SignpostTown.Position.Z);
        foreach (var spot in new[] { Places.HomeDoor, Places.ClinicDoor, Places.MillieSpot, Places.FurnitureShopSpot, Places.AbbySpot, Places.FoodSpot })
            Assert.Contains(town.Marks, m => m.Kind == MinimapMarkKind.Spot && m.X == spot.Position.X && m.Z == spot.Position.Z);
        Assert.DoesNotContain(town.Marks, m => m.X == Places.LucySpot.Position.X && m.Z == Places.LucySpot.Position.Z);
    }

    [Fact]
    public void OtherZonesMarkTheirBuildingsPathsAndSignposts()
    {
        var city = MinimapPicture.For(Area.PetCity)!;
        Assert.Contains(city.Buildings, m => m.Kind == BuildingKind.PetStore && m.Center == Buildings.First(BuildingKind.PetStore).Center);
        Assert.Equal(11, city.Buildings.Length);
        Assert.Equal(MinimapCell.Path, city.CellAt(0.5f, -200.5f)); // the avenue
        Assert.Equal(MinimapCell.Paving, city.CellAt(20.5f, -200.5f));
        Assert.Contains(city.Marks, m => m.Kind == MinimapMarkKind.Signpost && m.Z == Places.SignpostPetCity.Position.Z);
        Assert.Contains(city.Marks, m => m.Kind == MinimapMarkKind.Spot && m.Z == Places.LucySpot.Position.Z);

        var forest = MinimapPicture.For(Area.Forest)!;
        Assert.Empty(forest.Buildings);
        Assert.Equal(MinimapCell.Path, forest.CellAt(-14f, 2f)); // on the west-east path
        Assert.Equal(MinimapCell.Path, forest.CellAt(0.5f, 97.5f)); // the road in the south gap
        Assert.Equal(MinimapCell.Clearing, forest.CellAt(-56f, 30f)); // a clearing off the path
        Assert.Equal(MinimapCell.Ground, forest.CellAt(70.5f, 20.5f));
        Assert.Equal(2, forest.Marks.Length); // its south and north signposts
        Assert.All(forest.Marks, m => Assert.Equal(MinimapMarkKind.Signpost, m.Kind));

        var deep = MinimapPicture.For(Area.DeepForest)!;
        Assert.Empty(deep.Buildings);
        Assert.Equal(MinimapCell.Path, deep.CellAt(-110.5f, 0.5f)); // the overgrown path's end
        Assert.Equal(MinimapCell.Moss, deep.CellAt(-200f, 0f));
        var sign = Assert.Single(deep.Marks);
        Assert.Equal(Places.SignpostDeepForest.Position.X, sign.X);
    }

    [Fact]
    public void MinimapIsHiddenIndoorsOnTheStartScreenAndFadesWhenFainting()
    {
        Assert.False(_hud.MinimapVisible); // no player yet
        Send(new StartScreenState(true));
        Send(At(0f, 215f));
        Tick();
        Assert.False(_hud.MinimapVisible); // start screen
        Send(new StartScreenState(false));
        Tick();
        Assert.True(_hud.MinimapVisible);

        var inside = Places.HomeInterior.Position;
        Send(At(inside.X, inside.Z));
        Tick();
        Assert.Equal(Area.Home, _hud.MinimapArea);
        Assert.Null(_hud.Minimap);
        Assert.False(_hud.MinimapVisible); // in the home interior
        Assert.Null(MinimapPicture.For(Area.Home));
        Send(At(Places.HomeDoor.Position.X, Places.HomeDoor.Position.Z));
        Tick();
        Assert.True(_hud.MinimapVisible);

        Assert.Equal(1f, _hud.MinimapAlpha(1f));
        Send(new PlayerDefeated(default));
        Send(At(0f, 215f, alive: false));
        Tick(10);
        Assert.True(_hud.MinimapVisible); // fading out with the screen going black
        Assert.InRange(_hud.MinimapAlpha(1f), 0.01f, 0.99f);
        Tick(Ticks(HudModule.FadeInSeconds));
        Assert.False(_hud.MinimapVisible); // fainted: black
        Assert.Equal(0f, _hud.MinimapAlpha(1f));
        Send(At(Places.ClinicDoor.Position.X, Places.ClinicDoor.Position.Z));
        Tick();
        Assert.True(_hud.MinimapVisible); // woken at the clinic: back with the screen
        Assert.InRange(_hud.MinimapAlpha(1f), 0f, 0.1f);
        Tick(Ticks(HudModule.FadeOutSeconds));
        Assert.Equal(1f, _hud.MinimapAlpha(1f));
    }
}
