using System;
using System.Collections.Generic;
using System.Text.Json;
using Fungtopia.Input;
using Fungtopia.Monsters;
using Fungtopia.Player;
using Fungtopia.Save;
using Fungtopia.Shell;
using Fungtopia.Wallet;

namespace Fungtopia.Wardrobe;

/// <summary>What became of the latest purchase in Abby's shop.</summary>
public enum BuyResult
{
    None,
    /// <summary>Asked the wallet; waiting for its <see cref="SpendResult"/>.</summary>
    Pending,
    Bought,
    NotEnough,
}

/// <summary>
/// The player's clothes and looks: which <see cref="Catalog"/> items they own (the starters from
/// the start), what they wear in each <see cref="Slot"/> and their <see cref="Looks"/>. Handles
/// <see cref="SetLook"/>, <see cref="Equip"/>, <see cref="GrantItem"/>, <see cref="OpenWardrobe"/>
/// and <see cref="OpenShop"/>; gives the Spider Silk Scarf and the Ladybug Backpack for the first
/// Mushroom Tarantula and Mushroom Ladybug defeated; buys in Abby's shop with
/// <see cref="SpendCoins"/>, trying the highlighted item on first. Publishes <see cref="AppearanceState"/>
/// and <see cref="WardrobeState"/> every tick, <see cref="PreviewYaw"/> every tick a screen is open
/// (the character turned by the view's drag, keys and stick), <see cref="ItemReceived"/> for each
/// new item. Saves its look, owned and worn items as one <see cref="SavePart"/> ("Wardrobe") on
/// <see cref="CollectSave"/> and restores them from <see cref="RestorePart"/>. No allocations per tick.
/// </summary>
public sealed class WardrobeModule : IModule, IMessageHandler<SetLook>, IMessageHandler<Equip>,
    IMessageHandler<GrantItem>, IMessageHandler<OpenWardrobe>, IMessageHandler<OpenShop>,
    IMessageHandler<MonsterKilled>, IMessageHandler<PlayerState>, IMessageHandler<WalletState>,
    IMessageHandler<SpendResult>, IMessageHandler<CollectSave>, IMessageHandler<RestorePart>,
    IMessageHandler<ActiveDevice>, IMessageHandler<MenuNav>
{
    /// <summary>The <see cref="SpendCoins.RequestId"/>s of the shop start here (other shops use their own).</summary>
    public const int RequestIdBase = 0x5744_0000;

    public const string ShopReason = "Abby's shop";
    public const string TarantulaReason = "First Mushroom Tarantula";
    public const string LadybugReason = "First Mushroom Ladybug";

    /// <summary>Turning speed with Q / E, LB / RB or the right stick fully pushed, radians a second.</summary>
    public const float TurnRadiansPerSecond = 3f;

    /// <summary>Turning per pixel of mouse drag.</summary>
    public const float DragRadiansPerPixel = 0.01f;

    /// <summary>The right stick turns only past this deflection (then from 0 to full speed).</summary>
    public const float StickDeadZone = 0.2f;

    /// <summary>The <see cref="SaveData.V"/> written; other versions are not restored.</summary>
    public const int SaveVersion = 1;

    private const int Slots = 6;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly bool[] _owned = new bool[Catalog.Count];
    private readonly int[] _worn = new int[Slots];
    private IMessageBus? _bus;
    private bool _outfitChosen;
    private bool _characterKnown;
    private int _buyQueued = -1;
    private int _buyPending = -1;
    private int _requestId = RequestIdBase;
    private int _tryOn = -1;
    private bool _turnLeft;
    private bool _turnRight;
    private float _turnStick;
    private float _drag;
    private bool _restoreProblemLogged;
    private int _navVertical;
    private int _navHorizontal;

    public WardrobeModule()
    {
        Array.Fill(_worn, -1);
        for (int i = 0; i < _owned.Length; i++)
            _owned[i] = Catalog.Items[i].Source == Sources.Starter;
        WearOutfit(Catalog.DefaultOutfit(0));
        _outfitChosen = false;
    }

    public int Skin { get; private set; }
    public int HairStyle { get; private set; } = Looks.NoHair;
    public int HairColor { get; private set; }
    public int EyeColor { get; private set; }

    /// <summary>0 Girl, 1 Boy (from the latest <see cref="PlayerState"/>; Girl before it).</summary>
    public int Character { get; private set; }

    /// <summary>The latest <see cref="WalletState.Coins"/>, for the shop.</summary>
    public int Coins { get; private set; }

    public bool Open { get; private set; }

    /// <summary>
    /// The device whose prompts the screens show: the latest <see cref="ActiveDevice"/>, or the one
    /// <see cref="NoteDevice"/> saw since (the view takes the input while a screen is open, so the
    /// Input module does not see a device change there). Keyboard and mouse before any.
    /// </summary>
    public DeviceKind Device { get; private set; }

    private DeviceKind _busDevice;
    public bool Shop { get; private set; }

    public bool TarantulaRewarded { get; private set; }
    public bool LadybugRewarded { get; private set; }

    /// <summary>The latest purchase and its item (an index in <see cref="Catalog.Items"/>, or -1).</summary>
    public BuyResult LastBuy { get; private set; }
    public int LastBuyItem { get; private set; } = -1;

    /// <summary>Counts changes of the owned items (each item bought or granted, each restore), for the view.</summary>
    public int ReceivedCount { get; private set; }

    public long Ticks { get; private set; }

    /// <summary>Where the one log line about an unreadable saved part goes (the console by default).</summary>
    public Action<string> Log { get; set; } = Console.WriteLine;

    /// <summary>How the character is turned on the open screen (the <see cref="PreviewYaw"/> published), radians.</summary>
    public float Yaw { get; private set; }

    /// <summary>The shop item tried on (an index in <see cref="Catalog.Items"/>, or -1): shown, not worn.</summary>
    public int TryingOn => _tryOn;

    /// <summary>Whether the player owns item <paramref name="index"/> of <see cref="Catalog.Items"/>.</summary>
    public bool Owns(int index) => (uint)index < (uint)_owned.Length && _owned[index];

    public bool Owns(string id) => Owns(Catalog.IndexOf(id));

    /// <summary>Whether item <paramref name="index"/> fits the current <see cref="Character"/>.</summary>
    public bool FitsCharacter(int index) =>
        (uint)index < (uint)_owned.Length && Fits.Matches(Catalog.Items[index].Fit, Character);

    /// <summary>The item put on in <paramref name="slot"/> (an index in <see cref="Catalog.Items"/>, or -1), shown or not.</summary>
    public int Worn(Slot slot) => _worn[(int)slot];

    /// <summary>Whether the bottom is hidden by a dress.</summary>
    public bool BottomHidden => _worn[(int)Slot.Top] >= 0 && Catalog.Items[_worn[(int)Slot.Top]].Dress;

    /// <summary>
    /// What the player looks like now (the <see cref="AppearanceState"/> published each tick): what
    /// they wear, with the item tried on in the shop in its slot.
    /// </summary>
    public AppearanceState Appearance
    {
        get
        {
            int top = Shown(Slot.Top);
            bool dress = top >= 0 && Catalog.Items[top].Dress;
            return new(Skin, HairStyle, HairColor, EyeColor, Id(top), dress ? "" : Id(Shown(Slot.Bottom)),
                Id(Shown(Slot.Shoes)), Id(Shown(Slot.Head)), Id(Shown(Slot.Face)), Id(Shown(Slot.Back)));
        }
    }

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        bus.Subscribe<SetLook>(this);
        bus.Subscribe<Equip>(this);
        bus.Subscribe<GrantItem>(this);
        bus.Subscribe<OpenWardrobe>(this);
        bus.Subscribe<OpenShop>(this);
        bus.Subscribe<MonsterKilled>(this);
        bus.Subscribe<PlayerState>(this);
        bus.Subscribe<WalletState>(this);
        bus.Subscribe<SpendResult>(this);
        bus.Subscribe<CollectSave>(this);
        bus.Subscribe<RestorePart>(this);
        bus.Subscribe<ActiveDevice>(this);
        bus.Subscribe<MenuNav>(this);
    }

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("WardrobeModule.Tick before Start.");
        Ticks++;
        if (_buyQueued >= 0)
        {
            int index = _buyQueued;
            _buyQueued = -1;
            SendBuy(index);
        }
        _bus.Publish(Appearance);
        _bus.Publish(new WardrobeState(Open, Shop));
        if (Open)
        {
            Turn((float)context.StepSeconds);
            _bus.Publish(new PreviewYaw(Yaw));
        }
    }

    /// <summary>
    /// The turn buttons held now (the view, every frame): <paramref name="left"/> Q / LB,
    /// <paramref name="right"/> E / RB. Each tick they turn the character at <see cref="TurnRadiansPerSecond"/>.
    /// </summary>
    public void SetTurnButtons(bool left, bool right)
    {
        _turnLeft = left;
        _turnRight = right;
    }

    /// <summary>The right stick's x now (-1 left .. 1 right; the view, every frame). The buttons win when both are used.</summary>
    public void SetTurnStick(float x) => _turnStick = float.IsFinite(x) ? Math.Clamp(x, -1f, 1f) : 0f;

    /// <summary>A mouse drag on the left half of the screen, in pixels (right positive); added up and applied at the next tick.</summary>
    public void Drag(float pixels)
    {
        if (Open && float.IsFinite(pixels))
            _drag += pixels;
    }

    /// <summary>
    /// Shows shop item <paramref name="id"/> on the character (highlighted in the open shop) instead
    /// of what is worn in its slot; an empty or other id shows what is worn again. Only shop items
    /// that fit, only while the shop is open; closing the shop takes it off unless it was bought.
    /// </summary>
    public void TryOn(string id)
    {
        int index = Catalog.IndexOf(id ?? "");
        _tryOn = Open && Shop && index >= 0 && Catalog.Items[index].Source == Sources.Shop && FitsCharacter(index) ? index : -1;
    }

    /// <summary>Buys item <paramref name="id"/> in the open shop at the next tick (the view's Buy button).</summary>
    public void Buy(string id)
    {
        int index = Catalog.IndexOf(id);
        if (!Open || !Shop || !CanBuy(index) || _buyPending >= 0)
            return;
        _buyQueued = index;
        LastBuy = BuyResult.Pending;
        LastBuyItem = index;
    }

    /// <summary>Whether Abby sells item <paramref name="index"/> to this player now (a shop item that fits and is not owned).</summary>
    public bool CanBuy(int index) =>
        (uint)index < (uint)_owned.Length && Catalog.Items[index].Source == Sources.Shop && !_owned[index] &&
        FitsCharacter(index);

    /// <summary>Closes the open screen (the view's Done button, Esc, gamepad B).</summary>
    public void Close()
    {
        Open = false;
        Shop = false;
        _tryOn = -1;
        _drag = 0f;
        _navVertical = 0;
        _navHorizontal = 0;
    }

    /// <summary>Puts on starter outfit <paramref name="outfit"/> (character creation): its slots, the rest empty but face and back.</summary>
    public void WearOutfit(int outfit)
    {
        if ((uint)outfit >= (uint)Catalog.StarterOutfits.Length)
            return;
        var o = Catalog.StarterOutfits[outfit];
        if (_characterKnown && !Fits.Matches(o.Fit, Character))
            return;
        _worn[(int)Slot.Top] = Catalog.IndexOf(o.Top);
        _worn[(int)Slot.Bottom] = Catalog.IndexOf(o.Bottom);
        _worn[(int)Slot.Shoes] = Catalog.IndexOf(o.Shoes);
        _worn[(int)Slot.Head] = Catalog.IndexOf(o.Head);
        _worn[(int)Slot.Face] = _worn[(int)Slot.Face] is var f && f >= 0 && _owned[f] ? f : -1;
        _worn[(int)Slot.Back] = _worn[(int)Slot.Back] is var b && b >= 0 && _owned[b] ? b : -1;
        _outfitChosen = true;
    }

    public void Handle(in SetLook message)
    {
        if (!Looks.Valid(message.Skin, message.HairStyle, message.HairColor, message.EyeColor))
            return;
        Skin = message.Skin;
        HairStyle = message.HairStyle;
        HairColor = message.HairColor;
        EyeColor = message.EyeColor;
    }

    public void Handle(in Equip message)
    {
        if (string.IsNullOrEmpty(message.ItemId))
        {
            if ((uint)message.EquipSlot < Slots)
                _worn[(int)message.EquipSlot] = -1;
            _outfitChosen = true;
            return;
        }
        int index = Catalog.IndexOf(message.ItemId);
        if (index < 0 || !_owned[index] || !FitsCharacter(index))
            return;
        _worn[(int)Catalog.Items[index].Slot] = index;
        _outfitChosen = true;
    }

    public void Handle(in GrantItem message) => Give(Catalog.IndexOf(message.ItemId), message.Why ?? "");

    public void Handle(in OpenWardrobe message) => OpenScreen(false);

    public void Handle(in OpenShop message) => OpenScreen(true);

    public void Handle(in MonsterKilled message)
    {
        if (message.Kind == MonsterKind.Tarantula && !TarantulaRewarded)
        {
            TarantulaRewarded = true;
            Give(Catalog.IndexOf(Catalog.SpiderSilkScarf), TarantulaReason);
        }
        else if (message.Kind == MonsterKind.Ladybug && !LadybugRewarded)
        {
            LadybugRewarded = true;
            Give(Catalog.IndexOf(Catalog.LadybugBackpack), LadybugReason);
        }
    }

    public void Handle(in PlayerState message)
    {
        int character = message.Character == CharacterKind.Boy ? 1 : 0;
        if (_characterKnown && character == Character)
            return;
        _characterKnown = true;
        Character = character;
        if (!_outfitChosen)
        {
            WearOutfit(Catalog.DefaultOutfit(character));
            _outfitChosen = false;
            return;
        }
        // Take off what does not fit the new character.
        for (int s = 0; s < Slots; s++)
            if (_worn[s] >= 0 && !FitsCharacter(_worn[s]))
                _worn[s] = -1;
        if (_tryOn >= 0 && !FitsCharacter(_tryOn))
            _tryOn = -1;
    }

    public void Handle(in WalletState message) => Coins = message.Coins;

    /// <summary>Follows the Input module's device when it changes (it publishes it every tick).</summary>
    public void Handle(in ActiveDevice message)
    {
        if (message.Kind == _busDevice)
            return;
        _busDevice = message.Kind;
        Device = message.Kind;
    }

    /// <summary>The view saw input from <paramref name="kind"/> (while a screen is open): its prompts show.</summary>
    public void NoteDevice(DeviceKind kind) => Device = kind;

    /// <summary>
    /// A <see cref="MenuNav"/> step (the Input module paces them: one on a push, then one per 0.2 s
    /// held) is kept for the view while a screen is open; only the latest untaken one, so steps never pile up.
    /// </summary>
    public void Handle(in MenuNav message)
    {
        if (!Open || !message.Any)
            return;
        _navVertical = message.Vertical;
        _navHorizontal = message.Horizontal;
    }

    /// <summary>
    /// The view, every frame: the focus step to make now (<paramref name="vertical"/> -1 up, +1 down;
    /// <paramref name="horizontal"/> -1 left, +1 right), from the latest <see cref="MenuNav"/> step not
    /// taken yet. False when there is none; each step is taken once.
    /// </summary>
    public bool TakeMenuStep(out int vertical, out int horizontal)
    {
        vertical = _navVertical;
        horizontal = _navHorizontal;
        _navVertical = 0;
        _navHorizontal = 0;
        return vertical != 0 || horizontal != 0;
    }

    public void Handle(in SpendResult message)
    {
        if (_buyPending < 0 || message.RequestId != _requestId)
            return;
        int index = _buyPending;
        _buyPending = -1;
        Coins = message.CoinsLeft;
        if (message.Ok)
        {
            LastBuy = BuyResult.Bought;
            Give(index, ShopReason);
            // Bought while trying it on: it stays on.
            if (index == _tryOn)
            {
                _worn[(int)Catalog.Items[index].Slot] = index;
                _outfitChosen = true;
            }
        }
        else
        {
            LastBuy = BuyResult.NotEnough;
        }
    }

    public void Handle(in CollectSave message) =>
        _bus?.Publish(new SavePart(message.RequestId, SaveModules.Wardrobe, SaveJson()));

    public void Handle(in RestorePart message)
    {
        if (message.Module == SaveModules.Wardrobe)
            Restore(message.Json);
    }

    /// <summary>The look, owned and worn items as the JSON of one <see cref="SaveData"/>.</summary>
    public string SaveJson()
    {
        var owned = new List<string>();
        for (int i = 0; i < _owned.Length; i++)
            if (_owned[i])
                owned.Add(Catalog.Items[i].Id);
        var worn = new string[Slots];
        for (int s = 0; s < Slots; s++)
            worn[s] = Id(_worn[s]);
        var data = new SaveData(SaveVersion, new[] { Skin, HairStyle, HairColor, EyeColor }, owned.ToArray(), worn);
        return JsonSerializer.Serialize(data, JsonOptions);
    }

    /// <summary>
    /// Restores what <see cref="SaveJson"/> wrote, with no side effects (no <see cref="ItemReceived"/>,
    /// no purchase) and closes any open screen. Missing or unreadable JSON or another version
    /// changes nothing (logged once). Unknown item ids (no longer in the catalog) are skipped.
    /// Returns whether it restored.
    /// </summary>
    public bool Restore(string? json)
    {
        string? problem = Parse(json, out var data);
        if (problem != null)
        {
            if (!_restoreProblemLogged)
            {
                _restoreProblemLogged = true;
                Log($"Fungtopia: Wardrobe: saved part not restored ({problem}), keeping the current clothes and look");
            }
            return false;
        }
        var look = data!.Look!;
        Skin = look[0];
        HairStyle = look[1];
        HairColor = look[2];
        EyeColor = look[3];
        for (int i = 0; i < _owned.Length; i++)
            _owned[i] = Catalog.Items[i].Source == Sources.Starter;
        foreach (var id in data.Owned!)
        {
            int index = Catalog.IndexOf(id ?? "");
            if (index >= 0)
                _owned[index] = true;
        }
        for (int s = 0; s < Slots; s++)
        {
            int index = Catalog.IndexOf(data.Worn![s] ?? "");
            _worn[s] = index >= 0 && _owned[index] && (int)Catalog.Items[index].Slot == s ? index : -1;
        }
        TarantulaRewarded = Owns(Catalog.SpiderSilkScarf);
        LadybugRewarded = Owns(Catalog.LadybugBackpack);
        _outfitChosen = true;
        Close();
        _buyQueued = -1;
        _buyPending = -1;
        LastBuy = BuyResult.None;
        LastBuyItem = -1;
        ReceivedCount++;
        return true;
    }

    /// <summary>Reads <paramref name="json"/>; returns why it cannot be restored, or null.</summary>
    private static string? Parse(string? json, out SaveData? data)
    {
        data = null;
        if (string.IsNullOrWhiteSpace(json))
            return "no data";
        try
        {
            data = JsonSerializer.Deserialize<SaveData>(json, JsonOptions);
        }
        catch (JsonException e)
        {
            return "bad JSON: " + e.Message;
        }
        if (data == null)
            return "no data";
        if (data.V != SaveVersion)
            return $"unknown version {data.V}";
        if (data.Look is not { Length: 4 } look || !Looks.Valid(look[0], look[1], look[2], look[3]))
            return "bad look";
        if (data.Owned == null || data.Worn is not { Length: Slots })
            return "bad items";
        return null;
    }

    /// <summary>
    /// The saved part: <c>{"v":1,"look":[skin,hairStyle,hairColor,eyeColor],"owned":[ids],"worn":[6 ids by Slot, "" for none]}</c>.
    /// </summary>
    private sealed record SaveData(int V, int[]? Look, string[]? Owned, string[]? Worn);

    private void SendBuy(int index)
    {
        if (!CanBuy(index))
        {
            LastBuy = BuyResult.None;
            return;
        }
        var item = Catalog.Items[index];
        _requestId++;
        _buyPending = index;
        _bus!.Publish(new SpendCoins(_requestId, item.Price, item.Name));
        // No answer (no wallet): the purchase does not happen.
        if (_buyPending == index)
        {
            _buyPending = -1;
            LastBuy = BuyResult.NotEnough;
        }
    }

    private void OpenScreen(bool shop)
    {
        Open = true;
        Shop = shop;
        // The press that opened the screen went through the Input module: its device is current.
        Device = _busDevice;
        LastBuy = BuyResult.None;
        LastBuyItem = -1;
        _tryOn = -1;
        _drag = 0f;
        _navVertical = 0;
        _navHorizontal = 0;
        Yaw = 0f;
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

    /// <summary>The item shown in <paramref name="slot"/>: the one tried on, else the one worn.</summary>
    private int Shown(Slot slot) => _tryOn >= 0 && Catalog.Items[_tryOn].Slot == slot ? _tryOn : _worn[(int)slot];

    private void Give(int index, string why)
    {
        if (index < 0 || _owned[index])
            return;
        _owned[index] = true;
        ReceivedCount++;
        _bus?.Publish(new ItemReceived(Catalog.Items[index].Id, why));
    }

    private static string Id(int index) => index >= 0 ? Catalog.Items[index].Id : "";
}
