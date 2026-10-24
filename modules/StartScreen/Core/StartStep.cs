namespace Fungtopia.StartScreen;

/// <summary>The steps of the start screen, in order.</summary>
public enum StartStep
{
    /// <summary>The "Fungtopia" logo over Mushroom Town: Confirm goes on.</summary>
    Title,
    /// <summary>"Choose your character": the Girl and the Boy.</summary>
    Character,
    /// <summary>"Make your look": skin tone, hair style, hair color, eye color and a starter outfit.</summary>
    Look,
    /// <summary>"Choose your starter pet": the Mushroom Cat and the Mushroom Dog.</summary>
    Pet,
    /// <summary>Both choices and the "Start!" button.</summary>
    Ready,
    /// <summary>"Start!" was pressed: the screen is closed for good.</summary>
    Started,
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

/// <summary>What the view reports from mouse, keyboard and gamepad.</summary>
public enum StartButton
{
    /// <summary>Arrow left, stick or D-pad left: the left choice (Girl, Cat), or the previous value of a look row.</summary>
    Left,
    /// <summary>Arrow right, stick or D-pad right: the right choice (Boy, Dog), or the next value of a look row.</summary>
    Right,
    /// <summary>Enter, E, gamepad A or a click: go on with the highlighted choice.</summary>
    Confirm,
    /// <summary>Esc, Backspace, gamepad B or the back arrow: one step back.</summary>
    Back,
    /// <summary>Arrow up, stick or D-pad up: the look row above.</summary>
    Up,
    /// <summary>Arrow down, stick or D-pad down: the look row below.</summary>
    Down,
}
