using System;
using System.Collections.Generic;
using Fungtopia.DayNight;
using Fungtopia.Input;
using Fungtopia.Map;
using Fungtopia.Player;
using Fungtopia.Shell;
using Fungtopia.StartScreen;
using Fungtopia.Wallet;

namespace Fungtopia.Save;

/// <summary>
/// Saves and loads the game: three slots, each with a manual save and a separate autosave.
/// Requests (<see cref="SaveGame"/>, <see cref="LoadGame"/>, <see cref="DeleteSlot"/>,
/// <see cref="NewGameInSlot"/>, and the pause menu's) are carried out in order on the module's
/// next tick. Also keeps the pause menu's state for its view.
/// </summary>
public sealed class SaveModule : IModule,
    IMessageHandler<SaveGame>, IMessageHandler<LoadGame>, IMessageHandler<DeleteSlot>, IMessageHandler<NewGameInSlot>,
    IMessageHandler<SavePart>, IMessageHandler<UiFocus>, IMessageHandler<StartScreenState>, IMessageHandler<GameStart>,
    IMessageHandler<PlayerState>, IMessageHandler<WalletState>, IMessageHandler<TimeOfDay>, IMessageHandler<ActiveDevice>
{
    /// <summary>The pause menu's <see cref="UiFocus.Owner"/> ("SAVE").</summary>
    public const int MenuOwner = 0x53415645;

    /// <summary>Seconds of play between autosaves.</summary>
    public double AutosaveSeconds { get; set; } = 60.0;

    /// <summary>Ticks between two <see cref="SaveSlots"/> messages (one second).</summary>
    public int SlotsEveryTicks { get; set; } = 60;

    /// <summary>
    /// Ticks after a load or a new game during which entering Home does not autosave (the
    /// player is being put back where the save left them).
    /// </summary>
    public int AreaGraceTicks { get; set; } = 60;

    /// <summary>The clock for the saved time (UTC).</summary>
    public Func<DateTime> Now { get; set; } = () => DateTime.UtcNow;

    private enum Kind : byte { Save, Load, Delete, NewGame }

    private readonly struct Request(Kind kind, int slot, bool auto)
    {
        public readonly Kind Kind = kind;
        public readonly int Slot = slot;
        public readonly bool Auto = auto;
    }

    private IMessageBus? _bus;
    private SaveFiles? _files;
    private readonly Request[] _requests = new Request[16];
    private int _requestCount;
    private int _requestId;
    private int _collecting;
    private readonly List<KeyValuePair<string, string>> _parts = new();
    private int[] _openMenus = new int[8];
    private int _openMenuCount;
    private bool _focusPublished;
    private bool _startOpen;
    private bool _hasArea;
    private Area _area;
    private bool _homeDue;
    private int _graceTicks;
    private CharacterKind _character;
    private int _coins;
    private int _day;
    private double _playSeconds;
    private bool _slotsDirty = true;
    private bool _slotsChanged;
    private long _ticks;

    /// <summary>
    /// The folder of the save files (the view sets user://saves). Until it is set nothing is
    /// saved or loaded and every slot is empty.
    /// </summary>
    public string? SaveFolder
    {
        get => _files?.Folder;
        set
        {
            _files = string.IsNullOrEmpty(value) ? null : new SaveFiles(value);
            _slotsDirty = true;
        }
    }

    public SaveFiles? Files => _files;

    /// <summary>The slot being played, 0 for none.</summary>
    public int Current { get; private set; }

    /// <summary>The game is under way: after <see cref="GameStart"/> or a load.</summary>
    public bool Started { get; private set; }

    /// <summary>The slot list as last read from disk.</summary>
    public SaveSlots Slots { get; private set; }

    /// <summary>Saves tried so far (manual and auto), and whether the latest one was written.</summary>
    public int SaveCount { get; private set; }

    public bool LastSaveOk { get; private set; }

    public bool LastSaveAuto { get; private set; }

    /// <summary>Loads tried so far, and whether the latest one was read.</summary>
    public int LoadCount { get; private set; }

    public bool LastLoadOk { get; private set; }

    /// <summary>Seconds of play since the last autosave (or load, or new game).</summary>
    public double PlaySeconds => _playSeconds;

    /// <summary>Whether the pause menu is open.</summary>
    public bool MenuOpen { get; private set; }

    /// <summary>Another module's menu is open (a <see cref="UiFocus"/> owner other than ours).</summary>
    public bool OtherMenuOpen => _openMenuCount > 0;

    /// <summary>The player is playing: started, the start screen closed, no menu open.</summary>
    public bool Playing => Started && !_startOpen && !OtherMenuOpen && !MenuOpen;

    /// <summary>Esc / Start may open the pause menu now.</summary>
    public bool CanOpenMenu => Playing;

    public long Ticks => _ticks;

    /// <summary>The device the player used last (<see cref="ActiveDevice"/>); the pause menu's hints show its buttons.</summary>
    public DeviceKind Device { get; private set; }

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        bus.Subscribe<SaveGame>(this);
        bus.Subscribe<LoadGame>(this);
        bus.Subscribe<DeleteSlot>(this);
        bus.Subscribe<NewGameInSlot>(this);
        bus.Subscribe<SavePart>(this);
        bus.Subscribe<UiFocus>(this);
        bus.Subscribe<StartScreenState>(this);
        bus.Subscribe<GameStart>(this);
        bus.Subscribe<PlayerState>(this);
        bus.Subscribe<WalletState>(this);
        bus.Subscribe<TimeOfDay>(this);
        bus.Subscribe<ActiveDevice>(this);
    }

    public void Handle(in SaveGame message) => Enqueue(Kind.Save, message.Slot, message.Auto);

    public void Handle(in LoadGame message) => Enqueue(Kind.Load, message.Slot, message.Auto);

    public void Handle(in DeleteSlot message) => Enqueue(Kind.Delete, message.Slot, false);

    public void Handle(in NewGameInSlot message) => Enqueue(Kind.NewGame, message.Slot, false);

    public void Handle(in SavePart message)
    {
        if (_collecting == 0 || message.RequestId != _collecting || string.IsNullOrEmpty(message.Module))
            return;
        var part = new KeyValuePair<string, string>(message.Module, message.Json ?? "");
        for (int i = 0; i < _parts.Count; i++)
        {
            if (_parts[i].Key == message.Module)
            {
                _parts[i] = part;
                return;
            }
        }
        _parts.Add(part);
    }

    /// <summary>Tracks other modules' menus one owner at a time, like the Input module.</summary>
    public void Handle(in UiFocus message)
    {
        if (message.Owner == MenuOwner)
            return;
        int index = Array.IndexOf(_openMenus, message.Owner, 0, _openMenuCount);
        if (message.Open)
        {
            if (index >= 0)
                return;
            if (_openMenuCount == _openMenus.Length)
                Array.Resize(ref _openMenus, _openMenus.Length * 2);
            _openMenus[_openMenuCount++] = message.Owner;
        }
        else if (index >= 0)
        {
            _openMenus[index] = _openMenus[--_openMenuCount];
        }
    }

    public void Handle(in StartScreenState message) => _startOpen = message.Open;

    public void Handle(in GameStart message)
    {
        if (Started)
            return;
        Started = true;
        // Until the title screen picks a slot (NewGameInSlot), a new game plays in slot 1.
        if (Current == 0)
            Current = 1;
        _playSeconds = 0;
    }

    public void Handle(in PlayerState message)
    {
        _character = message.Character;
        var area = message.Area;
        if (_hasArea && area == Area.Home && _area != Area.Home && Started && !_startOpen && _graceTicks == 0)
            _homeDue = true;
        _area = area;
        _hasArea = true;
    }

    public void Handle(in WalletState message) => _coins = message.Coins;

    public void Handle(in TimeOfDay message) => _day = message.Day;

    public void Handle(in ActiveDevice message) => Device = message.Kind;

    /// <summary>The pause menu's Save, Load and the like go through these (carried out next tick).</summary>
    public void RequestSave(int slot, bool auto) => Enqueue(Kind.Save, slot, auto);

    public void RequestLoad(int slot, bool auto) => Enqueue(Kind.Load, slot, auto);

    public void RequestDelete(int slot) => Enqueue(Kind.Delete, slot, false);

    public void RequestNewGame(int slot) => Enqueue(Kind.NewGame, slot, false);

    /// <summary>Opens the pause menu if <see cref="CanOpenMenu"/>; <see cref="UiFocus"/> follows on the next tick.</summary>
    public bool OpenMenu()
    {
        if (!CanOpenMenu)
            return false;
        MenuOpen = true;
        return true;
    }

    public void CloseMenu() => MenuOpen = false;

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("SaveModule.Tick before Start.");

        for (int i = 0; i < _requestCount; i++)
            Carry(_requests[i]);
        _requestCount = 0;

        if (_graceTicks > 0)
            _graceTicks--;
        if (_homeDue)
        {
            _homeDue = false;
            if (Current != 0)
                Save(Current, true);
        }

        if (Playing && Current != 0)
        {
            _playSeconds += context.StepSeconds;
            if (_playSeconds >= AutosaveSeconds - 1e-6)
            {
                _playSeconds = 0;
                Save(Current, true);
            }
        }

        if (MenuOpen != _focusPublished)
        {
            _focusPublished = MenuOpen;
            _bus.Publish(new UiFocus(MenuOwner, MenuOpen));
        }

        if (_slotsDirty)
        {
            _slotsDirty = false;
            Slots = _files?.ReadSlots() ?? default;
            _slotsChanged = true;
        }
        if (_slotsChanged || _ticks % SlotsEveryTicks == 0)
        {
            _slotsChanged = false;
            var slots = Slots;
            _bus.Publish(slots);
        }

        _bus.Publish(new CurrentSlot(Current));
        _ticks++;
    }

    private void Enqueue(Kind kind, int slot, bool auto)
    {
        if (_requestCount < _requests.Length)
            _requests[_requestCount++] = new Request(kind, slot, auto);
    }

    private static bool Valid(int slot) => slot >= 1 && slot <= SaveSlots.Count;

    private void Carry(in Request request)
    {
        switch (request.Kind)
        {
            case Kind.Save:
                int slot = request.Slot == 0 ? Current : request.Slot;
                if (Valid(slot))
                    Save(slot, request.Auto);
                break;
            case Kind.Load:
                if (Valid(request.Slot))
                    Load(request.Slot, request.Auto);
                break;
            case Kind.Delete:
                if (Valid(request.Slot) && _files != null)
                {
                    _files.Delete(request.Slot);
                    _slotsDirty = true;
                }
                break;
            case Kind.NewGame:
                if (Valid(request.Slot))
                {
                    Current = request.Slot;
                    _playSeconds = 0;
                    _graceTicks = AreaGraceTicks;
                    _homeDue = false;
                }
                break;
        }
    }

    /// <summary>Collects every module's part and writes the slot's manual or auto file.</summary>
    private void Save(int slot, bool auto)
    {
        _parts.Clear();
        int id = ++_requestId;
        if (id == 0)
            id = _requestId = 1;
        _collecting = id;
        try
        {
            _bus!.Publish(new CollectSave(id));
        }
        finally
        {
            _collecting = 0;
        }

        var data = new SaveData
        {
            Slot = slot,
            Auto = auto,
            SavedAt = Now().ToUniversalTime(),
            CharacterName = _character.ToString(),
            Character = _character,
            Day = _day,
            Coins = _coins,
            Area = _area,
        };
        data.Parts.AddRange(_parts);
        _parts.Clear();
        bool ok = _files != null && _files.Write(data);
        SaveCount++;
        LastSaveOk = ok;
        LastSaveAuto = auto;
        _slotsDirty = true;
        _bus.Publish(new GameSaved(slot, auto, ok));
    }

    private void Load(int slot, bool auto)
    {
        LoadCount++;
        var data = _files?.Read(slot, auto);
        LastLoadOk = data != null;
        _slotsDirty = true;
        if (data == null)
            return;
        foreach (var part in data.Parts)
            _bus!.Publish(new RestorePart(part.Key, part.Value));
        Current = slot;
        Started = true;
        MenuOpen = false;
        _playSeconds = 0;
        _graceTicks = AreaGraceTicks;
        _homeDue = false;
        _bus!.Publish(new GameLoaded(slot, auto));
    }
}
