namespace Fungtopia.StartScreen;

/// <summary>
/// The steps of the start screen: <see cref="Title"/> to <see cref="Started"/> in the order of a
/// new game; the save menus come after (they are not reached by counting on).
/// </summary>
public enum StartStep
{
    /// <summary>The "Fungtopia" logo over Mushroom Town with Continue, Load and New Game.</summary>
    Title,
    /// <summary>"Choose your character": the Girl and the Boy.</summary>
    Character,
    /// <summary>"Make your look": skin tone, hair style, hair color, eye color and a starter outfit.</summary>
    Look,
    /// <summary>"Choose your starter pet": the Mushroom Cat and the Mushroom Dog.</summary>
    Pet,
    /// <summary>Both choices and the "Start!" button.</summary>
    Ready,
    /// <summary>"Start!" was pressed, or a save was loaded: the screen is closed for good.</summary>
    Started,
    /// <summary>"Load a game": the three slots; one with a readable save goes on to <see cref="LoadSave"/>.</summary>
    Load,
    /// <summary>The picked slot's manual save and autosave: choosing one loads it.</summary>
    LoadSave,
    /// <summary>"New game": pick slot 1..3; an occupied one asks <see cref="Overwrite"/>, an empty one goes to <see cref="Character"/>.</summary>
    NewSlot,
    /// <summary>"Overwrite?": Yes goes to <see cref="Character"/>, No back to <see cref="NewSlot"/>.</summary>
    Overwrite,
}

/// <summary>The items of the <see cref="StartStep.Title"/> menu, top to bottom (the <see cref="StartScreenModule.Highlight"/> values).</summary>
public enum TitleItem
{
    /// <summary>Loads the newest save of any slot; hidden when there is none.</summary>
    Continue,
    /// <summary>Goes to <see cref="StartStep.Load"/>; greyed when there is nothing to load.</summary>
    Load,
    /// <summary>Goes to <see cref="StartStep.NewSlot"/>.</summary>
    NewGame,
}

/// <summary>The rows of the <see cref="StartStep.Look"/> step, top to bottom.</summary>
public enum LookRow
{
    /// <summary>An index in <c>Fungtopia.Wardrobe.Looks.SkinTones</c>.</summary>
    Skin,
    /// <summary>An index in <c>Fungtopia.Wardrobe.Looks.HairStyles</c>.</summary>
    HairStyle,
    /// <summary>An index in <c>Fungtopia.Wardrobe.Looks.HairColors</c>.</summary>
    HairColor,
    /// <summary>An index in <c>Fungtopia.Wardrobe.Looks.EyeColors</c>.</summary>
    EyeColor,
    /// <summary>An index in <c>Fungtopia.Wardrobe.Catalog.StarterOutfits</c> (only those that fit the character).</summary>
    Outfit,
}

/// <summary>
/// The presses the screen applies: <see cref="Confirm"/> and <see cref="Back"/> from the view
/// (<see cref="StartScreenModule.Press"/>), the four directions from the Input module's
/// <c>Fungtopia.Input.MenuNav</c> steps only.
/// </summary>
public enum StartButton
{
    /// <summary>A MenuNav step left: the left choice (Girl, Cat), the previous value of a look row, or the previous menu item.</summary>
    Left,
    /// <summary>A MenuNav step right: the right choice (Boy, Dog), the next value of a look row, or the next menu item.</summary>
    Right,
    /// <summary>Enter, E, gamepad A or a click: go on with the highlighted choice.</summary>
    Confirm,
    /// <summary>Esc, Backspace, gamepad B or the back arrow: one step back.</summary>
    Back,
    /// <summary>A MenuNav step up: the look row above, or the previous menu item.</summary>
    Up,
    /// <summary>A MenuNav step down: the look row below, or the next menu item.</summary>
    Down,
}
