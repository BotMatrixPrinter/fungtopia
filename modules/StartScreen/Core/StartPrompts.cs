using System.Text;
using Fungtopia.Input;

namespace Fungtopia.StartScreen;

/// <summary>
/// The control hint under each step, as RichTextLabel BBCode: the buttons of one device only, as
/// icons (<see cref="Prompts.Rich"/>), each group followed by what it does, for example
/// "[left][right] Choose   [Enter] OK   [Esc] Back" on keyboard or "[A] OK" on Xbox. The look
/// step's hint has two lines (turning, then the rows). Built when the step, the device or the
/// icon size changes, not per tick.
/// </summary>
public static class StartPrompts
{
    /// <summary>What goes between two groups of a line.</summary>
    public const string Gap = "      ";

    /// <summary>The hint of <paramref name="step"/> for <paramref name="device"/>, icons <paramref name="iconSize"/> pixels wide ("" once started).</summary>
    public static string Hint(StartStep step, DeviceKind device, int iconSize)
    {
        if (step == StartStep.Started)
            return "";
        var text = new StringBuilder();
        if (step == StartStep.Look)
        {
            // Keyboard and mouse turn with Q / E or a drag; a pad with LB / RB or the right stick.
            Icons(text, device, iconSize, PromptAction.TurnLeft, PromptAction.TurnRight);
            text.Append(" or ");
            Icons(text, device, iconSize, PromptAction.Look);
            text.Append(device == DeviceKind.KeyboardMouse ? " Drag to turn\n" : " Turn\n");
            Group(text, device, iconSize, "Row", PromptAction.MenuUp, PromptAction.MenuDown);
            Group(text, device, iconSize, "Change", PromptAction.MenuLeft, PromptAction.MenuRight);
            Group(text, device, iconSize, "Next", PromptAction.Confirm);
        }
        else if (step == StartStep.Ready)
            Group(text, device, iconSize, "Start!", PromptAction.Confirm);
        else
        {
            Group(text, device, iconSize, "Choose", PromptAction.MenuLeft, PromptAction.MenuRight);
            Group(text, device, iconSize, "OK", PromptAction.Confirm);
        }
        if (step != StartStep.Title)
            Group(text, device, iconSize, "Back", PromptAction.Back);
        return text.ToString();
    }

    private static void Group(StringBuilder text, DeviceKind device, int size, string what, params PromptAction[] actions)
    {
        if (text.Length > 0 && text[text.Length - 1] != '\n')
            text.Append(Gap);
        Icons(text, device, size, actions);
        text.Append(' ').Append(what);
    }

    private static void Icons(StringBuilder text, DeviceKind device, int size, params PromptAction[] actions)
    {
        foreach (var action in actions)
            text.Append(Prompts.Rich(action, device, size));
    }
}
