using Fungtopia.Input;

namespace Fungtopia.Home;

/// <summary>The Home module's texts for the view.</summary>
public static class HomeText
{
    /// <summary>Icons' size in pixels next to the prompt's 30-pixel text.</summary>
    public const int PromptIconSize = 40;

    /// <summary>Icons' size in pixels in the 18-pixel hint lines.</summary>
    public const int HelpIconSize = 28;

    /// <summary>The Tab key's icon (the Input module has none); <c>HomePanels</c> makes it loadable.</summary>
    public const string TabIcon = "res://modules/Home/Godot/Glyphs/key_tab.svg";

    /// <summary>The R key's icon (decorating's turn on the keyboard).</summary>
    public const string RIcon = Prompts.GlyphDirectory + "key_r.svg";

    /// <summary>What a prompt does, e.g. "Enter".</summary>
    public static string PromptVerb(HomePrompt prompt) => prompt switch
    {
        HomePrompt.Enter => "Enter",
        HomePrompt.Leave => "Leave",
        HomePrompt.Shop => "Shop",
        HomePrompt.Decorate => "Decorate",
        HomePrompt.Wardrobe => "Wardrobe",
        _ => "",
    };

    /// <summary>
    /// RichTextLabel BBCode of the prompt for <paramref name="device"/> only: the Interact button's
    /// icon and the verb, e.g. "[img=40]res://.../xbox_x.svg[/img] Enter"; "" for no prompt (allocates:
    /// build it when the prompt or the device changes).
    /// </summary>
    public static string Prompt(HomePrompt prompt, DeviceKind device, int iconSize = PromptIconSize) =>
        prompt == HomePrompt.None ? "" : $"{Prompts.Rich(PromptAction.Interact, device, iconSize)} {PromptVerb(prompt)}";

    /// <summary>The shop panel's hint line (BBCode) for <paramref name="device"/>; <c>HomeView</c> reads these buttons.</summary>
    public static string ShopHelp(DeviceKind device, int iconSize = HelpIconSize) =>
        $"{Icons(device, iconSize, PromptAction.MenuUp, PromptAction.MenuDown)} choose{Gap}"
        + $"{Prompts.Rich(Select(device), device, iconSize)} buy{Gap}"
        + $"{Prompts.Rich(PromptAction.Back, device, iconSize)} close";

    /// <summary>Placement mode's hint line (BBCode) for <paramref name="device"/>; <c>HomeView</c> reads these buttons.</summary>
    public static string DecorateHelp(DeviceKind device, int iconSize = HelpIconSize) =>
        $"{Prompts.Rich(PromptAction.Move, device, iconSize)} move{Gap}"
        + $"{Turn(device, iconSize)} turn{Gap}"
        + $"{NextItem(device, iconSize)} next item{Gap}"
        + $"{Prompts.Rich(Select(device), device, iconSize)} place / pick up{Gap}"
        + $"{Prompts.Rich(PromptAction.Back, device, iconSize)} done";

    /// <summary>Placement mode's title with nothing held but unplaced items waiting (BBCode).</summary>
    public static string PickUpTitle(DeviceKind device, int iconSize = PromptIconSize) =>
        $"Decorating: pick something up, or {NextItem(device, iconSize)} for an item";

    // Buy / place: E on the keyboard (Interact), A / Cross / B on a gamepad (Confirm: it arrives as Interact while a panel is open; X / Square works too).
    private static PromptAction Select(DeviceKind device) =>
        device == DeviceKind.KeyboardMouse ? PromptAction.Interact : PromptAction.Confirm;

    private static string Turn(DeviceKind device, int size) =>
        device == DeviceKind.KeyboardMouse ? Image(RIcon, size) : Prompts.Rich(PromptAction.TurnRight, device, size);

    private static string NextItem(DeviceKind device, int size) =>
        device == DeviceKind.KeyboardMouse ? Image(TabIcon, size) : Prompts.Rich(PromptAction.TurnLeft, device, size);

    private static string Icons(DeviceKind device, int size, PromptAction first, PromptAction second) =>
        Prompts.Rich(first, device, size) + Prompts.Rich(second, device, size);

    private static string Image(string path, int size) => $"[img={size}]{path}[/img]";

    private const string Gap = "      ";

    /// <summary>A line of the shop's catalog, e.g. "Lamp  150 M¢" (allocates: build it once).</summary>
    public static string CatalogLine(FurnitureKind kind) => $"{Furniture.Name(kind)}  {Furniture.Price(kind):N0} M¢";

    public static string ShopAnswer(ShopResult result, FurnitureKind kind) => result switch
    {
        ShopResult.Bought => $"Bought a {Furniture.Name(kind)}! Place it with the catalog book at home.",
        ShopResult.TooFewCoins => $"Not enough M¢ for the {Furniture.Name(kind)}.",
        ShopResult.Full => "Your house can't hold any more furniture.",
        _ => "",
    };
}
