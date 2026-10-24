namespace Fungtopia.Dialogue;

/// <summary>The tone of a speaker's letter blips in the speech bubble.</summary>
public static class Voices
{
    public const int Deadpan = 0;
    public const int Bubbly = 1;
    public const int Kind = 2;
    public const int Neutral = 3;
}

/// <summary>
/// One line of speech, published by an NPC module one line at a time (the next one after a
/// <see cref="DialogueAdvanced"/> for its <see cref="SpeakerId"/>). A new line replaces the shown one.
/// </summary>
public readonly struct SayLine
{
    /// <summary>The NPC's id, echoed back in <see cref="DialogueAdvanced"/>, <see cref="DialogueState"/> and <see cref="DialogueClosed"/>.</summary>
    public readonly int SpeakerId;

    /// <summary>The name shown on the bubble's mushroom-cap tab.</summary>
    public readonly string Speaker;

    public readonly string Text;

    /// <summary>Letter blip voice: 0 deadpan, 1 bubbly, 2 kind, 3 neutral (see <see cref="Voices"/>).</summary>
    public readonly int Voice;

    /// <summary>True for the last line: dismissing it closes the dialogue.</summary>
    public readonly bool Last;

    public SayLine(int speakerId, string speaker, string text, int voice, bool last)
    {
        SpeakerId = speakerId;
        Speaker = speaker;
        Text = text;
        Voice = voice;
        Last = last;
    }
}

/// <summary>The player pressed Interact on a fully shown line of <see cref="SpeakerId"/> to continue.</summary>
public readonly struct DialogueAdvanced
{
    public readonly int SpeakerId;

    public DialogueAdvanced(int speakerId) => SpeakerId = speakerId;
}

/// <summary>Published by the Dialogue module every tick.</summary>
public readonly struct DialogueState
{
    /// <summary>True while a speech bubble is shown.</summary>
    public readonly bool Open;

    /// <summary>The speaker of the shown line, or -1 when <see cref="Open"/> is false.</summary>
    public readonly int SpeakerId;

    public DialogueState(bool open, int speakerId)
    {
        Open = open;
        SpeakerId = speakerId;
    }
}

/// <summary>Published once after the player dismisses a <see cref="SayLine.Last"/> line.</summary>
public readonly struct DialogueClosed
{
    public readonly int SpeakerId;

    public DialogueClosed(int speakerId) => SpeakerId = speakerId;
}
