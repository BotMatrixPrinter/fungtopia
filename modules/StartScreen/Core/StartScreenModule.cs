using System;
using Fungtopia.Input;
using Fungtopia.Player;
using Fungtopia.Save;
using Fungtopia.Shell;
using Fungtopia.Wardrobe;

namespace Fungtopia.StartScreen;

/// <summary>
/// The start screen's flow: the title's Continue / Load / New Game; Load picks a slot, then its
/// manual save or autosave; New Game picks a slot (asking "Overwrite?" when it is taken), then
/// the character, then "Make your look", then the starter pet, then "Start!". It keeps the
/// latest <see cref="SaveSlots"/> for the slot info. The view reports presses between ticks (<see cref="Press"/>,
/// <see cref="Point"/>, <see cref="Pick"/>); each tick applies them in order, sends the look as
/// <see cref="SetLook"/> and the outfit as <see cref="Equip"/>s on the tick they change,
/// publishes <see cref="PreviewYaw"/> every tick of the look step (the character turned by the
/// view's drag, keys and stick, like the wardrobe's), <see cref="GameStart"/> once on the tick "Start!" is pressed, and
/// <see cref="NewGameInSlot"/> on the tick a new game's slot is picked, <see cref="LoadGame"/>
/// then <see cref="GameStart"/> on the tick a save is chosen, <see cref="UiFocus"/> and
/// <see cref="StartScreenState"/> every tick.
/// </summary>
public sealed class StartScreenModule : IModule, IMessageHandler<SaveSlots>
{
    /// <summary>The <see cref="UiFocus.Owner"/> of the start screen ("STRT").</summary>
    public const int FocusOwner = 0x5354_5254;

    /// <summary>The most items a menu step has (<see cref="Point"/> takes 0 up to this, less one).</summary>
    public const int MaxOptions = 4;

    /// <summary>Presses kept between two ticks; more are dropped.</summary>
    public const int MaxQueued = 32;

    /// <summary>The number of <see cref="LookRow"/>s.</summary>
    public const int LookRows = 5;

    /// <summary>Turning speed with Q / E, LB / RB or the right stick fully pushed, radians a second (the wardrobe's).</summary>
    public const float TurnRadiansPerSecond = 3f;

    /// <summary>Turning per pixel of mouse drag (the wardrobe's).</summary>
    public const float DragRadiansPerPixel = 0.01f;

    /// <summary>The right stick turns only past this deflection (then from 0 to full speed; the wardrobe's).</summary>
    public const float StickDeadZone = 0.2f;

    private IMessageBus? _bus;
    private readonly StartButton[] _queue = new StartButton[MaxQueued];
    private readonly int[] _queuedOption = new int[MaxQueued];
    private readonly int[] _queuedRow = new int[MaxQueued];
    private int _queued;
    private int _outfitFor = -1;
    private bool _lookChanged;
    private bool _outfitChanged;
    private bool _turnLeft;
    private bool _turnRight;
    private float _turnStick;
    private float _drag;
    private SaveSlots _slots;
    private bool _titleMoved;
    private bool _newGameDue;
    private bool _focusOpen;

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

    /// <summary>
    /// How the character is turned on the look step (the <see cref="PreviewYaw"/> published),
    /// radians in [-π, π): 0 faces the camera, positive turns its front to the screen's right.
    /// 0 each time the look step is reached.
    /// </summary>
    public float Yaw { get; private set; }

    /// <summary>
    /// The highlighted item of a menu step: a <see cref="TitleItem"/> on the title, slot − 1 on
    /// <see cref="StartStep.Load"/> and <see cref="StartStep.NewSlot"/>, 0 manual / 1 autosave on
    /// <see cref="StartStep.LoadSave"/>, 0 Yes / 1 No on <see cref="StartStep.Overwrite"/>.
    /// </summary>
    public int Highlight { get; private set; } = (int)TitleItem.NewGame;

    /// <summary>The slot (1..3) picked on <see cref="StartStep.Load"/> or <see cref="StartStep.NewSlot"/>, then loaded or played; 0 before.</summary>
    public int GameSlot { get; private set; }

    /// <summary>Whether a save was loaded (Continue or Load) rather than a new game started.</summary>
    public bool Loaded { get; private set; }

    /// <summary>Whether the save loaded was the slot's autosave.</summary>
    public bool LoadedAuto { get; private set; }

    /// <summary>The latest <see cref="SaveSlots"/> (every slot empty until one comes).</summary>
    public SaveSlots Slots => _slots;

    /// <summary>Whether any slot has a readable save: Continue shows, Load can be chosen.</summary>
    public bool CanContinue => _slots.TryGetNewest(out _, out _);

    /// <summary>Ticks so far.</summary>
    public long Ticks { get; private set; }

    /// <summary>Ticks since the current step began (0 on the tick it began).</summary>
    public long StepTicks { get; private set; }

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        bus.Subscribe<SaveSlots>(this);
    }

    public void Handle(in SaveSlots message) => _slots = message;

    /// <summary>How many items the menu of <paramref name="step"/> has (0 when it is not a menu).</summary>
    public static int ItemCount(StartStep step) => step switch
    {
        StartStep.Title or StartStep.Load or StartStep.NewSlot => 3,
        StartStep.LoadSave or StartStep.Overwrite => 2,
        _ => 0,
    };

    /// <summary>Whether item <paramref name="item"/> of the current step's menu can be chosen (see <see cref="Highlight"/>).</summary>
    public bool ItemEnabled(int item)
    {
        if ((uint)item >= (uint)ItemCount(Step))
            return false;
        return Step switch
        {
            StartStep.Title => item == (int)TitleItem.NewGame || CanContinue,
            StartStep.Load => CanLoad(_slots.Get(item + 1)),
            StartStep.LoadSave => item == 0 ? _slots.Get(GameSlot).Manual.Ok : _slots.Get(GameSlot).Auto.Ok,
            _ => true,
        };
    }

    private static bool CanLoad(in SlotInfo slot) => slot.Manual.Ok || slot.Auto.Ok;

    private bool IsMenu => ItemCount(Step) > 0;

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
    /// step, or over item <paramref name="option"/> of a menu step (see <see cref="Highlight"/>):
    /// it becomes highlighted on the next tick (items that cannot be chosen are not). A click is
    /// <c>Point</c> then <c>Press(Confirm)</c>.
    /// </summary>
    public void Point(int option)
    {
        if ((uint)option < MaxOptions)
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

    /// <summary>
    /// The turn buttons held now (the view, every frame): <paramref name="left"/> Q / LB,
    /// <paramref name="right"/> E / RB. Each look-step tick they turn the character at <see cref="TurnRadiansPerSecond"/>.
    /// </summary>
    public void SetTurnButtons(bool left, bool right)
    {
        _turnLeft = left;
        _turnRight = right;
    }

    /// <summary>The right stick's x now (-1 left .. 1 right; the view, every frame). The buttons win when both are used.</summary>
    public void SetTurnStick(float x) => _turnStick = float.IsFinite(x) ? Math.Clamp(x, -1f, 1f) : 0f;

    /// <summary>
    /// A mouse drag on the left half of the screen, in pixels (right positive), on the look step;
    /// added up and applied at the next tick (dropped if the step is left first).
    /// </summary>
    public void Drag(float pixels)
    {
        if (Step == StartStep.Look && float.IsFinite(pixels))
            _drag += pixels;
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
        if (Step == StartStep.Title)
            FixTitleHighlight();
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
        if (Step == StartStep.Look)
        {
            Turn((float)context.StepSeconds);
            _bus.Publish(new PreviewYaw(Yaw));
        }
        else
            _drag = 0f;
        if (_newGameDue)
            _bus.Publish(new NewGameInSlot(GameSlot));
        _newGameDue = false;
        if (started && Loaded)
            _bus.Publish(new LoadGame(GameSlot, LoadedAuto));
        if (started)
            _bus.Publish(new GameStart(Character, StarterPet));
        if (Open || _focusOpen)
            _bus.Publish(new UiFocus(FocusOwner, Open));
        _focusOpen = Open;
        _bus.Publish(new StartScreenState(Open));
    }

    /// <summary>
    /// The title highlights Continue once saves are known (until the player moves the
    /// highlight), and never an item that cannot be chosen.
    /// </summary>
    private void FixTitleHighlight()
    {
        bool canContinue = CanContinue;
        if (!_titleMoved && canContinue)
            Highlight = (int)TitleItem.Continue;
        else if (!canContinue)
            Highlight = (int)TitleItem.NewGame;
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
            if (IsMenu)
            {
                if (ItemEnabled(option))
                    SetHighlight(option);
            }
            else
                Choose(option);
            return false;
        }
        switch (button)
        {
            case StartButton.Left:
                if (Step == StartStep.Look)
                    Cycle(-1);
                else if (IsMenu)
                    MoveHighlight(-1);
                else
                    Choose(0);
                return false;
            case StartButton.Right:
                if (Step == StartStep.Look)
                    Cycle(1);
                else if (IsMenu)
                    MoveHighlight(1);
                else
                    Choose(1);
                return false;
            case StartButton.Up:
                if (Step == StartStep.Look && Row > LookRow.Skin)
                    Row--;
                else if (IsMenu)
                    MoveHighlight(-1);
                return false;
            case StartButton.Down:
                if (Step == StartStep.Look && Row < LookRow.Outfit)
                    Row++;
                else if (IsMenu)
                    MoveHighlight(1);
                return false;
            case StartButton.Back:
                Back();
                return false;
            case StartButton.Confirm:
                return Confirm();
            default:
                return false;
        }
    }

    private void Back()
    {
        switch (Step)
        {
            case StartStep.Load:
                GoTo(StartStep.Title, (int)TitleItem.Load);
                break;
            case StartStep.NewSlot:
                GoTo(StartStep.Title, (int)TitleItem.NewGame);
                break;
            case StartStep.LoadSave:
                GoTo(StartStep.Load, GameSlot - 1);
                break;
            case StartStep.Overwrite:
            case StartStep.Character:
                GoTo(StartStep.NewSlot, GameSlot - 1);
                break;
            case StartStep.Look or StartStep.Pet or StartStep.Ready:
                Step--;
                if (Step == StartStep.Look)
                    FaceCamera();
                break;
        }
    }

    /// <summary>Chooses the highlighted item or goes on; true when the screen closes (Start! or a load).</summary>
    private bool Confirm()
    {
        if (IsMenu && !ItemEnabled(Highlight))
            return false;
        switch (Step)
        {
            case StartStep.Title:
                if (Highlight == (int)TitleItem.Continue)
                {
                    _slots.TryGetNewest(out int slot, out bool auto);
                    return BeginLoad(slot, auto);
                }
                if (Highlight == (int)TitleItem.Load)
                {
                    // The first slot that can be loaded is highlighted.
                    int first = 0;
                    while (first < SaveSlots.Count - 1 && !CanLoad(_slots.Get(first + 1)))
                        first++;
                    GoTo(StartStep.Load, first);
                }
                else
                    GoTo(StartStep.NewSlot, 0);
                return false;
            case StartStep.Load:
                GameSlot = Highlight + 1;
                GoTo(StartStep.LoadSave, _slots.Get(GameSlot).NewestIsAuto || !_slots.Get(GameSlot).Manual.Ok ? 1 : 0);
                return false;
            case StartStep.LoadSave:
                return BeginLoad(GameSlot, Highlight == 1);
            case StartStep.NewSlot:
                GameSlot = Highlight + 1;
                if (_slots.Get(GameSlot).Exists)
                    GoTo(StartStep.Overwrite, 1);
                else
                    BeginNewGame();
                return false;
            case StartStep.Overwrite:
                if (Highlight == 0)
                    BeginNewGame();
                else
                    GoTo(StartStep.NewSlot, GameSlot - 1);
                return false;
            case StartStep.Character or StartStep.Look or StartStep.Pet or StartStep.Ready:
                Step++;
                if (Step == StartStep.Look)
                    EnterLook();
                return Step == StartStep.Started;
            default:
                return false;
        }
    }

    private void GoTo(StartStep step, int highlight)
    {
        Step = step;
        Highlight = highlight;
    }

    /// <summary>The new game plays in <see cref="GameSlot"/>: <see cref="NewGameInSlot"/> this tick, then the character.</summary>
    private void BeginNewGame()
    {
        _newGameDue = true;
        Step = StartStep.Character;
    }

    /// <summary>Loads <paramref name="slot"/>'s manual save or autosave: the screen closes, as that save's character.</summary>
    private bool BeginLoad(int slot, bool auto)
    {
        var info = auto ? _slots.Get(slot).Auto : _slots.Get(slot).Manual;
        GameSlot = slot;
        Loaded = true;
        LoadedAuto = auto;
        // The restored parts set the real state; GameStart only lets the modules that wait for it start.
        Character = info.Character == CharacterKind.Boy ? GameStart.Boy : GameStart.Girl;
        StarterPet = GameStart.Cat;
        _newGameDue = false;
        Step = StartStep.Started;
        return true;
    }

    private void SetHighlight(int item)
    {
        Highlight = item;
        if (Step == StartStep.Title)
            _titleMoved = true;
    }

    /// <summary>The next (<paramref name="direction"/> 1) or previous (-1) item that can be chosen; it stops at the ends.</summary>
    private void MoveHighlight(int direction)
    {
        int count = ItemCount(Step);
        for (int item = Highlight + direction; item >= 0 && item < count; item += direction)
        {
            if (ItemEnabled(item))
            {
                SetHighlight(item);
                return;
            }
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
        FaceCamera();
    }

    /// <summary>Reaching the look step: the character faces the camera again.</summary>
    private void FaceCamera()
    {
        Yaw = 0f;
        _drag = 0f;
    }

    private void Turn(float seconds)
    {
        float axis = (_turnRight ? 1f : 0f) - (_turnLeft ? 1f : 0f);
        if (axis == 0f && Math.Abs(_turnStick) > StickDeadZone)
            axis = Math.Sign(_turnStick) * (Math.Abs(_turnStick) - StickDeadZone) / (1f - StickDeadZone);
        float yaw = Yaw + axis * TurnRadiansPerSecond * seconds + _drag * DragRadiansPerPixel;
        _drag = 0f;
        // Wrapped to [-π, π).
        yaw -= MathF.Tau * MathF.Floor((yaw + MathF.PI) / MathF.Tau);
        if (yaw >= MathF.PI)
            yaw -= MathF.Tau;
        Yaw = yaw;
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
        if (option is not (0 or 1))
            return;
        if (Step == StartStep.Character)
            Character = option;
        else if (Step == StartStep.Pet)
            StarterPet = option;
    }
}
