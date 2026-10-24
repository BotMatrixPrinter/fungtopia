using System;
using Fungtopia.Monsters;
using Fungtopia.Player;
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
/// <see cref="SpendCoins"/>. Publishes <see cref="AppearanceState"/> and <see cref="WardrobeState"/>
/// every tick, <see cref="ItemReceived"/> for each new item. No allocations per tick.
/// </summary>
public sealed class WardrobeModule : IModule, IMessageHandler<SetLook>, IMessageHandler<Equip>,
    IMessageHandler<GrantItem>, IMessageHandler<OpenWardrobe>, IMessageHandler<OpenShop>,
    IMessageHandler<MonsterKilled>, IMessageHandler<PlayerState>, IMessageHandler<WalletState>,
    IMessageHandler<SpendResult>
{
    /// <summary>The <see cref="SpendCoins.RequestId"/>s of the shop start here (other shops use their own).</summary>
    public const int RequestIdBase = 0x5744_0000;

    public const string ShopReason = "Abby's shop";
    public const string TarantulaReason = "First Mushroom Tarantula";
    public const string LadybugReason = "First Mushroom Ladybug";

    private const int Slots = 6;

    private readonly bool[] _owned = new bool[Catalog.Count];
    private readonly int[] _worn = new int[Slots];
    private IMessageBus? _bus;
    private bool _outfitChosen;
    private bool _characterKnown;
    private int _buyQueued = -1;
    private int _buyPending = -1;
    private int _requestId = RequestIdBase;

    public WardrobeModule()
    {
        Array.Fill(_worn, -1);
        for (int i = 0; i < _owned.Length; i++)
            _owned[i] = Catalog.Items[i].Source == Sources.Starter;
        WearOutfit(Catalog.DefaultOutfit(0));
        _outfitChosen = false;
    }

    public int Skin { get; private set; }
    public int HairStyle { get; private set; }
    public int HairColor { get; private set; }
    public int EyeColor { get; private set; }

    /// <summary>0 Girl, 1 Boy (from the latest <see cref="PlayerState"/>; Girl before it).</summary>
    public int Character { get; private set; }

    /// <summary>The latest <see cref="WalletState.Coins"/>, for the shop.</summary>
    public int Coins { get; private set; }

    public bool Open { get; private set; }
    public bool Shop { get; private set; }

    public bool TarantulaRewarded { get; private set; }
    public bool LadybugRewarded { get; private set; }

    /// <summary>The latest purchase and its item (an index in <see cref="Catalog.Items"/>, or -1).</summary>
    public BuyResult LastBuy { get; private set; }
    public int LastBuyItem { get; private set; } = -1;

    /// <summary>Items received since the start (bought or granted), for the view.</summary>
    public int ReceivedCount { get; private set; }

    public long Ticks { get; private set; }

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

    /// <summary>What the player looks like now (the <see cref="AppearanceState"/> published each tick).</summary>
    public AppearanceState Appearance => new(Skin, HairStyle, HairColor, EyeColor,
        Id(Slot.Top), BottomHidden ? "" : Id(Slot.Bottom), Id(Slot.Shoes), Id(Slot.Head), Id(Slot.Face), Id(Slot.Back));

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
    }

    public void Handle(in WalletState message) => Coins = message.Coins;

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
        }
        else
        {
            LastBuy = BuyResult.NotEnough;
        }
    }

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
        LastBuy = BuyResult.None;
        LastBuyItem = -1;
    }

    private void Give(int index, string why)
    {
        if (index < 0 || _owned[index])
            return;
        _owned[index] = true;
        ReceivedCount++;
        _bus?.Publish(new ItemReceived(Catalog.Items[index].Id, why));
    }

    private string Id(Slot slot) => _worn[(int)slot] is var i && i >= 0 ? Catalog.Items[i].Id : "";
}
