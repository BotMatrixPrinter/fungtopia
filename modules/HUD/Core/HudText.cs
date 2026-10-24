using System.Globalization;

namespace Fungtopia.HUD;

/// <summary>The HUD's texts. The view formats them only when a value changes.</summary>
public static class HudText
{
    /// <summary>Shown over the soft fade after <c>PlayerDefeated</c>.</summary>
    public const string Fainted = "You fainted...";

    /// <summary>The coin counter: "M¢ 1,250".</summary>
    public static string Coins(int coins) => "M¢ " + coins.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>The floating reward: "+50".</summary>
    public static string Earned(int amount) => "+" + amount.ToString("N0", CultureInfo.InvariantCulture);
}
