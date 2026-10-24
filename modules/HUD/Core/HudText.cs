using System.Globalization;
using Fungtopia.Map;

namespace Fungtopia.HUD;

/// <summary>The HUD's texts. The view formats them only when a value changes.</summary>
public static class HudText
{
    /// <summary>Shown over the fade to black after <c>PlayerDefeated</c>.</summary>
    public const string Fainted = "You fainted...";

    /// <summary>The coin counter: "M¢ 1,250".</summary>
    public static string Coins(int coins) => "M¢ " + coins.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>The floating reward: "+50".</summary>
    public static string Earned(int amount) => "+" + amount.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>The place-name banner's text; empty for the roads between places (no banner).</summary>
    public static string AreaName(Area area) => area switch
    {
        Area.Town => "Mushroom Town",
        Area.Forest => "Mushroom Forest",
        Area.PetCity => "Pet City",
        Area.Home => "Home",
        Area.DeepForest => "Deep Forest",
        _ => "",
    };

    /// <summary>Places whose banner gets the spookier style (darker cap, pale text): the Deep Forest.</summary>
    public static bool IsSpooky(Area area) => area == Area.DeepForest;

    /// <summary>The clock's day: "Day 3".</summary>
    public static string Day(int day) => "Day " + day.ToString(CultureInfo.InvariantCulture);

    /// <summary>The clock's time from minutes after midnight (0..1439): "9:05 AM", "12:00 PM", "12:30 AM".</summary>
    public static string Time(int minuteOfDay)
    {
        int hour = minuteOfDay / 60 % 24, minute = minuteOfDay % 60;
        int shown = hour % 12 == 0 ? 12 : hour % 12;
        return shown.ToString(CultureInfo.InvariantCulture) + ":" + minute.ToString("00", CultureInfo.InvariantCulture)
            + (hour < 12 ? " AM" : " PM");
    }

    /// <summary>Shown as the screen comes back after a faint: "Day 4 - you wake up at Dr. Jeremy's".</summary>
    public static string WokeUp(int day) => Day(day) + " - you wake up at Dr. Jeremy's";

    /// <summary>The quest tracker: "Defeat 5 Mushroom Caterpillars 3/5".</summary>
    public static string Quest(string title, int progress, int goal) =>
        title + " " + progress.ToString(CultureInfo.InvariantCulture) + "/" + goal.ToString(CultureInfo.InvariantCulture);
}
