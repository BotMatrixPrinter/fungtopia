using Fungtopia.Input;

namespace Fungtopia.Home;

/// <summary>The Home module's texts for the view.</summary>
public static class HomeText
{
    /// <summary>Icons' size in pixels next to the prompt's 30-pixel text.</summary>
    public const int PromptIconSize = 40;

    /// <summary>Icons' size in pixels in the 18-pixel hint lines.</summary>
    public const int HelpIconSize = 28;

    /// <summary>The R key's icon (decorating's turn, and put away, on the keyboard).</summary>
    public const string RIcon = Prompts.GlyphDirectory + "key_r.svg";

    /// <summary>The picker's last line: placement mode with nothing held.</summary>
    public const string MovePlacedLine = "Move placed items";

    /// <summary>The picker's line when every owned item stands in the house.</summary>
    public const string EverythingPlaced = "Everything is placed";

    /// <summary>What a prompt does, e.g. "Enter".</summary>
    public static string PromptVerb(HomePrompt prompt) => prompt switch
    {
        HomePrompt.Enter => "Enter",
        HomePrompt.Leave => "Leave",
        HomePrompt.Shop => "Shop",
        HomePrompt.Decorate => "Decorate",
        HomePrompt.Wardrobe => "Wardrobe",
        HomePrompt.TakeCat => "Take Mushroom Cat along",
        HomePrompt.TakeDog => "Take Mushroom Dog along",
        HomePrompt.TakeCrow => "Take Mushroom Crow along",
        HomePrompt.Sleep => "Sleep",
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

    /// <summary>The decorating picker's hint line (BBCode) for <paramref name="device"/>; <c>HomeView</c> reads these buttons.</summary>
    public static string PickerHelp(DeviceKind device, int iconSize = HelpIconSize) =>
        $"{Icons(device, iconSize, PromptAction.MenuUp, PromptAction.MenuDown)} choose{Gap}"
        + $"{Prompts.Rich(Select(device), device, iconSize)} take{Gap}"
        + $"{Prompts.Rich(PromptAction.Back, device, iconSize)} done";

    /// <summary>Placement mode's hint line with an item held (BBCode) for <paramref name="device"/>; <c>HomeView</c> reads these buttons.</summary>
    public static string DecorateHelp(DeviceKind device, int iconSize = HelpIconSize) =>
        $"{Prompts.Rich(PromptAction.Move, device, iconSize)} move{Gap}"
        + $"{Turn(device, iconSize)} turn{Gap}"
        + $"{Prompts.Rich(Select(device), device, iconSize)} place{Gap}"
        + $"{Prompts.Rich(PromptAction.Back, device, iconSize)} back";

    /// <summary>
    /// Placement mode's hint line with nothing held (BBCode) for <paramref name="device"/>: over a
    /// placed item (<paramref name="overItem"/>) also "[Interact] Pick up" and "[Put away] Put away".
    /// </summary>
    public static string MoveHelp(DeviceKind device, bool overItem, int iconSize = HelpIconSize) =>
        $"{Prompts.Rich(PromptAction.Move, device, iconSize)} move{Gap}"
        + (overItem
            ? $"{Prompts.Rich(Select(device), device, iconSize)} Pick up{Gap}{PutAwayIcon(device, iconSize)} Put away{Gap}"
            : "")
        + $"{Prompts.Rich(PromptAction.Back, device, iconSize)} back";

    /// <summary>The picker's hint line while the mouse is in use (BBCode): point to choose, "Left click take", Back.</summary>
    public static string PickerMouseHelp(int iconSize = HelpIconSize) =>
        $"{Prompts.Rich(PromptAction.Look, DeviceKind.KeyboardMouse, iconSize)} choose{Gap}"
        + $"Left click take{Gap}"
        + $"{Prompts.Rich(PromptAction.Back, DeviceKind.KeyboardMouse, iconSize)} done";

    /// <summary>
    /// Placement mode's hint line while the mouse is in use (BBCode): with an item held (<paramref name="held"/>)
    /// "Left click Place" and "Middle click Rotate"; with nothing held, over a placed item
    /// (<paramref name="overItem"/>), "Left click Pick up" and "[Put away] Put away"; Back always.
    /// </summary>
    public static string MouseHelp(bool held, bool overItem, int iconSize = HelpIconSize) =>
        $"{Prompts.Rich(PromptAction.Look, DeviceKind.KeyboardMouse, iconSize)} move{Gap}"
        + (held
            ? $"Left click Place{Gap}Middle click Rotate{Gap}"
            : overItem ? $"Left click Pick up{Gap}{PutAwayIcon(DeviceKind.KeyboardMouse, iconSize)} Put away{Gap}" : "")
        + $"{Prompts.Rich(PromptAction.Back, DeviceKind.KeyboardMouse, iconSize)} back";

    /// <summary>
    /// The put-away button's icon: R on the keyboard, the left face button on a gamepad (X on Xbox,
    /// Square on PlayStation, Y on Nintendo; Godot's <c>JoyButton.X</c>).
    /// </summary>
    public static string PutAwayIcon(DeviceKind device, int size = HelpIconSize) => device switch
    {
        DeviceKind.KeyboardMouse => Image(RIcon, size),
        DeviceKind.PlayStation => Image(Prompts.GlyphDirectory + "ps_square.svg", size),
        DeviceKind.Nintendo => Image(Prompts.GlyphDirectory + "ns_y.svg", size),
        _ => Image(Prompts.GlyphDirectory + "xbox_x.svg", size),
    };

    /// <summary>A line of the decorating picker, e.g. "Chair x2" (allocates: build it when the picker changes).</summary>
    public static string PickerLine(PickerEntry entry) => $"{Furniture.Name(entry.Kind)} x{entry.Count}";

    // Buy / place: E on the keyboard (Interact), A / Cross / B on a gamepad (Confirm: it arrives as Interact while a panel is open; X / Square works too).
    private static PromptAction Select(DeviceKind device) =>
        device == DeviceKind.KeyboardMouse ? PromptAction.Interact : PromptAction.Confirm;

    private static string Turn(DeviceKind device, int size) =>
        device == DeviceKind.KeyboardMouse ? Image(RIcon, size) : Prompts.Rich(PromptAction.TurnRight, device, size);

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
