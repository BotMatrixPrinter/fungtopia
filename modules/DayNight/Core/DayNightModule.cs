using System;
using Fungtopia.Map;
using Fungtopia.Player;
using Fungtopia.Shell;

namespace Fungtopia.DayNight;

/// <summary>
/// The day/night cycle. Advances the clock (one day per 20 real minutes, from Day 1 at 9:00) and
/// publishes <see cref="TimeOfDay"/> every tick. When the player faints
/// (<see cref="PlayerDefeated"/>) it waits <see cref="FaintSeconds"/>, skips to the next
/// morning at <see cref="WakeHour"/>, and sends the player to Dr. Jeremy's clinic
/// (<see cref="TeleportPlayer"/> to <see cref="Places.ClinicDoor"/>) with one <see cref="WokeUp"/>.
/// </summary>
public sealed class DayNightModule : IModule, IMessageHandler<PlayerDefeated>, IMessageHandler<PlayerState>
{
    /// <summary>The hour Day 1 starts at.</summary>
    public const double StartHour = 9.0;

    /// <summary>The hour the player wakes at the clinic after fainting.</summary>
    public const double WakeHour = 7.0;

    /// <summary>Real seconds from fainting to waking (the screen fades meanwhile).</summary>
    public const double FaintSeconds = 3.0;

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

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        bus.Subscribe<PlayerDefeated>(this);
        bus.Subscribe<PlayerState>(this);
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
        var clinic = Places.ClinicDoor;
        _bus!.Publish(new TeleportPlayer(clinic.Position, clinic.Yaw));
        _bus.Publish(new WokeUp(Day));
    }
}
