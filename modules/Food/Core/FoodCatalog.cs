using Fungtopia.Player;

namespace Fungtopia.Food;

/// <summary>
/// What Mushroom Munchies sells, by <see cref="FoodKinds"/> number: names, prices (M¢) and
/// healing. Cheap on purpose: one 50 M¢ monster buys about 10 muffins, 4 soups or a pie.
/// </summary>
public static class FoodCatalog
{
    /// <summary>How many of each kind the bag holds.</summary>
    public const int MaxEach = 9;

    /// <summary>Health per heart (the HUD draws one heart per 2 health).</summary>
    public const int HealthPerHeart = 2;

    public static bool Valid(int kind) => kind >= 0 && kind < FoodKinds.Count;

    public static string Name(int kind) => kind switch
    {
        FoodKinds.Muffin => "Berry Muffin",
        FoodKinds.Soup => "Mushroom Soup",
        FoodKinds.Pie => "Spore Pie",
        _ => "",
    };

    /// <summary>Price in Mushroom Coins.</summary>
    public static int Price(int kind) => kind switch
    {
        FoodKinds.Muffin => 5,
        FoodKinds.Soup => 12,
        FoodKinds.Pie => 35,
        _ => 0,
    };

    /// <summary>Hearts healed; 0 for the pie, which heals fully (see <see cref="HealsFully"/>).</summary>
    public static int Hearts(int kind) => kind switch
    {
        FoodKinds.Muffin => 1,
        FoodKinds.Soup => 3,
        _ => 0,
    };

    public static bool HealsFully(int kind) => kind == FoodKinds.Pie;

    /// <summary>The <see cref="HealPlayer.Amount"/> eating it sends: 2 per heart, <see cref="HealPlayer.Full"/> (999) for the pie.</summary>
    public static int HealAmount(int kind) =>
        HealsFully(kind) ? HealPlayer.Full : Hearts(kind) * HealthPerHeart;
}
