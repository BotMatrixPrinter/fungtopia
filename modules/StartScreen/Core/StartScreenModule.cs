using System;
using Fungtopia.Shell;

namespace Fungtopia.StartScreen;

/// <summary>
/// The start screen's flow: Title, then the character, then the starter pet, then "Start!".
/// The view reports presses between ticks (<see cref="Press"/>, <see cref="Point"/>); each tick
/// applies them in order, publishes <see cref="GameStart"/> once on the tick "Start!" is
/// pressed, and <see cref="StartScreenState"/> every tick.
/// </summary>
public sealed class StartScreenModule : IModule
{
    /// <summary>Presses kept between two ticks; more are dropped.</summary>
    public const int MaxQueued = 32;

    private IMessageBus? _bus;
    private readonly StartButton[] _queue = new StartButton[MaxQueued];
    private readonly int[] _queuedOption = new int[MaxQueued];
    private int _queued;

    public StartStep Step { get; private set; } = StartStep.Title;

    /// <summary>The highlighted (then chosen) character: <see cref="GameStart.Girl"/> or <see cref="GameStart.Boy"/>.</summary>
    public int Character { get; private set; } = GameStart.Girl;

    /// <summary>The highlighted (then chosen) starter pet: <see cref="GameStart.Cat"/> or <see cref="GameStart.Dog"/>.</summary>
    public int StarterPet { get; private set; } = GameStart.Cat;

    /// <summary>True until "Start!" is pressed.</summary>
    public bool Open => Step != StartStep.Started;

    /// <summary>Ticks so far.</summary>
    public long Ticks { get; private set; }

    /// <summary>Ticks since the current step began (0 on the tick it began).</summary>
    public long StepTicks { get; private set; }

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
    }

    /// <summary>Reports a button press; applied on the next tick.</summary>
    public void Press(StartButton button) => Enqueue(button, -1);

    /// <summary>
    /// Reports the mouse over choice <paramref name="option"/> (0 left, 1 right) of the current
    /// step: it becomes highlighted on the next tick. A click is <c>Point</c> then
    /// <c>Press(Confirm)</c>.
    /// </summary>
    public void Point(int option)
    {
        if (option is 0 or 1)
            Enqueue(StartButton.Left, option);
    }

    private void Enqueue(StartButton button, int option)
    {
        if (_queued == MaxQueued || !Open)
            return;
        _queue[_queued] = button;
        _queuedOption[_queued] = option;
        _queued++;
    }

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("StartScreenModule.Tick before Start.");
        var before = Step;
        bool started = false;
        for (int i = 0; i < _queued && !started; i++)
            started = Apply(_queue[i], _queuedOption[i]);
        _queued = 0;

        StepTicks = Step == before && Ticks > 0 ? StepTicks + 1 : 0;
        Ticks++;
        if (started)
            _bus.Publish(new GameStart(Character, StarterPet));
        _bus.Publish(new StartScreenState(Open));
    }

    /// <summary>Applies one press; true when it pressed "Start!".</summary>
    private bool Apply(StartButton button, int option)
    {
        if (option >= 0)
        {
            Choose(option);
            return false;
        }
        switch (button)
        {
            case StartButton.Left:
                Choose(0);
                return false;
            case StartButton.Right:
                Choose(1);
                return false;
            case StartButton.Back:
                if (Step is StartStep.Character or StartStep.Pet or StartStep.Ready)
                    Step--;
                return false;
            case StartButton.Confirm:
                Step++;
                return Step == StartStep.Started;
            default:
                return false;
        }
    }

    private void Choose(int option)
    {
        if (Step == StartStep.Character)
            Character = option;
        else if (Step == StartStep.Pet)
            StarterPet = option;
    }
}
