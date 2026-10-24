using Fungtopia.Input;

namespace Fungtopia.Food;

/// <summary>The Food module's texts for the view (they allocate: build them when what they show changes).</summary>
public static class FoodText
{
    /// <summary>Icons' size in pixels next to the prompt's 30-pixel text.</summary>
    public const int PromptIconSize = 40;

    /// <summary>Icons' size in pixels in the 18-pixel hint lines.</summary>
    public const int HelpIconSize = 28;

    public const string StallName = "Mushroom Munchies";

    /// <summary>The stall's prompt (BBCode): the Interact button of <paramref name="device"/> and "Food" ("E: Food", "X: Food").</summary>
    public static string Prompt(DeviceKind device, int iconSize = PromptIconSize) =>
        $"{Prompts.Rich(PromptAction.Interact, device, iconSize)} Food";

    /// <summary>The menu's hint line (BBCode) for <paramref name="device"/>: Up / Down choose, E / A buy, Q / B close.</summary>
    public static string MenuHelp(DeviceKind device, int iconSize = HelpIconSize) =>
        $"{Prompts.Rich(PromptAction.MenuUp, device, iconSize)}{Prompts.Rich(PromptAction.MenuDown, device, iconSize)} choose{Gap}"
        + $"{Prompts.Rich(Select(device), device, iconSize)} buy{Gap}"
        + $"{Image(Close(device), iconSize)} close";

    /// <summary>The food bar's hint (BBCode): the eat button and the select buttons.</summary>
    public static string BarHelp(DeviceKind device, int iconSize = HelpIconSize) =>
        device == DeviceKind.KeyboardMouse
            ? $"{Prompts.Rich(PromptAction.Eat, device, iconSize)} eat   {Image(Key(1), iconSize)}{Image(Key(2), iconSize)}{Image(Key(3), iconSize)} choose"
            : $"{Prompts.Rich(PromptAction.Eat, device, iconSize)} eat   {Prompts.Rich(PromptAction.FoodPrev, device, iconSize)}{Prompts.Rich(PromptAction.FoodNext, device, iconSize)} choose";

    /// <summary>What a food heals, e.g. "+1 ♥", "+3 ♥", "all ♥".</summary>
    public static string Heals(int kind) =>
        FoodCatalog.HealsFully(kind) ? "all ♥" : $"+{FoodCatalog.Hearts(kind)} ♥";

    /// <summary>A menu line, e.g. "Mushroom Soup   12 M¢   +3 ♥   2/9".</summary>
    public static string MenuLine(int kind, int carried) =>
        $"{FoodCatalog.Name(kind)}   {FoodCatalog.Price(kind)} M¢   {Heals(kind)}   {carried}/{FoodCatalog.MaxEach}";

    /// <summary>Health healed as hearts over the eater's head, e.g. "+3 ♥", "+½ ♥", "+2½ ♥".</summary>
    public static string Healed(int health)
    {
        int whole = health / FoodCatalog.HealthPerHeart;
        bool half = health % FoodCatalog.HealthPerHeart != 0;
        return whole == 0 && half ? "+½ ♥" : half ? $"+{whole}½ ♥" : $"+{whole} ♥";
    }

    public static string BuyAnswer(BuyResult result, int kind) => result switch
    {
        BuyResult.Bought => $"Yum! One {FoodCatalog.Name(kind)} in your bag.",
        BuyResult.TooFewCoins => "Not enough M¢",
        BuyResult.Full => $"You can't carry more than {FoodCatalog.MaxEach}.",
        _ => "",
    };

    // Buy: E on the keyboard (Interact), A / Cross / B on a gamepad (Confirm; Interact works too).
    private static PromptAction Select(DeviceKind device) =>
        device == DeviceKind.KeyboardMouse ? PromptAction.Interact : PromptAction.Confirm;

    /// <summary>Close: Q on the keyboard, the Back button (B / Circle / A) on a gamepad.</summary>
    private static string Close(DeviceKind device) =>
        device == DeviceKind.KeyboardMouse ? Prompts.GlyphDirectory + "key_q.svg" : Prompts.Icon(PromptAction.Back, device);

    /// <summary>The icon of number key <paramref name="number"/> (1..3, selecting a food on the keyboard).</summary>
    public static string Key(int number) => $"{Prompts.GlyphDirectory}key_{number}.svg";

    private static string Image(string path, int size) => $"[img={size}]{path}[/img]";

    private const string Gap = "      ";
}
