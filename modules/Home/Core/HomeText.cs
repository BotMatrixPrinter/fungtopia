namespace Fungtopia.Home;

/// <summary>The Home module's texts for the view.</summary>
public static class HomeText
{
    public static string Prompt(HomePrompt prompt) => prompt switch
    {
        HomePrompt.Enter => "E / X: Enter",
        HomePrompt.Leave => "E / X: Leave",
        HomePrompt.Shop => "E / X: Shop",
        HomePrompt.Decorate => "E / X: Decorate",
        HomePrompt.Wardrobe => "E / X: Wardrobe",
        _ => "",
    };

    /// <summary>A line of the shop's catalog, e.g. "Lamp  150 M¢" (allocates: build it once).</summary>
    public static string CatalogLine(FurnitureKind kind) => $"{Furniture.Name(kind)}  {Furniture.Price(kind):N0} M¢";

    public static string ShopAnswer(ShopResult result, FurnitureKind kind) => result switch
    {
        ShopResult.Bought => $"Bought a {Furniture.Name(kind)}! Place it with the catalog book at home.",
        ShopResult.TooFewCoins => $"Not enough M¢ for the {Furniture.Name(kind)}.",
        ShopResult.Full => "Your house can't hold any more furniture.",
        _ => "",
    };

    public const string ShopHelp = "Up / Down: choose    E / X: buy    Q / B: close";

    public const string DecorateHelp =
        "Arrows / D-pad: move    R / RB: turn    Tab / LB: next item    E / X: place / pick up    Q / B: done";
}
