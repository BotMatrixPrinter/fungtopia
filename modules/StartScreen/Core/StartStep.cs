namespace Fungtopia.StartScreen;

/// <summary>The steps of the start screen, in order.</summary>
public enum StartStep
{
    /// <summary>The "Fungtopia" logo over Mushroom Town: Confirm goes on.</summary>
    Title,
    /// <summary>"Choose your character": the Girl and the Boy.</summary>
    Character,
    /// <summary>"Choose your starter pet": the Mushroom Cat and the Mushroom Dog.</summary>
    Pet,
    /// <summary>Both choices and the "Start!" button.</summary>
    Ready,
    /// <summary>"Start!" was pressed: the screen is closed for good.</summary>
    Started,
}

/// <summary>What the view reports from mouse, keyboard and gamepad.</summary>
public enum StartButton
{
    /// <summary>Arrow left, stick or D-pad left: the left choice (Girl, Cat).</summary>
    Left,
    /// <summary>Arrow right, stick or D-pad right: the right choice (Boy, Dog).</summary>
    Right,
    /// <summary>Enter, E, gamepad A or a click: go on with the highlighted choice.</summary>
    Confirm,
    /// <summary>Esc, Backspace, gamepad B or the back arrow: one step back.</summary>
    Back,
}
