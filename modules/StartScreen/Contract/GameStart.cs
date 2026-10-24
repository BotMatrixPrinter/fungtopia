namespace Fungtopia.StartScreen;

/// <summary>
/// Published by the StartScreen module once, on the tick the player presses "Start!": the
/// character and starter pet they chose.
/// </summary>
public readonly struct GameStart
{
    /// <summary>The chosen character: <see cref="Girl"/> (0) or <see cref="Boy"/> (1).</summary>
    public readonly int Character;

    /// <summary>The chosen starter pet: <see cref="Cat"/> (0) or <see cref="Dog"/> (1).</summary>
    public readonly int StarterPet;

    public const int Girl = 0;
    public const int Boy = 1;
    public const int Cat = 0;
    public const int Dog = 1;

    public GameStart(int character, int starterPet)
    {
        Character = character;
        StarterPet = starterPet;
    }
}
