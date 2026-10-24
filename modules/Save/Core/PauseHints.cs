using Fungtopia.Input;

namespace Fungtopia.Save;

/// <summary>
/// The pause menu's hint line, as RichTextLabel BBCode with the button icons of one device only
/// (<see cref="Prompts.Rich"/>): "[icon] Choose  [icons] Move  [icon] Resume" (or "Back" on the Load pages).
/// </summary>
public static class PauseHints
{
    /// <summary>Space between two hints.</summary>
    public const string Gap = "      ";

    /// <summary>
    /// The hints for <paramref name="device"/>: on the first page (<paramref name="mainPage"/>)
    /// Pause resumes, on the Load pages Back goes back. Icons are <paramref name="iconSize"/> pixels.
    /// </summary>
    public static string Line(DeviceKind device, bool mainPage, int iconSize = 28)
    {
        string choose = Prompts.Rich(PromptAction.Confirm, device, iconSize) + " Choose";
        string move = Prompts.Rich(PromptAction.MenuUp, device, iconSize) + Prompts.Rich(PromptAction.MenuDown, device, iconSize) + " Move";
        string leave = mainPage
            ? Prompts.Rich(PromptAction.Pause, device, iconSize) + " Resume"
            : Prompts.Rich(PromptAction.Back, device, iconSize) + " Back";
        return move + Gap + choose + Gap + leave;
    }
}
