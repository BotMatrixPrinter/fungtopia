namespace Fungtopia.DayNight;

/// <summary>
/// The game's clock, published by DayNight once every tick. One in-game day lasts
/// <see cref="RealSecondsPerDay"/> real seconds; night is from <see cref="NightStartHour"/> to
/// <see cref="NightEndHour"/>.
/// </summary>
public readonly struct TimeOfDay
{
    /// <summary>Real seconds in one in-game day (20 minutes).</summary>
    public const float RealSecondsPerDay = 1200f;

    /// <summary>Night begins at 20:00.</summary>
    public const float NightStartHour = 20f;

    /// <summary>Night ends at 6:00.</summary>
    public const float NightEndHour = 6f;

    /// <summary>Day number, starting at 1.</summary>
    public readonly int Day;

    /// <summary>Hour of the day, 0 (midnight) up to but not including 24.</summary>
    public readonly float Hour;

    /// <summary>Whether <see cref="Hour"/> is in the night window (20:00-6:00).</summary>
    public readonly bool IsNight;

    public TimeOfDay(int day, float hour, bool isNight)
    {
        Day = day;
        Hour = hour;
        IsNight = isNight;
    }

    /// <summary>Whether <paramref name="hour"/> (0..24) is night: at or after 20:00 or before 6:00.</summary>
    public static bool IsNightAt(float hour) => hour >= NightStartHour || hour < NightEndHour;
}

/// <summary>Published once when the player wakes up at Dr. Jeremy's clinic after fainting.</summary>
public readonly struct WokeUp
{
    /// <summary>The day the player wakes on.</summary>
    public readonly int Day;

    public WokeUp(int day)
    {
        Day = day;
    }
}
