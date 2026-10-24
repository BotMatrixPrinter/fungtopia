using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Map;
using Fungtopia.Player;
using Fungtopia.Save;
using Fungtopia.Shell;
using Xunit;

namespace Fungtopia.DayNight.Tests;

public class DayNightModuleTests
{
    private const double Step = 1.0 / 60;
    private const int TicksPerSecond = 60;

    /// <summary>Fake of the shell's bus: delivers to subscribers and records what DayNight publishes.</summary>
    private sealed class FakeBus : IMessageBus
    {
        private readonly Dictionary<Type, List<object>> _handlers = new();
        public readonly List<TimeOfDay> Times = new();
        public readonly List<TeleportPlayer> Teleports = new();
        public readonly List<WokeUp> Wakes = new();
        public readonly List<SavePart> SaveParts = new();
        public readonly List<Type> Published = new();

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
                _handlers[typeof(T)] = list = new List<object>();
            list.Add(handler);
        }

        public void Publish<T>(in T message) where T : struct
        {
            Published.Add(typeof(T));
            if (message is SavePart part)
                SaveParts.Add(part);
            if (message is TimeOfDay time)
                Times.Add(time);
            else if (message is TeleportPlayer teleport)
                Teleports.Add(teleport);
            else if (message is WokeUp woke)
                Wakes.Add(woke);
            if (_handlers.TryGetValue(typeof(T), out var list))
                foreach (var handler in list)
                    ((IMessageHandler<T>)handler).Handle(in message);
        }
    }

    private readonly FakeBus _bus = new();
    private readonly DayNightModule _clock = new();
    private long _tick;

    public DayNightModuleTests() => _clock.Start(_bus);

    [Fact]
    public void VolumetricFogIsOnWithNoDensityOfItsOwn()
    {
        Assert.True(VolumetricFog.Enabled);
        Assert.Equal(0f, VolumetricFog.Density);
    }

    [Fact]
    public void VolumetricFogIsCheaperThanGodotsDefaults()
    {
        Assert.InRange(VolumetricFog.Length, 16f, 63f);
        Assert.InRange(VolumetricFog.VolumeSize, 16, 63);
        Assert.InRange(VolumetricFog.VolumeDepth, 16, 63);
        Assert.Equal(0f, VolumetricFog.GiInject);
    }

    private void Ticks(int count)
    {
        for (int i = 0; i < count; i++)
            _clock.Tick(new TickContext(_tick++, Step));
    }

    private void Seconds(double seconds) => Ticks((int)Math.Round(seconds * TicksPerSecond));

    private TimeOfDay Last => _bus.Times[^1];

    private void Faint() => _bus.Publish(new PlayerDefeated(new Vector3(3f, 0f, -20f)));

    // ---- The clock ----

    [Fact]
    public void StartsOnDayOneAtNine()
    {
        Ticks(1);
        Assert.Equal(1, Last.Day);
        Assert.Equal(9f, Last.Hour, 2);
        Assert.False(Last.IsNight);
    }

    [Fact]
    public void PublishesTimeOfDayEveryTick()
    {
        Ticks(120);
        Assert.Equal(120, _bus.Times.Count);
        for (int i = 1; i < _bus.Times.Count; i++)
            Assert.True(_bus.Times[i].Hour > _bus.Times[i - 1].Hour);
    }

    [Fact]
    public void HoursAdvanceTwentyFourPerTwentyMinutes()
    {
        // 50 real seconds are one in-game hour.
        Seconds(50);
        Assert.Equal(10f, Last.Hour, 3);
        Seconds(150);
        Assert.Equal(13f, Last.Hour, 3);
        Assert.Equal(1, Last.Day);
    }

    [Fact]
    public void ADayLastsTwentyRealMinutes()
    {
        Seconds(20 * 60);
        Assert.Equal(2, Last.Day);
        Assert.Equal(9f, Last.Hour, 2);
    }

    [Fact]
    public void DayCountsUpAtMidnight()
    {
        // 9:00 to midnight is 15 hours: 750 real seconds.
        Seconds(750 - 1);
        Assert.Equal(1, Last.Day);
        Assert.True(Last.Hour > 23.9f && Last.Hour < 24f, $"hour {Last.Hour}");
        Seconds(2);
        Assert.Equal(2, Last.Day);
        Assert.True(Last.Hour < 0.1f, $"hour {Last.Hour}");
        Seconds(20 * 60 * 3);
        Assert.Equal(5, Last.Day);
    }

    [Fact]
    public void HourStaysBelowTwentyFour()
    {
        Seconds(750 - 2);
        for (int i = 0; i < 240; i++)
        {
            Ticks(1);
            Assert.True(Last.Hour >= 0f && Last.Hour < 24f, $"hour {Last.Hour}");
        }
    }

    // ---- Night ----

    [Theory]
    [InlineData(19.99f, false)]
    [InlineData(20f, true)]
    [InlineData(23.5f, true)]
    [InlineData(0f, true)]
    [InlineData(5.99f, true)]
    [InlineData(6f, false)]
    [InlineData(12f, false)]
    public void NightIsFromEightPmToSixAm(float hour, bool night)
    {
        Assert.Equal(night, TimeOfDay.IsNightAt(hour));
        _clock.SetTime(1, hour - Step / 50.0); // one tick earlier, so the tick lands on the hour
        Ticks(1);
        Assert.Equal(hour, Last.Hour, 3);
        Assert.Equal(night, Last.IsNight);
        Assert.Equal(night, _clock.IsNight);
    }

    [Fact]
    public void EveningTurnsToNightAndMorningBack()
    {
        Seconds(11 * 50 - 1); // 9:00 + 11 h - a second
        Assert.False(Last.IsNight);
        Seconds(2);
        Assert.True(Last.IsNight);
        Seconds(10 * 50 - 2); // to just before 6:00 the next day
        Assert.True(Last.IsNight);
        Assert.Equal(2, Last.Day);
        Seconds(2);
        Assert.False(Last.IsNight);
    }

    // ---- Fainting ----

    [Fact]
    public void DefeatSkipsToNextMorningAtTheClinicAfterThreeSeconds()
    {
        _clock.SetTime(1, 15.0);
        Ticks(1);
        Faint();
        Ticks(3 * TicksPerSecond - 1);
        Assert.Empty(_bus.Teleports);
        Assert.Empty(_bus.Wakes);
        Assert.True(_clock.Fainting);
        Assert.Equal(1, Last.Day);

        Ticks(1);
        var teleport = Assert.Single(_bus.Teleports);
        Assert.Equal(Places.ClinicDoor.Position, teleport.Position);
        Assert.Equal(Places.ClinicDoor.Yaw, teleport.Facing);
        Assert.Equal(2, Assert.Single(_bus.Wakes).Day);
        Assert.Equal(2, Last.Day);
        Assert.Equal(7f, Last.Hour, 3);
        Assert.False(Last.IsNight);
        Assert.False(_clock.Fainting);
    }

    [Fact]
    public void OneTeleportAndOneWokeUpPerFaint()
    {
        Faint();
        Ticks(30);
        Faint(); // a second defeat message while already fainting changes nothing
        Seconds(10);
        Assert.Single(_bus.Teleports);
        Assert.Single(_bus.Wakes);
        Seconds(5);
        Assert.Single(_bus.Teleports);
    }

    [Fact]
    public void FaintingAtNightWakesNextMorning()
    {
        _clock.SetTime(3, 22.5);
        Faint();
        Seconds(3);
        Assert.Equal(4, Assert.Single(_bus.Wakes).Day);
        Assert.Equal(4, Last.Day);
        Assert.Equal(7f, Last.Hour, 3);
    }

    [Fact]
    public void FaintingInTheSmallHoursWakesThatSameMorning()
    {
        // At 2:00 on day 4 the next morning is day 4's 7:00, not a whole day later.
        _clock.SetTime(4, 2.0);
        Faint();
        Seconds(3);
        Assert.Equal(4, Assert.Single(_bus.Wakes).Day);
        Assert.Equal(7f, Last.Hour, 3);
    }

    [Fact]
    public void FaintingAgainOnALaterDayWakesAgain()
    {
        Faint();
        Seconds(3);
        Seconds(100);
        Faint();
        Seconds(3);
        Assert.Equal(2, _bus.Teleports.Count);
        Assert.Equal(new[] { 2, 3 }, new[] { _bus.Wakes[0].Day, _bus.Wakes[1].Day });
    }

    [Fact]
    public void FaintProgressRisesDuringTheFaint()
    {
        Assert.Equal(0f, _clock.FaintProgress);
        Faint();
        Seconds(1.5);
        Assert.Equal(0.5f, _clock.FaintProgress, 2);
        Seconds(1.5);
        Assert.Equal(0f, _clock.FaintProgress);
    }

    [Fact]
    public void TracksThePlayersArea()
    {
        _bus.Publish(new PlayerState(Vector3.Zero, 0f, Vector3.Zero, true, 10, 10, true, CharacterKind.Girl, Area.Forest));
        Assert.Equal(Area.Forest, _clock.PlayerArea);
    }

    // ---- Settings, determinism, performance ----

    [Fact]
    public void SetTimeWrapsHoursAndRejectsDayZero()
    {
        _clock.SetTime(2, 25.0);
        Assert.Equal(1.0, _clock.Hour, 6);
        _clock.SetTime(2, -1.0);
        Assert.Equal(23.0, _clock.Hour, 6);
        Assert.Throws<ArgumentOutOfRangeException>(() => _clock.SetTime(0, 12.0));
    }

    [Fact]
    public void TickBeforeStartThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new DayNightModule().Tick(new TickContext(0, Step)));
    }

    [Fact]
    public void IsDeterministic()
    {
        var other = new FakeBus();
        var clock = new DayNightModule();
        clock.Start(other);
        Seconds(400);
        for (int i = 0; i < 400 * TicksPerSecond; i++)
            clock.Tick(new TickContext(i, Step));
        Assert.Equal(Last.Day, other.Times[^1].Day);
        Assert.Equal(Last.Hour, other.Times[^1].Hour);
    }

    [Fact]
    public void TicksWithoutAllocating()
    {
        var bus = new NullBus();
        var clock = new DayNightModule();
        clock.Start(bus);
        clock.Tick(new TickContext(0, Step)); // warm up
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 2000; i++)
        {
            if (i % 500 == 0)
                clock.Handle(new PlayerDefeated(Vector3.Zero));
            clock.Handle(default(PlayerState));
            clock.Tick(new TickContext(i + 1, Step));
        }
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    // ---- Saving and restoring ----

    private SavePart Save(int requestId = 7)
    {
        int before = _bus.SaveParts.Count;
        _bus.Publish(new CollectSave(requestId));
        Assert.Equal(before + 1, _bus.SaveParts.Count);
        return _bus.SaveParts[^1];
    }

    /// <summary>A fresh DayNight on its own bus, with its log lines collected.</summary>
    private static (DayNightModule Clock, FakeBus Bus, List<string> Logs) Fresh()
    {
        var bus = new FakeBus();
        var clock = new DayNightModule();
        var logs = new List<string>();
        clock.Log = logs.Add;
        clock.Start(bus);
        return (clock, bus, logs);
    }

    [Fact]
    public void CollectSaveIsAnsweredAtOnceWithOneCompactVersionedPart()
    {
        _clock.SetTime(4, 13.25);
        var part = Save(42);
        Assert.Equal(42, part.RequestId);
        Assert.Equal("DayNight", part.Module);
        Assert.Equal(SaveModules.DayNight, part.Module);
        Assert.DoesNotContain(" ", part.Json);
        Assert.DoesNotContain("\n", part.Json);
        using var doc = System.Text.Json.JsonDocument.Parse(part.Json);
        Assert.Equal(DayNightModule.SaveVersion, doc.RootElement.GetProperty("v").GetInt32());
        Assert.Equal(4, doc.RootElement.GetProperty("day").GetInt32());
        Assert.Equal(13.25, doc.RootElement.GetProperty("hour").GetDouble());
    }

    [Theory]
    [InlineData(1, 9.0)]
    [InlineData(3, 0.0)]
    [InlineData(12, 23.999)]
    public void SaveThenRestoreRoundTripsTheDayAndHour(int day, double hour)
    {
        _clock.SetTime(day, hour);
        Seconds(37.3); // an hour that is not a round number
        int savedDay = _clock.Day;
        double savedHour = _clock.Hour;
        float savedHourF = _clock.HourF;
        var part = Save();

        var (other, otherBus, logs) = Fresh();
        otherBus.Publish(new RestorePart(part.Module, part.Json));
        Assert.Equal(savedDay, other.Day);
        Assert.Equal(savedHour, other.Hour);
        Assert.Equal(savedHourF, other.HourF);
        Assert.Equal(_clock.IsNight, other.IsNight);
        Assert.Empty(logs);

        // Saving the restored clock gives the same JSON.
        otherBus.Publish(new CollectSave(1));
        Assert.Equal(part.Json, otherBus.SaveParts[^1].Json);

        // Both clocks then run on identically.
        _clock.Tick(new TickContext(0, Step));
        other.Tick(new TickContext(0, Step));
        Assert.Equal(_bus.Times[^1].Day, otherBus.Times[^1].Day);
        Assert.Equal(_bus.Times[^1].Hour, otherBus.Times[^1].Hour);
    }

    [Fact]
    public void RestoringPublishesNothingAndDropsAFaint()
    {
        var (other, otherBus, _) = Fresh();
        other.SetTime(2, 10.0);
        var part = Save();
        Faint();
        _clock.SetTime(5, 21.5);
        var saved = Save();
        Assert.NotEqual(part.Json, saved.Json);

        otherBus.Publish(new PlayerDefeated(Vector3.Zero));
        Assert.True(other.Fainting);
        otherBus.Published.Clear();
        otherBus.Publish(new RestorePart(saved.Module, saved.Json));
        Assert.Equal(new[] { typeof(RestorePart) }, otherBus.Published);
        Assert.False(other.Fainting);
        Assert.Equal(5, other.Day);
        Assert.Equal(21.5, other.Hour, 9);

        // Ticking past the faint's three seconds wakes nobody at the clinic.
        for (int i = 0; i < 5 * TicksPerSecond; i++)
            other.Tick(new TickContext(i, Step));
        Assert.Empty(otherBus.Teleports);
        Assert.Empty(otherBus.Wakes);
        Assert.Equal(5, otherBus.Times[^1].Day);
    }

    [Fact]
    public void RestorePartsOfOtherModulesAreIgnored()
    {
        var (other, otherBus, logs) = Fresh();
        other.SetTime(3, 15.0);
        otherBus.Publish(new RestorePart("Wallet", "{\"v\":1,\"day\":9,\"hour\":2}"));
        otherBus.Publish(new RestorePart("Player", "not json"));
        Assert.Equal(3, other.Day);
        Assert.Equal(15.0, other.Hour, 9);
        Assert.Empty(logs);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"v\":1,\"day\":")]
    [InlineData("[1,2,3]")]
    [InlineData("null")]
    [InlineData("{\"v\":\"one\",\"day\":2,\"hour\":3}")]
    [InlineData("{\"v\":2,\"day\":5,\"hour\":12}")]
    [InlineData("{\"v\":0,\"day\":5,\"hour\":12}")]
    [InlineData("{\"day\":5,\"hour\":12}")]
    [InlineData("{\"v\":1,\"day\":0,\"hour\":12}")]
    [InlineData("{\"v\":1,\"day\":5,\"hour\":24}")]
    [InlineData("{\"v\":1,\"day\":5,\"hour\":-1}")]
    public void BadJsonOrUnknownVersionKeepsDefaultsAndLogsOnce(string json)
    {
        var (other, otherBus, logs) = Fresh();
        otherBus.Publish(new RestorePart(SaveModules.DayNight, json));
        Assert.Equal(1, other.Day);
        Assert.Equal(DayNightModule.StartHour, other.Hour, 9);
        Assert.Single(logs);
        Assert.DoesNotContain("ERROR", logs[0]);
        Assert.Equal(new[] { typeof(RestorePart) }, otherBus.Published);

        // A clock that had run on is put back to the defaults too, not left at the old game's time.
        other.SetTime(6, 2.0);
        otherBus.Publish(new RestorePart(SaveModules.DayNight, json));
        Assert.Equal(1, other.Day);
        Assert.Equal(DayNightModule.StartHour, other.Hour, 9);
        Assert.Equal(2, logs.Count);

        other.Tick(new TickContext(0, Step));
        Assert.Equal(1, otherBus.Times[^1].Day);
    }

    private sealed class NullBus : IMessageBus
    {
        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct { }
        public void Publish<T>(in T message) where T : struct { }
    }
}
