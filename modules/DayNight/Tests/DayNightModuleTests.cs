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

    // ---- The light (Lighting's palette) ----

    /// <summary>Every hour from 0 to 24 in quarter hours.</summary>
    private static IEnumerable<float> QuarterHours()
    {
        for (int q = 0; q <= 96; q++)
            yield return q / 4f;
    }

    private static IEnumerable<Rgb> Colors(Mood m)
    {
        yield return m.Top;
        yield return m.Horizon;
        yield return m.Ground;
        yield return m.Light;
        yield return m.Ambient;
        yield return m.Fog;
    }

    [Fact]
    public void MoodsRunFromMidnightToMidnightInOrder()
    {
        var moods = Lighting.Moods;
        Assert.Equal(0f, moods[0].Hour);
        Assert.Equal(24f, moods[^1].Hour);
        for (int i = 1; i < moods.Length; i++)
            Assert.True(moods[i].Hour > moods[i - 1].Hour);
        Assert.Equal(moods[0].Top.Hue, Lighting.At(24f).Top.Hue, 3);
    }

    [Fact]
    public void AtBlendsBetweenTheKeysAndHitsThemExactly()
    {
        var day = Lighting.Moods[Lighting.Day];
        Assert.Equal(day.Top.B, Lighting.At(day.Hour).Top.B, 5);
        Assert.Equal(day.FogDensity, Lighting.At(13f).FogDensity, 6);
        // Halfway from evening to dusk lies between the two.
        var evening = Lighting.Moods[Lighting.Evening];
        var dusk = Lighting.Moods[Lighting.Dusk];
        float g = Lighting.At((evening.Hour + dusk.Hour) / 2f).Horizon.G;
        Assert.InRange(g, Math.Min(evening.Horizon.G, dusk.Horizon.G), Math.Max(evening.Horizon.G, dusk.Horizon.G));
    }

    [Fact]
    public void DaySkyIsAClearSaturatedBlue()
    {
        foreach (float hour in new[] { 11f, 13f, 16f })
        {
            var m = Lighting.At(hour);
            Assert.InRange(m.Top.Hue, 205f, 235f);
            Assert.True(m.Top.Saturation >= 0.8f, $"top saturation {m.Top.Saturation} at {hour}");
            Assert.True(m.Top.B >= 0.95f);
            // The horizon is a lighter blue, not white.
            Assert.InRange(m.Horizon.Hue, 195f, 230f);
            Assert.InRange(m.Horizon.Saturation, 0.3f, 0.7f);
        }
    }

    [Fact]
    public void DaySunlightIsWarmAndBright()
    {
        var light = Lighting.At(13f).Light;
        Assert.True(light.R >= light.G && light.G > light.B, "warm: red >= green > blue");
        Assert.InRange(light.Hue, 35f, 55f);
        Assert.InRange(light.Saturation, 0.15f, 0.35f); // warm, but not orange
        Assert.True(light.Luminance >= 0.9f);
        Assert.InRange(Lighting.SunEnergy, 1.15f, 1.4f);
    }

    [Fact]
    public void AmbientIsColoredNeverGrey()
    {
        foreach (float hour in QuarterHours())
        {
            var ambient = Lighting.At(hour).Ambient;
            Assert.True(ambient.Saturation >= 0.25f, $"ambient saturation {ambient.Saturation} at {hour}");
        }
        // By day it is a sky blue.
        Assert.InRange(Lighting.At(13f).Ambient.Hue, 210f, 235f);
    }

    [Fact]
    public void DayHazeIsThinAndBlueNotGrey()
    {
        foreach (float hour in new[] { 10f, 11f, 13f, 16f })
            Assert.True(Lighting.At(hour).FogDensity <= 0.002f, $"fog {Lighting.At(hour).FogDensity} at {hour}");
        Assert.True(Lighting.At(13f).FogDensity <= 0.0015f);
        var fog = Lighting.At(13f).Fog;
        Assert.True(fog.Saturation >= 0.35f);
        Assert.InRange(fog.Hue, 200f, 230f);
        // Day fog is thinner than at dawn, dusk and night.
        foreach (int key in new[] { Lighting.Night, Lighting.Dawn, Lighting.Dusk })
            Assert.True(Lighting.Moods[key].FogDensity > Lighting.At(13f).FogDensity * 3f);
    }

    [Fact]
    public void EnvironmentGivesAGentleSaturationBoost()
    {
        Assert.InRange(Lighting.AdjustmentSaturation, 1.25f, 1.4f);
        Assert.InRange(Lighting.AdjustmentContrast, 1f, 1.1f);
        Assert.InRange(Lighting.AdjustmentBrightness, 1f, 1.05f);
    }

    [Fact]
    public void EveningIsGolden()
    {
        var m = Lighting.Moods[Lighting.Evening];
        Assert.InRange(m.Hour, 18f, 19.5f);
        Assert.InRange(m.Light.Hue, 30f, 50f);
        Assert.True(m.Light.Saturation >= 0.6f);
        Assert.InRange(m.Horizon.Hue, 28f, 45f);
        Assert.True(m.Horizon.Saturation >= 0.7f);
        Assert.InRange(m.Fog.Hue, 28f, 45f);
    }

    [Fact]
    public void DuskIsPinkPurple()
    {
        var m = Lighting.Moods[Lighting.Dusk];
        Assert.Equal(TimeOfDay.NightStartHour, m.Hour);
        Assert.InRange(m.Top.Hue, 255f, 300f); // purple overhead
        Assert.InRange(m.Horizon.Hue, 310f, 345f); // pink at the horizon
        Assert.InRange(m.Ambient.Hue, 260f, 300f);
        Assert.True(m.Horizon.Saturation >= 0.5f && m.Top.Saturation >= 0.6f);
    }

    [Fact]
    public void NightIsADeepBluePurpleInWhichGlowStandsOut()
    {
        foreach (float hour in new[] { 22f, 0f, 3f })
        {
            var m = Lighting.At(hour);
            Assert.InRange(m.Top.Hue, 240f, 270f);
            Assert.True(m.Top.Saturation >= 0.8f);
            Assert.True(m.Top.Luminance <= 0.06f, "deep");
            Assert.InRange(m.Horizon.Hue, 250f, 285f);
            Assert.InRange(m.Ambient.Hue, 245f, 275f);
            Assert.InRange(m.Fog.Hue, 245f, 280f);
            // Dim ambient and strong glow, so glowing mushrooms stand out; the stars are out.
            Assert.True(m.AmbientEnergy <= 0.5f);
            Assert.True(m.Glow >= 1.1f && m.Glow > Lighting.At(13f).Glow * 2f);
            Assert.Equal(1f, m.Stars);
        }
    }

    [Fact]
    public void NothingIsWashedOutOrGarish()
    {
        foreach (float hour in QuarterHours())
        {
            var m = Lighting.At(hour);
            foreach (var c in Colors(m))
            {
                Assert.InRange(c.R, 0f, 1f);
                Assert.InRange(c.G, 0f, 1f);
                Assert.InRange(c.B, 0f, 1f);
                // Never a grey or near-white wash: every color carries some hue.
                Assert.True(c.Saturation >= 0.15f, $"saturation {c.Saturation} at {hour}");
            }
            Assert.InRange(m.AmbientEnergy, 0.4f, 0.8f); // shadows stay readable
            Assert.InRange(m.Glow, 0.4f, 1.3f);
            Assert.InRange(m.FogDensity, 0f, 0.012f);
        }
    }

    [Fact]
    public void ForestHazeIsAGreenGoldByDayAndTealByNight()
    {
        Assert.InRange(Lighting.ForestFogDay.Hue, 85f, 110f); // yellow-green
        Assert.True(Lighting.ForestFogDay.Saturation >= 0.45f);
        Assert.InRange(Lighting.ForestFogNight.Hue, 195f, 225f);
        Assert.InRange(Lighting.ForestAmbient.Hue, 130f, 170f);
    }

    [Fact]
    public void RgbHueAndSaturation()
    {
        Assert.Equal(0f, new Rgb(1f, 0f, 0f).Hue);
        Assert.Equal(120f, new Rgb(0f, 1f, 0f).Hue);
        Assert.Equal(240f, new Rgb(0f, 0f, 1f).Hue);
        Assert.Equal(300f, new Rgb(1f, 0f, 1f).Hue);
        Assert.Equal(0f, new Rgb(0.5f, 0.5f, 0.5f).Saturation);
        Assert.Equal(0.5f, new Rgb(1f, 0.5f, 0.5f).Saturation, 5);
        Assert.Equal(0f, new Rgb(0f, 0f, 0f).Saturation);
    }

    [Fact]
    public void VividLerpPassesThroughNoGrey()
    {
        var peach = new Rgb(1f, 0.8f, 0.6f);
        var blue = new Rgb(0.6f, 0.8f, 1f);
        Assert.True(peach.Lerp(blue, 0.4f).Saturation < 0.1f); // a plain blend is almost grey
        var mid = peach.VividLerp(blue, 0.4f);
        Assert.Equal(peach.Saturation, mid.Saturation, 3);
        Assert.Equal(peach.Lerp(blue, 0.4f).Value, mid.Value, 5);
        Assert.Equal(peach.Lerp(blue, 0.4f).Hue, mid.Hue, 2);
        // The ends are exact.
        Assert.Equal(peach.R, peach.VividLerp(blue, 0f).R, 5);
        Assert.Equal(blue.B, peach.VividLerp(blue, 1f).B, 5);
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
        Assert.Equal(DayNightModule.WakeSpot.Position, teleport.Position);
        Assert.Equal(DayNightModule.WakeSpot.Yaw, teleport.Facing);
        Assert.Equal(2, Assert.Single(_bus.Wakes).Day);
        // The teleport comes first, then WokeUp.
        int teleportAt = _bus.Published.LastIndexOf(typeof(TeleportPlayer));
        Assert.True(teleportAt >= 0 && _bus.Published.IndexOf(typeof(WokeUp)) == teleportAt + 1);
        Assert.Equal(2, Last.Day);
        Assert.Equal(7f, Last.Hour, 3);
        Assert.False(Last.IsNight);
        Assert.False(_clock.Fainting);
    }

    [Fact]
    public void WakesBesideJeremyOnFreeGroundFacingTheClinic()
    {
        Faint();
        Seconds(3);
        var teleport = Assert.Single(_bus.Teleports);
        var door = Places.ClinicDoor;
        var p = teleport.Position;

        // Not inside Dr. Jeremy, who stands exactly at ClinicDoor.
        float fromJeremy = new Vector2(p.X - door.Position.X, p.Z - door.Position.Z).Length();
        Assert.True(fromJeremy >= 1f, $"wakes {fromJeremy} m from Jeremy");
        Assert.True(fromJeremy <= 6f, $"wakes {fromJeremy} m from Jeremy, not beside him");

        // Standable: walkable with 0.5 m to spare, no obstacle for a 0.5 m walker, on the ground.
        Assert.True(Areas.Walkable(p.X, p.Z, 0.5f));
        Assert.False(Ground.Blocks(p.X, p.Z, 0.5f));
        Assert.Equal(Ground.HeightAt(p.X, p.Z), p.Y, 3);

        // Out on the street: farther from the clinic's front than ClinicDoor.
        var clinic = Buildings.First(BuildingKind.Clinic);
        float OutFromFront(float x, float z) => Vector2.Dot(new Vector2(x, z) - clinic.Center, clinic.Front);
        Assert.True(OutFromFront(p.X, p.Z) > OutFromFront(door.Position.X, door.Position.Z) + 1f);

        // Facing the clinic: within 45 degrees of ClinicDoor's yaw + pi, so the camera behind is outside.
        float diff = MathF.IEEERemainder(teleport.Facing - (door.Yaw + MathF.PI), 2f * MathF.PI);
        Assert.True(MathF.Abs(diff) <= MathF.PI / 4f, $"facing off by {diff} rad");
        // The camera spot about 4 m behind the player is out on the street too, not inside the clinic.
        var behind = new Vector2(p.X + MathF.Sin(teleport.Facing) * 4f, p.Z + MathF.Cos(teleport.Facing) * 4f);
        Assert.True(OutFromFront(behind.X, behind.Y) > OutFromFront(p.X, p.Z));
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

    // ---- Sleeping ----

    private void GoToSleep() => _bus.Publish(new Sleep());

    [Fact]
    public void SleepFadesForThreeSecondsThenSkipsToSevenTheNextDayWithoutTeleportOrWokeUp()
    {
        _clock.SetTime(2, 21.0);
        Ticks(1);
        GoToSleep();
        Assert.True(_clock.Sleeping);
        Assert.False(_clock.Fainting);
        Seconds(1.5);
        Assert.Equal(0.5f, _clock.SleepProgress, 2);
        Ticks(3 * TicksPerSecond / 2 - 1);
        Assert.True(_clock.Sleeping);
        Assert.True(_clock.SleepProgress > 0.99f);
        Assert.Equal(2, Last.Day);

        Ticks(1);
        Assert.False(_clock.Sleeping);
        Assert.Equal(0f, _clock.SleepProgress);
        Assert.Equal(3, Last.Day);
        Assert.Equal(7f, Last.Hour, 3);
        Assert.False(Last.IsNight);
        // The player stays by the bed: no teleport, and no WokeUp (that is waking at the clinic).
        Assert.Empty(_bus.Teleports);
        Assert.Empty(_bus.Wakes);
        Assert.DoesNotContain(typeof(TeleportPlayer), _bus.Published);
        Assert.DoesNotContain(typeof(WokeUp), _bus.Published);

        // Then the clock runs on from the morning.
        Seconds(50);
        Assert.Equal(8f, Last.Hour, 2);
    }

    [Fact]
    public void SleepingInTheSmallHoursWakesThatSameMorning()
    {
        _clock.SetTime(4, 2.0);
        GoToSleep();
        Seconds(3);
        Assert.False(_clock.Sleeping);
        Assert.Equal(4, Last.Day);
        Assert.Equal(7f, Last.Hour, 3);
        Assert.Empty(_bus.Teleports);
    }

    [Fact]
    public void SleepIsIgnoredWhileFaintingOrAlreadySleeping()
    {
        _clock.SetTime(1, 15.0);
        GoToSleep();
        Seconds(2);
        GoToSleep(); // a second Sleep does not start the fade over
        Seconds(1);
        Assert.False(_clock.Sleeping);
        Assert.Equal(2, Last.Day);
        Assert.Equal(7f, Last.Hour, 2);

        // While fainting, Sleep changes nothing: the faint still wakes at the clinic.
        _clock.SetTime(2, 15.0);
        Faint();
        Ticks(30);
        GoToSleep();
        Assert.False(_clock.Sleeping);
        Seconds(3);
        Assert.Single(_bus.Teleports);
        Assert.Equal(3, Assert.Single(_bus.Wakes).Day);
        Assert.False(_clock.Sleeping);
        Assert.Equal(3, Last.Day);
    }

    [Fact]
    public void RestoringDropsASleepInProgress()
    {
        var (other, otherBus, _) = Fresh();
        _clock.SetTime(5, 21.5);
        var saved = Save();

        other.SetTime(2, 10.0);
        otherBus.Publish(new Sleep());
        Assert.True(other.Sleeping);
        otherBus.Published.Clear();
        otherBus.Publish(new RestorePart(saved.Module, saved.Json));
        Assert.Equal(new[] { typeof(RestorePart) }, otherBus.Published);
        Assert.False(other.Sleeping);
        for (int i = 0; i < 5 * TicksPerSecond; i++)
            other.Tick(new TickContext(i, Step));
        // No morning skip: the restored evening just runs on.
        Assert.Equal(5, otherBus.Times[^1].Day);
        Assert.True(otherBus.Times[^1].Hour > 21.5f);
        Assert.Empty(otherBus.Teleports);
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
}
