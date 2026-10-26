using System;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fungtopia.Map;
using Fungtopia.Player;
using Fungtopia.Save;
using Fungtopia.Shell;

namespace Fungtopia.DayNight;

/// <summary>
/// The day/night cycle. Advances the clock (one day per 20 real minutes, from Day 1 at 9:00) and
/// publishes <see cref="TimeOfDay"/> every tick. When the player faints
/// (<see cref="PlayerDefeated"/>) it waits <see cref="FaintSeconds"/>, skips to the next
/// morning at <see cref="WakeHour"/>, and sends the player to Dr. Jeremy's clinic
/// (<see cref="TeleportPlayer"/> to <see cref="WakeSpot"/>, beside Jeremy facing the clinic) with one <see cref="WokeUp"/>.
/// It saves its day and hour as one <see cref="SavePart"/> and restores them from a <see cref="RestorePart"/>.
/// </summary>
public sealed class DayNightModule : IModule, IMessageHandler<PlayerDefeated>, IMessageHandler<PlayerState>,
    IMessageHandler<CollectSave>, IMessageHandler<RestorePart>
{
    /// <summary>The version written in the saved JSON; other versions are not restored.</summary>
    public const int SaveVersion = 1;

    /// <summary>The hour Day 1 starts at.</summary>
    public const double StartHour = 9.0;

    /// <summary>The hour the player wakes at the clinic after fainting.</summary>
    public const double WakeHour = 7.0;

    /// <summary>Real seconds from fainting to waking (the screen fades meanwhile).</summary>
    public const double FaintSeconds = 3.0;

    /// <summary>How far further out from the clinic than <see cref="Places.ClinicDoor"/> the player wakes (meters).</summary>
    public const float WakeOut = 2.5f;

    /// <summary>How far to one side of <see cref="Places.ClinicDoor"/> the player wakes (meters).</summary>
    public const float WakeSide = 1.2f;

    /// <summary>The walker radius (and walkable margin) a wake spot must be free for.</summary>
    public const float WakeClearance = 0.5f;

    /// <summary>
    /// Where the player wakes after fainting: beside Dr. Jeremy (who stands at <see cref="Places.ClinicDoor"/>),
    /// <see cref="WakeOut"/> further out and <see cref="WakeSide"/> to one side, facing the clinic
    /// (<c>ClinicDoor.Yaw + pi</c>), so the camera behind the player is out on the street. The first free spot
    /// of: the right side, the left side, then each again 0.5 m further out (up to 3 m more).
    /// </summary>
    public static readonly Place WakeSpot = FindWakeSpot();

    private const double SecondsPerHour = TimeOfDay.RealSecondsPerDay / 24.0;

    private IMessageBus? _bus;
    private double _secondsIntoDay = StartHour * SecondsPerHour;
    private double _faintElapsed = -1.0;

    /// <summary>Day number, from 1.</summary>
    public int Day { get; private set; } = 1;

    /// <summary>Hour of the day, 0 up to 24 (double precision; the message carries a float).</summary>
    public double Hour => _secondsIntoDay / SecondsPerHour;

    /// <summary><see cref="Hour"/> as <see cref="TimeOfDay"/> carries it: a float kept below 24.</summary>
    public float HourF => MathF.Min((float)Hour, MathF.BitDecrement(24f));

    public bool IsNight => TimeOfDay.IsNightAt(HourF);

    /// <summary>Whether the player has fainted and is waiting to wake at the clinic.</summary>
    public bool Fainting => _faintElapsed >= 0.0;

    /// <summary>0 while awake; rises from 0 to 1 over the <see cref="FaintSeconds"/> of a faint.</summary>
    public float FaintProgress => Fainting ? (float)Math.Min(1.0, _faintElapsed / FaintSeconds) : 0f;

    /// <summary>The area the player was last in (from <see cref="PlayerState"/>); the view tunes its mood to it.</summary>
    public Area PlayerArea { get; private set; } = Area.Town;

    /// <summary>Number of fixed steps ticked.</summary>
    public long Ticks { get; private set; }

    /// <summary>Where a refused <see cref="RestorePart"/> is logged (one line each); the console by default.</summary>
    public Action<string> Log { get; set; } = Console.WriteLine;

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        bus.Subscribe<PlayerDefeated>(this);
        bus.Subscribe<PlayerState>(this);
        bus.Subscribe<CollectSave>(this);
        bus.Subscribe<RestorePart>(this);
    }

    /// <summary>Sets the clock (tests, screenshots). <paramref name="hour"/> wraps into 0..24.</summary>
    public void SetTime(int day, double hour)
    {
        if (day < 1)
            throw new ArgumentOutOfRangeException(nameof(day), day, "days start at 1");
        hour %= 24.0;
        if (hour < 0.0)
            hour += 24.0;
        Day = day;
        _secondsIntoDay = hour * SecondsPerHour;
    }

    public void Handle(in PlayerDefeated message)
    {
        if (!Fainting)
            _faintElapsed = 0.0;
    }

    public void Handle(in PlayerState message) => PlayerArea = message.Area;

    /// <summary>Answers at once with this module's <see cref="SavePart"/>: the day and hour as compact JSON.</summary>
    public void Handle(in CollectSave message)
    {
        if (_bus == null)
            throw new InvalidOperationException("DayNightModule got CollectSave before Start.");
        string json = JsonSerializer.Serialize(new DayNightSave(SaveVersion, Day, Hour));
        _bus.Publish(new SavePart(message.RequestId, SaveModules.DayNight, json));
    }

    /// <summary>
    /// Restores the day and hour saved by <see cref="Handle(in CollectSave)"/>, quietly: nothing is
    /// published (no <see cref="WokeUp"/>, no teleport) and a faint in progress is dropped. Bad JSON, an
    /// unknown version or out-of-range values put the clock back to its defaults (Day 1, 9:00) and log one line.
    /// </summary>
    public void Handle(in RestorePart message)
    {
        if (message.Module != SaveModules.DayNight)
            return;
        _faintElapsed = -1.0;
        DayNightSave? save = null;
        try
        {
            if (!string.IsNullOrEmpty(message.Json))
                save = JsonSerializer.Deserialize<DayNightSave>(message.Json);
        }
        catch (JsonException)
        {
        }

        if (save is { Version: SaveVersion, Day: >= 1 } s && double.IsFinite(s.Hour) && s.Hour >= 0.0 && s.Hour < 24.0)
        {
            Day = s.Day;
            _secondsIntoDay = s.Hour * SecondsPerHour;
            return;
        }

        Day = 1;
        _secondsIntoDay = StartHour * SecondsPerHour;
        Log(save == null
            ? "Fungtopia: DayNight: saved time unreadable, starting at Day 1, 9:00."
            : $"Fungtopia: DayNight: saved time version {save.Version} (day {save.Day}, hour {save.Hour}) not restored, starting at Day 1, 9:00.");
    }

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("DayNightModule.Tick before Start.");
        Ticks++;
        double step = context.StepSeconds;
        _secondsIntoDay += step;
        while (_secondsIntoDay >= TimeOfDay.RealSecondsPerDay)
        {
            _secondsIntoDay -= TimeOfDay.RealSecondsPerDay;
            Day++;
        }

        if (Fainting)
        {
            _faintElapsed += step;
            // A small tolerance so 3 s of 1/60 s steps (180 ticks) wakes on the 180th.
            if (_faintElapsed >= FaintSeconds - 1e-9)
                WakeAtClinic();
        }

        _bus.Publish(new TimeOfDay(Day, HourF, IsNight));
    }

    private void WakeAtClinic()
    {
        _faintElapsed = -1.0;
        // The next morning: later the same day when fainting in the small hours, else tomorrow.
        if (Hour >= WakeHour)
            Day++;
        _secondsIntoDay = WakeHour * SecondsPerHour;
        var wake = WakeSpot;
        _bus!.Publish(new TeleportPlayer(wake.Position, wake.Yaw));
        _bus.Publish(new WokeUp(Day));
    }

    private static Place FindWakeSpot()
    {
        var door = Places.ClinicDoor;
        var outward = door.Facing;
        var side = new Vector3(-outward.Z, 0f, outward.X);
        float yaw = door.Yaw + MathF.PI;
        if (yaw > MathF.PI)
            yaw -= 2f * MathF.PI;
        for (int step = 0; step <= 6; step++)
        {
            float o = WakeOut + 0.5f * step;
            for (int s = 1; s >= -1; s -= 2)
            {
                float x = door.Position.X + outward.X * o + side.X * WakeSide * s;
                float z = door.Position.Z + outward.Z * o + side.Z * WakeSide * s;
                if (Areas.Walkable(x, z, WakeClearance) && !Ground.Blocks(x, z, WakeClearance))
                    return new Place(x, z, yaw);
            }
        }

        // No free spot (the town would have to change a lot): the first choice anyway, still beside Jeremy.
        return new Place(door.Position.X + outward.X * WakeOut + side.X * WakeSide,
            door.Position.Z + outward.Z * WakeOut + side.Z * WakeSide, yaw);
    }
}

/// <summary>DayNight's saved state: the version (<see cref="DayNightModule.SaveVersion"/>), the day and the hour.</summary>
internal sealed record DayNightSave(
    [property: JsonPropertyName("v")] int Version,
    [property: JsonPropertyName("day")] int Day,
    [property: JsonPropertyName("hour")] double Hour);
