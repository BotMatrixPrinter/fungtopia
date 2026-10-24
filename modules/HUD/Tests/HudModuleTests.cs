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
}
