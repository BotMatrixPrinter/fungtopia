using System;
using Fungtopia.Input;
using Fungtopia.Shell;

namespace Fungtopia.Dialogue;

/// <summary>
/// The speech bubble's state. It shows the latest <see cref="SayLine"/>, types it out at
/// <see cref="LettersPerSecond"/>, and on <see cref="PlayerInput.Interact"/> either completes the
/// typing or, when the line is fully shown, publishes <see cref="DialogueAdvanced"/> (and, after a
/// <see cref="SayLine.Last"/> line, closes and publishes <see cref="DialogueClosed"/>). It
/// publishes <see cref="DialogueState"/> every tick, and <see cref="InteractAvailable"/> (owner
/// <see cref="InteractOwner"/>) on the tick a dialogue opens and the tick it closes, so the gamepad's
/// A button advances it instead of jumping.
/// </summary>
public sealed class DialogueModule : IModule, IMessageHandler<SayLine>, IMessageHandler<PlayerInput>
{
    public const float LettersPerSecond = 40f;

    /// <summary>The <see cref="InteractAvailable.Owner"/> of an open dialogue.</summary>
    public const int InteractOwner = 0x6469_616C; // "dial"

    private IMessageBus? _bus;
    private float _typed;
    private bool _interactPressed;
    // A press counts only once a line has been shown for a tick, so the press that started the
    // talk (or one landing between two lines) does not skip the new line.
    private bool _armed;
    private bool _offered;

    public bool Open { get; private set; }
    public int SpeakerId { get; private set; } = -1;
    public string Speaker { get; private set; } = "";
    public string Text { get; private set; } = "";
    public int Voice { get; private set; }
    public bool Last { get; private set; }

    /// <summary>True after a non-last line was advanced, until its speaker sends the next line.</summary>
    public bool Waiting { get; private set; }

    /// <summary>Lines received so far; changes whenever a new line replaces the shown one.</summary>
    public long LineCount { get; private set; }

    /// <summary>How many letters of <see cref="Text"/> are shown.</summary>
    public int VisibleLetters => Math.Min(Text.Length, (int)_typed);

    public bool Typing => Open && VisibleLetters < Text.Length;

    /// <summary>True when the line is fully shown and Interact would continue (the bouncing arrow).</summary>
    public bool CanContinue => Open && !Waiting && !Typing;

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        bus.Subscribe<SayLine>(this);
        bus.Subscribe<PlayerInput>(this);
    }

    public void Handle(in SayLine message)
    {
        Open = true;
        SpeakerId = message.SpeakerId;
        Speaker = message.Speaker ?? "";
        Text = message.Text ?? "";
        Voice = message.Voice is >= Voices.Deadpan and <= Voices.Neutral ? message.Voice : Voices.Neutral;
        Last = message.Last;
        Waiting = false;
        LineCount++;
        _typed = 0f;
        _armed = false;
    }

    public void Handle(in PlayerInput message)
    {
        if (message.Interact)
            _interactPressed = true;
    }

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("DialogueModule.Tick before Start.");
        bool interact = _interactPressed && _armed && Open;
        _interactPressed = false;
        _armed = Open;

        if (interact)
        {
            if (Typing)
                _typed = Text.Length;
            else if (!Waiting)
                Advance();
        }

        if (Open && _typed < Text.Length)
            _typed = MathF.Min(Text.Length, _typed + LettersPerSecond * (float)context.StepSeconds);

        _bus.Publish(new DialogueState(Open, Open ? SpeakerId : -1));
        if (Open != _offered)
        {
            _offered = Open;
            _bus.Publish(new InteractAvailable(InteractOwner, Open));
        }
    }

    private void Advance()
    {
        int speaker = SpeakerId;
        bool last = Last;
        if (last)
            Close();
        else
            Waiting = true;
        _bus!.Publish(new DialogueAdvanced(speaker));
        if (last)
            _bus.Publish(new DialogueClosed(speaker));
    }

    private void Close()
    {
        Open = false;
        Waiting = false;
        SpeakerId = -1;
        Speaker = Text = "";
        Last = false;
        _typed = 0f;
    }
}
