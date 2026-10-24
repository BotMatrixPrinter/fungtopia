using System;
using Fungtopia.Shell;
using Fungtopia.Wardrobe;

namespace Fungtopia.StartScreen;

/// <summary>
/// The start screen's flow: Title, then the character, then "Make your look", then the starter
/// pet, then "Start!". The view reports presses between ticks (<see cref="Press"/>,
/// <see cref="Point"/>, <see cref="Pick"/>); each tick applies them in order, sends the look as
/// <see cref="SetLook"/> and the outfit as <see cref="Equip"/>s on the tick they change,
/// publishes <see cref="GameStart"/> once on the tick "Start!" is pressed, and
/// <see cref="StartScreenState"/> every tick.
/// </summary>
public sealed class StartScreenModule : IModule
{
    /// <summary>Presses kept between two ticks; more are dropped.</summary>
    public const int MaxQueued = 32;

    /// <summary>The number of <see cref="LookRow"/>s.</summary>
    public const int LookRows = 5;

    private IMessageBus? _bus;
    private readonly StartButton[] _queue = new StartButton[MaxQueued];
    private readonly int[] _queuedOption = new int[MaxQueued];
    private readonly int[] _queuedRow = new int[MaxQueued];
    private int _queued;
    private int _outfitFor = -1;
    private bool _lookChanged;
    private bool _outfitChanged;

    public StartStep Step { get; private set; } = StartStep.Title;

    /// <summary>The highlighted (then chosen) character: <see cref="GameStart.Girl"/> or <see cref="GameStart.Boy"/>.</summary>
    public int Character { get; private set; } = GameStart.Girl;

    /// <summary>The highlighted (then chosen) starter pet: <see cref="GameStart.Cat"/> or <see cref="GameStart.Dog"/>.</summary>
    public int StarterPet { get; private set; } = GameStart.Cat;

    /// <summary>The highlighted row of the <see cref="StartStep.Look"/> step.</summary>
    public LookRow Row { get; private set; } = LookRow.Skin;

    /// <summary>Index in <see cref="Looks.SkinTones"/>.</summary>
    public int Skin { get; private set; }

    /// <summary>Index in <see cref="Looks.HairStyles"/>.</summary>
    public int HairStyle { get; private set; }

    /// <summary>Index in <see cref="Looks.HairColors"/>.</summary>
    public int HairColor { get; private set; }

    /// <summary>Index in <see cref="Looks.EyeColors"/>.</summary>
    public int EyeColor { get; private set; }

    /// <summary>
    /// Index in <see cref="Catalog.StarterOutfits"/>: <see cref="Catalog.DefaultOutfit"/> of the
    /// character when the look step is first reached with it, then the player's choice.
    /// </summary>
    public int Outfit { get; private set; } = Catalog.DefaultOutfit(GameStart.Girl);

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

    /// <summary>The value of look row <paramref name="row"/>.</summary>
    public int LookValue(LookRow row) => row switch
    {
        LookRow.Skin => Skin,
        LookRow.HairStyle => HairStyle,
        LookRow.HairColor => HairColor,
        LookRow.EyeColor => EyeColor,
        _ => Outfit,
    };

    /// <summary>How many values look row <paramref name="row"/> has (all the starter outfits for <see cref="LookRow.Outfit"/>).</summary>
    public static int LookCount(LookRow row) => row switch
    {
        LookRow.Skin => Looks.SkinTones.Length,
        LookRow.HairStyle => Looks.HairStyles.Length,
        LookRow.HairColor => Looks.HairColors.Length,
        LookRow.EyeColor => Looks.EyeColors.Length,
        _ => Catalog.StarterOutfits.Length,
    };

    /// <summary>Whether starter outfit <paramref name="outfit"/> fits the current <see cref="Character"/>.</summary>
    public bool OutfitFits(int outfit) =>
        (uint)outfit < (uint)Catalog.StarterOutfits.Length && Fits.Matches(Catalog.StarterOutfits[outfit].Fit, Character);

    /// <summary>Reports a button press; applied on the next tick.</summary>
    public void Press(StartButton button) => Enqueue(button, -1, -1);

    /// <summary>
    /// Reports the mouse over choice <paramref name="option"/> (0 left, 1 right) of the current
    /// step: it becomes highlighted on the next tick. A click is <c>Point</c> then
    /// <c>Press(Confirm)</c>.
    /// </summary>
    public void Point(int option)
    {
        if (option is 0 or 1)
            Enqueue(StartButton.Left, option, -1);
    }

    /// <summary>
    /// Reports a click on value <paramref name="value"/> of look row <paramref name="row"/>: on
    /// the next tick, on the look step, the row is highlighted and takes that value (an outfit
    /// that does not fit the character is ignored).
    /// </summary>
    public void Pick(LookRow row, int value)
    {
        if ((uint)row < LookRows && (uint)value < (uint)LookCount(row))
            Enqueue(StartButton.Left, value, (int)row);
    }

    private void Enqueue(StartButton button, int option, int row)
    {
        if (_queued == MaxQueued || !Open)
            return;
        _queue[_queued] = button;
        _queuedOption[_queued] = option;
        _queuedRow[_queued] = row;
        _queued++;
    }

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("StartScreenModule.Tick before Start.");
        var before = Step;
        bool started = false;
        for (int i = 0; i < _queued && !started; i++)
            started = Apply(_queue[i], _queuedOption[i], _queuedRow[i]);
        _queued = 0;

        StepTicks = Step == before && Ticks > 0 ? StepTicks + 1 : 0;
        Ticks++;
        if (_lookChanged)
            _bus.Publish(new SetLook(Skin, HairStyle, HairColor, EyeColor));
        if (_outfitChanged)
            SendOutfit(_bus);
        _lookChanged = false;
        _outfitChanged = false;
        if (started)
            _bus.Publish(new GameStart(Character, StarterPet));
        _bus.Publish(new StartScreenState(Open));
    }

    /// <summary>Puts on the whole <see cref="Outfit"/>: one <see cref="Equip"/> per slot it has (top first), an unequip per slot it leaves empty.</summary>
    private void SendOutfit(IMessageBus bus)
    {
        var outfit = Catalog.StarterOutfits[Outfit];
        SendSlot(bus, outfit.Top, Slot.Top);
        SendSlot(bus, outfit.Bottom, Slot.Bottom);
        SendSlot(bus, outfit.Shoes, Slot.Shoes);
        SendSlot(bus, outfit.Head, Slot.Head);
    }

    private static void SendSlot(IMessageBus bus, string id, Slot slot)
    {
        if (string.IsNullOrEmpty(id))
            bus.Publish(Equip.Unequip(slot));
        else
            bus.Publish(new Equip(id));
    }

    /// <summary>Applies one press; true when it pressed "Start!".</summary>
    private bool Apply(StartButton button, int option, int row)
    {
        if (row >= 0)
        {
            if (Step == StartStep.Look)
            {
                Row = (LookRow)row;
                SetValue(Row, option);
            }
            return false;
        }
        if (option >= 0)
        {
            Choose(option);
            return false;
        }
        switch (button)
        {
            case StartButton.Left:
                if (Step == StartStep.Look)
                    Cycle(-1);
                else
                    Choose(0);
                return false;
            case StartButton.Right:
                if (Step == StartStep.Look)
                    Cycle(1);
                else
                    Choose(1);
                return false;
            case StartButton.Up:
                if (Step == StartStep.Look && Row > LookRow.Skin)
                    Row--;
                return false;
            case StartButton.Down:
                if (Step == StartStep.Look && Row < LookRow.Outfit)
                    Row++;
                return false;
            case StartButton.Back:
                if (Step is StartStep.Character or StartStep.Look or StartStep.Pet or StartStep.Ready)
                    Step--;
                return false;
            case StartButton.Confirm:
                Step++;
                if (Step == StartStep.Look)
                    EnterLook();
                return Step == StartStep.Started;
            default:
                return false;
        }
    }

    /// <summary>
    /// Coming to the look step from the character: a new character gets its default outfit, and
    /// the whole look is sent so the character shows what the step shows.
    /// </summary>
    private void EnterLook()
    {
        if (_outfitFor != Character || !OutfitFits(Outfit))
        {
            Outfit = Catalog.DefaultOutfit(Character);
            Row = LookRow.Skin;
        }
        _outfitFor = Character;
        _lookChanged = true;
        _outfitChanged = true;
    }

    /// <summary>The next (<paramref name="direction"/> 1) or previous (-1) value of the highlighted row, wrapping round; outfits that do not fit are skipped.</summary>
    private void Cycle(int direction)
    {
        int count = LookCount(Row);
        int value = LookValue(Row);
        for (int i = 0; i < count; i++)
        {
            value = (value + direction + count) % count;
            if (Row != LookRow.Outfit || OutfitFits(value))
                break;
        }
        SetValue(Row, value);
    }

    private void SetValue(LookRow row, int value)
    {
        if (value == LookValue(row))
            return;
        switch (row)
        {
            case LookRow.Skin:
                Skin = value;
                break;
            case LookRow.HairStyle:
                HairStyle = value;
                break;
            case LookRow.HairColor:
                HairColor = value;
                break;
            case LookRow.EyeColor:
                EyeColor = value;
                break;
            default:
                if (!OutfitFits(value))
                    return;
                Outfit = value;
                _outfitChanged = true;
                return;
        }
        _lookChanged = true;
    }

    private void Choose(int option)
    {
        if (Step == StartStep.Character)
            Character = option;
        else if (Step == StartStep.Pet)
            StarterPet = option;
    }
}
